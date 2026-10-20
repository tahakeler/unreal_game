using UnityEngine;

namespace HarvestPanic.Camcorder
{
    /// <summary>Result of offering a trigger to <see cref="CaptureRules"/>.</summary>
    public enum CaptureDecision { Ignored, Started, Extended, Reopened, DeniedManualCap, DeniedOff }

    /// <summary>
    /// Pure trigger state machine (design/gdd/camcorder-replay.md rules 2-3, edge case "two disasters 4 s apart"):
    /// Idle -> Capturing on a disaster/manual press; the tape window is [t - PreRoll, t + PostRoll].
    /// A disaster while capturing, or within <c>Cooldown</c> s of the last trigger, extends the same tape (Reopened when it
    /// was already finalised: the recorder re-cuts and replaces it) up to <c>MaxTape</c> s. Manual saves are capped per day.
    /// </summary>
    public sealed class CaptureRules
    {
        public float PreRoll = 15f, PostRoll = 3f, Cooldown = 8f, MaxTape = 45f;
        public int ManualPerDay = 3, MinSeverity;
        public bool Enabled = true;

        public bool IsCapturing { get; private set; }
        public bool HasTape { get; private set; }
        public float TapeStart { get; private set; }
        public float TapeEnd { get; private set; }
        public float FirstTrigger { get; private set; }
        public float LastTrigger { get; private set; } = float.NegativeInfinity;
        public bool TapeIsManual { get; private set; }
        public int ManualUsed { get; private set; }
        public int ManualLeft => Mathf.Max(0, ManualPerDay - ManualUsed);

        public static CaptureRules From(CamcorderTuning t) => new CaptureRules
        {
            PreRoll = t.preRollSeconds, PostRoll = t.postRollSeconds, Cooldown = t.captureCooldown, MaxTape = t.maxTapeSeconds,
            ManualPerDay = t.manualSavesPerDay, MinSeverity = t.minAutoSeverity, Enabled = t.enabled
        };

        /// <summary>A disaster happened at <paramref name="now"/>; <paramref name="oldest"/> is the oldest buffered time.</summary>
        public CaptureDecision OnDisaster(float now, int severity, float oldest)
        {
            if (!Enabled) return CaptureDecision.DeniedOff;
            if (severity < MinSeverity) return CaptureDecision.Ignored;
            return Trigger(now, oldest, false);
        }

        /// <summary>The player pressed the save key.</summary>
        public CaptureDecision OnManual(float now, float oldest)
        {
            if (!Enabled) return CaptureDecision.DeniedOff;
            if (ManualUsed >= ManualPerDay) return CaptureDecision.DeniedManualCap;
            ManualUsed++;
            // A manual press always makes its own tape unless one is still rolling.
            if (IsCapturing) { TapeIsManual = true; return Extend(now); }
            return Start(now, oldest, true);
        }

        CaptureDecision Trigger(float now, float oldest, bool manual)
        {
            if (IsCapturing) return Extend(now);
            if (HasTape && now - LastTrigger <= Cooldown && now + PostRoll - TapeStart <= MaxTape + 1e-3f)
            {
                IsCapturing = true;
                Extend(now);
                return CaptureDecision.Reopened;
            }
            return Start(now, oldest, manual);
        }

        CaptureDecision Start(float now, float oldest, bool manual)
        {
            IsCapturing = true; HasTape = true; TapeIsManual = manual;
            TapeStart = Mathf.Max(oldest, now - PreRoll);
            FirstTrigger = LastTrigger = now;
            TapeEnd = Mathf.Min(now + PostRoll, TapeStart + MaxTape);
            return CaptureDecision.Started;
        }

        CaptureDecision Extend(float now)
        {
            LastTrigger = now;
            TapeEnd = Mathf.Min(Mathf.Max(TapeEnd, now + PostRoll), TapeStart + MaxTape);
            return CaptureDecision.Extended;
        }

        /// <summary>True exactly once when the open tape's post-roll has elapsed (the recorder then cuts it).</summary>
        public bool Tick(float now)
        {
            if (!IsCapturing || now < TapeEnd) return false;
            IsCapturing = false;
            return true;
        }

        /// <summary>New day: manual budget refills, no tape to extend.</summary>
        public void NewDay() { ManualUsed = 0; IsCapturing = false; HasTape = false; LastTrigger = float.NegativeInfinity; }
    }
}
