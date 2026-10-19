using UnityEngine;

namespace HarvestPanic.Core
{
    /// <summary>
    /// Anything a player can pick up. Implemented by carryable prefabs (goods, critters, tools);
    /// consumed by the player carry system. Player ids are NGO client ids (see <see cref="PlayerIds"/>).
    /// State-changing calls are made on the server; implementers replicate the result.
    /// </summary>
    public interface ICarryable
    {
        /// <summary>The carried object's transform.</summary>
        Transform CarryTransform { get; }

        /// <summary>Hands needed to lift (1, 2, 4, 6, 8; each player supplies 2). Below this it is dragged.</summary>
        int HandsRequired { get; }

        /// <summary>Mass in kilograms; drives carry speed and throw speed.</summary>
        float MassKg { get; }

        /// <summary>True for tools/light props that attach rigidly (no spring-damped wobble, no bruising).</summary>
        bool IsKinematicCarry { get; }

        /// <summary>A player started holding the object (first holder or an additional one).</summary>
        void OnGrabbed(ulong clientId);

        /// <summary>A player let go (release, slip, ragdoll or disconnect).</summary>
        void OnReleased(ulong clientId);

        /// <summary>Launches the object with <paramref name="velocity"/> (m/s); <paramref name="thrower"/> is blamed for impacts.</summary>
        void ApplyThrow(Vector3 velocity, ulong thrower);
    }
}
