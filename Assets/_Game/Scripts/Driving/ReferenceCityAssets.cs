using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OddsCab
{
    /// <summary>Shared, locally authored reference kit. Loading and material adaptation happen during setup only.</summary>
    public static class ReferenceCityAssets
    {
        static readonly Dictionary<string, GameObject> Prefabs = new Dictionary<string, GameObject>(80);
        static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>(48);
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        static readonly int Smoothness = Shader.PropertyToID("_Smoothness");
        static readonly int Metallic = Shader.PropertyToID("_Metallic");
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        static Shader _lit;
        static bool _night;

        public static void SetNightMood(bool night)
        {
            _night = night;
            foreach (var entry in Materials) ApplyDistrictEmission(entry.Key, entry.Value);
        }

        static void ApplyDistrictEmission(string key, Material material)
        {
            if (material == null) return;
            if (!key.StartsWith("LC_", System.StringComparison.Ordinal)) return;
            bool glass = key.Contains("Glass"), interior = key.Contains("Interior"), lamp = key.Contains("Glow");
            if (!glass && !interior && !lamp) return;
            material.EnableKeyword("_EMISSION");
            float intensity = glass ? (_night ? 0.48f : 0f) : interior ? (_night ? 0.32f : 0.07f) : (_night ? 2.0f : 0.30f);
            material.SetColor(EmissionColor, new Color(1f, 0.57f, 0.23f) * intensity);
        }

        public static bool Available(string name) => Load(name) != null;

        static GameObject Load(string name)
        {
            if (Prefabs.TryGetValue(name, out var prefab)) return prefab;
            prefab = Resources.Load<GameObject>("ReferenceKit/" + name);
            Prefabs.Add(name, prefab);
            return prefab;
        }

        public static bool TryCreate(string name, Transform parent, Vector3 position, Quaternion rotation,
            float scale, out GameObject instance)
        {
            var prefab = Load(name);
            instance = null;
            if (prefab == null) return false;
            instance = Object.Instantiate(prefab, parent, false);
            instance.name = "EModel_Reference_" + name;
            instance.transform.SetLocalPositionAndRotation(position, rotation);
            instance.transform.localScale = Vector3.one * scale;
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                var materials = renderer.sharedMaterials;
                for (int m = 0; m < materials.Length; m++) materials[m] = Adapt(materials[m]);
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            }
            // The kit contains visual geometry only; no imported colliders participate in route-space movement.
            var colliders = instance.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
                Object.Destroy(colliders[i]);
            }
            AddDistanceLod(name, instance, renderers);
            return true;
        }

        static void AddDistanceLod(string name, GameObject instance, Renderer[] detailedRenderers)
        {
            bool mansion = name == "MansionRow" || (name.StartsWith("Districts/DistrictBlock_", System.StringComparison.Ordinal) && !name.EndsWith("_LOD1", System.StringComparison.Ordinal));
            bool furniture = name == "CafeSet" || name == "Bench" || name == "PlanterOlive" || name == "MarketDisplay" || name == "StreetBin";
            if (!mansion && !furniture && name != "PlaneTree" && name != "Cypress" && name != "FlowerTree") return;
            if (!TryCreate(name + "_LOD1", instance.transform, Vector3.zero, Quaternion.identity, 1f, out var distant)) return;
            var distantRenderers = distant.GetComponentsInChildren<Renderer>(true);
            var group = instance.AddComponent<LODGroup>();
            group.fadeMode = LODFadeMode.None;
            group.SetLODs(new[]
            {
                new LOD(mansion ? 0.28f : furniture ? 0.10f : 0.20f, detailedRenderers),
                new LOD(0.008f, distantRenderers)
            });
            group.RecalculateBounds();
        }

        static Material Adapt(Material source)
        {
            if (source == null) return null;
            string key = source.name;
            if (Materials.TryGetValue(key, out var material) && material != null) return material;
            if (_lit == null) _lit = Shader.Find("Universal Render Pipeline/Lit");
            if (_lit == null) return source;
            material = new Material(_lit) { name = key + "_ReferencePBR", enableInstancing = true };
            var color = source.HasProperty("baseColorFactor") ? source.GetColor("baseColorFactor") :
                source.HasProperty(BaseColor) ? source.GetColor(BaseColor) : source.color;
            var texture = source.HasProperty("baseColorTexture") ? source.GetTexture("baseColorTexture") : source.mainTexture;
            material.SetColor(BaseColor, color);
            if (texture != null) material.SetTexture(BaseMap, texture);
            float roughness = source.HasProperty("roughnessFactor") ? source.GetFloat("roughnessFactor") : 0.7f;
            material.SetFloat(Smoothness, Mathf.Clamp01(1f - roughness));
            material.SetFloat(Metallic, source.HasProperty("metallicFactor") ? source.GetFloat("metallicFactor") : 0f);
            if (key.Contains("LC_Glass"))
            {
                // One shared glass batch per row reveals the furnished shops without a refraction copy.
                material.SetColor(BaseColor, new Color(0.22f, 0.39f, 0.42f, 0.24f));
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetOverrideTag("RenderType", "Transparent");
                material.renderQueue = (int)RenderQueue.Transparent;
                material.SetShaderPassEnabled("ShadowCaster", false);
            }
            if (source.HasProperty("emissiveFactor"))
            {
                Color emission = source.GetColor("emissiveFactor");
                if (emission.maxColorComponent > 0.001f)
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor(EmissionColor, emission);
                }
            }
            if (key.Contains("Interior"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor(EmissionColor, new Color(0.10f, 0.043f, 0.012f));
            }
            bool districtSurface = key.StartsWith("LC_", System.StringComparison.Ordinal);
            string normalName = key.Contains("LC_Brick") ? "Districts/Brick" :
                key.Contains("CreamPlaster") || key.Contains("LC_Cream") ? "PlasterCream" :
                key.Contains("CoralPlaster") || key.Contains("LC_Coral") ? "PlasterCoral" :
                key.Contains("Stone") || key.Contains("LC_Lime") ? "Limestone" :
                key.Contains("Wood") ? "WoodOak" :
                key.Contains("Bark") ? "TreeBark" : key.Contains("LC_Tile") ? "RoofTile" : null;
            string surfaceName = normalName ?? (key.Contains("RK_Tile") ? "RoofTile" :
                key.Contains("RK_Iznik") ? "Iznik" : null);
            if (surfaceName != null)
            {
                string surfacePath = surfaceName == "Districts/Brick" ? "ReferenceKit/Districts/Surfaces/Brick" : "ReferenceKit/Surfaces/" + surfaceName;
                var surface = Resources.Load<Texture2D>(surfacePath);
                if (surface != null)
                {
                    material.SetTexture(BaseMap, surface);
                    if (districtSurface) material.SetColor(BaseColor, Color.white);
                    if (surfaceName == "Districts/Brick") material.mainTextureScale = Vector2.one * 1.875f;
                }
            }
            if (normalName != null)
            {
                string normalPath = normalName == "Districts/Brick" ? "ReferenceKit/Districts/Normals/Brick" : "ReferenceKit/Normals/" + normalName;
                var normal = Resources.Load<Texture2D>(normalPath);
                if (normal != null)
                {
                    material.SetTexture("_BumpMap", normal);
                    material.SetFloat("_BumpScale", key.Contains("Plaster") ? 0.22f : 0.40f);
                    material.EnableKeyword("_NORMALMAP");
                }
            }
            if (key.Contains("Leaf") || key.Contains("Flower"))
            {
                material.SetFloat("_Cull", 0f);
                material.SetFloat(Smoothness, 0.12f);
            }
            ApplyDistrictEmission(key, material);
            Materials[key] = material;
            return material;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache()
        {
            foreach (var material in Materials.Values)
                if (material != null) Object.Destroy(material);
            Materials.Clear();
            Prefabs.Clear();
            _lit = null;
            _night = false;
        }
    }
}
