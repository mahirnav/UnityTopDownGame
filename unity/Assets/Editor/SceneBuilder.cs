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
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) Build();
                EditorSceneManager.OpenScene(ScenePath);
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
