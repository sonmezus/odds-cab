using UnityEngine;
using OddsCab.Puzzles;

namespace OddsCab
{
    /// <summary>1080-wide design units: 144 units become 48 logical pixels on a 360-wide phone.</summary>
    public static class MobileUiLayout
    {
        public const float PanelWidth = 1016f;
        public const float TapHeight = 144f;
        public const float DenseTapSize = 136f;
        public const float FooterCenter = 84f;
        public const float ChoiceHeight = 900f;
        public static Vector2 ButtonSize(Vector2 requested)
            => new Vector2(Mathf.Max(TapHeight, requested.x), Mathf.Max(TapHeight, requested.y));
        public static float CardHeight(PuzzleKind kind)
            => kind == PuzzleKind.Choice || kind == PuzzleKind.Bells || kind == PuzzleKind.Ordering ? ChoiceHeight
                : kind == PuzzleKind.Grid || kind == PuzzleKind.Akari || kind == PuzzleKind.ValveCause ? 1240f : 1160f;
        public static Rect PortraitFrame(float screenWidth, float screenHeight, Rect safe)
        {
            float width = Mathf.Max(1f, screenWidth), height = Mathf.Max(1f, screenHeight);
            float left = Mathf.Clamp(safe.xMin, 0f, width), right = Mathf.Clamp(safe.xMax, left, width);
            float bottom = Mathf.Clamp(safe.yMin, 0f, height), top = Mathf.Clamp(safe.yMax, bottom, height);
            float safeWidth = Mathf.Max(1f, right - left), safeHeight = Mathf.Max(1f, top - bottom);
            float frameWidth = Mathf.Min(safeWidth, safeHeight * (9f / 16f));
            return new Rect(left + (safeWidth - frameWidth) * .5f, bottom, frameWidth, safeHeight);
        }
        public static Vector2 GridPitch(int columns, int rows)
            => new Vector2(Mathf.Min(184f, 920f / Mathf.Max(1, columns)), Mathf.Min(144f, 720f / Mathf.Max(1, rows)));
        public static float CanvasReferenceWidth(float screenWidth, float frameWidth)
            => 1080f * Mathf.Max(1f, screenWidth) / Mathf.Max(1f, frameWidth);
        public static Vector2 QualityButtonCenter(int index) => new Vector2(.14f + Mathf.Clamp(index, 0, 4) * .18f, .175f);
    }
}
