using UnityEngine;

namespace HarvestPanic.Customization
{
    /// <summary>
    /// One authored catalogue item (Assets/Art/Cosmetics/Items/&lt;Slot&gt;/&lt;key&gt;.asset), listed in
    /// <see cref="CosmeticCatalog.entries"/>. Same slot + id as a built-in placeholder = replaces it (the Tripo Straw Hat
    /// replaces the primitive one); a new id adds an item. Created by HarvestPanic/Cosmetics/Import Tripo Cosmetic or by hand
    /// (Create > HarvestPanic > Cosmetic Entry). Ids are part of the save and the network loadout: never reuse one.
    /// </summary>
    [CreateAssetMenu(menuName = "HarvestPanic/Cosmetic Entry", fileName = "CosmeticEntry")]
    public sealed class CosmeticEntry : ScriptableObject
    {
        /// <summary>Slot the item goes in.</summary>
        public CosmeticSlot slot;
        /// <summary>The item (prefab, icon, unlock rule, tint mode, placement).</summary>
        public CosmeticItem item = new CosmeticItem();
    }
}
