using UnityEngine;

namespace OddsCab
{
    /// <summary>Eight prewarmed road vehicles. Motion/spacing, wheel rotation and reuse have no frame-loop managed storage.</summary>
    public sealed class AmbientTrafficDirector : MonoBehaviour
    {
        const int Capacity = 8;
        sealed class Vehicle
        {
            public GameObject root;
            public Transform[] wheels;
            public Quaternion[] wheelRest;
            public Vector3[] wheelAxes;
            public float length, speed, cruise, rollAngle, wheelRadius;
            public int direction, model;
        }
        readonly Vehicle[] _vehicles = new Vehicle[Capacity];
        Transform _taxi;
        TaxiController _controller;
        int _district;
        bool _visible = true, _ready;
        public int PoolCount { get; private set; }
        public int ActiveTrafficCount { get; private set; }

        public void Initialize(Transform taxi, int district)
        {
            if (_ready) return;
            _taxi = taxi; _controller = taxi.GetComponent<TaxiController>();
            for (int i = 0; i < Capacity; i++)
            {
                int model = i == 5 ? 2 : i % 3 == 0 ? 0 : i % 3 == 1 ? 3 : 1;
                if (!AmbientTrafficAssets.TryCreate(model, transform, out var root)) continue;
                var transforms = root.GetComponentsInChildren<Transform>(true);
                int count = 0;
                for (int j = 0; j < transforms.Length; j++) if (transforms[j].name.StartsWith("Wheel", System.StringComparison.Ordinal)) count++;
                var vehicle = new Vehicle { root = root, model = model, length = AmbientTrafficAssets.Length(model),
                    direction = (i & 1) == 0 ? 1 : -1, cruise = model == 2 ? 8.5f : 10f + i * .42f,
                    wheelRadius = model == 2 ? .41f : model == 1 ? .34f : .285f,
                    wheels = new Transform[count], wheelRest = new Quaternion[count], wheelAxes = new Vector3[count] };
                int index = 0;
                for (int j = 0; j < transforms.Length; j++)
                {
                    var wheel = transforms[j];
                    if (!wheel.name.StartsWith("Wheel", System.StringComparison.Ordinal)) continue;
                    vehicle.wheels[index] = wheel; vehicle.wheelRest[index] = wheel.localRotation;
                    vehicle.wheelAxes[index] = wheel.parent.InverseTransformDirection(root.transform.right).normalized; index++;
                }
                _vehicles[i] = vehicle; PoolCount++;
            }
            _ready = true; SetDistrict(district);
        }

        public void SetDistrict(int district)
        {
            _district = Mathf.Clamp(district, 0, 7);
            if (_taxi == null) return;
            for (int i = 0; i < _vehicles.Length; i++)
            {
                var vehicle = _vehicles[i]; if (vehicle == null) continue;
                float z = _taxi.position.z + 42f + (i / 2) * 39f + (i & 1) * 18f;
                vehicle.root.transform.SetPositionAndRotation(new Vector3(vehicle.direction * AmbientTrafficPolicy.LaneCenter, 0f, z),
                    vehicle.direction > 0 ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f));
                vehicle.speed = vehicle.cruise;
            }
            UpdateVisibility();
        }

        public void SetStreetVisible(bool visible) { _visible = visible; UpdateVisibility(); }

        void UpdateVisibility()
        {
            bool night = WeatherDirector.Instance != null && WeatherDirector.Instance.CurrentWeather == WeatherPreset.Night;
            int count = _visible ? AmbientTrafficPolicy.Population(_district, night, DeviceTierDetector.CurrentTier == DeviceTier.Low) : 0;
            ActiveTrafficCount = 0;
            for (int i = 0; i < _vehicles.Length; i++)
            {
                var vehicle = _vehicles[i]; if (vehicle == null) continue;
                bool show = i < count && (vehicle.model != 2 || AmbientTrafficPolicy.BusAllowed(_district));
                if (vehicle.root.activeSelf != show) vehicle.root.SetActive(show);
                if (show) ActiveTrafficCount++;
            }
        }

        void Update()
        {
            if (!_ready || _taxi == null) return;
            UpdateVisibility();
            float dt = Time.deltaTime;
            if (dt <= 0f || !_visible) return;
            float taxiZ = _taxi.position.z, taxiSpeed = _controller != null ? _controller.Speed : 0f;
            for (int i = 0; i < _vehicles.Length; i++)
            {
                var vehicle = _vehicles[i];
                if (vehicle == null || !vehicle.root.activeSelf) continue;
                Vector3 position = vehicle.root.transform.position;
                float relative = position.z - taxiZ;
                float desired = vehicle.direction > 0 ? AmbientTrafficPolicy.LeadSpeed(relative, taxiSpeed, vehicle.cruise, vehicle.length) : vehicle.cruise;
                // Do not permit one civilian car to overtake another in the same narrow lane.
                for (int j = 0; j < _vehicles.Length; j++)
                {
                    var other = _vehicles[j];
                    if (other == null || other == vehicle || !other.root.activeSelf || other.direction != vehicle.direction) continue;
                    float gap = (other.root.transform.position.z - position.z) * vehicle.direction;
                    if (gap > 0f && gap < (other.length + vehicle.length) * .5f + 12f) desired = Mathf.Min(desired, other.speed);
                }
                vehicle.speed = Mathf.MoveTowards(vehicle.speed, desired, 8f * dt);
                position.z += vehicle.direction * vehicle.speed * dt;
                if (vehicle.direction > 0)
                {
                    // Queue outside the useful viewing distance. Recycling a faster lead car back into the
                    // player's view would cause a visible pop and could collapse traffic spacing.
                    position.z = Mathf.Min(position.z, taxiZ + AmbientTrafficPolicy.RecycleAhead);
                    for (int j = 0; j < _vehicles.Length; j++)
                    {
                        var other = _vehicles[j];
                        if (other == null || other == vehicle || !other.root.activeSelf || other.direction != vehicle.direction) continue;
                        float otherZ = other.root.transform.position.z;
                        if (otherZ > vehicle.root.transform.position.z)
                            position.z = Mathf.Min(position.z, otherZ - (other.length + vehicle.length) * .5f - 12f);
                    }
                    position.z = AmbientTrafficPolicy.KeepLeadClear(position.z, taxiZ, vehicle.length);
                }
                if (position.z - taxiZ < AmbientTrafficPolicy.RecycleBehind)
                {
                    float furthest = taxiZ + 125f;
                    for (int j = 0; j < _vehicles.Length; j++)
                        if (_vehicles[j] != null && _vehicles[j] != vehicle && _vehicles[j].direction == vehicle.direction)
                            furthest = Mathf.Max(furthest, _vehicles[j].root.transform.position.z);
                    position.z = AmbientTrafficPolicy.SpawnAhead(furthest, taxiZ, vehicle.length);
                }
                vehicle.root.transform.position = position;
                vehicle.rollAngle = Mathf.Repeat(vehicle.rollAngle + vehicle.speed * dt / vehicle.wheelRadius * Mathf.Rad2Deg, 360f);
                for (int w = 0; w < vehicle.wheels.Length; w++)
                    vehicle.wheels[w].localRotation = Quaternion.AngleAxis(vehicle.rollAngle, vehicle.wheelAxes[w]) * vehicle.wheelRest[w];
            }
        }
    }
}
