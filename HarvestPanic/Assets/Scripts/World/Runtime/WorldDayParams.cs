namespace HarvestPanic.World
{
    /// <summary>Day parameter names read by the world (bridge-hazards section 3.5). Candidates for Core DayParams (lead request).</summary>
    public static class WorldDayParams
    {
        /// <summary>Multiplier on plank damage (upgrades, landlord events).</summary>
        public const string PlankBreakMult = "Bridge.PlankBreakMult";
        /// <summary>Multiplier on plank safe load (storm, sabotage).</summary>
        public const string SafeLoadMult = "Bridge.SafeLoadMult";
        /// <summary>Mud speed multiplier override.</summary>
        public const string MudSpeedMult = "Mud.SpeedMult";
    }
}
