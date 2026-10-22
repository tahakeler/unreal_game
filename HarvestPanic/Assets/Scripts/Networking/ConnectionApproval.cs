using System.Collections.Generic;

namespace HarvestPanic.Networking
{
    /// <summary>Result of evaluating a join request.</summary>
    public struct ApprovalDecision
    {
        public bool Approved;
        /// <summary>Player-readable rejection reason (sent to the client), null when approved.</summary>
        public string Reason;
        /// <summary>Sanitised, de-duplicated name to put in the roster.</summary>
        public string PlayerName;
    }

    /// <summary>
    /// Server-side connection approval rules (pure, EditMode tested). The host's own connection is always approved.
    /// Order: malformed payload -> version mismatch -> lobby locked -> full.
    /// </summary>
    public static class ConnectionApproval
    {
        public const string MalformedReason = "Couldn't read your join request. Please update the game.";
        public const string LockedReason = "The host isn't taking new farmhands right now.";
        /// <summary>Join held while the host switched scenes, for longer than the hold limit (SceneTransitionGate).</summary>
        public const string BusyReason = "The host is still loading the farm. Try joining again in a moment.";

        /// <summary>Evaluates a join.</summary>
        /// <param name="payloadBytes">Raw NGO connection data.</param>
        /// <param name="isHostSelf">True when the request is the host's own local client.</param>
        /// <param name="connectedCount">Players already in the session (host included).</param>
        /// <param name="maxPlayers">Session cap (1..GameInfo.MaxPlayers).</param>
        /// <param name="hostProtocol">Host protocol version.</param>
        /// <param name="hostBuild">Host Application.version.</param>
        /// <param name="locked">True while joins are refused (e.g. payout cutscene).</param>
        /// <param name="takenNames">Names already in the roster.</param>
        public static ApprovalDecision Evaluate(byte[] payloadBytes, bool isHostSelf, int connectedCount, int maxPlayers,
            int hostProtocol, string hostBuild, bool locked, IEnumerable<string> takenNames)
        {
            bool parsed = ConnectionPayload.TryParse(payloadBytes, out var p);
            if (isHostSelf)
                return new ApprovalDecision { Approved = true, PlayerName = PlayerNameSanitizer.Sanitize(parsed ? p.PlayerName : null) };
            if (!parsed) return Reject(MalformedReason);
            if (p.Protocol != hostProtocol || (p.BuildVersion ?? "") != (hostBuild ?? ""))
                return Reject(VersionReason(p.BuildVersion, p.Protocol, hostBuild, hostProtocol));
            if (locked) return Reject(LockedReason);
            int cap = maxPlayers < 1 ? 1 : (maxPlayers > HarvestPanic.Core.GameInfo.MaxPlayers ? HarvestPanic.Core.GameInfo.MaxPlayers : maxPlayers);
            if (connectedCount >= cap) return Reject(FullReason(cap));
            string name = PlayerNameSanitizer.MakeUnique(PlayerNameSanitizer.Sanitize(p.PlayerName), takenNames);
            return new ApprovalDecision { Approved = true, PlayerName = name };
        }

        /// <summary>"The farm is full (4/4)."</summary>
        public static string FullReason(int cap) => $"The farm is full ({cap}/{cap}).";

        /// <summary>"Your farm is a different version (vX vs vY)." per frontend-lobby GDD.</summary>
        public static string VersionReason(string clientBuild, int clientProto, string hostBuild, int hostProto) =>
            $"Your farm is a different version (v{clientBuild} p{clientProto} vs v{hostBuild} p{hostProto}).";

        static ApprovalDecision Reject(string reason) => new ApprovalDecision { Approved = false, Reason = reason };
    }
}
