namespace HarvestPanic.Core
{
    /// <summary>
    /// The local player's chosen display name ("Farm Name"). Provider: Frontend UI (backed by its settings and the
    /// session platform); consumer: Customization's character creator (Name tab). Empty means "use the Steam / default name".
    /// Add-only contract.
    /// </summary>
    public interface IPlayerNameService
    {
        /// <summary>Saved Farm Name, or "" when none was chosen.</summary>
        string PlayerName { get; }

        /// <summary>Saves and applies a (pre-filtered) Farm Name; "" clears it.</summary>
        void SetPlayerName(string name);
    }
}
