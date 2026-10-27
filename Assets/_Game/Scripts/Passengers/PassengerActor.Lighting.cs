using UnityEngine;

namespace OddsCab
{
    public partial class PassengerActor
    {
        /// <summary>Animated anatomical anchor for portrait lighting; works with rigged and fallback actors.</summary>
        public Vector3 FaceLightingAnchor => _boneHead != null
            ? _boneHead.position : transform.position + transform.up * 1.25f;
    }
}
