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

        [Header("Lasso (farm-tools rule 3, section 4)")]
        public float LassoRange = 12f;
        [Tooltip("Rope length on a hit (m)")] public float LassoLength = 6f;
        [Tooltip("Rope length when tied to a post or wagon hitch (m)")] public float HitchLength = 3f;
        [Tooltip("Flying loop speed (m/s)")] public float LassoLoopSpeed = 24f;
        [Tooltip("Loop catch radius at the landing point (m)")] public float LassoCatchRadius = 0.9f;
        [Tooltip("Reel speed while Use is held (m/s)")] public float LassoReelSpeed = 2f;
        [Tooltip("Reeling under this distance auto-grabs the catch (m)")] public float LassoAutoGrab = 1.5f;
        public float PlayerAnchorBase = 600f;
        [Tooltip("Anchor multiplier at 0 stamina")] public float LassoExhaustedMult = 0.4f;
        [Tooltip("Stamina cost while pulling (per s)")] public float LassoStaminaPerSecond = 10f;
        [Tooltip("Rope breaking force (N)")] public float RopeBreakForce = 4000f;
        [Tooltip("Rope snaps when stretched this far past its length (m)")] public float RopeSnapSlack = 5f;
        [Tooltip("Dropping a tethered lasso within this distance of a tie point ties it (m)")] public float TieRadius = 2.5f;
        [Tooltip("Lasso targets critters of at least this size (2 = Medium)")] public HarvestPanic.Core.SizeClass LassoMinSize = HarvestPanic.Core.SizeClass.Medium;
        [Tooltip("Critter pull when no personality is found (N)")] public float DefaultCritterPull = 300f;
        [Tooltip("Ragdoll impulse (design units) when a critter out-pulls the rope holder (the water-ski)")] public float LassoDragImpulse = 950f;
        [Tooltip("Speed a yanked critter is dragged along the rope (m/s)")] public float LassoPullSpeed = 3.5f;
        [Tooltip("A critter held this far past the rope length by the NavMesh edge is yanked off its feet (m)")] public float LassoYankSlack = 0.75f;
        [Tooltip("How long a critter that out-pulls the holder drags them before the rope slips (s)")] public float LassoWaterSkiSeconds = 2.5f;
        [Tooltip("Rope crack on a catch: spooks the critter away from the thrower (Tool noise)")] public float LassoCatchLoudness = 0.45f;

        [Header("Whistle (farm-tools rule 5)")]
        public float WhistleLoudness = 0.7f;
        public float WhistleCooldown = 3f;
        [Tooltip("Holding Use this long makes a long (rally) blow (s)")] public float WhistleLongSeconds = 1f;
        [Tooltip("Carried critters in earshot: +50% struggle")] public float WhistleStruggleBoost = 0.5f;
        public float WhistleStruggleSeconds = 2f;

        [Header("Toolbelt")]
        [Tooltip("Equip swap time (s)")] public float EquipSeconds = 0.4f;
    }
}
