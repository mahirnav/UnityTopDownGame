using Rats.Sim;
using UnityEngine;

namespace Rats.Game
{
    /// <summary>
    /// Combat effects driven by sim state: hit sparks (gold for backstabs), dust while dashing, and a flash of
    /// the melee hitbox (reach and arc) during active frames. Particles pause while time is frozen.
    /// Visual only; nothing here feeds back into the sim.
    /// </summary>
    sealed class Effects
    {
        readonly ParticleSystem sparks, dust;
        readonly GameObject[] arcs = new GameObject[2];
        const int FireSegments = 96;
        readonly Mesh fireMesh;
        readonly GameObject fireRing;
        readonly Vector3[] fireVerts = new Vector3[(FireSegments + 1) * 2];
        readonly Color[] fireColors = new Color[(FireSegments + 1) * 2];
        readonly VisualSettings look;
        bool paused;

        public Effects(Transform world, VisualSettings look)
        {
            this.look = look;
            sparks = MakeSystem(world, "Sparks", "Legacy Shaders/Particles/Additive");
            dust = MakeSystem(world, "Dust", "Legacy Shaders/Particles/Alpha Blended");

            // Fire ring: a band from the safe zone's edge out to the wall, rebuilt as the ring closes.
            fireMesh = new Mesh { name = "FireRing" };
            fireMesh.MarkDynamic();
            var tris = new int[FireSegments * 6];
            for (int s = 0; s < FireSegments; s++)
            {
                int i = s * 2;
                tris[s * 6] = i; tris[s * 6 + 1] = i + 1; tris[s * 6 + 2] = i + 2;
                tris[s * 6 + 3] = i + 1; tris[s * 6 + 4] = i + 3; tris[s * 6 + 5] = i + 2;
            }
            fireMesh.vertices = fireVerts;
            fireMesh.triangles = tris;
            fireRing = new GameObject("FireRing");
            fireRing.transform.SetParent(world, false);
            fireRing.AddComponent<MeshFilter>().sharedMesh = fireMesh;
            fireRing.AddComponent<MeshRenderer>().sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            fireRing.SetActive(false);
        }

        public void Sparks(Vector3 at, bool backstab)
        {
            int count = backstab ? 26 : 14;
            var color = backstab ? new Color(1f, 0.85f, 0.25f) : new Color(1f, 0.55f, 0.2f);
            for (int i = 0; i < count; i++)
            {
                sparks.Emit(new ParticleSystem.EmitParams
                {
                    position = at,
                    velocity = (Random.onUnitSphere + Vector3.up * 0.6f) * Random.Range(2f, 5f),
                    startColor = color,
                    startSize = Random.Range(0.05f, 0.11f),
                    startLifetime = Random.Range(0.2f, 0.45f),
                }, 1);
            }
        }

        public void Sync(Simulation sim)
        {
            SyncFire(sim);
            bool frozen = sim.Phase != Phase.Combat;
            if (frozen != paused)
            {
                paused = frozen;
                if (paused) { sparks.Pause(); dust.Pause(); }
                else { sparks.Play(); dust.Play(); }
            }

            for (int i = 0; i < 2; i++)
            {
                var f = sim.Fighters[i];
                var feet = SimConvert.ToUnity(f.Position) + Vector3.up * 0.05f;
                if (!paused && f.Action == ActionKind.Dash && f.Frames.IsActive(f.ActionTick))
                    dust.Emit(new ParticleSystem.EmitParams
                    {
                        position = feet + Random.insideUnitSphere * 0.2f,
                        velocity = Vector3.up * 0.4f + Random.insideUnitSphere * 0.3f,
                        startColor = new Color(0.62f, 0.52f, 0.38f, 0.55f),
                        startSize = Random.Range(0.2f, 0.4f),
                        startLifetime = 0.5f,
                    }, 1);

                bool showArc = look.SwingArcs && f.Action == ActionKind.Melee && f.Frames.IsActive(f.ActionTick);
                if (showArc && arcs[i] == null) arcs[i] = MakeArc(sparks.transform.parent, (float)(f.ActionReach + sim.T.FighterRadius), (float)sim.T.MeleeArcDegrees);
                if (arcs[i] != null)
                {
                    arcs[i].SetActive(showArc);
                    if (showArc)
                    {
                        arcs[i].transform.position = feet;
                        arcs[i].transform.rotation = Quaternion.LookRotation(SimConvert.ToUnity(f.Facing), Vector3.up);
                        float reach = (float)(f.ActionReach + sim.T.FighterRadius);
                        arcs[i].transform.localScale = new Vector3(reach, 1f, reach); // mesh is built at radius 1
                    }
                }
            }
        }

        void SyncFire(Simulation sim)
        {
            double scale = sim.FireScale;
            fireRing.SetActive(scale < 1);
            if (scale >= 1) return;
            float rx = (float)sim.Arena.RadiusX, rz = (float)sim.Arena.RadiusZ, s = (float)scale;
            float t = paused ? 0f : Time.realtimeSinceStartup; // flames hold still while time is frozen
            for (int k = 0; k <= FireSegments; k++)
            {
                float a = k * Mathf.PI * 2f / FireSegments;
                float c = Mathf.Cos(a), sn = Mathf.Sin(a);
                float flicker = paused ? 0.5f : Mathf.PerlinNoise(k * 0.35f, t * 3f);
                fireVerts[k * 2] = new Vector3(c * rx * s, 0.04f, sn * rz * s);
                fireVerts[k * 2 + 1] = new Vector3(c * (rx + 0.3f), 0.04f, sn * (rz + 0.3f));
                fireColors[k * 2] = Linear(new Color(1f, 0.55f + 0.3f * flicker, 0.12f, 0.95f));
                fireColors[k * 2 + 1] = Linear(new Color(0.55f, 0.08f, 0.03f, 0.75f));
                if (!paused && Random.value < 0.08f)
                    sparks.Emit(new ParticleSystem.EmitParams
                    {
                        position = fireVerts[k * 2] + Vector3.up * 0.1f,
                        velocity = new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(1.5f, 3f), Random.Range(-0.3f, 0.3f)),
                        startColor = new Color(1f, 0.5f + Random.value * 0.3f, 0.1f),
                        startSize = Random.Range(0.06f, 0.14f),
                        startLifetime = Random.Range(0.4f, 0.9f),
                    }, 1);
            }
            fireMesh.vertices = fireVerts;
            fireMesh.colors = fireColors;
            fireMesh.RecalculateBounds();
        }

        /// <summary>Mesh vertex colors skip Unity's color-space conversion; convert by hand in Linear projects.</summary>
        static Color Linear(Color c) => QualitySettings.activeColorSpace == ColorSpace.Linear ? c.linear : c;

        static ParticleSystem MakeSystem(Transform parent, string name, string shader)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = name == "Sparks" ? 1.2f : -0.05f;
            main.maxParticles = 600;
            var emission = ps.emission;
            emission.enabled = false;            // only explicit Emit calls
            var shape = ps.shape;
            shape.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = new Material(Shader.Find(shader));
            ps.Play();
            return ps;
        }

        /// <summary>Flat fan on the ground: radius 1 (scaled to reach), the melee arc wide.</summary>
        static GameObject MakeArc(Transform parent, float reach, float arcDegrees)
        {
            const int steps = 16;
            var verts = new Vector3[steps + 2];
            var colors = new Color[verts.Length];
            var tris = new int[steps * 3];
            verts[0] = Vector3.zero;
            colors[0] = new Color(1f, 0.95f, 0.85f, 0.05f);
            for (int s = 0; s <= steps; s++)
            {
                float a = (-arcDegrees * 0.5f + arcDegrees * s / steps) * Mathf.Deg2Rad;
                verts[s + 1] = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                colors[s + 1] = new Color(1f, 0.95f, 0.85f, 0.45f);
                if (s < steps) { tris[s * 3] = 0; tris[s * 3 + 1] = s + 1; tris[s * 3 + 2] = s + 2; }
            }
            var mesh = new Mesh { vertices = verts, colors = colors, triangles = tris };
            var go = new GameObject("SwingArc");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            return go;
        }
    }
}
