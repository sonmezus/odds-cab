using UnityEngine;

namespace OddsCab
{
    public enum AmbientNpcActivity { Idle, Walking, Conversation, Reading, Fishing }

    /// <summary>Cached skeletal animation shared by the independent street cast and miniature travelers.</summary>
    public sealed class AmbientNpcAnimator : MonoBehaviour
    {
        const int Hips = 0, Spine = 1, Chest = 2, Neck = 3, Head = 4;
        const int ArmL = 5, ForeL = 6, HandL = 7, ArmR = 8, ForeR = 9, HandR = 10;
        const int ThighL = 11, ShinL = 12, FootL = 13, ThighR = 14, ShinR = 15, FootR = 16;
        readonly Transform[] _bones = new Transform[17];
        readonly Quaternion[] _rest = new Quaternion[17];
        Quaternion _relaxL, _relaxR;
        Vector3 _hipPosition;
        float _phase, _seed, _footHeight;
        bool _bound;
        PassengerHandPose _leftHand, _rightHand;

        public Transform RightHand => _bones[HandR];
        public Transform LeftHand => _bones[HandL];
        public Transform HeadJoint => _bones[Head];

        public void Bind(Transform skeleton)
        {
            var transforms = skeleton.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                var joint = transforms[i];
                int index = BoneIndex(joint.name);
                if (index < 0 || _bones[index] != null) continue;
                _bones[index] = joint;
                _rest[index] = joint.localRotation;
            }
            _bound = _bones[Hips] != null && _bones[Head] != null;
            if (!_bound) return;
            _hipPosition = _bones[Hips].localPosition;
            _seed = Mathf.Repeat(transform.GetEntityId().GetHashCode() * 0.371f, 6.283185f);
            _phase = _seed;
            _relaxL = Relaxation(ArmL, ForeL, new Vector3(-.075f, -.996f, .04f));
            _relaxR = Relaxation(ArmR, ForeR, new Vector3(.075f, -.996f, .04f));
            _footHeight = Mathf.Min(FootHeight(FootL), FootHeight(FootR));
            _leftHand = new PassengerHandPose(_bones[HandL], true);
            _rightHand = new PassengerHandPose(_bones[HandR], false);
            var animators = skeleton.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++) animators[i].enabled = false;
            Tick(0f, 0f, AmbientNpcActivity.Idle);
        }

        static int BoneIndex(string name)
        {
            switch (name)
            {
                case "Hips": return Hips;
                case "Spine": return Spine;
                case "Spine2": return Chest;
                case "Neck": return Neck;
                case "Head": return Head;
                case "LeftArm": return ArmL;
                case "LeftForeArm": return ForeL;
                case "LeftHand": return HandL;
                case "RightArm": return ArmR;
                case "RightForeArm": return ForeR;
                case "RightHand": return HandR;
                case "LeftUpLeg": return ThighL;
                case "LeftLeg": return ShinL;
                case "LeftFoot": return FootL;
                case "RightUpLeg": return ThighR;
                case "RightLeg": return ShinR;
                case "RightFoot": return FootR;
                default: return -1;
            }
        }

        Quaternion Relaxation(int arm, int forearm, Vector3 target)
        {
            if (_bones[arm] == null || _bones[forearm] == null) return Quaternion.identity;
            var direction = transform.InverseTransformDirection(_bones[forearm].position - _bones[arm].position).normalized;
            return Quaternion.FromToRotation(direction, target.normalized);
        }

        float FootHeight(int index)
        {
            return _bones[index] != null ? transform.InverseTransformPoint(_bones[index].position).y : .08f;
        }

        void Rotate(int index, Quaternion delta)
        {
            Transform joint = _bones[index];
            if (joint == null || joint.parent == null) return;
            Quaternion parentInCharacter = Quaternion.Inverse(transform.rotation) * joint.parent.rotation;
            joint.localRotation = Quaternion.Inverse(parentInCharacter) * delta * parentInCharacter * joint.localRotation;
        }

        void OffsetHips(Vector3 characterOffset)
        {
            var hips = _bones[Hips];
            hips.localPosition = _hipPosition + hips.parent.InverseTransformVector(transform.TransformVector(characterOffset));
        }

        /// <param name="distanceMetres">Actual world distance travelled since the previous tick.</param>
        public void Tick(float deltaTime, float distanceMetres, AmbientNpcActivity activity)
        {
            if (!_bound) return;
            for (int i = 0; i < _bones.Length; i++)
                if (_bones[i] != null) _bones[i].localRotation = _rest[i];
            float time = Time.time + _seed;
            float leftHip = 0f, rightHip = 0f, leftKnee = 0f, rightKnee = 0f;
            float leftArm = 0f, rightArm = 0f, leftElbow = -13f, rightElbow = -13f;
            float rightSpread = 0f, headYaw = Mathf.Sin(time * .37f) * 6f, headPitch = 0f;
            float chestYaw = 0f, bodyLift = Mathf.Sin(time * 1.5f) * .003f;
            if (activity == AmbientNpcActivity.Walking)
            {
                _phase += Mathf.Max(0f, distanceMetres) * (Mathf.PI * 2f /
                    (PassengerLocomotion.StrideMetres * Mathf.Max(.1f, transform.lossyScale.y)));
                if (_phase > 100f) _phase = Mathf.Repeat(_phase, Mathf.PI * 2f);
                PassengerLocomotion.Legs(_phase, 21f, out leftHip, out rightHip, out leftKnee, out rightKnee);
                float swing = Mathf.Sin(_phase);
                leftArm = swing * 17f;
                rightArm = -swing * 17f;
                bodyLift = Mathf.Abs(Mathf.Cos(_phase)) * .025f;
                chestYaw = -swing * 3.5f;
            }
            else if (activity == AmbientNpcActivity.Conversation)
            {
                float gesture = .5f + .5f * Mathf.Sin(time * .9f);
                rightArm = -18f - gesture * 13f;
                rightElbow = -35f - gesture * 22f;
                rightSpread = 9f;
                headPitch = Mathf.Sin(time * 1.9f) * 3.5f;
            }
            else if (activity == AmbientNpcActivity.Reading)
            {
                leftArm = rightArm = -26f;
                leftElbow = rightElbow = -57f;
                headPitch = 15f;
                headYaw *= .25f;
            }
            else if (activity == AmbientNpcActivity.Fishing)
            {
                leftArm = -36f;
                rightArm = -42f - Mathf.Sin(time * .48f) * 2.5f;
                leftElbow = -65f;
                rightElbow = -52f;
                headPitch = 10f + Mathf.Sin(time * .31f) * 3f;
                headYaw *= .4f;
                chestYaw = Mathf.Sin(time * .28f) * 2f;
            }

            OffsetHips(new Vector3(0f, bodyLift, 0f));
            Rotate(Spine, Quaternion.Euler(0f, chestYaw * .55f, 0f));
            Rotate(Chest, Quaternion.Euler(0f, chestYaw * .45f, 0f));
            Rotate(Neck, Quaternion.Euler(headPitch * .4f, headYaw * .4f, 0f));
            Rotate(Head, Quaternion.Euler(headPitch * .6f, headYaw * .6f, 0f));
            Rotate(ThighL, Quaternion.Euler(leftHip, 0f, 0f));
            Rotate(ThighR, Quaternion.Euler(rightHip, 0f, 0f));
            Rotate(ShinL, Quaternion.Euler(leftKnee, 0f, 0f));
            Rotate(ShinR, Quaternion.Euler(rightKnee, 0f, 0f));
            Rotate(FootL, Quaternion.Euler(-leftHip - leftKnee, 0f, 0f));
            Rotate(FootR, Quaternion.Euler(-rightHip - rightKnee, 0f, 0f));
            Rotate(ArmL, Quaternion.Euler(leftArm, 0f, 0f) * _relaxL);
            Rotate(ArmR, Quaternion.Euler(rightArm, 0f, rightSpread) * _relaxR);
            Rotate(ForeL, Quaternion.Euler(leftElbow, 0f, 0f));
            Rotate(ForeR, Quaternion.Euler(rightElbow, 0f, 0f));
            bool reading = activity == AmbientNpcActivity.Reading;
            bool fishing = activity == AmbientNpcActivity.Fishing;
            _leftHand.OrientPalm(reading ? transform.up : transform.right);
            _rightHand.OrientPalm(reading || activity == AmbientNpcActivity.Conversation ? transform.up : -transform.right);
            _leftHand.SetFingerCurl(fishing ? .9f : reading ? .35f : .32f);
            _rightHand.SetFingerCurl(fishing ? .95f : reading ? .35f : activity == AmbientNpcActivity.Conversation ? .15f : .32f);
            if (activity == AmbientNpcActivity.Walking)
            {
                float current = Mathf.Min(FootHeight(FootL), FootHeight(FootR));
                float correction = Mathf.Clamp(_footHeight - current, -.045f, .045f);
                OffsetHips(new Vector3(0f, bodyLift + correction, 0f));
            }
        }
    }
}
