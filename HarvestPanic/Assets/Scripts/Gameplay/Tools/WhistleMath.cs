using HarvestPanic.Core;
using UnityEngine;

namespace HarvestPanic.Gameplay
{
    /// <summary>Short blow scares, long blow (held for LongBlowSeconds) rallies (farm-tools rule 5).</summary>
    public enum WhistleBlow : byte { Short, Long }

    /// <summary>How one critter answers a whistle.</summary>
    public enum WhistleReaction : byte { None, Away, Toward }

    /// <summary>
    /// Pure whistle rules (design/gdd/farm-tools.md rule 5). The whistle emits a Tool noise; critters react through their
    /// own Critter Personality <c>NoiseTagResponses</c> (CritterBrain.HearNoise). The brain uses the FIRST matching tag, so
    /// a long blow carries only <see cref="NoiseTag.WhistleLong"/> (adding Whistle would make it a scare for everyone).
    /// </summary>
    public static class WhistleMath
    {
        /// <summary>Blow kind for how long Use was held.</summary>
        public static WhistleBlow Classify(float heldSeconds, float longBlowSeconds) =>
            heldSeconds >= longBlowSeconds ? WhistleBlow.Long : WhistleBlow.Short;

        /// <summary>Noise tags for a blow.</summary>
        public static NoiseTag TagsFor(WhistleBlow blow) => blow == WhistleBlow.Long ? NoiseTag.WhistleLong : NoiseTag.Whistle;

        /// <summary>
        /// Expected reaction (mirrors CritterBrain.HearNoise): Flee = away; Lure or Rally = toward; Ignore = none.
        /// No listed response means a plain loud noise, which scares on a short blow and is ignored on a long one.
        /// </summary>
        public static WhistleReaction ReactionFor(CritterPersonality p, WhistleBlow blow)
        {
            if (p == null) return WhistleReaction.None;
            if (p.TryGetNoiseResponse(TagsFor(blow), out var r))
            {
                switch (r.Reaction)
                {
                    case NoiseReaction.Flee: return WhistleReaction.Away;
                    case NoiseReaction.Lure:
                    case NoiseReaction.Rally: return WhistleReaction.Toward;
                    default: return WhistleReaction.None;
                }
            }
            return blow == WhistleBlow.Short ? WhistleReaction.Away : WhistleReaction.None;
        }

        /// <summary>Cooldown gate.</summary>
        public static bool Ready(float now, float readyAt) => now >= readyAt;

        /// <summary>True when <paramref name="distance"/> is inside the audible radius of the blow.</summary>
        public static bool InEarshot(float distance, float radius) => radius > 0f && distance <= radius;

        /// <summary>Scared carried critters wobble/struggle harder: <c>struggle * (1 + boost)</c> (+50% for 2 s).</summary>
        public static float BoostedStruggle(float struggle, float boost) => Mathf.Max(0f, struggle) * (1f + Mathf.Max(0f, boost));
    }
}
