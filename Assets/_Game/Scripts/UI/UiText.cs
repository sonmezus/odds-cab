using TMPro;
using UnityEngine;

namespace OddsCab
{
    /// <summary>
    /// The UI's label component: TextMeshPro (sharp SDF text at any size and on any screen density) with the
    /// small slice of the legacy <see cref="UnityEngine.UI.Text"/> API the UI code was written against, mapped
    /// onto TMP. New code can use the TMP properties directly.
    /// </summary>
    public class UiText : TextMeshProUGUI
    {
        static bool s_fallbacksReady;

        /// <summary>Dynamic LiberationSans with the symbol glyphs (set up by <see cref="EnsureSymbolFallbacks"/>).</summary>
        public static TMP_FontAsset FullSymbolFont { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { s_fallbacksReady = false; FullSymbolFont = null; }

        /// <summary>
        /// The UI's ▶ ◀ ⚙ ☀ ↺ arrows and emoji icons are not in the default font (LiberationSans); legacy Text
        /// borrowed them from the OS font fallback. Give TMP the same: dynamic font assets built from installed
        /// symbol/emoji fonts (Windows, Android, iOS names), added as global fallbacks once.
        /// Emoji render as single-colour shapes (TMP draws outlines, not colour bitmaps).
        /// </summary>
        public static void EnsureSymbolFallbacks()
        {
            if (s_fallbacksReady) return;
            s_fallbacksReady = true;
            var settingsFallbacks = TMP_Settings.fallbackFontAssets;
            if (settingsFallbacks == null) return;
            // The list lives on the TMP Settings asset, which outlives a play session in the editor: drop the
            // runtime fonts a previous session added (their materials are already destroyed).
            settingsFallbacks.RemoveAll(f => f == null || f.material == null || f.name.StartsWith("Fallback "));
            // First the full LiberationSans file (Resources/Fonts, OFL): the bundled TMP asset is a pre-baked subset,
            // the font itself has the WGL4 symbols the UI and puzzle texts use (→ ← ● ► ♪ ♫ • √) on every phone
            var ttf = Resources.Load<Font>("Fonts/LiberationSans");
            if (ttf != null)
            {
                var lib = TMP_FontAsset.CreateFontAsset(ttf, 72, 8, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 512, 512, AtlasPopulationMode.Dynamic);
                if (lib != null)
                {
                    lib.name = "Fallback LiberationSans (full)";
                    lib.hideFlags = HideFlags.DontSave;
                    if (TMP_Settings.defaultFontAsset != null) lib.material.shader = TMP_Settings.defaultFontAsset.material.shader;
                    settingsFallbacks.Insert(0, lib);
                    FullSymbolFont = lib;
                }
            }
            string[] families =
            {
                "Segoe UI Symbol", "Segoe UI Emoji",                      // Windows
                "Noto Sans Symbols", "Noto Sans Symbols 2", "Noto Emoji",  // Android
                "Apple Symbols", "Arial Unicode MS",                      // iOS / macOS
                "DejaVu Sans"
            };
            foreach (var family in families)
            {
                TMP_FontAsset fa = null;
                try { fa = TMP_FontAsset.CreateFontAsset(family, "Regular"); } catch { }
                if (fa == null) continue;
                fa.name = "Fallback " + family;
                fa.hideFlags = HideFlags.DontSave;   // runtime only: never written into TMP Settings.asset
                settingsFallbacks.Add(fa);
            }
        }

        /// <summary>
        /// The display face (Titan One) has every Turkish letter except the dotted capital "İ"; in that face it is
        /// written as an "I" with a dot set over it, so headings like "SONRAKİ DURAK" stay in one font.
        /// </summary>
        public override string text
        {
            get => base.text;
            set => base.text = DotCapitalI(value);
        }

        const string IWithDot = "I<space=-0.31em><voffset=0.86em><size=72%>.</size></voffset><space=0.07em>";

        string DotCapitalI(string value)
        {
            if (string.IsNullOrEmpty(value) || value.IndexOf('İ') < 0 || font == null || !font.name.StartsWith("TitanOne")) return value;
            return value.Replace("İ", IWithDot);
        }

        /// <summary>Re-applies <see cref="DotCapitalI"/> after the font changed.</summary>
        public void RefreshDisplayText() => text = base.text;

        /// <summary>Legacy anchor-style alignment, mapped to TMP's alignment.</summary>
        public new TextAnchor alignment
        {
            get
            {
                switch (base.alignment)
                {
                    case TextAlignmentOptions.TopLeft: return TextAnchor.UpperLeft;
                    case TextAlignmentOptions.Top: return TextAnchor.UpperCenter;
                    case TextAlignmentOptions.TopRight: return TextAnchor.UpperRight;
                    case TextAlignmentOptions.Left: return TextAnchor.MiddleLeft;
                    case TextAlignmentOptions.Right: return TextAnchor.MiddleRight;
                    case TextAlignmentOptions.BottomLeft: return TextAnchor.LowerLeft;
                    case TextAlignmentOptions.Bottom: return TextAnchor.LowerCenter;
                    case TextAlignmentOptions.BottomRight: return TextAnchor.LowerRight;
                    default: return TextAnchor.MiddleCenter;
                }
            }
            set
            {
                switch (value)
                {
                    case TextAnchor.UpperLeft: base.alignment = TextAlignmentOptions.TopLeft; break;
                    case TextAnchor.UpperCenter: base.alignment = TextAlignmentOptions.Top; break;
                    case TextAnchor.UpperRight: base.alignment = TextAlignmentOptions.TopRight; break;
                    case TextAnchor.MiddleLeft: base.alignment = TextAlignmentOptions.Left; break;
                    case TextAnchor.MiddleRight: base.alignment = TextAlignmentOptions.Right; break;
                    case TextAnchor.LowerLeft: base.alignment = TextAlignmentOptions.BottomLeft; break;
                    case TextAnchor.LowerCenter: base.alignment = TextAlignmentOptions.Bottom; break;
                    case TextAnchor.LowerRight: base.alignment = TextAlignmentOptions.BottomRight; break;
                    default: base.alignment = TextAlignmentOptions.Center; break;
                }
            }
        }

        public new FontStyle fontStyle
        {
            get => (base.fontStyle & FontStyles.Bold) != 0
                ? ((base.fontStyle & FontStyles.Italic) != 0 ? FontStyle.BoldAndItalic : FontStyle.Bold)
                : ((base.fontStyle & FontStyles.Italic) != 0 ? FontStyle.Italic : FontStyle.Normal);
            set => base.fontStyle = value == FontStyle.Bold ? FontStyles.Bold
                : value == FontStyle.Italic ? FontStyles.Italic
                : value == FontStyle.BoldAndItalic ? FontStyles.Bold | FontStyles.Italic
                : FontStyles.Normal;
        }

        public HorizontalWrapMode horizontalOverflow
        {
            get => textWrappingMode == TextWrappingModes.NoWrap ? HorizontalWrapMode.Overflow : HorizontalWrapMode.Wrap;
            set => textWrappingMode = value == HorizontalWrapMode.Wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        }

        public VerticalWrapMode verticalOverflow
        {
            get => overflowMode == TextOverflowModes.Overflow ? VerticalWrapMode.Overflow : VerticalWrapMode.Truncate;
            set => overflowMode = value == VerticalWrapMode.Overflow ? TextOverflowModes.Overflow : TextOverflowModes.Truncate;
        }

        public bool resizeTextForBestFit { get => enableAutoSizing; set => enableAutoSizing = value; }
        public int resizeTextMinSize { get => Mathf.RoundToInt(fontSizeMin); set => fontSizeMin = value; }
        public int resizeTextMaxSize { get => Mathf.RoundToInt(fontSizeMax); set => fontSizeMax = value; }
    }
}
