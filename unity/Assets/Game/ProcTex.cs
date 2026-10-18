using UnityEngine;

namespace Rats.Game
{
    /// <summary>
    /// Small generated textures with point filtering for the chunky classic-WoW / PS2 look. All tile seamlessly
    /// (noise and cells wrap), so they repeat cleanly across walls and floors. Visual only; seeded so they're stable.
    /// </summary>
    static class ProcTex
    {
        /// <summary>Smooth (trilinear) filtering for the hand-painted WoW feel; false = crisp point sampling. Set before building.</summary>
        public static bool Smooth = true;

        public static Texture2D Sand(Color light, Color dark, int seed, int size = 128)
        {
            var tex = New("Sand", size);
            var rng = new System.Random(seed);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float n = Noise(x, y, size, 8, seed) * 0.6f + Noise(x, y, size, 32, seed + 1) * 0.4f;
                var c = Color.Lerp(dark, light, n);
                if (rng.NextDouble() < 0.04) c *= 0.82f;
                tex.SetPixel(x, y, Opaque(c));
            }
            return Done(tex);
        }

        /// <summary>Staggered stone blocks with dark mortar and per-block tint.</summary>
        public static Texture2D Blocks(Color stone, int rows, int cols, int seed, int size = 128)
        {
            var tex = New("Blocks", size);
            var rng = new System.Random(seed);
            var tints = new float[rows * (cols + 1)];
            for (int i = 0; i < tints.Length; i++) tints[i] = 0.85f + (float)rng.NextDouble() * 0.25f;
            int rowH = size / rows, colW = size / cols;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int row = y / rowH;
                int shifted = (x + (row % 2) * colW / 2) % size;
                int col = shifted / colW;
                bool mortar = y % rowH < 3 || shifted % colW < 3;
                float n = Noise(x, y, size, 16, seed);
                var c = mortar ? stone * 0.45f : stone * tints[row * (cols + 1) + col % (cols + 1)] * (0.85f + n * 0.3f);
                tex.SetPixel(x, y, Opaque(c));
            }
            return Done(tex);
        }

        /// <summary>Weathered tan plaster: blotches, darker grime toward the bottom, a few branching cracks.</summary>
        public static Texture2D Plaster(Color baseColor, int seed, int size = 128)
        {
            var tex = New("Plaster", size);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float n = Noise(x, y, size, 4, seed) * 0.5f + Noise(x, y, size, 16, seed + 1) * 0.35f + Noise(x, y, size, 48, seed + 2) * 0.15f;
                float grime = Mathf.Lerp(0.78f, 1f, y / (float)size);
                px[y * size + x] = Opaque(baseColor * (0.8f + n * 0.35f) * grime);
            }
            var rng = new System.Random(seed);
            for (int crack = 0; crack < 4; crack++)
            {
                float cx = rng.Next(size), cy = rng.Next(size), dir = (float)(rng.NextDouble() * Mathf.PI * 2);
                int length = 20 + rng.Next(30);
                for (int step = 0; step < length; step++)
                {
                    dir += (float)(rng.NextDouble() - 0.5) * 0.9f;
                    cx = (cx + Mathf.Cos(dir) + size) % size;
                    cy = (cy + Mathf.Sin(dir) + size) % size;
                    int i = (int)cy * size + (int)cx;
                    px[i] = Opaque(px[i] * 0.45f);
                }
            }
            tex.SetPixels(px);
            return Done(tex);
        }

        /// <summary>Rounded fieldstones (cellular): blue-grey stones with dark gaps, like the reference's buttresses.</summary>
        public static Texture2D RoundStones(Color stone, int seed, int size = 128) =>
            Cells("RoundStones", stone, size, 6, seed, 0.5f, 0.22f, gapDarkness: 0.3f);

        /// <summary>Small cobbles for the floor: warm brown stones with dark dirt between.</summary>
        public static Texture2D Cobbles(Color stone, int seed, int size = 256) =>
            Cells("Cobbles", stone, size, 22, seed, 0.25f, 0.18f, gapDarkness: 0.6f);

        /// <summary>Vertical-grain weathered wood with grey sun-bleached streaks.</summary>
        public static Texture2D Wood(Color wood, int seed, int size = 128)
        {
            var tex = New("Wood", size);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float grain = Noise(x, y, size, 24, seed, stretchY: 6) * 0.7f + Noise(x, y, size, 64, seed + 1, stretchY: 8) * 0.3f;
                float bleach = Mathf.Clamp01(Noise(x, y, size, 4, seed + 2) * 1.6f - 0.6f);
                var c = wood * (0.65f + grain * 0.6f);
                c = Color.Lerp(c, new Color(0.5f, 0.47f, 0.43f), bleach * 0.5f);
                tex.SetPixel(x, y, Opaque(c));
            }
            return Done(tex);
        }

        /// <summary>Dark iron plate with mottling, an edge band and rivets.</summary>
        public static Texture2D Iron(Color metal, int seed, int size = 128)
        {
            var tex = New("Iron", size);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float n = Noise(x, y, size, 12, seed);
                bool edge = x < 8 || y < 8 || x >= size - 8 || y >= size - 8;
                bool rivet = (x % 32 - 16) * (x % 32 - 16) + (y < 16 ? (y - 4) * (y - 4) : (y - size + 4) * (y - size + 4)) < 6;
                var c = metal * (0.8f + n * 0.4f) * (edge ? 0.75f : 1f);
                if (rivet) c = metal * 1.7f;
                tex.SetPixel(x, y, Opaque(c));
            }
            return Done(tex);
        }

        /// <summary>Large soft ochre patches (alpha), stretched once over the floor so they don't repeat.</summary>
        public static Texture2D Patches(Color ochre, int seed, int size = 256)
        {
            var tex = New("Patches", size);
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float n = Noise(x, y, size, 5, seed) * 0.7f + Noise(x, y, size, 20, seed + 1) * 0.3f;
                float a = Mathf.Clamp01((n - 0.42f) * 2.6f) * 0.75f;
                tex.SetPixel(x, y, new Color(ochre.r, ochre.g, ochre.b, a));
            }
            return Done(tex);
        }

        /// <summary>Directional-light cookie: soft blobs of shade, like light through canopy and banners.</summary>
        public static Texture2D Dapple(int seed, int size = 128)
        {
            var tex = new Texture2D(size, size, TextureFormat.Alpha8, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float n = Noise(x, y, size, 6, seed) * 0.65f + Noise(x, y, size, 18, seed + 1) * 0.35f;
                float light = Mathf.Lerp(0.42f, 1f, Mathf.Clamp01((n - 0.32f) * 3f));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, light));
            }
            tex.Apply(false);
            return tex;
        }

        // --- Building blocks ----------------------------------------------------------------

        /// <summary>Wrapping cellular texture: each cell a stone with its own tint, darker toward the gaps.</summary>
        static Texture2D Cells(string name, Color stone, int size, int cellsAcross, int seed, float gapWidth, float tintSpread, float gapDarkness)
        {
            var tex = New(name, size);
            var rng = new System.Random(seed);
            var sites = new Vector2[cellsAcross * cellsAcross];
            var tints = new float[sites.Length];
            for (int i = 0; i < sites.Length; i++)
            {
                sites[i] = new Vector2((float)rng.NextDouble(), (float)rng.NextDouble());
                tints[i] = 1f - tintSpread * 0.5f + (float)rng.NextDouble() * tintSpread;
            }
            float cell = size / (float)cellsAcross;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int cx = (int)(x / cell), cy = (int)(y / cell);
                float d1 = float.MaxValue, d2 = float.MaxValue;
                int nearest = 0;
                for (int oy = -1; oy <= 1; oy++)
                for (int ox = -1; ox <= 1; ox++)
                {
                    int gx = cx + ox, gy = cy + oy;
                    int wx = (gx % cellsAcross + cellsAcross) % cellsAcross, wy = (gy % cellsAcross + cellsAcross) % cellsAcross;
                    var site = (new Vector2(gx, gy) + sites[wy * cellsAcross + wx]) * cell;
                    float d = Vector2.Distance(site, new Vector2(x, y));
                    if (d < d1) { d2 = d1; d1 = d; nearest = wy * cellsAcross + wx; }
                    else if (d < d2) d2 = d;
                }
                float edge = Mathf.Clamp01((d2 - d1) / (cell * gapWidth));       // 0 at the gap, 1 inside a stone
                float dome = 1f - Mathf.Clamp01(d1 / cell) * 0.35f;               // rounded: lighter toward the middle
                float n = Noise(x, y, size, 32, seed);
                var c = stone * tints[nearest] * dome * (0.9f + n * 0.2f);
                c = Color.Lerp(stone * gapDarkness, c, Mathf.SmoothStep(0f, 1f, edge));
                tex.SetPixel(x, y, Opaque(c));
            }
            return Done(tex);
        }

        /// <summary>
        /// Tileable value noise in 0..1: period cells across the texture (period / stretchY cells down, for grain),
        /// wrapping at the edges.
        /// </summary>
        static float Noise(int x, int y, int size, int period, int seed, int stretchY = 1)
        {
            int px = period, py = Mathf.Max(1, period / stretchY);
            float fx = x * px / (float)size, fy = y * py / (float)size;
            int x0 = (int)fx, y0 = (int)fy;
            float tx = fx - x0, ty = fy - y0;
            tx = tx * tx * (3 - 2 * tx);
            ty = ty * ty * (3 - 2 * ty);
            float a = Hash(x0 % px, y0 % py, seed), b = Hash((x0 + 1) % px, y0 % py, seed);
            float c = Hash(x0 % px, (y0 + 1) % py, seed), d = Hash((x0 + 1) % px, (y0 + 1) % py, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177u;
                return (h ^ (h >> 16)) / (float)uint.MaxValue;
            }
        }

        static Color Opaque(Color c) { c.a = 1f; return c; }

        static Texture2D New(string name, int size) => new Texture2D(size, size, TextureFormat.RGBA32, true)
        {
            name = name, filterMode = Smooth ? FilterMode.Trilinear : FilterMode.Point, wrapMode = TextureWrapMode.Repeat,
        };

        static Texture2D Done(Texture2D tex) { tex.Apply(true); return tex; }
    }
}
