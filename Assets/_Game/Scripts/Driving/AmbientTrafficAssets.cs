using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OddsCab
{
    /// <summary>Shared user-supplied vehicle deliveries and explicit external texture bindings.</summary>
    public static class AmbientTrafficAssets
    {
        [Serializable] sealed class Binding
        {
            public string name, baseMap, normalMap;
            public float roughness, metallic, r, g, b;
        }
        [Serializable] sealed class Manifest { public Binding[] entries; }
        static readonly string[] Names = { "Punto", "Zuk", "Bus", "Compact" };
        static readonly float[] Lengths = { 3.76f, 4.35f, 10f, 3.6f };
        static readonly Dictionary<string, GameObject> Prefabs = new Dictionary<string, GameObject>(8);
        static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>(40);
        static Dictionary<string, Binding> _bindings;

        public static float Length(int model) => Lengths[Mathf.Clamp(model, 0, Names.Length - 1)];
        public static string ModelName(int model) => Names[Mathf.Clamp(model, 0, Names.Length - 1)];

        static GameObject Load(string name)
        {
            if (Prefabs.TryGetValue(name, out var prefab)) return prefab;
            prefab = Resources.Load<GameObject>("WorldDetail/Traffic/" + name);
            Prefabs.Add(name, prefab); return prefab;
        }

        public static bool TryCreate(int model, Transform parent, out GameObject instance)
        {
            string name = ModelName(model);
            instance = Create(name, parent);
            if (instance == null) return false;
            var detailed = instance.GetComponentsInChildren<Renderer>(true);
            var low = Create(name + "_LOD1", instance.transform);
            if (model == 3)
            {
                // Measured in the provided scan: rear tyre bottoms are at zero, front tyre bottoms
                // are 44 mm above the datum. Seat both LODs on the road before caching wheel motion.
                foreach (var part in instance.GetComponentsInChildren<Transform>(true))
                    if (part.name.StartsWith("WheelF", StringComparison.Ordinal))
                        part.localPosition -= part.parent.InverseTransformVector(instance.transform.up) * .044f;
            }
            if (low != null)
            {
                var group = instance.AddComponent<LODGroup>();
                group.fadeMode = LODFadeMode.None;
                group.SetLODs(new[] { new LOD(.19f, detailed), new LOD(.009f, low.GetComponentsInChildren<Renderer>(true)) });
                group.RecalculateBounds();
            }
            CurveBender.Convert(instance.transform);
            return true;
        }

        static GameObject Create(string name, Transform parent)
        {
            var prefab = Load(name);
            if (prefab == null) return null;
            var instance = UnityEngine.Object.Instantiate(prefab, parent, false);
            instance.name = "IstanbulTraffic_" + name;
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = Adapt(materials[i]);
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
                renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            }
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
            { collider.enabled = false; UnityEngine.Object.Destroy(collider); }
            return instance;
        }

        static Material Adapt(Material source)
        {
            string name = source.name;
            if (Materials.TryGetValue(name, out var material) && material != null) return material;
            if (_bindings == null)
            {
                _bindings = new Dictionary<string, Binding>(40);
                var asset = Resources.Load<TextAsset>("WorldDetail/Traffic/MaterialBindings");
                var manifest = asset != null ? JsonUtility.FromJson<Manifest>(asset.text) : null;
                if (manifest?.entries != null)
                    for (int i = 0; i < manifest.entries.Length; i++) _bindings[manifest.entries[i].name] = manifest.entries[i];
            }
            _bindings.TryGetValue(name, out var binding);
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name + "_SharedTraffic", enableInstancing = true };
            var color = binding != null ? new Color(binding.r, binding.g, binding.b)
                : source.HasProperty("baseColorFactor") ? source.GetColor("baseColorFactor") : source.color;
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", binding != null ? 1f - binding.roughness : .35f);
            material.SetFloat("_Metallic", binding != null ? binding.metallic : .1f);
            if (binding != null)
            {
                if (!string.IsNullOrEmpty(binding.baseMap)) material.SetTexture("_BaseMap", Resources.Load<Texture2D>(binding.baseMap));
                if (!string.IsNullOrEmpty(binding.normalMap))
                {
                    material.SetTexture("_BumpMap", Resources.Load<Texture2D>(binding.normalMap));
                    material.SetFloat("_BumpScale", .40f); material.EnableKeyword("_NORMALMAP");
                }
            }
            Materials[name] = material; return material;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            foreach (var material in Materials.Values) if (material != null) UnityEngine.Object.Destroy(material);
            Materials.Clear(); Prefabs.Clear(); _bindings = null;
        }
    }
}
