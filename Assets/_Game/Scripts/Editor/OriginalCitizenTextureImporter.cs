using UnityEditor;
using UnityEngine;

namespace OddsCab.Editor
{
    /// <summary>The original crowd uses one small shared atlas, never passenger textures.</summary>
    public sealed class OriginalCitizenTextureImporter : AssetPostprocessor
    {
        public override uint GetVersion() => 1;
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/_Game/Resources/LivingIstanbul/Citizens/", System.StringComparison.Ordinal)) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.isReadable = false;
            importer.alphaIsTransparency = false;
            importer.maxTextureSize = 512;
            importer.filterMode = FilterMode.Trilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            foreach (string platform in new[] { "Android", "iPhone" })
            {
                var settings = importer.GetPlatformTextureSettings(platform);
                settings.name = platform; settings.overridden = true; settings.maxTextureSize = 512;
                settings.format = TextureImporterFormat.ASTC_4x4; settings.compressionQuality = 100;
                settings.crunchedCompression = false; importer.SetPlatformTextureSettings(settings);
            }
        }
    }
}
