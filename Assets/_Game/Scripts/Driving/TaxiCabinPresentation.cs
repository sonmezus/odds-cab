using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OddsCab
{
    /// <summary>Soft sculpted cabin with a restrained shared film-look palette, prepared before frame updates.</summary>
    public static class TaxiCabinPresentation
    {
        static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>(12);
        static GameObject _prefab;
        public static bool Available => (_prefab != null ? _prefab : (_prefab = Resources.Load<GameObject>("WorldDetail/Cabin/TaxiRearCabin"))) != null;

        public static bool TryCreate(Transform parent)
        {
            if (!Available) return false;
            var instance = Object.Instantiate(_prefab, parent, false);
            instance.name = "AuthoredRearCabin";
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                var shared = renderers[r].sharedMaterials;
                for (int i = 0; i < shared.Length; i++) shared[i] = Adapt(shared[i]);
                renderers[r].sharedMaterials = shared;
                renderers[r].shadowCastingMode = ShadowCastingMode.Off;
                renderers[r].receiveShadows = false;
            }
            return true;
        }

        static Material Adapt(Material source)
        {
            string key = source.name;
            if (Materials.TryGetValue(key, out var cached) && cached != null) return cached;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = key + "_CabinPBR", enableInstancing = true };
            Color color = source.HasProperty("baseColorFactor") ? source.GetColor("baseColorFactor") : source.color;
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", source.HasProperty("roughnessFactor") ? 1f - source.GetFloat("roughnessFactor") : .25f);
            material.SetFloat("_Metallic", source.HasProperty("metallicFactor") ? source.GetFloat("metallicFactor") : 0f);
            if (key.Contains("GreenFabric"))
            {
                // Broad cushion forms and sewn piping supply the detail. A dense
                // photographic weave competes with the passenger at portrait distance.
                material.SetFloat("_Smoothness", .22f);
                material.SetFloat("_Metallic", 0f);
            }
            if (key.Contains("SageSeatInset"))
            {
                material.SetFloat("_Smoothness", .18f);
                material.SetFloat("_Metallic", 0f);
            }
            if (key.Contains("ClearGlass"))
            {
                material.SetColor("_BaseColor", new Color(.53f, .67f, .65f, .035f));
                material.SetFloat("_Surface", 1f); material.SetFloat("_Blend", 0f);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f); material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetOverrideTag("RenderType", "Transparent"); material.renderQueue = (int)RenderQueue.Transparent;
            }
            if (key.Contains("WarmDome"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", new Color(.28f, .17f, .07f));
            }
            Materials[key] = material;
            return material;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            foreach (var material in Materials.Values) if (material != null) Object.Destroy(material);
            Materials.Clear(); _prefab = null;
        }
    }
}
