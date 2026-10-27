using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace OddsCab.Editor
{
    /// <summary>Predictable import budget for the locally authored reference-overhaul sound bank.</summary>
    public sealed class ReferenceAudioImporter : AssetPostprocessor
    {
        const string BankRoot = "Assets/_Game/Resources/Audio/ReferenceOverhaul/";
        const string LivingRoot = "Assets/_Game/Resources/Audio/LivingIstanbul/";

        public override uint GetVersion() => 3;

        void OnPreprocessAudio()
        {
            bool living = assetPath.StartsWith(LivingRoot, StringComparison.Ordinal);
            if (!living && !assetPath.StartsWith(BankRoot, StringComparison.Ordinal)) return;
            var importer = (AudioImporter)assetImporter;
            string name = Path.GetFileNameWithoutExtension(assetPath);
            bool guide = assetPath.Contains("/Guide/");
            bool stereo = (living && !guide) || name == "music_bosphorus" || name == "waves" || name == "rain" || name == "cafe";
            bool loop = stereo || name == "engine_idle" || name == "engine_load" || name == "road" || name == "night";
            importer.forceToMono = !stereo;
            importer.loadInBackground = false;
            var samples = importer.defaultSampleSettings;
            samples.loadType = AudioClipLoadType.DecompressOnLoad;
            samples.preloadAudioData = true;
            samples.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
            samples.sampleRateOverride = living ? 22050u : 44100u;
            samples.compressionFormat = loop ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
            samples.quality = .85f;
            importer.defaultSampleSettings = samples;
            // The bank uses the same small, predecoded budget on all supported targets.
            // Old platform overrides must not silently reintroduce first-play decoding.
            importer.ClearSampleSettingOverride("Standalone");
            importer.ClearSampleSettingOverride("Android");
            importer.ClearSampleSettingOverride("iOS");
        }
    }
}
