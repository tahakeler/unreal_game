using System;

namespace HarvestPanic.Core
{
    /// <summary>
    /// Optional social features of a session. Networking may implement it alongside <see cref="ISessionService"/>;
    /// UI checks <c>Services.TryGet(out ISessionSocial s)</c> or <c>session is ISessionSocial</c>.
    /// </summary>
    public interface ISessionSocial
    {
        /// <summary>Host only: removes a player from the session.</summary>
        void Kick(ulong clientId);

        /// <summary>Sends a text chat line to everyone in the session.</summary>
        void SendChat(string text);

        /// <summary>Raised on every machine for each received chat line: (sender client id, text).</summary>
        event Action<ulong, string> OnChat;
    }
}
