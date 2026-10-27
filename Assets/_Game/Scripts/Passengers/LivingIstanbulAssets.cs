using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OddsCab
{
    /// <summary>Original street citizens, independent from the passenger cast, and pooled wildlife.</summary>
    public static class LivingIstanbulAssets
    {
        static readonly string[] Identities = { "Dockworker", "Bookseller", "Courier", "Neighbour", "Angler", "Ceramist", "Student", "MarketVendor" };
        public static int CitizenIdentityCount => Identities.Length;
        public const string CitizenResourceFolder = "LivingIstanbul/StreetCastArt";
        static readonly Dictionary<string, GameObject> Prefabs = new Dictionary<string, GameObject>(20);
        static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>(20);
        static readonly List<Material> OwnedMaterials = new List<Material>(16);
        static Shader _lit;

        static GameObject Load(string name)
        {
            if (Prefabs.TryGetValue(name, out var prefab) && prefab != null) return prefab;
            prefab = Resources.Load<GameObject>("LivingIstanbul/" + name);
            Prefabs[name] = prefab;
            return prefab;
        }

        public static GameObject Create(string name, Transform parent)
        {
            var prefab = Load(name);
            if (prefab == null) return null;
            var result = Object.Instantiate(prefab, parent, false);
            result.name = "LivingIstanbul_" + name;
            var renderers = result.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                var materials = renderer.sharedMaterials;
                for (int m = 0; m < materials.Length; m++) materials[m] = SharedMaterial(materials[m]);
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                if (renderer is SkinnedMeshRenderer skin)
                {
                    skin.updateWhenOffscreen = false;
                    // GPU curved-world displacement is bounded within the director's 125 m crowd window.
                    var bounds = skin.localBounds;
                    bounds.Expand(new Vector3(22f, 4f, 5f));
                    skin.localBounds = bounds;
                }
            }
            return result;
        }

        static Material SharedMaterial(Material source)
        {
            if (source == null) return null;
            string key = source.name;
            // Editor play-mode exit releases runtime materials while static caches can survive.
            // Unity's destroyed-object null check must guard the hit before reusing it.
            if (Materials.TryGetValue(key, out var existing) && existing != null) return existing;
            Material material;
            {
                if (_lit == null) _lit = Shader.Find("Universal Render Pipeline/Lit");
                material = new Material(_lit) { name = key + "_Shared", enableInstancing = true };
                OwnedMaterials.Add(material);
                Color color = source.HasProperty("baseColorFactor") ? source.GetColor("baseColorFactor") : source.color;
                material.SetColor("_BaseColor", color);
                material.SetFloat("_Smoothness", source.HasProperty("roughnessFactor") ? 1f - source.GetFloat("roughnessFactor") : .25f);
                material.SetFloat("_Metallic", source.HasProperty("metallicFactor") ? source.GetFloat("metallicFactor") : 0f);
                if (key.StartsWith("CITIZEN_", System.StringComparison.Ordinal))
                {
                    material.shader = Shader.Find("OddsCab/StylizedCharacter") ?? _lit;
                    var atlas = Resources.Load<Texture2D>(CitizenResourceFolder + "/CitizenAtlas");
                    material.SetTexture("_BaseMap", atlas);
                    material.SetColor("_BaseColor", Color.white);
                    material.SetFloat("_Metallic", 0f);
                    material.SetFloat("_Smoothness", key.StartsWith("CITIZEN_Eyes", System.StringComparison.Ordinal) ? .82f : .25f);
                }
                if (key.EndsWith("_Palette", System.StringComparison.Ordinal))
                {
                    var palette = Resources.Load<Texture2D>("LivingIstanbul/" + key.Substring(5));
                    if (palette != null) material.SetTexture("_BaseMap", palette);
                }
                if (key.StartsWith("DECO_", System.StringComparison.Ordinal))
                {
                    material.SetTexture("_BaseMap", Resources.Load<Texture2D>("LivingIstanbul/ArtDirection/WarmIstanbulPalette"));
                    material.SetColor("_BaseColor", Color.white);
                    material.SetFloat("_Smoothness", .36f);
                    material.SetFloat("_Metallic", 0f);
                }
                if (key.Contains("Feather")) material.SetFloat("_Cull", 0f);
            }
            Materials[key] = material;
            return material;
        }

        public static bool TryCreateCharacter(int identity, Transform parent, out GameObject instance,
            out AmbientNpcAnimator motion)
        {
            identity = Mathf.Clamp(identity, 0, Identities.Length - 1);
            string name = "StreetCastArt/Citizen_" + Identities[identity];
            instance = Create(name, parent);
            motion = null;
            if (instance == null) return false;
            // The authored street cast is 1.91-1.95 m tall; passengers are fitted to 1.72 m.
            // Scale the entire rig uniformly, including fishermen and both LODs, before binding animation.
            instance.transform.localScale *= .88f;
            var high = instance.GetComponentsInChildren<Renderer>(true);
            var low = Create(name + "_LOD1", instance.transform);
            if (low != null)
            {
                var skeleton = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true)[0];
                var boneMap = new Dictionary<string, Transform>(64);
                for (int i = 0; i < skeleton.bones.Length; i++) boneMap[skeleton.bones[i].name] = skeleton.bones[i];
                var lowSkins = low.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                for (int i = 0; i < lowSkins.Length; i++)
                {
                    var mapped = lowSkins[i].bones;
                    for (int b = 0; b < mapped.Length; b++)
                        if (mapped[b] != null && boneMap.TryGetValue(mapped[b].name, out var joint)) mapped[b] = joint;
                    lowSkins[i].bones = mapped;
                    lowSkins[i].rootBone = skeleton.rootBone;
                }
                var group = instance.AddComponent<LODGroup>();
                group.fadeMode = LODFadeMode.None;
                group.SetLODs(new[] { new LOD(.09f, high), new LOD(.008f, low.GetComponentsInChildren<Renderer>(true)) });
                group.localReferencePoint = Vector3.up * .98f;
                group.size = 2f;
            }
            motion = instance.AddComponent<AmbientNpcAnimator>();
            motion.Bind(instance.transform);
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCaches()
        {
            for (int i = 0; i < OwnedMaterials.Count; i++)
                if (OwnedMaterials[i] != null) Object.Destroy(OwnedMaterials[i]);
            OwnedMaterials.Clear(); Materials.Clear(); Prefabs.Clear(); _lit = null;
        }
    }
}
