using System;
using UnityEngine;

namespace HarvestPanic.Core
{
    /// <summary>Origin class of a noise (proximity-voice contract).</summary>
    public enum NoiseSource
    {
        Voice, Shout, Footstep, Impact, Tool, Hazard, Critter, Crop, Vehicle, Scripted
    }

    /// <summary>Registered noise tags (flags so events stay allocation free).</summary>
    [Flags]
    public enum NoiseTag
    {
        None = 0, Scream = 1 << 0, Splash = 1 << 1, Metal = 1 << 2, Mandrake = 1 << 3, Food = 1 << 4,
        Whistle = 1 << 5, WhistleLong = 1 << 6, Squelch = 1 << 7, Creak = 1 << 8, PlankBreak = 1 << 9, Squeal = 1 << 10
    }

    /// <summary>
    /// Any sound in the world as an AI stimulus. Created only by the server through
    /// <see cref="INoiseService"/>; listeners decide their reaction from perceived strength and tags.
    /// </summary>
    [Serializable]
    public struct NoiseEvent
    {
        /// <summary>Unique id per event.</summary>
        public Guid EventId;
        /// <summary>What produced the sound.</summary>
        public NoiseSource Source;
        /// <summary>Normalised loudness 0..1.</summary>
        public float Loudness;
        /// <summary>Audible radius in metres (service-derived from loudness and day modifiers).</summary>
        public float Radius;
        /// <summary>World position.</summary>
        public Vector3 Location;
        /// <summary>NetworkObjectId of the noise maker (0 = none).</summary>
        public ulong InstigatorId;
        /// <summary>Player responsible for blame (<see cref="PlayerIds.None"/> if nobody).</summary>
        public ulong BlamedPlayerId;
        /// <summary>Tags describing the sound.</summary>
        public NoiseTag Tags;
        /// <summary>Server clock in seconds.</summary>
        public double ServerTime;

        /// <summary>Perceived strength at <paramref name="distance"/>: <c>Loudness * (1 - distance/Radius)</c>, 0 when out of range.</summary>
        public float PerceivedStrength(float distance, float hearingMult = 1f)
        {
            float r = Radius * hearingMult;
            return r <= 0f || distance > r ? 0f : Loudness * (1f - distance / r);
        }
    }

    /// <summary>
    /// Noise broadcast service (provider: Player/proximity-voice side; consumers: Critter AI, world, etc.).
    /// Register with <c>Services.Register&lt;INoiseService&gt;</c>.
    /// </summary>
    public interface INoiseService
    {
        /// <summary>Raised on the server for every accepted event.</summary>
        event Action<NoiseEvent> OnNoise;

        /// <summary>
        /// Server only. Fills radius, id and time, applies rate limits, raises <see cref="OnNoise"/>.
        /// Returns false when called on a client or rate-limited.
        /// </summary>
        bool Emit(NoiseSource source, float loudness, Vector3 location, ulong instigatorId, ulong blamedPlayerId, NoiseTag tags);
    }
}
