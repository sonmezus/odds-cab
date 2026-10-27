using UnityEngine;

namespace OddsCab
{
    /// <summary>
    /// Wind along the taxi's flanks while it picks up speed: thin, faint air streaks peel off the front corners and
    /// slip back along the left and right sides, bowing slightly outward as air flows round the body. They only
    /// appear while the taxi accelerates (more the harder it pulls away, full during the post-answer boost) and fade
    /// once the speed settles. Two particle systems riding on the taxi; off in the cab.
    /// </summary>
    public class SpeedStreaks : MonoBehaviour
    {
        const float FullAccel = 3.2f;      // m/s² that gives the full effect (the taxi accelerates at 4)
        const float MinKmh = 12f;

        ParticleSystem[] _sides;
        Transform _taxi;
        float _lastKmh = -1f, _accel, _intensity;

        void Build(Transform taxi)
        {
            _taxi = taxi;
            var mat = new Material(Shader.Find("Sprites/Default")) { mainTexture = MakeStreakTexture(), renderQueue = 3100 };
            _sides = new ParticleSystem[2];
            for (int s = 0; s < 2; s++)
            {
                float side = s == 0 ? -1f : 1f;
                var go = new GameObject(side < 0 ? "WindLeft" : "WindRight");
                go.transform.SetParent(taxi, false);
                // just off the front corner of the body, from sill to roof height
                go.transform.localPosition = new Vector3(side * 0.92f, 0.85f, 1.2f);
                var ps = go.AddComponent<ParticleSystem>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main;
                main.loop = true;
                main.playOnAwake = false;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;   // the air rushes past the car, not the street
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.45f);
                main.startSpeed = 0f;
                main.startSize = new ParticleSystem.MinMaxCurve(0.018f, 0.035f);
                main.startColor = new Color(1f, 1f, 1f, 0.5f);
                main.maxParticles = 90;
                var em = ps.emission; em.rateOverTime = 0f;
                var sh = ps.shape;
                sh.shapeType = ParticleSystemShapeType.Box;
                sh.scale = new Vector3(0.25f, 0.75f, 0.6f);
                // streaming backwards and bowing out round the body (all three axes in one curve mode)
                var vel = ps.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.Local;
                vel.x = new ParticleSystem.MinMaxCurve(side * 0.3f, side * 1.1f);
                vel.y = new ParticleSystem.MinMaxCurve(-0.15f, 0.25f);
                vel.z = new ParticleSystem.MinMaxCurve(-16f, -11f);
                var col = ps.colorOverLifetime;
                col.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.85f, 0.92f, 1f), 1f) },
                          new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.55f, 0.25f), new GradientAlphaKey(0.35f, 0.6f), new GradientAlphaKey(0f, 1f) });
                col.color = g;
                var r = go.GetComponent<ParticleSystemRenderer>();
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.velocityScale = 0.09f;      // long, hair-thin lines at speed
                r.lengthScale = 1f;
                r.material = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                ps.Play();
                _sides[s] = ps;
            }
        }

        /// <param name="boost">Post-answer acceleration: the wind at full strength and golden.</param>
        public void Initialize(Transform taxi)
        {
            if (taxi != null && _sides == null) Build(taxi);
        }

        public void Tick(Transform taxi, int kmh, bool show, bool boost)
        {
            if (taxi == null || _sides == null || _taxi != taxi) return;

            // acceleration from the speed change, smoothed (only speeding up counts)
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            if (_lastKmh < 0f) _lastKmh = kmh;
            float a = (kmh - _lastKmh) / 3.6f / dt;
            _lastKmh = kmh;
            _accel = Mathf.Lerp(_accel, Mathf.Max(0f, a), 1f - Mathf.Exp(-6f * dt));

            float target = show && kmh > MinKmh ? Mathf.Clamp01(_accel / FullAccel) : 0f;
            if (show && boost) target = 1f;
            _intensity = Mathf.MoveTowards(_intensity, target, dt * (target > _intensity ? 3f : 1.2f));

            float tier = DeviceTierDetector.CurrentTier == DeviceTier.Low ? 0.5f : 1f;
            var tint = boost ? new Color(1f, 0.85f, 0.45f, 0.6f) : new Color(1f, 1f, 1f, 0.45f);
            foreach (var ps in _sides)
            {
                var em = ps.emission;
                em.rateOverTime = 70f * _intensity * tier;
                var main = ps.main;
                main.startColor = tint;
            }
        }

        /// <summary>Soft line: faint tail, brighter head, feathered edges.</summary>
        static Texture2D MakeStreakTexture()
        {
            const int w = 8, h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;
                float along = Mathf.SmoothStep(0f, 1f, v / 0.7f) * Mathf.SmoothStep(1f, 0f, (v - 0.85f) / 0.15f);
                for (int x = 0; x < w; x++)
                {
                    float edge = 1f - Mathf.Pow(Mathf.Abs((x + 0.5f) / w * 2f - 1f), 2f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, along * edge));
                }
            }
            tex.Apply();
            return tex;
        }
    }
}
