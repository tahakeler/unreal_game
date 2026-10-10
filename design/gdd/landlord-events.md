# Landlord Events

> **Status**: Draft
> **Author**: systems-designer + economy-designer (Batch C)
> **Last Updated**: 2026-10-10
> **Implements Pillar**: The Farm Gets Worse, You Get Better · Failure Is the Content
> **Quick reference**: Layer `Feature` · Priority `Alpha` · Key deps `week-quota`, `day-loop`

## Summary

The Landlord is a smiling man in a too-tall hat who never attacks but always makes things worse. He adds one new rule every week, shows up mid-week for inspections, walks the farm "helping", and offers devil's-bargain quota deals. All of it is authored as Day Modifiers.

## 1. Overview

Landlord Events is an event deck plus one non-hostile NPC. It provides (a) **Weekly Rules**, persistent Day Modifiers that escalate each week, (b) **Visits**, one or two mid-week events (Inspection, Farm Walk, Double Down offer, Helpful Visit), and (c) the host role in Landlord Sunday (`week-quota`). It never damages players directly; its pressure is economic (fines, tax, quota terms) and chaotic (opening gates, rearranging things).

## 2. Player Fantasy

"He's polite, he's smiling, and he's ruining us." The Landlord should feel like a cartoon bureaucrat: you can't hurt him (well, you *can*, but it costs $50), you must work around him, and every visit is an anxious little comedy scene.

## 3. Detailed Rules

### Weekly Rules
1. After each paid Sunday, draw 1 rule from the Weekly Rule deck (no repeats in a run). Duration `Week`, category `Landlord`, Weight `Minor` (doesn't count toward chaos cap).
2. Rules **accumulate** across the region run, max 3 active; the 4th replaces the oldest ("the Landlord forgot that one").
3. Week 1 has no rule (tutorial week).

| Rule | Effect (Day Modifier params) |
|---|---|
| Muddy Boots Are a Violation | Inspections fine $25 per muddy player |
| Sunday Best | Livestock sold without a hat −10% (`Goods.ValueMult` on tag) — hats cost $5 at market |
| Landlord's Cut | `Economy.IncomeTaxRate` +0.05 |
| No Running on the Bridge | `Bridge.PlankBreakMult` ×1.3 |
| Early Curfew | `Day.WorkdaySeconds` −45 |
| Respawn Paperwork | `Economy.RespawnFeeMult` ×1.2 |
| Quiet Hours | Noise Events from voice ×1.25 radius at dusk (via `proximity-voice` param) |
| One Pumpkin Per Family | Order lines never request the same GoodsId twice; `Order.LineCount` +1 |

### Visits (mid-week)
- From Week 2: 1 visit per week on a random day Tue–Thu; from Week 4: 2 visits (different days). Announced on the Morning twist card. Each is a **Major** Day Modifier (counts toward cap; if capped, it is deferred, never dropped, and Sunday is the last fallback slot).

| Visit | Duration | Rules | Typical $ impact |
|---|---|---|---|
| **Inspection** | 120 s window in Workday | Landlord picks 3 checks from list (muddy players, loose livestock in barn zone, broken fence, unbruised order item shown); each failed check = fine $25 × week cap $150 | −$50 to −$150 |
| **Farm Walk** | 90 s | Landlord walks a spline across the farm & bridge; any goods/player/wagon that physically hits him = $50 fine ("dry-cleaning"); he "helpfully" opens one pen gate | −$0 to −$150 + escaped animals |
| **Double Down** | offer at Morning, 20 s vote | Accept: this week's Due ×2, but next week's quota is waived ("paid in advance"). Decline: nothing. Offered max once per run, never in the region's final week | ±a whole week |
| **Helpful Visit** | Day | Landlord "fixes" the bridge: `Bridge.PlankBreakMult` ×0.5 and charges a $100 "service fee" on Payout. Can't decline | −$100, + safer hauling |

## 4. Formulas

**Inspection fine**: `Fine = min(150, Σ failed_checks × 25 × (1 + 0.2 (w−2)))` → W2 $25/check, W4 $35/check.

**Farm Walk fine**: `$50 × hits`, max 3 hits counted per walk (then he leaves, offended).

**Double Down EV check** (designer guideline): accepting in week w trades `Q(w)` extra now for skipping `Q(w+1) = 1.35 Q(w)`; worth it if the crew's current carryover + this week's net ≥ 2 Q(w). Example W2 4p: Due 3,240 vs typical net ~2,050 + carryover → usually a trap unless carrying ≥ $1,200.

**Expected Landlord cost per week** (tuning target): ≈ 3–6% of weekly gross (W3 ≈ $75–$190).

## 5. Edge Cases
- **Landlord ragdolled into river**: he cannot die; respawns at his car after 10 s, fine applies once.
- **Bull charges Landlord**: fine attributed to whoever wore red nearest (Ledger: "Dave's shirt −$50"). Livestock-caused hits with no player in 5 m → no fine.
- **Inspection during storm with bridge out**: Landlord inspects only barn bank.
- **Double Down accepted then evicted**: just eviction, no special case.
- **Double Down then a player leaves**: re-scale applies to the doubled Due (`week-quota` formula).
- **Visit deferred beyond Friday**: converted into a $50 "missed appointment" fee on Sunday.

## 6. Dependencies
- **Upstream**: `week-quota` (week number, Sunday, Due terms), `day-loop` (Day Modifier contract, scheduling).
- **Downstream / interacts**: `shared-wallet-economy` (fines, tax), `critter-ai` (gate opening releases livestock), `bridge-hazards`, `blame-ledger` (fine attribution), `physics-carry` (hit detection on Landlord), `proximity-voice`, `audio-feedback`, `hud-menus` (rule chalkboard).

## 7. Tuning Knobs
| Knob | Default | Range |
|---|---|---|
| Rules max active | 3 | 2–5 |
| Visits/week (W2–3 / W4+) | 1 / 2 | 0–3 |
| Inspection fine per check | $25 (+20%/wk) | 10–50 |
| Inspection cap | $150 | 75–300 |
| Farm Walk fine / max hits | $50 / 3 | 25–100 / 1–5 |
| Double Down multiplier | ×2 | ×1.5–×2.5 |
| Helpful Visit fee | $100 | 50–200 |

## 8. Acceptance Criteria
1. Week 1 has zero Landlord rules and zero visits.
2. Paying Week 3 Sunday adds one undrawn rule; with 3 active, a 4th removes the oldest.
3. Inspection in W4 with 3 failed checks fines exactly min(150, 3 × 35) = $105.
4. Landlord takes 4 hits in one Farm Walk → $150 fine, then exits.
5. Double Down accepted in W2 doubles Due; W3 Sunday shows Due $0 and still advances week.
6. Landlord never applies damage/knockout to players (automated check: no damage events sourced from Landlord actor).

## Comedy beats
- The Landlord compliments a pig, then fines it.
- On Farm Walk he holds a gate open and waves the animals through like a doorman.
- Hitting him with a pumpkin produces a slow-motion hat fall that the Camcorder always captures.
- Rule cards are typed memos stamped "FRIENDLY REMINDER" in huge red letters.
