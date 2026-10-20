using HarvestPanic.Core;
using UnityEngine;

namespace HarvestPanic.Economy
{
    /// <summary>
    /// Add-only adapter (Audio team, 2026-10-20): exposes the EconomyDirector day clock as the Core
    /// <see cref="IDayPhaseInfo"/> so presentation (music) can follow the day without referencing Economy.
    /// Valid on host and clients (EconomyDirector replicates Phase/PhaseRemaining). "None" before a director exists.
    /// </summary>
    public sealed class EconomyDayPhaseInfo : IDayPhaseInfo
    {
        /// <summary>The registered instance.</summary>
        public static readonly EconomyDayPhaseInfo Shared = new EconomyDayPhaseInfo();

        public string PhaseName
        {
            get { var d = EconomyDirector.Instance; return d != null ? d.Phase.ToString() : "None"; }
        }

        public float PhaseRemaining
        {
            get { var d = EconomyDirector.Instance; return d != null ? d.PhaseRemaining : 0f; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            if (!Services.TryGet(out IDayPhaseInfo _)) Services.Register<IDayPhaseInfo>(Shared);
        }
    }
}
