using UnityEditor;
using UnityEngine;

namespace Rats.EditorTools
{
    /// <summary>
    /// Import settings for Poly Haven assets (Resources/PolyHaven), applied in code: normal maps flagged as
    /// normal maps, the HDRI kept at 4K and readable (the game finds the sun in it), textures filtered smoothly.
    /// </summary>
    public sealed class PolyHavenImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            var path = assetPath.Replace('\\', '/');
            if (!path.Contains("/PolyHaven/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4;
            if (path.Contains("/Sky/"))
            {
                importer.textureType = TextureImporterType.Default;
                importer.textureShape = TextureImporterShape.Texture2D;
                importer.maxTextureSize = 4096;
                importer.mipmapEnabled = true;
                importer.isReadable = true;
                importer.wrapModeU = TextureWrapMode.Repeat;
                importer.wrapModeV = TextureWrapMode.Clamp;
            }
            else if (path.Contains("_nor"))
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.maxTextureSize = 2048;
            }
            else
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.maxTextureSize = 2048;
            }
        }
    }
}
