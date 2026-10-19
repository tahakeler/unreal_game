using Unity.Netcode;

namespace HarvestPanic.World
{
    /// <summary>Server-authority helper: world simulation runs on the host, or locally when no session is running (tests, editor).</summary>
    public static class WorldAuthority
    {
        /// <summary>True on the server/host, or when there is no listening NetworkManager.</summary>
        public static bool IsServerOrOffline
        {
            get
            {
                var nm = NetworkManager.Singleton;
                return nm == null || !nm.IsListening || nm.IsServer;
            }
        }
    }
}
