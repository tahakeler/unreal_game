# Landlord + HUD polish — progress (feature/landlord-events)

## Session 1
Built:
- Weekly rules 8/8: added `Mod_Landlord_MuddyBoots` (grant `World.Landlord.MuddyBootsRule`), `Mod_Landlord_SundayBest`
  (`Landlord.HatlessLivestockValueMult` ×0.9, applied in `QuoteValue` to livestock — no hats exist yet, so all livestock),
  `Mod_Landlord_OnePerFamily` (`Order.LineCount` +1; order lines were already distinct). Week 1 never draws a rule.
- Visits as Major Day modifiers: `Mod.Landlord.Visit.Inspection`, `Mod.Landlord.Visit.HelpfulVisit`. Plan per week
  (W2–3: 1, W4+: 2, distinct Tue–Thu) in `LandlordMath.PlanVisits`; applied at Morning (chaos cap defers to tomorrow;
  still queued at Sunday → $50 missed-appointment fine).
- Inspection at 50% of Workday: 3 of 4 checks (muddy = rain/storm active, loose livestock > 18 m from BarnRespawn,
  plank broken today per ledger, bruised order item). Fine = min(150, units × 25(1+0.2(w−2))), weekly cap $150.
  Blame `LandlordFine` (Nature "The Landlord"). Helpful Visit: PlankBreak ×0.5, $100 service fee at Payout.
- Landlord barks (replicated RPC) → plum HUD toasts.
- Sunday walk-in `LandlordVisitor` (primitive figure, stovepipe hat, clipboard, TextMesh world-space bubble + stamp),
  timeline `LandlordVisitScript` (explicit state machine). PAID = hat tip; EVICTED = hat pops off. HUD holds the Sunday
  receipt until the verdict beat ends.
- HUD: forecast line on the morning card (IWeatherService.ForecastText), Painter2D weather icon on the clock,
  "FRIENDLY REMINDER" rule memos (left), awards on daily and Sunday receipts.
- Respawn fee × S_p^0.5 (tuning `RespawnFeePlayerExponent`): W1 4p/3p/2p/1p = $25/$25/$20/$15.
- Awards: `AwardPicker.Daily` (MVD, Pig Whisperer, Bridge Inspector, Butterfingers, Frequent Swimmer, Town Crier,
  Escape Artist's Agent; max 3) and `.Weekly` (5 ledger awards); solo → MVD + Pig Whisperer, self-deprecating copy.

Tests: EditMode `LandlordTests` (11) + `EconomyFormulaTests`: 45/45.

Gaps: Farm Walk and Double Down visits not built (need NPC hit detection / vote UI); hats for Sunday Best;
real per-player mud state (rain proxy); `World.PenGateOpened` contract.

## Session 2
- PlayMode `LandlordPlayModeTests` (4): Sunday visit paid (receipt held until verdict, leaves Monday, W2 draws 1 rule),
  short -> EVICTED, forecast on morning card + weather icon, inspection fine + bark.
- QA `LandlordScreenshots` -> `docs/qa/screens/landlord/` (receipt-awards, visit-count, visit-verdict, sunday-receipt,
  rule-memos). Iterated: shop panel no longer covers the walk-in; bubble is unlit and sized to the text.
- GDD §9.6 documents the crew-size fee formula.
- Full run: EditMode 188/188; PlayMode 40 pass / 9 skipped (Explicit QA) / 1 fail
  (`PlayerPlayModeTests.HostPlayer_GrabsCarriesAndThrowsCrate`, Player domain).
