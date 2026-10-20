using UnityEngine;

namespace HarvestPanic.Gameplay
{
    /// <summary>Optional veto on grabbing (e.g. players may only be grabbed while knocked out).</summary>
    public interface IGrabGate
    {
        /// <summary>True when <paramref name="clientId"/> may grab this now.</summary>
        bool CanBeGrabbedBy(ulong clientId);
    }

    /// <summary>
    /// A carryable whose physics is simulated by its own owner (player bodies). The server-side
    /// <see cref="CarryBody"/> tracks grips but applies no forces; the owner pulls itself toward the carrier.
    /// </summary>
    public interface ISelfSimulatedCarryable { }

    /// <summary>A held tool that reacts to the Use button.</summary>
    public interface IUsableTool
    {
        /// <summary>Server: Use pressed (true) or released (false) by <paramref name="user"/>.</summary>
        void ServerUse(bool down, PlayerCarry user);

        /// <summary>Prompt shown while holding, e.g. "RMB: Shake (7 left)".</summary>
        string UsePrompt { get; }
    }
}
