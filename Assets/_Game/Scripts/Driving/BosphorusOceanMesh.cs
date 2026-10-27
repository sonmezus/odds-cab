using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OddsCab
{
    /// <summary>
    /// Two shared meshes for the flat Bosphorus: a one-metre foreground and an eight-metre ocean
    /// with a matching hole. Stitched perimeter triangles share the coarse boundary positions,
    /// so GPU wave displacement opens neither cracks nor coplanar overlaps at the change in density.
    /// </summary>
    public static class BosphorusOceanMesh
    {
        public const float Width = 1600f;
        public const float Length = 1800f;
        public const float NearWidth = 240f;
        public const float NearLength = 256f;
        public const float NearCellSize = 1f;
        public const float FarCellSize = 8f;

        static Mesh s_near, s_far;

        public static Mesh NearMesh { get { EnsureMeshes(); return s_near; } }
        public static Mesh FarMesh { get { EnsureMeshes(); return s_far; } }

        public static Transform Create(Transform parent, string name, Vector3 center, Material material)
        {
            EnsureMeshes();
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = center;
            CreateSurface(root, "DetailedWater_1m", s_near, material);
            CreateSurface(root, "DistantWater_8m", s_far, material);
            return root;
        }

        static void CreateSurface(Transform parent, string name, Mesh mesh, Material material)
        {
            var surface = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            surface.transform.SetParent(parent, false);
            surface.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = surface.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        static void EnsureMeshes()
        {
            if (s_near != null && s_far != null) return;
            if (s_near != null) Object.Destroy(s_near);
            if (s_far != null) Object.Destroy(s_far);
            s_near = BuildNearMesh();
            s_far = BuildFarMesh();
        }

        static Mesh BuildNearMesh()
        {
            int nx = Mathf.RoundToInt(NearWidth / NearCellSize);
            int nz = Mathf.RoundToInt(NearLength / NearCellSize);
            int row = nx + 1;
            int coarseStep = Mathf.RoundToInt(FarCellSize / NearCellSize);
            var xs = new float[row];
            var zs = new float[nz + 1];
            for (int x = 0; x <= nx; x++) xs[x] = -NearWidth * 0.5f + x * NearCellSize;
            for (int z = 0; z <= nz; z++) zs[z] = -NearLength * 0.5f + z * NearCellSize;
            var indices = new List<int>(nx * nz * 6);
            // The dense interior stops one cell short of the edge. The boundary strip below stitches
            // its one-metre vertices to the very same eight-metre vertices used by the far mesh.
            for (int z = 1; z < nz - 1; z++)
                for (int x = 1; x < nx - 1; x++) AddQuad(indices, z * row + x, row);

            StitchEdge(indices, 0, nx / coarseStep, coarseStep, row + 1, 1, nx - 2);
            StitchEdge(indices, nx, nz / coarseStep, coarseStep * row, 2 * row - 2, row, nz - 2);
            StitchEdge(indices, nz * row + nx, nx / coarseStep, -coarseStep,
                (nz - 1) * row + nx - 1, -1, nx - 2);
            StitchEdge(indices, nz * row, nz / coarseStep, -coarseStep * row,
                (nz - 1) * row + 1, -row, nz - 2);
            return BuildMesh("Bosphorus_NearGrid_1m_Stitched", xs, zs, indices);
        }

        static void StitchEdge(List<int> indices, int outerStart, int outerSegments,
            int outerStep, int innerStart, int innerStep, int innerSegments)
        {
            int outer = 0, inner = 0;
            while (outer < outerSegments || inner < innerSegments)
            {
                int a = outerStart + outer * outerStep;
                int b = innerStart + inner * innerStep;
                bool advanceInner = outer == outerSegments || (inner < innerSegments
                    && (inner + 1) * outerSegments <= (outer + 1) * innerSegments);
                indices.Add(a);
                indices.Add(b);
                if (advanceInner)
                {
                    indices.Add(b + innerStep);
                    inner++;
                }
                else
                {
                    indices.Add(a + outerStep);
                    outer++;
                }
            }
        }

        static Mesh BuildFarMesh()
        {
            float[] xs = CoarseCoordinates(Width), zs = CoarseCoordinates(Length);
            int row = xs.Length;
            var indices = new List<int>((xs.Length - 1) * (zs.Length - 1) * 6);
            for (int z = 0; z < zs.Length - 1; z++)
                for (int x = 0; x < xs.Length - 1; x++)
                {
                    bool insideNear = xs[x] >= -NearWidth * 0.5f && xs[x + 1] <= NearWidth * 0.5f
                        && zs[z] >= -NearLength * 0.5f && zs[z + 1] <= NearLength * 0.5f;
                    if (!insideNear) AddQuad(indices, z * row + x, row);
                }
            return BuildMesh("Bosphorus_FarGrid_8m_WithNearHole", xs, zs, indices);
        }

        static float[] CoarseCoordinates(float size)
        {
            // Anchor to zero so both hole edges land on the coarse grid even when the ocean's
            // outer dimensions aren't divisible by eight. Only its outermost cells become smaller.
            float half = size * 0.5f;
            var values = new List<float>(Mathf.CeilToInt(size / FarCellSize) + 2) { -half };
            float first = (Mathf.Floor(-half / FarCellSize) + 1f) * FarCellSize;
            for (float value = first; value < half; value += FarCellSize) values.Add(value);
            values.Add(half);
            return values.ToArray();
        }

        static void AddQuad(List<int> indices, int a, int row)
        {
            indices.Add(a); indices.Add(a + row); indices.Add(a + 1);
            indices.Add(a + 1); indices.Add(a + row); indices.Add(a + row + 1);
        }

        static Mesh BuildMesh(string name, float[] xs, float[] zs, List<int> indices)
        {
            int row = xs.Length, count = row * zs.Length;
            var vertices = new Vector3[count];
            var normals = new Vector3[count];
            var uv = new Vector2[count];
            for (int z = 0; z < zs.Length; z++)
                for (int x = 0; x < row; x++)
                {
                    int index = z * row + x;
                    vertices[index] = new Vector3(xs[x], 0f, zs[z]);
                    normals[index] = Vector3.up;
                    uv[index] = new Vector2((xs[x] - xs[0]) / (xs[row - 1] - xs[0]),
                        (zs[z] - zs[0]) / (zs[zs.Length - 1] - zs[0]));
                }
            var mesh = new Mesh
            {
                name = name,
                hideFlags = HideFlags.DontSave,
                indexFormat = IndexFormat.UInt16,
                vertices = vertices,
                normals = normals,
                uv = uv,
                triangles = indices.ToArray(),
                bounds = new Bounds(Vector3.zero, new Vector3(xs[row - 1] - xs[0] + BosphorusWaterMesh.DisplacementMargin * 2f,
                    BosphorusWaterMesh.DisplacementMargin * 2f, zs[zs.Length - 1] - zs[0] + BosphorusWaterMesh.DisplacementMargin * 2f))
            };
            return mesh;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            if (s_near != null) Object.Destroy(s_near);
            if (s_far != null) Object.Destroy(s_far);
            s_near = s_far = null;
        }
    }
}
