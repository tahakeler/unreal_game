using UnityEngine;

namespace HarvestPanic.AI
{
    /// <summary>
    /// Data for the livestock signature tasks (livestock-roster §3.1): cow moo/stubbornness and goat
    /// headbutt/climb/graze. Personality fields stay in <see cref="HarvestPanic.Core.CritterPersonality"/>; this asset holds
    /// the extra knobs Core does not have. Authored at <c>Resources/Data/Critters/LivestockTuning.asset</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "HarvestPanic/Livestock Tuning", fileName = "LivestockTuning")]
    public sealed class LivestockTuning : ScriptableObject
    {
        public const string ResourcePath = "Data/Critters/LivestockTuning";

        [Header("Cow (Big Martha)")]
        [Tooltip("Seconds between moos (roster: one per 20 s).")] [Min(1)] public float MooInterval = 20f;
        [Tooltip("Random +/- jitter on the moo interval (s).")] [Min(0)] public float MooJitter = 4f;
        [Tooltip("Moo noise loudness (roster 0.5).")] [Range(0, 1)] public float MooLoudness = 0.5f;
        [Tooltip("Extra moo when grabbed (complaint).")] public bool MooOnGrab = true;

        [Header("Goat (Gary) - headbutt")]
        [Tooltip("Max distance to start a headbutt wind-up (m).")] [Min(0.3f)] public float HeadbuttRange = 1.8f;
        [Tooltip("Half-angle of the front cone a target must be in (deg).")] [Range(10, 180)] public float HeadbuttConeDeg = 70f;
        [Tooltip("Readable telegraph (head lowered, hoof scrape) before the hit (s).")] [Min(0.1f)] public float HeadbuttTelegraph = 0.6f;
        [Tooltip("Reach checked at the end of the telegraph; sidestep beyond this to dodge (m).")] [Min(0.3f)] public float HeadbuttHitReach = 2.2f;
        [Tooltip("Design-unit impulse on the player (ragdoll 900, KO 1800). 1100 = knockdown, never KO.")] [Min(0)] public float HeadbuttImpulse = 1100f;
        [Tooltip("Upward share of the hit direction.")] [Range(0, 1)] public float HeadbuttLift = 0.35f;
        [Tooltip("Cooldown after a headbutt attempt (s).")] [Min(0)] public float HeadbuttCooldown = 7f;
        [Tooltip("Chance per check to headbutt a valid target (curious, not certain).")] [Range(0, 1)] public float HeadbuttChance = 0.5f;

        [Header("Goat - climb")]
        [Tooltip("Search radius for something to climb (m).")] [Min(0)] public float ClimbSearchRadius = 5f;
        [Min(0)] public float ClimbMinHeight = 0.5f;
        [Min(0)] public float ClimbMaxHeight = 2.2f;
        [Tooltip("Chance per idle check to try climbing.")] [Range(0, 1)] public float ClimbChance = 0.25f;
        [Tooltip("Hop duration (s).")] [Min(0.1f)] public float HopSeconds = 0.55f;
        [Tooltip("Perch time before getting bored and jumping down (roster: 45 s).")] [Min(1)] public float PerchBoredSeconds = 45f;
        [Tooltip("Min perch time before a threat makes it jump down (s).")] [Min(0)] public float PerchMinSeconds = 4f;

        [Header("Goat - graze")]
        [Tooltip("Radius to look for loose crops/goods to eat (m).")] [Min(0)] public float GrazeSearchRadius = 8f;
        [Range(0, 1)] public float GrazeAffinity = 0.7f;
        [Tooltip("Distance at which it bites (m).")] [Min(0.3f)] public float BiteRange = 1.3f;
        [Min(0.1f)] public float BiteInterval = 1.5f;
        [Tooltip("Design impulse fed to IGoods.ApplyBruise per bite (bruise = (I-400)/40 x fragility).")] [Min(0)] public float BiteImpulse = 1400f;

        [Header("Sheep (flock)")]
        [Tooltip("Seconds between idle baas.")] [Min(1)] public float BaaInterval = 14f;
        [Tooltip("Random +/- jitter on the baa interval (s).")] [Min(0)] public float BaaJitter = 5f;
        [Tooltip("Idle baa loudness (Critter source).")] [Range(0, 1)] public float BaaLoudness = 0.35f;
        [Tooltip("Panic baa loudness when the flock stampedes.")] [Range(0, 1)] public float PanicBaaLoudness = 0.55f;
        [Tooltip("Min seconds between baas from one sheep.")] [Min(0)] public float BaaCooldown = 2.5f;

        [Header("Shared")]
        [Tooltip("Signature task check rate (Hz).")] [Range(1, 20)] public float CheckHz = 4f;

        static LivestockTuning _default;
        public static LivestockTuning Default
        {
            get
            {
                if (_default != null) return _default;
                _default = Resources.Load<LivestockTuning>(ResourcePath);
                if (_default == null) { _default = CreateInstance<LivestockTuning>(); _default.hideFlags = HideFlags.DontSave; }
                return _default;
            }
        }
    }
}
