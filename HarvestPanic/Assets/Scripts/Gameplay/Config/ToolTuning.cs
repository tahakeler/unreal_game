using UnityEngine;

namespace HarvestPanic.Gameplay
{
    /// <summary>Farm-tool knobs (design/gdd/farm-tools.md section 7). Asset: <c>Prefabs/Tools/ToolTuning.asset</c>.</summary>
    [CreateAssetMenu(menuName = "HarvestPanic/Player/Tool Tuning", fileName = "ToolTuning")]
    public sealed class ToolTuning : ScriptableObject
    {
        [Header("Net")]
        public float NetWindUp = 0.5f;
        public float NetCooldown = 1.0f;
        public float NetReach = 1.5f;
        [Tooltip("Full arc in degrees")] public float NetArc = 90f;
        [Tooltip("Items with HandsRequired <= this can be scooped")] public int NetMaxHands = 1;
        public int NetCapacity = 1;
        [Tooltip("Escape chance per EscapeInterval for living catches")] [Range(0, 1)] public float NetEscapeChance = 0.2f;
        public float NetEscapeInterval = 5f;
        [Tooltip("Catch chance for living critters: clamp(0.95 - 0.1 * agility, 0.4, 0.95); agility unknown through contracts so this default is used")]
        public float NetDefaultAgility = 0f;

        [Header("Feed bucket")]
        public float FeedLoudness = 0.4f;
        [Tooltip("While Use is held a shake noise is emitted every N seconds")] public float FeedShakeInterval = 0.5f;
        public int FeedUses = 10;
        [Tooltip("Refills when within this distance of a BarnRespawn marker (m)")] public float FeedRefillRadius = 6f;
    }
}
