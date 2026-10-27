using UnityEngine;

namespace OddsCab
{
    /// <summary>Three soft portrait lights, active only while an exterior passenger is close to the taxi.</summary>
    public sealed class CurbPortraitLighting : MonoBehaviour
    {
        TaxiController _taxi;
        Camera _camera;
        Light _key, _fill, _rim;

        public void Initialize(TaxiController taxi)
        {
            _taxi = taxi;
            _camera = Camera.main;
            _key = MakeLight("PassengerKey", new Color(1f, 0.91f, 0.78f), 3.5f, 65f);
            _fill = MakeLight("PassengerSkyFill", new Color(0.69f, 0.82f, 1f), 3.4f, 75f);
            _rim = MakeLight("PassengerRim", new Color(1f, 0.83f, 0.60f), 3f, 62f);
        }

        Light MakeLight(string name, Color color, float range, float angle)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(transform, false);
            light.type = LightType.Spot;
            light.color = color;
            light.range = range;
            light.spotAngle = angle;
            light.innerSpotAngle = angle * 0.65f;
            light.shadows = LightShadows.None;
            light.enabled = false;
            return light;
        }

        void LateUpdate()
        {
            if (_key == null) return;
            var game = GameManager.Instance;
            var actor = game != null ? game.ActiveCurbPassenger : null;
            bool visible = _taxi != null && _taxi.ExteriorVisible && _camera != null && actor != null
                && actor.isActiveAndEnabled && (actor.transform.position - _taxi.transform.position).sqrMagnitude < 64f;
            SetEnabled(_key, visible);
            bool rich = visible && DeviceTierDetector.CurrentTier != DeviceTier.Low;
            SetEnabled(_fill, rich);
            SetEnabled(_rim, rich);
            if (!visible) return;

            Vector3 anchor = actor.FaceLightingAnchor;
            Vector3 towardCamera = (_camera.transform.position - anchor).normalized;
            Vector3 right = _camera.transform.right;
            float strength = PortraitStrength(game.Weather != null ? game.Weather.CurrentWeather : WeatherPreset.Sunny);
            _key.intensity = 1.65f * strength;
            _fill.intensity = 0.64f * strength;
            _rim.intensity = 1.15f * strength;
            int mask = 1 << actor.gameObject.layer;
            _key.cullingMask = _fill.cullingMask = _rim.cullingMask = mask;
            Place(_key, anchor + towardCamera * 1.25f - right * 0.85f + Vector3.up * 0.55f, anchor);
            Place(_fill, anchor + towardCamera * 1.3f + right * 0.95f + Vector3.up * 0.15f, anchor);
            Place(_rim, anchor - towardCamera * 0.75f + right * 0.6f + Vector3.up * 0.55f, anchor);
        }

        public static float PortraitStrength(WeatherPreset weather)
        {
            switch (weather)
            {
                case WeatherPreset.Night: return 1.15f;
                case WeatherPreset.Rain: return 0.95f;
                case WeatherPreset.FoggyMorning: return 0.6f;
                case WeatherPreset.GoldenSunset: return 0.75f;
                default: return 0.5f;
            }
        }

        static void SetEnabled(Light light, bool enabled)
        {
            if (light.enabled != enabled) light.enabled = enabled;
        }

        static void Place(Light light, Vector3 position, Vector3 anchor)
        {
            light.transform.SetPositionAndRotation(position, Quaternion.LookRotation(anchor - position));
        }
    }
}
