using UnityEngine;

namespace OddsCab
{
    public enum PassengerPose
    {
        // Row 1 of 08-animation.png
        Waiting,                // Panel 1.1: Relaxed curbside wait beside streetlamp, hands near pockets
        NoticingTaxi,           // Panel 1.2: Spotting approaching taxi with 3 black attention marks (///) by head
        WavingBackView,         // Panel 1.3: 3/4 back view, teal backpack visible, right arm raised high waving at taxi
        Waving = WavingBackView,// Alias for roadside pedestrian pool
        ApproachingDoor,        // Panel 1.4: Stepping off curb in happy anticipation waving at the taxi side door

        // Row 2 of 08-animation.png
        WalkingToCab,           // Panels 2.1..2.4: 4 consecutive walking cycle master poses in clean side view (no extra limbs)

        // Back-Seat & Row 4 States
        SittingInCab,           // Seated in dark-green ribbed back seat (Curious / Thoughtful)
        Satisfied,              // Row 4.3: Cheerful satisfied smile & victory cheer; stays in cab until next stop

        // Row 3 of 08-animation.png (Incorrect answer sequence)
        DisappointedInCab,      // Panel 3.1: Chin resting on hand in back seat with disappointed expression
        SteppingOutOfDoor,      // Panel 3.2: Stepping out from open yellow taxi door, holding frame, looking down
        StandingSadBesideDoor,  // Panel 3.3: Standing beside open door in 3/4 rear view, teal backpack, head bowed
        WalkingAway,            // Panel 3.4: Walking away in 3/4 rear-side view waving goodbye

        // Taxi Driver Pose (ModelsV2/Taxi_Şöförü)
        DriverDriving           // Seated in front-left driver seat steering the vintage yellow taxi
    }

    public enum PassengerExpression
    {
        Curious = 0,        // Row 4.1: Wide-eyed curious head tilt
        Thoughtful = 1,     // Row 4.2: Hand to chin, furrowed brow thoughtful gaze
        Satisfied = 2,      // Row 4.3: Warm joyful smile & victory cheer
        Disappointed = 3    // Row 4.4: Downcast eyes, sad expression
    }

    /// <summary>
    /// Full-body procedural skeletal & soft-mesh character animator for the Rodin 3D rigged characters
    /// in Resources/ModelsV2 (52-bone humanoid skeleton) and fallback meshes:
    /// - Automatically converts the Rodin T-pose bind stance into a natural relaxed stance and drives
    ///   Hips, Spine, Spine1, Spine2, Neck, Head, Shoulders, Upper Arms, Forearms, Hands, Thighs, Calves, and Feet
    ///   in true Character Root Space across all 12 game states.
    /// - Includes the teal-green backpack and 3 black attention marks (///).
    /// </summary>
    public partial class PassengerActor : MonoBehaviour
    {
        public PassengerProfile Profile { get; private set; }
        public PassengerPose Pose { get; private set; } = PassengerPose.Waiting;
        public PassengerExpression CurrentExpression { get; private set; } = PassengerExpression.Curious;
        public int WalkCycleStep { get; private set; } = 1;

        Vector3 _basePos;
        Quaternion _baseRot;
        float _phase;
        float _poseTimer;
        float _walkDistance;
        float _walkFrameDistance, _walkWeight;
        float _restFootHeight;
        Transform[] _poseBones;
        Quaternion[] _transitionRotations;
        Vector3 _transitionHipsPosition;
        bool _blendPose;
        static readonly int PoseParameter = Animator.StringToHash("Pose");
        static readonly int ExpressionParameter = Animator.StringToHash("Expression");

        Transform _backpackProp;
        Transform _characterEModelProp;
        Vector3 _characterPropBasePos;
        Quaternion _characterPropBaseRot;
        GameObject _attentionMarksGroup;
        Material _attentionMaterial;

        // Soft-skinned mesh deformation fallback + Rodin 52-bone Humanoid Skeleton Rig support
        MeshFilter _meshFilter;
        SkinnedMeshRenderer _skinnedRenderer;
        Animator _animator;
        Mesh _workingMesh;
        Vector3[] _origVerts;
        Vector3[] _deformedVerts;
        float _minY, _maxY, _heightY;
        float _centerX, _centerZ;

        // Rodin / Mixamo 52-Bone Humanoid Skeleton Transforms & Rest Local Rotations
        bool _hasBoneRig;
        Transform _boneHips, _boneSpine, _boneSpine1, _boneSpine2, _boneNeck, _boneHead;
        Transform _boneLeftShoulder, _boneLeftArm, _boneLeftForeArm, _boneLeftHand;
        Transform _boneRightShoulder, _boneRightArm, _boneRightForeArm, _boneRightHand;
        Transform _boneLeftUpLeg, _boneLeftLeg, _boneLeftFoot;
        Transform _boneRightUpLeg, _boneRightLeg, _boneRightFoot;

        Quaternion _restHips, _restSpine, _restSpine1, _restSpine2, _restNeck, _restHead;
        Vector3 _restHipsPos;
        Quaternion _restLeftShoulder, _restLeftArm, _restLeftForeArm, _restLeftHand;
        Quaternion _restRightShoulder, _restRightArm, _restRightForeArm, _restRightHand;
        Quaternion _restLeftUpLeg, _restLeftLeg, _restLeftFoot;
        Quaternion _restRightUpLeg, _restRightLeg, _restRightFoot;

        // Automatic T-pose to relaxed side-arm adduction rotations in Character Root Space
        Quaternion _qLeftArmRelaxChar = Quaternion.identity;
        Quaternion _qRightArmRelaxChar = Quaternion.identity;

        // Left-hand grip for held props (cane, briefcase): finger chains with their rest rotations and the
        // bone-local axis that curls each finger toward the palm.
        Transform _leftIndex1, _leftMiddle1, _leftPinky1;
        Transform[][] _leftFingerChains;
        Quaternion[][] _leftFingerRest;
        Vector3[] _leftFingerCurlAxis;
        bool[] _leftFingerIsThumb;
        Vector3 _propBaseScale = Vector3.one;
        PassengerHandPose _rightHandPose;
        static readonly float[] HeldFingerCurl = { 62f, 78f, 55f };
        static readonly float[] HeldThumbCurl = { 25f, 45f, 35f };
        bool IsRearSeatPose => Pose == PassengerPose.SittingInCab || Pose == PassengerPose.Satisfied
            || Pose == PassengerPose.DisappointedInCab;

        public void Setup(PassengerProfile profile, PassengerPose initialPose)
        {
            Profile = profile;
            Pose = initialPose;
            _basePos = transform.localPosition;
            _baseRot = transform.localRotation;
            _phase = Random.value * 6.28f;

            SetupSkeletonRig();
            if (!_hasBoneRig)
                SetupMeshDeformer();
            BuildBackpackAndAttentionMarks();
            UpdatePropsAndExpression();
        }

        void OnDestroy()
        {
            if (_workingMesh != null)
                Destroy(_workingMesh);
            if (_attentionMaterial != null) Destroy(_attentionMaterial);
        }

        void SetupSkeletonRig()
        {
            _animator = GetComponentInChildren<Animator>();
            var skinnedRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var smr in skinnedRenderers)
                smr.updateWhenOffscreen = true;

            var allTransforms = GetComponentsInChildren<Transform>(true);
            foreach (var tr in allTransforms)
            {
                if (tr == transform) continue;
                string n = tr.name.ToLowerInvariant();
                bool isLeft = n.Contains("left") || n.EndsWith("_l") || n.EndsWith(".l") || n.StartsWith("l_");
                bool isRight = n.Contains("right") || n.EndsWith("_r") || n.EndsWith(".r") || n.StartsWith("r_");

                if (_boneHips == null && (n == "hips" || n.Contains("hips") || n.Contains("pelvis")))
                    _boneHips = tr;
                else if (_boneSpine == null && n == "spine")
                    _boneSpine = tr;
                else if (_boneSpine1 == null && (n == "spine1" || n == "chest"))
                    _boneSpine1 = tr;
                else if (_boneSpine2 == null && (n == "spine2" || n == "upperchest"))
                    _boneSpine2 = tr;
                else if (_boneNeck == null && (n == "neck" || n.Contains("neck")))
                    _boneNeck = tr;
                else if (_boneHead == null && (n == "head" || (n.Contains("head") && !n.Contains("top") && !n.Contains("end"))))
                    _boneHead = tr;
                else if (_boneLeftShoulder == null && (n == "leftshoulder" || (isLeft && (n.Contains("shoulder") || n.Contains("clavicle") || n.Contains("collar")))))
                    _boneLeftShoulder = tr;
                else if (_boneRightShoulder == null && (n == "rightshoulder" || (isRight && (n.Contains("shoulder") || n.Contains("clavicle") || n.Contains("collar")))))
                    _boneRightShoulder = tr;
                else if (_boneLeftForeArm == null && (n == "leftforearm" || (isLeft && (n.Contains("forearm") || n.Contains("lowerarm")))))
                    _boneLeftForeArm = tr;
                else if (_boneRightForeArm == null && (n == "rightforearm" || (isRight && (n.Contains("forearm") || n.Contains("lowerarm")))))
                    _boneRightForeArm = tr;
                else if (_boneLeftArm == null && (n == "leftarm" || (isLeft && (n.Contains("upperarm") || (n.Contains("arm") && !n.Contains("fore") && !n.Contains("lower") && !n.Contains("shoulder"))))))
                    _boneLeftArm = tr;
                else if (_boneRightArm == null && (n == "rightarm" || (isRight && (n.Contains("upperarm") || (n.Contains("arm") && !n.Contains("fore") && !n.Contains("lower") && !n.Contains("shoulder"))))))
                    _boneRightArm = tr;
                else if (_boneLeftHand == null && (n == "lefthand" || (isLeft && n.EndsWith("hand"))))
                    _boneLeftHand = tr;
                else if (_boneRightHand == null && (n == "righthand" || (isRight && n.EndsWith("hand"))))
                    _boneRightHand = tr;
                else if (_boneLeftUpLeg == null && (n == "leftupleg" || (isLeft && (n.Contains("upleg") || n.Contains("thigh") || n.Contains("upperleg")))))
                    _boneLeftUpLeg = tr;
                else if (_boneRightUpLeg == null && (n == "rightupleg" || (isRight && (n.Contains("upleg") || n.Contains("thigh") || n.Contains("upperleg")))))
                    _boneRightUpLeg = tr;
                else if (_boneLeftLeg == null && (n == "leftleg" || (isLeft && (n.Contains("calf") || n.Contains("shin") || n.Contains("lowerleg") || (n.Contains("leg") && !n.Contains("up"))))))
                    _boneLeftLeg = tr;
                else if (_boneRightLeg == null && (n == "rightleg" || (isRight && (n.Contains("calf") || n.Contains("shin") || n.Contains("lowerleg") || (n.Contains("leg") && !n.Contains("up"))))))
                    _boneRightLeg = tr;
                else if (_boneLeftFoot == null && (n == "leftfoot" || (isLeft && n.Contains("foot"))))
                    _boneLeftFoot = tr;
                else if (_boneRightFoot == null && (n == "rightfoot" || (isRight && n.Contains("foot"))))
                    _boneRightFoot = tr;
            }

            _hasBoneRig = (_boneHead != null || _boneSpine != null) && (_boneLeftArm != null || _boneRightArm != null || _boneLeftUpLeg != null);
            if (_hasBoneRig)
            {
                if (_boneHips != null)
                {
                    _restHips = _boneHips.localRotation;
                    _restHipsPos = _boneHips.localPosition;
                }
                if (_boneSpine != null) _restSpine = _boneSpine.localRotation;
                if (_boneSpine1 != null) _restSpine1 = _boneSpine1.localRotation;
                if (_boneSpine2 != null) _restSpine2 = _boneSpine2.localRotation;
                if (_boneNeck != null) _restNeck = _boneNeck.localRotation;
                if (_boneHead != null) _restHead = _boneHead.localRotation;
                if (_boneLeftShoulder != null) _restLeftShoulder = _boneLeftShoulder.localRotation;
                if (_boneLeftArm != null) _restLeftArm = _boneLeftArm.localRotation;
                if (_boneLeftForeArm != null) _restLeftForeArm = _boneLeftForeArm.localRotation;
                if (_boneLeftHand != null) _restLeftHand = _boneLeftHand.localRotation;
                if (_boneRightShoulder != null) _restRightShoulder = _boneRightShoulder.localRotation;
                if (_boneRightArm != null) _restRightArm = _boneRightArm.localRotation;
                if (_boneRightForeArm != null) _restRightForeArm = _boneRightForeArm.localRotation;
                if (_boneRightHand != null) _restRightHand = _boneRightHand.localRotation;
                if (_boneLeftUpLeg != null) _restLeftUpLeg = _boneLeftUpLeg.localRotation;
                if (_boneLeftLeg != null) _restLeftLeg = _boneLeftLeg.localRotation;
                if (_boneLeftFoot != null) _restLeftFoot = _boneLeftFoot.localRotation;
                if (_boneRightUpLeg != null) _restRightUpLeg = _boneRightUpLeg.localRotation;
                if (_boneRightLeg != null) _restRightLeg = _boneRightLeg.localRotation;
                if (_boneRightFoot != null) _restRightFoot = _boneRightFoot.localRotation;

                // Measure bind/rest arm directions in character root space and compute T-pose -> relaxed side-arm rotation
                if (_boneLeftArm != null && _boneLeftForeArm != null)
                {
                    Vector3 restDirL = transform.InverseTransformDirection((_boneLeftForeArm.position - _boneLeftArm.position).normalized);
                    Vector3 targetRelaxL = new Vector3(-0.18f, -0.97f, 0.03f).normalized;
                    _qLeftArmRelaxChar = Quaternion.FromToRotation(restDirL, targetRelaxL);
                }
                if (_boneRightArm != null && _boneRightForeArm != null)
                {
                    Vector3 restDirR = transform.InverseTransformDirection((_boneRightForeArm.position - _boneRightArm.position).normalized);
                    Vector3 targetRelaxR = new Vector3(0.18f, -0.97f, 0.03f).normalized;
                    _qRightArmRelaxChar = Quaternion.FromToRotation(restDirR, targetRelaxR);
                }
                SetupLeftHandGrip();
                _rightHandPose = new PassengerHandPose(_boneRightHand, false);
                _poseBones = new[] { _boneHips, _boneSpine, _boneSpine1, _boneSpine2, _boneNeck, _boneHead,
                    _boneLeftShoulder, _boneLeftArm, _boneLeftForeArm, _boneLeftHand,
                    _boneRightShoulder, _boneRightArm, _boneRightForeArm, _boneRightHand,
                    _boneLeftUpLeg, _boneLeftLeg, _boneLeftFoot, _boneRightUpLeg, _boneRightLeg, _boneRightFoot };
                _transitionRotations = new Quaternion[_poseBones.Length];
                if (_boneLeftFoot != null && _boneRightFoot != null)
                    _restFootHeight = Mathf.Min(transform.InverseTransformPoint(_boneLeftFoot.position).y,
                        transform.InverseTransformPoint(_boneRightFoot.position).y);
            }
            SetupFaceRig();
        }

        static Transform FindChildBone(Transform root, string nameLower)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name.ToLowerInvariant() == nameLower) return t;
            return null;
        }

        /// <summary>
        /// Mixamo-style finger chains (LeftHandIndex1..3 etc.). Measured in the bind pose: the curl axis of each
        /// finger turns it from its rest direction toward the palm normal.
        /// </summary>
        void SetupLeftHandGrip()
        {
            if (_boneLeftHand == null) return;
            _leftIndex1 = FindChildBone(_boneLeftHand, "lefthandindex1");
            _leftMiddle1 = FindChildBone(_boneLeftHand, "lefthandmiddle1");
            _leftPinky1 = FindChildBone(_boneLeftHand, "lefthandpinky1");
            if (_leftIndex1 == null || _leftMiddle1 == null || _leftPinky1 == null) return;

            Vector3 palm = LeftPalmNormal();
            string[] fingers = { "thumb", "index", "middle", "ring", "pinky" };
            var chains = new System.Collections.Generic.List<Transform[]>();
            var rests = new System.Collections.Generic.List<Quaternion[]>();
            var axes = new System.Collections.Generic.List<Vector3>();
            var thumbs = new System.Collections.Generic.List<bool>();
            foreach (var f in fingers)
            {
                var j1 = FindChildBone(_boneLeftHand, "lefthand" + f + "1");
                var j2 = FindChildBone(_boneLeftHand, "lefthand" + f + "2");
                var j3 = FindChildBone(_boneLeftHand, "lefthand" + f + "3");
                if (j1 == null || j2 == null) continue;
                var chain = j3 != null ? new[] { j1, j2, j3 } : new[] { j1, j2 };
                Vector3 dir = (j2.position - j1.position).normalized;
                var q = Quaternion.FromToRotation(dir, Vector3.RotateTowards(dir, palm, 0.6f, 0f));
                q.ToAngleAxis(out _, out Vector3 axisWorld);
                chains.Add(chain);
                rests.Add(System.Array.ConvertAll(chain, b => b.localRotation));
                // One world axis per finger, stored per joint in that joint's own frame
                axes.Add(axisWorld);
                thumbs.Add(f == "thumb");
            }
            _leftFingerChains = chains.ToArray();
            _leftFingerRest = rests.ToArray();
            _leftFingerIsThumb = thumbs.ToArray();
            // Convert to joint-local axes now, while every joint is still in its rest orientation
            var local = new System.Collections.Generic.List<Vector3>();
            for (int c = 0; c < _leftFingerChains.Length; c++)
                foreach (var j in _leftFingerChains[c])
                    local.Add(Quaternion.Inverse(j.rotation) * axes[c]);
            _leftFingerCurlAxis = local.ToArray();
        }

        /// <summary>Left palm normal (world): thumb-side axis × finger axis.</summary>
        Vector3 LeftPalmNormal()
        {
            Vector3 across = (_leftIndex1.position - _leftPinky1.position).normalized;
            Vector3 along = (_leftMiddle1.position - _boneLeftHand.position).normalized;
            return Vector3.Cross(across, along).normalized;
        }

        /// <summary>
        /// Holding: turn the palm toward the thigh (the relaxed rig hangs with palms forward) and close the
        /// fingers around a handle. Returns the fist's grip point in world space.
        /// </summary>
        Vector3 ApplyLeftHandGrip(bool holding)
        {
            if (_leftFingerChains == null) return _boneLeftHand != null ? _boneLeftHand.position : transform.position;
            int k = 0;
            bool relaxed = Pose == PassengerPose.Waiting || Pose == PassengerPose.WalkingToCab || Pose == PassengerPose.WalkingAway
                || Pose == PassengerPose.StandingSadBesideDoor;
            if (holding || relaxed || IsRearSeatPose)
            {
                Vector3 along = (_leftMiddle1.position - _boneLeftHand.position).normalized;
                Vector3 toBody = Vector3.ProjectOnPlane(IsRearSeatPose ? -transform.up : transform.right, along).normalized;
                _boneLeftHand.rotation = Quaternion.FromToRotation(LeftPalmNormal(), toBody) * _boneLeftHand.rotation;
            }
            for (int c = 0; c < _leftFingerChains.Length; c++)
            {
                var chain = _leftFingerChains[c];
                for (int j = 0; j < chain.Length; j++, k++)
                {
                    float angle = (holding ? 1f : relaxed || IsRearSeatPose ? .3f : .12f)
                        * (_leftFingerIsThumb[c] ? HeldThumbCurl[j] : HeldFingerCurl[j]);
                    chain[j].localRotation = _leftFingerRest[c][j] * Quaternion.AngleAxis(angle, _leftFingerCurlAxis[k]);
                }
            }
            // The handle sits inside the curled fingers: level with the knuckles, just off the palm
            float s = transform.lossyScale.y;
            return _leftMiddle1.position + LeftPalmNormal() * (0.024f * s);
        }

        void ResetBonesToRest()
        {
            if (_boneHips != null)
            {
                _boneHips.localRotation = _restHips;
                _boneHips.localPosition = _restHipsPos;
            }
            if (_boneSpine != null) _boneSpine.localRotation = _restSpine;
            if (_boneSpine1 != null) _boneSpine1.localRotation = _restSpine1;
            if (_boneSpine2 != null) _boneSpine2.localRotation = _restSpine2;
            if (_boneNeck != null) _boneNeck.localRotation = _restNeck;
            if (_boneHead != null) _boneHead.localRotation = _restHead;
            if (_boneLeftShoulder != null) _boneLeftShoulder.localRotation = _restLeftShoulder;
            if (_boneLeftArm != null) _boneLeftArm.localRotation = _restLeftArm;
            if (_boneLeftForeArm != null) _boneLeftForeArm.localRotation = _restLeftForeArm;
            if (_boneLeftHand != null) _boneLeftHand.localRotation = _restLeftHand;
            if (_boneRightShoulder != null) _boneRightShoulder.localRotation = _restRightShoulder;
            if (_boneRightArm != null) _boneRightArm.localRotation = _restRightArm;
            if (_boneRightForeArm != null) _boneRightForeArm.localRotation = _restRightForeArm;
            if (_boneRightHand != null) _boneRightHand.localRotation = _restRightHand;
            if (_boneLeftUpLeg != null) _boneLeftUpLeg.localRotation = _restLeftUpLeg;
            if (_boneLeftLeg != null) _boneLeftLeg.localRotation = _restLeftLeg;
            if (_boneLeftFoot != null) _boneLeftFoot.localRotation = _restLeftFoot;
            if (_boneRightUpLeg != null) _boneRightUpLeg.localRotation = _restRightUpLeg;
            if (_boneRightLeg != null) _boneRightLeg.localRotation = _restRightLeg;
            if (_boneRightFoot != null) _boneRightFoot.localRotation = _restRightFoot;
        }

        void RotateBoneInCharSpace(Transform bone, Quaternion qCharDelta)
        {
            if (bone == null) return;
            Quaternion worldDelta = transform.rotation * qCharDelta * Quaternion.Inverse(transform.rotation);
            bone.rotation = worldDelta * bone.rotation;
        }

        void SetupMeshDeformer()
        {
            _meshFilter = GetComponentInChildren<MeshFilter>();
            if (_meshFilter != null && _meshFilter.sharedMesh != null && _meshFilter.sharedMesh.isReadable && _meshFilter.sharedMesh.vertexCount <= 8000)
            {
                _workingMesh = Instantiate(_meshFilter.sharedMesh);
                _workingMesh.MarkDynamic();
                _meshFilter.mesh = _workingMesh;
            }
            else
            {
                _skinnedRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
                if (_skinnedRenderer != null && _skinnedRenderer.sharedMesh != null && _skinnedRenderer.sharedMesh.isReadable && _skinnedRenderer.sharedMesh.vertexCount <= 8000)
                {
                    _workingMesh = Instantiate(_skinnedRenderer.sharedMesh);
                    _workingMesh.MarkDynamic();
                    _skinnedRenderer.sharedMesh = _workingMesh;
                }
            }

            if (_workingMesh != null)
            {
                _origVerts = _workingMesh.vertices;
                _deformedVerts = new Vector3[_origVerts.Length];
                Bounds b = _workingMesh.bounds;
                _minY = b.min.y;
                _maxY = b.max.y;
                _heightY = Mathf.Max(0.01f, _maxY - _minY);
                _centerX = b.center.x;
                _centerZ = b.center.z;
            }
        }

        void BuildBackpackAndAttentionMarks()
        {
            // Note: User override explicitly requested removing backpacks from all passengers.
            _backpackProp = null;

            // 3 Black Attention Marks (///) beside head for Row 1 Panel 1.2 (NoticingTaxi)
            _attentionMarksGroup = new GameObject("AttentionMarks_Row1Panel2");
            _attentionMarksGroup.transform.SetParent(transform, false);
            _attentionMarksGroup.transform.localPosition = new Vector3(0.38f, 1.62f, 0.12f);

            _attentionMaterial = GlbModelLoader.MakeMat(new Color(0.10f, 0.14f, 0.20f), 0.2f);
            float[] angles = { 28f, 4f, -20f };
            float[] yOffsets = { 0.12f, 0f, -0.11f };
            for (int i = 0; i < 3; i++)
            {
                var ray = MakePart(_attentionMarksGroup.transform, "Ray_" + i, PrimitiveType.Cube,
                    new Vector3(0f, yOffsets[i], 0f), new Vector3(0.14f, 0.032f, 0.032f), _attentionMaterial);
                ray.localRotation = Quaternion.Euler(0f, 0f, angles[i]);
            }
            _attentionMarksGroup.SetActive(false);

            // Signature 3D Passenger Prop from Resources/EModel (Walking Cane, Croissant Box, Ukulele Case, Briefcase)
            BuildCharacterSignatureProp();
        }

        void BuildCharacterSignatureProp()
        {
            if (Profile == null || string.IsNullOrEmpty(Profile.eModelPropName)) return;

            Vector3 propPos = new Vector3(-0.26f, 0.45f, 0.08f);
            Quaternion propRot = Quaternion.identity;
            float propHeight = 0.40f;

            switch (Profile.id)
            {
                case PassengerId.Grandfather:
                    // Yolcu_Dede_Baston: Walking cane held in left hand resting on ground
                    propPos = new Vector3(-0.26f, 0.0f, 0.14f);
                    propRot = Quaternion.identity;
                    propHeight = 0.88f;
                    break;
                case PassengerId.Chef:
                    // Aşçı_Kadın_Kruvasan_Pasta_Kutusu: Croissant & pastry box carried in left hand
                    propPos = new Vector3(-0.25f, 0.76f, 0.18f);
                    propRot = Quaternion.Euler(0f, 18f, 0f);
                    propHeight = 0.28f;
                    break;
                case PassengerId.Minstrel:
                    // Müzisyen_Genç_Çantası_Ukulele_Kılıfı: Ukulele case slung across his back
                    propPos = new Vector3(0.0f, 0.76f, -0.19f);
                    propRot = Quaternion.Euler(0f, 180f, 14f);
                    propHeight = 0.68f;
                    break;
                case PassengerId.Professional:
                    // Memur_Kadın_Yolcu_Evrak_Çantası: Leather briefcase held in left hand
                    propPos = new Vector3(-0.28f, 0.42f, 0.06f);
                    propRot = Quaternion.Euler(0f, 90f, 0f);
                    propHeight = 0.36f;
                    break;
            }

            if (GlbModelLoader.TryInstantiateEModel(Profile.eModelPropName, transform, propPos, propRot, propHeight, out var propGo))
            {
                _characterEModelProp = propGo.transform;
                _characterPropBasePos = propPos;
                _characterPropBaseRot = propRot;
                _propBaseScale = propGo.transform.localScale;
            }
        }

        static Transform MakePart(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            Destroy(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go.transform;
        }

        public void SetPose(PassengerPose pose, Vector3 localPos, Quaternion localRot)
        {
            _blendPose = _hasBoneRig && Pose != pose;
            if (_blendPose)
            {
                for (int i = 0; i < _poseBones.Length; i++)
                    if (_poseBones[i] != null) _transitionRotations[i] = _poseBones[i].localRotation;
                if (_boneHips != null) _transitionHipsPosition = _boneHips.localPosition;
            }
            _hasExpressionOverride = false;
            Pose = pose;
            _poseTimer = 0f;
            _walkDistance = 0f;
            _walkFrameDistance = _walkWeight = 0f;
            _basePos = localPos;
            _baseRot = localRot;
            transform.localPosition = localPos;
            transform.localRotation = localRot;
            UpdatePropsAndExpression();
        }

        public void SetExpressionOverride(PassengerExpression expr)
        {
            _hasExpressionOverride = true;
            CurrentExpression = expr;
            ApplyStaticExpressionDeform(expr);
        }

        public void UpdateBaseTransform(Vector3 localPos, Quaternion localRot)
        {
            if (Pose == PassengerPose.WalkingToCab || Pose == PassengerPose.WalkingAway)
            {
                Vector3 travel = localPos - _basePos;
                if (transform.parent != null) travel = transform.parent.TransformVector(travel);
                float distance = travel.magnitude;
                // A route jump repositions the actor; it must not advance the walking cycle by kilometres.
                if (distance < 1.8f) { _walkDistance += distance; _walkFrameDistance += distance; }
            }
            _basePos = localPos;
            _baseRot = localRot;
        }

        void UpdatePropsAndExpression()
        {
            if (_attentionMarksGroup != null)
                _attentionMarksGroup.SetActive(Pose == PassengerPose.NoticingTaxi);

            if (_backpackProp != null)
            {
                bool hidePack = Pose == PassengerPose.SittingInCab ||
                                Pose == PassengerPose.Satisfied ||
                                Pose == PassengerPose.DisappointedInCab ||
                                Pose == PassengerPose.DriverDriving ||
                                (_characterEModelProp != null && Profile != null && Profile.id == PassengerId.Minstrel);
                _backpackProp.gameObject.SetActive(!hidePack);
            }

            if (_characterEModelProp != null)
            {
                bool hideProp = Pose == PassengerPose.SittingInCab ||
                                Pose == PassengerPose.Satisfied ||
                                Pose == PassengerPose.DisappointedInCab ||
                                Pose == PassengerPose.DriverDriving;
                // after a wrong answer they get out and walk off with their own cane / case / ukulele
                _characterEModelProp.gameObject.SetActive(!hideProp);
            }

            if (_hasExpressionOverride) return;
            switch (Pose)
            {
                case PassengerPose.Satisfied:
                    CurrentExpression = PassengerExpression.Satisfied;
                    break;
                case PassengerPose.DisappointedInCab:
                case PassengerPose.SteppingOutOfDoor:
                case PassengerPose.StandingSadBesideDoor:
                case PassengerPose.WalkingAway:
                    CurrentExpression = PassengerExpression.Disappointed;
                    break;
                case PassengerPose.SittingInCab:
                    CurrentExpression = (Mathf.Sin(Time.time * 0.85f + _phase) > 0f)
                        ? PassengerExpression.Curious
                        : PassengerExpression.Thoughtful;
                    break;
                default:
                    CurrentExpression = PassengerExpression.Curious;
                    break;
            }
        }

        // Knee bend for walking poses (-1: derive from the hip as before)
        float _walkKneeL = -1f, _walkKneeR = -1f;

        /// <summary>
        /// Walk cycle legs: hips swing ±<paramref name="stride"/> degrees; a knee bends only while its leg swings
        /// forward (most at mid-swing) and is straight at heel strike and through stance, so the leading leg
        /// never kicks up stiff.
        /// </summary>
        void SetWalkLegs(float phase, float stride, out float leftPitch, out float rightPitch)
        {
            PassengerLocomotion.Legs(phase, stride, out leftPitch, out rightPitch, out _walkKneeL, out _walkKneeR);
            leftPitch *= _walkWeight; rightPitch *= _walkWeight;
            _walkKneeL *= _walkWeight; _walkKneeR *= _walkWeight;
        }

        void Update()
        {
            _poseTimer += Time.deltaTime;
            float movement = Mathf.Clamp01(_walkFrameDistance / Mathf.Max(.0001f, Time.deltaTime * .65f));
            _walkWeight = Mathf.MoveTowards(_walkWeight, movement, Time.deltaTime * 7f);
            _walkFrameDistance = 0f;
            _walkKneeL = _walkKneeR = -1f;
            float t = Time.time * 3.5f + _phase;
            UpdatePropsAndExpression();

            float leftLegPitch = 0f, rightLegPitch = 0f;
            float leftArmPitch = 0f, rightArmPitch = 0f, rightArmRaiseZ = 0f;
            float leftForeArmPitch = -14f, rightForeArmPitch = -14f, rightForeArmRoll = 0f;
            float headPitch = 0f, headYaw = 0f, headRoll = 0f;
            float torsoLeanX = 0f, torsoYaw = 0f;

            switch (Pose)
            {
                case PassengerPose.Waiting:
                    // Row 1 Panel 1.1: Relaxed curbside stance, hands near pockets, light calm breathing
                    {
                        float breath = Mathf.Sin(t * 0.55f) * 0.016f;
                        transform.localPosition = _basePos + new Vector3(0f, Mathf.Max(0f, breath), 0f);
                        transform.localRotation = _baseRot * Quaternion.Euler(0f, Mathf.Sin(t * 0.35f) * 3f, 0f);
                        torsoLeanX = Mathf.Sin(t * 0.55f) * 1.5f;
                        headYaw = Mathf.Sin(t * 0.4f) * 7f;
                        leftForeArmPitch = -16f;
                        rightForeArmPitch = -16f;
                    }
                    break;

                case PassengerPose.NoticingTaxi:
                    // Row 1 Panel 1.2: Leaning toward the street spotting the approaching yellow taxi (3 black marks /// active)
                    transform.localPosition = _basePos + new Vector3(0f, Mathf.Abs(Mathf.Sin(t * 1.4f)) * 0.03f, 0f);
                    transform.localRotation = _baseRot * Quaternion.Euler(4f, -12f, -3f);
                    torsoLeanX = 8f;
                    headYaw = -18f;
                    headPitch = -5f;
                    leftForeArmPitch = -26f;
                    rightForeArmPitch = -28f;
                    if (_attentionMarksGroup != null)
                    {
                        float pulse = 1f + Mathf.Sin(t * 5f) * 0.14f;
                        _attentionMarksGroup.transform.localScale = Vector3.one * pulse;
                    }
                    break;

                case PassengerPose.WavingBackView:
                    // Row 1 Panel 1.3: Turned 3/4 back view toward approaching taxi, right arm raised high waving
                    transform.localPosition = _basePos + new Vector3(0f, Mathf.Abs(Mathf.Sin(t * 1.8f)) * 0.04f, 0f);
                    transform.localRotation = _baseRot;
                    rightArmRaiseZ = 122f + Mathf.Sin(t * 3.8f) * 16f;
                    rightArmPitch = -24f;
                    rightForeArmPitch = -22f;
                    rightForeArmRoll = Mathf.Sin(t * 4.2f) * 18f;
                    headYaw = 15f;
                    headPitch = -8f;
                    break;

                case PassengerPose.ApproachingDoor:
                    // Stepping beside open taxi door:
                    // Initial warm greeting wave that smoothly eases into an attentive, smiling ready-to-board stance!
                    {
                        float waveBlend = Mathf.Clamp01(1.0f - (_poseTimer - 1.2f) / 0.8f);
                        float waveMotion = Mathf.Sin(t * 3.2f) * waveBlend;
                        transform.localPosition = _basePos + new Vector3(0f, Mathf.Abs(Mathf.Sin(t * 1.5f)) * 0.02f, 0f);
                        transform.localRotation = _baseRot * Quaternion.Euler(2f, 0f, -1f);
                        leftLegPitch = -10f;
                        rightLegPitch = 10f;
                        leftArmPitch = 10f;
                        leftForeArmPitch = -16f;
                        rightArmPitch = Mathf.Lerp(-22f, -64f, waveBlend);
                        rightArmRaiseZ = Mathf.Lerp(10f, 42f + waveMotion * 12f, waveBlend);
                        rightForeArmPitch = Mathf.Lerp(-26f, -48f, waveBlend);
                        rightForeArmRoll = waveMotion * 14f;
                        torsoLeanX = 3f;
                        headYaw = -8f;
                        headRoll = -2f;
                    }
                    break;

                case PassengerPose.WalkingToCab:
                    // Row 2 Panels 2.1..2.4: Four consecutive master walking cycle poses in clean side view
                    {
                        float strideRad = PassengerLocomotion.Phase(_walkDistance / Mathf.Max(.1f, transform.lossyScale.y), _phase);
                        float cycle = Mathf.Repeat(strideRad / (Mathf.PI * .5f), 4f);
                        WalkCycleStep = Mathf.Clamp(Mathf.FloorToInt(cycle) + 1, 1, 4);
                        float s = Mathf.Sin(strideRad);
                        float bob = Mathf.Abs(Mathf.Cos(strideRad)) * .015f * _walkWeight;

                        SetWalkLegs(strideRad, 20f, out leftLegPitch, out rightLegPitch);
                        leftArmPitch = s * 22f * _walkWeight;
                        rightArmPitch = -s * 22f * _walkWeight;
                        leftForeArmPitch = -22f;
                        rightForeArmPitch = -22f;
                        torsoLeanX = 4.5f;
                        torsoYaw = -s * 5f;

                        transform.localPosition = _basePos + new Vector3(0f, bob, 0f);
                        transform.localRotation = _baseRot;
                    }
                    break;

                case PassengerPose.SittingInCab:
                    // Seated comfortably; the final arm solve rests both hands on the lap.
                    transform.localPosition = _basePos + new Vector3(0f, Mathf.Sin(t * 0.7f) * 0.008f, 0f);
                    transform.localRotation = _baseRot;
                    leftArmPitch = rightArmPitch = -12f;
                    leftForeArmPitch = rightForeArmPitch = -60f;
                    if (CurrentExpression == PassengerExpression.Thoughtful)
                    {
                        headYaw = -6f;
                        headPitch = -2f;
                        headRoll = -6f;
                    }
                    else
                    {
                        headYaw = 4f + Mathf.Sin(t * 0.6f) * 3f;
                        headPitch = 2f;
                        headRoll = -5f;
                    }
                    break;

                case PassengerPose.Satisfied:
                    {
                        float cheer = 1f - Mathf.SmoothStep(0f, 1f, (_poseTimer - .6f) / .85f);
                        transform.localPosition = _basePos + Vector3.up * (Mathf.Abs(Mathf.Sin(t * 2.6f)) * .018f * cheer);
                        transform.localRotation = _baseRot;
                        // Keep the chest-up portrait calm; delight reads through the face and nod.
                        leftArmPitch = rightArmPitch = -12f;
                        leftForeArmPitch = rightForeArmPitch = -60f;
                        headPitch = Mathf.Sin(t * 1.3f) * (2f + cheer * 3f) - 3f;
                        headYaw = Mathf.Sin(t * .65f) * 4f;
                        headRoll = -3f;
                    }
                    break;

                case PassengerPose.DisappointedInCab:
                    // Downcast expression with hands resting together on the lap.
                    transform.localPosition = _basePos + new Vector3(0f, -0.02f, 0f);
                    transform.localRotation = _baseRot;
                    torsoLeanX = 9f;
                    headPitch = 14f;
                    headYaw = -10f;
                    headRoll = 8f;
                    leftArmPitch = rightArmPitch = -12f;
                    leftForeArmPitch = rightForeArmPitch = -60f;
                    break;

                case PassengerPose.SteppingOutOfDoor:
                    // Row 3 Panel 3.2: Stepping out of the open yellow taxi door, left hand on door frame, looking down sadly
                    transform.localPosition = _basePos + new Vector3(0f, Mathf.Sin(t * 1.5f) * 0.02f, 0f);
                    transform.localRotation = _baseRot;
                    torsoLeanX = 14f;
                    rightLegPitch = -26f; // Stepping forward/down out of cab
                    leftLegPitch = 14f;
                    leftArmPitch = -36f;  // Reaching for open door frame
                    leftForeArmPitch = -28f;
                    headPitch = 16f;
                    headYaw = 8f;
                    break;

                case PassengerPose.StandingSadBesideDoor:
                    // Row 3 Panel 3.3: Standing beside open door in 3/4 rear profile, teal backpack visible, head bowed
                    transform.localPosition = _basePos;
                    transform.localRotation = _baseRot;
                    torsoLeanX = 6f;
                    headPitch = 18f;
                    headRoll = 5f;
                    leftArmPitch = 12f;
                    break;

                case PassengerPose.WalkingAway:
                    // One farewell gesture settles into a downcast, balanced walk.
                    {
                        float strideRad = PassengerLocomotion.Phase(_walkDistance / Mathf.Max(.1f, transform.lossyScale.y), _phase);
                        float cycle = Mathf.Repeat(strideRad / (Mathf.PI * .5f), 4f);
                        WalkCycleStep = Mathf.Clamp(Mathf.FloorToInt(cycle) + 1, 1, 4);
                        float s = Mathf.Sin(strideRad);
                        float bob = Mathf.Abs(Mathf.Cos(strideRad)) * .012f * _walkWeight;
                        float farewell = PassengerLocomotion.FarewellWeight(_poseTimer);

                        SetWalkLegs(strideRad, 17f, out leftLegPitch, out rightLegPitch);
                        leftArmPitch = s * 14f * _walkWeight;
                        leftForeArmPitch = -12f;
                        rightArmPitch = Mathf.Lerp(-s * 14f * _walkWeight, -60f, farewell);
                        rightArmRaiseZ = (35f + Mathf.Sin(t * 3.4f) * 7f) * farewell;
                        rightForeArmPitch = Mathf.Lerp(-12f, -48f, farewell);
                        rightForeArmRoll = Mathf.Sin(t * 4f) * 12f * farewell;
                        torsoLeanX = 5f;
                        torsoYaw = Mathf.Lerp(-s * 3f, 12f, farewell);
                        headPitch = Mathf.Lerp(11f, -2f, farewell);
                        headYaw = 32f * farewell;
                        headRoll = -3f;

                        transform.localPosition = _basePos + new Vector3(0f, bob, 0f);
                        transform.localRotation = _baseRot;
                    }
                    break;

                case PassengerPose.DriverDriving:
                    // Taxi Driver (ModelsV2/Taxi_Şöförü) seated in front-left cab seat steering the wheel
                    {
                        float steer = Mathf.Sin(t * 0.65f) * 6f;
                        transform.localPosition = _basePos;
                        transform.localRotation = _baseRot;
                        leftArmPitch = -56f + steer;
                        rightArmPitch = -56f - steer;
                        leftForeArmPitch = -28f;
                        rightForeArmPitch = -28f;
                        headYaw = Mathf.Sin(t * 0.45f) * 8f;
                        headPitch = -2f;
                    }
                    break;
            }

            ApplyCharacterAnimation(
                leftLegPitch, rightLegPitch,
                leftArmPitch, rightArmPitch, rightArmRaiseZ,
                leftForeArmPitch, rightForeArmPitch, rightForeArmRoll,
                headPitch, headYaw, headRoll,
                torsoLeanX, torsoYaw);

            BlendPoseTransition();

            bool holding = _characterEModelProp != null && _characterEModelProp.gameObject.activeSelf && Profile != null &&
                           (Profile.id == PassengerId.Grandfather || Profile.id == PassengerId.Professional);
            _leftGripWorld = ApplyLeftHandGrip(holding);
            if (_rightHandPose != null)
            {
                bool relaxed = Pose == PassengerPose.Waiting || Pose == PassengerPose.WalkingToCab || Pose == PassengerPose.StandingSadBesideDoor
                    || Pose == PassengerPose.WalkingAway && _poseTimer > 1.2f;
                if (relaxed) _rightHandPose.OrientPalm(-transform.right);
                if (IsRearSeatPose) _rightHandPose.OrientPalm(-transform.up);
                _rightHandPose.SetFingerCurl(relaxed || IsRearSeatPose ? .3f : .1f);
            }
            UpdateBoneAttachedProps(t, leftLegPitch, leftArmPitch, torsoLeanX, torsoYaw);
        }

        void BlendPoseTransition()
        {
            if (!_blendPose) return;
            float blend = Mathf.SmoothStep(0f, 1f, _poseTimer / .22f);
            for (int i = 0; i < _poseBones.Length; i++)
                if (_poseBones[i] != null)
                    _poseBones[i].localRotation = Quaternion.Slerp(_transitionRotations[i], _poseBones[i].localRotation, blend);
            if (_boneHips != null)
                _boneHips.localPosition = Vector3.Lerp(_transitionHipsPosition, _boneHips.localPosition, blend);
            if (blend >= 1f) _blendPose = false;
        }

        void UpdateBoneAttachedProps(float t, float leftLegPitch, float leftArmPitch, float torsoLeanX, float torsoYaw)
        {
            Quaternion qTorso = Quaternion.Euler(torsoLeanX, torsoYaw, 0f);
            Transform chestBone = _boneSpine2 != null ? _boneSpine2 : (_boneSpine1 != null ? _boneSpine1 : _boneSpine);

            // 1. Lock teal backpack to upper spine bone so it leans & rotates naturally with the torso
            if (_backpackProp != null && _backpackProp.gameObject.activeSelf)
            {
                if (chestBone != null)
                {
                    Vector3 spineLocal = transform.InverseTransformPoint(chestBone.position);
                    _backpackProp.localPosition = spineLocal + qTorso * new Vector3(0f, -0.04f, -0.18f);
                }
                else
                {
                    _backpackProp.localPosition = new Vector3(0f, 1.04f, -0.20f);
                }
                _backpackProp.localRotation = qTorso;
            }

            // 2. Lock signature 3D EModel prop to _boneLeftHand or _boneSpine2
            if (_characterEModelProp != null && _characterEModelProp.gameObject.activeSelf && Profile != null)
            {
                Transform handL = _boneLeftHand != null ? _boneLeftHand : _boneLeftForeArm;
                switch (Profile.id)
                {
                    case PassengerId.Minstrel:
                        if (chestBone != null)
                        {
                            Vector3 spineLocal = transform.InverseTransformPoint(chestBone.position);
                            _characterEModelProp.localPosition = spineLocal + qTorso * new Vector3(0f, -0.28f, -0.19f);
                        }
                        else
                        {
                            _characterEModelProp.localPosition = _characterPropBasePos;
                        }
                        _characterEModelProp.localRotation = qTorso * _characterPropBaseRot;
                        break;

                    case PassengerId.Grandfather:
                        if (_leftFingerChains != null)
                        {
                            // T-handle cane: the handle (93% up the model) sits in the fist, the tip on the ground.
                            // The cane is resized to the fist's height so it always reaches the pavement.
                            Vector3 grip = transform.InverseTransformPoint(_leftGripWorld);
                            float h = Mathf.Clamp(grip.y / CaneGripHeight, 0.5f, 1.2f);
                            _characterEModelProp.localScale = _propBaseScale * (h / CaneFitHeight);
                            // Crook turned to point behind the fist, so it doesn't cover the hand from the front
                            HangFromGrip(grip, CaneGripHeight * h, reverse: true);
                        }
                        else if (handL != null)
                        {
                            Vector3 handLocal = transform.InverseTransformPoint(handL.position);
                            _characterEModelProp.localPosition = new Vector3(
                                handLocal.x - 0.04f,
                                Mathf.Max(0f, -leftLegPitch * 0.003f),
                                handLocal.z + 0.08f);
                            _characterEModelProp.localRotation = _characterPropBaseRot * Quaternion.Euler(leftLegPitch * 0.4f, 0f, 0f);
                        }
                        else
                        {
                            _characterEModelProp.localPosition = _characterPropBasePos + new Vector3(0f, Mathf.Max(0f, -leftLegPitch * 0.003f), -leftLegPitch * 0.005f);
                            _characterEModelProp.localRotation = _characterPropBaseRot * Quaternion.Euler(leftLegPitch * 0.4f, 0f, 0f);
                        }
                        break;

                    case PassengerId.Chef:
                        if (handL != null)
                        {
                            Vector3 handLocal = transform.InverseTransformPoint(handL.position);
                            _characterEModelProp.localPosition = handLocal + new Vector3(-0.04f, -0.10f, 0.08f);
                        }
                        else
                        {
                            _characterEModelProp.localPosition = _characterPropBasePos + new Vector3(0f, Mathf.Sin(t * 1.2f) * 0.012f, -leftArmPitch * 0.004f);
                        }
                        _characterEModelProp.localRotation = qTorso * _characterPropBaseRot * Quaternion.Euler(leftArmPitch * 0.35f, 0f, 0f);
                        break;

                    case PassengerId.Professional:
                        if (_leftFingerChains != null)
                        {
                            // Briefcase hangs from its top handle (94% up the model), closed in the fist
                            HangFromGrip(transform.InverseTransformPoint(_leftGripWorld), BriefcaseGripHeight * BriefcaseFitHeight);
                        }
                        else
                        {
                            if (handL != null)
                                _characterEModelProp.localPosition = transform.InverseTransformPoint(handL.position) + new Vector3(-0.04f, -0.34f, 0.02f);
                            else
                                _characterEModelProp.localPosition = _characterPropBasePos + new Vector3(0f, Mathf.Sin(t * 1.2f) * 0.012f, -leftArmPitch * 0.004f);
                            _characterEModelProp.localRotation = qTorso * _characterPropBaseRot * Quaternion.Euler(leftArmPitch * 0.45f, 0f, 0f);
                        }
                        break;
                }
            }
        }

        // Handle positions of the held props, as a fraction of the model height (both handles are centred in x/z;
        // the cane's is a crook)
        const float CaneGripHeight = 0.93f, CaneFitHeight = 0.88f;
        const float BriefcaseGripHeight = 0.94f, BriefcaseFitHeight = 0.36f;
        Vector3 _leftGripWorld;

        /// <summary>
        /// Places the held prop upright with its handle (at <paramref name="handleY"/> above the prop's base)
        /// in the fist, the handle running along the fist (thumb-to-pinky), as a carried cane or case hangs.
        /// </summary>
        void HangFromGrip(Vector3 gripLocal, float handleY, bool reverse = false)
        {
            Vector3 across = transform.InverseTransformDirection(_leftIndex1.position - _leftPinky1.position);
            if (reverse) across = -across;
            across.y = 0f;
            if (across.sqrMagnitude < 1e-6f) across = Vector3.forward;
            var rot = Quaternion.LookRotation(Vector3.Cross(across.normalized, Vector3.up), Vector3.up);
            _characterEModelProp.localRotation = rot;
            _characterEModelProp.localPosition = gripLocal - rot * new Vector3(0f, handleY, 0f);
        }

        public void ApplyStaticExpressionDeform(PassengerExpression expr)
        {
            CurrentExpression = expr;
            switch (expr)
            {
                case PassengerExpression.Curious:
                    ApplyCharacterAnimation(0f, 0f, 0f, 0f, 0f, -14f, -14f, 0f, -6f, 7f, -6f, 0f, 0f);
                    break;
                case PassengerExpression.Thoughtful:
                    ApplyCharacterAnimation(0f, 0f, -18f, -54f, 16f, -24f, -78f, -22f, -4f, -8f, 5f, 3f, 0f);
                    break;
                case PassengerExpression.Satisfied:
                    ApplyCharacterAnimation(0f, 0f, -66f, -66f, 22f, -60f, -60f, 0f, -5f, 4f, -4f, -2f, 0f);
                    break;
                case PassengerExpression.Disappointed:
                    ApplyCharacterAnimation(0f, 0f, 0f, -48f, 16f, -14f, -74f, -18f, 15f, -9f, 7f, 6f, 0f);
                    break;
            }
            AnimateFace(0f, true);
        }

        /// <summary>Two-bone arm placement, measured from each rig, keeps seated wrists above the thighs.</summary>
        void RestHandOnLap(Transform upperArm, Transform forearm, Transform hand, float side)
        {
            if (upperArm == null || forearm == null || hand == null) return;
            Vector3 shoulder = upperArm.position;
            float upperLength = Vector3.Distance(shoulder, forearm.position);
            float foreLength = Vector3.Distance(forearm.position, hand.position);
            float reach = upperLength + foreLength;
            if (upperLength < .001f || foreLength < .001f) return;

            // Targets use arm length rather than the source FBX/glTF units. The wrists stay well
            // inside the shoulders, slightly ahead of the hips and above the seated thighs.
            Vector3 target = _boneHips.position + transform.forward * (reach * .35f)
                + transform.up * (reach * .10f) + transform.right * (side * reach * .13f);
            Vector3 direction = target - shoulder;
            float distance = Mathf.Clamp(direction.magnitude, Mathf.Abs(upperLength - foreLength) + .001f, reach * .985f);
            if (direction.sqrMagnitude < .000001f) return;
            direction.Normalize();
            target = shoulder + direction * distance;
            float along = (upperLength * upperLength + distance * distance - foreLength * foreLength) / (2f * distance);
            float bendHeight = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
            Vector3 elbowHint = -transform.up + transform.right * (side * .22f) + transform.forward * .08f;
            Vector3 bendDirection = Vector3.ProjectOnPlane(elbowHint, direction).normalized;
            if (bendDirection.sqrMagnitude < .0001f)
                bendDirection = Vector3.ProjectOnPlane(transform.right * side, direction).normalized;
            Vector3 elbow = shoulder + direction * along + bendDirection * bendHeight;
            upperArm.rotation = Quaternion.FromToRotation(forearm.position - shoulder, elbow - shoulder) * upperArm.rotation;
            forearm.rotation = Quaternion.FromToRotation(hand.position - forearm.position, target - forearm.position) * forearm.rotation;
        }

        void ApplyCharacterAnimation(
            float leftLegPitch, float rightLegPitch,
            float leftArmPitch, float rightArmPitch, float rightArmRaiseZ,
            float leftForeArmPitch, float rightForeArmPitch, float rightForeArmRoll,
            float headPitch, float headYaw, float headRoll,
            float torsoLeanX, float torsoYaw)
        {
            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                _animator.SetInteger(PoseParameter, (int)Pose);
                _animator.SetInteger(ExpressionParameter, (int)CurrentExpression);
            }

            // 1. Drive the 52-bone Rodin / Mixamo Humanoid Skeleton in true Character Root Space!
            if (_hasBoneRig)
            {
                ResetBonesToRest();

                // Spine chain (Spine -> Spine1 -> Spine2)
                int spineCount = (_boneSpine != null ? 1 : 0) + (_boneSpine1 != null ? 1 : 0) + (_boneSpine2 != null ? 1 : 0);
                float spineShare = spineCount > 0 ? (1f / spineCount) : 1f;
                Quaternion qSpineStep = Quaternion.Euler(torsoLeanX * spineShare, torsoYaw * spineShare, 0f);
                if (_boneSpine != null) RotateBoneInCharSpace(_boneSpine, qSpineStep);
                if (_boneSpine1 != null) RotateBoneInCharSpace(_boneSpine1, qSpineStep);
                if (_boneSpine2 != null) RotateBoneInCharSpace(_boneSpine2, qSpineStep);

                // Neck & Head
                if (_boneNeck != null)
                    RotateBoneInCharSpace(_boneNeck, Quaternion.Euler(headPitch * 0.35f, headYaw * 0.35f, headRoll * 0.35f));
                if (_boneHead != null)
                    RotateBoneInCharSpace(_boneHead, Quaternion.Euler(headPitch * 0.65f, headYaw * 0.65f, headRoll * 0.65f));
                _headYawNow = headYaw; _headPitchNow = headPitch;

                // Legs (UpLeg -> Leg -> Foot)
                bool isSeated = (Pose == PassengerPose.SittingInCab ||
                                 Pose == PassengerPose.Satisfied ||
                                 Pose == PassengerPose.DisappointedInCab ||
                                 Pose == PassengerPose.DriverDriving);
                if (isSeated && _boneHips != null)
                {
                    // Seat drop is measured in the model's own space (its root under this actor) and converted into
                    // the hips' parent space: the face-rigged FBX characters have a x100 Armature rotated to Z-up,
                    // where the raw offset sent the body 24 m away.
                    _boneHips.localPosition = _restHipsPos + _boneHips.parent.InverseTransformVector(ModelRoot.TransformVector(new Vector3(0f, -0.38f, -0.08f)));
                }
                float seatHipBend = isSeated ? 80f : 0f;
                float seatKneeBend = isSeated ? -80f : 0f;

                if (_boneLeftUpLeg != null)
                    RotateBoneInCharSpace(_boneLeftUpLeg, Quaternion.Euler(seatHipBend + leftLegPitch, 0f, 0f));
                if (_boneRightUpLeg != null)
                    RotateBoneInCharSpace(_boneRightUpLeg, Quaternion.Euler(seatHipBend + rightLegPitch, 0f, 0f));

                float kneeFlexL = isSeated ? seatKneeBend : _walkKneeL >= 0f ? _walkKneeL : Mathf.Max(0f, leftLegPitch * 0.65f);
                float kneeFlexR = isSeated ? seatKneeBend : _walkKneeR >= 0f ? _walkKneeR : Mathf.Max(0f, rightLegPitch * 0.65f);
                if (_boneLeftLeg != null)
                    RotateBoneInCharSpace(_boneLeftLeg, Quaternion.Euler(kneeFlexL, 0f, 0f));
                if (_boneRightLeg != null)
                    RotateBoneInCharSpace(_boneRightLeg, Quaternion.Euler(kneeFlexR, 0f, 0f));

                if (_boneLeftFoot != null)
                    RotateBoneInCharSpace(_boneLeftFoot, Quaternion.Euler(-(seatHipBend + leftLegPitch + kneeFlexL), 0f, 0f));
                if (_boneRightFoot != null)
                    RotateBoneInCharSpace(_boneRightFoot, Quaternion.Euler(-(seatHipBend + rightLegPitch + kneeFlexR), 0f, 0f));
                if (!isSeated && _walkKneeL >= 0f && _boneHips != null && _boneLeftFoot != null && _boneRightFoot != null)
                {
                    float feet = Mathf.Min(transform.InverseTransformPoint(_boneLeftFoot.position).y,
                        transform.InverseTransformPoint(_boneRightFoot.position).y);
                    float lift = Mathf.Clamp(_restFootHeight - feet, -.045f, .045f);
                    _boneHips.position += transform.up * (lift * transform.lossyScale.y);
                }

                // Left Arm (adduct from T-pose to relaxed side stance first, then apply pose rotation, then elbow bend)
                float leftSpreadZ = 0f;
                if (_boneLeftArm != null)
                {
                    Quaternion qUpperL = Quaternion.Euler(leftArmPitch, 0f, leftSpreadZ) * _qLeftArmRelaxChar;
                    RotateBoneInCharSpace(_boneLeftArm, qUpperL);
                }
                if (_boneLeftForeArm != null)
                {
                    RotateBoneInCharSpace(_boneLeftForeArm, Quaternion.Euler(leftForeArmPitch, 0f, 0f));
                }

                // Right Arm (adduct from T-pose to relaxed side stance first, then apply pose rotation, then elbow bend)
                if (_boneRightArm != null)
                {
                    Quaternion qUpperR = Quaternion.Euler(rightArmPitch, 0f, rightArmRaiseZ) * _qRightArmRelaxChar;
                    RotateBoneInCharSpace(_boneRightArm, qUpperR);
                }
                if (_boneRightForeArm != null)
                {
                    RotateBoneInCharSpace(_boneRightForeArm, Quaternion.Euler(rightForeArmPitch, 0f, rightForeArmRoll));
                }

                if (IsRearSeatPose && _boneHips != null)
                {
                    RestHandOnLap(_boneLeftArm, _boneLeftForeArm, _boneLeftHand, -1f);
                    RestHandOnLap(_boneRightArm, _boneRightForeArm, _boneRightHand, 1f);
                }

                return;
            }

            // 2. Fallback soft-skinned vertex deformation for unrigged static meshes
            if (_workingMesh == null || _origVerts == null) return;

            float hipY = _minY + _heightY * 0.47f;
            float shoulderY = _minY + _heightY * 0.74f;
            float neckY = _minY + _heightY * 0.79f;
            float armThresholdX = _heightY * 0.105f;

            Quaternion qLeftLeg = Quaternion.Euler(leftLegPitch, 0f, 0f);
            Quaternion qRightLeg = Quaternion.Euler(rightLegPitch, 0f, 0f);
            Quaternion qLeftArm = Quaternion.Euler(leftArmPitch, 0f, 0f);
            Quaternion qRightArm = Quaternion.Euler(rightArmPitch, 0f, -rightArmRaiseZ);
            Quaternion qHead = Quaternion.Euler(headPitch, headYaw, headRoll);
            Quaternion qTorso = Quaternion.Euler(torsoLeanX, torsoYaw, 0f);

            Vector3 hipPivotL = new Vector3(_centerX - _heightY * 0.06f, hipY, _centerZ);
            Vector3 hipPivotR = new Vector3(_centerX + _heightY * 0.06f, hipY, _centerZ);
            Vector3 shPivotL = new Vector3(_centerX - armThresholdX, shoulderY, _centerZ);
            Vector3 shPivotR = new Vector3(_centerX + armThresholdX, shoulderY, _centerZ);
            Vector3 neckPivot = new Vector3(_centerX, neckY, _centerZ);
            Vector3 waistPivot = new Vector3(_centerX, hipY, _centerZ);

            int count = _origVerts.Length;
            for (int i = 0; i < count; i++)
            {
                Vector3 v = _origVerts[i];
                float yn = (v.y - _minY) / _heightY;
                float dx = v.x - _centerX;

                if (yn < 0.47f)
                {
                    float w = Mathf.Clamp01((0.47f - yn) / 0.12f);
                    if (dx < 0f)
                        v = Vector3.Lerp(v, hipPivotL + qLeftLeg * (v - hipPivotL), w);
                    else
                        v = Vector3.Lerp(v, hipPivotR + qRightLeg * (v - hipPivotR), w);
                }
                else
                {
                    if (Mathf.Abs(torsoLeanX) > 0.01f || Mathf.Abs(torsoYaw) > 0.01f)
                    {
                        float tw = Mathf.Clamp01((yn - 0.47f) / 0.15f);
                        v = Vector3.Lerp(v, waistPivot + qTorso * (v - waistPivot), tw);
                    }

                    if (yn >= 0.45f && yn <= 0.78f && Mathf.Abs(dx) > armThresholdX)
                    {
                        float aw = Mathf.Clamp01((Mathf.Abs(dx) - armThresholdX) / (_heightY * 0.05f));
                        if (dx < 0f)
                            v = Vector3.Lerp(v, shPivotL + qLeftArm * (v - shPivotL), aw);
                        else
                            v = Vector3.Lerp(v, shPivotR + qRightArm * (v - shPivotR), aw);
                    }

                    if (yn > 0.78f)
                    {
                        float hw = Mathf.Clamp01((yn - 0.78f) / 0.06f);
                        v = Vector3.Lerp(v, neckPivot + qHead * (v - neckPivot), hw);
                    }
                }

                _deformedVerts[i] = v;
            }

            _workingMesh.vertices = _deformedVerts;
        }

        /// <summary>
        /// Applies a static close-up expression pose (Row 4 of 08-animation.png:
        /// Curious, Thoughtful, Satisfied, Disappointed) to a model hierarchy for thumbnail rendering.
        /// </summary>
        public static void ApplyStaticExpressionDeform(GameObject modelRoot, PassengerExpression expr)
        {
            if (modelRoot == null) return;
            var actor = modelRoot.GetComponent<PassengerActor>();
            if (actor == null)
            {
                actor = modelRoot.AddComponent<PassengerActor>();
                actor.SetupSkeletonRig();
                if (!actor._hasBoneRig)
                    actor.SetupMeshDeformer();
            }

            actor.ApplyStaticExpressionDeform(expr);
        }
    }
}
