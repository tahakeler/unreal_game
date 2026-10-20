using System;
using HarvestPanic.Core;
using Unity.Netcode;

namespace HarvestPanic.Economy
{
    /// <summary>Small NGO helpers shared by the economy runtime.</summary>
    internal static class NetUtil
    {
        /// <summary>True when a NetworkManager is running as host/server/client.</summary>
        public static bool IsListening => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    }

    /// <summary>
    /// Resolves marker keys to goods definitions: exact id ("Livestock.Pig") or last segment ("Pig", case-insensitive).
    /// </summary>
    public static class GoodsResolver
    {
        /// <summary>Legacy/alternate ids (World or design notes) mapped to canonical ids.</summary>
        static readonly System.Collections.Generic.Dictionary<string, string> Aliases = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Product.EggCrate", "Product.Crate" }, { "EggCrate", "Product.Crate" }, { "Crate", "Product.Crate" },
            { "PrizePumpkin", "Crop.PrizePumpkin" }, { "Pumpkin", "Crop.PrizePumpkin" },
        };

        public static bool TryResolve(string key, out GoodsDefinition def)
        {
            def = null;
            if (string.IsNullOrEmpty(key)) return false;
            if (Content.Goods.TryGet(key, out def)) return true;
            if (Aliases.TryGetValue(key, out var alias) && Content.Goods.TryGet(alias, out def)) return true;
            foreach (var g in Content.Goods.All)
            {
                string id = g.GoodsId;
                int dot = id.LastIndexOf('.');
                if (string.Equals(dot >= 0 ? id.Substring(dot + 1) : id, key, StringComparison.OrdinalIgnoreCase)) { def = g; return true; }
            }
            return false;
        }
    }
}
