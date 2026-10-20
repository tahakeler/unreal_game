using UnityEngine;

namespace HarvestPanic.UI
{
    /// <summary>Reserved meaning colours (art bible: gold money, tomato loss/blame, plum landlord, lime danger).</summary>
    public enum ReservedColor { Money, Loss, Landlord, Danger, Good }

    /// <summary>
    /// Colour-blind-safe substitutes for reserved colours. Shapes and labels always back colours up; these swaps keep
    /// every reserved pair distinguishable under the chosen deficiency (Okabe-Ito based).
    /// </summary>
    public static class UiPalette
    {
        static readonly Color[] Normal = { Hex(0xF2B72B), Hex(0xE5533D), Hex(0x7E4A8C), Hex(0xB4E04A), Hex(0x7FA34A) };
        // Deutan/Protan: avoid red/green; loss = vermilion-orange vs good = blue, danger = yellow.
        static readonly Color[] RedGreen = { Hex(0xE69F00), Hex(0xD55E00), Hex(0xCC79A7), Hex(0xF0E442), Hex(0x0072B2) };
        // Tritan: avoid blue/yellow; money = orange, danger = magenta-pink.
        static readonly Color[] Tritan = { Hex(0xE69F00), Hex(0xC0392B), Hex(0x4B4B9A), Hex(0xFF5FA2), Hex(0x009E73) };

        public static Color Get(ReservedColor c, ColorblindMode mode)
        {
            var table = mode == ColorblindMode.Off ? Normal : mode == ColorblindMode.Tritanopia ? Tritan : RedGreen;
            return table[(int)c];
        }

        /// <summary>USS root class for the mode ("" when off).</summary>
        public static string RootClass(ColorblindMode mode) => mode switch
        {
            ColorblindMode.Deuteranopia => "hp-root--cb-deutan",
            ColorblindMode.Protanopia => "hp-root--cb-protan",
            ColorblindMode.Tritanopia => "hp-root--cb-tritan",
            _ => "",
        };

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);
    }
}
