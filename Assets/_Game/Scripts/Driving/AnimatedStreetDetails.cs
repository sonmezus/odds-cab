using UnityEngine;

namespace OddsCab
{
    /// <summary>Additive, pooled Blender props. Existing building geometry and road clearance stay intact.</summary>
    public static class AnimatedStreetDetails
    {
        static readonly string[] Modules = { "Decor_FlowerCart", "Decor_TeaCorner", "Decor_CeramicShelf" };

        public static void Populate(Transform chunk, int variant, bool coastal)
        {
            Place(chunk, variant % 3, 5.65f, 10.5f, -90f);
            Place(chunk, (variant + 1) % 3, 5.65f, 32.5f, -90f);
            if (!coastal) Place(chunk, (variant + 2) % 3, -5.65f, 20.5f, 90f);
        }

        static void Place(Transform parent, int module, float x, float z, float yaw)
        {
            var prop = LivingIstanbulAssets.Create("ArtDirection/" + Modules[module], parent);
            if (prop == null) return;
            prop.transform.localPosition = new Vector3(x, .16f, z);
            prop.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var renderers = prop.GetComponentsInChildren<Renderer>();
            var lod = prop.AddComponent<LODGroup>();
            lod.SetLODs(new[] { new LOD(.022f, renderers) });
            lod.RecalculateBounds();
        }
    }
}
