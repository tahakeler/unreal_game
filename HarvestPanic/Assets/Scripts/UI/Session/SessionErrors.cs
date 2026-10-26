using System;
using System.Text.RegularExpressions;

namespace HarvestPanic.UI
{
    public enum SessionErrorKind
    {
        Generic, VersionMismatch, FarmFull, Kicked, HostLeft, ConnectionLost, Timeout, Locked, Busy,
        BadAddress, Unreachable, SteamUnavailable, SteamFailed, HostFailed, AlreadyInSession, UpdateNeeded,
    }

    /// <summary>A player-facing dialog for a session failure: string-table title/body, badge glyph and whether Retry makes sense.</summary>
    public readonly struct SessionError
    {
        public readonly SessionErrorKind Kind;
        public readonly string Title, Body, Badge;
        public readonly bool CanRetry;
        public SessionError(SessionErrorKind kind, string title, string body, string badge, bool canRetry)
        { Kind = kind; Title = title; Body = body; Badge = badge; CanRetry = canRetry; }
    }

    /// <summary>
    /// Turns the Networking team's reason strings (SessionService / ConnectionApproval constants, reused verbatim) into
    /// friendly dialogs. Matching is on stable phrases of those constants; an EditMode test feeds the real constants
    /// through <see cref="Classify"/> so a reworded reason fails CI instead of silently becoming "Generic".
    /// Unknown reasons keep their text as the body under a generic title.
    /// </summary>
    public static class SessionErrors
    {
        static readonly Regex Full = new Regex(@"full \((\d+)/(\d+)\)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        static readonly Regex Version = new Regex(@"v(\S+) p(\d+) vs v(\S+) p(\d+)", RegexOptions.CultureInvariant);

        public static SessionErrorKind KindOf(string reason)
        {
            string r = reason ?? "";
            bool Has(string s) => r.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0;
            if (Has("different version")) return SessionErrorKind.VersionMismatch;
            if (Has("read your join request")) return SessionErrorKind.UpdateNeeded;
            if (Has("is full")) return SessionErrorKind.FarmFull;
            if (Has("sent home")) return SessionErrorKind.Kicked;
            if (Has("went to bed")) return SessionErrorKind.HostLeft;
            if (Has("lost connection") || Has("hosting stopped unexpectedly")) return SessionErrorKind.ConnectionLost;
            if (Has("never answered")) return SessionErrorKind.Timeout;
            if (Has("isn't taking new farmhands")) return SessionErrorKind.Locked;
            if (Has("still loading")) return SessionErrorKind.Busy;
            if (Has("isn't a valid address") || Has("doesn't look like a steam")) return SessionErrorKind.BadAddress;
            if (Has("steam isn't running") || Has("steam is not running")) return SessionErrorKind.SteamUnavailable;
            if (Has("through steam") || Has("steam lobby")) return SessionErrorKind.SteamFailed;
            if (Has("start hosting")) return SessionErrorKind.HostFailed;
            if (Has("already in a session")) return SessionErrorKind.AlreadyInSession;
            if (Has("couldn't reach") || Has("could not reach") || Has("couldn't start connecting")) return SessionErrorKind.Unreachable;
            return SessionErrorKind.Generic;
        }

        /// <summary>Dialog content for <paramref name="reason"/>.</summary>
        public static SessionError Classify(string reason)
        {
            var kind = KindOf(reason);
            string raw = string.IsNullOrWhiteSpace(reason) ? UiText.Get("error.generic.body") : reason.Trim();
            switch (kind)
            {
                case SessionErrorKind.VersionMismatch:
                {
                    var m = Version.Match(raw);
                    string body = m.Success ? UiText.Format("error.version.body", m.Groups[1].Value, m.Groups[3].Value) : raw;
                    return Make(kind, "error.version.title", body, "!", false);
                }
                case SessionErrorKind.UpdateNeeded: return Make(kind, "error.version.title", UiText.Get("error.update.body"), "!", false);
                case SessionErrorKind.FarmFull:
                {
                    var m = Full.Match(raw);
                    string body = m.Success ? UiText.Format("error.full.body", m.Groups[2].Value) : raw;
                    return Make(kind, "error.full.title", body, "4", true);
                }
                case SessionErrorKind.Kicked: return Make(kind, "error.kicked.title", UiText.Get("error.kicked.body"), "!", false);
                case SessionErrorKind.HostLeft: return Make(kind, "error.hostleft.title", raw, "z", false);
                case SessionErrorKind.ConnectionLost: return Make(kind, "error.lost.title", UiText.Get("error.lost.body"), "?", true);
                case SessionErrorKind.Timeout: return Make(kind, "error.timeout.title", UiText.Get("error.timeout.body"), "?", true);
                case SessionErrorKind.Locked: return Make(kind, "error.locked.title", UiText.Get("error.locked.body"), "!", true);
                case SessionErrorKind.Busy: return Make(kind, "error.busy.title", UiText.Get("error.busy.body"), "...", true);
                case SessionErrorKind.BadAddress: return Make(kind, "error.address.title", raw, "?", false);
                case SessionErrorKind.Unreachable: return Make(kind, "error.unreachable.title", raw, "?", true);
                case SessionErrorKind.SteamUnavailable: return Make(kind, "error.steam.title", UiText.Get("error.steam.body"), "!", false);
                case SessionErrorKind.SteamFailed: return Make(kind, "error.steamfail.title", raw, "!", true);
                case SessionErrorKind.HostFailed: return Make(kind, "error.host.title", raw, "!", true);
                case SessionErrorKind.AlreadyInSession: return Make(kind, "error.busysession.title", raw, "!", false);
                default: return Make(kind, "error.generic.title", raw, "!", true);
            }
        }

        static SessionError Make(SessionErrorKind k, string titleKey, string body, string badge, bool retry) =>
            new SessionError(k, UiText.Get(titleKey), body, badge, retry);
    }
}
