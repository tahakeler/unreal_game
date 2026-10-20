using System;
using UnityEngine;

namespace HarvestPanic.Core
{
    /// <summary>Grouping for UI and caps.</summary>
    public enum DayModifierCategory { Weather, Curse, Creature, Landlord, Region, Eviction, Upgrade }

    /// <summary>Major modifiers count toward the chaos cap.</summary>
    public enum DayModifierWeight { Minor, Major }

    /// <summary>How a re-applied modifier behaves.</summary>
    public enum DayModifierStackRule { Unique, Refresh, Stack, ReplaceGroup }

    /// <summary>How long a modifier lasts.</summary>
    public enum DayModifierDuration { Day, WorkdayOnly, DuskOnly, Seconds, Week, Run }

    /// <summary>Math applied to a day parameter.</summary>
    public enum DayParamOp { Add, Mul, Override }

    /// <summary>One parameter change.</summary>
    [Serializable]
    public struct DayParamEffect
    {
        /// <summary>Parameter name, see <see cref="DayParams"/>.</summary>
        public string ParamName;
        /// <summary>Operation.</summary>
        public DayParamOp Op;
        /// <summary>Operand.</summary>
        public float Value;
    }

    /// <summary>Well-known day parameter names (add-only; owners document new ones here).</summary>
    public static class DayParams
    {
        /// <summary>Noise radius multiplier (fog 0.8, storm 0.6, night 1.25).</summary>
        public const string NoiseEnvMult = "Noise.EnvMult";
        /// <summary>Extra multiplier for voice noise (Landlord quiet hours).</summary>
        public const string NoiseVoiceRadiusMult = "Noise.VoiceRadiusMult";
        /// <summary>Respawn fee multiplier.</summary>
        public const string EconomyRespawnFeeMult = "Economy.RespawnFeeMult";
        /// <summary>Multiplier on the load a bridge plank carries safely.</summary>
        public const string BridgeSafeLoadMult = "Bridge.SafeLoadMult";
        /// <summary>Multiplier on plank break chance.</summary>
        public const string BridgePlankBreakMult = "Bridge.PlankBreakMult";
        /// <summary>Player speed multiplier in mud.</summary>
        public const string MudSpeedMult = "Mud.SpeedMult";
        /// <summary>Multiplier on max stamina and regen (Grandpa's Boots upgrade). Consumer: Player.</summary>
        public const string PlayerStaminaMult = "Player.StaminaMult";
        /// <summary>Multiplier on net reach (Better Nets upgrade). Consumer: Player tools.</summary>
        public const string ToolsNetReachMult = "Tools.NetReachMult";
    }

    /// <summary>
    /// Day Modifier contract (day-loop). Weather, curses, landlord events and upgrades are authored as assets
    /// under <c>Assets/Resources/Data/DayModifiers/</c>; the day-loop service resolves them.
    /// </summary>
    [CreateAssetMenu(menuName = "HarvestPanic/Day Modifier", fileName = "Mod_New")]
    public sealed class DayModifier : ScriptableObject, IIdentified
    {
        [field: SerializeField, Tooltip("Unique id, e.g. Mod.Weather.Storm")] public string ModifierId { get; private set; } = "Mod.New";
        [field: SerializeField] public DayModifierCategory Category { get; private set; }
        [field: SerializeField] public DayModifierWeight Weight { get; private set; }
        [field: SerializeField] public DayModifierStackRule StackRule { get; private set; } = DayModifierStackRule.Unique;
        [field: SerializeField, Min(1)] public int MaxStacks { get; private set; } = 1;
        [field: SerializeField, Tooltip("Used by ReplaceGroup, e.g. Group.Weather")] public string GroupTag { get; private set; }
        [field: SerializeField] public DayModifierDuration Duration { get; private set; } = DayModifierDuration.Day;
        [field: SerializeField, Min(0), Tooltip("Used when Duration = Seconds")] public float DurationSeconds { get; private set; }
        [field: SerializeField, Tooltip("Higher wins for Override ops")] public int Priority { get; private set; }
        [field: SerializeField] public DayParamEffect[] Effects { get; private set; } = Array.Empty<DayParamEffect>();
        [field: SerializeField, Tooltip("World tags granted, e.g. World.BridgeOut")] public string[] GrantTags { get; private set; } = Array.Empty<string>();
        [field: SerializeField, TextArea] public string CardText { get; private set; }
        [field: SerializeField, Tooltip("False = secret modifier revealed by the ledger")] public bool Announced { get; private set; } = true;

        /// <summary>Catalog key (same as <see cref="ModifierId"/>).</summary>
        public string Id => ModifierId;
    }

    /// <summary>
    /// Read access to the resolved day state (provider: Economy/day-loop; consumers: AI, world, noise).
    /// </summary>
    public interface IDayModifierService
    {
        /// <summary>Current day within the run, 0-based.</summary>
        int DayIndex { get; }

        /// <summary>Applies every active modifier for <paramref name="paramName"/> to <paramref name="baseValue"/>.</summary>
        float Resolve(string paramName, float baseValue);

        /// <summary>True if any active modifier grants <paramref name="worldTag"/>.</summary>
        bool HasWorldTag(string worldTag);

        /// <summary>Raised when the active set changes.</summary>
        event Action OnModifiersChanged;
    }
}
