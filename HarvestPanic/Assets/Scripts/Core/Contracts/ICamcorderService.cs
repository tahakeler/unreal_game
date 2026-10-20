namespace HarvestPanic.Core
{
    /// <summary>
    /// Grandpa's Camcorder (design/gdd/camcorder-replay.md). Provider: <c>HarvestPanic.Camcorder</c> (client-local, auto-created);
    /// consumers: Frontend pause menu ("Replays"), HUD. Presentation only, never changes gameplay. Add-only contract.
    /// </summary>
    public interface ICamcorderService
    {
        /// <summary>Saved tapes currently held in memory for this day.</summary>
        int TapeCount { get; }
        /// <summary>True while the replay viewer or tape shelf is on screen.</summary>
        bool IsViewing { get; }
        /// <summary>Opens the tape shelf (list of saved moments).</summary>
        void OpenShelf();
        /// <summary>Saves a manual moment (same as the F8 hotkey); false when the daily manual cap is used up.</summary>
        bool SaveManualMoment();
    }
}
