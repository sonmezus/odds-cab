using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OddsCab
{
    /// <summary>
    /// Shared two-dimensional water grids. Built during world setup; waves run entirely on the GPU.
    /// Cell spacing resolves the shader's minimum 16 m wavelength across both axes.
    /// </summary>
    public static class BosphorusWaterMesh
    {
        public const float DefaultCellSize = 4f;
        // Two amplitudes are clamped to .18m, including their horizontal Gerstner displacement.
        public const float DisplacementMargin = BosphorusSeaState.MaximumDisplacement;
        const int MaxSegments = 256;
        // Match CurveBender's production limits and CurvedWorld.hlsl's tangent extension.
        const float StraightDistance = 28f;
        const float FullCurveDistance = 90f;
        const float MaxSideCurvature = 0.0022f;
        const float MaxRiseCurvature = 0.0011f;

        static readonly Dictionary<(float width, float length, int x, int z), Mesh> Meshes =
            new Dictionary<(float, float, int, int), Mesh>();

        public static Transform Create(Transform parent, string name, Vector3 center, Vector2 size,
            Material material, float cellSize = DefaultCellSize, bool curveBounds = false)
        {
            var surface = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            surface.transform.SetParent(parent, false);
            surface.transform.localPosition = center;
            surface.GetComponent<MeshFilter>().sharedMesh = GetSharedMesh(size.x, size.y, cellSize);
            var renderer = surface.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            if (curveBounds)
            {
                // Include the complete last tile ahead, even when the taxi has only just entered its tile.
                // CurveBender displaces vertices without touching CPU bounds.
                float distance = Mathf.Max(0f,
                    (GameConfig.ChunksAhead + 1) * GameConfig.ChunkLength + DisplacementMargin - StraightDistance);
                float curved = Mathf.Min(distance, FullCurveDistance);
                float bend = curved * curved + 2f * FullCurveDistance * (distance - curved);
                var bounds = renderer.localBounds;
                bounds.Expand(new Vector3(bend * MaxSideCurvature * 2f, bend * MaxRiseCurvature * 2f, 0f));
                renderer.localBounds = bounds;
            }
            return surface.transform;
        }

        public static Mesh GetSharedMesh(float width, float length, float cellSize = DefaultCellSize)
        {
            if (!IsPositiveFinite(width)) throw new ArgumentOutOfRangeException(nameof(width));
            if (!IsPositiveFinite(length)) throw new ArgumentOutOfRangeException(nameof(length));
            if (!IsPositiveFinite(cellSize)) throw new ArgumentOutOfRangeException(nameof(cellSize));
            int nx = Mathf.Clamp(Mathf.CeilToInt(width / cellSize), 1, MaxSegments);
            int nz = Mathf.Clamp(Mathf.CeilToInt(length / cellSize), 1, MaxSegments);
            var key = (width, length, nx, nz);
            if (Meshes.TryGetValue(key, out var existing) && existing != null) return existing;

            int row = nx + 1;
            int vertexCount = row * (nz + 1);
            var positions = new Vector3[vertexCount];
            var normals = new Vector3[vertexCount];
            var uv = new Vector2[vertexCount];
            var indices = new int[nx * nz * 6];
            for (int z = 0; z <= nz; z++)
            {
                float vz = (float)z / nz;
                for (int x = 0; x <= nx; x++)
                {
                    int index = z * row + x;
                    float vx = (float)x / nx;
                    positions[index] = new Vector3((vx - 0.5f) * width, 0f, (vz - 0.5f) * length);
                    normals[index] = Vector3.up;
                    uv[index] = new Vector2(vx, vz);
                }
            }
            int triangle = 0;
            for (int z = 0; z < nz; z++)
            {
                for (int x = 0; x < nx; x++)
                {
                    int a = z * row + x;
                    indices[triangle++] = a;
                    indices[triangle++] = a + row;
                    indices[triangle++] = a + 1;
                    indices[triangle++] = a + 1;
                    indices[triangle++] = a + row;
                    indices[triangle++] = a + row + 1;
                }
            }
            var mesh = new Mesh
            {
                name = "BosphorusWaterGrid",
                hideFlags = HideFlags.DontSave,
                indexFormat = vertexCount > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16,
                vertices = positions,
                normals = normals,
                uv = uv,
                triangles = indices,
                bounds = new Bounds(Vector3.zero,
                    new Vector3(width + DisplacementMargin * 2f, DisplacementMargin * 2f,
                        length + DisplacementMargin * 2f))
            };
            Meshes[key] = mesh;
            return mesh;
        }

        static bool IsPositiveFinite(float value) => value > 0f && !float.IsInfinity(value);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache()
        {
            foreach (var mesh in Meshes.Values)
                if (mesh != null) UnityEngine.Object.Destroy(mesh);
            Meshes.Clear();
        }
    }
}
