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
    /// Scripted run that renders screenshots without play mode or a window, so the
    /// freeze can be checked from the terminal:
    ///   Unity.exe -batchmode -projectPath unity -executeMethod Rats.EditorTools.Capture.Run -captureDir &lt;dir&gt;
    /// Walk, fire, swing, freeze mid-swing with the projectile in flight, hold, resume.
    /// Exit code 0 = frozen frames were pixel-identical and the run finished.
    /// </summary>
    public static class Capture
    {
        const int FreezeTick = 40;
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
            bootstrap.Build();

            var input = new ScriptedInput();
            for (int t = 0; t < 20; t++) input.Set(t, new InputFrame { MoveX = 0.6f, MoveZ = 1f });
            input.Set(20, new InputFrame { Fire = true });
            input.Set(25, new InputFrame { Melee = true });
            bootstrap.InputSource = input;
            var sim = bootstrap.Sim;

            var animator = bootstrap.Character.GetComponentInChildren<Animator>();
            var hand = animator != null ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            var body = bootstrap.Character.transform;
            var idleHand = hand != null ? body.InverseTransformPoint(hand.position) : Vector3.zero;

            while (sim.Tick < FreezeTick) StepAndSync(bootstrap);
            sim.Freeze();
            bootstrap.SyncViews(10f); // long delta settles the follow camera, so only sim-driven visuals can differ
            var start = Shot(bootstrap, dir, "unity_frozen_start.png");

            // Regression: an animation path that silently never posed the model still gave identical frames.
            bool posed = true;
            if (hand != null)
            {
                float moved = Vector3.Distance(body.InverseTransformPoint(hand.position), idleHand);
                posed = moved > 0.1f;
                Debug.Log($"[Capture] right hand moved {moved:F2} m from idle to frozen swing pose: {(posed ? "posed" : "NOT POSED")}");
            }

            var clock = new TickClock();
            for (int frame = 0; frame < HoldFrames; frame++)
            {
                int ticks = clock.Advance(1.0 / 60.0, sim.IsFrozen);
                for (int i = 0; i < ticks; i++) sim.Step(input.Read(sim.Tick));
                bootstrap.SyncViews(1f / 60f);
            }
            var end = Shot(bootstrap, dir, "unity_frozen_end.png");
            bool identical = start.AsSpan().SequenceEqual(end);
            Debug.Log($"[Capture] frozen frames pixel-identical: {identical}");

            sim.Resume();
            while (sim.Tick < FreezeTick + 60) StepAndSync(bootstrap);
            Shot(bootstrap, dir, "unity_after_resume.png");
            return identical && posed ? 0 : 2;
        }

        static void StepAndSync(Bootstrap b)
        {
            b.Sim.Step(b.InputSource.Read(b.Sim.Tick));
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
            Debug.Log($"[Capture] {path}  tick={sim.Tick}  frozen={sim.IsFrozen}  " +
                      $"swing={sim.Player.SwingProgress:F3}  projectile={proj}  humanoid={b.Character.HasHumanoid}");
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
