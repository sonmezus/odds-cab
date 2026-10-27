using UnityEngine;
using UnityEngine.InputSystem;

namespace OddsCab
{
    /// <summary>Predecoded tyre texture and one curb impulse source, plus bounded controller rumble.</summary>
    public sealed class TaxiSurfaceFeedback : MonoBehaviour
    {
        AudioSource _stone, _impact;
        Gamepad _pad;
        float _motorClock, _impactCooldown;

        public static TaxiSurfaceFeedback Create(Transform taxi)
        {
            var controller = taxi.gameObject.AddComponent<TaxiSurfaceFeedback>();
            controller._stone = controller.MakeSource("CobblestoneContact", "Audio/WorldDetail/cobblestone_contact", true);
            controller._impact = controller.MakeSource("CurbContact", "Audio/WorldDetail/curb_contact", false);
            return controller;
        }

        AudioSource MakeSource(string name, string path, bool loop)
        {
            var source = new GameObject(name).AddComponent<AudioSource>();
            source.transform.SetParent(transform, false);
            source.clip = Resources.Load<AudioClip>(path);
            source.playOnAwake = false; source.loop = loop; source.spatialBlend = 0f;
            source.volume = 0f; source.priority = loop ? 180 : 110;
            var filter = source.gameObject.AddComponent<AudioLowPassFilter>();
            filter.cutoffFrequency = loop ? 2400f : 3400f;
            return source;
        }

        public void Tick(float speed, in TaxiRoadFeel feel, bool inCab, float dt)
        {
            _impactCooldown = Mathf.Max(0f, _impactCooldown - dt);
            float gain = Mathf.Clamp01(SaveSystem.Data.sfxVolume);
            bool moving = dt > 0f && speed > .1f && gain > .001f;
            float amount = moving ? Mathf.Clamp01(speed / 13f) * feel.CobbleBlend : 0f;
            float desired = amount * gain * (inCab ? .25f : .18f);
            if (_stone != null)
            {
                _stone.volume = Mathf.MoveTowards(_stone.volume, desired, Mathf.Max(.001f, dt) * 1.8f);
                _stone.pitch = Mathf.Lerp(.62f, 1.28f, Mathf.Clamp01(speed / 17f));
                if (desired > .001f && !_stone.isPlaying && _stone.clip != null) _stone.Play();
                if ((!moving || _stone.volume < .001f) && _stone.isPlaying) _stone.Stop();
            }
            if (feel.Impact > .1f && _impactCooldown <= 0f && moving && _impact != null && _impact.clip != null)
            {
                _impact.volume = gain * Mathf.Lerp(.20f, .48f, feel.Impact);
                _impact.pitch = Mathf.Lerp(.91f, 1.07f, feel.Impact);
                _impact.Play(); _impactCooldown = .18f;
            }
            _motorClock -= dt;
            if (_motorClock > 0f && dt > 0f) return;
            _motorClock = .05f;
            var current = Gamepad.current;
            if (_pad != current)
            {
                if (_pad != null) _pad.SetMotorSpeeds(0f, 0f);
                _pad = current;
            }
            if (_pad != null)
                _pad.SetMotorSpeeds(moving ? Mathf.Clamp(amount * .075f + feel.Impact * .20f, 0f, .25f) : 0f,
                    moving ? Mathf.Clamp(amount * .12f + feel.Impact * .35f, 0f, .40f) : 0f);
        }

        void OnDisable()
        {
            if (_stone != null) _stone.Stop();
            if (_impact != null) _impact.Stop();
            if (_pad != null) _pad.SetMotorSpeeds(0f, 0f);
            _pad = null;
        }
    }
}
