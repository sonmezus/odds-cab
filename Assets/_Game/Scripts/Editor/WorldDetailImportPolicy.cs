using UnityEditor;
using UnityEngine;

namespace OddsCab.Editor
{
    public sealed class WorldDetailImportPolicy : AssetPostprocessor
    {
        public override uint GetVersion() => 1;
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/_Game/Resources/WorldDetail/", System.StringComparison.Ordinal)) return;
            var importer = (TextureImporter)assetImporter;
            bool normal = assetPath.Contains("/Normals/");
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal; importer.convertToNormalmap = false;
            importer.mipmapEnabled = true; importer.isReadable = false;
            importer.filterMode = FilterMode.Trilinear; importer.wrapMode = TextureWrapMode.Repeat;
            importer.anisoLevel = 4; importer.maxTextureSize = 1024;
            foreach (string platform in new[] { "Android", "iPhone" })
            {
                var settings = importer.GetPlatformTextureSettings(platform);
                settings.name = platform; settings.overridden = true; settings.maxTextureSize = 1024;
                settings.format = TextureImporterFormat.ASTC_6x6; settings.compressionQuality = 100;
                settings.crunchedCompression = false; importer.SetPlatformTextureSettings(settings);
            }
        }

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/_Game/Resources/Audio/WorldDetail/", System.StringComparison.Ordinal)) return;
            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = true; importer.loadInBackground = false;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad; settings.preloadAudioData = true;
            settings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate; settings.sampleRateOverride = 22050;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            importer.defaultSampleSettings = settings;
        }
    }
}
