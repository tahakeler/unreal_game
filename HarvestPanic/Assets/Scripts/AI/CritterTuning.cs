using UnityEngine;

namespace HarvestPanic.AI
{
    /// <summary>
    /// Global Critter AI knobs (critter-ai §4 formulas and §7 tuning). Species values live in
    /// <see cref="HarvestPanic.Core.CritterPersonality"/>; this asset holds the shared constants.
    /// Authored at <c>Assets/Resources/Data/Critters/CritterTuning.asset</c>; code falls back to these defaults.
    /// </summary>
    [CreateAssetMenu(menuName = "HarvestPanic/Critter Tuning", fileName = "CritterTuning")]
    public sealed class CritterTuning : ScriptableObject
    {
        /// <summary>Resources path of the shared asset.</summary>
        public const string ResourcePath = "Data/Critters/CritterTuning";

        [Header("Tick")]
        [Tooltip("Brain/perception tick rate (Hz). GDD 10, safe 5-20.")] [Range(5, 20)] public float TickHz = 10f;
        [Tooltip("Global speed multiplier (difficulty). Safe 0.8-1.2.")] [Range(0.5f, 1.5f)] public float GlobalSpeedMult = 1f;
        [Tooltip("Global noise multiplier. Safe 0.5-1.5.")] [Range(0.5f, 1.5f)] public float GlobalNoiseMult = 1f;

        [Header("Threat formula")]
        [Range(0, 1)] public float WeightProximity = 0.5f;
        [Range(0, 1)] public float WeightSprint = 0.2f;
        [Range(0, 1)] public float WeightSight = 0.15f;
        [Tooltip("Share of the non-max sources added to total threat.")] [Range(0, 1)] public float OtherSourcesWeight = 0.25f;
        [Tooltip("Noise threat decay per second.")] [Min(0)] public float NoiseDecayPerSecond = 0.25f;
        [Tooltip("Damage threat added on a hit/throw.")] [Range(0, 1)] public float DamageThreat = 0.6f;
        [Min(0)] public float DamageDecayPerSecond = 0.3f;
        [Tooltip("Player horizontal speed counted as sprinting (walk 4.0, sprint 6.5 m/s).")] [Min(0)] public float SprintSpeedThreshold = 5.2f;
        [Tooltip("Max tracked threat sources per critter.")] [Range(4, 32)] public int MaxThreatSources = 16;

        [Header("Alert")]
        [Tooltip("Readable tell before fleeing (s). Safe 0.2-0.8.")] [Range(0.1f, 1f)] public float AlertTellSeconds = 0.4f;
        [Tooltip("Time below AlertThreshold before Alert returns to Idle (s).")] [Min(0)] public float AlertDecaySeconds = 3f;

        [Header("Flee")]
        [Tooltip("Min seconds between flee path queries. Safe 0.1-0.5.")] [Range(0.05f, 1f)] public float FleeQueryInterval = 0.25f;
        [Tooltip("Distance of sampled flee points (m).")] [Min(1)] public float FleeDistance = 12f;
        [Tooltip("NavMesh.SamplePosition radius for flee points (m).")] [Min(0.1f)] public float FleeSampleRadius = 3f;
        [Tooltip("Cornered when best flee path length exceeds this many times the straight distance.")] [Min(1)] public float CorneredCostRatio = 3f;
        [Min(0)] public float CorneredPlayerRadius = 4f;
        [Min(1)] public int CorneredPlayersNeeded = 2;
        [Min(0)] public float CorneredMinSeconds = 2f;
        [Min(0)] public float CorneredMaxSeconds = 5f;
        [Tooltip("Juke dash speed as a multiple of SprintSpeed.")] [Min(1)] public float JukeSpeedMult = 1.15f;

        [Header("Lure")]
        [Tooltip("Lure range in metres (formula: affinity * (1 - d/range)).")] [Min(1)] public float LureRange = 15f;
        [Tooltip("Lure wins if attraction > threat * this.")] [Range(0, 2)] public float LureThreatFactor = 0.8f;
        [Tooltip("While lured, threat below this is ignored.")] [Range(0, 1)] public float LuredIgnoreBelow = 0.5f;
        [Tooltip("Threat spike that breaks a lure straight into Flee.")] [Range(0, 1)] public float LuredBreakAbove = 0.8f;
        [Tooltip("How long a food noise stays an active lure (s).")] [Min(0)] public float LureMemorySeconds = 8f;
        [Min(0.1f)] public float EatDistance = 1.5f;

        [Header("Idle / Wander")]
        [Min(0)] public float WanderRadius = 6f;
        [Min(0)] public float IdleMinSeconds = 2f;
        [Min(0)] public float IdleMaxSeconds = 5f;
        [Tooltip("Wander speed as a fraction of MoveSpeed.")] [Range(0.1f, 1f)] public float WanderSpeedFraction = 0.5f;
        [Tooltip("No path progress for this long = stuck, re-query.")] [Min(0.5f)] public float StuckSeconds = 3f;
        [Tooltip("Stuck this long = warp to a nearby NavMesh point. Safe 5-15.")] [Min(1)] public float StuckWarpSeconds = 8f;

        [Header("Carry / Stun")]
        [Tooltip("Break-free chance added per missing secure carrier (resolves the GDD 3-5 s mean).")] [Range(0, 1)] public float BreakFreePerMissingCarrier = 0.2f;
        [Tooltip("Base break-free chance when at least one carrier is missing.")] [Range(0, 1)] public float BreakFreeBase = 0.05f;
        [Range(0, 1)] public float BreakFreeMax = 0.9f;
        [Tooltip("Hop speed when a critter wriggles out of a grip (m/s).")] [Min(0)] public float BreakFreeHopSpeed = 3f;
        [Min(0)] public float StunSeconds = 1.5f;
        [Min(0)] public float DropStunSeconds = 0.6f;
        [Min(0)] public float BreakFreeStunSeconds = 0.25f;
        [Tooltip("Max airborne time before the stun timer starts anyway (s).")] [Min(0)] public float MaxAirSeconds = 3f;
        [Tooltip("Squeal loudness when a Screams critter is grabbed (livestock-roster: 0.7).")] [Range(0, 1)] public float GrabSquealLoudness = 0.7f;
        [Tooltip("Unity N*s to contract impulse units for bruising (Economy to confirm).")] [Min(0)] public float ImpactImpulseScale = 1f;

        [Header("Flock (chickens)")]
        [Range(0, 90)] public float FlockScatterJitterDegrees = 50f;
        [Tooltip("Weight of 'away from flock centre' in a scatter.")] [Min(0)] public float FlockOutwardWeight = 1.2f;
        [Tooltip("Seconds after a scatter before the flock regroups (livestock-roster 15, safe 8-25).")] [Min(0)] public float FlockRegroupSeconds = 15f;
        [Tooltip("Members within this distance of the flock centre count as regrouped (m).")] [Min(0)] public float FlockRegroupRadius = 3f;
        [Tooltip("Max distance to auto-join an existing flock (m).")] [Min(0)] public float FlockJoinRadius = 12f;

        static CritterTuning _default;

        /// <summary>The shared asset, or a defaults instance when none is authored.</summary>
        public static CritterTuning Default
        {
            get
            {
                if (_default != null) return _default;
                _default = Resources.Load<CritterTuning>(ResourcePath);
                if (_default == null) { _default = CreateInstance<CritterTuning>(); _default.hideFlags = HideFlags.DontSave; }
                return _default;
            }
        }
    }
}
