using UnityEngine;

namespace HarvestPanic.Core
{
    /// <summary>
    /// A player body other systems can shove, knock out or send home (implemented on the player root by the
    /// Player team; callers: AI charges, hazards, wagons, night creatures). Server only.
    /// </summary>
    public interface IKnockable
    {
        /// <summary>True while ragdolled, fainted or waiting to respawn.</summary>
        bool IsDown { get; }

        /// <summary>Applies an impulse in design units (see <see cref="ImpactUnits"/>); the player decides ragdoll/KO from thresholds.</summary>
        void ApplyImpulse(Vector3 impulse, ulong instigator, BlameVerb verb);

        /// <summary>Teleports to the barn respawn; <paramref name="chargeFee"/> charges the respawn fee.</summary>
        void SendToBarn(BlameVerb reason, bool chargeFee);
    }

    /// <summary>
    /// Optional, add-only (polish-2): a downed body that can be dragged (e.g. by Mr. Stitches). Implemented by the
    /// Player root next to <see cref="IKnockable"/>; callers check <c>knockable is IDraggable</c>. Server only; the
    /// implementation forwards to the owning client, which simulates the ragdoll.
    /// </summary>
    public interface IDraggable
    {
        /// <summary>Server: pull the body toward <paramref name="toward"/> (world point) for <paramref name="seconds"/>. Ignored unless down.</summary>
        void RequestDrag(Vector3 toward, float seconds);
    }

    /// <summary>Lookup helpers for <see cref="IKnockable"/>.</summary>
    public static class Knockables
    {
        /// <summary>Finds the knockable on <paramref name="go"/> or its parents (e.g. from a hit collider).</summary>
        public static bool TryGetFor(GameObject go, out IKnockable knockable)
        {
            knockable = go != null ? go.GetComponentInParent<IKnockable>() : null;
            return knockable != null;
        }
    }
}
