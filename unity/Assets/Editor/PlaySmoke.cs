using Rats.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Rats.EditorTools
{
    /// <summary>
    /// Enters real play mode in the Bootstrap scene, lets it run ~2 s, and exits.
    /// Fails on any error logged from our code or if the sim never ticked.
    ///   Unity.exe -batchmode -projectPath unity -executeMethod Rats.EditorTools.PlaySmoke.Run
    /// Entering play mode reloads the script domain, so state lives in SessionState
    /// and the hooks are re-installed by the static constructor after the reload.
    /// </summary>
    [InitializeOnLoad]
    public static class PlaySmoke
    {
        const string ActiveKey = "Rats.PlaySmoke.Active";
        const double RunSeconds = 2.0;
        static int errors;
        static double playStarted = -1;

        static PlaySmoke()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            Application.logMessageReceived += (msg, stack, type) =>
            {
                bool isError = type == LogType.Error || type == LogType.Exception || type == LogType.Assert;
                if (isError && (msg + stack).Contains("Rats.")) errors++;
            };
            EditorApplication.update += Tick;
        }

        public static void Run()
        {
            SessionState.SetBool(ActiveKey, true);
            EditorSceneManager.OpenScene(SceneBuilder.ScenePath);
            EditorApplication.EnterPlaymode();
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            if (playStarted < 0) playStarted = EditorApplication.timeSinceStartup;
            if (EditorApplication.timeSinceStartup - playStarted < RunSeconds) return;
            EditorApplication.update -= Tick;
            SessionState.EraseBool(ActiveKey);

            var bootstrap = Object.FindAnyObjectByType<Bootstrap>();
            int tick = bootstrap != null && bootstrap.Sim != null ? bootstrap.Sim.Tick : -1;
            bool ok = errors == 0 && tick > 0;
            Debug.Log($"[PlaySmoke] sim tick after {RunSeconds}s: {tick}, errors from Rats code: {errors} -> {(ok ? "PASS" : "FAIL")}");
            EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
