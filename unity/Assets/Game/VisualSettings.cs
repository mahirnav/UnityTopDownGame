using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Rats.Game
{
    /// <summary>
    /// Look-only knobs from Assets/StreamingAssets/visuals.txt ("Name = value  # comment"), reloaded with F5.
    /// Kept apart from tunables.txt, which is gameplay only (and is what the host sends and replays record).
    /// </summary>
    public sealed class VisualSettings
    {
        // Rendering (defaults lean classic WoW; the PS2 values are in the comments)
        public int PixelHeight = 0;            // render height in pixels before upscaling (0 = full resolution; 360 = PS2 look)
        public int ColorLevels = 0;            // color steps per channel (0 = no banding; 24 = PS2 look)
        public float Dither = 0f;              // ordered dither strength (0 = none; 0.6 = PS2 look)
        public bool SmoothTextures = true;     // smooth hand-painted WoW feel (false = crisp point-sampled pixels)
        public float FreezeDesaturate = 0.55f; // how gray/icy the world goes during a freeze (0 = off)
        public float TintR = 1.03f, TintG = 0.98f, TintB = 0.94f; // color grade (1,1,1 = neutral; 1.06/0.94/0.86 suits the painted sky)
        public float Saturation = 1.08f;       // 1 = unchanged

        // Light and sky (HDRI from Resources/PolyHaven/Sky; falls back to the painted dome)
        public bool SkyHdri = true;            // photographic sky + lighting from it (false = painted sunset dome)
        public float SkyExposure = 0.5f;       // sky brightness (also how strongly the sky lights the scene)
        public float SkyRotation = 0f;         // degrees: turns the sky (and the sun with it) around the arena
        public float SkyAmbient = 1f;          // extra multiplier on the sky's light on the scene
        public float SunMinElevation = 25f;    // degrees: sunlight never comes in lower than this (long shadows hide the pit)
        public float Exposure = 1f;            // overall camera exposure
        public bool Tonemap = true;            // film-style ACES tone mapping (false = plain clamp)
        public float Bloom = 0.25f;            // glow around bright things: sun, torches, fire (0 = off)
        public float BloomThreshold = 1.5f;    // brightness where glow starts

        // Surfaces: "polyhaven" uses the downloaded textures, "generated" the code-made ones
        public string FloorStyle = "polyhaven";
        public string WallStyle = "polyhaven";

        // Atmosphere
        public float FogStart = 14f;
        public float FogEnd = 70f;
        public float SunIntensity = 1.3f;
        public int CrowdSize = 260;            // spectator silhouettes around the pit

        // Camera
        public float CameraDistance = 7f;      // behind the fighter; farther feels calmer and shows more of the arena
        public float CameraHeight = 3.2f;

        // Combat effects
        public float ShakeScale = 1f;          // camera shake multiplier (0 = off)
        public bool SwingArcs = true;          // flash the melee reach during active frames

        public string Error { get; private set; }

        public static VisualSettings Load()
        {
            var s = new VisualSettings();
            var path = Path.Combine(Application.streamingAssetsPath, "visuals.txt");
            if (!File.Exists(path)) return s;
            var lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                int hash = line.IndexOf('#');
                if (hash >= 0) line = line.Substring(0, hash);
                line = line.Trim();
                if (line.Length == 0) continue;
                int eq = line.IndexOf('=');
                var field = eq > 0 ? typeof(VisualSettings).GetField(line.Substring(0, eq).Trim(), BindingFlags.Public | BindingFlags.Instance) : null;
                if (field == null) { s.Error = $"visuals.txt line {i + 1}: not understood"; continue; }
                try { field.SetValue(s, Convert.ChangeType(line.Substring(eq + 1).Trim(), field.FieldType, CultureInfo.InvariantCulture)); }
                catch (Exception) { s.Error = $"visuals.txt line {i + 1}: bad value"; }
            }
            return s;
        }
    }
}
