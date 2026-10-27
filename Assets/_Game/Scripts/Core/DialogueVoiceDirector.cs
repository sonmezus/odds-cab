using UnityEngine;
using UnityEngine.Audio;

namespace OddsCab
{
    public enum GuideCue { Welcome, Pickup, QuestionIntro, Correct, Incorrect, Hint, LowTime, DistrictArrival, Night, Rain, Fog, Count }

    /// <summary>Optional, baked synthetic guide. One narrator source; no network or speech synthesis in a build.</summary>
    public sealed class DialogueVoiceDirector
    {
        static readonly string[] CueNames =
        { "welcome", "pickup", "question", "correct", "incorrect", "hint", "low_time", "district", "night", "rain", "fog" };
        static readonly float[] Cooldowns = { 120f, 12f, 8f, 2f, 2f, 3f, 15f, 20f, 120f, 120f, 120f };
        readonly AudioClip[,] _clips = new AudioClip[2, (int)GuideCue.Count];
        readonly double[] _nextAllowed = new double[(int)GuideCue.Count];
        readonly AudioSource _source;
        int _priority = 100;
        float _gain = 1f;
        bool _paused;

        public bool Enabled { get; private set; } = true;
        public bool Ready { get; private set; } = true;
        public bool IsSpeaking => _source != null && (_source.isPlaying || _paused);

        public DialogueVoiceDirector(Transform parent, AudioMixerGroup output)
        {
            for (int language = 0; language < 2; language++)
                for (int cue = 0; cue < (int)GuideCue.Count; cue++)
                {
                    _clips[language, cue] = Resources.Load<AudioClip>("Audio/LivingIstanbul/Guide/" +
                        (language == 0 ? "tr_" : "en_") + CueNames[cue]);
                    if (_clips[language, cue] == null) Ready = false;
                }
            var child = new GameObject("OptionalSpokenGuide");
            child.transform.SetParent(parent, false);
            _source = child.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            _source.dopplerLevel = 0f;
            _source.priority = 8;
            _source.volume = 0f;
            _source.outputAudioMixerGroup = output;
        }

        public void SetEnabled(bool enabled)
        {
            Enabled = enabled;
            if (!enabled) { _source.Stop(); _paused = false; }
        }

        public float Play(GuideCue cue, Language language, float gain = 1f)
        {
            int index = (int)cue;
            if (!Enabled || index < 0 || index >= (int)GuideCue.Count) return 0f;
            var clip = _clips[Mathf.Clamp((int)language, 0, 1), index];
            if (clip == null) return 0f;
            double now = AudioSettings.dspTime;
            if (now < _nextAllowed[index]) return 0f;
            int priority = Priority(cue);
            if (IsSpeaking && priority > _priority) return 0f;
            _source.Stop();
            _source.clip = clip;
            _source.pitch = 1f;
            _source.panStereo = 0f;
            _gain = Mathf.Clamp01(gain);
            _priority = priority;
            _paused = false;
            _nextAllowed[index] = now + Cooldowns[index];
            _source.Play();
            return clip.length;
        }

        public void Tick(float volume)
        {
            if (_source == null) return;
            _source.volume = Mathf.Clamp01(volume * _gain);
            bool pause = Time.timeScale <= 0f;
            if (pause && !_paused && _source.isPlaying) { _source.Pause(); _paused = true; }
            else if (!pause && _paused) { _source.UnPause(); _paused = false; }
        }

        public static int Priority(GuideCue cue)
        {
            switch (cue)
            {
                case GuideCue.LowTime: return 5;
                case GuideCue.Correct:
                case GuideCue.Incorrect: return 10;
                case GuideCue.Hint: return 15;
                case GuideCue.QuestionIntro: return 20;
                case GuideCue.DistrictArrival: return 25;
                case GuideCue.Pickup: return 30;
                default: return 40;
            }
        }
    }
}
