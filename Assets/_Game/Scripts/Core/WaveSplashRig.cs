using UnityEngine;

namespace OddsCab
{
    /// <summary>Cached splash emitters; firing a wave never enumerates children or reads object names.</summary>
    public sealed class WaveSplashRig : MonoBehaviour
    {
        ParticleSystem _droplets, _foam;

        public void Initialize(ParticleSystem droplets, ParticleSystem foam)
        {
            _droplets = droplets;
            _foam = foam;
        }

        public void Emit(float strength)
        {
            if (_droplets != null) _droplets.Emit(Mathf.RoundToInt(Mathf.Lerp(20f, 50f, strength)));
            if (_foam != null) _foam.Emit(Mathf.RoundToInt(Mathf.Lerp(5f, 10f, strength)));
        }
    }
}
