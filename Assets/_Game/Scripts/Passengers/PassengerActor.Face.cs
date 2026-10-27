using UnityEngine;

namespace OddsCab
{
    public partial class PassengerActor
    {
        // Rodin eyes point along local Y, not Z. Cache anatomical axes in each bone's
        // bind frame so gaze remains correct for every FBX and while the head turns.
        struct FaceJoint
        {
            public Transform bone;
            public Quaternion rest;
            public Vector3 pitchAxis, yawAxis;

            public void Bind(Transform joint, Transform head)
            {
                bone = joint;
                rest = joint.localRotation;
                pitchAxis = Quaternion.Inverse(joint.rotation) * head.right;
                yawAxis = Quaternion.Inverse(joint.rotation) * head.up;
            }

            public void Apply(float pitch, float yaw)
            {
                if (bone != null)
                    bone.localRotation = rest * Quaternion.AngleAxis(yaw, yawAxis) * Quaternion.AngleAxis(pitch, pitchAxis);
            }
        }

        /// <summary>The instantiated model under this actor (GlbModelLoader names it "Mesh" / "DriverMesh").</summary>
        Transform ModelRoot => transform.childCount > 0 ? transform.GetChild(0) : transform;

        FaceJoint _faceJaw, _faceLeftEye, _faceRightEye;
        Vector3 _facePose;
        bool _hasExpressionOverride;
        Camera _faceMainCamera;

        enum FaceMorphChannel { Blink, BrowInnerUp, BrowOuterUp, BrowDown }

        struct FaceMorph
        {
            public SkinnedMeshRenderer renderer;
            public int index;
            public float restWeight;
            public FaceMorphChannel channel;
        }

        FaceMorph[] _faceMorphs = System.Array.Empty<FaceMorph>();
        Vector3 _browPose;
        float _nextBlink, _blinkTime = -1f;
        const float BlinkDuration = 0.18f;

        public bool HasJawRig => _faceJaw.bone != null;
        public bool HasEyeRig => _faceLeftEye.bone != null || _faceRightEye.bone != null;
        public bool HasBlinkChannels { get; private set; }
        public bool HasBrowChannels { get; private set; }
        public bool HasFaceRig => HasJawRig || HasEyeRig || HasBlinkChannels || HasBrowChannels;

        /// <summary>Stable world-space portrait framing, using joints cached during rig setup.</summary>
        public bool TryGetPortraitAnchor(out Vector3 anchor)
        {
            if (_faceLeftEye.bone != null && _faceRightEye.bone != null)
            {
                anchor = (_faceLeftEye.bone.position + _faceRightEye.bone.position) * 0.5f;
                return true;
            }
            if (_boneHead != null)
            {
                anchor = _boneHead.position + transform.up * 0.055f;
                return true;
            }
            anchor = transform.position + transform.up * 1.4f;
            return false;
        }

        /// <summary>Call after replacing the main camera. The passenger camera is read directly from GameManager.</summary>
        public void RefreshFaceCamera() => _faceMainCamera = Camera.main;

        void SetupFaceRig()
        {
            _faceJaw = _faceLeftEye = _faceRightEye = default;
            _facePose = Vector3.zero;
            _browPose = Vector3.zero;
            _speakTime = 0f;
            _lastFacePose = (PassengerPose)(-1);
            _saccade = Vector2.zero;
            _nextSaccade = Time.time + Random.Range(0.6f, 2f);
            _blinkTime = -1f;
            _nextBlink = Time.time + Random.Range(2.4f, 5.5f);
            RefreshFaceCamera();
            if (_boneHead != null)
            {
                foreach (var bone in _boneHead.GetComponentsInChildren<Transform>(true))
                {
                    string name = bone.name;
                    int colon = name.LastIndexOf(':');
                    if (colon >= 0) name = name.Substring(colon + 1);
                    switch (name.ToLowerInvariant())
                    {
                        case "jaw": case "mandible": _faceJaw.Bind(bone, _boneHead); break;
                        case "lefteye": case "eye_l": _faceLeftEye.Bind(bone, _boneHead); break;
                        case "righteye": case "eye_r": _faceRightEye.Bind(bone, _boneHead); break;
                    }
                }
            }
            BindFaceMorphs();
        }

        void BindFaceMorphs()
        {
            HasBlinkChannels = HasBrowChannels = false;
            var bindings = new System.Collections.Generic.List<FaceMorph>();
            foreach (var renderer in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Mesh mesh = renderer.sharedMesh;
                if (mesh == null) continue;
                int combinedBlink = -1;
                bool hasSeparateBlink = false;
                for (int index = 0; index < mesh.blendShapeCount; index++)
                {
                    string name = CanonicalMorphName(mesh.GetBlendShapeName(index));
                    FaceMorphChannel channel;
                    switch (name)
                    {
                        case "blink": case "eyeblink": case "eyesclosed":
                            combinedBlink = index;
                            continue;
                        case "eyeblinkleft": case "eyeblinkright":
                        case "eyeblinkl": case "eyeblinkr":
                        case "blinkleft": case "blinkright": case "blinkl": case "blinkr":
                            channel = FaceMorphChannel.Blink;
                            hasSeparateBlink = HasBlinkChannels = true;
                            break;
                        case "browinnerup": case "browinnerupleft": case "browinnerupright":
                            channel = FaceMorphChannel.BrowInnerUp;
                            HasBrowChannels = true;
                            break;
                        case "browouterupleft": case "browouterupright":
                        case "browouterupl": case "browouterupr":
                            channel = FaceMorphChannel.BrowOuterUp;
                            HasBrowChannels = true;
                            break;
                        case "browdownleft": case "browdownright": case "browdownl": case "browdownr":
                            channel = FaceMorphChannel.BrowDown;
                            HasBrowChannels = true;
                            break;
                        default: continue;
                    }
                    bindings.Add(new FaceMorph { renderer = renderer, index = index,
                        restWeight = renderer.GetBlendShapeWeight(index), channel = channel });
                }
                // Avoid applying a bilateral blink on top of separate left/right channels.
                if (combinedBlink >= 0 && !hasSeparateBlink)
                {
                    HasBlinkChannels = true;
                    bindings.Add(new FaceMorph { renderer = renderer, index = combinedBlink,
                        restWeight = renderer.GetBlendShapeWeight(combinedBlink), channel = FaceMorphChannel.Blink });
                }
            }
            _faceMorphs = bindings.ToArray();
        }

        static string CanonicalMorphName(string name)
        {
            int separator = Mathf.Max(name.LastIndexOf(':'), name.LastIndexOf('.'));
            if (separator >= 0) name = name.Substring(separator + 1);
            return name.Replace("_", "").Replace("-", "").Replace(" ", "").ToLowerInvariant();
        }

        void LateUpdate()
        {
            AnimateFace(Time.deltaTime, false);
        }

        // Talking: seconds left of jaw "speech", started by Speak() or automatically on a greeting / new question
        float _speakTime;
        PassengerPose _lastFacePose = (PassengerPose)(-1);
        // Saccades: the eyes hold a point, then jump to the next one
        Vector2 _saccade;
        float _nextSaccade;

        /// <summary>Moves the jaw like speech for <paramref name="seconds"/> (greetings, questions, chat).</summary>
        public void Speak(float seconds)
        {
            if (!float.IsNaN(seconds) && !float.IsInfinity(seconds))
                _speakTime = Mathf.Max(_speakTime, Mathf.Max(0f, seconds));
        }

        // Head turn applied by the body animation this frame (deg), so the eyes compensate it
        float _headYawNow, _headPitchNow;

        void AnimateFace(float deltaTime, bool immediate)
        {
            if (!HasFaceRig) return;
            // With an audio bank, greeting and question jaw timing comes from the actual cue.
            if (Pose != _lastFacePose)
            {
                var audio = SynthAudio.Instance;
                if (audio == null || !audio.BankReady)
                {
                    if (Pose == PassengerPose.ApproachingDoor) Speak(1.4f);
                    else if (Pose == PassengerPose.SittingInCab) Speak(2.6f);
                    else if (Pose == PassengerPose.Satisfied) Speak(0.9f);
                }
                else if (Pose == PassengerPose.Satisfied)
                {
                    var game = GameManager.Instance;
                    Speak(audio.PlayPassengerVoice(game != null ? game.CurrentStop % 5 : 0, 0.55f));
                }
                _lastFacePose = Pose;
            }

            // X: jaw opening (deg). Y/Z: gaze pitch/yaw in the head's anatomical frame (deg).
            Vector3 target = Vector3.zero;
            switch (CurrentExpression)
            {
                case PassengerExpression.Thoughtful: target.Set(.15f, 2.5f, -3.5f); break;      // looking up and aside
                case PassengerExpression.Satisfied: target.Set(3.5f, -1f, 0f); break;           // open, smiling
                case PassengerExpression.Disappointed: target.Set(.3f, 4f, -1.5f); break;       // eyes down
                default: target.Set(.8f, -1.5f, 1f); break;
            }
            if (Pose == PassengerPose.DriverDriving) target = Vector3.zero;
            if (!immediate && deltaTime > 0f)
            {
                float clock = Time.time + _phase;

                // Curious / satisfied passengers look at the camera (the player) - in the cab and on the webcam
                if (Pose != PassengerPose.DriverDriving &&
                    (CurrentExpression == PassengerExpression.Curious || CurrentExpression == PassengerExpression.Satisfied))
                {
                    var g = GazeAtCamera();
                    if (g.HasValue) { target.y = g.Value.x; target.z = g.Value.y; }
                }

                // Saccades: small jumps every 0.6-2 s instead of a smooth drift
                if (Time.time >= _nextSaccade)
                {
                    _saccade.Set(Random.Range(-1.2f, 1.2f), Random.Range(-2.5f, 2.5f));
                    _nextSaccade = Time.time + Random.Range(0.6f, 2f);
                }
                if (Pose != PassengerPose.DriverDriving)
                {
                    target.y += _saccade.x;
                    target.z += _saccade.y;
                }

                // Speech: syllable-like jaw motion from two beating sines, gated so it rests between words
                if (_speakTime > 0f)
                {
                    _speakTime = Mathf.Max(0f, _speakTime - deltaTime);
                    float syll = Mathf.Abs(Mathf.Sin(clock * 11f)) * (0.6f + 0.4f * Mathf.Sin(clock * 3.7f));
                    float words = Mathf.SmoothStep(0f, 1f, Mathf.Sin(clock * 2.3f) * 0.5f + 0.7f);
                    // the Rodin jaw weights are soft: ~7 deg barely parts the lips, 12 reads as speech
                    target.x += syll * words * 12f * Mathf.Clamp01(_speakTime * 3f);
                }
                if (Pose == PassengerPose.Satisfied)
                    target.x += Mathf.Sin(Mathf.Clamp01(_poseTimer / .75f) * Mathf.PI) * 1.5f;
            }
            // The source face is one continuous weighted surface. The eye joints
            // also influence the lids, so large eyeball-style rotations stretch
            // the painted iris/rim. Keep a subtle gaze while the head does the turn.
            target.y = Mathf.Clamp(target.y, -3f, 3f);
            target.z = Mathf.Clamp(target.z, -4f, 4f);
            // the jaw snaps faster than the eyes drift
            float kJaw = immediate ? 1f : 1f - Mathf.Exp(-22f * Mathf.Max(0f, deltaTime));
            float kEye = immediate ? 1f : 1f - Mathf.Exp(-30f * Mathf.Max(0f, deltaTime));
            _facePose.Set(Mathf.Lerp(_facePose.x, target.x, kJaw),
                          Mathf.Lerp(_facePose.y, target.y, kEye),
                          Mathf.Lerp(_facePose.z, target.z, kEye));
            _faceJaw.Apply(_facePose.x, 0f);
            _faceLeftEye.Apply(_facePose.y, _facePose.z);
            _faceRightEye.Apply(_facePose.y, _facePose.z);
            AnimateFaceMorphs(deltaTime, immediate);
        }

        void AnimateFaceMorphs(float deltaTime, bool immediate)
        {
            if (_faceMorphs.Length == 0) return;
            Vector3 browTarget = Vector3.zero;
            switch (CurrentExpression)
            {
                case PassengerExpression.Curious: browTarget.Set(0.18f, 0.12f, 0f); break;
                case PassengerExpression.Thoughtful: browTarget.Set(0f, 0f, 0.32f); break;
                case PassengerExpression.Satisfied: browTarget.Set(0.12f, 0.18f, 0f); break;
                case PassengerExpression.Disappointed: browTarget.Set(0.38f, 0f, 0.08f); break;
            }
            if (Pose == PassengerPose.DriverDriving) browTarget = Vector3.zero;
            float blend = immediate ? 1f : 1f - Mathf.Exp(-9f * Mathf.Max(0f, deltaTime));
            _browPose = Vector3.Lerp(_browPose, browTarget, blend);

            float blink = 0f;
            if (!immediate && deltaTime > 0f && HasBlinkChannels)
            {
                if (_blinkTime < 0f && Time.time >= _nextBlink) _blinkTime = 0f;
                if (_blinkTime >= 0f)
                {
                    _blinkTime += deltaTime;
                    float phase = Mathf.Clamp01(_blinkTime / BlinkDuration);
                    // Fast closure, slower reopening; weights return to the authored resting value.
                    blink = phase < 0.4f ? Mathf.SmoothStep(0f, 1f, phase / 0.4f)
                        : 1f - Mathf.SmoothStep(0f, 1f, (phase - 0.4f) / 0.6f);
                    if (_blinkTime >= BlinkDuration)
                    {
                        _blinkTime = -1f;
                        _nextBlink = Time.time + Random.Range(2.4f, 5.5f);
                    }
                }
            }
            for (int i = 0; i < _faceMorphs.Length; i++)
            {
                FaceMorph morph = _faceMorphs[i];
                if (morph.renderer == null) continue;
                float weight;
                switch (morph.channel)
                {
                    case FaceMorphChannel.Blink: weight = blink; break;
                    case FaceMorphChannel.BrowInnerUp: weight = _browPose.x; break;
                    case FaceMorphChannel.BrowOuterUp: weight = _browPose.y; break;
                    default: weight = _browPose.z; break;
                }
                morph.renderer.SetBlendShapeWeight(morph.index,
                    Mathf.Lerp(morph.restWeight, 100f, Mathf.Clamp01(weight)));
            }
        }

        /// <summary>
        /// Eye pitch/yaw (deg) that points the eyes at the camera looking at this character: the in-car webcam
        /// while it renders, otherwise the main camera. Null when the camera is behind the head or far off-axis.
        /// </summary>
        Vector2? GazeAtCamera()
        {
            if (_boneHead == null || !HasEyeRig) return null;
            var manager = GameManager.Instance;
            Camera liveCamera = manager != null ? manager.PassengerLiveCamera : null;
            Camera cam = liveCamera != null && liveCamera.isActiveAndEnabled && liveCamera.targetTexture != null
                ? liveCamera : _faceMainCamera;
            if (cam == null || !cam.isActiveAndEnabled) return null;
            Vector3 eye = _faceLeftEye.bone != null ? _faceLeftEye.bone.position : _faceRightEye.bone.position;
            if (_faceLeftEye.bone != null && _faceRightEye.bone != null)
                eye = (eye + _faceRightEye.bone.position) * 0.5f;
            // character frame; the head only turns a few degrees, its turn is subtracted below
            Vector3 d = cam.transform.position - eye;
            Vector3 up = transform.up, right = transform.right, fwd = transform.forward;
            float x = Vector3.Dot(d, right), y = Vector3.Dot(d, up), z = Vector3.Dot(d, fwd);
            if (z <= 0.05f) return null;
            float yaw = Mathf.Atan2(x, z) * Mathf.Rad2Deg, pitch = -Mathf.Atan2(y, Mathf.Sqrt(x * x + z * z)) * Mathf.Rad2Deg;
            yaw -= _headYawNow; pitch -= _headPitchNow;
            if (Mathf.Abs(yaw) > 40f || Mathf.Abs(pitch) > 30f) return null;
            Vector2 gaze = Vector2.zero;
            gaze.Set(Mathf.Clamp(pitch, -10f, 10f) * GazePitchSign, Mathf.Clamp(yaw, -14f, 14f) * GazeYawSign);
            return gaze;
        }

        // Rodin eye bones' local axes vs the head frame (sign flips found by checking the gaze in Unity)
        const float GazePitchSign = 1f, GazeYawSign = 1f;
    }
}
