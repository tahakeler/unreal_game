using System;
using System.Collections.Generic;
using HarvestPanic.Core;
using UnityEngine;

namespace HarvestPanic.Economy
{
    /// <summary>
    /// Pure formulas from goods-value §4, shared-wallet-economy §4, week-quota §4 and day-loop §4.
    /// No Unity state: every input is a parameter so EditMode tests can pin the GDD examples.
    /// </summary>
    public static class EconomyMath
    {
        /// <summary>Half-up rounding (Unity's Mathf.Round is banker's rounding).</summary>
        public static int RoundHalfUp(double v) => (int)Math.Floor(v + 0.5);

        /// <summary>round_to_N with half-up.</summary>
        public static int RoundTo(double v, int step) => (int)(Math.Floor(v / step + 0.5) * step);

        // ---------------- Goods value ----------------

        /// <summary>ΔBP = max(0, I − I_min) / I_scale × Fragility × M_bruise, capped per hit; 0 for solver spikes.</summary>
        public static float BruiseGain(float designImpulse, float fragility, float bruiseMult, EconomyTuning t)
        {
            if (designImpulse > t.ImpulseSolverErrorAbove) return 0f;
            float bp = Mathf.Max(0f, designImpulse - t.ImpulseMin) / t.ImpulseScale * fragility * bruiseMult;
            return Mathf.Min(bp, t.PerHitBruiseCap);
        }

        /// <summary>Tier from bruise points: Pristine &lt;10, Scuffed 10–34, Bruised 35–64, Busted 65–99, Ruined 100.</summary>
        public static BruiseState StateFor(float bp, EconomyTuning t)
        {
            if (bp >= t.RuinedAt) return BruiseState.Ruined;
            if (bp >= t.BustedAt) return BruiseState.Busted;
            if (bp >= t.BruisedAt) return BruiseState.Bruised;
            if (bp >= t.ScuffedAt) return BruiseState.Scuffed;
            return BruiseState.Pristine;
        }

        /// <summary>B(state); ruined livestock floor at 25 %, ruined crops/products 0.</summary>
        public static float BruiseMult(BruiseState s, GoodsCategory c, EconomyTuning t)
        {
            switch (s)
            {
                case BruiseState.Pristine: return t.MultPristine;
                case BruiseState.Scuffed: return t.MultScuffed;
                case BruiseState.Bruised: return t.MultBruised;
                case BruiseState.Busted: return t.MultBusted;
                default: return c == GoodsCategory.Livestock ? t.MultRuinedLivestock : t.MultRuinedCrop;
            }
        }

        /// <summary>F(t) = clamp(1 − max(0, t − grace)/decay, floor, 1).</summary>
        public static float Freshness(float elapsed, float grace, float decay, float floor = 0.5f)
        {
            if (elapsed < 0f) return 1f;
            return Mathf.Clamp(1f - Mathf.Max(0f, elapsed - grace) / decay, floor, 1f);
        }

        /// <summary>Freshness for a category (livestock and specials never decay).</summary>
        public static float FreshnessFor(GoodsCategory c, float elapsed, EconomyTuning t)
        {
            switch (c)
            {
                case GoodsCategory.Crop: return Freshness(elapsed, t.CropGrace, t.CropDecay, t.FreshnessFloor);
                case GoodsCategory.Product: return Freshness(elapsed, t.ProductGrace, t.ProductDecay, t.FreshnessFloor);
                default: return 1f;
            }
        }

        /// <summary>
        /// V = round(Base × B × F × D × O × ValueModifierMult). Ruined crops/products sell as compost ($1).
        /// </summary>
        public static int SaleValue(int baseValue, GoodsCategory c, BruiseState s, float freshness, float demand,
                                    float orderMult, float valueModifierMult, EconomyTuning t)
        {
            if (s == BruiseState.Ruined && c != GoodsCategory.Livestock) return t.CompostValue;
            double v = baseValue * (double)BruiseMult(s, c, t) * freshness * demand * orderMult * valueModifierMult;
            return Math.Max(0, RoundHalfUp(v));
        }

        /// <summary>ΔDollars = BaseValue × D_expected × (B_old − B_new), whole dollars, &gt;= 0.</summary>
        public static int BruiseDollarLoss(int baseValue, float expectedDemand, float bOld, float bNew) =>
            Math.Max(0, RoundHalfUp(baseValue * (double)expectedDemand * (bOld - bNew)));

        // ---------------- Market ----------------

        /// <summary>D(g, n) = clamp(D_day × decay^max(0, n − N_sat) × DemandMult, min, max); n is 1-based.</summary>
        public static float Demand(float dayDemand, int unitIndex1Based, float demandMult, EconomyTuning t)
        {
            float sat = Mathf.Pow(t.SaturationDecay, Mathf.Max(0, unitIndex1Based - t.SaturationUnits));
            return Mathf.Clamp(dayDemand * sat * demandMult, t.DemandMin, t.DemandMax);
        }

        // ---------------- Wallet / fees / quota ----------------

        /// <summary>Fee(w) = round_to_5(25 × (1 + 0.25(w − 1)) × RespawnFeeMult) → 25/30/40/45/50/55.</summary>
        public static int RespawnFee(int week, float feeMult, EconomyTuning t) => RespawnFee(week, feeMult, t, 4);

        /// <summary>
        /// Player-scaled fee (GDD §9 open issue 5): <c>round_to_5(25 × (1 + 0.25(w − 1)) × RespawnFeeMult × S_p^0.5)</c>.
        /// W1: 4p $25, 3p $25, 2p $20, 1p $15. Keeps a death a similar share of rent at every crew size.
        /// </summary>
        public static int RespawnFee(int week, float feeMult, EconomyTuning t, int players) =>
            RoundTo(t.RespawnFeeBase * (1.0 + t.RespawnFeeWeeklyGrowth * (week - 1)) * feeMult * RespawnFeePlayerScale(players, t), 5);

        /// <summary>S_p^exponent (1 for 4 players).</summary>
        public static double RespawnFeePlayerScale(int players, EconomyTuning t) =>
            t.RespawnFeePlayerExponent <= 0f ? 1.0 : Math.Pow(t.PlayerScaleFor(players), t.RespawnFeePlayerExponent);

        /// <summary>Q(w) = round_to_10(Q_base × G^(w−1) × S_p × R).</summary>
        public static int Quota(int week, int players, EconomyTuning t, float regionMult = -1f)
        {
            float r = regionMult > 0f ? regionMult : t.RegionMult;
            double w1 = week <= 1 ? t.QuotaWeekOneMult : 1.0; // learning week (tuned 2026-10-21)
            return RoundTo(t.QuotaBase * Math.Pow(t.QuotaGrowth, week - 1) * t.PlayerScaleFor(players) * r * w1, 10);
        }

        /// <summary>Extra daily units of <paramref name="goodsId"/> for a crew of <paramref name="players"/> (0 for solo or unlisted goods).</summary>
        public static int ExtraDailyGoods(string goodsId, int players, EconomyTuning t)
        {
            if (t.ExtraDailyGoodsPerExtraPlayer == null || string.IsNullOrEmpty(goodsId)) return 0;
            int extraPlayers = Math.Max(0, Math.Min(players, 4) - 1), n = 0;
            foreach (var e in t.ExtraDailyGoodsPerExtraPlayer) if (e.GoodsId == goodsId) n += e.PerExtraPlayer * extraPlayers;
            return n;
        }

        /// <summary>Mid-week leaver re-scale: Q' = Q × (d_done + d_left × S_new/S_old) / days.</summary>
        public static int ProratedQuota(int quota, int daysDone, int days, int oldPlayers, int newPlayers, EconomyTuning t)
        {
            float ratio = t.PlayerScaleFor(newPlayers) / t.PlayerScaleFor(oldPlayers);
            return RoundTo(quota * (daysDone + (days - daysDone) * ratio) / days, 10);
        }

        /// <summary>Order Complete Bonus = 60 × week.</summary>
        public static int OrderCompleteBonus(int week, EconomyTuning t) => t.OrderCompleteBonusPerWeek * week;

        /// <summary>q = max(1, round(BaseQ × S_p × (1 + 0.15 (w − 1)))).</summary>
        public static int OrderQuantity(int baseQ, int players, int week, EconomyTuning t) =>
            Math.Max(1, RoundHalfUp(baseQ * t.PlayerScaleFor(players) * (1.0 + t.OrderWeeklyGrowth * (week - 1))));

        // ---------------- Blame ----------------

        /// <summary>Severity = clamp(100 × |Δ$| / (Quota/5), 0, 100) + VerbBonus, clamped 0..100.</summary>
        public static byte Severity(int dollarDelta, int weeklyQuota, BlameVerb verb)
        {
            float share = Mathf.Max(1f, weeklyQuota / 5f);
            float s = Mathf.Clamp(100f * Math.Abs(dollarDelta) / share, 0f, 100f) + VerbBonus(verb);
            return (byte)Mathf.Clamp(RoundHalfUp(s), 0, 100);
        }

        /// <summary>VerbBonus table (blame-ledger Formulas).</summary>
        public static int VerbBonus(BlameVerb v)
        {
            switch (v)
            {
                case BlameVerb.DroppedInRiver: return 25;
                case BlameVerb.TippedWagon: return 30;
                case BlameVerb.SummonedCreature: return 20;
                case BlameVerb.KnockedOut:
                case BlameVerb.Drowned:
                case BlameVerb.Stranded: return 10;
                case BlameVerb.YeetedTeammate: return 15;
                default: return 0;
            }
        }

        /// <summary>Verbs that survive the $ noise floor.</summary>
        public static bool IsNonEconomic(BlameVerb v) =>
            v == BlameVerb.KnockedOut || v == BlameVerb.Drowned || v == BlameVerb.Stranded ||
            v == BlameVerb.SummonedCreature || v == BlameVerb.YeetedTeammate;

        /// <summary>Shares 0.6/0.3/0.1 renormalised for fewer touchers (newest first).</summary>
        public static float[] TouchShares(int touchers, EconomyTuning t)
        {
            if (touchers <= 0) return Array.Empty<float>();
            int n = Math.Min(touchers, t.TouchShares.Length);
            var r = new float[n];
            float sum = 0f;
            for (int i = 0; i < n; i++) sum += t.TouchShares[i];
            for (int i = 0; i < n; i++) r[i] = t.TouchShares[i] / sum;
            return r;
        }

        /// <summary>Weekly Blame Score for one player (floor 0).</summary>
        public static float BlameScore(IEnumerable<(int dollarDelta, float share, bool disasterPrimary)> events, EconomyTuning t)
        {
            float score = 0f;
            foreach (var e in events)
            {
                if (e.dollarDelta < 0) score += -e.dollarDelta * e.share;
                else score -= 0.5f * e.dollarDelta * e.share;
                if (e.disasterPrimary) score += t.DisasterScoreBonus;
            }
            return Mathf.Max(0f, score);
        }
    }
}
