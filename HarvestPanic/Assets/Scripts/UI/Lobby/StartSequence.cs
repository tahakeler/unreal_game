using System.Collections.Generic;
using HarvestPanic.Core;

namespace HarvestPanic.UI
{
    public enum BellResult { NotHost, NoPlayers, NeedsConfirm, CountdownStarted, AlreadyCounting }

    /// <summary>
    /// Dinner-bell rules from the lobby GDD, free of Unity so it is testable.
    /// All ready: ring starts a 3 s countdown. Not all ready: first ring asks "Start anyway?", a second ring within
    /// <see cref="ConfirmWindow"/> seconds starts it. A countdown begun with everyone ready is cancelled when someone un-readies.
    /// </summary>
    public sealed class StartSequence
    {
        public float CountdownSeconds = 3f;
        public float ConfirmWindow = 3f;

        float _confirmLeft;
        float _countdownLeft;
        bool _requiresAllReady;

        public bool IsCounting { get; private set; }
        public bool IsAwaitingConfirm => _confirmLeft > 0f;
        public float SecondsLeft => _countdownLeft;
        public int ReadyCount { get; private set; }
        public int TotalCount { get; private set; }

        public BellResult RingBell(bool isHost, IReadOnlyList<PlayerInfo> players)
        {
            if (!isHost) return BellResult.NotHost;
            if (players == null || players.Count == 0) return BellResult.NoPlayers;
            if (IsCounting) return BellResult.AlreadyCounting;
            Count(players);
            bool all = ReadyCount == TotalCount;
            if (all) { Begin(true); return BellResult.CountdownStarted; }
            if (IsAwaitingConfirm) { Begin(false); return BellResult.CountdownStarted; }
            _confirmLeft = ConfirmWindow;
            return BellResult.NeedsConfirm;
        }

        /// <summary>Advances timers. Returns true on the frame the countdown finishes.</summary>
        public bool Tick(float dt)
        {
            if (_confirmLeft > 0f) _confirmLeft = _confirmLeft - dt > 0f ? _confirmLeft - dt : 0f;
            if (!IsCounting) return false;
            _countdownLeft -= dt;
            if (_countdownLeft > 0f) return false;
            Cancel();
            return true;
        }

        /// <summary>Call when the player list changes. Returns true if a countdown was cancelled because someone un-readied.</summary>
        public bool OnPlayersChanged(IReadOnlyList<PlayerInfo> players)
        {
            Count(players);
            if (IsCounting && _requiresAllReady && ReadyCount < TotalCount) { Cancel(); return true; }
            return false;
        }

        public void Cancel()
        {
            IsCounting = false;
            _countdownLeft = 0f;
            _confirmLeft = 0f;
        }

        void Begin(bool requiresAllReady)
        {
            IsCounting = true;
            _requiresAllReady = requiresAllReady;
            _countdownLeft = CountdownSeconds;
            _confirmLeft = 0f;
        }

        void Count(IReadOnlyList<PlayerInfo> players)
        {
            ReadyCount = 0; TotalCount = players?.Count ?? 0;
            for (int i = 0; i < TotalCount; i++) if (players[i].IsReady) ReadyCount++;
        }
    }
}
