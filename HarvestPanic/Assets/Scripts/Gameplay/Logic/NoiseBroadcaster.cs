using System;
using System.Collections.Generic;
using HarvestPanic.Core;
using UnityEngine;

namespace HarvestPanic.Gameplay
{
    /// <summary>
    /// Engine-free core of the Noise Event contract (design/gdd/proximity-voice.md): radius formula, per
    /// instigator/source rate limit (louder replaces quieter inside the window) and the global events/s cap.
    /// <see cref="NoiseService"/> wraps it with server checks and day modifiers.
    /// </summary>
    public sealed class NoiseBroadcaster
    {
        readonly float _radiusMin, _radiusMax, _exponent, _rateWindow;
        readonly int _globalCap;
        readonly Dictionary<(ulong, NoiseSource), (double time, float loudness)> _last = new Dictionary<(ulong, NoiseSource), (double, float)>();
        readonly Queue<double> _recent = new Queue<double>();

        /// <summary>Raised for every accepted event.</summary>
        public event Action<NoiseEvent> OnNoise;

        /// <summary>Creates a broadcaster with contract defaults unless overridden.</summary>
        public NoiseBroadcaster(float radiusMin = 4f, float radiusMax = 50f, float exponent = 1.5f, float rateWindow = 0.25f, int globalCap = 60)
        {
            _radiusMin = radiusMin; _radiusMax = radiusMax; _exponent = exponent; _rateWindow = rateWindow; _globalCap = globalCap;
        }

        /// <summary>Contract radius: <c>Min + (Max - Min) * loudness^Exponent</c> (4 + 46 * L^1.5 by default).</summary>
        public static float RadiusFor(float loudness, float min = 4f, float max = 50f, float exponent = 1.5f) =>
            min + (max - min) * Mathf.Pow(Mathf.Clamp01(loudness), exponent);

        /// <summary>Instance radius using this broadcaster's tuning.</summary>
        public float Radius(float loudness) => RadiusFor(loudness, _radiusMin, _radiusMax, _exponent);

        /// <summary>
        /// Validates and raises an event. <paramref name="radiusMult"/> carries day-modifier scaling.
        /// Returns false when rate limited or capped.
        /// </summary>
        public bool TryEmit(NoiseSource source, float loudness, Vector3 location, ulong instigatorId, ulong blamedPlayerId,
            NoiseTag tags, double now, float radiusMult, out NoiseEvent evt)
        {
            evt = default;
            loudness = Mathf.Clamp01(loudness);
            if (loudness <= 0f) return false;

            var key = (instigatorId, source);
            if (_last.TryGetValue(key, out var prev) && now - prev.time < _rateWindow && loudness <= prev.loudness)
                return false;

            while (_recent.Count > 0 && now - _recent.Peek() >= 1.0) _recent.Dequeue();
            if (_recent.Count >= _globalCap) return false;

            _recent.Enqueue(now);
            _last[key] = (now, loudness);
            evt = new NoiseEvent
            {
                EventId = Guid.NewGuid(),
                Source = source,
                Loudness = loudness,
                Radius = Radius(loudness) * Mathf.Max(0f, radiusMult),
                Location = location,
                InstigatorId = instigatorId,
                BlamedPlayerId = blamedPlayerId,
                Tags = tags,
                ServerTime = now,
            };
            OnNoise?.Invoke(evt);
            return true;
        }
    }
}
