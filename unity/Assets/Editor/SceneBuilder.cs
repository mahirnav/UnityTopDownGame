using Rats.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rats.EditorTools
{
    /// <summary>
    /// Generates the one Bootstrap scene from code, so nobody has to build it by hand.
    /// Terminal: Unity.exe -batchmode -quit -projectPath unity -executeMethod Rats.EditorTools.SceneBuilder.Build
    /// Also opens that scene automatically when the editor starts on an empty scene.
    /// </summary>
    [InitializeOnLoad]
    public static class SceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Bootstrap.unity";

        static SceneBuilder()
        {
            if (Application.isBatchMode) return;
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (!string.IsNullOrEmpty(SceneManager.GetActiveScene().path)) return;
                // Only open, never regenerate: rebuilding writes new object IDs into the scene file,
                // which would cause merge conflicts every time someone opens the editor.
                // (The asset database can report the scene missing this early, so check the disk.)
                if (System.IO.File.Exists(ScenePath)) EditorSceneManager.OpenScene(ScenePath);
                else Debug.LogWarning($"[SceneBuilder] {ScenePath} missing; use RATS > Rebuild Bootstrap Scene.");
            };
        }

        [MenuItem("RATS/Rebuild Bootstrap Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Bootstrap").AddComponent<Bootstrap>();
            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"[SceneBuilder] wrote {ScenePath}");
        }
    }
}
