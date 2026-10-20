using HarvestPanic.Core;

namespace HarvestPanic.Gameplay
{
    /// <summary>How the body was last handled; picks the blame verb for a bruise.</summary>
    public enum HandlingMode : byte { None, Held, Dropped, Thrown, Slipped }

    /// <summary>
    /// Carry-side attribution (physics-carry rule 8): wraps the shared <see cref="TouchHistory"/> (last 3 players
    /// within 3 s, newest first) and remembers how the body was last handled so impacts get the right verb.
    /// </summary>
    public sealed class TouchLog
    {
        readonly TouchHistory _history;
        readonly float _reportWindow;
        float _modeSince;

        /// <summary>Last handling mode.</summary>
        public HandlingMode Mode { get; private set; }

        /// <summary>Player who handled it last (grab, drop, throw).</summary>
        public ulong LastHandler { get; private set; } = PlayerIds.None;

        /// <summary>Creates a log.</summary>
        public TouchLog(float touchWindow, int capacity, float reportWindow)
        {
            _history = new TouchHistory(touchWindow, capacity);
            _reportWindow = reportWindow;
        }

        /// <summary>Records a touch without changing the mode (e.g. periodic refresh while held).</summary>
        public void Touch(ulong clientId, float now) => _history.Record(clientId, now);

        /// <summary>Records a touch and sets the mode.</summary>
        public void Handle(ulong clientId, HandlingMode mode, float now)
        {
            _history.Record(clientId, now);
            LastHandler = clientId;
            Mode = mode;
            _modeSince = now;
        }

        /// <summary>
        /// True while impacts should be reported: while held, or within the report window after a drop/throw/slip.
        /// </summary>
        public bool IsReporting(float now) =>
            Mode == HandlingMode.Held || (Mode != HandlingMode.None && now - _modeSince <= _reportWindow);

        /// <summary>Instigator for an impact now: newest toucher inside the window, else the last handler while reporting.</summary>
        public ulong Instigator(float now)
        {
            ulong id = _history.LastToucher(now);
            if (id != PlayerIds.None) return id;
            return IsReporting(now) ? LastHandler : PlayerIds.None;
        }

        /// <summary>Blame verb for an impact in the current mode.</summary>
        public BlameVerb VerbForImpact()
        {
            switch (Mode)
            {
                case HandlingMode.Thrown: return BlameVerb.Threw;
                case HandlingMode.Dropped: return BlameVerb.Dropped;
                case HandlingMode.Slipped: return BlameVerb.Slipped;
                default: return BlameVerb.Bruised;
            }
        }

        /// <summary>Copies valid touchers (newest first) into <paramref name="dest"/>.</summary>
        public int Snapshot(float now, ulong[] dest) => _history.Snapshot(now, dest);
    }
}
