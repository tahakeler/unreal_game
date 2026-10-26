using HarvestPanic.Core;
using UnityEngine;

namespace HarvestPanic.World
{
    /// <summary>
    /// Client-local "lights on" for Muddlebury's windows and lamp glass (art bible 4.3: cold night, warm windows).
    /// Reads the read-only day clock (<see cref="IDayClockService"/>) twice a second and, only when the answer changes,
    /// swaps the day material for the warm emissive night material on the listed renderers' submeshes.
    /// No clock (sandbox scene, tests) = daytime. Nothing here touches gameplay.
    /// </summary>
    public sealed class NightWindows : MonoBehaviour
    {
        [SerializeField] Renderer[] renderers = new Renderer[0];
        [SerializeField] Material[] dayMaterials = new Material[0];
        [SerializeField] Material[] nightMaterials = new Material[0];

        /// <summary>Test/QA override: null = follow the clock.</summary>
        public static bool? ForceLit;

        bool? _lit;
        float _next;

        /// <summary>True while it is light-the-lamps time on this peer.</summary>
        public bool Lit => _lit == true;

        /// <summary>Build-time setup: pairs dayMaterials[i] -> nightMaterials[i] on every renderer.</summary>
        public void Configure(Renderer[] targets, Material[] day, Material[] night)
        {
            renderers = targets; dayMaterials = day; nightMaterials = night;
        }

        /// <summary>Evening by the day clock: late Dusk onward, through Payout/Sunday/Evicted.</summary>
        public static bool IsEvening()
        {
            if (ForceLit.HasValue) return ForceLit.Value;
            if (!Services.TryGet(out IDayClockService clock)) return false;
            switch (clock.Phase)
            {
                case DayClockPhase.Workday: return clock.PhaseProgress01 > 0.97f;
                case DayClockPhase.Dusk:
                case DayClockPhase.Payout:
                case DayClockPhase.Sunday:
                case DayClockPhase.Evicted: return true;
                default: return false;
            }
        }

        void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.5f;
            Refresh();
        }

        /// <summary>Re-evaluates now (QA captures call this after setting <see cref="ForceLit"/>).</summary>
        public void Refresh()
        {
            bool lit = IsEvening();
            if (_lit == lit) return;
            _lit = lit;
            var from = lit ? dayMaterials : nightMaterials;
            var to = lit ? nightMaterials : dayMaterials;
            foreach (var r in renderers)
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                for (int k = 0; k < from.Length && k < to.Length; k++)
                    if (mats[i] == from[k]) { mats[i] = to[k]; changed = true; break; }
                if (changed) r.sharedMaterials = mats;
            }
        }
    }
}
