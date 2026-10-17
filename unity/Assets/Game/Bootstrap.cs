using System;
using System.Collections.Generic;
using System.IO;
using Rats.Sim;
using UnityEngine;

namespace Rats.Game
{
    /// <summary>
    /// The only component in the Bootstrap scene. Loads tunables, builds the arena in code, drives
    /// the sim on a fixed-tick clock with one input source per fighter, and syncs visuals to sim state.
    /// Keys: WASD move, J melee, K ranged, Space dash, Tab lock-on, Q/E orbit (unlocked),
    /// Enter restart round, F5 reload tunables.txt and restart.
    /// </summary>
    public sealed class Bootstrap : MonoBehaviour
    {
        static readonly Color SandColor = new Color(0.66f, 0.56f, 0.40f);
        static readonly Color StoneColor = new Color(0.42f, 0.40f, 0.37f);
        static readonly Color OutsideColor = new Color(0.22f, 0.20f, 0.18f);
        static readonly Color SkyColor = new Color(0.30f, 0.28f, 0.26f);
        static readonly Color[] FighterColors = { new Color(0.65f, 0.25f, 0.18f), new Color(0.2f, 0.35f, 0.6f) };
        const int WallSegments = 72;
        const float WallHeight = 0.8f, WallThickness = 0.5f, PillarHeight = 4f;

        public Simulation Sim { get; private set; }
        public Camera Camera => cameraRig.Camera;
        /// <summary>One input source per fighter. [0] keyboard, [1] bot by default; tools may replace them.</summary>
        public readonly IInputSource[] Inputs = new IInputSource[2];

        readonly TickClock clock = new TickClock();
        readonly Dictionary<int, GameObject> projectileViews = new Dictionary<int, GameObject>();
        readonly HashSet<int> seen = new HashSet<int>();
        readonly CharacterView[] fighters = new CharacterView[2];
        KeyboardInput keyboard;
        CameraRig cameraRig;
        Transform world;
        string tunablesError;

        void Awake() => Build();

        /// <summary>Builds everything from a fresh sim. Called from Awake, on restart, or by editor tools.</summary>
        public void Build(Tunables overrideTunables = null)
        {
            if (world != null) DestroyImmediate(world.gameObject);
            projectileViews.Clear();
            world = new GameObject("World").transform;

            var tunables = overrideTunables ?? LoadTunables();
            Sim = new Simulation(tunables);
            BuildArena(Sim.Arena);

            for (int i = 0; i < 2; i++)
            {
                fighters[i] = new GameObject($"Fighter {i}").AddComponent<CharacterView>();
                fighters[i].transform.SetParent(world, false);
                fighters[i].Build(FighterColors[i], i == 0 ? 0 : tunables.ActionPointsPerTurn); // pips over the opponent only
            }

            var cam = new GameObject("Camera").AddComponent<Camera>();
            cam.transform.SetParent(world, false);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = SkyColor;
            cam.fieldOfView = 55f;
            cameraRig = new CameraRig(cam);

            keyboard = new KeyboardInput(cam.transform);
            Inputs[0] = keyboard;
            Inputs[1] = new SimpleBot(Sim, 1, Environment.TickCount);

            SyncViews(0f, snapCamera: true);
        }

        Tunables LoadTunables()
        {
            tunablesError = null;
            var path = Path.Combine(Application.streamingAssetsPath, "tunables.txt");
            try
            {
                return Tunables.Parse(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                tunablesError = $"tunables.txt: {e.Message} (using defaults)";
                Debug.LogError(tunablesError);
                return new Tunables();
            }
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F5)) { Build(); return; }
            if (Input.GetKeyDown(KeyCode.Return)) { Build(Sim.T); return; }

            keyboard.Poll();
            int ticks = clock.Advance(Time.unscaledDeltaTime);
            for (int i = 0; i < ticks; i++) Step();
            SyncViews(Time.unscaledDeltaTime);
        }

        /// <summary>One sim tick with each fighter's input source.</summary>
        public void Step() => Sim.Step(Inputs[0].Read(Sim.Tick), Inputs[1].Read(Sim.Tick));

        public void SyncViews(float deltaTime, bool snapCamera = false)
        {

            seen.Clear();
            foreach (var p in Sim.Projectiles)
            {
                seen.Add(p.Id);
                if (!projectileViews.TryGetValue(p.Id, out var view))
                {
                    view = Primitives.Create(PrimitiveType.Sphere, world, new Color(1f, 0.45f, 0.1f));
                    view.name = $"Projectile {p.Id}";
                    view.transform.localScale = Vector3.one * (Sim.T.ProjectileRadius * 2f);
                    projectileViews[p.Id] = view;
                }
                view.transform.position = SimConvert.ToUnity(p.Position);
            }
            var gone = new List<int>();
            foreach (var id in projectileViews.Keys) if (!seen.Contains(id)) gone.Add(id);
            foreach (var id in gone) { DestroyImmediate(projectileViews[id]); projectileViews.Remove(id); }

            cameraRig.Update(Sim.Fighters[0], Sim.Fighters[1], deltaTime, snapCamera);
            for (int i = 0; i < 2; i++) fighters[i].Sync(Sim.Fighters[i], Sim.T, Camera.transform.rotation);
        }

        void BuildArena(Arena arena)
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(world, false);
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.intensity = 1.1f;
            sun.color = new Color(1f, 0.93f, 0.8f);
            sun.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.36f, 0.33f, 0.30f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = SkyColor;
            RenderSettings.fogStartDistance = 25f;
            RenderSettings.fogEndDistance = 70f;

            var outside = Primitives.Create(PrimitiveType.Plane, world, OutsideColor);
            outside.name = "Outside";
            outside.transform.localPosition = new Vector3(0f, -0.02f, 0f);
            outside.transform.localScale = new Vector3(20f, 1f, 20f);

            var sand = Primitives.Create(PrimitiveType.Cylinder, world, SandColor);
            sand.name = "Sand";
            sand.transform.localScale = new Vector3(arena.RadiusX * 2f, 0.01f, arena.RadiusZ * 2f);

            for (int i = 0; i < WallSegments; i++)
            {
                var a = EllipsePoint(arena, i, WallThickness * 0.5f);
                var b = EllipsePoint(arena, i + 1, WallThickness * 0.5f);
                var wall = Primitives.Create(PrimitiveType.Cube, world, StoneColor);
                wall.name = $"Wall {i}";
                wall.transform.localPosition = (a + b) * 0.5f + Vector3.up * (WallHeight * 0.5f);
                wall.transform.localRotation = Quaternion.LookRotation(b - a, Vector3.up);
                wall.transform.localScale = new Vector3(WallThickness, WallHeight, Vector3.Distance(a, b) + 0.05f);
            }

            foreach (var c in arena.Pillars)
            {
                var pillar = Primitives.Create(PrimitiveType.Cylinder, world, StoneColor * 1.15f);
                pillar.name = "Pillar";
                pillar.transform.localPosition = SimConvert.ToUnity(c) + Vector3.up * (PillarHeight * 0.5f);
                pillar.transform.localScale = new Vector3(arena.PillarRadius * 2f, PillarHeight * 0.5f, arena.PillarRadius * 2f);
            }
        }

        static Vector3 EllipsePoint(Arena arena, int i, float outset)
        {
            float angle = i * Mathf.PI * 2f / WallSegments;
            return new Vector3(Mathf.Cos(angle) * (arena.RadiusX + outset), 0f, Mathf.Sin(angle) * (arena.RadiusZ + outset));
        }

        void OnGUI()
        {
            if (Sim == null) return;
            var me = Sim.Fighters[0];
            var bot = Sim.Fighters[1];
            string phase = Sim.Phase == Phase.Combat ? $"COMBAT  {Sim.PhaseTicksLeft / 60f:F1}s"
                         : Sim.Phase == Phase.Freeze ? $"FROZEN  {Sim.PhaseTicksLeft / 60f:F1}s"
                         : "ROUND OVER";
            GUI.Label(new Rect(12, 8, 700, 120),
                $"Turn {Sim.Turn}   {phase}\n" +
                $"tick {Sim.Tick}   combat tick {Sim.CombatTick}\n" +
                $"You  HP {me.Hp}  AP {me.ActionPoints}  {(me.LockedOn ? "LOCKED" : "free")}   {Describe(me)}\n" +
                $"Bot  HP {bot.Hp}  AP {bot.ActionPoints}   {Describe(bot)}\n" +
                "WASD move  J melee  K ranged  Space dash  Tab lock  Q/E orbit  Enter restart  F5 reload tunables");
            if (tunablesError != null) GUI.Label(new Rect(12, 130, 900, 40), tunablesError);

            if (Sim.Phase == Phase.RoundOver)
            {
                string result = Sim.Winner == Simulation.Draw ? "DRAW" : Sim.Winner == 0 ? "YOU WIN" : "BOT WINS";
                var style = new GUIStyle(GUI.skin.label) { fontSize = 40, alignment = TextAnchor.MiddleCenter };
                GUI.Label(new Rect(0, Screen.height * 0.35f, Screen.width, 60), result, style);
                GUI.Label(new Rect(0, Screen.height * 0.35f + 60, Screen.width, 30), "Enter to restart",
                    new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
            }
        }

        string Describe(Fighter f)
        {
            if (!f.IsBusy) return "";
            var def = Sim.T.Get(f.Action);
            string part = f.ActionTick < def.Startup ? "startup" : def.IsActive(f.ActionTick) ? "ACTIVE" : "recovery";
            return $"{f.Action} {part} {f.ActionTick}/{def.Total}";
        }
    }
}
