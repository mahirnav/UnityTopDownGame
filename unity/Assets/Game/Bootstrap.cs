using System.Collections.Generic;
using Rats.Sim;
using UnityEngine;

namespace Rats.Game
{
    /// <summary>
    /// The only component in the Bootstrap scene. Builds the world in code, drives
    /// the sim on a fixed-tick clock, and makes every visual follow sim state.
    /// Keys: WASD move, Space fire, E melee, F freeze, R resume.
    /// </summary>
    public sealed class Bootstrap : MonoBehaviour
    {
        static readonly Vector3 CameraOffset = new Vector3(0f, 3f, -6f);
        const float CameraFollowRate = 8f;

        public Simulation Sim { get; private set; }
        public Camera Camera { get; private set; }
        public CharacterView Character { get; private set; }
        public IInputSource InputSource;

        readonly TickClock clock = new TickClock();
        readonly KeyboardInput keyboard = new KeyboardInput();
        readonly Dictionary<int, GameObject> projectileViews = new Dictionary<int, GameObject>();
        readonly HashSet<int> seen = new HashSet<int>();
        Transform world;

        void Awake() => Build();

        /// <summary>Builds the scene. Called from Awake in play mode, or directly by editor tools.</summary>
        public void Build()
        {
            Sim = new Simulation();
            InputSource = keyboard;
            world = new GameObject("World").transform;

            var floor = Primitives.Create(PrimitiveType.Plane, world, new Color(0.72f, 0.62f, 0.45f));
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(4f, 1f, 4f); // 40 m x 40 m

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(world, false);
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.5f, 0.45f);

            Character = new GameObject("Player").AddComponent<CharacterView>();
            Character.transform.SetParent(world, false);
            Character.Build();

            Camera = new GameObject("Camera").AddComponent<Camera>();
            Camera.transform.SetParent(world, false);
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = new Color(0.32f, 0.30f, 0.27f);
            Camera.transform.position = CameraOffset;
            Camera.transform.LookAt(Vector3.up);

            SyncViews(1f);
        }

        void Update()
        {
            keyboard.Poll();
            if (UnityEngine.Input.GetKeyDown(KeyCode.F)) Sim.Freeze();
            if (UnityEngine.Input.GetKeyDown(KeyCode.R)) Sim.Resume();

            int ticks = clock.Advance(Time.unscaledDeltaTime, Sim.IsFrozen);
            for (int i = 0; i < ticks; i++) Sim.Step(InputSource.Read(Sim.Tick));
            if (Sim.IsFrozen) keyboard.ClearPresses(); // no buffering through a freeze in the spike

            SyncViews(Time.unscaledDeltaTime);
        }

        public void SyncViews(float deltaTime)
        {
            Character.Sync(Sim.Player);

            seen.Clear();
            foreach (var p in Sim.Projectiles)
            {
                seen.Add(p.Id);
                if (!projectileViews.TryGetValue(p.Id, out var view))
                {
                    view = Primitives.Create(PrimitiveType.Sphere, world, new Color(1f, 0.45f, 0.1f));
                    view.name = $"Projectile {p.Id}";
                    view.transform.localScale = Vector3.one * 0.3f;
                    projectileViews[p.Id] = view;
                }
                view.transform.position = SimConvert.ToUnity(p.Position);
            }
            var gone = new List<int>();
            foreach (var id in projectileViews.Keys) if (!seen.Contains(id)) gone.Add(id);
            foreach (var id in gone) { DestroyImmediate(projectileViews[id]); projectileViews.Remove(id); }

            var focus = SimConvert.ToUnity(Sim.Player.Position);
            var target = focus + CameraOffset;
            Camera.transform.position = Vector3.Lerp(Camera.transform.position, target,
                1f - Mathf.Exp(-CameraFollowRate * deltaTime));
            Camera.transform.LookAt(focus + Vector3.up);
        }

        void OnGUI()
        {
            GUI.Label(new Rect(12, 8, 600, 80),
                $"tick {Sim.Tick}   {(Sim.IsFrozen ? "FROZEN" : "combat")}\n" +
                $"swing {Sim.Player.SwingProgress:F2}   projectiles {Sim.Projectiles.Count}\n" +
                "WASD move  Space fire  E melee  F freeze  R resume");
        }
    }
}
