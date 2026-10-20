using System;
using System.Collections.Generic;
using HarvestPanic.Core;
using UnityEngine;

namespace HarvestPanic.Economy
{
    /// <summary>Registered day parameter (day-loop §3 "FDayParamSpec").</summary>
    public readonly struct DayParamSpec
    {
        public readonly string Name; public readonly float Base, Min, Max;
        public DayParamSpec(string name, float b, float min, float max) { Name = name; Base = b; Min = min; Max = max; }
    }

    /// <summary>Day phases (day-loop). Shop follows Payout; Sunday is the rent check; Collateral = 60 s sell-back when short; Evicted ends the run.</summary>
    public enum DayPhase : byte { None, Morning, Workday, Dusk, Payout, Sunday, Evicted, Shop, Collateral }

    /// <summary>
    /// Active Day Modifier set with the day-loop stacking rules, chaos cap queue and parameter resolution
    /// <c>P = Override_highestPriority ?? clamp((P_base + ΣAdd) × ΠMul, min, max)</c>. Pure logic.
    /// </summary>
    public sealed class DayModifierStack
    {
        /// <summary>Well-known parameter names owned by Economy (registered in the spec table).</summary>
        public static class Params
        {
            public const string WorkdaySeconds = "Day.WorkdaySeconds", DuskSeconds = "Day.DuskSeconds", DuskBonus = "Day.DuskBonus";
            public const string GoodsValueMult = "Goods.ValueMult", GoodsBruiseMult = "Goods.BruiseMult", FreshnessDecayMult = "Goods.FreshnessDecayMult";
            public const string DemandMult = "Market.DemandMult", AcceptSeconds = "Market.AcceptSeconds";
            public const string IncomeTaxRate = "Economy.IncomeTaxRate", OrderLineCount = "Order.LineCount";
        }

        /// <summary>Spec table (day-loop "Registered parameters").</summary>
        public static readonly Dictionary<string, DayParamSpec> Specs = new Dictionary<string, DayParamSpec>(StringComparer.Ordinal)
        {
            { Params.WorkdaySeconds, new DayParamSpec(Params.WorkdaySeconds, 540, 300, 720) },
            { Params.DuskSeconds, new DayParamSpec(Params.DuskSeconds, 180, 90, 300) },
            { Params.DuskBonus, new DayParamSpec(Params.DuskBonus, 1.25f, 1, 2) },
            { Params.GoodsValueMult, new DayParamSpec(Params.GoodsValueMult, 1, 0.25f, 3) },
            { Params.GoodsBruiseMult, new DayParamSpec(Params.GoodsBruiseMult, 1, 0.5f, 2) },
            { Params.FreshnessDecayMult, new DayParamSpec(Params.FreshnessDecayMult, 1, 0, 4) },
            { Params.DemandMult, new DayParamSpec(Params.DemandMult, 1, 0.25f, 3) },
            { Params.AcceptSeconds, new DayParamSpec(Params.AcceptSeconds, 2, 0.5f, 5) },
            { DayParams.EconomyRespawnFeeMult, new DayParamSpec(DayParams.EconomyRespawnFeeMult, 1, 0, 5) },
            { Params.IncomeTaxRate, new DayParamSpec(Params.IncomeTaxRate, 0, 0, 0.9f) },
            { Params.OrderLineCount, new DayParamSpec(Params.OrderLineCount, 3, 2, 4) },
            { DayParams.NoiseEnvMult, new DayParamSpec(DayParams.NoiseEnvMult, 1, 0.2f, 3) },
            { DayParams.NoiseVoiceRadiusMult, new DayParamSpec(DayParams.NoiseVoiceRadiusMult, 1, 0.2f, 3) },
        };

        /// <summary>One active modifier instance.</summary>
        public sealed class Active
        {
            public DayModifier Mod;
            public int Stacks = 1;
            public long AppliedOrder;
            public float SecondsLeft;
        }

        readonly List<Active> _active = new List<Active>();
        readonly List<DayModifier> _queued = new List<DayModifier>();
        long _order;

        /// <summary>Active modifiers in apply order.</summary>
        public IReadOnlyList<Active> ActiveList => _active;
        /// <summary>Majors waiting for the next day because of the chaos cap.</summary>
        public IReadOnlyList<DayModifier> Queued => _queued;
        /// <summary>Raised when the active set changes.</summary>
        public event Action Changed;

        /// <summary>MajorCap(w) = min(4, 1 + w).</summary>
        public static int MajorCap(int week) => Math.Min(4, 1 + Math.Max(1, week));

        /// <summary>Applies a modifier; returns false when ignored (Unique dup) or queued (chaos cap).</summary>
        public bool Apply(DayModifier mod, int week, bool bypassCap = false)
        {
            if (mod == null) return false;
            var existing = _active.Find(a => a.Mod.ModifierId == mod.ModifierId);
            switch (mod.StackRule)
            {
                case DayModifierStackRule.Unique when existing != null: return false;
                case DayModifierStackRule.Refresh when existing != null:
                    existing.SecondsLeft = mod.DurationSeconds; existing.AppliedOrder = ++_order; Changed?.Invoke(); return true;
                case DayModifierStackRule.Stack when existing != null:
                    if (existing.Stacks >= mod.MaxStacks) return false;
                    existing.Stacks++; existing.AppliedOrder = ++_order; Changed?.Invoke(); return true;
            }
            if (mod.Weight == DayModifierWeight.Major && !bypassCap)
            {
                int majors = 0;
                foreach (var a in _active) if (a.Mod.Weight == DayModifierWeight.Major) majors++;
                bool replacesGroupMajor = mod.StackRule == DayModifierStackRule.ReplaceGroup &&
                    _active.Exists(a => a.Mod.Weight == DayModifierWeight.Major && a.Mod.GroupTag == mod.GroupTag && !string.IsNullOrEmpty(mod.GroupTag));
                if (!replacesGroupMajor && majors >= MajorCap(week)) { _queued.Add(mod); return false; }
            }
            if (mod.StackRule == DayModifierStackRule.ReplaceGroup && !string.IsNullOrEmpty(mod.GroupTag))
                _active.RemoveAll(a => a.Mod.GroupTag == mod.GroupTag);
            _active.Add(new Active { Mod = mod, AppliedOrder = ++_order, SecondsLeft = mod.DurationSeconds });
            Changed?.Invoke();
            return true;
        }

        /// <summary>Removes a modifier by id.</summary>
        public bool Remove(string id)
        {
            int n = _active.RemoveAll(a => a.Mod.ModifierId == id);
            if (n > 0) Changed?.Invoke();
            return n > 0;
        }

        /// <summary>Removes everything (new run).</summary>
        public void Clear() { _active.Clear(); _queued.Clear(); Changed?.Invoke(); }

        /// <summary>Returns and clears the queued Majors (applied next morning).</summary>
        public List<DayModifier> TakeQueued() { var q = new List<DayModifier>(_queued); _queued.Clear(); return q; }

        /// <summary>Ticks Seconds-duration modifiers.</summary>
        public void Tick(float dt)
        {
            bool changed = false;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var a = _active[i];
                if (a.Mod.Duration != DayModifierDuration.Seconds) continue;
                a.SecondsLeft -= dt;
                if (a.SecondsLeft <= 0f) { _active.RemoveAt(i); changed = true; }
            }
            if (changed) Changed?.Invoke();
        }

        /// <summary>Expires modifiers whose duration ends at <paramref name="ending"/>.</summary>
        public void ExpireAt(DayModifierDuration ending)
        {
            int n = _active.RemoveAll(a => a.Mod.Duration == ending ||
                (ending == DayModifierDuration.Day && (a.Mod.Duration == DayModifierDuration.WorkdayOnly || a.Mod.Duration == DayModifierDuration.DuskOnly || a.Mod.Duration == DayModifierDuration.Seconds)) ||
                (ending == DayModifierDuration.Week && a.Mod.Duration == DayModifierDuration.Day));
            if (n > 0) Changed?.Invoke();
        }

        /// <summary>True when the modifier only applies in another phase.</summary>
        static bool InactiveInPhase(DayModifier m, DayPhase phase) =>
            (m.Duration == DayModifierDuration.WorkdayOnly && phase != DayPhase.Workday) ||
            (m.Duration == DayModifierDuration.DuskOnly && phase != DayPhase.Dusk);

        /// <summary>Resolves <paramref name="param"/> from <paramref name="baseValue"/> in <paramref name="phase"/>.</summary>
        public float Resolve(string param, float baseValue, DayPhase phase = DayPhase.Workday)
        {
            float add = 0f, mul = 1f; bool hasOverride = false; float ov = 0f; int ovPri = int.MinValue; long ovOrder = -1;
            foreach (var a in _active)
            {
                if (InactiveInPhase(a.Mod, phase)) continue;
                foreach (var fx in a.Mod.Effects)
                {
                    if (!string.Equals(fx.ParamName, param, StringComparison.Ordinal)) continue;
                    switch (fx.Op)
                    {
                        case DayParamOp.Add: add += fx.Value * a.Stacks; break;
                        case DayParamOp.Mul: mul *= Mathf.Pow(fx.Value, a.Stacks); break;
                        case DayParamOp.Override:
                            if (a.Mod.Priority > ovPri || (a.Mod.Priority == ovPri && a.AppliedOrder > ovOrder))
                            { hasOverride = true; ov = fx.Value; ovPri = a.Mod.Priority; ovOrder = a.AppliedOrder; }
                            break;
                    }
                }
            }
            if (hasOverride) return ov;
            float v = (baseValue + add) * mul;
            return Specs.TryGetValue(param, out var spec) ? Mathf.Clamp(v, spec.Min, spec.Max) : v;
        }

        /// <summary>True if any active modifier grants <paramref name="tag"/>.</summary>
        public bool HasWorldTag(string tag, DayPhase phase = DayPhase.Workday)
        {
            foreach (var a in _active)
            {
                if (InactiveInPhase(a.Mod, phase)) continue;
                foreach (var g in a.Mod.GrantTags) if (string.Equals(g, tag, StringComparison.Ordinal)) return true;
            }
            return false;
        }
    }
}
