using System;
using UnityEditor;
using UnityEngine;

namespace OddsCab.Editor
{
    /// <summary>Keep the supplied 2K passenger maps intact, including the portrait AO bake.</summary>
    public sealed class PassengerQualityImportPolicy : AssetPostprocessor
    {
        public override uint GetVersion() => 1;

        void OnPreprocessTexture()
        {
            bool source = assetPath.StartsWith("Assets/_Game/Resources/ModelKYüzveVücutKemikli/", StringComparison.Ordinal);
            bool baked = assetPath.StartsWith("Assets/_Game/Resources/PassengersArt/Baked/", StringComparison.Ordinal);
            if (!source && !baked) return;
            bool normal = assetPath.EndsWith("texture_normal.png", StringComparison.Ordinal);
            bool color = assetPath.EndsWith("texture_diffuse.png", StringComparison.Ordinal);
            bool packed = assetPath.EndsWith("MetallicSmoothness.png", StringComparison.Ordinal);
            var importer = (TextureImporter)assetImporter;
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = color;
            importer.convertToNormalmap = false;
            importer.maxTextureSize = 2048;
            importer.mipmapEnabled = true;
            importer.isReadable = false;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4;
            importer.alphaIsTransparency = false;
            importer.alphaSource = packed ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.crunchedCompression = false;
            foreach (string platform in new[] { "Android", "iPhone" })
            {
                var settings = importer.GetPlatformTextureSettings(platform);
                settings.name = platform;
                settings.overridden = true;
                settings.maxTextureSize = 2048;
                settings.format = TextureImporterFormat.ASTC_4x4;
                settings.compressionQuality = 100;
                settings.crunchedCompression = false;
                importer.SetPlatformTextureSettings(settings);
            }
        }
    }
}
