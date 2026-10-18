using Rats.Sim;
using UnityEngine;

namespace Rats.Game
{
    /// <summary>
    /// Builds the arena's look in code: generated sand/stone textures, dusk sky, low walls with caps,
    /// pillars, tiered stands with a crowd, torches and banners. Purely visual; collisions live in the sim.
    /// </summary>
    static class ArenaView
    {
        const int WallSegments = 72;
        const float WallHeight = 0.8f, WallThickness = 0.5f, PillarHeight = 4f;
        static readonly Color SkyHorizon = new Color(0.62f, 0.42f, 0.30f);
        static readonly Color SkyZenith = new Color(0.16f, 0.14f, 0.24f);

        public static void Build(Transform world, Arena arena, VisualSettings look)
        {
            var sand = Lit(ProcTex.Sand(new Color(0.74f, 0.61f, 0.42f), new Color(0.6f, 0.47f, 0.31f), 11), new Vector2(7f, 5f));
            var dirt = Lit(ProcTex.Sand(new Color(0.30f, 0.25f, 0.21f), new Color(0.22f, 0.18f, 0.15f), 12), new Vector2(40f, 40f));
            var wallStone = Lit(ProcTex.Blocks(new Color(0.47f, 0.43f, 0.38f), 2, 3, 21), new Vector2(1f, 1f));
            var capStone = Lit(ProcTex.Blocks(new Color(0.58f, 0.54f, 0.48f), 1, 2, 22), new Vector2(1f, 1f));
            var pillarStone = Lit(ProcTex.Blocks(new Color(0.52f, 0.48f, 0.42f), 6, 4, 23), new Vector2(2f, 1.5f));
            var standStone = Lit(ProcTex.Blocks(new Color(0.30f, 0.27f, 0.24f), 2, 4, 24), new Vector2(4f, 1f));

            Lighting(world, look);

            var outside = Primitives.Create(PrimitiveType.Plane, world, dirt);
            outside.name = "Outside";
            outside.transform.localPosition = new Vector3(0f, -0.02f, 0f);
            outside.transform.localScale = new Vector3(20f, 1f, 20f);

            var floor = Primitives.Create(PrimitiveType.Cylinder, world, sand);
            floor.name = "Sand";
            floor.transform.localScale = new Vector3((float)arena.RadiusX * 2f, 0.01f, (float)arena.RadiusZ * 2f);

            for (int i = 0; i < WallSegments; i++)
            {
                var a = EllipsePoint(arena, i, WallThickness * 0.5f);
                var b = EllipsePoint(arena, i + 1, WallThickness * 0.5f);
                var rot = Quaternion.LookRotation(b - a, Vector3.up);
                float len = Vector3.Distance(a, b) + 0.05f;
                var wall = Primitives.Create(PrimitiveType.Cube, world, wallStone);
                wall.name = "Wall";
                wall.transform.localPosition = (a + b) * 0.5f + Vector3.up * (WallHeight * 0.5f);
                wall.transform.localRotation = rot;
                wall.transform.localScale = new Vector3(WallThickness, WallHeight, len);
                var cap = Primitives.Create(PrimitiveType.Cube, world, capStone);
                cap.name = "WallCap";
                cap.transform.localPosition = (a + b) * 0.5f + Vector3.up * (WallHeight + 0.06f);
                cap.transform.localRotation = rot;
                cap.transform.localScale = new Vector3(WallThickness + 0.18f, 0.12f, len);
            }

            foreach (var c in arena.Pillars)
            {
                var pos = SimConvert.ToUnity(c);
                float r = (float)arena.PillarRadius;
                Part(PrimitiveType.Cylinder, world, pillarStone, "Pillar", pos + Vector3.up * (PillarHeight * 0.5f), new Vector3(r * 2f, PillarHeight * 0.5f, r * 2f));
                Part(PrimitiveType.Cylinder, world, capStone, "PillarBase", pos + Vector3.up * 0.15f, new Vector3(r * 2.4f, 0.15f, r * 2.4f));
                Part(PrimitiveType.Cylinder, world, capStone, "PillarCap", pos + Vector3.up * (PillarHeight + 0.12f), new Vector3(r * 2.5f, 0.12f, r * 2.5f));
            }

            Stands(world, arena, standStone, look.CrowdSize);
            Torches(world, arena, capStone);
        }

        static void Lighting(Transform world, VisualSettings look)
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(world, false);
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.intensity = look.SunIntensity;
            sun.color = new Color(1f, 0.82f, 0.62f);
            sun.transform.rotation = Quaternion.Euler(32f, -40f, 0f); // low dusk sun, long shadows

            SkyDome(world);
            RenderSettings.skybox = null;
            RenderSettings.sun = sun;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.38f, 0.40f);
            RenderSettings.ambientEquatorColor = new Color(0.36f, 0.30f, 0.26f);
            RenderSettings.ambientGroundColor = new Color(0.18f, 0.15f, 0.13f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = SkyHorizon;
            RenderSettings.fogStartDistance = look.FogStart;
            RenderSettings.fogEndDistance = look.FogEnd;
        }

        /// <summary>Three tiers of stone stands outside the wall, with a crowd of simple silhouettes.</summary>
        static void Stands(Transform world, Arena arena, Material stone, int crowd)
        {
            const int segments = 48;
            for (int tier = 0; tier < 3; tier++)
            {
                float outset = 1.6f + tier * 1.6f, height = 0.7f + tier * 0.9f;
                for (int i = 0; i < segments; i++)
                {
                    var a = EllipsePoint(arena, i, outset, segments);
                    var b = EllipsePoint(arena, i + 1, outset, segments);
                    var step = Primitives.Create(PrimitiveType.Cube, world, stone);
                    step.name = "Stand";
                    step.transform.localPosition = (a + b) * 0.5f + Vector3.up * (height * 0.5f);
                    step.transform.localRotation = Quaternion.LookRotation(b - a, Vector3.up);
                    step.transform.localScale = new Vector3(1.6f, height, Vector3.Distance(a, b) + 0.1f);
                }
            }

            var rng = new System.Random(5);
            var cloth = new[]
            {
                new Color(0.35f, 0.18f, 0.14f), new Color(0.42f, 0.36f, 0.26f), new Color(0.24f, 0.22f, 0.2f),
                new Color(0.3f, 0.26f, 0.32f), new Color(0.46f, 0.3f, 0.18f),
            };
            var mats = new Material[cloth.Length];
            for (int i = 0; i < cloth.Length; i++) mats[i] = Lit(null, Vector2.one, cloth[i]);
            for (int i = 0; i < crowd; i++)
            {
                int tier = rng.Next(3);
                float angle = (float)(rng.NextDouble() * Mathf.PI * 2);
                float outset = 1.6f + tier * 1.6f, height = 0.7f + tier * 0.9f;
                var p = new Vector3(Mathf.Cos(angle) * ((float)arena.RadiusX + outset), height, Mathf.Sin(angle) * ((float)arena.RadiusZ + outset));
                var person = Primitives.Create(PrimitiveType.Capsule, world, mats[rng.Next(mats.Length)]);
                person.name = "Spectator";
                person.transform.localPosition = p + Vector3.up * 0.45f;
                person.transform.localScale = new Vector3(0.38f, 0.45f, 0.38f);
            }
        }

        static void Torches(Transform world, Arena arena, Material stone)
        {
            var bannerColors = new[] { new Color(0.45f, 0.08f, 0.06f), new Color(0.55f, 0.38f, 0.1f) };
            const int count = 8;
            for (int i = 0; i < count; i++)
            {
                int seg = i * WallSegments / count + WallSegments / (count * 2);
                var p = EllipsePoint(arena, seg, WallThickness * 0.5f);
                Part(PrimitiveType.Cylinder, world, stone, "TorchPole", p + Vector3.up * 1.4f, new Vector3(0.12f, 0.6f, 0.12f));
                var flame = Primitives.Create(PrimitiveType.Sphere, world, Unlit(new Color(1f, 0.62f, 0.2f)));
                flame.name = "TorchFlame";
                flame.transform.localPosition = p + Vector3.up * 2.1f;
                flame.transform.localScale = Vector3.one * 0.22f;
                var light = new GameObject("TorchLight").AddComponent<Light>();
                light.transform.SetParent(world, false);
                light.transform.localPosition = p + Vector3.up * 2.2f;
                light.type = LightType.Point;
                light.color = new Color(1f, 0.6f, 0.25f);
                light.range = 7f;
                light.intensity = 1.6f;
                light.gameObject.AddComponent<Flicker>().Seed = i * 13.7f;

                // Banner hanging inside the wall, halfway to the next torch.
                int bannerSeg = seg + WallSegments / (count * 2);
                var bp = EllipsePoint(arena, bannerSeg, -0.02f);
                var banner = Primitives.Create(PrimitiveType.Quad, world, Lit(null, Vector2.one, bannerColors[i % 2]));
                banner.name = "Banner";
                banner.transform.localPosition = bp + Vector3.up * 0.95f;
                banner.transform.localRotation = Quaternion.LookRotation(bp.normalized, Vector3.up); // faces the center
                banner.transform.localScale = new Vector3(0.7f, 1.1f, 1f);
            }
        }

        /// <summary>Painted dusk: a big inverted dome with vertex colors from warm horizon to dark zenith (unlit, unfogged).</summary>
        static void SkyDome(Transform world)
        {
            const int rings = 12, sides = 32;
            const float radius = 400f;
            var verts = new System.Collections.Generic.List<Vector3>();
            var colors = new System.Collections.Generic.List<Color>();
            var tris = new System.Collections.Generic.List<int>();
            for (int r = 0; r <= rings; r++)
            {
                float v = r / (float)rings;                 // 0 = below horizon, 1 = zenith
                float elevation = Mathf.Lerp(-0.25f, 1f, v) * Mathf.PI * 0.5f;
                var color = v < 0.2f ? Color.Lerp(SkyHorizon * 0.6f, SkyHorizon, v / 0.2f) : Color.Lerp(SkyHorizon, SkyZenith, Mathf.Pow((v - 0.2f) / 0.8f, 0.6f));
                for (int s = 0; s <= sides; s++)
                {
                    float a = s / (float)sides * Mathf.PI * 2f;
                    verts.Add(new Vector3(Mathf.Cos(a) * Mathf.Cos(elevation), Mathf.Sin(elevation), Mathf.Sin(a) * Mathf.Cos(elevation)) * radius);
                    colors.Add(color);
                }
            }
            for (int r = 0; r < rings; r++)
            for (int s = 0; s < sides; s++)
            {
                int i = r * (sides + 1) + s, up = i + sides + 1;
                tris.AddRange(new[] { i, i + 1, up, i + 1, up + 1, up }); // wound to face inward
            }
            var mesh = new Mesh { name = "SkyDome" };
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            var go = new GameObject("SkyDome");
            go.transform.SetParent(world, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static void Part(PrimitiveType type, Transform world, Material mat, string name, Vector3 pos, Vector3 scale)
        {
            var go = Primitives.Create(type, world, mat);
            go.name = name;
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
        }

        static Vector3 EllipsePoint(Arena arena, int i, float outset, int segments = WallSegments)
        {
            float angle = i * Mathf.PI * 2f / segments;
            return new Vector3(Mathf.Cos(angle) * ((float)arena.RadiusX + outset), 0f, Mathf.Sin(angle) * ((float)arena.RadiusZ + outset));
        }

        static Material Lit(Texture2D tex, Vector2 tiling, Color? color = null)
        {
            var m = new Material(Shader.Find("Standard")) { color = color ?? Color.white };
            m.SetFloat("_Glossiness", 0.05f);
            if (tex != null) { m.mainTexture = tex; m.mainTextureScale = tiling; }
            return m;
        }

        /// <summary>Flat bright color (torch flames); Unlit/Color has no keywords for a build to strip.</summary>
        static Material Unlit(Color color) => new Material(Shader.Find("Unlit/Color")) { color = color };
    }

    /// <summary>Torch light flicker (visual only).</summary>
    sealed class Flicker : MonoBehaviour
    {
        public float Seed;
        Light lamp;
        void Awake() => lamp = GetComponent<Light>();
        void Update() => lamp.intensity = 1.3f + Mathf.PerlinNoise(Seed, Time.unscaledTime * 6f) * 0.7f;
    }

    /// <summary>Small generated textures with point filtering, for the chunky PS2-era look.</summary>
    static class ProcTex
    {
        const int Size = 128;

        public static Texture2D Sand(Color light, Color dark, int seed)
        {
            var tex = New("Sand");
            var rng = new System.Random(seed);
            float ox = seed * 17.3f, oy = seed * 9.1f;
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float n = Mathf.PerlinNoise(ox + x * 0.06f, oy + y * 0.06f) * 0.6f + Mathf.PerlinNoise(ox + x * 0.25f, oy + y * 0.25f) * 0.4f;
                var c = Color.Lerp(dark, light, n);
                if (rng.NextDouble() < 0.04) c *= 0.82f;   // grit
                c.a = 1f;
                tex.SetPixel(x, y, c);
            }
            tex.Apply(true);
            return tex;
        }

        /// <summary>Staggered stone blocks with dark mortar and per-block tint.</summary>
        public static Texture2D Blocks(Color stone, int rows, int cols, int seed)
        {
            var tex = New("Stone");
            var rng = new System.Random(seed);
            var tints = new float[rows * (cols + 1)];
            for (int i = 0; i < tints.Length; i++) tints[i] = 0.85f + (float)rng.NextDouble() * 0.25f;
            int rowH = Size / rows, colW = Size / cols;
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                int row = y / rowH;
                int shifted = x + (row % 2) * colW / 2;
                int col = shifted / colW;
                bool mortar = y % rowH < 3 || shifted % colW < 3;
                float n = Mathf.PerlinNoise(seed + x * 0.18f, seed * 2 + y * 0.18f);
                var c = mortar ? stone * 0.45f : stone * tints[row * (cols + 1) + col % (cols + 1)] * (0.85f + n * 0.3f);
                c.a = 1f;
                tex.SetPixel(x, y, c);
            }
            tex.Apply(true);
            return tex;
        }

        static Texture2D New(string name) => new Texture2D(Size, Size, TextureFormat.RGBA32, true)
        {
            name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat,
        };
    }
}
