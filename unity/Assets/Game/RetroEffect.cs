using UnityEngine;

namespace Rats.Game
{
    /// <summary>
    /// PS2-style post effect on the world camera: renders down to PixelHeight with point filtering, then
    /// color banding + ordered dither, and an icy desaturation while time is frozen. The HUD has its own
    /// camera, so text stays crisp.
    /// </summary>
    [ExecuteAlways, RequireComponent(typeof(Camera))] // ExecuteAlways: also applies in headless edit-mode captures
    public sealed class RetroEffect : MonoBehaviour
    {
        public VisualSettings Look;
        /// <summary>0..1, eased by Bootstrap toward the freeze state.</summary>
        public float Freeze;
        Material material;

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (Look == null) { Graphics.Blit(src, dst); return; }
            if (material == null) material = new Material(Shader.Find("Hidden/RatsRetro"));

            int h = Look.PixelHeight > 0 ? Mathf.Min(Look.PixelHeight, src.height) : src.height;
            int w = Mathf.Max(1, Mathf.RoundToInt(src.width * (h / (float)src.height)));
            var low = RenderTexture.GetTemporary(w, h, 0, src.format);
            low.filterMode = FilterMode.Point;
            Graphics.Blit(src, low);
            material.SetVector("_LowRes", new Vector4(w, h, 0, 0));
            material.SetFloat("_Levels", Look.ColorLevels);
            material.SetFloat("_Dither", Look.Dither);
            material.SetFloat("_Freeze", Freeze * Look.FreezeDesaturate);
            material.SetVector("_Tint", new Vector4(Look.TintR, Look.TintG, Look.TintB, 1f));
            material.SetFloat("_Saturation", Look.Saturation);
            Graphics.Blit(low, dst, material);
            RenderTexture.ReleaseTemporary(low);
        }
    }
}
