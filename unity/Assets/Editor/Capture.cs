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

            Shot(bootstrap, dir, "p0_start.png");
            while (!sim.IsFrozen) StepAndSync(bootstrap);
            bootstrap.SyncViews(0f, snapCamera: true); // settle the follow camera so only sim-driven visuals can differ
            var start = Shot(bootstrap, dir, "p0_frozen_start.png");
            bool caughtMidAction = sim.Projectiles.Count == 1 && sim.Fighters[1].Action == ActionKind.Dash;

            for (int frame = 0; frame < HoldFrames; frame++) StepAndSync(bootstrap);
            var end = Shot(bootstrap, dir, "p0_frozen_end.png");
            bool identical = start.AsSpan().SequenceEqual(end);
            Debug.Log($"[Capture] caught mid-action: {caughtMidAction}, frozen frames pixel-identical: {identical}");

            while (sim.IsFrozen) StepAndSync(bootstrap);
            for (int i = 0; i < 30; i++) StepAndSync(bootstrap);
            Shot(bootstrap, dir, "p0_after_resume.png");
            return identical && caughtMidAction ? 0 : 2;
        }

        static void StepAndSync(Bootstrap b)
        {
            b.Step();
            b.SyncViews(1f / 60f);
        }

        static byte[] Shot(Bootstrap b, string dir, string fileName)
        {
            const int w = 1280, h = 720;
            var rt = RenderTexture.GetTemporary(w, h, 24);
            var cam = b.Camera;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            cam.targetTexture = null;
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

        static string ArgValue(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
