using System;
using UnityEngine;

namespace HarvestPanic.Core
{
    /// <summary>Behaviour traits (critter-ai). Flags so a species can combine them.</summary>
    [Flags]
    public enum CritterTrait
    {
        None = 0, Skittish = 1, Greedy = 2, Aggressive = 4, Sneaky = 8, Herd = 16, Burrower = 32,
        Climber = 64, Swimmer = 128, Thief = 256, Loud = 512, Nocturnal = 1024, Stalker = 2048, Dramatic = 4096
    }

    /// <summary>What a cornered critter does.</summary>
    public enum CorneredResponse { Freeze, Charge, Juke, PlayDead, Burrow, Teleport }

    /// <summary>Physical size bracket (net captures work up to Small, lasso on Medium and up).</summary>
    public enum SizeClass { Tiny, Small, Medium, Large, Huge }

    /// <summary>Group behaviour.</summary>
    public enum HerdMode { None, Flock, Follow, Guard }

    /// <summary>When the critter is active.</summary>
    public enum ActivePhase { Day, Night, Both }

    /// <summary>How a critter reacts to a tagged noise.</summary>
    public enum NoiseReaction { Ignore, Lure, Flee, Rally }

    /// <summary>Lure categories.</summary>
    [Flags]
    public enum LureTag { None = 0, FeedBucket = 1, Whistle = 2, Shiny = 4, Light = 8 }

    /// <summary>Nav shortcuts a critter may use.</summary>
    [Flags]
    public enum NavShortcut { None = 0, UnderFence = 1, Hay = 2, Climb = 4, Water = 8, Underground = 16 }

    /// <summary>Comedy behaviours picked up by audio/camcorder.</summary>
    [Flags]
    public enum ComedyFlag { None = 0, Screams = 1, Faints = 2, Ragdolls = 4 }

    /// <summary>Per-noise-source loudness multiplier entry.</summary>
    [Serializable] public struct NoiseSourceMultiplier { public NoiseSource Source; public float Mult; }

    /// <summary>Reaction to a specific noise tag.</summary>
    [Serializable] public struct NoiseTagResponse { public NoiseTag Tag; public NoiseReaction Reaction; [Range(0, 1)] public float Strength; }

    /// <summary>Affinity for a lure type.</summary>
    [Serializable] public struct LureAffinityEntry { public LureTag Lure; [Range(0, 1)] public float Affinity; }

    /// <summary>Multipliers applied while the critter's goods item is Grumpy.</summary>
    [Serializable] public struct GrumpyMods { public float FleeSpeedMult; public float AggressionAdd; }

    /// <summary>Day-modifier scaling of one personality field (stacking is multiplicative then additive).</summary>
    [Serializable] public struct CritterModifier { public string FieldName; public float Mult; public float Add; }

    /// <summary>
    /// Critter Personality contract (critter-ai). Every species, cursed crop, night creature and rival is
    /// one asset under <c>Assets/Resources/Data/Critters/</c>; no species logic is hard-coded. Units are
    /// metres and seconds (the GDD's cm/s values are divided by 100). Treat instances as read-only at runtime.
    /// </summary>
    [CreateAssetMenu(menuName = "HarvestPanic/Critter Personality", fileName = "Critter_New")]
    public sealed class CritterPersonality : ScriptableObject, IIdentified
    {
        [field: Header("Identity"), SerializeField, Tooltip("e.g. Critter.Pig")]
        public string CritterId { get; private set; } = "Critter.New";
        [field: SerializeField] public CritterTrait Traits { get; private set; }
        [field: SerializeField, Tooltip("Prefab spawned for CritterSpawner markers")] public GameObject Prefab { get; private set; }
        [field: SerializeField, Tooltip("Goods this critter becomes when caught (may be empty for hostile creatures)")] public string GoodsId { get; private set; }

        [field: Header("Movement (m/s, m/s^2)"), SerializeField, Min(0)] public float MoveSpeed { get; private set; } = 3f;
        [field: SerializeField, Min(0)] public float SprintSpeed { get; private set; } = 5.5f;
        [field: SerializeField, Min(0)] public float Acceleration { get; private set; } = 15f;

        [field: Header("Senses"), SerializeField, Min(0), Tooltip("Threat detection radius (m)")] public float AlertRadius { get; private set; } = 10f;
        [field: SerializeField, Range(0, 1)] public float AlertThreshold { get; private set; } = 0.3f;
        [field: SerializeField, Range(0, 1)] public float FleeThreshold { get; private set; } = 0.55f;
        [field: SerializeField, Range(0, 1)] public float CalmThreshold { get; private set; } = 0.15f;
        [field: SerializeField, Min(0), Tooltip("Seconds to calm down")] public float CalmTime { get; private set; } = 4f;
        [field: SerializeField, Range(0, 2)] public float NoiseSensitivity { get; private set; } = 1f;
        [field: SerializeField] public NoiseSourceMultiplier[] NoiseSourceMult { get; private set; } = Array.Empty<NoiseSourceMultiplier>();
        [field: SerializeField] public NoiseTagResponse[] NoiseTagResponses { get; private set; } = Array.Empty<NoiseTagResponse>();
        [field: SerializeField, Range(0, 360), Tooltip("Degrees")] public float SightCone { get; private set; } = 220f;
        [field: SerializeField] public LureAffinityEntry[] LureAffinity { get; private set; } = Array.Empty<LureAffinityEntry>();
        [field: SerializeField, Min(0)] public float EatTime { get; private set; } = 3f;

        [field: Header("Conflict"), SerializeField] public CorneredResponse CorneredResponse { get; private set; } = CorneredResponse.Juke;
        [field: SerializeField, Min(0), Tooltip("N*s on a player hit by a Charge (player ragdoll threshold 900, KO 1800)")] public float ChargeImpulse { get; private set; }
        [field: SerializeField] public SizeClass SizeClass { get; private set; } = SizeClass.Medium;
        [field: SerializeField, Range(0, 5)] public float Agility { get; private set; } = 2f;
        [field: SerializeField, Min(0), Tooltip("Pull force when tethered/lassoed/hitched (N)")] public float CritterPullN { get; private set; } = 300f;
        [field: SerializeField, Range(1, 4), Tooltip("Players needed to hold it so it cannot break free")] public int SecureCarriers { get; private set; } = 1;
        [field: SerializeField, Min(0)] public float StruggleImpulse { get; private set; } = 120f;
        [field: SerializeField, Min(0)] public float StruggleIntervalMin { get; private set; } = 0.5f;
        [field: SerializeField, Min(0)] public float StruggleIntervalMax { get; private set; } = 1.5f;
        [field: SerializeField] public GrumpyMods GrumpyMods { get; private set; } = new GrumpyMods { FleeSpeedMult = 1.5f, AggressionAdd = 0.3f };
        [field: SerializeField, Range(0, 1)] public float PenEscapeChance { get; private set; } = 0.6f;

        [field: Header("Behaviour"), SerializeField] public HerdMode HerdMode { get; private set; }
        [field: SerializeField] public NavShortcut ShortcutAccess { get; private set; }
        [field: SerializeField] public ActivePhase ActivePhase { get; private set; } = ActivePhase.Day;
        [field: SerializeField, Range(0, 1)] public float Aggression { get; private set; }
        [field: SerializeField, Tooltip("Goods ids or tool names it may grab")] public string[] StealTargets { get; private set; } = Array.Empty<string>();
        [field: SerializeField, Range(0, 1)] public float Curiosity { get; private set; } = 0.2f;

        [field: Header("Presentation"), SerializeField] public AudioClip[] BarkSet { get; private set; } = Array.Empty<AudioClip>();
        [field: SerializeField] public ComedyFlag ComedyFlags { get; private set; }

        /// <summary>Catalog key (same as <see cref="CritterId"/>).</summary>
        public string Id => CritterId;

        /// <summary>Loudness multiplier for a noise source (1 when not listed).</summary>
        public float GetNoiseSourceMult(NoiseSource source)
        {
            foreach (var e in NoiseSourceMult) if (e.Source == source) return e.Mult;
            return 1f;
        }

        /// <summary>Finds the response to the first matching tag in <paramref name="tags"/>.</summary>
        public bool TryGetNoiseResponse(NoiseTag tags, out NoiseTagResponse response)
        {
            foreach (var e in NoiseTagResponses)
                if ((tags & e.Tag) != 0 && e.Tag != NoiseTag.None) { response = e; return true; }
            response = default;
            return false;
        }
    }
}
