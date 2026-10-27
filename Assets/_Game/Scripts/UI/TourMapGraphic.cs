using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace OddsCab
{
    /// <summary>
    /// Stylised map of the Istanbul tour (TourRoute) drawn as one UI mesh: land, the Sea of Marmara, the Bosphorus and
    /// the Golden Horn, then the taxi's route - driven stretch gold, the rest cream, car-ferry crossings dashed.
    /// The map fills the RectTransform keeping its aspect; <see cref="MapToLocal"/> places pins on it.
    /// </summary>
    public class TourMapGraphic : MaskableGraphic
    {
        public static readonly Color LandColor = new Color(0.95f, 0.90f, 0.79f);
        public static readonly Color WaterColor = new Color(0.42f, 0.70f, 0.86f);
        static readonly Color RoadOutline = new Color(0.06f, 0.11f, 0.19f);
        static readonly Color DrivenColor = new Color(0.98f, 0.76f, 0.16f);
        static readonly Color AheadColor = new Color(1f, 0.98f, 0.93f);
        static readonly Color FerryColor = new Color(0.10f, 0.33f, 0.62f);

        float _progressKm;
        List<(Vector2 km, int district, bool ferry)> _route;
        float[] _routeDist;

        /// <summary>Distance along the tour (km) driven so far; the route up to here is drawn gold.</summary>
        public float ProgressKm { get => _progressKm; set { _progressKm = value; SetVerticesDirty(); } }

        void EnsureRoute()
        {
            if (_route != null) return;
            _route = TourRoute.Polyline();
            _routeDist = new float[_route.Count];
            for (int i = 1; i < _route.Count; i++)
                _routeDist[i] = _routeDist[i - 1] + (_route[i].km - _route[i - 1].km).magnitude;
        }

        /// <summary>Distance along the tour (km) at which <paramref name="district"/> is reached.</summary>
        public float DistrictDistance(int district)
        {
            EnsureRoute();
            for (int i = _route.Count - 1; i > 0; i--)
                if (_route[i].district == district) return _routeDist[i];
            return 0f;
        }

        public float TotalKm { get { EnsureRoute(); return _routeDist[_routeDist.Length - 1]; } }

        /// <summary>Point on the tour (in map km) at distance <paramref name="km"/>.</summary>
        public Vector2 PointAt(float km)
        {
            EnsureRoute();
            for (int i = 1; i < _route.Count; i++)
                if (_routeDist[i] >= km)
                {
                    float seg = _routeDist[i] - _routeDist[i - 1];
                    float t = seg > 1e-5f ? (km - _routeDist[i - 1]) / seg : 1f;
                    return Vector2.Lerp(_route[i - 1].km, _route[i].km, Mathf.Clamp01(t));
                }
            return _route[_route.Count - 1].km;
        }

        float Scale
        {
            get
            {
                Vector2 size = rectTransform.rect.size, km = TourRoute.SizeKm;
                return Mathf.Min(size.x / km.x, size.y / km.y);
            }
        }

        /// <summary>Map kilometres -> local position in this rect (centred).</summary>
        public Vector2 MapToLocal(Vector2 km) => (km - TourRoute.SizeKm * 0.5f) * Scale + rectTransform.rect.center;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            EnsureRoute();
            float s = Scale;
            Vector2 half = TourRoute.SizeKm * 0.5f * s, c = rectTransform.rect.center;
            Quad(vh, c - half, new Vector2(c.x + half.x, c.y - half.y), c + half, new Vector2(c.x - half.x, c.y + half.y), LandColor);

            // Water: Marmara fan, then the straits as thick strips
            var m = TourRoute.Marmara;
            for (int i = 1; i < m.Length; i++)
                Tri(vh, P(m[0]), P(m[i]), P(m[i == m.Length - 1 ? 1 : i + 1]), WaterColor);
            Strip(vh, TourRoute.Bosphorus, s);
            Strip(vh, TourRoute.GoldenHorn, s);

            // Route: dark outline, then driven / ahead / ferry dashes on top
            for (int pass = 0; pass < 2; pass++)
                for (int i = 1; i < _route.Count; i++)
                {
                    Vector2 a = MapToLocal(_route[i - 1].km), b = MapToLocal(_route[i].km);
                    if (_route[i].ferry)
                    {
                        if (pass == 1) Dashes(vh, a, b, 0.11f * s, 6f, FerryColor);
                        continue;
                    }
                    if (pass == 0) { Line(vh, a, b, 13f, RoadOutline); continue; }
                    float d0 = _routeDist[i - 1], d1 = _routeDist[i];
                    if (_progressKm >= d1) Line(vh, a, b, 7f, DrivenColor);
                    else if (_progressKm <= d0) Line(vh, a, b, 7f, AheadColor);
                    else
                    {
                        Vector2 mid = Vector2.Lerp(a, b, (_progressKm - d0) / (d1 - d0));
                        Line(vh, a, mid, 7f, DrivenColor);
                        Line(vh, mid, b, 7f, AheadColor);
                    }
                }

            Vector2 P(Vector2 latLon) => MapToLocal(TourRoute.ToKm(latLon));
        }

        void Strip(VertexHelper vh, Vector3[] line, float s)
        {
            for (int i = 1; i < line.Length; i++)
            {
                Vector2 a = MapToLocal(TourRoute.ToKm(new Vector2(line[i - 1].x, line[i - 1].y)));
                Vector2 b = MapToLocal(TourRoute.ToKm(new Vector2(line[i].x, line[i].y)));
                Vector2 n = Vector2.Perpendicular((b - a).normalized);
                float wa = line[i - 1].z * s * 0.5f, wb = line[i].z * s * 0.5f;
                Quad(vh, a - n * wa, b - n * wb, b + n * wb, a + n * wa, WaterColor);
                Disc(vh, b, wb, WaterColor);   // round joints
            }
        }

        static void Dashes(VertexHelper vh, Vector2 a, Vector2 b, float dash, float width, Color col)
        {
            float len = (b - a).magnitude;
            for (float t = 0f; t < len; t += dash * 2f)
                Line(vh, Vector2.Lerp(a, b, t / len), Vector2.Lerp(a, b, Mathf.Min(len, t + dash) / len), width, col);
        }

        static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color col)
        {
            if ((b - a).sqrMagnitude < 1e-4f) return;
            Vector2 dir = (b - a).normalized, n = Vector2.Perpendicular(dir) * width * 0.5f;
            a -= dir * width * 0.35f; b += dir * width * 0.35f;   // overlap so joints stay closed
            Quad(vh, a - n, b - n, b + n, a + n, col);
        }

        static void Disc(VertexHelper vh, Vector2 c, float r, Color col)
        {
            const int n = 12;
            for (int i = 0; i < n; i++)
            {
                float a0 = i * Mathf.PI * 2f / n, a1 = (i + 1) * Mathf.PI * 2f / n;
                Tri(vh, c, c + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * r, c + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * r, col);
            }
        }

        static void Tri(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color col)
        {
            int i = vh.currentVertCount;
            vh.AddVert(a, col, Vector4.zero); vh.AddVert(b, col, Vector4.zero); vh.AddVert(c, col, Vector4.zero);
            vh.AddTriangle(i, i + 1, i + 2);
        }

        static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color col)
        {
            int i = vh.currentVertCount;
            vh.AddVert(a, col, Vector4.zero); vh.AddVert(b, col, Vector4.zero);
            vh.AddVert(c, col, Vector4.zero); vh.AddVert(d, col, Vector4.zero);
            vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
        }
    }
}
