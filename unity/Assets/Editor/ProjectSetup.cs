using UnityEditor;
using UnityEngine;

namespace Rats.EditorTools
{
    /// <summary>
    /// Project-wide settings applied from code so they're reproducible:
    ///   Unity.exe -batchmode -quit -projectPath unity -executeMethod Rats.EditorTools.ProjectSetup.Apply
    /// Linear color space: light adds up physically, which HDR skies, tone mapping and glow need.
    /// </summary>
    public static class ProjectSetup
    {
        [MenuItem("RATS/Apply Project Settings")]
        public static void Apply()
        {
            if (PlayerSettings.colorSpace != ColorSpace.Linear)
            {
                PlayerSettings.colorSpace = ColorSpace.Linear;
                Debug.Log("[ProjectSetup] color space set to Linear");
            }
            AssetDatabase.SaveAssets();
        }
    }
}
