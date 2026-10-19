using System;

namespace HarvestPanic.Core
{
    /// <summary>
    /// Last-N-touchers memory used by physics-carry, goods-value and the blame ledger so all three
    /// share one attribution rule. Window and capacity come from config (blame-ledger TouchWindow).
    /// </summary>
    public sealed class TouchHistory
    {
        readonly ulong[] _ids;
        readonly float[] _times;
        readonly float _window;
        int _count;

        /// <summary>Creates a history that remembers <paramref name="capacity"/> players for <paramref name="windowSeconds"/>.</summary>
        public TouchHistory(float windowSeconds, int capacity)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            _window = windowSeconds;
            _ids = new ulong[capacity];
            _times = new float[capacity];
        }

        /// <summary>Records a touch; the player moves to the front if already present.</summary>
        public void Record(ulong clientId, float now)
        {
            int existing = Array.IndexOf(_ids, clientId, 0, _count);
            int last = existing >= 0 ? existing : Math.Min(_count, _ids.Length - 1);
            for (int i = last; i > 0; i--) { _ids[i] = _ids[i - 1]; _times[i] = _times[i - 1]; }
            _ids[0] = clientId;
            _times[0] = now;
            if (existing < 0 && _count < _ids.Length) _count++;
        }

        /// <summary>Copies still-valid touchers (newest first) into <paramref name="dest"/>; returns how many.</summary>
        public int Snapshot(float now, ulong[] dest)
        {
            int n = 0;
            for (int i = 0; i < _count && n < dest.Length; i++)
                if (now - _times[i] <= _window) dest[n++] = _ids[i];
            return n;
        }

        /// <summary>Newest toucher inside the window or <see cref="PlayerIds.None"/>.</summary>
        public ulong LastToucher(float now) =>
            _count > 0 && now - _times[0] <= _window ? _ids[0] : PlayerIds.None;
    }
}
