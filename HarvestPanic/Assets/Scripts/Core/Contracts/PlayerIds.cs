namespace HarvestPanic.Core
{
    /// <summary>
    /// Player identity convention: a player is the NGO <c>ulong</c> client id (host = 0).
    /// Sentinel values for "nobody" live here so every system uses the same ones.
    /// </summary>
    public static class PlayerIds
    {
        /// <summary>No player (empty slot / no instigator).</summary>
        public const ulong None = ulong.MaxValue;

        /// <summary>Blame target meaning "Nature" (wind, curses): used with an empty instigator list.</summary>
        public const ulong Nature = ulong.MaxValue - 1;

        /// <summary>True when <paramref name="id"/> refers to a real connected-player id.</summary>
        public static bool IsPlayer(ulong id) => id != None && id != Nature;
    }
}
