using System;
using UnityEngine;

namespace HarvestPanic.Core
{
    /// <summary>
    /// Optional companion to <see cref="ICarryable"/>: carryables that can fight back also implement this.
    /// The carry system subscribes when <c>carryable is ICarryableEvents</c>; carryables that do not implement
    /// it are unaffected. Kept separate so adding it never breaks existing <see cref="ICarryable"/> implementers.
    /// </summary>
    public interface ICarryableEvents
    {
        /// <summary>The carryable forces these holders (client ids) to let go, e.g. a critter breaks free.</summary>
        event Action<ulong[]> OnForcedRelease;

        /// <summary>Wriggle applied to holders: direction * magnitude in design units (see <see cref="ImpactUnits"/>).</summary>
        event Action<Vector3> OnStruggle;
    }
}
