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
            IncludeCodeOnlyShaders();

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

        /// <summary>
        /// The world is built in code, so no scene or asset references these shaders and builds would strip
        /// them (magenta objects), along with fog. Keep them explicitly. Add any shader new code uses here.
        /// </summary>
        static readonly string[] CodeOnlyShaders =
        {
            "Standard", "Unlit/Color", "Sprites/Default", "UI/Default", "Hidden/RatsRetro",
            "Legacy Shaders/Particles/Additive", "Legacy Shaders/Particles/Alpha Blended",
        };

        static void IncludeCodeOnlyShaders()
        {
            var settings = new SerializedObject(UnityEngine.Rendering.GraphicsSettings.GetGraphicsSettings());
            var shaders = settings.FindProperty("m_AlwaysIncludedShaders");
            foreach (var name in CodeOnlyShaders)
            {
                var shader = Shader.Find(name);
                if (shader == null) { Debug.LogError($"[PlayerBuild] shader not found: {name}"); continue; }
                bool present = false;
                for (int i = 0; i < shaders.arraySize; i++)
                    if (shaders.GetArrayElementAtIndex(i).objectReferenceValue == shader) present = true;
                if (present) continue;
                shaders.InsertArrayElementAtIndex(shaders.arraySize);
                shaders.GetArrayElementAtIndex(shaders.arraySize - 1).objectReferenceValue = shader;
            }
            settings.FindProperty("m_FogStripping").intValue = 1; // custom: keep the modes below
            settings.FindProperty("m_FogKeepLinear").boolValue = true;
            settings.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }
    }
}
