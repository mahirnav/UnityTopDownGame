using System.Collections.Generic;
using Rats.Sim;
using UnityEngine;

namespace Rats.Game
{
    /// <summary>
    /// Builds the arena's look in code, after Nick's references (classic-WoW gladiator pit at sunset):
    /// tall cracked plaster walls with stone trim and buttresses, an arched gate, wooden posts on spiked iron
    /// bases with chains, a cobbled dirt floor with ochre patches and dappled light, stands with a crowd above
    /// the wall, banners, torches, mountains against a red-orange sky. Purely visual; collisions live in the sim.
    /// </summary>
    static class ArenaView
    {
        const int WallSegments = 96;
        const float WallHeight = 3.5f, WallThickness = 0.6f, PostHeight = 6f;
        static readonly Color SkyHorizon = new Color(0.96f, 0.52f, 0.26f);
        static readonly Color SkyMid = new Color(0.86f, 0.34f, 0.24f);
        static readonly Color SkyZenith = new Color(0.42f, 0.16f, 0.22f);
        static readonly Color Haze = new Color(0.86f, 0.44f, 0.27f);

        public static void Build(Transform world, Arena arena, VisualSettings look)
        {
            ProcTex.Smooth = look.SmoothTextures;
            var cobbles = Lit(ProcTex.Cobbles(new Color(0.66f, 0.50f, 0.33f), 31), new Vector2((float)arena.RadiusX * 0.8f, (float)arena.RadiusZ * 0.8f)); // same cobble size at any arena size
            var dirt = Lit(ProcTex.Sand(new Color(0.34f, 0.24f, 0.18f), new Color(0.25f, 0.17f, 0.13f), 12), new Vector2(40f, 40f));
            var plaster = Lit(ProcTex.Plaster(new Color(0.66f, 0.55f, 0.42f), 41), new Vector2(1f, 2.5f));

            // Downloaded Poly Haven surfaces (visuals.txt FloorStyle / WallStyle); generated ones if missing.
            bool sandFloor = false;
            if (look.FloorStyle == "polyhaven")
            {
                var sand = Scanned("Sand/sand", new Vector2((float)arena.RadiusX * 0.6f, (float)arena.RadiusZ * 0.6f));
                if (sand != null)
                {
                    cobbles = sand;
                    dirt = Scanned("Sand/sand", new Vector2(60f, 60f), new Color(0.62f, 0.55f, 0.5f));
                    sandFloor = true;
                }
            }
            if (look.WallStyle == "polyhaven") plaster = Scanned("RockWall/rock", new Vector2(1f, 2.5f)) ?? plaster;
            var trim = Lit(ProcTex.Blocks(new Color(0.34f, 0.33f, 0.34f), 1, 2, 22), Vector2.one);
            var roundStone = Lit(ProcTex.RoundStones(new Color(0.36f, 0.39f, 0.44f), 51), new Vector2(2f, 3f));
            var wood = Lit(ProcTex.Wood(new Color(0.30f, 0.24f, 0.19f), 61), new Vector2(2f, 2f));
            var iron = Lit(ProcTex.Iron(new Color(0.22f, 0.24f, 0.28f), 71), Vector2.one);
            var standStone = Lit(ProcTex.Blocks(new Color(0.30f, 0.25f, 0.22f), 2, 4, 24), new Vector2(4f, 1f));

            bool photoSky = Lighting(world, look);

            Part(PrimitiveType.Plane, world, dirt, "Outside", new Vector3(0f, -0.02f, 0f), new Vector3(30f, 1f, 30f));
            Part(PrimitiveType.Cylinder, world, cobbles, "Floor", Vector3.zero, new Vector3((float)arena.RadiusX * 2f, 0.01f, (float)arena.RadiusZ * 2f));
            if (!sandFloor) Patches(world, arena);

            Walls(world, arena, plaster, trim);
            var buttressAt = new int[8];
            for (int k = 0; k < buttressAt.Length; k++) buttressAt[k] = k * WallSegments / buttressAt.Length + WallSegments / 16; // evenly spaced, clear of the gate
            foreach (int seg in buttressAt) Buttress(world, arena, seg, roundStone, trim);
            Gate(world, arena, roundStone, iron);
            Banners(world, arena);
            Torches(world, arena, buttressAt);

            foreach (var c in arena.Pillars) Post(world, SimConvert.ToUnity(c), (float)arena.PillarRadius, wood, iron, arena);

            Stands(world, arena, standStone, look.CrowdSize);
            if (!photoSky) Mountains(world);
        }

        // --- Light and sky ------------------------------------------------------------------

        /// <summary>Sun, sky, ambient and fog. True when the photographic HDRI sky is in use.</summary>
        static bool Lighting(Transform world, VisualSettings look)
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(world, false);
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.intensity = look.SunIntensity;
            sun.color = new Color(1f, 0.6f, 0.36f);
            sun.transform.rotation = Quaternion.Euler(24f, -150f, 0f); // low sunset sun from across the pit
            sun.cookie = ProcTex.Dapple(81);                          // dappled shade, as if through canopy and banners
            sun.cookieSize = 18f;

            RenderSettings.sun = sun;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Haze;
            RenderSettings.fogStartDistance = look.FogStart;
            RenderSettings.fogEndDistance = look.FogEnd;
            if (SkyView.TrySetup(world, look, sun)) return true;

            SkyDome(world);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.38f, 0.34f);
            RenderSettings.ambientEquatorColor = new Color(0.52f, 0.32f, 0.24f);
            RenderSettings.ambientGroundColor = new Color(0.24f, 0.15f, 0.12f);
            return false;
        }

        /// <summary>Painted sunset: inverted dome with vertex colors, hot horizon to wine-red zenith (unlit, unfogged).</summary>
        static void SkyDome(Transform world)
        {
            const int rings = 14, sides = 32;
            const float radius = 400f;
            var verts = new List<Vector3>();
            var colors = new List<Color>();
            var tris = new List<int>();
            for (int r = 0; r <= rings; r++)
            {
                float v = r / (float)rings;                       // 0 = below horizon, 1 = zenith
                float elevation = Mathf.Lerp(-0.2f, 1f, v) * Mathf.PI * 0.5f;
                var color = v < 0.17f ? Color.Lerp(Haze * 0.8f, SkyHorizon, v / 0.17f)
                          : v < 0.45f ? Color.Lerp(SkyHorizon, SkyMid, (v - 0.17f) / 0.28f)
                          : Color.Lerp(SkyMid, SkyZenith, (v - 0.45f) / 0.55f);
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
                tris.AddRange(new[] { i, i + 1, up, i + 1, up + 1, up });
            }
            Unshaded(world, "SkyDome", verts, colors, tris);
        }

        /// <summary>Jagged dark ridges on the horizon, like the reference's silhouetted mountains.</summary>
        static void Mountains(Transform world)
        {
            const int peaks = 220;
            var rng = new System.Random(9);
            var verts = new List<Vector3>();
            var colors = new List<Color>();
            var tris = new List<int>();
            var foot = new Color(0.30f, 0.12f, 0.13f);
            var top = new Color(0.40f, 0.17f, 0.17f);
            for (int i = 0; i <= peaks; i++)
            {
                float a = i / (float)peaks * Mathf.PI * 2f;
                float radius = 230f + (float)rng.NextDouble() * 40f;
                float height = 6f + (float)(rng.NextDouble() * rng.NextDouble()) * 26f + Mathf.PerlinNoise(i * 0.08f, 3f) * 14f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                verts.Add(dir * radius + Vector3.down * 5f);
                verts.Add(dir * radius + Vector3.up * height);
                colors.Add(foot);
                colors.Add(top);
                if (i < peaks)
                {
                    int k = i * 2;
                    tris.AddRange(new[] { k, k + 1, k + 2, k + 1, k + 3, k + 2 });
                }
            }
            Unshaded(world, "Mountains", verts, colors, tris);
        }

        // --- Floor ------------------------------------------------------------------------

        /// <summary>Big ochre patches over the cobbles (one stretched texture, so they don't visibly tile).</summary>
        static void Patches(Transform world, Arena arena)
        {
            var mat = new Material(Shader.Find("Sprites/Default")) { mainTexture = ProcTex.Patches(new Color(0.74f, 0.6f, 0.22f), 91) };
            var go = Part(PrimitiveType.Quad, world, mat, "OchrePatches", new Vector3(0f, 0.012f, 0f),
                new Vector3((float)arena.RadiusX * 2f, (float)arena.RadiusZ * 2f, 1f));
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.GetComponent<Renderer>().receiveShadows = false;
        }

        // --- Walls ------------------------------------------------------------------------

        static void Walls(Transform world, Arena arena, Material plaster, Material trim)
        {
            for (int i = 0; i < WallSegments; i++)
            {
                var a = EllipsePoint(arena, i, WallThickness * 0.5f);
                var b = EllipsePoint(arena, i + 1, WallThickness * 0.5f);
                var rot = Quaternion.LookRotation(b - a, Vector3.up);
                var mid = (a + b) * 0.5f;
                float len = Vector3.Distance(a, b) + 0.06f;
                Segment(world, plaster, "Wall", mid + Vector3.up * (WallHeight * 0.5f), rot, new Vector3(WallThickness, WallHeight, len));
                Segment(world, trim, "WallBase", mid + Vector3.up * 0.3f, rot, new Vector3(WallThickness + 0.25f, 0.6f, len));
                Segment(world, trim, "WallCap", mid + Vector3.up * (WallHeight + 0.1f), rot, new Vector3(WallThickness + 0.3f, 0.2f, len));
            }
        }

        static void Buttress(Transform world, Arena arena, int seg, Material stone, Material trim)
        {
            var p = EllipsePoint(arena, seg, 0.05f);
            Part(PrimitiveType.Cylinder, world, stone, "Buttress", p + Vector3.up * (WallHeight * 0.6f), new Vector3(1.5f, WallHeight * 0.6f, 1.5f));
            Part(PrimitiveType.Cylinder, world, trim, "ButtressCap", p + Vector3.up * (WallHeight * 1.2f + 0.1f), new Vector3(1.7f, 0.12f, 1.7f));
        }

        /// <summary>Arched stone gate in the far wall, with two glowing eyes either side.</summary>
        static void Gate(Transform world, Arena arena, Material stone, Material iron)
        {
            int seg = WallSegments / 4;                                  // +Z end of the oval
            var p = EllipsePoint(arena, seg, -0.05f);
            var inward = -p.normalized;
            var side = Vector3.Cross(Vector3.up, inward).normalized;
            var rot = Quaternion.LookRotation(inward, Vector3.up);
            const float halfWidth = 1.4f, sideHeight = 2.2f;

            var door = Part(PrimitiveType.Cube, world, iron, "GateDoor", p + Vector3.up * 1.6f - inward * 0.05f, new Vector3(halfWidth * 2f, 3.2f, 0.2f));
            door.transform.localRotation = rot;
            for (int s = -1; s <= 1; s += 2)
            {
                var col = Part(PrimitiveType.Cube, world, stone, "GateSide", p + side * (halfWidth + 0.3f) * s + Vector3.up * (sideHeight * 0.5f) + inward * 0.15f, new Vector3(0.6f, sideHeight, 0.7f));
                col.transform.localRotation = rot;
                var eye = Part(PrimitiveType.Sphere, world, Unlit(new Color(1f, 0.25f, 0.08f)), "GateEye", p + side * (halfWidth + 0.15f) * s + Vector3.up * 2.4f + inward * 0.5f, Vector3.one * 0.14f);
                eye.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            const int stones = 9;
            for (int k = 0; k < stones; k++)
            {
                float a = Mathf.PI * (k + 0.5f) / stones;
                var offset = side * Mathf.Cos(a) * (halfWidth + 0.3f) + Vector3.up * (sideHeight + Mathf.Sin(a) * (halfWidth + 0.3f));
                var block = Part(PrimitiveType.Cube, world, stone, "GateArch", p + offset + inward * 0.15f, new Vector3(0.62f, 0.55f, 0.7f));
                block.transform.localRotation = rot * Quaternion.Euler(0f, 0f, (a - Mathf.PI * 0.5f) * Mathf.Rad2Deg);
            }
        }

        static void Banners(Transform world, Arena arena)
        {
            var cloth = new[] { Lit(null, Vector2.one, new Color(0.86f, 0.38f, 0.12f)), Lit(null, Vector2.one, new Color(0.5f, 0.1f, 0.07f)) };
            var at = new int[6];
            for (int k = 0; k < at.Length; k++) at[k] = (k < 3 ? k + 1 : k + 2) * WallSegments / 8; // between buttresses, skipping the gate
            for (int i = 0; i < at.Length; i++)
            {
                var p = EllipsePoint(arena, at[i], -0.03f);
                var banner = Part(PrimitiveType.Quad, world, cloth[i % 2], "Banner", p + Vector3.up * 2.3f, new Vector3(1.3f, 2.2f, 1f));
                banner.transform.localRotation = Quaternion.LookRotation(p.normalized, Vector3.up); // faces the center
            }
        }

        static void Torches(Transform world, Arena arena, int[] buttressAt)
        {
            for (int i = 0; i < buttressAt.Length; i++)
            {
                var p = EllipsePoint(arena, buttressAt[i], -0.75f);
                var flame = Part(PrimitiveType.Sphere, world, Unlit(new Color(1f, 0.62f, 0.2f)), "TorchFlame", p + Vector3.up * 3.0f, Vector3.one * 0.26f);
                flame.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var light = new GameObject("TorchLight").AddComponent<Light>();
                light.transform.SetParent(world, false);
                light.transform.localPosition = p + Vector3.up * 3.1f;
                light.type = LightType.Point;
                light.color = new Color(1f, 0.55f, 0.22f);
                light.range = 7f;
                light.intensity = 1.6f;
                light.gameObject.AddComponent<Flicker>().Seed = i * 13.7f;
            }
        }

        // --- Posts --------------------------------------------------------------------------

        /// <summary>
        /// A cover pillar dressed as a thick weathered wooden post on a spiked iron base block, with a chain
        /// sagging to the wall. The base matches the sim's collision radius.
        /// </summary>
        static void Post(Transform world, Vector3 at, float collisionRadius, Material wood, Material iron, Arena arena)
        {
            float baseHalf = collisionRadius * 0.95f;
            Part(PrimitiveType.Cube, world, iron, "PostBase", at + Vector3.up * 0.45f, new Vector3(baseHalf * 2f, 0.9f, baseHalf * 2f));
            Part(PrimitiveType.Cube, world, iron, "PostBaseLip", at + Vector3.up * 0.96f, new Vector3(baseHalf * 1.7f, 0.14f, baseHalf * 1.7f));
            Part(PrimitiveType.Cylinder, world, wood, "Post", at + Vector3.up * (PostHeight * 0.5f), new Vector3(0.95f, PostHeight * 0.5f, 0.95f));

            var spikeMat = Lit(null, Vector2.one, new Color(0.78f, 0.74f, 0.66f));
            var spike = SpikeMesh();
            for (int k = 0; k < 4; k++)
            {
                float a = (45f + k * 90f) * Mathf.Deg2Rad;
                var corner = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * baseHalf * 0.85f;
                var go = new GameObject("Spike");
                go.transform.SetParent(world, false);
                go.transform.localPosition = at + corner + Vector3.up * 1.0f;
                go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (corner.normalized * 0.45f + Vector3.up).normalized);
                go.transform.localScale = new Vector3(0.22f, 0.75f, 0.22f);
                go.AddComponent<MeshFilter>().sharedMesh = spike;
                go.AddComponent<MeshRenderer>().sharedMaterial = spikeMat;
            }

            // Chain from the post top to the nearest point on the wall.
            var wallPoint = NearestWallTop(arena, at);
            var chain = new GameObject("Chain").AddComponent<LineRenderer>();
            chain.transform.SetParent(world, false);
            chain.useWorldSpace = true;
            chain.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            chain.startColor = chain.endColor = new Color(0.12f, 0.11f, 0.11f);
            chain.widthMultiplier = 0.07f;
            const int links = 24;
            chain.positionCount = links + 1;
            var from = at + Vector3.up * (PostHeight - 0.5f);
            for (int i = 0; i <= links; i++)
            {
                float t = i / (float)links;
                chain.SetPosition(i, Vector3.Lerp(from, wallPoint, t) + Vector3.down * Mathf.Sin(t * Mathf.PI) * 1.4f);
            }
        }

        static Vector3 NearestWallTop(Arena arena, Vector3 from)
        {
            var best = Vector3.zero;
            float bestDist = float.MaxValue;
            for (int i = 0; i < WallSegments; i++)
            {
                var p = EllipsePoint(arena, i, 0f) + Vector3.up * WallHeight;
                float d = (p - from).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = p; }
            }
            return best;
        }

        /// <summary>A 4-sided pyramid, base at y=0, tip at y=1.</summary>
        static Mesh SpikeMesh()
        {
            var verts = new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(0.5f, 0, -0.5f), new Vector3(0.5f, 0, 0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(0, 1, 0) };
            var mesh = new Mesh { name = "Spike", vertices = verts, triangles = new[] { 0, 4, 1, 1, 4, 2, 2, 4, 3, 3, 4, 0, 0, 1, 2, 0, 2, 3 } };
            mesh.RecalculateNormals();
            return mesh;
        }

        // --- Stands and crowd ---------------------------------------------------------------

        /// <summary>Three tiers of stone stands above the wall, with a crowd of simple silhouettes.</summary>
        static void Stands(Transform world, Arena arena, Material stone, int crowd)
        {
            const int segments = 64;
            for (int tier = 0; tier < 3; tier++)
            {
                float outset = 1.2f + tier * 1.6f, height = WallHeight + 0.3f + tier * 1.0f;
                for (int i = 0; i < segments; i++)
                {
                    var a = EllipsePoint(arena, i, outset, segments);
                    var b = EllipsePoint(arena, i + 1, outset, segments);
                    Segment(world, stone, "Stand", (a + b) * 0.5f + Vector3.up * (height * 0.5f), Quaternion.LookRotation(b - a, Vector3.up),
                        new Vector3(1.6f, height, Vector3.Distance(a, b) + 0.1f));
                }
            }

            var rng = new System.Random(5);
            var cloth = new[]
            {
                new Color(0.38f, 0.16f, 0.12f), new Color(0.45f, 0.36f, 0.24f), new Color(0.2f, 0.18f, 0.18f),
                new Color(0.32f, 0.22f, 0.3f), new Color(0.55f, 0.32f, 0.16f),
            };
            var mats = new Material[cloth.Length];
            for (int i = 0; i < cloth.Length; i++) mats[i] = Lit(null, Vector2.one, cloth[i]);
            for (int i = 0; i < crowd; i++)
            {
                int tier = rng.Next(3);
                float angle = (float)(rng.NextDouble() * Mathf.PI * 2);
                float outset = 1.2f + tier * 1.6f, height = WallHeight + 0.3f + tier * 1.0f;
                var p = new Vector3(Mathf.Cos(angle) * ((float)arena.RadiusX + outset), height, Mathf.Sin(angle) * ((float)arena.RadiusZ + outset));
                Part(PrimitiveType.Capsule, world, mats[rng.Next(mats.Length)], "Spectator", p + Vector3.up * 0.45f, new Vector3(0.38f, 0.45f, 0.38f));
            }
        }

        // --- Helpers ------------------------------------------------------------------------

        static GameObject Part(PrimitiveType type, Transform world, Material mat, string name, Vector3 pos, Vector3 scale)
        {
            var go = Primitives.Create(type, world, mat);
            go.name = name;
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            return go;
        }

        static void Segment(Transform world, Material mat, string name, Vector3 pos, Quaternion rot, Vector3 scale)
        {
            var go = Part(PrimitiveType.Cube, world, mat, name, pos, scale);
            go.transform.localRotation = rot;
        }

        static void Unshaded(Transform world, string name, List<Vector3> verts, List<Color> colors, List<int> tris)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            if (QualitySettings.activeColorSpace == ColorSpace.Linear) // vertex colors skip the color-space conversion
                for (int i = 0; i < colors.Count; i++) colors[i] = colors[i].linear;
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            var go = new GameObject(name);
            go.transform.SetParent(world, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
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

        /// <summary>
        /// Scanned Poly Haven surface from Resources/PolyHaven: "{path}_diff" color plus "{path}_nor_gl" bumps.
        /// Null if the color texture is missing.
        /// </summary>
        static Material Scanned(string path, Vector2 tiling, Color? color = null)
        {
            var diffuse = Resources.Load<Texture2D>("PolyHaven/" + path + "_diff");
            if (diffuse == null) return null;
            var m = Lit(diffuse, tiling, color);
            var normal = Resources.Load<Texture2D>("PolyHaven/" + path + "_nor_gl");
            if (normal != null)
            {
                m.SetTexture("_BumpMap", normal);
                m.EnableKeyword("_NORMALMAP");
            }
            return m;
        }

        /// <summary>Flat bright color (torch flames, gate eyes); Unlit/Color has no keywords for a build to strip.</summary>
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
}
