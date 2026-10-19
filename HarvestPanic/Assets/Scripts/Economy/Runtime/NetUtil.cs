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
        public static bool TryResolve(string key, out GoodsDefinition def)
        {
            def = null;
            if (string.IsNullOrEmpty(key)) return false;
            if (Content.Goods.TryGet(key, out def)) return true;
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
