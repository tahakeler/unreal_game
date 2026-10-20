using System;
using System.Collections.Generic;
using HarvestPanic.Core;
using Unity.Netcode;
using UnityEngine;

namespace HarvestPanic.Economy
{
    /// <summary>
    /// Landlord events (landlord-events.md): mid-week visits as Major Day Modifiers (Inspection, Helpful Visit),
    /// inspection fines, Helpful Visit service fee, missed-appointment fee, barks, and the Sunday walk-in figure.
    /// Weekly rules are drawn in <see cref="DrawLandlordRule"/> (EconomyDirector.cs).
    /// </summary>
    public sealed partial class EconomyDirector
    {
        readonly List<(int dayOfWeek, string visitId)> _visitPlan = new List<(int, string)>();
        int _inspectionFinedThisWeek;
        bool _inspectedToday;

        /// <summary>Raised on every peer when the Landlord says something (HUD toast).</summary>
        public event Action<string> OnLandlordBark;
        /// <summary>Last Landlord bark on this peer (tests).</summary>
        public string LastLandlordBark { get; private set; }
        /// <summary>Last inspection outcome on the server (tests/debug).</summary>
        public InspectionResult LastInspection { get; private set; }
        /// <summary>This week's planned visits (server).</summary>
        public IReadOnlyList<(int dayOfWeek, string visitId)> VisitPlan => _visitPlan;
        /// <summary>Active weekly Landlord rules (mirrored on clients via the modifier list).</summary>
        public IEnumerable<DayModifier> ActiveLandlordRules { get { foreach (var m in ActiveModifiers) if (LandlordParams.IsRule(m)) yield return m; } }

        void LandlordPlanWeek()
        {
            _inspectionFinedThisWeek = 0;
            _visitPlan.Clear();
            var pool = new List<string>();
            foreach (var m in Content.DayModifiers.All) if (LandlordParams.IsVisit(m)) pool.Add(m.ModifierId);
            pool.Sort(StringComparer.Ordinal);
            _visitPlan.AddRange(LandlordMath.PlanVisits(_clock.Week, pool, _rng, Tuning));
        }

        /// <summary>Morning: apply today's planned visit (chaos cap may defer it to tomorrow).</summary>
        void LandlordMorning()
        {
            _inspectedToday = false;
            foreach (var v in _visitPlan)
            {
                if (v.dayOfWeek != _clock.DayOfWeek || !Content.DayModifiers.TryGet(v.visitId, out var mod)) continue;
                if (_mods.Apply(mod, _clock.Week)) LandlordBark(mod.ModifierId.EndsWith("Inspection") ? LandlordText.InspectionArrive : LandlordText.HelpfulArrive);
            }
        }

        /// <summary>Workday tick: run the inspection once the workday passes the configured fraction.</summary>
        void LandlordTick()
        {
            if (_inspectedToday || _clock.Phase != DayPhase.Workday || !_mods.HasWorldTag(LandlordParams.TagInspection, DayPhase.Workday)) return;
            if (_clock.Duration <= 0f || 1f - _clock.Remaining / _clock.Duration < Tuning.InspectionAtWorkday01) return;
            RunInspection();
        }

        /// <summary>Server/test: performs the inspection now with the gathered world snapshot.</summary>
        public InspectionResult RunInspection() => RunInspection(GatherInspection());

        /// <summary>Server/test: performs the inspection with an explicit snapshot.</summary>
        public InspectionResult RunInspection(InspectionSnapshot snap)
        {
            if (!IsEconomyAuthority) return null;
            _inspectedToday = true;
            var r = LandlordMath.Inspect(snap, _clock.Week, _rng, Tuning, _inspectionFinedThisWeek);
            LastInspection = r;
            if (r.Fine > 0)
            {
                _inspectionFinedThisWeek += r.Fine;
                AddWalletTxn(new WalletTxn { Type = WalletTxnType.Fine, Amount = -r.Fine, PlayerId = PlayerIds.None });
                EmitBlame(new BlameEvent { Verb = BlameVerb.LandlordFine, DollarDelta = -r.Fine, CauseTag = "Landlord", ItemTag = "Inspection" });
                LandlordBark(LandlordText.CheckLine(r.Failed[0], LandlordMath.InspectionFinePerCheck(_clock.Week, Tuning)) + $" (−${r.Fine})");
            }
            else LandlordBark(LandlordText.InspectionClean);
            return r;
        }

        InspectionSnapshot GatherInspection()
        {
            var s = new InspectionSnapshot { MuddyBootsRule = _mods.HasWorldTag(LandlordParams.TagMuddyBootsRule, DayPhase.Workday) };
            if (_mods.HasWorldTag(LandlordParams.TagRain, DayPhase.Workday)) s.MuddyPlayers = _players; // everyone is muddy in the rain
            Vector3? barn = null;
            foreach (var m in LevelMarker.FindAll(MarkerRole.BarnRespawn)) { barn = m.transform.position; break; }
            foreach (var g in Goods.All)
            {
                if (g == null || g.Definition == null) continue;
                if (barn.HasValue && g.Definition.IsAlive && Vector3.Distance(g.transform.position, barn.Value) > Tuning.LooseLivestockRadius) s.LooseLivestock++;
                if (_market.WouldFillOrder(g.GoodsId) && g.BruiseState >= BruiseState.Bruised) s.BruisedOrderItems++;
            }
            foreach (var e in _ledger.Day) if (e.Event.Verb == BlameVerb.BrokePlank) { s.BrokePlankToday = true; break; }
            return s;
        }

        /// <summary>Payout (before Day modifiers expire): Helpful Visit service fee.</summary>
        void LandlordPayoutFees()
        {
            int fee = Mathf.RoundToInt(_mods.Resolve(LandlordParams.ServiceFee, 0f, DayPhase.Payout));
            if (fee <= 0) return;
            AddWalletTxn(new WalletTxn { Type = WalletTxnType.Fine, Amount = -fee, PlayerId = PlayerIds.None });
            EmitBlame(new BlameEvent { Verb = BlameVerb.LandlordFine, DollarDelta = -fee, CauseTag = "Landlord", ItemTag = "ServiceFee" });
        }

        /// <summary>Sunday (before rent): visits deferred past Friday become a missed-appointment fee each.</summary>
        void LandlordSundayFees()
        {
            foreach (var _ in _mods.TakeQueued(LandlordParams.IsVisit))
            {
                AddWalletTxn(new WalletTxn { Type = WalletTxnType.Fine, Amount = -Tuning.MissedAppointmentFee, PlayerId = PlayerIds.None });
                LandlordBark(LandlordText.MissedAppointment);
            }
        }

        void LandlordBark(string line)
        {
            LastLandlordBark = line;
            OnLandlordBark?.Invoke(line);
            if (IsSpawned && IsServer) LandlordBarkRpc(line);
        }

        [Rpc(SendTo.NotServer)]
        void LandlordBarkRpc(string line) { LastLandlordBark = line; OnLandlordBark?.Invoke(line); }

        /// <summary>Every peer: spawns the Sunday walk-in figure (presentation only, never networked).</summary>
        void LandlordPhaseChanged(DayPhase from, DayPhase to)
        {
            if (to == DayPhase.Sunday && LandlordVisitor.Current == null) LandlordVisitor.Spawn(this);
            if (to == DayPhase.Morning && LandlordVisitor.Current != null) Destroy(LandlordVisitor.Current.gameObject);
        }
    }
}
