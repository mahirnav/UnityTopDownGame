using UnityEditor;

namespace Rats.EditorTools
{
    /// <summary>
    /// Import settings for anything under a Mixamo folder, applied in code so nobody
    /// clicks through the FBX inspector: Humanoid rig, and clips that stay in place
    /// and don't loop (the sim owns position and timing, not the animation).
    /// </summary>
    public sealed class MixamoImport : AssetPostprocessor
    {
        bool IsMixamo => assetPath.Replace('\\', '/').Contains("/Mixamo/");

        void OnPreprocessModel()
        {
            if (!IsMixamo) return;
            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        }

        void OnPreprocessAnimation()
        {
            if (!IsMixamo) return;
            var importer = (ModelImporter)assetImporter;
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                clip.loopTime = false;
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
                clip.keepOriginalOrientation = true;
                clip.keepOriginalPositionY = true;
                clip.keepOriginalPositionXZ = true;
            }
            importer.clipAnimations = clips;
        }
    }
}
