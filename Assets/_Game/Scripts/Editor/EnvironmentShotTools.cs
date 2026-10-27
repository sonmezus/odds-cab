using System.IO;
using UnityEditor;
using UnityEngine;

namespace OddsCab.Editor
{
    /// <summary>
    /// QA helper: while in play mode, renders a few fixed aerial views of the streamed street around the taxi
    /// to PNG files in &lt;project&gt;/_ClaudeShots, so street variety and environment changes can be reviewed
    /// without driving through the whole level.
    /// </summary>
    public static class EnvironmentShotTools
    {
        [MenuItem("OddsCab/Debug/Capture Street Overview")]
        public static void CaptureStreetOverview()
        {
            if (!EditorApplication.isPlaying || GameManager.Instance == null || GameManager.Instance.Taxi == null)
            {
                Debug.LogWarning("[OddsCab] Enter play mode first.");
                return;
            }
            float z = GameManager.Instance.Taxi.DistanceZ;
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "_ClaudeShots"));
            Directory.CreateDirectory(dir);

            // Right-hand building rows seen from across the street, then an oblique view down the road.
            Shot(dir, "street_right_a.png", new Vector3(-7f, 12f, z + 30f), new Vector3(9.4f, 3f, z + 30f), 75f);
            Shot(dir, "street_right_b.png", new Vector3(-7f, 12f, z + 80f), new Vector3(9.4f, 3f, z + 80f), 75f);
            Shot(dir, "street_down.png", new Vector3(-2f, 16f, z - 6f), new Vector3(6f, 2f, z + 60f), 60f);
            // Horizon from roughly the driving camera, aimed at the sea side where the district landmarks stand.
            int d = GameManager.Instance.City != null ? GameManager.Instance.City.District : 0;
            Shot(dir, "vista_d" + (d + 1) + ".png", new Vector3(-0.6f, 4f, z - 6.8f), new Vector3(-14f, 4f, z + 80f), 55f);
            Debug.Log("[OddsCab] Street overview saved to " + dir);
        }

        [MenuItem("OddsCab/Debug/Preview Next District Look")]
        public static void PreviewNextDistrict()
        {
            if (!EditorApplication.isPlaying || GameManager.Instance == null || GameManager.Instance.City == null)
            {
                Debug.LogWarning("[OddsCab] Enter play mode first.");
                return;
            }
            var city = GameManager.Instance.City;
            int next = (city.District + 1) % GameConfig.DistrictCount;
            city.SetDistrict(next);
            Debug.Log("[OddsCab] Previewing district " + (next + 1) + " (" + Loc.DistrictName(next) + "). Not saved.");
        }

        static void Shot(string dir, string file, Vector3 pos, Vector3 lookAt, float fov)
        {
            var go = new GameObject("QA_ShotCamera");
            try
            {
                var cam = go.AddComponent<Camera>();
                go.transform.position = pos;
                go.transform.LookAt(lookAt);
                cam.fieldOfView = fov;
                cam.nearClipPlane = 0.3f;
                cam.farClipPlane = 600f;
                var main = Camera.main;
                if (main != null) { cam.clearFlags = main.clearFlags; cam.backgroundColor = main.backgroundColor; }

                const int w = 1080, h = 1080;
                var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                cam.Render();
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                cam.targetTexture = null;
                File.WriteAllBytes(Path.Combine(dir, file), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                rt.Release();
                Object.DestroyImmediate(rt);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
