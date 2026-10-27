using UnityEngine;

namespace OddsCab
{
    /// <summary>Cached wrist and finger posture. Finger transforms change only when the activity changes.</summary>
    public sealed class PassengerHandPose
    {
        readonly Transform _hand, _index, _middle, _pinky;
        readonly Transform[] _joints = new Transform[15];
        readonly Quaternion[] _rest = new Quaternion[15];
        readonly Vector3[] _axes = new Vector3[15];
        readonly bool _left;
        float _curl = -1f;

        public PassengerHandPose(Transform hand, bool left)
        {
            _hand = hand; _left = left;
            if (hand == null) return;
            string prefix = left ? "LeftHand" : "RightHand";
            string[] fingers = { "Thumb", "Index", "Middle", "Ring", "Pinky" };
            var bones = hand.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < bones.Length; i++)
                for (int finger = 0; finger < fingers.Length; finger++)
                    for (int joint = 0; joint < 3; joint++)
                        if (bones[i].name == prefix + fingers[finger] + (joint + 1))
                        { _joints[finger * 3 + joint] = bones[i]; _rest[finger * 3 + joint] = bones[i].localRotation; }
            _index = _joints[3]; _middle = _joints[6]; _pinky = _joints[12];
            if (_index == null || _middle == null || _pinky == null) return;
            Vector3 palm = PalmNormal();
            for (int finger = 0; finger < 5; finger++)
            {
                int first = finger * 3;
                if (_joints[first] == null || _joints[first + 1] == null) continue;
                Vector3 along = (_joints[first + 1].position - _joints[first].position).normalized;
                Vector3 axis = Vector3.Cross(along, palm).normalized;
                for (int j = 0; j < 3; j++)
                    if (_joints[first + j] != null) _axes[first + j] = Quaternion.Inverse(_joints[first + j].rotation) * axis;
            }
        }

        Vector3 PalmNormal()
        {
            Vector3 across = (_index.position - _pinky.position).normalized;
            Vector3 along = (_middle.position - _hand.position).normalized;
            return (_left ? Vector3.Cross(across, along) : Vector3.Cross(along, across)).normalized;
        }

        public void OrientPalm(Vector3 worldTarget)
        {
            if (_hand == null || _index == null || _middle == null || _pinky == null) return;
            Vector3 along = (_middle.position - _hand.position).normalized;
            Vector3 target = Vector3.ProjectOnPlane(worldTarget, along);
            if (target.sqrMagnitude < .0001f) return;
            _hand.rotation = Quaternion.FromToRotation(PalmNormal(), target.normalized) * _hand.rotation;
        }

        public void SetFingerCurl(float curl)
        {
            if (Mathf.Abs(curl - _curl) < .001f) return;
            _curl = curl;
            for (int i = 0; i < _joints.Length; i++)
            {
                if (_joints[i] == null) continue;
                int segment = i % 3;
                float angle = i < 3 ? (segment == 0 ? 25f : segment == 1 ? 40f : 30f)
                    : segment == 0 ? 58f : segment == 1 ? 74f : 51f;
                _joints[i].localRotation = _rest[i] * Quaternion.AngleAxis(angle * curl, _axes[i]);
            }
        }
    }
}
