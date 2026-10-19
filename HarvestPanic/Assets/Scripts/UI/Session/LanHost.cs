namespace HarvestPanic.UI
{
    /// <summary>One row of the LAN browser. Filled from whatever the session provider exposes (see <see cref="SessionAccess"/>).</summary>
    public struct LanHost
    {
        public string Name;
        public string Address;
        public int Players;
        public int MaxPlayers;
        public bool InProgress;
        public string Display => string.IsNullOrEmpty(Name) ? Address : Name;
    }
}
