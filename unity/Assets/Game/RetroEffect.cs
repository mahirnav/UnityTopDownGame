using UnityEngine;

namespace Rats.Game
{
    /// <summary>
    /// Post effect on the (HDR) world camera: soft glow around bright things (bloom), exposure and film-style
    /// ACES tone mapping, the warm grade, and the icy tint while time is frozen. Optionally the PS2 look:
    /// renders down to PixelHeight with point filtering, then color banding + dither. The HUD has its own
    /// camera, so text stays crisp. Knobs live in visuals.txt.
    /// </summary>
    [ExecuteAlways, RequireComponent(typeof(Camera))] // ExecuteAlways: also applies in headless edit-mode captures
    public sealed class RetroEffect : MonoBehaviour
    {
        const int BloomLevels = 5;
        public VisualSettings Look;
        /// <summary>0..1, eased by Bootstrap toward the freeze state.</summary>
        public float Freeze;
        Material material;
        readonly RenderTexture[] levels = new RenderTexture[BloomLevels];

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (Look == null) { Graphics.Blit(src, dst); return; }
            if (material == null) material = new Material(Shader.Find("Hidden/RatsRetro"));

            // Optional PS2 pixelation: shrink first, then everything below works on the small image.
            var main = src;
            RenderTexture low = null;
            if (Look.PixelHeight > 0 && Look.PixelHeight < src.height)
            {
                int h = Look.PixelHeight, w = Mathf.Max(1, Mathf.RoundToInt(src.width * (h / (float)src.height)));
                low = RenderTexture.GetTemporary(w, h, 0, src.format);
                low.filterMode = FilterMode.Point;
                Graphics.Blit(src, low);
                main = low;
            }

            // Bloom: bright parts, blurred down a chain of half-size images and added back up.
            RenderTexture bloom = null;
            if (Look.Bloom > 0f)
            {
                int bw = main.width / 2, bh = main.height / 2;
                material.SetFloat("_Threshold", Look.BloomThreshold);
                int count = 0;
                for (; count < BloomLevels && bw >= 8 && bh >= 8; count++, bw /= 2, bh /= 2)
                {
                    levels[count] = RenderTexture.GetTemporary(bw, bh, 0, RenderTextureFormat.DefaultHDR);
                    levels[count].filterMode = FilterMode.Bilinear;
                    Graphics.Blit(count == 0 ? main : levels[count - 1], levels[count], material, count == 0 ? 0 : 1);
                }
                for (int i = count - 1; i > 0; i--) Graphics.Blit(levels[i], levels[i - 1], material, 2);
                bloom = count > 0 ? levels[0] : null;
                for (int i = 1; i < count; i++) RenderTexture.ReleaseTemporary(levels[i]);
            }

            material.SetTexture("_BloomTex", bloom != null ? (Texture)bloom : Texture2D.blackTexture);
            material.SetFloat("_Bloom", Look.Bloom);
            material.SetFloat("_Exposure", Look.Exposure);
            material.SetFloat("_Tonemap", Look.Tonemap ? 1f : 0f);
            material.SetVector("_LowRes", new Vector4(main.width, main.height, 0, 0));
            material.SetFloat("_Levels", Look.ColorLevels);
            material.SetFloat("_Dither", Look.Dither);
            material.SetFloat("_Freeze", Freeze * Look.FreezeDesaturate);
            material.SetVector("_Tint", new Vector4(Look.TintR, Look.TintG, Look.TintB, 1f));
            material.SetFloat("_Saturation", Look.Saturation);
            Graphics.Blit(main, dst, material, 3);

            if (bloom != null) RenderTexture.ReleaseTemporary(bloom);
            if (low != null) RenderTexture.ReleaseTemporary(low);
        }
    }
}
