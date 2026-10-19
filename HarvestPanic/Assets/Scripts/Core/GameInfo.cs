namespace HarvestPanic.Core
{
    /// <summary>
    /// Project-wide constants shared by every runtime assembly.
    /// Engine layer: must never reference gameplay assemblies.
    /// </summary>
    /// <example><code>if (players > GameInfo.MaxPlayers) Reject();</code></example>
    public static class GameInfo
    {
        public const string ProductName = "HARVEST PANIC";
        public const int MinPlayers = 1;
        public const int MaxPlayers = 4;
        public const string BootstrapScene = "Bootstrap";
    }
}
