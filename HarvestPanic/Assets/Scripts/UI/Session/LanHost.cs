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
        /// <summary>Host player's name (may be empty).</summary>
        public string HostName;
        /// <summary>Host build version (empty when unknown); a different version cannot be joined.</summary>
        public string Version;
        public string Display => string.IsNullOrEmpty(Name) ? Address : Name;
    }
}
