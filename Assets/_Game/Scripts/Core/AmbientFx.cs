using UnityEngine;

namespace OddsCab
{
    /// <summary>
    /// Ambient effects from Unity's Particle Pack (Resources/Fx), tuned for mobile on load (particle caps,
    /// no per-particle lights, rates by device tier):
    /// - DustMotes: golden-hour dust drifting in the air around the taxi (sunny and sunset weather)
    /// - FireFlies: fireflies along the promenade at night
    /// - Smoke: a faint exhaust puff from the taxi when it pulls away from a stop
    /// - waves breaking against the quay wall ahead every few seconds (own effect, ParticleFx.CreateWaveSplash;
    ///   the pack's BigSplash is made for an object dropped in water and barely showed at this scale)
    /// Chimney Steam is placed by the street builder (<see cref="CreateChimneySteam"/>).
    /// </summary>
    public class AmbientFx : MonoBehaviour
    {
        ParticleSystem _dust, _fireflies, _exhaust, _splash;
        float _nextSplash;
        // Sea-side face of the promenade's quay wall and the sea surface (streamed street chunks)
        // (x on the district's water side, CityChunkStreamer.CurrentSeaSide)
        const float QuayWallX = 6.35f, SeaY = -0.35f;
        float _lastSpeed;

        static float TierScale => DeviceTierDetector.CurrentTier == DeviceTier.Low ? 0.4f
                                : DeviceTierDetector.CurrentTier == DeviceTier.Medium ? 0.7f : 1f;

        /// <summary>Instantiates a pack prefab and makes it mobile-friendly. Returns its root system.</summary>
        public static ParticleSystem Spawn(string name, Transform parent, int maxParticles, float rateScale)
        {
            var prefab = Resources.Load<GameObject>("Fx/" + name);
            if (prefab == null) return null;
            var go = Instantiate(prefab, parent, false);
            go.name = "Fx_" + name;
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.maxParticles = Mathf.Max(1, Mathf.Min(main.maxParticles, maxParticles));
                main.playOnAwake = false;
                var lights = ps.lights; lights.enabled = false;           // a real light per particle: far too costly
                var em = ps.emission;
                em.rateOverTimeMultiplier *= rateScale * TierScale;
                var col = ps.collision; col.enabled = false;
                var r = ps.GetComponent<ParticleSystemRenderer>();
                if (r != null) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }
            }
            foreach (var l in go.GetComponentsInChildren<Light>(true)) l.enabled = false;
            var root = go.GetComponent<ParticleSystem>();
            root.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return root;
        }

        /// <summary>A slow, thin wisp of steam from a chimney (street houses). Plays on its own.</summary>
        public static void CreateChimneySteam(Transform parent, Vector3 localPos)
        {
            var ps = Spawn("Steam", parent, 24, 0.35f);
            if (ps == null) return;
            ps.transform.localPosition = localPos;
            ps.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            ps.transform.localScale = Vector3.one * 0.8f;
            var main = ps.main; main.playOnAwake = true;
            ps.Play(true);
        }

        void Awake()
        {
            _dust = Spawn("DustMotesEffect", transform, 60, 0.25f);
            _fireflies = Spawn("FireFlies", transform, 24, 1f);
            _exhaust = Spawn("SmokeEffect", transform, 30, 1f);
            _splash = ParticleFx.CreateWaveSplash(transform);
            if (_exhaust != null)
            {
                // The pack's smoke is a thick dark plume; a classic car's exhaust is a small pale wisp
                foreach (var ps in _exhaust.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var m = ps.main;
                    m.scalingMode = ParticleSystemScalingMode.Hierarchy;
                    m.startSizeMultiplier *= 0.25f;
                    m.startLifetimeMultiplier *= 0.45f;
                    m.startSpeedMultiplier *= 0.5f;
                    m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.82f, 0.80f, 0.78f, 0.35f));
                }
                var em = _exhaust.emission; em.enabled = false;            // bursts only, on pull-away
            }
        }

        public void Tick(Transform taxi, float speed, bool exterior)
        {
            if (taxi == null) return;
            var w = WeatherDirector.Instance != null ? WeatherDirector.Instance.CurrentWeather : WeatherPreset.Sunny;

            // Dust motes hover in the warm light just ahead of the taxi
            bool dust = exterior && (w == WeatherPreset.Sunny || w == WeatherPreset.GoldenSunset);
            Toggle(_dust, dust);
            if (_dust != null) _dust.transform.position = taxi.position + new Vector3(0f, 1.5f, 6f);

            // Fireflies over the promenade on the sea side
            bool flies = exterior && w == WeatherPreset.Night;
            Toggle(_fireflies, flies);
            if (_fireflies != null) _fireflies.transform.position = new Vector3((CityChunkStreamer.CurrentSeaSide != 0 ? CityChunkStreamer.CurrentSeaSide : -1) * 4.8f, 0.8f, taxi.position.z + 8f);

            // Exhaust puff when pulling away
            if (_exhaust != null)
            {
                _exhaust.transform.position = taxi.TransformPoint(GameConfig.SideV(new Vector3(0.3f, 0.25f, -1.45f)));
                _exhaust.transform.rotation = Quaternion.LookRotation(-taxi.forward + Vector3.up * 0.3f);
                if (exterior && _lastSpeed < 1.0f && speed >= 1.0f) _exhaust.Emit(5);
                else if (exterior && speed > 1f && speed < 8f && Time.frameCount % 30 == 0) _exhaust.Emit(1);
            }
            _lastSpeed = speed;

            // Sound of the place: surf and gulls by the water, birds by day, cicadas at night
            var gm = GameManager.Instance;
            bool ferry = gm != null && gm.State == GameState.FerryCrossing;
            bool byWater = ferry || CityChunkStreamer.CurrentRoad == TourRoadType.Bridge
                       || (CityChunkStreamer.CurrentRoad == TourRoadType.Coastal && CityChunkStreamer.CurrentSeaSide != 0);
            SynthAudio.Instance?.UpdateAmbience(byWater, w == WeatherPreset.Night, exterior, Time.unscaledDeltaTime);

            // A wave breaks against the quay ahead every few seconds
            if (_splash != null && exterior && w != WeatherPreset.FoggyMorning && Time.time >= _nextSplash
                && CityChunkStreamer.CurrentRoad == TourRoadType.Coastal && CityChunkStreamer.CurrentSeaSide != 0
                && (GameManager.Instance == null || GameManager.Instance.State != GameState.FerryCrossing))
            {
                _nextSplash = Time.time + Random.Range(2.2f, 4.5f) / TierScale;
                // against the foot of the quay wall, on the water; rain and night bring rougher sea
                float strength = Random.Range(0.4f, 1f) * (w == WeatherPreset.Rain ? 1.3f : 1f) * TierScale;
                int sea = CityChunkStreamer.CurrentSeaSide;
                ParticleFx.FireWave(_splash, new Vector3(sea * (QuayWallX + 0.25f), SeaY, taxi.position.z + Random.Range(10f, 28f)), Mathf.Clamp01(strength), sea);
            }
        }

        static void Toggle(ParticleSystem ps, bool on)
        {
            if (ps == null) return;
            if (on && !ps.isPlaying) ps.Play(true);
            else if (!on && ps.isPlaying) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}
