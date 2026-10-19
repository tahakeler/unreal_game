namespace HarvestPanic.AI
{
    /// <summary>
    /// Critter brain states (critter-ai §3.1). Server-authoritative; replicated as a byte for presentation.
    /// Add-only: values are sent over the network.
    /// </summary>
    public enum CritterState : byte
    {
        /// <summary>Standing/grazing at home.</summary>
        Idle = 0,
        /// <summary>Walking to a random point near home.</summary>
        Wander = 1,
        /// <summary>Facing a threat for the readable 0.4 s tell before fleeing.</summary>
        Alert = 2,
        /// <summary>Running away from the weighted threat centroid.</summary>
        Flee = 3,
        /// <summary>Walking to (and eating at) a food lure.</summary>
        Lured = 4,
        /// <summary>No good escape and players close: personality response (Freeze/Charge/Juke/PlayDead).</summary>
        Cornered = 5,
        /// <summary>Held by one or more players; struggles and may break free.</summary>
        Carried = 6,
        /// <summary>Thrown/dropped; physics only, no movement.</summary>
        Stunned = 7,
        /// <summary>Winding down after a flee: threat below CalmThreshold, waiting CalmTime.</summary>
        Calm = 8
    }
}
