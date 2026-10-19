using System;
using System.Collections.Generic;

namespace HarvestPanic.Economy
{
    /// <summary>
    /// Host-authoritative day state machine (day-loop §3 Phases, week-quota §3). Explicit transition table;
    /// timed phases (Morning/Workday/Dusk) auto-advance, Payout/Sunday/Evicted wait for <see cref="GoTo"/>.
    /// Calendar: DayIndex 0-based across the run, Week 1-based, DayOfWeek 1..DaysPerWeek.
    /// </summary>
    public sealed class DayClock
    {
        static readonly Dictionary<DayPhase, DayPhase[]> Transitions = new Dictionary<DayPhase, DayPhase[]>
        {
            { DayPhase.None, new[] { DayPhase.Morning } },
            { DayPhase.Morning, new[] { DayPhase.Workday } },
            { DayPhase.Workday, new[] { DayPhase.Dusk, DayPhase.Payout } },
            { DayPhase.Dusk, new[] { DayPhase.Payout } },
            { DayPhase.Payout, new[] { DayPhase.Morning, DayPhase.Sunday } },
            { DayPhase.Sunday, new[] { DayPhase.Morning, DayPhase.Evicted } },
            { DayPhase.Evicted, new[] { DayPhase.Morning } },
        };

        /// <summary>Current phase.</summary>
        public DayPhase Phase { get; private set; } = DayPhase.None;
        /// <summary>Seconds elapsed in this phase (game seconds after time scale).</summary>
        public float Elapsed { get; private set; }
        /// <summary>Length of the current timed phase (0 = untimed).</summary>
        public float Duration { get; private set; }
        /// <summary>Remaining seconds of a timed phase.</summary>
        public float Remaining => Duration > 0f ? Math.Max(0f, Duration - Elapsed) : 0f;
        /// <summary>Day within the run, 0-based.</summary>
        public int DayIndex { get; private set; }
        /// <summary>Week, 1-based.</summary>
        public int Week { get; private set; } = 1;
        /// <summary>Working day within the week, 1-based.</summary>
        public int DayOfWeek { get; private set; } = 1;
        /// <summary>Days per week (Mon–Fri).</summary>
        public int DaysPerWeek { get; set; } = 5;
        /// <summary>Dev time scale: phase timers advance by dt / TimeScale... i.e. durations are multiplied by it.</summary>
        public float DurationScale { get; set; } = 1f;

        /// <summary>Provides the base duration of a timed phase (already modifier-resolved).</summary>
        public Func<DayPhase, float> DurationOf = _ => 0f;
        /// <summary>Raised after every transition (old, new).</summary>
        public event Action<DayPhase, DayPhase> PhaseChanged;
        /// <summary>Raised when a timed phase runs out; the owner decides the next phase.</summary>
        public event Action<DayPhase> PhaseTimedOut;

        /// <summary>True if <paramref name="to"/> is a legal next phase.</summary>
        public bool CanGo(DayPhase to) => Transitions.TryGetValue(Phase, out var next) && Array.IndexOf(next, to) >= 0;

        /// <summary>Resets the calendar to Week 1 Day 1 (phase None).</summary>
        public void ResetRun() { Phase = DayPhase.None; DayIndex = 0; Week = 1; DayOfWeek = 1; Elapsed = 0; Duration = 0; }

        /// <summary>Transitions; advances the calendar when entering Morning from Payout/Sunday. Throws on illegal moves.</summary>
        public void GoTo(DayPhase to)
        {
            if (!CanGo(to)) throw new InvalidOperationException($"Illegal day transition {Phase} -> {to}");
            var from = Phase;
            if (to == DayPhase.Morning && from == DayPhase.Payout) { DayIndex++; DayOfWeek++; }
            else if (to == DayPhase.Morning && from == DayPhase.Sunday) { DayIndex++; Week++; DayOfWeek = 1; }
            else if (to == DayPhase.Morning && from == DayPhase.Evicted) { DayIndex = 0; Week = 1; DayOfWeek = 1; }
            Phase = to;
            Elapsed = 0f;
            Duration = DurationOf(to) * DurationScale;
            PhaseChanged?.Invoke(from, to);
        }

        /// <summary>True on the last working day of the week.</summary>
        public bool IsLastDayOfWeek => DayOfWeek >= DaysPerWeek;

        /// <summary>Advances the timer; raises <see cref="PhaseTimedOut"/> once when a timed phase ends.</summary>
        public void Tick(float dt)
        {
            if (Duration <= 0f) return;
            float before = Elapsed;
            Elapsed += dt;
            if (before < Duration && Elapsed >= Duration) PhaseTimedOut?.Invoke(Phase);
        }

        /// <summary>
        /// A modifier changed the current phase's base length: remaining time scales by new/old, never below
        /// <paramref name="minRemaining"/> (day-loop Edge Cases).
        /// </summary>
        public void Rescale(float newBaseDuration, float minRemaining = 30f)
        {
            if (Duration <= 0f) return;
            float newDur = newBaseDuration * DurationScale;
            float remaining = Remaining * (newDur / Duration);
            remaining = Math.Max(Math.Min(minRemaining * DurationScale, Remaining), remaining);
            Duration = newDur;
            Elapsed = Math.Max(0f, newDur - remaining);
        }

        /// <summary>Client mirror: sets the replicated state directly.</summary>
        public void Mirror(DayPhase phase, float elapsed, float duration, int dayIndex, int week, int dayOfWeek)
        {
            Phase = phase; Elapsed = elapsed; Duration = duration; DayIndex = dayIndex; Week = week; DayOfWeek = dayOfWeek;
        }
    }
}
