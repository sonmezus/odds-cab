using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OddsCab
{
    /// <summary>
    /// Procedural VFX system matching 06-vfx.png:
    /// - Stop Arrival: Concentric emerald-green glowing receiver rings on the road surface
    /// - Deceleration: Brake sparks/glow accompanying the audible approach warning
    /// - Passenger Pickup, Achievement/Success, Rewind, and Error bursts.
    /// </summary>
    public class VfxDirector : MonoBehaviour
    {
        public static VfxDirector Instance { get; private set; }

        GameObject _stopRingsRoot;
        readonly Transform[] _rings = new Transform[3];
        readonly Material[] _ringMaterials = new Material[3];
        Material _emeraldMat;
        Mesh _ringMesh;
        ParticleSystem _receiverMotes;

        // Post-Answer Acceleration Celebration VFX (media_1792765036993_fce7d4e5.jpg):
        // - Golden road speed trails beside wheels
        // - 3D spinning gold coins & golden sparkles around the taxi
        // - Glowing golden road chevron arrows (>>>) leading to a giant glowing golden 3D Location Pin ahead!
        GameObject _accelCelebrationRoot;
        readonly List<Transform> _goldenRoadTrails = new List<Transform>();
        ParticleSystem _coinFountain;
        readonly List<Transform> _roadChevrons = new List<Transform>();
        Transform _nextStopPinBeacon;
        bool _accelCelebrationActive;
        Material _goldCoinMat;
        Material _goldGlowMat;

        // Post-Answer Wrong/Rewind VFX (media_1792765107419_582e5035.jpg):
        // - 3 Glowing Cyan/Turquoise Rewind Triangles (◀◀◀) floating above the yellow taxi's TAKSİ roof sign
        // - 2 Glowing Cyan/Turquoise S-Curve Light Ribbon Trails snaking along the cobblestone street under the taxi's wheels!
        GameObject _wrongRewindRoot;
        readonly List<Transform> _rewindTriangles = new List<Transform>();
        readonly List<Transform> _cyanRewindNodes = new List<Transform>();
        bool _wrongRewindActive;
        Material _cyanGlowMat;

        SpeedStreaks _speedStreaks;
        AmbientFx _ambient;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
        }

        void Awake()
        {
            Instance = this;
            BuildStopRings();
            BuildAccelerationRewardCelebration();
            BuildWrongAnswerRewindVfx();
            _speedStreaks = gameObject.AddComponent<SpeedStreaks>();
            _ambient = gameObject.AddComponent<AmbientFx>();
            for (int i = 0; i < _bursts.Length; i++) _bursts[i] = ParticleFx.CreateBurst(transform);
        }

        public void PrepareDrivingEffects(Transform taxi)
        {
            if (_speedStreaks != null) _speedStreaks.Initialize(taxi);
        }

        void BuildStopRings()
        {
            _stopRingsRoot = new GameObject("StopReceiverRings");
            _stopRingsRoot.transform.SetParent(transform, false);

            var shader = Resources.Load<Shader>("Shaders/ReferenceRing") ?? Shader.Find("OddsCab/Reference Ring");
            _emeraldMat = new Material(shader) { name = "ReceiverGlow_Runtime" };
            _emeraldMat.color = new Color(0.12f, 1.3f, 0.95f, 0.5f);
            _ringMesh = BuildAnnulus();

            for (int i = 0; i < 3; i++)
            {
                var ring = new GameObject("ReceiverRing", typeof(MeshFilter), typeof(MeshRenderer));
                ring.name = "EmeraldRing_" + i;
                ring.transform.SetParent(_stopRingsRoot.transform, false);
                ring.GetComponent<MeshFilter>().sharedMesh = _ringMesh;
                var renderer = ring.GetComponent<MeshRenderer>();
                _ringMaterials[i] = new Material(_emeraldMat);
                renderer.sharedMaterial = _ringMaterials[i];
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                _rings[i] = ring.transform;
            }

            _receiverMotes = ParticleFx.CreateReceiverMotes(_stopRingsRoot.transform);

            _stopRingsRoot.SetActive(false);
        }

        static Mesh BuildAnnulus()
        {
            const int segments = 64;
            var vertices = new Vector3[(segments + 1) * 2];
            var uv = new Vector2[vertices.Length];
            var indices = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices[i * 2] = radial * 0.90f;
                vertices[i * 2 + 1] = radial;
                uv[i * 2] = new Vector2((float)i / segments, 0f);
                uv[i * 2 + 1] = new Vector2((float)i / segments, 1f);
                if (i == segments) continue;
                int v = i * 2, t = i * 6;
                indices[t] = v; indices[t + 1] = v + 2; indices[t + 2] = v + 1;
                indices[t + 3] = v + 1; indices[t + 4] = v + 2; indices[t + 5] = v + 3;
            }
            return new Mesh { name = "ReceiverAnnulus_Runtime", vertices = vertices, uv = uv, triangles = indices,
                bounds = new Bounds(Vector3.zero, new Vector3(2f, 0.05f, 2f)) };
        }

        void BuildAccelerationRewardCelebration()
        {
            _accelCelebrationRoot = new GameObject("AccelerationRewardCelebration");
            _accelCelebrationRoot.transform.SetParent(transform, false);

            _goldCoinMat = GlbModelLoader.MakeMat(new Color(1.0f, 0.80f, 0.16f), 0.88f, 0.65f);
            if (_goldCoinMat.HasProperty("_EmissionColor"))
            {
                _goldCoinMat.EnableKeyword("_EMISSION");
                _goldCoinMat.SetColor("_EmissionColor", new Color(0.85f, 0.58f, 0.08f) * 1.15f);
            }

            _goldGlowMat = GlbModelLoader.MakeMat(new Color(1.0f, 0.88f, 0.28f), 0.92f);
            if (_goldGlowMat.HasProperty("_EmissionColor"))
            {
                _goldGlowMat.EnableKeyword("_EMISSION");
                _goldGlowMat.SetColor("_EmissionColor", new Color(1.0f, 0.78f, 0.18f) * 2.1f);
            }
            var darkCenterMat = GlbModelLoader.MakeMat(new Color(0.72f, 0.46f, 0.06f), 0.55f);

            // 1. Golden road speed light trails streaking along the asphalt on both sides of the taxi
            for (int i = 0; i < 12; i++)
            {
                var trail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                trail.name = "GoldenRoadTrail_" + i;
                trail.transform.SetParent(_accelCelebrationRoot.transform, false);
                Destroy(trail.GetComponent<Collider>());
                trail.GetComponent<Renderer>().sharedMaterial = _goldGlowMat;

                float side = (i % 2 == 0) ? -1f : 1f;
                float x = side * (0.85f + (i % 3) * 0.42f);
                float z = -3.6f + (i * 0.65f);
                trail.transform.localPosition = new Vector3(x, 0.04f, z);
                trail.transform.localScale = new Vector3(0.06f, 0.02f, 2.4f);
                _goldenRoadTrails.Add(trail.transform);
            }

            // 2-3. Spinning gold coins fountaining up from the taxi + golden glitter trail (Particle Systems)
            _coinFountain = ParticleFx.CreateCoinFountain(_accelCelebrationRoot.transform);

            // 4. Glowing Golden Chevron Speed Arrows (>>>) along the road ahead of the taxi
            for (int c = 0; c < 6; c++)
            {
                var chev = new GameObject("RoadChevron_" + c);
                chev.transform.SetParent(_accelCelebrationRoot.transform, false);
                chev.transform.localPosition = new Vector3(0f, 0.03f, 4.5f + c * 3.2f);

                var wingL = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wingL.transform.SetParent(chev.transform, false);
                wingL.transform.localPosition = new Vector3(-0.42f, 0f, 0f);
                wingL.transform.localRotation = Quaternion.Euler(0f, 38f, 0f);
                wingL.transform.localScale = new Vector3(0.22f, 0.02f, 1.15f);
                Destroy(wingL.GetComponent<Collider>());
                wingL.GetComponent<Renderer>().sharedMaterial = _goldGlowMat;

                var wingR = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wingR.transform.SetParent(chev.transform, false);
                wingR.transform.localPosition = new Vector3(0.42f, 0f, 0f);
                wingR.transform.localRotation = Quaternion.Euler(0f, -38f, 0f);
                wingR.transform.localScale = new Vector3(0.22f, 0.02f, 1.15f);
                Destroy(wingR.GetComponent<Collider>());
                wingR.GetComponent<Renderer>().sharedMaterial = _goldGlowMat;

                _roadChevrons.Add(chev.transform);
            }

            // 5. Giant Glowing Golden 3D Map-Pin Beacon ahead on the road at the end of the chevron path
            var pinRoot = new GameObject("NextStopGoldenPinBeacon");
            pinRoot.transform.SetParent(_accelCelebrationRoot.transform, false);
            pinRoot.transform.localPosition = new Vector3(0f, 1.85f, 25f);
            _nextStopPinBeacon = pinRoot.transform;

            var pinHead = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pinHead.transform.SetParent(pinRoot.transform, false);
            pinHead.transform.localPosition = new Vector3(0f, 0.65f, 0f);
            pinHead.transform.localScale = new Vector3(1.55f, 1.55f, 0.55f);
            Destroy(pinHead.GetComponent<Collider>());
            pinHead.GetComponent<Renderer>().sharedMaterial = _goldGlowMat;

            var pinHole = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pinHole.transform.SetParent(pinRoot.transform, false);
            pinHole.transform.localPosition = new Vector3(0f, 0.65f, 0f);
            pinHole.transform.localScale = new Vector3(0.62f, 0.62f, 0.62f);
            Destroy(pinHole.GetComponent<Collider>());
            pinHole.GetComponent<Renderer>().sharedMaterial = darkCenterMat;

            var pinTip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pinTip.transform.SetParent(pinRoot.transform, false);
            pinTip.transform.localPosition = new Vector3(0f, -0.05f, 0f);
            pinTip.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            pinTip.transform.localScale = new Vector3(0.95f, 0.95f, 0.42f);
            Destroy(pinTip.GetComponent<Collider>());
            pinTip.GetComponent<Renderer>().sharedMaterial = _goldGlowMat;

            _accelCelebrationRoot.SetActive(false);
        }

        public void SetAccelerationCelebrationActive(bool active)
        {
            _accelCelebrationActive = active;
            if (_accelCelebrationRoot != null && _accelCelebrationRoot.activeSelf != active)
                _accelCelebrationRoot.SetActive(active);
            if (_coinFountain != null)
            {
                if (active) _coinFountain.Play(true);
                else _coinFountain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        void BuildWrongAnswerRewindVfx()
        {
            _wrongRewindRoot = new GameObject("WrongAnswerRewindVfx");
            _wrongRewindRoot.transform.SetParent(transform, false);

            _cyanGlowMat = GlbModelLoader.MakeMat(new Color(0.22f, 0.96f, 0.92f), 0.92f);
            if (_cyanGlowMat.HasProperty("_EmissionColor"))
            {
                _cyanGlowMat.EnableKeyword("_EMISSION");
                _cyanGlowMat.SetColor("_EmissionColor", new Color(0.16f, 0.88f, 0.85f) * 2.2f);
            }

            // 1. 3 Glowing Cyan Rewind Triangles (◀ ◀ ◀) floating above the TAKSİ roof sign (media_1792765107419_582e5035.jpg)
            for (int i = 0; i < 3; i++)
            {
                var tri = new GameObject("CyanRewindTriangle_" + i);
                tri.transform.SetParent(_wrongRewindRoot.transform, false);
                tri.transform.localPosition = new Vector3(-0.52f + i * 0.48f, 1.98f, 0.05f);

                var topBar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                topBar.transform.SetParent(tri.transform, false);
                topBar.transform.localPosition = new Vector3(0f, 0.13f, 0f);
                topBar.transform.localRotation = Quaternion.Euler(0f, 0f, 34f);
                topBar.transform.localScale = new Vector3(0.46f, 0.085f, 0.08f);
                Destroy(topBar.GetComponent<Collider>());
                topBar.GetComponent<Renderer>().sharedMaterial = _cyanGlowMat;

                var botBar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                botBar.transform.SetParent(tri.transform, false);
                botBar.transform.localPosition = new Vector3(0f, -0.13f, 0f);
                botBar.transform.localRotation = Quaternion.Euler(0f, 0f, -34f);
                botBar.transform.localScale = new Vector3(0.46f, 0.085f, 0.08f);
                Destroy(botBar.GetComponent<Collider>());
                botBar.GetComponent<Renderer>().sharedMaterial = _cyanGlowMat;

                var rightEdge = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rightEdge.transform.SetParent(tri.transform, false);
                rightEdge.transform.localPosition = new Vector3(0.18f, 0f, 0f);
                rightEdge.transform.localScale = new Vector3(0.08f, 0.48f, 0.08f);
                Destroy(rightEdge.GetComponent<Collider>());
                rightEdge.GetComponent<Renderer>().sharedMaterial = _cyanGlowMat;

                _rewindTriangles.Add(tri.transform);
            }

            // 2. 2 Glowing Cyan S-Curve Light Ribbon Trails snaking along the cobblestone street under the taxi's wheels
            float[] ribbonOffsets = { -0.32f, 0.32f };
            for (int r = 0; r < ribbonOffsets.Length; r++)
            {
                float rx = ribbonOffsets[r];
                for (int s = -6; s <= 18; s++)
                {
                    float z = s * 0.95f;
                    float curveX = rx + Mathf.Sin(z * 0.18f + 0.6f) * 0.85f - (z > 2f ? (z - 2f) * 0.08f : 0f);
                    float nextZ = (s + 1) * 0.95f;
                    float nextX = rx + Mathf.Sin(nextZ * 0.18f + 0.6f) * 0.85f - (nextZ > 2f ? (nextZ - 2f) * 0.08f : 0f);

                    Vector3 p0 = new Vector3(curveX, 0.04f, z);
                    Vector3 p1 = new Vector3(nextX, 0.04f, nextZ);
                    Vector3 mid = (p0 + p1) * 0.5f;
                    Vector3 dir = (p1 - p0);

                    var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    seg.name = $"CyanRibbon_{r}_{s}";
                    seg.transform.SetParent(_wrongRewindRoot.transform, false);
                    seg.transform.localPosition = mid;
                    if (dir.sqrMagnitude > 0.0001f)
                        seg.transform.localRotation = Quaternion.LookRotation(dir.normalized);
                    seg.transform.localScale = new Vector3(0.085f, 0.024f, dir.magnitude * 1.06f);
                    Destroy(seg.GetComponent<Collider>());
                    seg.GetComponent<Renderer>().sharedMaterial = _cyanGlowMat;
                    _cyanRewindNodes.Add(seg.transform);
                }
            }

            _wrongRewindRoot.SetActive(false);
        }

        public void SetWrongAnswerRewindActive(bool active)
        {
            _wrongRewindActive = active;
            if (_wrongRewindRoot != null && _wrongRewindRoot.activeSelf != active)
                _wrongRewindRoot.SetActive(active);
        }

        public void ShowStopReceiverRings(Vector3 stopRoadPos)
        {
            if (_stopRingsRoot == null) return;
            _stopRingsRoot.transform.position = new Vector3(stopRoadPos.x, 0.015f, stopRoadPos.z);
            _stopRingsRoot.SetActive(true);
            if (_receiverMotes != null) _receiverMotes.Play();
        }

        public void HideStopReceiverRings()
        {
            if (_stopRingsRoot != null)
                _stopRingsRoot.SetActive(false);
            if (_receiverMotes != null) _receiverMotes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        public void UpdateDrivingVfx(Transform taxi, int kmh, bool exteriorVisible)
        {
            if (_speedStreaks != null) _speedStreaks.Tick(taxi, kmh, exteriorVisible, _accelCelebrationActive);
            if (_ambient != null) _ambient.Tick(taxi, kmh / 3.6f, exteriorVisible);

            // Pulse concentric emerald receiver rings on road surface
            if (_stopRingsRoot != null && _stopRingsRoot.activeSelf)
            {
                for (int i = 0; i < 3; i++)
                {
                    float phase = Mathf.Repeat(Time.time * 1.15f + i * 0.33f, 1f);
                    float radius = Mathf.Lerp(0.85f, 1.65f, phase);
                    float y = 0.018f + i * 0.002f;
                    _rings[i].localPosition = new Vector3(0f, y, 0f);
                    _rings[i].localScale = new Vector3(radius, 1f, radius);
                    Color glow = new Color(0.12f, 1.3f, 0.95f, (1f - phase) * 0.58f);
                    _ringMaterials[i].color = glow;
                }
            }

            // Post-Answer Acceleration Celebration (Golden Road Trails, 3D Spinning Coins & Golden Chevrons)
            if (_accelCelebrationRoot != null && taxi != null)
            {
                bool showAccel = _accelCelebrationActive && exteriorVisible;
                if (_accelCelebrationRoot.activeSelf != showAccel)
                    _accelCelebrationRoot.SetActive(showAccel);

                if (showAccel)
                {
                    _accelCelebrationRoot.transform.position = taxi.position;
                    float t = Time.time;

                    for (int i = 0; i < _goldenRoadTrails.Count; i++)
                    {
                        var tr = _goldenRoadTrails[i];
                        Vector3 lp = tr.localPosition;
                        lp.z -= 24f * Time.deltaTime;
                        if (lp.z < -4.2f) lp.z = 3.6f;
                        tr.localPosition = lp;
                    }

                    if (_nextStopPinBeacon != null)
                    {
                        _nextStopPinBeacon.localPosition = new Vector3(0f, 1.85f + Mathf.Sin(t * 3.5f) * 0.18f, 25f);
                    }
                }
            }

            // Post-Answer Wrong/Rewind VFX (3 Cyan Rewind Triangles ◀◀◀ above TAKSİ sign + Cyan S-Curve Street Ribbons)
            if (_wrongRewindRoot != null && taxi != null)
            {
                bool showRewind = _wrongRewindActive && exteriorVisible;
                if (_wrongRewindRoot.activeSelf != showRewind)
                    _wrongRewindRoot.SetActive(showRewind);

                if (showRewind)
                {
                    _wrongRewindRoot.transform.position = taxi.position;
                    float t = Time.time;
                    for (int i = 0; i < _rewindTriangles.Count; i++)
                    {
                        var tri = _rewindTriangles[i];
                        float pulse = 1.0f + Mathf.Sin(t * 5.2f + (2 - i) * 0.9f) * 0.14f;
                        tri.localScale = Vector3.one * pulse;
                        tri.localPosition = new Vector3(-0.52f + i * 0.48f, 1.98f + Mathf.Sin(t * 3.2f + i * 0.6f) * 0.05f, 0.05f);
                    }
                }
            }

        }

        public void PlayBrakeDecel(Vector3 taxiPos)
        {
            Burst(taxiPos + new Vector3(0f, 0.25f, -1.5f), new Color(1.0f, 0.55f, 0.18f), 10, 0.7f);
        }

        public void PlayPickup(Vector3 worldPos)
        {
            Burst(worldPos + Vector3.up * 1.1f, new Color(1f, 0.85f, 0.25f), 18, 1f);
        }

        public void PlaySuccess(Vector3 worldPos)
        {
            Burst(worldPos + Vector3.up * 1.3f, new Color(1f, 0.82f, 0.28f), 28, 1.2f);
            Burst(worldPos + Vector3.up * 1.3f, new Color(0.28f, 0.92f, 0.55f), 14, 1.2f);
        }

        public void PlayError(Vector3 worldPos)
        {
            Burst(worldPos + Vector3.up * 1.2f, new Color(0.95f, 0.28f, 0.28f), 14, 0.9f);
        }

        public void PlayRewind(Vector3 worldPos)
        {
            Burst(worldPos + Vector3.up * 1.2f, new Color(0.35f, 0.82f, 0.98f), 20, 1f);
        }

        // A few pooled burst systems, used round-robin so overlapping bursts don't cut each other off
        readonly ParticleSystem[] _bursts = new ParticleSystem[3];
        int _nextBurst;

        void Burst(Vector3 pos, Color color, int count, float scale)
        {
            int i = _nextBurst++ % _bursts.Length;
            if (_bursts[i] == null) return;
            ParticleFx.Fire(_bursts[i], pos, color, count, scale);
        }

        void OnDestroy()
        {
            if (_emeraldMat != null) Destroy(_emeraldMat);
            if (_ringMesh != null) Destroy(_ringMesh);
            for (int i = 0; i < _ringMaterials.Length; i++)
                if (_ringMaterials[i] != null) Destroy(_ringMaterials[i]);
        }
    }
}
