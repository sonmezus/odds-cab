using UnityEditor;
using UnityEngine;

namespace OddsCab.Editor
{
    /// <summary>Explicit import policy for the reference kit's technical surface maps.</summary>
    public sealed class ReferenceKitImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/_Game/Resources/ReferenceKit/")) return;
            var importer = (TextureImporter)assetImporter;
            bool normal = assetPath.Contains("/Normals/");
            bool backdrop = assetPath.Contains("BosphorusShoreline");
            bool cloud = assetPath.Contains("ReferenceCumulus");
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            // Cloud RGB is a luminance shading mask for ReferenceCloud.shader, not a colour texture.
            importer.sRGBTexture = !normal && !cloud;
            importer.convertToNormalmap = false;
            importer.mipmapEnabled = true;
            importer.isReadable = false;
            importer.wrapMode = backdrop || cloud ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = normal || assetPath.Contains("/Surfaces/") ? 4 : 2;
            importer.alphaIsTransparency = backdrop || cloud;
            // Thin minarets and roof edges must survive distant mip levels. Keep the native
            // 1774 x 887 panorama without an unnecessary power-of-two resampling pass.
            if (backdrop)
            {
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.mipMapsPreserveCoverage = true;
                importer.alphaTestReferenceValue = 0.4f;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.compressionQuality = 100;
            }
            importer.maxTextureSize = backdrop ? 2048 : 1024;
            foreach (string platform in new[] { "Android", "iPhone" })
            {
                var settings = importer.GetPlatformTextureSettings(platform);
                settings.name = platform;
                settings.overridden = true;
                settings.maxTextureSize = backdrop ? 2048 : 1024;
                settings.format = cloud || backdrop ? TextureImporterFormat.ASTC_4x4 : TextureImporterFormat.ASTC_6x6;
                settings.compressionQuality = 100;
                settings.crunchedCompression = false;
                importer.SetPlatformTextureSettings(settings);
            }
        }
    }
}
