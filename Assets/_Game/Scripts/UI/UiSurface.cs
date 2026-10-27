using UnityEngine;

namespace OddsCab
{
    /// <summary>Vertex-colour lighting for sliced UI surfaces, sharing the normal uGUI material and draw call.</summary>
    [DisallowMultipleComponent]
    public sealed class UiSurface : UnityEngine.UI.BaseMeshEffect
    {
        public override void ModifyMesh(UnityEngine.UI.VertexHelper vertices)
        {
            if (!IsActive() || vertices.currentVertCount == 0) return;
            if (graphic is UnityEngine.UI.Image image && image.sprite != UiDirector.RoundedSprite) return;
            Rect rect = graphic.rectTransform.rect;
            float inverseHeight = 1f / Mathf.Max(1f, rect.height);
            float inverseWidth = 1f / Mathf.Max(1f, rect.width);
            UIVertex vertex = default;
            for (int i = 0; i < vertices.currentVertCount; i++)
            {
                vertices.PopulateUIVertex(ref vertex, i);
                float y = Mathf.Clamp01((vertex.position.y - rect.yMin) * inverseHeight);
                float x = Mathf.Clamp01((vertex.position.x - rect.xMin) * inverseWidth);
                Color colour = vertex.color;
                float light = Mathf.Lerp(0.84f, 1.12f, y) + Mathf.Max(0f, 1f - Mathf.Abs(x - 0.28f) * 2f) * 0.025f;
                colour.r *= light;
                colour.g *= light;
                colour.b *= light;
                vertex.color = colour;
                vertices.SetUIVertex(vertex, i);
            }
        }
    }
}
