using System.Collections.Generic;
using UnityEngine;

namespace OddsCab
{
    /// <summary>
    /// Moving parts of the remade taxi (Models/TaxiRigHQ: Body, DoorL, DoorR, WheelFL/FR/RL/RR, pivots at the door
    /// hinges and wheel hubs): the curb-side door swings open on its hinge and the four wheels roll with the taxi's
    /// speed. Works out which door faces the curb and which way it opens from the model itself, so it does not
    /// depend on the importer's axis convention.
    /// </summary>
    public class TaxiRig : MonoBehaviour
    {
        const float DoorOpenAngle = 55f;

        Transform _taxiRoot;
        Transform _curbDoor;
        Quaternion _curbDoorClosed;
        Vector3 _curbDoorAxis;
        float _curbDoorSign = 1f;
        float _doorAngle;
        readonly List<Transform> _wheels = new List<Transform>();
        readonly List<Quaternion> _wheelRest = new List<Quaternion>();
        readonly List<Vector3> _wheelAxes = new List<Vector3>();
        readonly List<Vector3> _wheelUpAxes = new List<Vector3>();
        readonly List<float> _wheelRadii = new List<float>();
        readonly List<float> _wheelAngles = new List<float>();
        readonly List<bool> _frontWheels = new List<bool>();
        readonly List<Vector3> _wheelRestPositions = new List<Vector3>(4);
        readonly List<int> _wheelContactIndices = new List<int>(4);
        readonly List<Vector3> _wheelTravelAxes = new List<Vector3>(4);

        public bool HasDoor => _curbDoor != null;

        public static TaxiRig TryCreate(Transform taxiRoot, Transform visual)
        {
            Transform doorL = FindDeep(visual, "DoorL"), doorR = FindDeep(visual, "DoorR");
            if (doorL == null || doorR == null) return null;
            var rig = taxiRoot.gameObject.AddComponent<TaxiRig>();
            rig._taxiRoot = taxiRoot;
            // The curb (sidewalk) is on the taxi's GameConfig.CurbSide.
            float lx = taxiRoot.InverseTransformPoint(Center(doorL)).x;
            float rx = taxiRoot.InverseTransformPoint(Center(doorR)).x;
            rig.BindDoor((lx < rx) == (GameConfig.CurbSide < 0f) ? doorL : doorR);
            foreach (var n in new[] { "WheelFL", "WheelFR", "WheelRL", "WheelRR" })
            {
                var w = FindDeep(visual, n);
                if (w == null) continue;
                rig._wheels.Add(w);
                rig._wheelRest.Add(w.localRotation);
                rig._wheelAxes.Add(w.parent.InverseTransformDirection(taxiRoot.right).normalized);
                rig._wheelUpAxes.Add(w.parent.InverseTransformDirection(taxiRoot.up).normalized);
                rig._frontWheels.Add(n == "WheelFL" || n == "WheelFR");
                rig._wheelRestPositions.Add(w.localPosition);
                rig._wheelTravelAxes.Add(w.parent.InverseTransformVector(taxiRoot.up));
                rig._wheelContactIndices.Add(n == "WheelFL" ? 0 : n == "WheelFR" ? 1 : n == "WheelRL" ? 2 : 3);
                var r = w.GetComponentInChildren<Renderer>();
                rig._wheelRadii.Add(r != null ? Mathf.Max(0.05f, r.bounds.extents.y) : 0.3f);
                rig._wheelAngles.Add(0f);
            }
            MakeTwoSided(visual);
            return rig;
        }

        void BindDoor(Transform door)
        {
            _curbDoor = door;
            _curbDoorClosed = door.localRotation;
            _curbDoorAxis = door.parent.InverseTransformDirection(_taxiRoot.up).normalized;
            // Pick the swing direction that moves the door's free edge away from the car.
            float closedOut = Mathf.Abs(_taxiRoot.InverseTransformPoint(Center(door)).x);
            door.localRotation = Quaternion.AngleAxis(40f, _curbDoorAxis) * _curbDoorClosed;
            float plus = Mathf.Abs(_taxiRoot.InverseTransformPoint(Center(door)).x);
            door.localRotation = _curbDoorClosed;
            _curbDoorSign = plus > closedOut ? 1f : -1f;
        }

        /// <summary>With a door open you look into the cabin and at the door's inner face, so the painted shell
        /// must render from both sides.</summary>
        static void MakeTwoSided(Transform visual)
        {
            foreach (var r in visual.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.materials;   // instances: never touch the imported asset
                foreach (var m in mats)
                    if (m != null && m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
                r.materials = mats;
            }
        }

        public void Tick(bool doorOpen, float speed, float steeringAngle, float dt)
        {
            if (_curbDoor != null)
            {
                _doorAngle = Mathf.MoveTowards(_doorAngle, doorOpen ? DoorOpenAngle : 0f, 240f * dt);
                // Ease so the door slows as it reaches the stop, like a real check strap
                float shown = DoorOpenAngle * Mathf.SmoothStep(0f, 1f, _doorAngle / DoorOpenAngle);
                _curbDoor.localRotation = Quaternion.AngleAxis(_curbDoorSign * shown, _curbDoorAxis) * _curbDoorClosed;
            }
            if (_wheels.Count > 0)
            {
                for (int i = 0; i < _wheels.Count; i++)
                {
                    _wheelAngles[i] = Mathf.Repeat(_wheelAngles[i] + speed * dt / _wheelRadii[i] * Mathf.Rad2Deg, 360f);
                    _wheels[i].localRotation = Quaternion.AngleAxis(_frontWheels[i] ? steeringAngle : 0f, _wheelUpAxes[i])
                        * Quaternion.AngleAxis(_wheelAngles[i], _wheelAxes[i]) * _wheelRest[i];
                }
            }
        }

        public void SetSuspension(float frontLeft, float frontRight, float rearLeft, float rearRight)
        {
            for (int i = 0; i < _wheels.Count; i++)
            {
                int index = _wheelContactIndices[i];
                float travel = index == 0 ? frontLeft : index == 1 ? frontRight : index == 2 ? rearLeft : rearRight;
                _wheels[i].localPosition = _wheelRestPositions[i] + _wheelTravelAxes[i] * travel;
            }
        }

        static Vector3 Center(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return t.position;
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b.center;
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var f = FindDeep(root.GetChild(i), name);
                if (f != null) return f;
            }
            return null;
        }
    }
}
