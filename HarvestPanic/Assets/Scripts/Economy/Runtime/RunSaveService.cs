using HarvestPanic.Core;

namespace HarvestPanic.Economy
{
    /// <summary>
    /// <see cref="IRunSaveService"/> provider (save-persistence). Plain object registered by the EconomyBootstrapper at
    /// startup so the main menu can show "Continue Farm" before any farm scene exists.
    /// </summary>
    public sealed class RunSaveService : IRunSaveService
    {
        static readonly string[] Days = { "MON", "TUE", "WED", "THU", "FRI" };
        public static RunSaveService Instance { get; } = new RunSaveService();

        public bool HasSave => RunSaveStore.Load(out _) != null;
        public bool ContinueRequested { get; set; }

        public string Summary
        {
            get
            {
                var d = RunSaveStore.Load(out _);
                if (d == null) return "";
                string day = d.Resume == RunResumePoint.WeekStart ? "MON" : Days[UnityEngine.Mathf.Clamp(d.DayOfWeek - 1, 0, 4)] + " shop";
                return $"Week {d.Week} · {day} · {ReceiptBuilder.Money(d.Balance)} · {d.Upgrades.Count} upgrades";
            }
        }

        public void DeleteSave() { RunSaveStore.Delete(); ContinueRequested = false; }

        /// <summary>Registers with <see cref="Services"/> (idempotent).</summary>
        public static void Install() => Services.Register<IRunSaveService>(Instance);
    }
}
