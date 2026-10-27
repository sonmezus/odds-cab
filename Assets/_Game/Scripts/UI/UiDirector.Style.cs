using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace OddsCab
{
    public partial class UiDirector
    {
        // ---------------------------------------------------------------- Display font (titles, headings, big buttons)

        static TMP_FontAsset s_displayFont;

        /// <summary>
        /// Titan One (Resources/Fonts, SIL Open Font License; body text stays LiberationSans): the chunky rounded
        /// display face of the reference posters. Dynamic atlas with room for a thick outline and a drop shadow.
        /// It has every Turkish letter except "İ", which <see cref="UiText"/> draws as I plus a dot in the same face.
        /// </summary>
        public static TMP_FontAsset DisplayFont
        {
            get
            {
                if (s_displayFont != null) return s_displayFont;
                var ttf = Resources.Load<Font>("Fonts/TitanOne-Regular");
                if (ttf == null) return null;
                s_displayFont = TMP_FontAsset.CreateFontAsset(ttf, 90, 12, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);
                if (s_displayFont == null) return null;
                s_displayFont.name = "TitanOne SDF (runtime)";
                // use the shader the bundled TMP font already ships with (guaranteed to be in the build)
                var baseShader = TMP_Settings.defaultFontAsset != null ? TMP_Settings.defaultFontAsset.material.shader : null;
                if (baseShader != null) s_displayFont.material.shader = baseShader;
                // body-font glyphs (arrows, symbols) still resolve through the usual fallbacks
                if (TMP_Settings.defaultFontAsset != null)
                    s_displayFont.fallbackFontAssetTable = UiText.FullSymbolFont != null
                        ? new System.Collections.Generic.List<TMP_FontAsset> { UiText.FullSymbolFont, TMP_Settings.defaultFontAsset }
                        : new System.Collections.Generic.List<TMP_FontAsset> { TMP_Settings.defaultFontAsset };
                return s_displayFont;
            }
        }

        /// <summary>A material for the display font with a thick outline and a drop shadow under it.</summary>
        static Material DisplayMaterial(Color outline, float outlineWidth, Color shadow, Vector2 shadowOffset, float shadowSoftness = 0.05f)
        {
            var font = DisplayFont;
            if (font == null) return null;
            var m = new Material(font.material) { name = "TitanOne Display" };
            m.EnableKeyword("OUTLINE_ON");
            m.SetColor("_OutlineColor", outline);
            m.SetFloat("_OutlineWidth", outlineWidth);
            m.SetFloat("_FaceDilate", outlineWidth * 0.55f);   // keep the letters as fat as before the outline ate into them
            m.EnableKeyword("UNDERLAY_ON");
            m.SetColor("_UnderlayColor", shadow);
            m.SetFloat("_UnderlayOffsetX", shadowOffset.x);
            m.SetFloat("_UnderlayOffsetY", shadowOffset.y);
            m.SetFloat("_UnderlayDilate", outlineWidth * 0.9f);
            m.SetFloat("_UnderlaySoftness", shadowSoftness);
            return m;
        }

        static Material s_headingMat;

        /// <summary>Display face with a vertical colour gradient, outline and shadow (title lines, result headings).</summary>
        static void StyleDisplay(UiText t, Color top, Color bottom, Material mat)
        {
            if (t == null || DisplayFont == null || mat == null) return;
            t.font = DisplayFont;
            t.fontSharedMaterial = mat;
            t.fontStyle = FontStyle.Normal;   // Titan One is heavy already; TMP's fake bold would smear it
            t.color = Color.white;
            t.enableVertexGradient = true;
            t.colorGradient = new VertexGradient(top, top, bottom, bottom);
            t.characterSpacing = 1.5f;
            t.RefreshDisplayText();
        }

        /// <summary>Headings on cards ("DOĞRU CEVAP!", "SONRAKİ DURAK", ...): display face, navy outline, soft shadow.</summary>
        static void StyleHeading(UiText t, Color top, Color bottom)
        {
            if (s_headingMat == null) s_headingMat = DisplayMaterial(new Color(0.05f, 0.10f, 0.19f), 0.22f, new Color(0f, 0f, 0f, 0.45f), new Vector2(0.35f, -0.55f), 0.25f);
            StyleDisplay(t, top, bottom, s_headingMat);
        }

        /// <summary>Big button labels ("YOLA ÇIK", "DEVAM ET"): display face, no outline, a faint drop shadow.</summary>
        static void StyleButtonLabel(UiText t, Color color)
        {
            StyleBody(t, color);
            if (t != null) t.characterSpacing = 0.6f;
        }

        /// <summary>Headings and the big action buttons in the display face (after the screens are built).</summary>
        void ApplyDisplayStyles()
        {
            Color cream = new Color(1f, 0.98f, 0.92f), creamLow = new Color(0.94f, 0.86f, 0.70f);
            Color gold = new Color(1f, 0.90f, 0.28f), goldLow = new Color(0.98f, 0.64f, 0.10f);
            foreach (var t in new[] { _resultTitleText, _stationArrivalTitleText, _routeTitleText, _settingsTitleText })
                StyleHeading(t, cream, creamLow);
            foreach (var t in new[] { _journeyMapTitleText, _districtFinaleTitleText, _taxiShopTitleText, _campaignTitleText, _ferryTitleText })
                StyleHeading(t, gold, goldLow);
            foreach (var t in new[] { _menuPlayBtnText, _stationArrivalBtnText, _resultContinueText })
                StyleButtonLabel(t, ColTextDark);
            if (_menuNextStopHeaderText != null)
            {
                StyleButtonLabel(_menuNextStopHeaderText, ColTextDark);
                _menuNextStopHeaderText.characterSpacing = 1f;
            }
            // the coloured layer under "DOĞRU CEVAP!" becomes the reference's teal extrusion in the same face
            // (its colour is set per result, so the gradient stays white)
            StyleHeading(_resultTitleShadow, Color.white, new Color(0.85f, 0.85f, 0.85f));
            // question, answers and the HUD counters in the same rounded face, without outline
            StyleButtonLabel(_promptText, Color.white);
            foreach (var t in _choiceLabels) StyleButtonLabel(t, ColTextDark);
            foreach (var t in new[] { _hudStopText, _hudSpeedText, _hudCoinText }) StyleButtonLabel(t, Color.white);
            foreach (var card in new[] { _resultBox, _stationArrivalCard, _ferryBanner })
                AddCardShadow(card);
        }

        // ---------------------------------------------------------------- Rounded panels

        static Sprite s_roundedSprite;

        /// <summary>White rounded rectangle, 9-sliced (24 px corners at the 1080-wide reference size).</summary>
        public static Sprite RoundedSprite
        {
            get
            {
                if (s_roundedSprite != null) return s_roundedSprite;
                const int s = 96, r = 32;
                var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "RoundedPanel" };
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        // distance outside the inner rectangle, anti-aliased over one pixel
                        float dx = Mathf.Max(0f, Mathf.Max(r - (x + 0.5f), (x + 0.5f) - (s - r)));
                        float dy = Mathf.Max(0f, Mathf.Max(r - (y + 0.5f), (y + 0.5f) - (s - r)));
                        float a = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                tex.Apply();
                s_roundedSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
                return s_roundedSprite;
            }
        }

        // ---------------------------------------------------------------- Drawn icons (no emoji: phones lack outline emoji fonts)

        static Sprite s_gearSprite, s_taxiIconSprite;

        /// <summary>White cog wheel: eight teeth round a ring.</summary>
        public static Sprite GearSprite
        {
            get
            {
                if (s_gearSprite != null) return s_gearSprite;
                const int s = 96;
                var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "GearIcon" };
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float dx = x + 0.5f - s * 0.5f, dy = y + 0.5f - s * 0.5f, r = Mathf.Sqrt(dx * dx + dy * dy) / (s * 0.5f);
                        float ang = Mathf.Atan2(dy, dx);
                        // tooth profile: squarish bumps from 0.70 out to 0.94 of the radius
                        float tooth = Mathf.Clamp01((Mathf.Cos(ang * 8f) - 0.2f) * 3f);
                        float outer = Mathf.Lerp(0.70f, 0.94f, tooth);
                        float a = Mathf.Clamp01((outer - r) * s * 0.5f) * Mathf.Clamp01((r - 0.30f) * s * 0.5f);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                tex.Apply();
                return s_gearSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
            }
        }

        /// <summary>Little yellow taxi in side view (body, cabin with windows, roof sign, two wheels) for map markers.</summary>
        public static Sprite TaxiIconSprite
        {
            get
            {
                if (s_taxiIconSprite != null) return s_taxiIconSprite;
                const int w = 128, h = 96;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "TaxiIcon" };
                Color clear = new Color(0f, 0f, 0f, 0f), body = new Color(1f, 0.80f, 0.12f), dark = new Color(0.06f, 0.10f, 0.18f),
                      glass = new Color(0.62f, 0.83f, 0.95f), sign = new Color(1f, 0.96f, 0.84f), chrome = new Color(0.85f, 0.87f, 0.9f);
                float Box(float px, float py, float x0, float y0, float x1, float y1, float rad)
                {
                    float cx = Mathf.Clamp(px, x0 + rad, x1 - rad), cy = Mathf.Clamp(py, y0 + rad, y1 - rad);
                    return Mathf.Clamp01(rad - Vector2.Distance(new Vector2(px, py), new Vector2(cx, cy)) + 0.5f);
                }
                float Disc(float px, float py, float cx, float cy, float rad) => Mathf.Clamp01(rad - Vector2.Distance(new Vector2(px, py), new Vector2(cx, cy)) + 0.5f);
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float px = x + 0.5f, py = y + 0.5f;
                        Color c = clear;
                        void Over(Color col, float a) { if (a <= 0f) return; c = Color.Lerp(c, col, a); c.a = Mathf.Max(c.a, a); }
                        // dark outline first (each shape slightly grown), then the colours on top
                        Over(dark, Box(px, py, 6, 18, 122, 56, 14));
                        Over(dark, Box(px, py, 28, 40, 98, 80, 16));
                        Over(dark, Box(px, py, 50, 76, 78, 92, 4));
                        Over(body, Box(px, py, 10, 22, 118, 52, 11));
                        Over(body, Box(px, py, 32, 44, 94, 76, 13));
                        Over(glass, Box(px, py, 38, 52, 60, 70, 4));
                        Over(glass, Box(px, py, 66, 52, 88, 70, 4));
                        Over(sign, Box(px, py, 54, 79, 74, 89, 3));
                        Over(chrome, Box(px, py, 10, 22, 118, 28, 3));
                        Over(dark, Disc(px, py, 34, 20, 14));
                        Over(dark, Disc(px, py, 94, 20, 14));
                        Over(chrome, Disc(px, py, 34, 20, 6));
                        Over(chrome, Disc(px, py, 94, 20, 6));
                        tex.SetPixel(x, y, c);
                    }
                tex.Apply();
                return s_taxiIconSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            }
        }

        /// <summary>Image child showing <paramref name="sprite"/> (icons in place of emoji glyphs).</summary>
        GameObject MakeIcon(Transform parent, string name, Sprite sprite, Vector2 size, Color color)
        {
            var go = MakePanel(parent, name, color, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return go;
        }

        /// <summary>Soft drop shadow under a floating card (reference: cards lift off the scene).</summary>
        static void AddCardShadow(GameObject card)
        {
            if (card == null || card.GetComponent<Shadow>() != null) return;
            var sh = card.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.35f);
            sh.effectDistance = new Vector2(0f, -7f);
        }
    }
}
