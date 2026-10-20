using HarvestPanic.Core;
using UnityEngine;

namespace HarvestPanic.Economy
{
    /// <summary>
    /// One farm upgrade (farm-upgrades.md §3 catalog). Data-only: price, tier, prerequisite and the permanent
    /// <see cref="DayModifier"/> (Category Upgrade, Duration Run) it applies while owned.
    /// Assets live in <c>Resources/Data/Upgrades</c>; their modifiers in <c>Resources/Data/DayModifiers</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "HarvestPanic/Farm Upgrade", fileName = "Upgrade_New")]
    public sealed class UpgradeDefinition : ScriptableObject, IIdentified
    {
        public const string ResourceFolder = "Data/Upgrades";

        [Tooltip("Stable id, e.g. Upgrade.BridgeReinforcement.1")] public string UpgradeId = "Upgrade.New";
        public string DisplayName = "New Upgrade";
        [TextArea] public string Description;
        [Min(0)] public int Price = 100;
        [Tooltip("Week tier for offer weighting (farm-upgrades §4)"), Min(1)] public int Tier = 1;
        [Tooltip("Must own this upgrade id first (Lv2 needs Lv1). Empty = none.")] public string RequiresId;
        [Tooltip("Shown in every shop until bought (farm-upgrades §3 rule 1).")] public bool Staple;
        [Tooltip("Permanent modifier applied while owned.")] public DayModifier Modifier;
        [Tooltip("Comedy ledger line printed on purchase.")] public string FlavorLine;

        public string Id => UpgradeId;
    }

    /// <summary>Upgrade catalog lookup.</summary>
    public static class UpgradeCatalog
    {
        public static readonly ContentCatalog<UpgradeDefinition> All = new ContentCatalog<UpgradeDefinition>(UpgradeDefinition.ResourceFolder);
    }
}
