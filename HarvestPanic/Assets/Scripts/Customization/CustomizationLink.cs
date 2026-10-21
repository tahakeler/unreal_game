using UnityEngine;

namespace HarvestPanic.Customization
{
    /// <summary>
    /// Lets the main menu (no farmer spawned yet) find the cosmetics catalogue: Resources/Customization/CustomizationLink.asset
    /// (built by FarmerAssetBuilder) points at Art/Cosmetics/CosmeticCatalog.asset.
    /// </summary>
    public sealed class CustomizationLink : ScriptableObject
    {
        /// <summary>Resources path of the link asset.</summary>
        public const string ResourcePath = "Customization/CustomizationLink";

        /// <summary>The shipped catalogue.</summary>
        public CosmeticCatalog catalog;

        static CosmeticCatalog _cached;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() => _cached = null;

        /// <summary>The catalogue: the local farmer's, else the linked asset (null when neither exists).</summary>
        public static CosmeticCatalog Catalog
        {
            get
            {
                var local = FarmerLoadoutSync.Local;
                if (local != null && local.Visual.Catalog != null) return local.Visual.Catalog;
                if (_cached != null) return _cached;
                var link = Resources.Load<CustomizationLink>(ResourcePath);
                _cached = link != null ? link.catalog : null;
                return _cached;
            }
        }
    }
}
