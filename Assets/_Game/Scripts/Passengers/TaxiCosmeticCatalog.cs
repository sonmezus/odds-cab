using UnityEngine;

namespace OddsCab
{
    /// <summary>
    /// Saveable taxi paint shades sold in the garage. The model's paint is a baked yellow texture tinted by
    /// multiplication, so every shade is an honest warm variant of taxi yellow (a "blue" tint would come out olive).
    /// Prices climb so coins earned across the whole 512-stop journey stay worth something.
    /// </summary>
    public static class TaxiCosmeticCatalog
    {
        public const int Count = 8;

        static readonly string[] TurkishNames = { "Klasik Sarı", "Bal Sarısı", "Kehribar", "Hardal", "Bakır", "Kiremit", "Gece Sarısı", "Altın Boğaz" };
        static readonly string[] EnglishNames = { "Classic Yellow", "Honey", "Amber", "Mustard", "Copper", "Terracotta", "Night Yellow", "Bosphorus Gold" };
        static readonly int[] Costs = { 0, 150, 250, 400, 600, 900, 1300, 2000 };
        static readonly Color[] Tints =
        {
            Color.white,
            new Color(1.00f, 0.93f, 0.78f, 1f),
            new Color(1.00f, 0.80f, 0.58f, 1f),
            new Color(0.88f, 0.82f, 0.55f, 1f),
            new Color(1.00f, 0.66f, 0.50f, 1f),
            new Color(0.96f, 0.56f, 0.44f, 1f),
            new Color(0.62f, 0.60f, 0.62f, 1f),
            new Color(1.12f, 0.98f, 0.66f, 1f),
        };

        static int Clamp(int id) => Mathf.Clamp(id, 0, Count - 1);
        public static int Cost(int id) => Costs[Clamp(id)];
        public static string Name(int id) => Loc.Pick(TurkishNames[Clamp(id)], EnglishNames[Clamp(id)]);
        public static Color Tint(int id) => Tints[Clamp(id)];
        // What the paint actually looks like on the yellow taxi (base yellow × tint), for the shop swatch.
        public static Color Swatch(int id)
        {
            var baseYellow = new Color(0.96f, 0.74f, 0.16f, 1f);
            var t = Tint(id);
            return new Color(Mathf.Clamp01(baseYellow.r * t.r), Mathf.Clamp01(baseYellow.g * t.g), Mathf.Clamp01(baseYellow.b * t.b), 1f);
        }
    }
}
