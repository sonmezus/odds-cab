using System.Collections.Generic;
using UnityEngine;

namespace OddsCab
{
    /// <summary>
    /// Loads imported GLB prefabs from Resources/Models/, Resources/ModelsV2/, and Resources/EModel/ (imported by glTFast),
    /// caches their unscaled bounds metrics so repeated instantiations require zero bounds recalculations,
    /// enables GPU instancing on shared materials, and provides procedural fallbacks if needed.
    /// </summary>
    public static class GlbModelLoader
    {
        struct ModelMetrics
        {
            public float height;
            public Vector3 bottomCenterOffset;
            public bool valid;
        }

        static Shader _litShader;
        static readonly Dictionary<string, GameObject> _eModelCache = new Dictionary<string, GameObject>();
        static readonly Dictionary<GameObject, ModelMetrics> _metricsCache = new Dictionary<GameObject, ModelMetrics>();

        public static Shader LitShader
        {
            get
            {
                if (_litShader == null)
                    _litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                return _litShader;
            }
        }

        public static Material MakeMat(Color color, float smoothness = 0.35f, float metallic = 0f)
        {
            var m = new Material(LitShader);
            m.SetColor("_BaseColor", color);
            m.color = color;
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            m.enableInstancing = true;
            return m;
        }

        /// <summary>True when the loaded taxi model already has front seats and a steering wheel.</summary>
        public static bool TaxiHasOwnSeats { get; private set; }

        public static GameObject InstantiateTaxi()
        {
            var root = new GameObject("TaxiRoot");
            // TaxiRigHQ = the remade taxi (.game-dev/taxi_remake/build_taxi_rig.py): Body, DoorL/R hinged at their
            // front edge, WheelFL/FR/RL/RR centred on their hubs, front along +Z. TaxiRig.cs animates them.
            // TaxiFiat = the Fiat 500 taxi (.game-dev/taxi_remake/build_fiat_taxi.py), same part names and axes,
            // with its own modelled front seats; it takes precedence when present.
            var fiat = Resources.Load<GameObject>("Models/TaxiFiat");
            TaxiHasOwnSeats = fiat != null;
            var hq = fiat ?? Resources.Load<GameObject>("Models/TaxiRigHQ");
            var prefab = hq ?? Resources.Load<GameObject>("Models/TaxiRig") ?? Resources.Load<GameObject>("Models/Taxi");
            if (prefab != null)
            {
                EnableInstancingOnPrefab(prefab);
                var inst = Object.Instantiate(prefab, root.transform);
                inst.name = "TaxiMesh";
                // Meshy Taxi.glb has its front bumper along +X; -90 deg Y aligns front with +Z
                inst.transform.localRotation = hq != null ? Quaternion.identity : Quaternion.Euler(0f, -90f, 0f);
                FitToHeightAndGround(inst, 1.58f);
                return root;
            }

            BuildFallbackTaxi(root.transform);
            return root;
        }

        public static GameObject InstantiatePassenger(PassengerProfile profile)
        {
            var root = new GameObject("Passenger_" + profile.id);
            GameObject prefab = null;
            string detailRole = null;
            if (!string.IsNullOrEmpty(profile.resourceFaceModelName))
            {
                // Enhanced copies preserve the source rigs, UVs and original resource fallback.
                string[] segments = profile.resourceFaceModelName.Split('/');
                if (segments.Length >= 2)
                {
                    detailRole = segments[segments.Length - 2];
                    prefab = Resources.Load<GameObject>("PassengersArt/" + segments[segments.Length - 2]);
                }
                if (prefab == null) prefab = Resources.Load<GameObject>(profile.resourceFaceModelName);
            }
            if (prefab == null && !string.IsNullOrEmpty(profile.resourceModelNameV2))
                prefab = Resources.Load<GameObject>(profile.resourceModelNameV2);
            if (prefab == null && !string.IsNullOrEmpty(profile.resourceModelName))
                prefab = Resources.Load<GameObject>(profile.resourceModelName);

            if (prefab != null)
            {
                EnableInstancingOnPrefab(prefab);
                var inst = Object.Instantiate(prefab, root.transform);
                inst.name = "Mesh";
                ApplyCachedFitToHeightAndGround(prefab, inst, 1.72f);
                AddPassengerDistanceLod(inst, detailRole);
                return root;
            }

            BuildFallbackPassenger(root.transform, profile.accentColor);
            return root;
        }

        public static GameObject InstantiateDriver()
        {
            var root = new GameObject("TaxiDriver_Rodin");
            var prefab = Resources.Load<GameObject>("PassengersArt/TaxiDriver")
                         ?? Resources.Load<GameObject>("ModelKYüzveVücutKemikli/TaxiDriver/base_basic_pbr")
                         ?? Resources.Load<GameObject>("ModelsV2/Taxi_Şöförü_base_basic_pbr")
                         ?? Resources.Load<GameObject>("Models/Driver")
                         ?? Resources.Load<GameObject>("Models/Taxi_Driver")
                         ?? Resources.Load<GameObject>("Models/Passenger_Driver");
            if (prefab != null)
            {
                EnableInstancingOnPrefab(prefab);
                var inst = Object.Instantiate(prefab, root.transform);
                inst.name = "DriverMesh";
                ApplyCachedFitToHeightAndGround(prefab, inst, 1.56f);
                AddPassengerDistanceLod(inst, "TaxiDriver");
                return root;
            }
            return root;
        }

        // Portraits retain the detailed mesh. Distant street actors share the same live
        // skeleton and materials with a lighter mesh, including the jaw and eye joints.
        static void AddPassengerDistanceLod(GameObject instance, string role)
        {
            if (string.IsNullOrEmpty(role)) return;
            var lowPrefab = Resources.Load<GameObject>("PassengersArt/LOD1/" + role);
            if (lowPrefab == null) return;
            var high = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (high.Length == 0) return;
            var boneMap = new Dictionary<string, Transform>();
            foreach (var renderer in high)
                foreach (var bone in renderer.bones)
                {
                    if (bone == null) continue;
                    if (boneMap.TryGetValue(bone.name, out var existing) && existing != bone) return;
                    boneMap[bone.name] = bone;
                }
            var lowRenderers = new List<Renderer>();
            foreach (var source in lowPrefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (source.sharedMaterials.Length != high[0].sharedMaterials.Length) continue;
                var bones = new Transform[source.bones.Length];
                bool valid = true;
                for (int b = 0; b < bones.Length; b++)
                    if (source.bones[b] == null || !boneMap.TryGetValue(source.bones[b].name, out bones[b]))
                    { valid = false; break; }
                if (!valid) continue;
                var child = new GameObject(source.name + "_DistanceLOD");
                child.transform.SetParent(instance.transform, false);
                child.transform.localPosition = lowPrefab.transform.InverseTransformPoint(source.transform.position);
                child.transform.localRotation = Quaternion.Inverse(lowPrefab.transform.rotation) * source.transform.rotation;
                var scale = source.transform.lossyScale;
                var rootScale = lowPrefab.transform.lossyScale;
                child.transform.localScale = new Vector3(scale.x / rootScale.x, scale.y / rootScale.y, scale.z / rootScale.z);
                var skin = child.AddComponent<SkinnedMeshRenderer>();
                skin.sharedMesh = source.sharedMesh;
                skin.sharedMaterials = high[0].sharedMaterials;
                skin.bones = bones;
                skin.rootBone = source.rootBone != null && boneMap.TryGetValue(source.rootBone.name, out var rootBone)
                    ? rootBone : high[0].rootBone;
                skin.localBounds = source.localBounds;
                skin.quality = SkinQuality.Bone4;
                skin.shadowCastingMode = high[0].shadowCastingMode;
                skin.receiveShadows = high[0].receiveShadows;
                lowRenderers.Add(skin);
            }
            if (lowRenderers.Count == 0) return;
            var group = instance.AddComponent<LODGroup>();
            group.fadeMode = LODFadeMode.None;
            group.SetLODs(new[] { new LOD(.30f, high), new LOD(.008f, lowRenderers.ToArray()) });
            group.RecalculateBounds();
        }

        public static GameObject LoadEModelPrefab(string folderName)
        {
            if (string.IsNullOrEmpty(folderName)) return null;
            if (_eModelCache.TryGetValue(folderName, out var cached) && cached != null)
                return cached;

            var prefab = Resources.Load<GameObject>("EModel/" + folderName + "/base_basic_pbr")
                         ?? Resources.Load<GameObject>("EModel/" + folderName);
            if (prefab != null)
            {
                EnableInstancingOnPrefab(prefab);
                _eModelCache[folderName] = prefab;
            }
            return prefab;
        }

        /// <summary>
        /// Instantiates a 3D environment or passenger prop model from Resources/EModel/&lt;folderName&gt;/base_basic_pbr.glb,
        /// normalizes its height to <paramref name="targetHeight"/> using cached bounds metrics (zero per-instance
        /// Renderer.bounds scans), and parents it under <paramref name="parent"/> at <paramref name="localPos"/> and <paramref name="localRot"/>.
        /// </summary>
        public static bool TryInstantiateEModel(
            string folderName,
            Transform parent,
            Vector3 localPos,
            Quaternion localRot,
            float targetHeight,
            out GameObject holder)
        {
            holder = null;
            var prefab = LoadEModelPrefab(folderName);
            if (prefab == null) return false;

            holder = new GameObject("EModel_" + folderName);
            var inst = Object.Instantiate(prefab, holder.transform);
            inst.name = "Mesh";
            ApplyCachedFitToHeightAndGround(prefab, inst, targetHeight);

            if (parent != null)
                holder.transform.SetParent(parent, false);
            holder.transform.localPosition = localPos;
            holder.transform.localRotation = localRot;
            return true;
        }

        static void EnableInstancingOnPrefab(GameObject prefab)
        {
            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var mats = renderers[i].sharedMaterials;
                for (int m = 0; m < mats.Length; m++)
                {
                    if (mats[m] != null)
                        mats[m].enableInstancing = true;
                }
            }
        }

        static void ApplyCachedFitToHeightAndGround(GameObject prefabKey, GameObject inst, float targetHeight)
        {
            if (!_metricsCache.TryGetValue(prefabKey, out var metrics))
            {
                // Measure once at unrotated world origin
                inst.transform.position = Vector3.zero;
                inst.transform.rotation = Quaternion.identity;
                inst.transform.localScale = Vector3.one;

                var renderers = inst.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    Bounds b = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++)
                        b.Encapsulate(renderers[i].bounds);

                    metrics = new ModelMetrics
                    {
                        height = Mathf.Max(0.01f, b.size.y),
                        bottomCenterOffset = new Vector3(b.center.x, b.min.y, b.center.z),
                        valid = true
                    };
                }
                _metricsCache[prefabKey] = metrics;
            }

            if (metrics.valid)
            {
                float scale = targetHeight / metrics.height;
                inst.transform.localScale = Vector3.one * scale;
                inst.transform.localPosition = -metrics.bottomCenterOffset * scale;
                inst.transform.localRotation = Quaternion.identity;
            }
        }

        static void FitToHeightAndGround(GameObject go, float targetHeight)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                b.Encapsulate(renderers[i].bounds);

            float h = Mathf.Max(0.01f, b.size.y);
            float scale = targetHeight / h;
            go.transform.localScale = Vector3.one * scale;

            b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                b.Encapsulate(renderers[i].bounds);

            Vector3 offset = go.transform.position - new Vector3(b.center.x, b.min.y, b.center.z);
            go.transform.position += offset;
        }

        static void BuildFallbackTaxi(Transform root)
        {
            var yellow = MakeMat(new Color(0.98f, 0.76f, 0.16f), 0.65f);
            var cabinMat = MakeMat(new Color(0.96f, 0.72f, 0.12f), 0.6f);
            var dark = MakeMat(new Color(0.14f, 0.16f, 0.20f), 0.2f);
            var signMat = MakeMat(new Color(1f, 0.85f, 0.2f), 0.8f);

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.transform.SetParent(root, false);
            body.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            body.transform.localScale = new Vector3(1.85f, 0.65f, 3.8f);
            body.GetComponent<Renderer>().sharedMaterial = yellow;

            var cabin = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cabin.transform.SetParent(root, false);
            cabin.transform.localPosition = new Vector3(0f, 1.12f, -0.15f);
            cabin.transform.localScale = new Vector3(1.55f, 0.58f, 2.0f);
            cabin.GetComponent<Renderer>().sharedMaterial = cabinMat;

            var sign = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sign.transform.SetParent(root, false);
            sign.transform.localPosition = new Vector3(0f, 1.48f, 0.1f);
            sign.transform.localScale = new Vector3(0.55f, 0.16f, 0.22f);
            sign.GetComponent<Renderer>().sharedMaterial = signMat;

            Vector3[] wheelPos =
            {
                new Vector3(-0.92f, 0.32f, 1.15f),
                new Vector3(0.92f, 0.32f, 1.15f),
                new Vector3(-0.92f, 0.32f, -1.15f),
                new Vector3(0.92f, 0.32f, -1.15f),
            };
            foreach (var wp in wheelPos)
            {
                var w = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                w.transform.SetParent(root, false);
                w.transform.localPosition = wp;
                w.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                w.transform.localScale = new Vector3(0.62f, 0.16f, 0.62f);
                w.GetComponent<Renderer>().sharedMaterial = dark;
            }
        }

        static void BuildFallbackPassenger(Transform root, Color jacket)
        {
            var cloth = MakeMat(jacket, 0.3f);
            var skin = MakeMat(new Color(0.95f, 0.78f, 0.66f), 0.35f);

            var torso = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            torso.transform.SetParent(root, false);
            torso.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            torso.transform.localScale = new Vector3(0.55f, 0.65f, 0.45f);
            torso.GetComponent<Renderer>().sharedMaterial = cloth;

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.transform.SetParent(root, false);
            head.transform.localPosition = new Vector3(0f, 1.48f, 0f);
            head.transform.localScale = Vector3.one * 0.42f;
            head.GetComponent<Renderer>().sharedMaterial = skin;
        }
    }
}
