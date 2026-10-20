using System;
using System.Collections.Generic;
using UnityEngine;

namespace HarvestPanic.UI
{
    /// <summary>Things that happen in the world that can arm a tape.</summary>
    public enum TipSignal { DayStarted, NearBridge, NearPen, ShopOpened, CritterNoiseNearby }

    /// <summary>One "Grandpa's tape": a short tip shown once per profile.</summary>
    public sealed class TipDef
    {
        public string Id;
        public string Title;
        /// <summary>Body with action tokens like {Interact} that resolve to current bindings.</summary>
        public string Body;
        /// <summary>Signal that arms it; null = armed by <see cref="AfterTip"/> + <see cref="AfterSeconds"/>.</summary>
        public TipSignal? Trigger;
        /// <summary>Tip that must already have been shown (this session or earlier) before this one.</summary>
        public string AfterTip;
        /// <summary>Seconds after <see cref="AfterTip"/> was shown this session before a timer-armed tip fires.</summary>
        public float AfterSeconds;
    }

    /// <summary>Which tapes the profile has already seen. Default: PlayerPrefs; tests: memory.</summary>
    public interface ITipStore
    {
        bool IsSeen(string id);
        void MarkSeen(string id);
        void ClearAll();
    }

    public sealed class MemoryTipStore : ITipStore
    {
        public readonly HashSet<string> Seen = new HashSet<string>();
        public bool IsSeen(string id) => Seen.Contains(id);
        public void MarkSeen(string id) => Seen.Add(id);
        public void ClearAll() => Seen.Clear();
    }

    public sealed class PlayerPrefsTipStore : ITipStore
    {
        const string Key = "HarvestPanic.TipsSeen.v1";
        readonly HashSet<string> _seen = new HashSet<string>();
        public PlayerPrefsTipStore()
        {
            foreach (var s in PlayerPrefs.GetString(Key, "").Split(',')) if (s.Length > 0) _seen.Add(s);
        }
        public bool IsSeen(string id) => _seen.Contains(id);
        public void MarkSeen(string id)
        {
            if (!_seen.Add(id)) return;
            PlayerPrefs.SetString(Key, string.Join(",", _seen)); PlayerPrefs.Save();
        }
        public void ClearAll() { _seen.Clear(); PlayerPrefs.DeleteKey(Key); PlayerPrefs.Save(); }
    }

    /// <summary>
    /// Pure scheduling rules for Grandpa's tapes (onboarding-accessibility "TapeEligible"): unseen, armed, spacing since the
    /// last tape, not within <see cref="DisasterQuietSeconds"/> of a disaster, never while a menu is open, one at a time.
    /// Shown = seen (marked when it appears, so a crash or skip still counts).
    /// </summary>
    public sealed class TipRules
    {
        /// <summary>Minimum seconds between two tapes. Design JIT spacing is 90 s; the first-morning basics use 12 s.</summary>
        public float Spacing = 12f;
        public float DisasterQuietSeconds = 10f;

        readonly ITipStore _store;
        readonly List<TipDef> _defs;
        readonly HashSet<string> _armed = new HashSet<string>();
        readonly Dictionary<string, float> _shownAt = new Dictionary<string, float>();
        float _lastShown = float.NegativeInfinity, _lastDisaster = float.NegativeInfinity;

        public bool Enabled = true;
        public IReadOnlyList<TipDef> Defs => _defs;

        public TipRules(ITipStore store, IEnumerable<TipDef> defs = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _defs = new List<TipDef>(defs ?? DefaultTapes());
        }

        public bool IsSeen(string id) => _store.IsSeen(id);

        /// <summary>Arms every unseen tape listening to <paramref name="signal"/>.</summary>
        public void Signal(TipSignal signal)
        {
            foreach (var d in _defs) if (d.Trigger == signal && !_store.IsSeen(d.Id)) _armed.Add(d.Id);
        }

        /// <summary>The player already did the thing (sold, hollered...): never teach it.</summary>
        public void MarkLearned(string id) => _store.MarkSeen(id);

        public void NotifyDisaster(float now) => _lastDisaster = now;

        /// <summary>Marks every tape seen ("skip all tapes").</summary>
        public void SkipAll() { foreach (var d in _defs) _store.MarkSeen(d.Id); _armed.Clear(); }

        /// <summary>
        /// Returns the next tape to show at <paramref name="now"/> (and marks it seen), or null.
        /// <paramref name="blocked"/>: a menu is open or a tape is already on screen.
        /// </summary>
        public TipDef Tick(float now, bool blocked)
        {
            if (!Enabled || blocked) return null;
            if (now - _lastShown < Spacing || now - _lastDisaster < DisasterQuietSeconds) return null;
            foreach (var d in _defs)
            {
                if (_store.IsSeen(d.Id)) continue;
                if (d.AfterTip != null && !_store.IsSeen(d.AfterTip)) continue;
                bool armed = d.Trigger.HasValue
                    ? _armed.Contains(d.Id)
                    : _shownAt.TryGetValue(d.AfterTip ?? "", out var t) && now - t >= d.AfterSeconds;
                if (!armed) continue;
                _store.MarkSeen(d.Id);
                _armed.Remove(d.Id);
                _shownAt[d.Id] = now;
                _lastShown = now;
                return d;
            }
            return null;
        }

        /// <summary>Replaces {Action} tokens with the current binding labels.</summary>
        public static string Resolve(string body, bool gamepad)
        {
            if (string.IsNullOrEmpty(body)) return body;
            var sb = new System.Text.StringBuilder(body.Length + 16);
            for (int i = 0; i < body.Length; i++)
            {
                if (body[i] == '{')
                {
                    int end = body.IndexOf('}', i + 1);
                    if (end > i)
                    {
                        string token = body.Substring(i + 1, end - i - 1);
                        sb.Append('[').Append(TokenLabel(token, gamepad)).Append(']');
                        i = end;
                        continue;
                    }
                }
                sb.Append(body[i]);
            }
            return sb.ToString();
        }

        static string TokenLabel(string token, bool gamepad)
        {
            switch (token)
            {
                case "MoveKeys":
                    if (gamepad) return "Left Stick";
                    return InputBindings.ActionLabel("Move", false) + string.Concat(
                        "/", InputBindings.Display(InputBindings.Defaults[2]), "/", InputBindings.Display(InputBindings.Defaults[1]),
                        "/", InputBindings.Display(InputBindings.Defaults[3]));
                case "Pause": return gamepad ? "Start" : "Esc";
                case "SkipTip": return gamepad ? "View" : "Backspace";
                default: return InputBindings.ActionLabel(token, gamepad);
            }
        }

        /// <summary>First-day tapes in teaching order (GDD ch.14 / onboarding-accessibility Tape #1 beats).</summary>
        public static List<TipDef> DefaultTapes() => new List<TipDef>
        {
            new TipDef { Id = "move", Title = "Grandpa's Tape #1: Walkin'", Trigger = TipSignal.DayStarted,
                Body = "Walk with {MoveKeys}, hop with {Jump}, run with {Sprint}. Your back is not what it was." },
            new TipDef { Id = "grab", Title = "Tape #2: Grabbin'", AfterTip = "move", AfterSeconds = 20f,
                Body = "Hold {Interact} on a critter or crate to grab it. Heavy things need a friend on the other end." },
            new TipDef { Id = "bridge", Title = "Tape #3: The Bridge", Trigger = TipSignal.NearBridge, AfterTip = "grab",
                Body = "Planks snap under too much weight. Cross one at a time, or swim and pray." },
            new TipDef { Id = "sell", Title = "Tape #4: Sellin'", Trigger = TipSignal.NearPen, AfterTip = "grab",
                Body = "Drop or {Throw} goods into the market pen before sundown. Bruised goods sell for less." },
            new TipDef { Id = "shout", Title = "Tape #5: Hollerin'", Trigger = TipSignal.CritterNoiseNearby, AfterTip = "grab",
                Body = "Press {Holler} to shout. Critters scatter from noise, so herd them, do not wake them." },
            new TipDef { Id = "shop", Title = "Tape #6: The Shop", Trigger = TipSignal.ShopOpened,
                Body = "Spend the farm's money on upgrades. Everybody votes, nobody agrees." },
        };
    }
}
