using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using OddsCab.Puzzles;

namespace OddsCab
{
    public partial class UiDirector
    {
        // Main-menu route (reference: the "512 DURAKLIK YOLCULUK" poster): a winding road climbing from Kız Kulesi on the
        // coast to Ortaköy, one pin per district, the stretch already driven in gold and the taxi at the current stop.
        // Points are reference units (canvas width 1080) in a 1080 x 720 box over the street and the hill behind it;
        // pin d is where district d starts, the last point is the finish after Ortaköy.
        static readonly Vector2[] RoutePoints =
        {
            new Vector2(-10f, -312f), new Vector2(200f, -232f), new Vector2(-185f, -150f), new Vector2(190f, -66f),
            new Vector2(-195f, 18f), new Vector2(185f, 102f), new Vector2(-165f, 184f), new Vector2(120f, 258f),
            new Vector2(-30f, 330f),
        };
        const int RouteSamplesPerLeg = 12;
        // opaque: overlapping translucent segments would read as stripes
        static readonly Color RouteAheadColor = new Color(0.97f, 0.92f, 0.80f);
        static readonly Color RouteFerryColor = new Color(0.36f, 0.72f, 0.95f), RouteFerryGap = new Color(0.04f, 0.09f, 0.16f);

        RectTransform _menuRoute;
        readonly List<Image> _routeSegments = new List<Image>();
        readonly List<float> _routeSegmentU = new List<float>();
        RectTransform[] _routePins;
        Image[] _routePinFills;
        UiText[] _routePinTexts, _routePinLabels;
        RectTransform _routeTaxi;
        Tween _routePulse;
        static Sprite s_discSprite;

        void BuildMenuRoute(Transform menu)
        {
            var go = new GameObject("MenuRoute", typeof(RectTransform));
            go.transform.SetParent(menu, false);
            _menuRoute = go.GetComponent<RectTransform>();
            SetRect(_menuRoute, new Vector2(0.5f, 0.585f), new Vector2(0.5f, 0.585f), Vector2.zero, new Vector2(1080f, 720f));
            if (s_discSprite == null) s_discSprite = CreateDiscSprite();

            // Road: a dark outline under the gold (driven) / cream (ahead) band
            int samples = (RoutePoints.Length - 1) * RouteSamplesPerLeg;
            for (int pass = 0; pass < 2; pass++)
            {
                Vector2 prev = RoutePoint(0f);
                for (int i = 1; i <= samples; i++)
                {
                    float u = (float)i / RouteSamplesPerLeg;
                    Vector2 p = RoutePoint(u);
                    var seg = MakeRouteSegment(prev, p, pass == 0 ? 24f : 12f, pass == 0 ? ColNavy : RouteAheadColor);
                    if (pass == 1) { _routeSegments.Add(seg); _routeSegmentU.Add(u - 0.5f / RouteSamplesPerLeg); }
                    prev = p;
                }
            }
            // Coins half way between the pins, as on the poster's road
            for (int i = 0; i < RoutePoints.Length - 1; i++)
                MakeRouteDisc("Coin", RoutePoint(i + 0.5f), 24f, new Color(1f, 0.80f, 0.22f));

            int n = GameConfig.DistrictCount;
            _routePins = new RectTransform[n];
            _routePinFills = new Image[n];
            _routePinTexts = new UiText[n];
            _routePinLabels = new UiText[n];
            for (int d = 0; d < n; d++)
            {
                Vector2 p = RoutePoints[d];
                var ring = MakeRouteDisc("DistrictPin_" + d, p, 66f, ColNavy);
                _routePins[d] = ring.rectTransform;
                _routePinFills[d] = MakeRouteDisc("Fill", Vector2.zero, 52f, ColNavySoft, ring.transform);
                _routePinTexts[d] = MakeLabel(ring.transform, "Num", (d + 1).ToString(), 28, FontStyle.Bold, ColCream, TextAnchor.MiddleCenter);
                Stretch(_routePinTexts[d].rectTransform);
                _routePinTexts[d].raycastTarget = false;

                // Name tag on the outer side of the bend
                bool right = p.x > 0f;
                var tag = MakePanel(_menuRoute, "Tag_" + d, new Color(0.04f, 0.09f, 0.16f, 0.78f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    p + new Vector2(right ? 172f : -172f, 0f), new Vector2(250f, 46f));
                tag.GetComponent<Image>().raycastTarget = false;
                tag.transform.SetSiblingIndex(ring.transform.GetSiblingIndex());
                _routePinLabels[d] = MakeLabel(tag.transform, "Name", "", 23, FontStyle.Bold, ColCream, TextAnchor.MiddleCenter);
                Stretch(_routePinLabels[d].rectTransform);
                _routePinLabels[d].raycastTarget = false;
            }

            // finish: a little chequered flag on a navy disc
            var finish = MakeRouteDisc("Finish", RoutePoints[RoutePoints.Length - 1], 60f, ColNavy);
            for (int cy = 0; cy < 3; cy++)
                for (int cx = 0; cx < 3; cx++)
                {
                    var sq = MakePanel(finish.transform, "Check", (cx + cy) % 2 == 0 ? ColCream : ColNavySoft, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        new Vector2((cx - 1f) * 11f, (cy - 1f) * 11f), new Vector2(11f, 11f));
                    sq.GetComponent<Image>().sprite = null;   // crisp squares
                    sq.GetComponent<Image>().raycastTarget = false;
                }

            var taxi = MakeRouteDisc("RouteTaxi", RoutePoint(0f), 78f, ColNavy);
            _routeTaxi = taxi.rectTransform;
            MakeRouteDisc("Fill", Vector2.zero, 68f, ColCream, taxi.transform);
            MakeIcon(taxi.transform, "Cab", TaxiIconSprite, new Vector2(56f, 42f), Color.white);
        }

        void RefreshMenuRoute()
        {
            if (_menuRoute == null) return;
            int stop = Mathf.Clamp(SaveSystem.Data.currentStop, 1, GameConfig.TotalStops);
            int district = StopPlanner.DistrictIndex(stop);
            int first = district * GameConfig.StopsPerDistrict + 1;
            float u = SaveSystem.Data.campaignComplete ? RoutePoints.Length - 1
                : district + (float)(stop - first) / GameConfig.StopsPerDistrict;

            for (int i = 0; i < _routeSegments.Count; i++)
            {
                // Legs that cross on the car ferry are dashed sea-blue (driven ones gold dashes)
                int toDistrict = (int)_routeSegmentU[i] + 1;
                bool ferry = toDistrict < GameConfig.DistrictCount && TourRoute.ApproachHasFerry(toDistrict);
                Color c = _routeSegmentU[i] <= u ? ColYellow : ferry ? RouteFerryColor : RouteAheadColor;
                _routeSegments[i].color = ferry && i % 2 == 1 ? RouteFerryGap : c;
            }

            for (int d = 0; d < _routePins.Length; d++)
            {
                bool done = d < district || SaveSystem.Data.campaignComplete;
                bool current = d == district && !done;
                _routePinFills[d].color = done ? ColTeal : current ? ColYellow : ColNavySoft;
                _routePinTexts[d].text = (d + 1).ToString();   // done = teal pin
                _routePinTexts[d].color = current ? ColTextDark : ColCream;
                _routePinLabels[d].text = Loc.DistrictLandmark(d);
                _routePinLabels[d].color = current ? ColYellow : ColCream;
                _routePins[d].localScale = Vector3.one;
            }

            _routeTaxi.anchoredPosition = RoutePoint(u);
            _routePulse?.Kill();
            if (district < _routePins.Length && !SaveSystem.Data.campaignComplete)
                _routePulse = _routePins[district].DOScale(1.16f, 0.6f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine)
                    .SetUpdate(true).SetLink(_routePins[district].gameObject);
        }

        /// <summary>Catmull-Rom curve through <see cref="RoutePoints"/>; u = 0 at Kız Kulesi, one unit per district.</summary>
        static Vector2 RoutePoint(float u)
        {
            int n = RoutePoints.Length;
            u = Mathf.Clamp(u, 0f, n - 1);
            int i = Mathf.Min((int)u, n - 2);
            float t = u - i, t2 = t * t, t3 = t2 * t;
            Vector2 p0 = RoutePoints[Mathf.Max(i - 1, 0)], p1 = RoutePoints[i], p2 = RoutePoints[i + 1], p3 = RoutePoints[Mathf.Min(i + 2, n - 1)];
            return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3);
        }

        Image MakeRouteSegment(Vector2 a, Vector2 b, float width, Color color)
        {
            var go = MakePanel(_menuRoute, "Road", color, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), (a + b) * 0.5f,
                new Vector2((b - a).magnitude + width * 0.5f, width));
            go.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            return img;
        }

        Image MakeRouteDisc(string name, Vector2 pos, float size, Color color, Transform parent = null)
        {
            var go = MakePanel(parent != null ? parent : _menuRoute, name, color, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
            var img = go.GetComponent<Image>();
            img.sprite = s_discSprite;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>White anti-aliased disc, tinted per use.</summary>
        static Sprite CreateDiscSprite()
        {
            const int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "RouteDisc" };
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = x + 0.5f - s * 0.5f, dy = y + 0.5f - s * 0.5f;
                    float a = Mathf.Clamp01(s * 0.5f - Mathf.Sqrt(dx * dx + dy * dy));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
