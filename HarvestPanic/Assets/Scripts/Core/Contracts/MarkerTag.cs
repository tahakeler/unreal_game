using System;
using System.Globalization;

namespace HarvestPanic.Core
{
    /// <summary>
    /// Roles of level marker empties. The World team places a <see cref="LevelMarker"/> with one of these roles;
    /// the system that owns the role spawns or wires its content at runtime (see unity-contracts.md).
    /// </summary>
    public enum MarkerRole
    {
        PlayerStart, MarketPen, CritterSpawner, Goods, BridgePlank, River, RiverPile, BarnRespawn, Wardrobe
    }

    /// <summary>
    /// Unity tag names for each <see cref="MarkerRole"/> (all defined in ProjectSettings/TagManager.asset)
    /// plus the legacy "HP_CritterSpawner_Pig:3" string format used by the old level notes.
    /// </summary>
    public static class MarkerTag
    {
        /// <summary>Tag for <see cref="MarkerRole.PlayerStart"/>.</summary>
        public const string PlayerStart = "HP_PlayerStart";
        /// <summary>Tag for <see cref="MarkerRole.MarketPen"/>.</summary>
        public const string MarketPen = "HP_MarketPen";
        /// <summary>Tag for <see cref="MarkerRole.CritterSpawner"/>.</summary>
        public const string CritterSpawner = "HP_CritterSpawner";
        /// <summary>Tag for <see cref="MarkerRole.Goods"/>.</summary>
        public const string Goods = "HP_Goods";
        /// <summary>Tag for <see cref="MarkerRole.BridgePlank"/>.</summary>
        public const string BridgePlank = "HP_BridgePlank";
        /// <summary>Tag for <see cref="MarkerRole.River"/>.</summary>
        public const string River = "HP_River";
        /// <summary>Tag for <see cref="MarkerRole.RiverPile"/>.</summary>
        public const string RiverPile = "HP_RiverPile";
        /// <summary>Tag for <see cref="MarkerRole.BarnRespawn"/>.</summary>
        public const string BarnRespawn = "HP_BarnRespawn";
        /// <summary>Tag for <see cref="MarkerRole.Wardrobe"/>.</summary>
        public const string Wardrobe = "HP_Wardrobe";

        /// <summary>Returns the Unity tag for <paramref name="role"/>.</summary>
        public static string ForRole(MarkerRole role)
        {
            switch (role)
            {
                case MarkerRole.PlayerStart: return PlayerStart;
                case MarkerRole.MarketPen: return MarketPen;
                case MarkerRole.CritterSpawner: return CritterSpawner;
                case MarkerRole.Goods: return Goods;
                case MarkerRole.BridgePlank: return BridgePlank;
                case MarkerRole.River: return River;
                case MarkerRole.RiverPile: return RiverPile;
                case MarkerRole.BarnRespawn: return BarnRespawn;
                case MarkerRole.Wardrobe: return Wardrobe;
                default: throw new ArgumentOutOfRangeException(nameof(role));
            }
        }

        /// <summary>Formats the legacy spawner string, e.g. <c>HP_CritterSpawner_Pig:3</c>.</summary>
        public static string FormatSpawner(string species, int count) => $"{CritterSpawner}_{species}:{count}";

        /// <summary>Parses <c>HP_CritterSpawner_Pig:3</c> into species and count (count defaults to 1 when omitted).</summary>
        public static bool TryParseSpawner(string tag, out string species, out int count)
        {
            species = null;
            count = 0;
            string prefix = CritterSpawner + "_";
            if (tag == null || !tag.StartsWith(prefix, StringComparison.Ordinal)) return false;
            string body = tag.Substring(prefix.Length);
            int colon = body.IndexOf(':');
            species = colon < 0 ? body : body.Substring(0, colon);
            count = 1;
            if (colon >= 0 && !int.TryParse(body.Substring(colon + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out count))
                return false;
            return species.Length > 0 && count > 0;
        }
    }
}
