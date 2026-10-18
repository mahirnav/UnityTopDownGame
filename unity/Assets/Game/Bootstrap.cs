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
    /// Keys: WASD move, J melee, K ranged, Space dash, Tab lock-on, 1-3 draft, Q/E orbit (unlocked), Enter new match,
    /// F5 reload tunables.txt + visuals.txt and restart (both host/practice only), F9 save replay, F1 debug overlay, Esc menu.
    /// </summary>
    public sealed class Bootstrap : MonoBehaviour
    {
        static readonly Color[] FighterColors = { new Color(0.65f, 0.25f, 0.18f), new Color(0.2f, 0.35f, 0.6f) };
        static readonly string[] FighterColorNames = { "red", "blue" };

        public Simulation Sim { get; private set; }
        public Camera Camera => cameraRig.Camera;
        /// <summary>One input source per fighter. [0] keyboard, [1] bot or remote player; tools may replace them.</summary>
        public readonly IInputSource[] Inputs = new IInputSource[2];

        readonly TickClock clock = new TickClock();
        readonly Dictionary<int, GameObject> projectileViews = new Dictionary<int, GameObject>();
        readonly HashSet<int> seen = new HashSet<int>();
        readonly CharacterView[] fighters = new CharacterView[2];
        Feedback feedback;
        Effects effects;
        Hud hud;
        RetroEffect retro;
        Camera uiCamera;
        VisualSettings look;
        bool showDebug;
        KeyboardInput keyboard;
        CameraRig cameraRig;
        Transform world;
        int localIndex;
        MatchRecording recording; // this machine's sim only (practice / host)
        string replayMessage;
        float replayMessageUntil;
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

        void OnDestroy()
        {
            SaveRecording("quit");
            StopNetwork(); // frees the port when leaving play mode
        }

        /// <summary>
        /// New round on this machine's sim (practice or host). Called from Awake, on restart, or by editor tools.
        /// </summary>
        public void Build(Tunables overrideTunables = null)
        {
            SaveRecording("restart");
            BuildWorld(overrideTunables ?? LoadTunables(), 0);
            Inputs[1] = host != null && host.HasPlayer ? host.RemoteInput : new SimpleBot(Sim, 1, Environment.TickCount);
            recording = new MatchRecording(Sim.T, Sim.Seed);
        }

        void BuildWorld(Tunables tunables, int local)
        {
            if (world != null) DestroyImmediate(world.gameObject);
            projectileViews.Clear();
            world = new GameObject("World").transform;
            localIndex = local;

            Sim = new Simulation(tunables, (ulong)DateTime.Now.Ticks); // fresh draft draws every round; replays store the seed
            look = VisualSettings.Load();
            ArenaView.Build(world, Sim.Arena, look);

            for (int i = 0; i < 2; i++)
            {
                fighters[i] = new GameObject($"Fighter {i}").AddComponent<CharacterView>();
                fighters[i].transform.SetParent(world, false);
                fighters[i].Build(FighterColors[i], i == localIndex ? 0 : tunables.ActionPointsPerTurn); // pips over the opponent only
            }

            int uiLayer = LayerMask.NameToLayer("UI");
            var cam = new GameObject("Camera").AddComponent<Camera>();
            cam.transform.SetParent(world, false);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.62f, 0.42f, 0.30f);
            cam.fieldOfView = 55f;
            cam.farClipPlane = 1000f;
            cam.cullingMask = ~(1 << uiLayer);
            retro = cam.gameObject.AddComponent<RetroEffect>();
            retro.Look = look;
            cameraRig = new CameraRig(cam, Sim.Arena);

            // The HUD gets its own camera so the retro effect leaves text crisp.
            uiCamera = new GameObject("UICamera").AddComponent<Camera>();
            uiCamera.transform.SetParent(world, false);
            uiCamera.clearFlags = CameraClearFlags.Depth;
            uiCamera.cullingMask = 1 << uiLayer;
            uiCamera.depth = 10;

            keyboard = new KeyboardInput(cam.transform);
            hud = new Hud(world, cam, uiCamera) { OnPickDraft = keyboard.ChooseDraft };
            effects = new Effects(world, look);
            feedback = new Feedback(cameraRig, fighters, hud, effects, look);
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
            if (Input.GetKeyDown(KeyCode.Escape)) menuOpen = !menuOpen;
            if (Input.GetKeyDown(KeyCode.F1)) showDebug = !showDebug;
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
            if (Sim.Phase == Phase.MatchOver && recording != null) { SaveRecording("match"); recording = null; }
            if (!menuOpen && Input.GetKeyDown(KeyCode.F9)) SaveRecording("marked");
        }

        void UpdateClient()
        {
            keyboard.Poll();
            int ticks = clock.Advance(Time.unscaledDeltaTime);
            for (int i = 0; i < ticks; i++) client.SendInput(keyboard.Read(i));
            if (client.TakeSnapshot(out var snapshot)) Sim.ApplySnapshot(snapshot);
        }

        /// <summary>One sim tick with each fighter's input source.</summary>
        public void Step()
        {
            var input0 = Inputs[0].Read(Sim.Tick);
            var input1 = Inputs[1].Read(Sim.Tick);
            recording?.Record(input0, input1);
            Sim.Step(input0, input1);
        }

        // --- Multiplayer ------------------------------------------------------------------

        void PollNetwork()
        {
            if (host != null)
            {
                switch (host.Poll())
                {
                    case HostSession.Event.PlayerJoined:
                        Build(); // fresh round with the remote player as fighter 1
                        host.SendWelcome(Sim.T.ToText());
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
                        SaveRecording("restart");
                        recording = null; // the host records the match
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
            if (host != null && host.HasPlayer) host.SendRestart(Sim.T.ToText());
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

        // --- Replays ----------------------------------------------------------------------

        const int KeepReplays = 100;

        /// <summary>unity/replays in the editor, next to RATS.exe in a build.</summary>
        static string ReplayDirectory => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "replays"));

        /// <summary>Writes the current round so far and returns the path. Recording continues; F9 can be pressed any number of times.</summary>
        public string SaveRecording(string reason)
        {
            if (recording == null || recording.Ticks < Tunables.TickRate || !Application.isPlaying) return null;
            try
            {
                string mode = host != null && host.HasPlayer ? "online" : "practice";
                recording.FinalSnapshot = Sim.SaveSnapshot();
                recording.Note = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {mode} {reason}, tick {Sim.Tick}";
                Directory.CreateDirectory(ReplayDirectory);
                var path = Path.Combine(ReplayDirectory, $"{DateTime.Now:yyyyMMdd_HHmmss}_{mode}_{reason}_t{Sim.Tick}.ratsreplay");
                using (var file = File.Create(path)) recording.Save(file);

                var old = Directory.GetFiles(ReplayDirectory, "*.ratsreplay");
                Array.Sort(old);
                for (int i = 0; i < old.Length - KeepReplays; i++) File.Delete(old[i]);

                replayMessage = $"Replay saved: {Path.GetFileName(path)}";
                replayMessageUntil = Time.unscaledTime + 4f;
                Debug.Log($"[Replay] saved {path}");
                return path;
            }
            catch (IOException e)
            {
                replayMessage = $"Replay not saved: {e.Message}";
                replayMessageUntil = Time.unscaledTime + 6f;
                return null;
            }
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
                    view.transform.localScale = Vector3.one * (float)(p.Radius * 2);
                    var trail = view.AddComponent<TrailRenderer>();
                    trail.time = 0.18f;
                    trail.widthMultiplier = (float)(p.Radius * 0.9);
                    trail.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
                    trail.startColor = new Color(1f, 0.6f, 0.2f, 0.8f);
                    trail.endColor = new Color(1f, 0.4f, 0.1f, 0f);
                    projectileViews[p.Id] = view;
                }
                view.transform.position = SimConvert.ToUnity(p.Position);
                view.GetComponent<TrailRenderer>().time = Sim.IsFrozen ? 1e6f : 0.18f; // the trail holds still while time is frozen
            }
            var gone = new List<int>();
            foreach (var id in projectileViews.Keys) if (!seen.Contains(id)) gone.Add(id);
            foreach (var id in gone) { DestroyImmediate(projectileViews[id]); projectileViews.Remove(id); }

            feedback.Sync(Sim, localIndex);
            effects.Sync(Sim);
            float freezeTarget = Sim.IsFrozen ? 1f : 0f;
            retro.Freeze = snapCamera ? freezeTarget : Mathf.MoveTowards(retro.Freeze, freezeTarget, deltaTime * 5f);
            cameraRig.Update(Sim.Fighters[localIndex], Sim.Fighters[1 - localIndex], deltaTime, snapCamera);
            for (int i = 0; i < 2; i++) fighters[i].Sync(Sim.Fighters[i], Sim.T, Camera.transform.rotation);
            hud.Sync(Sim, localIndex, Online ? "Opponent" : "Bot", isClient);
        }

        /// <summary>Renders the world camera then the HUD camera into a texture (used by the headless capture).</summary>
        public void RenderAll(RenderTexture target)
        {
            Canvas.ForceUpdateCanvases();
            foreach (var cam in new[] { Camera, uiCamera })
            {
                cam.targetTexture = target;
                cam.Render();
                cam.targetTexture = null;
            }
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
                         : Sim.Phase == Phase.RoundOver ? "ROUND OVER" : "MATCH OVER";
            if (tunablesError != null) GUI.Label(new Rect(12, Screen.height - 60, 900, 24), tunablesError);
            if (look?.Error != null) GUI.Label(new Rect(12, Screen.height - 40, 900, 24), look.Error);
            if (replayMessage != null && Time.unscaledTime < replayMessageUntil) GUI.Label(new Rect(12, Screen.height - 20, 900, 24), replayMessage);
            if (menuOpen) DrawMenu();
            if (!showDebug) return;

            // F1 debug overlay (the HUD is the player-facing view).
            string restartKeys = isClient ? "" : "  Enter restart  F5 reload tunables  F9 save replay";
            GUI.Box(new Rect(8, 100, 760, 140), GUIContent.none);
            GUI.Label(new Rect(16, 104, 900, 140),
                $"Turn {Sim.Turn}   {phase}\n" +
                $"tick {Sim.Tick}   combat tick {Sim.CombatTick}\n" +
                $"You ({FighterColorNames[localIndex]})  HP {me.Hp}/{me.MaxHp}  AP {me.ActionPoints}  {(me.LockedOn ? "LOCKED" : "free")}   {Describe(me)}\n" +
                $"{themName}  HP {them.Hp}/{them.MaxHp}  AP {them.ActionPoints}   {Describe(them)}\n" +
                $"Your hits: melee {Sim.HitDamage(me, ActionKind.Melee, them)} / ranged {Sim.HitDamage(me, ActionKind.Ranged, them)}   Upgrades: {Owned(me)}\n" +
                $"{themName} hits: melee {Sim.HitDamage(them, ActionKind.Melee, me)} / ranged {Sim.HitDamage(them, ActionKind.Ranged, me)}   Upgrades: {Owned(them)}\n" +
                $"WASD move  J melee  K ranged  Space dash  Tab lock  Q/E orbit{restartKeys}  Esc multiplayer\n" +
                NetStatusLine());
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

        static string Owned(Fighter f)
        {
            var names = new List<string>();
            foreach (var u in Upgrades.Pool) if (f.Has(u)) names.Add(Upgrades.Name(u));
            return names.Count == 0 ? "none" : string.Join(", ", names);
        }

        string Describe(Fighter f)
        {
            if (!f.IsBusy) return "";
            var def = f.Frames;
            string part = f.ActionTick < def.Startup ? "startup" : def.IsActive(f.ActionTick) ? "ACTIVE" : "recovery";
            return $"{f.Action} {part} {f.ActionTick}/{def.Total}";
        }
    }
}
