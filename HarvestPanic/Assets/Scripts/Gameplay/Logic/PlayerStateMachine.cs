using System.Collections.Generic;

namespace HarvestPanic.Gameplay
{
    /// <summary>Player states (player-character section 3, "States"). Diving is not implemented in this slice.</summary>
    public enum PlayerState : byte
    {
        Normal = 0,
        Ragdoll = 1,
        KnockedOut = 2,
        Respawning = 3,
    }

    /// <summary>Why the player is being sent to the barn.</summary>
    public enum RespawnCause : byte { Drowned, KnockedOut, Stranded }

    /// <summary>
    /// Explicit transition table. The server is the only writer of <see cref="PlayerState"/>; it calls
    /// <see cref="CanTransition"/> before every change so no invalid state is reachable.
    /// </summary>
    public static class PlayerStateMachine
    {
        static readonly HashSet<(PlayerState, PlayerState)> Allowed = new HashSet<(PlayerState, PlayerState)>
        {
            (PlayerState.Normal, PlayerState.Ragdoll),        // impulse / trip / plank / faint
            (PlayerState.Normal, PlayerState.KnockedOut),     // >= 2x impulse
            (PlayerState.Normal, PlayerState.Respawning),     // kill-Z / river
            (PlayerState.Ragdoll, PlayerState.Normal),        // get-up
            (PlayerState.Ragdoll, PlayerState.KnockedOut),    // bigger hit or 3rd ragdoll in window
            (PlayerState.Ragdoll, PlayerState.Respawning),    // ragdolled into the river
            (PlayerState.KnockedOut, PlayerState.Normal),     // timer or revive
            (PlayerState.KnockedOut, PlayerState.Respawning), // thrown into the river
            (PlayerState.Respawning, PlayerState.Normal),     // barn respawn
        };

        /// <summary>True when <paramref name="from"/> -&gt; <paramref name="to"/> is a legal transition.</summary>
        public static bool CanTransition(PlayerState from, PlayerState to) => Allowed.Contains((from, to));

        /// <summary>True for states in which the physics body (not the controller) moves the player.</summary>
        public static bool IsPhysicsDriven(PlayerState s) => s == PlayerState.Ragdoll || s == PlayerState.KnockedOut;

        /// <summary>True when the player has input control.</summary>
        public static bool HasControl(PlayerState s) => s == PlayerState.Normal;
    }
}
