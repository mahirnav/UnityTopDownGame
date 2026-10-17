using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Rats.EditorTools
{
    /// <summary>
    /// Builds a windowed Windows player into unity/Builds/RATS/RATS.exe.
    ///   Unity.exe -batchmode -quit -projectPath unity -executeMethod Rats.EditorTools.PlayerBuild.Windows
    /// </summary>
    public static class PlayerBuild
    {
        public const string OutputPath = "Builds/RATS/RATS.exe";

        [MenuItem("RATS/Build Windows Player")]
        public static void Windows()
        {
            PlayerSettings.productName = "RATS";
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { SceneBuilder.ScenePath },
                locationPathName = OutputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });
            var s = report.summary;
            Debug.Log($"[PlayerBuild] {s.result} in {s.totalTime.TotalSeconds:F0}s, {s.totalErrors} error(s) -> {OutputPath}");
            if (Application.isBatchMode) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
