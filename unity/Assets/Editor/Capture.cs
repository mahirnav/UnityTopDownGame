using System;
using System.IO;
using Rats.Game;
using Rats.Sim;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Rats.EditorTools
{
    /// <summary>
    /// Scripted run that renders screenshots without play mode or a window:
    ///   Unity.exe -batchmode -projectPath unity -executeMethod Rats.EditorTools.Capture.Run -captureDir &lt;dir&gt;
    /// Short turn: fighter 0 fires, fighter 1 dashes, the turn-end freeze catches both mid-action,
    /// hold 120 frames, resume. Exit 0 = frozen frames pixel-identical and both actions caught mid-flight.
    /// </summary>
    public static class Capture
    {
        const int HoldFrames = 120;

        public static void Run()
        {
            int exitCode = 1;
            try
            {
                exitCode = RunScenario(ArgValue("-captureDir") ?? "captures");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            EditorApplication.Exit(exitCode);
        }

        static int RunScenario(string dir)
        {
            Directory.CreateDirectory(dir);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var bootstrap = new GameObject("Bootstrap").AddComponent<Bootstrap>();
            var t = Tunables.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "tunables.txt")));
            t.TurnTicks = 90;
            bootstrap.Build(t);

            var p0 = new ScriptedInput();
            p0.Set(60, new InputFrame { Ranged = true });
            var p1 = new ScriptedInput();
            p1.Set(80, new InputFrame { Dash = true, MoveX = -0.5f, MoveZ = 1f });
            bootstrap.Inputs[0] = p0;
            bootstrap.Inputs[1] = p1;
            var sim = bootstrap.Sim;

            Shot(bootstrap, dir, "look_start.png");
            while (!sim.IsFrozen) StepAndSync(bootstrap);
            bootstrap.SyncViews(0f, snapCamera: true); // settle the follow camera so only sim-driven visuals can differ
            Shot(bootstrap, dir, "look_draft.png");
            var start = Shot(bootstrap, dir, "p0_frozen_start.png", worldOnly: true); // HUD timer counts down, so compare the world
            bool caughtMidAction = sim.Projectiles.Count == 1 && sim.Fighters[1].Action == ActionKind.Dash;

            for (int frame = 0; frame < HoldFrames; frame++) StepAndSync(bootstrap);
            var end = Shot(bootstrap, dir, "p0_frozen_end.png", worldOnly: true);
            int changed = ChangedPixels(start, end);
            bool identical = changed == 0; // tolerant of 1-2 step GPU noise (lights, soft shadows); a moving object changes far more
            Debug.Log($"[Capture] caught mid-action: {caughtMidAction}, frozen frames unchanged: {identical} ({changed} px changed beyond noise)");

            while (sim.IsFrozen) StepAndSync(bootstrap);
            for (int i = 0; i < 30; i++) StepAndSync(bootstrap);
            Shot(bootstrap, dir, "p0_after_resume.png");

            // A backstab up close: sparks, swing arc, pop-up. Then finish the round for the banner.
            var me = sim.Fighters[0];
            var them = sim.Fighters[1];
            me.Position = new Vec3(0, 0, 0);
            them.Position = new Vec3(1.3, 0, 0.2);
            them.Facing = new Vec3(1, 0, 0);
            them.LockedOn = false;
            var swing = new ScriptedInput();
            swing.Set(sim.Tick, new InputFrame { Melee = true });
            bootstrap.Inputs[0] = swing;
            bootstrap.Inputs[1] = new ScriptedInput();
            int hpBefore = them.Hp;
            for (int i = 0; i < 60 && them.Hp == hpBefore; i++) StepAndSync(bootstrap);
            StepAndSync(bootstrap);
            Shot(bootstrap, dir, "look_hit.png");
            them.Hp = 1;
            var finish = new ScriptedInput();
            for (int k = 0; k < 2000; k++) finish.Set(sim.Tick + k, new InputFrame { Melee = true }); // swing whenever possible
            bootstrap.Inputs[0] = finish;
            for (int i = 0; i < 2000 && sim.Phase != Phase.RoundOver && sim.Phase != Phase.MatchOver; i++)
            {
                them.Position = new Vec3(1.3, 0, 0.2); // keep the target in reach (the bot isn't driving it)
                StepAndSync(bootstrap);
            }
            StepAndSync(bootstrap);
            Shot(bootstrap, dir, "look_round.png");

            // The ring half closed and the opponent downed: fire band, downed pose, execute prompt.
            var late = Tunables.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "tunables.txt")));
            late.FireStartTicks = 0;
            late.RoundMaxTicks = 900;
            late.TurnTicks = 100000;
            bootstrap.Build(late);
            bootstrap.Inputs[0] = new ScriptedInput();
            bootstrap.Inputs[1] = new ScriptedInput();
            sim = bootstrap.Sim;
            for (int i = 0; i < 420; i++) StepAndSync(bootstrap);
            sim.Fighters[1].Hp = 0;
            StepAndSync(bootstrap);
            StepAndSync(bootstrap);
            Shot(bootstrap, dir, "look_fire.png");
            return identical && caughtMidAction ? 0 : 2;
        }

        static void StepAndSync(Bootstrap b)
        {
            b.Step();
            b.SyncViews(1f / 60f);
        }

        static byte[] Shot(Bootstrap b, string dir, string fileName, bool worldOnly = false)
        {
            const int w = 1280, h = 720;
            var rt = RenderTexture.GetTemporary(w, h, 24);
            if (worldOnly)
            {
                // Raw world render for the frozen comparison: the retro color banding turns 1-bit GPU noise into
                // whole-pixel flips, and this check is about sim-driven state, not the post effect.
                var retro = b.Camera.GetComponent<RetroEffect>();
                retro.enabled = false;
                b.Camera.targetTexture = rt;
                b.Camera.Render();
                b.Camera.targetTexture = null;
                retro.enabled = true;
            }
            else b.RenderAll(rt);
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);

            var png = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);
            var path = Path.Combine(dir, fileName);
            File.WriteAllBytes(path, png);

            var sim = b.Sim;
            var proj = sim.Projectiles.Count > 0 ? sim.Projectiles[0].Position.ToString() : "-";
            Debug.Log($"[Capture] {path}  tick={sim.Tick}  combat={sim.CombatTick}  phase={sim.Phase}  " +
                      $"f1={sim.Fighters[1].Action}:{sim.Fighters[1].ActionTick}  projectile={proj}");
            return png;
        }

        /// <summary>Pixels whose color moved by more than a small noise threshold between two PNGs.</summary>
        static int ChangedPixels(byte[] pngA, byte[] pngB, int threshold = 8)
        {
            var a = new Texture2D(2, 2);
            var b = new Texture2D(2, 2);
            a.LoadImage(pngA);
            b.LoadImage(pngB);
            var pa = a.GetPixels32();
            var pb = b.GetPixels32();
            int changed = 0;
            for (int i = 0; i < pa.Length; i++)
                if (Math.Abs(pa[i].r - pb[i].r) > threshold || Math.Abs(pa[i].g - pb[i].g) > threshold || Math.Abs(pa[i].b - pb[i].b) > threshold) changed++;
            UnityEngine.Object.DestroyImmediate(a);
            UnityEngine.Object.DestroyImmediate(b);
            return changed;
        }

        static string ArgValue(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
