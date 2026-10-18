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
        public float TintR = 1.06f, TintG = 0.94f, TintB = 0.86f; // warm sunset grade (1,1,1 = neutral)
        public float Saturation = 1.12f;       // 1 = unchanged

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
