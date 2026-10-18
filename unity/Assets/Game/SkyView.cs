using UnityEngine;
using UnityEngine.Rendering;

namespace Rats.Game
{
    /// <summary>
    /// Photographic sky from an HDRI (Resources/PolyHaven/Sky/sky.exr): skybox, ambient light and reflections
    /// taken from the image, and the sun pointed at the brightest spot in it with matching color. Falls back to
    /// the painted dome (returns false) if the file is missing or SkyHdri is off. Visual only.
    /// </summary>
    static class SkyView
    {
        const string SkyResource = "PolyHaven/Sky/sky";

        public static bool TrySetup(Transform world, VisualSettings look, Light sun)
        {
            if (!look.SkyHdri) return false;
            var hdri = Resources.Load<Texture2D>(SkyResource);
            if (hdri == null) return false;

            var sky = new Material(Shader.Find("Skybox/Panoramic"));
            sky.SetTexture("_MainTex", hdri);
            sky.SetFloat("_Exposure", look.SkyExposure);
            sky.SetFloat("_Rotation", look.SkyRotation);
            RenderSettings.skybox = sky;

            // Sun: brightest spot in a small mip of the HDRI, turned by the same rotation as the skybox.
            // The light comes from the sun's compass direction but never lower than SunMinElevation, so a sun
            // sitting on the horizon doesn't put the whole pit in wall shadow.
            if (TryFindSun(hdri, look.SkyRotation, out var toSun, out var sunColor, out var horizon))
            {
                float azimuth = Mathf.Atan2(toSun.x, toSun.z) * Mathf.Rad2Deg;
                float elevation = Mathf.Max(Mathf.Asin(toSun.y) * Mathf.Rad2Deg, look.SunMinElevation);
                sun.transform.rotation = Quaternion.Euler(elevation, azimuth + 180f, 0f);
                sun.color = sunColor.gamma;                          // the HDRI is linear; light and fog colors are not
                RenderSettings.fogColor = (horizon * look.SkyExposure).gamma;
                Debug.Log($"[SkyView] sun in the sky at {Mathf.Asin(toSun.y) * Mathf.Rad2Deg:0.0} deg up, compass {azimuth:0} deg; light at {elevation:0.0} deg");
            }

            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = look.SkyAmbient;
            DynamicGI.UpdateEnvironment();

            // Reflections of the sky on shiny surfaces (iron, spikes, later armor and blades).
            QualitySettings.realtimeReflectionProbes = true;
            var probe = new GameObject("SkyReflections").AddComponent<ReflectionProbe>();
            probe.transform.SetParent(world, false);
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.cullingMask = 0;                                   // the sky only
            probe.clearFlags = ReflectionProbeClearFlags.Skybox;
            probe.size = new Vector3(500f, 200f, 500f);
            probe.resolution = 128;
            probe.RenderProbe();
            return true;
        }

        /// <summary>
        /// Finds the brightest pixel (the sun) and the average horizon color. Mapping matches Unity's
        /// Skybox/Panoramic shader (latitude-longitude layout, rotated around Y by _Rotation degrees).
        /// </summary>
        static bool TryFindSun(Texture2D hdri, float rotationDegrees, out Vector3 toSun, out Color sunColor, out Color horizon)
        {
            toSun = Vector3.up; sunColor = Color.white; horizon = Color.gray;
            try
            {
                int mip = Mathf.Max(0, hdri.mipmapCount - 7);          // ~64 x 32
                int w = Mathf.Max(1, hdri.width >> mip), h = Mathf.Max(1, hdri.height >> mip);
                var px = hdri.GetPixels(mip);
                int best = 0;
                float bestLum = -1f;
                for (int i = 0; i < px.Length; i++)
                {
                    float lum = px[i].r * 0.3f + px[i].g * 0.59f + px[i].b * 0.11f;
                    if (lum > bestLum) { bestLum = lum; best = i; }
                }
                float u = (best % w + 0.5f) / w, v = (best / w + 0.5f) / h;
                toSun = DirectionFor(u, v, rotationDegrees);
                var c = px[best];
                float peak = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                sunColor = peak > 0f ? new Color(c.r / peak, c.g / peak, c.b / peak) : Color.white;

                // Horizon: average of the row just above the middle, squeezed into a fog-friendly range.
                int row = Mathf.Clamp(h / 2, 0, h - 1);
                var sum = Color.black;
                for (int x = 0; x < w; x++) sum += px[row * w + x];
                sum /= w;
                float m = Mathf.Max(sum.r, Mathf.Max(sum.g, sum.b));
                horizon = m > 1f ? sum / m : sum;
                horizon.a = 1f;
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[SkyView] couldn't read the HDRI to place the sun ({e.Message}); using the default sun.");
                return false;
            }
        }

        /// <summary>World direction of texture coordinate (u, v) on the panoramic sky.</summary>
        static Vector3 DirectionFor(float u, float v, float rotationDegrees)
        {
            float longitude = (0.5f - u) * 2f * Mathf.PI;
            float polar = (1f - v) * Mathf.PI;                      // 0 at the zenith
            var dir = new Vector3(Mathf.Sin(polar) * Mathf.Cos(longitude), Mathf.Cos(polar), Mathf.Sin(polar) * Mathf.Sin(longitude));
            // The shader samples the texture at the view direction rotated by +rotation; undo that.
            float a = -rotationDegrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(a) * dir.x - Mathf.Sin(a) * dir.z, dir.y, Mathf.Sin(a) * dir.x + Mathf.Cos(a) * dir.z).normalized;
        }
    }
}
