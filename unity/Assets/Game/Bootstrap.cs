using System;
using System.Collections.Generic;
using System.IO;
using Rats.Sim;
using Rats.Sim.Net;
using UnityEngine;

namespace Rats.Game
{
    /// <summary>
    /// The only component in the Bootstrap scene. Loads tunables, builds the arena in code, drives
    /// the sim on a fixed-tick clock with one input source per fighter, and syncs visuals to sim state.
    ///
    /// Practice / host: this machine runs the sim. Fighter 1 is the bot, or the remote player once one joins.
    /// Client: this machine runs no sim; it sends keyboard input and shows the host's snapshots as fighter 1.
    ///
    /// Keys: WASD move, J melee, K ranged, Space dash, Tab lock-on, Q/E orbit (unlocked),
    /// Enter restart round, F5 reload tunables.txt and restart (both host/practice only), Esc multiplayer menu.
    /// </summary>
    public sealed class Bootstrap : MonoBehaviour
    {
        static readonly Color SandColor = new Color(0.66f, 0.56f, 0.40f);
        static readonly Color StoneColor = new Color(0.42f, 0.40f, 0.37f);
        static readonly Color OutsideColor = new Color(0.22f, 0.20f, 0.18f);
        static readonly Color SkyColor = new Color(0.30f, 0.28f, 0.26f);
        static readonly Color[] FighterColors = { new Color(0.65f, 0.25f, 0.18f), new Color(0.2f, 0.35f, 0.6f) };
        static readonly string[] FighterColorNames = { "red", "blue" };
        const int WallSegments = 72;
        const float WallHeight = 0.8f, WallThickness = 0.5f, PillarHeight = 4f;

        public Simulation Sim { get; private set; }
        public Camera Camera => cameraRig.Camera;
        /// <summary>One input source per fighter. [0] keyboard, [1] bot or remote player; tools may replace them.</summary>
        public readonly IInputSource[] Inputs = new IInputSource[2];

        readonly TickClock clock = new TickClock();
        readonly Dictionary<int, GameObject> projectileViews = new Dictionary<int, GameObject>();
        readonly HashSet<int> seen = new HashSet<int>();
        readonly CharacterView[] fighters = new CharacterView[2];
        KeyboardInput keyboard;
        CameraRig cameraRig;
        Transform world;
        int localIndex;
        string tunablesText = "";
        string tunablesError;

        // Multiplayer
        HostSession host;
        ClientSession client;
        bool isClient;          // true once the host has started a round for us
        bool menuOpen;
        string joinAddress = "";
        string netMessage;      // last connection result, shown after a session ends
        List<string> myAddresses;

        void Awake()
        {
            Application.runInBackground = true; // a host that pauses when alt-tabbed would freeze the match
            Build();
            // Shortcuts for built players: RATS.exe -host   or   RATS.exe -join 100.x.y.z
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-host") StartHosting();
                else if (args[i] == "-join" && i + 1 < args.Length) Join(args[i + 1]);
            }
        }

        void OnDestroy() => StopNetwork(); // frees the port when leaving play mode

        /// <summary>
        /// New round on this machine's sim (practice or host). Called from Awake, on restart, or by editor tools.
        /// </summary>
        public void Build(Tunables overrideTunables = null)
        {
            BuildWorld(overrideTunables ?? LoadTunables(), 0);
            Inputs[1] = host != null && host.HasPlayer ? host.RemoteInput : new SimpleBot(Sim, 1, Environment.TickCount);
        }

        void BuildWorld(Tunables tunables, int local)
        {
            if (world != null) DestroyImmediate(world.gameObject);
            projectileViews.Clear();
            world = new GameObject("World").transform;
            localIndex = local;

            Sim = new Simulation(tunables);
            BuildArena(Sim.Arena);

            for (int i = 0; i < 2; i++)
            {
                fighters[i] = new GameObject($"Fighter {i}").AddComponent<CharacterView>();
                fighters[i].transform.SetParent(world, false);
                fighters[i].Build(FighterColors[i], i == localIndex ? 0 : tunables.ActionPointsPerTurn); // pips over the opponent only
            }

            var cam = new GameObject("Camera").AddComponent<Camera>();
            cam.transform.SetParent(world, false);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = SkyColor;
            cam.fieldOfView = 55f;
            cameraRig = new CameraRig(cam);

            keyboard = new KeyboardInput(cam.transform);
            Inputs[localIndex] = keyboard;
            Inputs[1 - localIndex] = null;

            SyncViews(0f, snapCamera: true);
        }

        Tunables LoadTunables()
        {
            tunablesError = null;
            var path = Path.Combine(Application.streamingAssetsPath, "tunables.txt");
            try
            {
                tunablesText = File.ReadAllText(path);
                return Tunables.Parse(tunablesText);
            }
            catch (Exception e)
            {
                tunablesText = ""; // an empty file means "all defaults" on the other side too
                tunablesError = $"tunables.txt: {e.Message} (using defaults)";
                Debug.LogError(tunablesError);
                return new Tunables();
            }
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) menuOpen = !menuOpen;
            keyboard.Enabled = !menuOpen;
            PollNetwork();
            if (isClient) UpdateClient();
            else UpdateLocalSim();
            SyncViews(Time.unscaledDeltaTime);
        }

        void UpdateLocalSim()
        {
            if (!menuOpen && Input.GetKeyDown(KeyCode.F5)) { Build(); SendRestartToPlayer(); return; }
            if (!menuOpen && Input.GetKeyDown(KeyCode.Return)) { Build(Sim.T); SendRestartToPlayer(); return; }

            keyboard.Poll();
            int ticks = clock.Advance(Time.unscaledDeltaTime);
            for (int i = 0; i < ticks; i++) Step();
            if (ticks > 0) host?.SendSnapshot(Sim.SaveSnapshot());
        }

        void UpdateClient()
        {
            keyboard.Poll();
            int ticks = clock.Advance(Time.unscaledDeltaTime);
            for (int i = 0; i < ticks; i++) client.SendInput(keyboard.Read(i));
            if (client.TakeSnapshot(out var snapshot)) Sim.ApplySnapshot(snapshot);
        }

        /// <summary>One sim tick with each fighter's input source.</summary>
        public void Step() => Sim.Step(Inputs[0].Read(Sim.Tick), Inputs[1].Read(Sim.Tick));

        // --- Multiplayer ------------------------------------------------------------------

        void PollNetwork()
        {
            if (host != null)
            {
                switch (host.Poll())
                {
                    case HostSession.Event.PlayerJoined:
                        Build(); // fresh round with the remote player as fighter 1
                        host.SendWelcome(tunablesText);
                        menuOpen = false;
                        Debug.Log("[Net] player joined, round started as host");
                        break;
                    case HostSession.Event.PlayerLeft:
                        Inputs[1] = new SimpleBot(Sim, 1, Environment.TickCount); // keep playing against the bot
                        Debug.Log("[Net] player left: " + host.Status);
                        break;
                }
            }

            if (client != null)
            {
                switch (client.Poll())
                {
                    case ClientSession.Event.RoundStarted:
                        BuildWorld(ParseHostTunables(client.RoundTunables), 1);
                        isClient = true;
                        menuOpen = false;
                        Debug.Log("[Net] round started as client");
                        break;
                    case ClientSession.Event.Lost:
                        netMessage = client.Status;
                        Debug.Log("[Net] connection ended: " + netMessage);
                        StopNetwork();
                        Build(); // back to practice
                        menuOpen = true;
                        break;
                }
            }
        }

        Tunables ParseHostTunables(string text)
        {
            try { return Tunables.Parse(text); }
            catch (FormatException e)
            {
                tunablesError = $"host tunables: {e.Message} (using defaults)";
                return new Tunables();
            }
        }

        void SendRestartToPlayer()
        {
            if (host != null && host.HasPlayer) host.SendRestart(tunablesText);
        }

        void StartHosting()
        {
            StopNetwork();
            try
            {
                host = new HostSession(NetProtocol.DefaultPort);
                Debug.Log("[Net] hosting on port " + host.Port);
                myAddresses = LocalAddresses.Describe();
                netMessage = null;
            }
            catch (System.Net.Sockets.SocketException e)
            {
                host = null;
                netMessage = $"Could not host on port {NetProtocol.DefaultPort}: {e.Message}";
            }
        }

        void Join(string address)
        {
            StopNetwork();
            address = address.Trim();
            int port = NetProtocol.DefaultPort;
            int colon = address.LastIndexOf(':');
            if (colon > 0 && int.TryParse(address.Substring(colon + 1), out var p)) { port = p; address = address.Substring(0, colon); }
            if (address.Length == 0) { netMessage = "Type the host's address first."; return; }
            client = new ClientSession(address, port);
            netMessage = null;
        }

        void StopNetwork()
        {
            host?.Dispose();
            host = null;
            client?.Dispose();
            client = null;
            isClient = false;
        }

        // --- Views ------------------------------------------------------------------------

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

            cameraRig.Update(Sim.Fighters[localIndex], Sim.Fighters[1 - localIndex], deltaTime, snapCamera);
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

        // --- Overlay and menu -------------------------------------------------------------

        bool Online => isClient || (host != null && host.HasPlayer);

        void OnGUI()
        {
            if (Sim == null) return;
            var me = Sim.Fighters[localIndex];
            var them = Sim.Fighters[1 - localIndex];
            string themName = Online ? "Opponent" : "Bot";
            string phase = Sim.Phase == Phase.Combat ? $"COMBAT  {Sim.PhaseTicksLeft / 60f:F1}s"
                         : Sim.Phase == Phase.Freeze ? $"FROZEN  {Sim.PhaseTicksLeft / 60f:F1}s"
                         : "ROUND OVER";
            string restartKeys = isClient ? "" : "  Enter restart  F5 reload tunables";
            GUI.Label(new Rect(12, 8, 900, 140),
                $"Turn {Sim.Turn}   {phase}\n" +
                $"tick {Sim.Tick}   combat tick {Sim.CombatTick}\n" +
                $"You ({FighterColorNames[localIndex]})  HP {me.Hp}  AP {me.ActionPoints}  {(me.LockedOn ? "LOCKED" : "free")}   {Describe(me)}\n" +
                $"{themName}  HP {them.Hp}  AP {them.ActionPoints}   {Describe(them)}\n" +
                $"WASD move  J melee  K ranged  Space dash  Tab lock  Q/E orbit{restartKeys}  Esc multiplayer\n" +
                NetStatusLine());
            if (tunablesError != null) GUI.Label(new Rect(12, 150, 900, 40), tunablesError);

            if (Sim.Phase == Phase.RoundOver)
            {
                string result = Sim.Winner == Simulation.Draw ? "DRAW"
                              : Sim.Winner == localIndex ? "YOU WIN"
                              : Online ? "OPPONENT WINS" : "BOT WINS";
                var style = new GUIStyle(GUI.skin.label) { fontSize = 40, alignment = TextAnchor.MiddleCenter };
                GUI.Label(new Rect(0, Screen.height * 0.35f, Screen.width, 60), result, style);
                GUI.Label(new Rect(0, Screen.height * 0.35f + 60, Screen.width, 30), isClient ? "Waiting for the host to restart" : "Enter to restart",
                    new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
            }

            if (menuOpen) DrawMenu();
        }

        string NetStatusLine()
        {
            if (host != null) return host.Status;
            if (client != null) return client.Status;
            return "Practice vs bot";
        }

        void DrawMenu()
        {
            const float w = 520, h = 330;
            GUILayout.BeginArea(new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h), GUI.skin.box);
            GUILayout.Label("MULTIPLAYER");
            GUILayout.Space(6);

            if (GUILayout.Button("Practice vs bot")) { StopNetwork(); Build(); menuOpen = false; netMessage = null; }

            GUILayout.Space(6);
            if (GUILayout.Button(host == null ? $"Host a game (port {NetProtocol.DefaultPort})" : "Stop hosting"))
            {
                if (host == null) StartHosting();
                else { StopNetwork(); Build(); }
            }
            if (host != null && myAddresses != null)
            {
                GUILayout.Label("Tell the other player one of these:");
                foreach (var a in myAddresses) GUILayout.Label("   " + a);
            }

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Host address:", GUILayout.Width(100));
            joinAddress = GUILayout.TextField(joinAddress, GUILayout.Width(240));
            if (GUILayout.Button(client == null ? "Join" : "Leave", GUILayout.Width(80)))
            {
                if (client == null) Join(joinAddress);
                else { StopNetwork(); Build(); }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            if (netMessage != null) GUILayout.Label(netMessage);
            else if (client != null) GUILayout.Label(client.Status);
            else if (host != null) GUILayout.Label(host.Status);
            GUILayout.FlexibleSpace();
            GUILayout.Label("Esc to close");
            GUILayout.EndArea();
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
