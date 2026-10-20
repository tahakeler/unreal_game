using System.Collections.Generic;

namespace HarvestPanic.Gameplay
{
    /// <summary>One prompt action: a key cap (may be empty) and its label.</summary>
    public struct PromptAction
    {
        /// <summary>Key text, e.g. "E", "LMB", "Hold E". Empty for plain status text.</summary>
        public string Key;
        /// <summary>Action label, e.g. "Grab (0/2 hands)".</summary>
        public string Label;
    }

    /// <summary>
    /// Splits <see cref="PlayerCarry.Prompt"/> ("E: Drop  LMB: Throw  [40%]") into key-cap chips for the HUD.
    /// Segments are separated by two spaces; "KEY: label" becomes a key cap, anything else plain text.
    /// </summary>
    public static class PromptFormat
    {
        /// <summary>Parses <paramref name="prompt"/> into <paramref name="into"/> (cleared first).</summary>
        public static void Parse(string prompt, List<PromptAction> into)
        {
            into.Clear();
            if (string.IsNullOrWhiteSpace(prompt)) return;
            foreach (var raw in prompt.Split(new[] { "  " }, System.StringSplitOptions.RemoveEmptyEntries))
            {
                string seg = raw.Trim();
                if (seg.Length == 0) continue;
                int colon = seg.IndexOf(": ", System.StringComparison.Ordinal);
                // A key is short (E, LMB, SPACE, Hold E); longer prefixes are plain sentences.
                if (colon > 0 && colon <= 8) into.Add(new PromptAction { Key = seg.Substring(0, colon), Label = seg.Substring(colon + 2) });
                else into.Add(new PromptAction { Key = string.Empty, Label = seg });
            }
        }
    }
}
