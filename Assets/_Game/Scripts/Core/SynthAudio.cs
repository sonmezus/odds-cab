using UnityEngine;
using UnityEngine.Audio;

namespace OddsCab
{
    /// <summary>
    /// Baked original audio, independent pooled voices, stable musical notes, organic nonverbal pitch
    /// variation, layered mechanical sound and a softly ducked Bosphorus soundscape.
    /// Clips and sources are prepared in Awake. Playback and frame updates allocate no managed memory.
    /// </summary>
    public sealed class SynthAudio : MonoBehaviour
    {
        public static SynthAudio Instance { get; private set; }
        public const int VoiceCount = 18;
        const string BankPath = "Audio/ReferenceOverhaul/";

        [Header("Bus levels")]
        [SerializeField, Range(0f, 1f)] float _interfaceGain = .82f;
        [SerializeField, Range(0f, 1f)] float _feedbackGain = .86f;
        [SerializeField, Range(0f, 1f)] float _mechanicalGain = .78f;
        [SerializeField, Range(0f, 1f)] float _voiceGain = .75f;
        [SerializeField, Range(0f, 1f)] float _ambienceGain = .64f;
        [SerializeField, Range(0f, 1f)] float _musicGain = .42f;
        [Header("Optional existing Audio Mixer outputs")]
        [SerializeField] AudioMixerGroup _interfaceOutput;
        [SerializeField] AudioMixerGroup _feedbackOutput;
        [SerializeField] AudioMixerGroup _mechanicalOutput;
        [SerializeField] AudioMixerGroup _voiceOutput;
        [SerializeField] AudioMixerGroup _ambienceOutput;
        [SerializeField] AudioMixerGroup _musicOutput;

        static readonly string[] CueNames =
        {
            "ui_press", "ui_hover", "ui_click", "ui_confirm", "panel_open", "panel_close", "correct", "wrong",
            "pickup", "rewind", "brake_warning", "door_open", "door_close", "brake_squeal", "horn", "wiper",
            "gear_shift", "hint", "coin", "timer", "bell_1", "bell_2", "bell_3", "bell_4", "voice_0",
            "gull_0", "bird_0", "ferry_horn"
        };

        readonly AudioSource[] _voices = new AudioSource[VoiceCount];
        readonly AudioLowPassFilter[] _voiceFilters = new AudioLowPassFilter[VoiceCount];
        readonly double[] _voiceEnds = new double[VoiceCount];
        readonly int[] _voicePriorities = new int[VoiceCount];
        readonly float[] _voiceGains = new float[VoiceCount];
        readonly CabAudioBus[] _voiceBuses = new CabAudioBus[VoiceCount];
        readonly AudioClip[] _clips = new AudioClip[(int)CabAudioCue.Count];
        readonly double[] _nextCue = new double[(int)CabAudioCue.Count];
        readonly AudioClip[] _passengerClips = new AudioClip[5];
        readonly AudioClip[] _gullClips = new AudioClip[3];
        readonly AudioClip[] _birdClips = new AudioClip[3];
        AudioSource _music, _engine, _engineLoad, _road, _waves, _night, _rain, _cafe;
        AudioSource _crowd, _market, _rainRoofs, _harbor;
        AudioLowPassFilter _crowdFilter, _marketFilter, _roofsFilter, _harborFilter;
        DialogueVoiceDirector _guide;
        int _district;
        bool _guideLowTimeIssued;
        AudioLowPassFilter _engineFilter, _loadFilter, _roadFilter, _wavesFilter, _rainFilter, _cafeFilter;
        float _sfxVolume = .8f, _musicVolume = .6f;
        float _speed, _previousSpeed, _engineMix, _muffle, _duck = 1f, _duckTarget = 1f;
        float _ambiencePresence = 1f;
        double _duckUntil, _nextGull, _nextBird, _nextFerry;
        bool _braking, _bySea, _isNight, _outside = true, _wipers;
        int _gear = 1, _wiperDirection, _wiperStroke;
        uint _random = 0x512CAB37u;
        WeatherPreset _weather = (WeatherPreset)(-1);

        public bool BankReady { get; private set; }
        public float LastPassengerVoiceDuration { get; private set; }
        public bool SpokenGuideEnabled => _guide != null && _guide.Enabled;
        public bool SpokenGuideReady => _guide != null && _guide.Ready;
        public int ActiveVoiceCount
        {
            get
            {
                int count = 0;
                double now = AudioSettings.dspTime;
                for (int i = 0; i < VoiceCount; i++) if (_voiceEnds[i] > now) count++;
                return count;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _random ^= unchecked((uint)System.Environment.TickCount);
            BankReady = true;
            for (int i = 0; i < _clips.Length; i++)
            {
                _clips[i] = Resources.Load<AudioClip>(BankPath + CueNames[i]);
                if (_clips[i] == null) BankReady = false;
            }
            for (int i = 0; i < _passengerClips.Length; i++)
            {
                _passengerClips[i] = Resources.Load<AudioClip>(BankPath + "voice_" + i);
                if (_passengerClips[i] == null) BankReady = false;
            }
            for (int i = 0; i < _gullClips.Length; i++)
            {
                _gullClips[i] = Resources.Load<AudioClip>(BankPath + "gull_" + i);
                _birdClips[i] = Resources.Load<AudioClip>(BankPath + "bird_" + i);
                if (_gullClips[i] == null || _birdClips[i] == null) BankReady = false;
            }
            for (int i = 0; i < VoiceCount; i++)
            {
                _voices[i] = CreateSource("CueVoice_" + i, null, false, CabAudioBus.Interface);
                _voiceFilters[i] = _voices[i].gameObject.AddComponent<AudioLowPassFilter>();
                _voiceFilters[i].enabled = false;
            }
            _music = CreateLoop("Music", "music_bosphorus", CabAudioBus.Music);
            _engine = CreateLoop("EngineIdle", "engine_idle", CabAudioBus.Mechanical);
            _engineLoad = CreateLoop("EngineLoad", "engine_load", CabAudioBus.Mechanical);
            _road = CreateLoop("TyreContact", "road", CabAudioBus.Mechanical);
            _waves = CreateLoop("BosphorusWaves", "waves", CabAudioBus.Ambience);
            _night = CreateLoop("NightAir", "night", CabAudioBus.Ambience);
            _rain = CreateLoop("Rain", "rain", CabAudioBus.Ambience);
            _cafe = CreateLoop("CafeAir", "cafe", CabAudioBus.Ambience);
            _crowd = CreateLoop("IstanbulDayCrowd", "crowd_day", CabAudioBus.Ambience, true);
            _market = CreateLoop("BazaarStalls", "market_day", CabAudioBus.Ambience, true);
            _rainRoofs = CreateLoop("RainOnRoofsAndGlass", "rain_roofs", CabAudioBus.Ambience, true);
            _harbor = CreateLoop("QuietHarbor", "night_harbor", CabAudioBus.Ambience, true);
            _engineFilter = AddFilter(_engine);
            _loadFilter = AddFilter(_engineLoad);
            _roadFilter = AddFilter(_road);
            _wavesFilter = AddFilter(_waves);
            _rainFilter = AddFilter(_rain);
            _cafeFilter = AddFilter(_cafe);
            _crowdFilter = AddFilter(_crowd);
            _marketFilter = AddFilter(_market);
            _roofsFilter = AddFilter(_rainRoofs);
            _harborFilter = AddFilter(_harbor);
            _guide = new DialogueVoiceDirector(transform, _voiceOutput);
            ApplyVolumes();
            double now = AudioSettings.dspTime;
            _nextGull = now + Range(10f, 20f);
            _nextBird = now + Range(5f, 11f);
            _nextFerry = now + Range(50f, 90f);
            if (!BankReady) Debug.LogWarning("[ODDS CAB Audio] Original audio bank is incomplete. Check Resources/Audio/ReferenceOverhaul.");
        }

        AudioSource CreateSource(string name, AudioClip clip, bool loop, CabAudioBus bus)
        {
            var child = new GameObject(name);
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.clip = clip;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.volume = 0f;
            source.priority = loop ? 160 : 128;
            source.outputAudioMixerGroup = MixerFor(bus);
            return source;
        }

        AudioSource CreateLoop(string name, string resource, CabAudioBus bus, bool livingIstanbul = false)
        {
            var clip = Resources.Load<AudioClip>((livingIstanbul ? "Audio/LivingIstanbul/" : BankPath) + resource);
            if (clip == null) BankReady = false;
            var source = CreateSource(name, clip, true, bus);
            if (clip != null) source.Play();
            return source;
        }

        static AudioLowPassFilter AddFilter(AudioSource source)
        {
            var filter = source.gameObject.AddComponent<AudioLowPassFilter>();
            filter.cutoffFrequency = 18000f;
            filter.lowpassResonanceQ = 1f;
            return filter;
        }

        AudioMixerGroup MixerFor(CabAudioBus bus)
        {
            switch (bus)
            {
                case CabAudioBus.Interface: return _interfaceOutput;
                case CabAudioBus.Feedback: return _feedbackOutput;
                case CabAudioBus.Mechanical: return _mechanicalOutput;
                case CabAudioBus.Voice: return _voiceOutput;
                case CabAudioBus.Music: return _musicOutput;
                default: return _ambienceOutput;
            }
        }

        float BusGain(CabAudioBus bus)
        {
            switch (bus)
            {
                case CabAudioBus.Interface: return _interfaceGain;
                case CabAudioBus.Feedback: return _feedbackGain;
                case CabAudioBus.Mechanical: return _mechanicalGain;
                case CabAudioBus.Voice: return _voiceGain;
                case CabAudioBus.Music: return _musicGain;
                default: return _ambienceGain;
            }
        }

        public void ApplyVolumes()
        {
            var data = SaveSystem.Data;
            _sfxVolume = data != null ? Mathf.Clamp01(data.sfxVolume) : .8f;
            _musicVolume = data != null ? Mathf.Clamp01(data.musicVolume) : .6f;
        }

        public void SetBusVolume(CabAudioBus bus, float gain)
        {
            gain = Mathf.Clamp01(gain);
            switch (bus)
            {
                case CabAudioBus.Interface: _interfaceGain = gain; break;
                case CabAudioBus.Feedback: _feedbackGain = gain; break;
                case CabAudioBus.Mechanical: _mechanicalGain = gain; break;
                case CabAudioBus.Voice: _voiceGain = gain; break;
                case CabAudioBus.Music: _musicGain = gain; break;
                default: _ambienceGain = gain; break;
            }
        }

        float Range(float min, float max) => Mathf.Lerp(min, max, AudioCuePolicy.Unit(ref _random));

        float Play(CabAudioCue cue, AudioClip clip = null, float gain = 1f, float pitch = 1f, float pan = 0f)
        {
            int index = (int)cue;
            if (index < 0 || index >= _clips.Length) return 0f;
            if (clip == null) clip = _clips[index];
            if (clip == null) return 0f;
            var settings = AudioCuePolicy.Get(cue);
            double now = AudioSettings.dspTime;
            if (now < _nextCue[index]) return 0f;
            int voice = AudioCuePolicy.SelectVoice(now, settings.Priority, _voiceEnds, _voicePriorities);
            if (voice < 0) return 0f;
            _nextCue[index] = now + settings.Cooldown;
            var source = _voices[voice];
            source.Stop();
            source.clip = clip;
            source.priority = settings.Priority;
            source.pitch = Mathf.Clamp(pitch * AudioCuePolicy.SamplePitch(settings.PitchCents, ref _random), .65f, 1.6f);
            source.panStereo = Mathf.Clamp(pan, -1f, 1f);
            source.ignoreListenerPause = settings.Bus == CabAudioBus.Interface;
            source.outputAudioMixerGroup = MixerFor(settings.Bus);
            bool muffle = settings.Bus == CabAudioBus.Mechanical || settings.Bus == CabAudioBus.Ambience;
            _voiceFilters[voice].enabled = muffle;
            if (muffle) _voiceFilters[voice].cutoffFrequency = Mathf.Lerp(18000f, 3400f, _muffle);
            _voiceGains[voice] = settings.Gain * Mathf.Clamp01(gain);
            _voiceBuses[voice] = settings.Bus;
            source.volume = _voiceGains[voice] * BusGain(settings.Bus) * _sfxVolume;
            float duration = clip.length / source.pitch;
            _voiceEnds[voice] = now + duration;
            _voicePriorities[voice] = settings.Priority;
            source.Play();
            if (settings.Duck < 1f)
            {
                if (now >= _duckUntil) _duckTarget = settings.Duck;
                else _duckTarget = Mathf.Min(_duckTarget, settings.Duck);
                _duckUntil = System.Math.Max(_duckUntil, now + Mathf.Min(duration + .25f, 2f));
            }
            return duration;
        }

        // Existing calls retain their API. Bell and feedback notes are deliberately not detuned.
        public void PlayBell(int bellNumber1To4) => Play((CabAudioCue)((int)CabAudioCue.Bell1 + Mathf.Clamp(bellNumber1To4 - 1, 0, 3)));
        public void PlayClick() => Play(CabAudioCue.Click);
        public void PlayUiPress() => Play(CabAudioCue.UiPress);
        public void PlayUiHover() => Play(CabAudioCue.UiHover);
        public void PlayHover() => PlayUiHover();
        public void PlayConfirm() => Play(CabAudioCue.Confirm);
        public void PlayPanelOpen() => Play(CabAudioCue.PanelOpen);
        public void PlayPanelClose() => Play(CabAudioCue.PanelClose);
        public void PlayCorrect() { Play(CabAudioCue.Correct); PlayGuide(GuideCue.Correct); }
        public void PlayWrong() { Play(CabAudioCue.Wrong); PlayGuide(GuideCue.Incorrect); }
        public void PlayPickup() { _guideLowTimeIssued = false; Play(CabAudioCue.Pickup); }
        public void PlayRewind() => Play(CabAudioCue.Rewind);
        public void PlayHint() { Play(CabAudioCue.Hint); PlayGuide(GuideCue.Hint); }
        public void PlayTimerWarning()
        {
            var game = GameManager.Instance;
            int seconds = game != null ? Mathf.CeilToInt(game.TimeRemaining) : 5;
            float urgency = Mathf.Clamp01((5f - seconds) / 4f);
            Play(CabAudioCue.Timer, gain: .70f + urgency * .30f, pitch: 1f + urgency * .28f);
            if (!_guideLowTimeIssued)
            {
                _guideLowTimeIssued = true;
                PlayGuide(GuideCue.LowTime);
            }
        }
        public void PlayTimerTick() => PlayTimerWarning();
        public void PlayBrakeWarning() => Play(CabAudioCue.BrakeWarning);
        public void PlayDoor() => PlayDoor(false);
        public void PlayDoor(bool opening) => Play(opening ? CabAudioCue.DoorOpen : CabAudioCue.DoorClose, pan: GameConfig.CurbSide * .12f);
        public void PlayBrakeSqueal() => Play(CabAudioCue.BrakeSqueal);
        public void PlayHorn() => Play(CabAudioCue.Horn);
        public void PlayWiper() => Play(CabAudioCue.Wiper, pan: (_wiperDirection++ & 1) == 0 ? -.13f : .13f);
        public void PlayCoinCollect(int index = 0) => Play(CabAudioCue.Coin, pitch: 1f + Mathf.Clamp(index, 0, 7) * .045f);

        public void SetSpokenGuideEnabled(bool enabled) => _guide?.SetEnabled(enabled);

        public float PlayGuide(GuideCue cue)
        {
            if (cue == GuideCue.QuestionIntro || cue == GuideCue.Pickup) _guideLowTimeIssued = false;
            if (_guide == null || _sfxVolume <= .001f) return 0f;
            float duration = _guide.Play(cue, Loc.Lang);
            _guide.Tick(_sfxVolume * _voiceGain);
            if (duration > 0f)
            {
                _duckTarget = Mathf.Min(_duckTarget, .48f);
                _duckUntil = System.Math.Max(_duckUntil, AudioSettings.dspTime + duration + .2f);
            }
            return duration;
        }

        /// <summary>Original wordless acknowledgement; returns the actual pitched duration, or zero if suppressed.</summary>
        public float PlayPassengerVoice(int voiceIndex, float strength = 1f)
        {
            var clip = _passengerClips[Mathf.Clamp(voiceIndex, 0, _passengerClips.Length - 1)];
            LastPassengerVoiceDuration = clip != null ? Play(CabAudioCue.PassengerVoice, clip, Mathf.Clamp01(strength)) : 0f;
            return LastPassengerVoiceDuration;
        }

        public void UpdateEngineSound(float speedMetersPerSec, bool isBraking)
        {
            _speed = Mathf.Clamp(speedMetersPerSec, 0f, 30f);
            _braking = isBraking;
        }

        public void SetCabInterior(bool inside) => _outside = !inside;

        public void SetWeather(WeatherPreset weather)
        {
            bool changedAfterStartup = (int)_weather >= 0 && weather != _weather;
            _weather = weather;
            _isNight = weather == WeatherPreset.Night;
            _wipers = weather == WeatherPreset.Rain;
            _wiperStroke = Mathf.FloorToInt((Time.time * 6.5f - Mathf.PI * .5f) / Mathf.PI);
            if (changedAfterStartup)
            {
                if (weather == WeatherPreset.Night) PlayGuide(GuideCue.Night);
                else if (weather == WeatherPreset.Rain) PlayGuide(GuideCue.Rain);
                else if (weather == WeatherPreset.FoggyMorning) PlayGuide(GuideCue.Fog);
            }
        }

        public void UpdateAmbience(bool sea, bool night, bool outside, float dt)
        {
            _bySea = sea;
            _isNight = night;
            _outside = outside;
        }

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, .1f);
            if (dt <= 0f) return;
            double now = AudioSettings.dspTime;
            var weather = WeatherDirector.Instance;
            if (weather != null && weather.CurrentWeather != _weather) SetWeather(weather.CurrentWeather);
            var game = GameManager.Instance;
            if (game != null && game.City != null) _district = Mathf.Clamp(game.City.District, 0, 7);
            if (_guide != null)
            {
                _guide.Tick(_sfxVolume * _voiceGain);
                if (_guide.IsSpeaking) { _duckTarget = Mathf.Min(_duckTarget, .48f); _duckUntil = now + .15f; }
            }
            float response = 1f - Mathf.Exp(-5f * dt);
            _muffle = Mathf.Lerp(_muffle, _outside ? 0f : 1f, response);
            float duckTarget = now < _duckUntil ? _duckTarget : 1f;
            _duck = Mathf.Lerp(_duck, duckTarget, 1f - Mathf.Exp(-(duckTarget < _duck ? 18f : 2.7f) * dt));
            _ambiencePresence = Mathf.Lerp(_ambiencePresence, _outside ? 1f : .48f, response);
            SetVolume(_music, _musicVolume * _musicGain * _duck);
            UpdateMechanical(dt, response);
            UpdateSoundscape(1f - Mathf.Exp(-.85f * dt), now,
                game != null ? game.State : GameState.MainMenu, dt);
            for (int i = 0; i < VoiceCount; i++)
            {
                if (_voiceEnds[i] <= now) continue;
                float gain = _voiceGains[i] * BusGain(_voiceBuses[i]) * _sfxVolume;
                if (_voiceBuses[i] == CabAudioBus.Ambience) gain *= _ambiencePresence * Mathf.Lerp(.75f, 1f, _duck);
                else if (_voiceBuses[i] == CabAudioBus.Mechanical) gain *= Mathf.Lerp(1f, .72f, _muffle);
                SetVolume(_voices[i], gain);
            }
        }

        void UpdateMechanical(float dt, float response)
        {
            int previousGear = _gear;
            while (_gear < 4 && _speed > _gear * 5.2f + .55f) _gear++;
            while (_gear > 1 && _speed < (_gear - 1) * 5.2f - .75f) _gear--;
            if (_gear > previousGear) Play(CabAudioCue.GearShift);
            float acceleration = (_speed - _previousSpeed) / Mathf.Max(dt, .0001f);
            _previousSpeed = _speed;
            float load = _braking ? .12f : Mathf.Clamp01(.28f + acceleration * .09f + _speed * .018f);
            _engineMix = Mathf.Lerp(_engineMix, load, response);
            float revs = Mathf.Clamp01((_speed - (_gear - 1) * 5.2f) / 5.8f);
            float enginePitch = Mathf.Lerp(.82f, 1.47f, revs);
            _engine.pitch = Mathf.Lerp(_engine.pitch, enginePitch, response * 1.4f);
            _engineLoad.pitch = Mathf.Lerp(_engineLoad.pitch, enginePitch * 1.04f, response * 1.4f);
            float active = Time.timeScale > 0f ? 1f : .10f;
            float baseGain = _sfxVolume * _mechanicalGain * active;
            float cabin = Mathf.Lerp(1f, .73f, _muffle);
            SetVolume(_engine, baseGain * cabin * Mathf.Lerp(.16f, .11f, _engineMix));
            SetVolume(_engineLoad, baseGain * cabin * _engineMix * .23f);
            float roadAmount = Mathf.Clamp01(_speed / 18f);
            _road.pitch = Mathf.Lerp(.78f, 1.25f, roadAmount);
            SetVolume(_road, baseGain * Mathf.Lerp(1f, .55f, _muffle) * roadAmount * .19f);
            _engineFilter.cutoffFrequency = Mathf.Lerp(15000f, 3600f, _muffle);
            _loadFilter.cutoffFrequency = Mathf.Lerp(17000f, 3100f, _muffle);
            _roadFilter.cutoffFrequency = Mathf.Lerp(13000f, 1800f, _muffle);
            if (_wipers && Time.timeScale > 0f)
            {
                // Match the actual scaled-time sine extrema, including after pauses or weather changes.
                int stroke = Mathf.FloorToInt((Time.time * 6.5f - Mathf.PI * .5f) / Mathf.PI);
                if (stroke != _wiperStroke)
                {
                    _wiperStroke = stroke;
                    PlayWiper();
                }
            }
        }

        void UpdateSoundscape(float response, double now, GameState state, float dt)
        {
            float baseGain = _sfxVolume * _ambienceGain * _ambiencePresence * Mathf.Lerp(.70f, 1f, _duck);
            bool puzzleFocus = state == GameState.PuzzleActive || state == GameState.PuzzleResult;
            var levels = IstanbulSoundscape.Evaluate(_district, _bySea, _weather, state);
            // Fade the two water recordings out within a fraction of a second on entry;
            // restore the ordinary slow soundscape blend automatically when driving resumes.
            float waterResponse = puzzleFocus ? 1f - Mathf.Exp(-12f * dt) : response;
            FadeLoop(_waves, baseGain * levels.Sea * (_isNight ? .31f : .24f), waterResponse);
            FadeLoop(_night, _isNight ? baseGain * .085f : 0f, response);
            FadeLoop(_rain, _weather == WeatherPreset.Rain ? baseGain * .50f : 0f, response);
            FadeLoop(_cafe, baseGain * levels.Cafe * .085f, response);
            FadeLoop(_crowd, baseGain * levels.Crowd * .22f, response);
            FadeLoop(_market, baseGain * levels.Market * .15f, response);
            FadeLoop(_rainRoofs, _weather == WeatherPreset.Rain ? baseGain * .33f : 0f, response);
            FadeLoop(_harbor, baseGain * levels.Harbor * (_isNight ? .20f : .045f), waterResponse);
            float cutoff = IstanbulSoundscape.ExteriorLowpass(_weather, _muffle);
            _wavesFilter.cutoffFrequency = puzzleFocus ? 600f : cutoff;
            _rainFilter.cutoffFrequency = Mathf.Lerp(17000f, 3000f, _muffle);
            _cafeFilter.cutoffFrequency = Mathf.Min(7000f, cutoff);
            _crowdFilter.cutoffFrequency = Mathf.Min(5600f, cutoff);
            _marketFilter.cutoffFrequency = Mathf.Min(6500f, cutoff);
            _roofsFilter.cutoffFrequency = Mathf.Lerp(15000f, 4800f, _muffle);
            _harborFilter.cutoffFrequency = puzzleFocus ? 600f : Mathf.Min(9200f, cutoff);
            if (!puzzleFocus && _bySea && !_isNight && now >= _nextGull)
            {
                _nextGull = now + Range(18f, 36f);
                Play(CabAudioCue.Gull, _gullClips[(int)(AudioCuePolicy.Next(ref _random) % 3u)], Range(.60f, 1f), pan: Range(-.65f, .65f));
            }
            if (!_isNight && now >= _nextBird)
            {
                _nextBird = now + Range(_bySea ? 12f : 7f, _bySea ? 25f : 15f);
                Play(CabAudioCue.Bird, _birdClips[(int)(AudioCuePolicy.Next(ref _random) % 3u)], Range(.55f, 1f), pan: Range(-.6f, .6f));
            }
            if (!puzzleFocus && _bySea && now >= _nextFerry)
            {
                _nextFerry = now + Range(65f, 110f);
                Play(CabAudioCue.FerryHorn, pan: Range(-.45f, .45f));
            }
        }

        static void SetVolume(AudioSource source, float volume)
        {
            if (source != null && Mathf.Abs(source.volume - volume) > .0001f) source.volume = Mathf.Clamp01(volume);
        }

        static void FadeLoop(AudioSource source, float target, float response)
        {
            if (source == null || source.clip == null) return;
            float value = Mathf.Lerp(source.volume, target, response);
            if (target > .0001f && !source.isPlaying) source.Play();
            if (target <= .0001f && value < .0001f)
            {
                value = 0f;
                if (source.isPlaying) source.Stop();
            }
            // Do not apply SetVolume's dead band to a fade: it can strand a quiet
            // loop above the stop threshold forever instead of reaching silence.
            source.volume = Mathf.Clamp01(value);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
