using UnityEditor;
using UnityEngine;

namespace OddsCab.Editor
{
    /// <summary>
    /// Creates Assets/_Game/Resources/OddsCabSky.mat (Skybox/Procedural) once.
    /// Keeping the material under Resources guarantees the skybox shader ships in mobile builds
    /// (a runtime Shader.Find alone can be stripped). WeatherDirector tints a runtime copy per weather.
    /// </summary>
    [InitializeOnLoad]
    public static class SkyMaterialSetup
    {
        public const string MaterialPath = "Assets/_Game/Resources/OddsCabSky.mat";

        static SkyMaterialSetup()
        {
            EditorApplication.delayCall += EnsureSkyMaterial;
        }

        [MenuItem("OddsCab/Recreate Sky Material")]
        public static void RecreateSkyMaterial()
        {
            AssetDatabase.DeleteAsset(MaterialPath);
            EnsureSkyMaterial();
        }

        static void EnsureSkyMaterial()
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) != null) return;

            var shader = Shader.Find("Skybox/Procedural");
            if (shader == null)
            {
                Debug.LogWarning("[OddsCab] Skybox/Procedural shader not found; sky stays a solid colour.");
                return;
            }

            var mat = new Material(shader) { name = "OddsCabSky" };
            mat.SetFloat("_SunDisk", 2f);              // high quality sun disk
            mat.SetFloat("_SunSize", 0.04f);
            mat.SetFloat("_SunSizeConvergence", 5f);
            mat.SetFloat("_AtmosphereThickness", 0.9f);
            mat.SetColor("_SkyTint", new Color(0.42f, 0.60f, 0.85f));
            mat.SetColor("_GroundColor", new Color(0.60f, 0.64f, 0.66f));
            mat.SetFloat("_Exposure", 1.2f);

            AssetDatabase.CreateAsset(mat, MaterialPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[OddsCab] Created " + MaterialPath);
        }
    }
}
