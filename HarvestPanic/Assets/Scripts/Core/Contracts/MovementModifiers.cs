using System.Collections.Generic;
using UnityEngine;

namespace HarvestPanic.Core
{
    /// <summary>A world zone (mud, ice...) that scales player movement speed at a position. Implemented by World.</summary>
    public interface IMovementModifierSource
    {
        /// <summary>Speed multiplier at <paramref name="position"/>; 1 means no effect.</summary>
        float GetSpeedMultiplier(Vector3 position);
    }

    /// <summary>
    /// Registry the player controller queries each frame. World zones register in OnEnable and unregister in
    /// OnDisable. Main thread only; cleared on entering Play mode.
    /// </summary>
    public static class MovementModifiers
    {
        static readonly List<IMovementModifierSource> Sources = new List<IMovementModifierSource>();

        /// <summary>Adds a source (ignored if already registered).</summary>
        public static void Register(IMovementModifierSource source)
        {
            if (source != null && !Sources.Contains(source)) Sources.Add(source);
        }

        /// <summary>Removes a source.</summary>
        public static void Unregister(IMovementModifierSource source) => Sources.Remove(source);

        /// <summary>Product of every source's multiplier at <paramref name="position"/> (1 when none).</summary>
        public static float Evaluate(Vector3 position)
        {
            float m = 1f;
            for (int i = 0; i < Sources.Count; i++) m *= Sources[i].GetSpeedMultiplier(position);
            return m;
        }

        /// <summary>Drops all sources (tests, session end).</summary>
        public static void Clear() => Sources.Clear();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => Clear();
    }
}
