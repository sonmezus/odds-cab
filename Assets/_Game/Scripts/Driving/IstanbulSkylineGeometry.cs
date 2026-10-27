using System.Collections.Generic;
using UnityEngine;

namespace OddsCab
{
    /// <summary>
    /// Shared, batched vista geometry. The existing painted waterfront keeps its detail while
    /// these landforms and an open suspension span give its edges depth and a Bosphorus identity.
    /// Coordinates are relative to each shoreline plate, with all foundations at its waterline.
    /// </summary>
    internal static class IstanbulSkylineGeometry
    {
        internal static Mesh BuildRidges()
        {
            var mesh = new Builder();
            // The ridgelines rise behind the central roofline as well as the panorama's edges.
            // Cool haze separates these shallow mainland slopes from the warm, detailed town.
            Ridge(mesh, -330f, 315f, 54f, 92f, new Color(0.42f, 0.54f, 0.59f), 0.7f);
            Ridge(mesh, -312f, 295f, 39f, 72f, new Color(0.29f, 0.43f, 0.43f), 2.1f);

            // Small, stepped roof groups on the shoulders rather than a regular wall of boxes.
            var random = new System.Random(271026);
            for (int side = 0; side < 2; side++)
            {
                float start = side == 0 ? -284f : 175f;
                for (int i = 0; i < 22; i++)
                {
                    float x = start + i * 4.6f;
                    float bank = HillHeight(x, -312f, 295f, 72f, 2.1f);
                    float width = 2.3f + (float)random.NextDouble() * 1.8f;
                    float height = 2.4f + (float)random.NextDouble() * 3.4f;
                    var wall = Color.Lerp(new Color(0.69f, 0.64f, 0.53f),
                        new Color(0.78f, 0.72f, 0.63f), (float)random.NextDouble());
                    mesh.Box(new Vector3(x, bank - 1f + height * 0.5f, 37f),
                        new Vector3(width, height, 3.1f), wall);
                    mesh.Triangle(new Vector3(x - width * 0.6f, bank - 1f + height, 35.4f),
                        new Vector3(x, bank + height + 0.9f, 35.4f),
                        new Vector3(x + width * 0.6f, bank - 1f + height, 35.4f),
                        new Color(0.50f, 0.36f, 0.28f));
                }
            }
            return mesh.Finish("Istanbul_LayeredRidges");
        }

        static void Ridge(Builder mesh, float left, float right, float depth, float height,
            Color color, float phase)
        {
            const int segments = 112;
            for (int i = 0; i < segments; i++)
            {
                float x0 = Mathf.Lerp(left, right, i / (float)segments);
                float x1 = Mathf.Lerp(left, right, (i + 1) / (float)segments);
                float y0 = HillHeight(x0, left, right, height, phase);
                float y1 = HillHeight(x1, left, right, height, phase);
                mesh.Quad(new Vector3(x0, -1.1f, depth), new Vector3(x0, y0, depth),
                    new Vector3(x1, y1, depth), new Vector3(x1, -1.1f, depth), color);
            }
        }

        static float HillHeight(float x, float left, float right, float height, float phase)
        {
            float t = Mathf.InverseLerp(left, right, x);
            float shore = Mathf.Pow(Mathf.Sin(t * Mathf.PI), 0.68f);
            return height * shore * (0.75f + 0.14f * Mathf.Sin(t * 11f + phase)
                + 0.08f * Mathf.Sin(t * 24f + phase));
        }

        internal static Mesh BuildBridge(bool lights)
        {
            var mesh = new Builder();
            var steel = new Color(0.48f, 0.55f, 0.58f);
            var cable = new Color(0.27f, 0.36f, 0.40f);
            var deck = new Color(0.36f, 0.44f, 0.47f);
            var stone = new Color(0.57f, 0.59f, 0.54f);
            var amber = new Color(1f, 0.66f, 0.29f);
            // Keep the main span inside the portion seen by both the portrait driving camera
            // and the reverse menu view. The town occludes its footings and approach deck.
            const float centreX = -70f, deckY = 29f, towerY = 92f;
            const float leftTower = -57f, rightTower = 57f;
            const float deckDepth = 20f;
            // Both towers have two legs and an open central portal, not solid vertical slabs.
            for (int tower = 0; tower < 2; tower++)
            {
                float x = centreX + (tower == 0 ? leftTower : rightTower);
                foreach (float z in new[] { deckDepth - 4.1f, deckDepth + 4.1f })
                {
                    if (!lights)
                    {
                        mesh.Box(new Vector3(x, towerY * 0.5f, z), new Vector3(2f, towerY, 1.8f), steel);
                        mesh.Box(new Vector3(x, 0.6f, z), new Vector3(4.4f, 2.1f, 4f), stone);
                    }
                    else mesh.Box(new Vector3(x, towerY + 0.2f, z), Vector3.one * 0.8f, amber);
                }
                if (!lights)
                {
                    mesh.Box(new Vector3(x, towerY - 3f, deckDepth), new Vector3(2f, 1.9f, 9.5f), steel);
                    mesh.Box(new Vector3(x, 52f, deckDepth), new Vector3(1.8f, 1.4f, 9.5f), steel);
                }
            }
            if (!lights)
            {
                mesh.Box(new Vector3(centreX, deckY, deckDepth), new Vector3(224f, 1.4f, 9f), deck);
                foreach (float x in new[] { -105f, 105f })
                    mesh.Box(new Vector3(centreX + x, 14f, deckDepth), new Vector3(2.1f, 29f, 8f), stone);
            }
            foreach (float z in new[] { deckDepth - 4.3f, deckDepth + 4.3f })
            {
                // The continuous suspension curve and regularly spaced suspenders remain readable
                // at the final small on-screen scale, without individual LineRenderer draw calls.
                const int segments = 64;
                for (int i = 0; i < segments; i++)
                {
                    float x0 = Mathf.Lerp(-112f, 112f, i / (float)segments);
                    float x1 = Mathf.Lerp(-112f, 112f, (i + 1) / (float)segments);
                    float y0 = CableHeight(x0), y1 = CableHeight(x1);
                    if (!lights)
                    {
                        mesh.Beam(new Vector3(centreX + x0, y0, z), new Vector3(centreX + x1, y1, z), 0.40f, cable);
                        if ((i & 1) == 0 && Mathf.Abs(x0) < 108f)
                            mesh.Beam(new Vector3(centreX + x0, deckY + 0.7f, z),
                                new Vector3(centreX + x0, y0, z), 0.20f, cable);
                    }
                    else if ((i & 3) == 0)
                        mesh.Box(new Vector3(centreX + x0, deckY + 1.1f, z), new Vector3(0.6f, 0.45f, 0.6f), amber);
                }
            }
            return mesh.Finish(lights ? "Istanbul_BridgeEveningLights" : "Istanbul_BosphorusSuspensionBridge");
        }

        static float CableHeight(float x)
        {
            float distance = Mathf.Abs(x);
            return distance <= 57f ? 43f + 49f * (distance / 57f) * (distance / 57f)
                : Mathf.Lerp(92f, 29.7f, (distance - 57f) / 55f);
        }

        sealed class Builder
        {
            readonly List<Vector3> _vertices = new List<Vector3>(4096);
            readonly List<Color> _colors = new List<Color>(4096);
            readonly List<int> _indices = new List<int>(8192);

            public void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color)
            {
                int index = _vertices.Count;
                _vertices.Add(a); _vertices.Add(b); _vertices.Add(c);
                for (int i = 0; i < 3; i++) { _colors.Add(color); _indices.Add(index + i); }
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
            {
                int index = _vertices.Count;
                _vertices.Add(a); _vertices.Add(b); _vertices.Add(c); _vertices.Add(d);
                for (int i = 0; i < 4; i++) _colors.Add(color);
                _indices.Add(index); _indices.Add(index + 1); _indices.Add(index + 2);
                _indices.Add(index); _indices.Add(index + 2); _indices.Add(index + 3);
            }

            public void Box(Vector3 centre, Vector3 size, Color color) =>
                Cuboid(centre, size, Quaternion.identity, color);

            public void Beam(Vector3 a, Vector3 b, float width, Color color)
            {
                Vector3 span = b - a;
                Cuboid((a + b) * 0.5f, new Vector3(width, span.magnitude, width),
                    Quaternion.FromToRotation(Vector3.up, span), color);
            }

            void Cuboid(Vector3 centre, Vector3 size, Quaternion rotation, Color color)
            {
                Vector3 x = rotation * (Vector3.right * size.x * 0.5f);
                Vector3 y = rotation * (Vector3.up * size.y * 0.5f);
                Vector3 z = rotation * (Vector3.forward * size.z * 0.5f);
                Quad(centre - x - y - z, centre - x + y - z, centre + x + y - z, centre + x - y - z, color);
                Quad(centre + x - y + z, centre + x + y + z, centre - x + y + z, centre - x - y + z, color * 0.82f);
                Quad(centre - x - y + z, centre - x + y + z, centre - x + y - z, centre - x - y - z, color * 0.90f);
                Quad(centre + x - y - z, centre + x + y - z, centre + x + y + z, centre + x - y + z, color * 0.84f);
                Quad(centre - x + y - z, centre - x + y + z, centre + x + y + z, centre + x + y - z, color * 1.08f);
                Quad(centre - x - y + z, centre - x - y - z, centre + x - y - z, centre + x - y + z, color * 0.76f);
            }

            public Mesh Finish(string name)
            {
                var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
                mesh.SetVertices(_vertices);
                mesh.SetColors(_colors);
                mesh.SetTriangles(_indices, 0);
                mesh.RecalculateBounds();
                mesh.UploadMeshData(true);
                return mesh;
            }
        }
    }
}
