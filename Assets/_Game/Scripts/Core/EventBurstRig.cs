using UnityEngine;

namespace OddsCab
{
    /// <summary>Cached one-shot emitters. Overlapping feedback uses VfxDirector's prepared pool.</summary>
    public sealed class EventBurstRig : MonoBehaviour
    {
        ParticleSystem _sparks, _flash;

        public void Initialize(ParticleSystem sparks, ParticleSystem flash)
        {
            _sparks = sparks;
            _flash = flash;
        }

        public void Fire(Color color, int count, float scale)
        {
            transform.localScale = Vector3.one * scale;
            var gradient = new ParticleSystem.MinMaxGradient(Color.Lerp(color, Color.white, 0.28f), color);
            var main = _sparks.main;
            main.startColor = gradient;
            _sparks.Emit(count);
            if (_flash == null) return;
            main = _flash.main;
            main.startColor = gradient;
            _flash.Emit(1);
        }
    }
}
