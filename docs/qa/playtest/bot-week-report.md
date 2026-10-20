# Bot week playtest report -- 2026-10-21

**Test**: `HarvestPanic.Tests.PlayMode.Perf.BotWeekSoakTests.BotFarmers_FullWeek_IntoWeek2` (PASS).
Raw day-by-day log: `docs/qa/playtest/raw/bot-week-log.md`.

**Setup**: Muddlebury, LAN host (idle) + 3 scripted bot farmers (unspawned copies of the real player prefab, moved
kinematically; the goods they carry are the real server NetworkObjects and sales go through the real MarketPen /
EconomyDirector). Day scale 0.05 (27 s Workday). Bots: grab nearest free goods -> carry over the bridge -> drop
in the Sale Pen; random shouts through `NoiseService` every 3-9 s. Host actions: Next Day at Payout, 60% chance to
buy a random shop offer each evening, Sunday -> Next Day, Collateral -> sell an upgrade. Night runs at every Dusk
(real NightDirector). Weather rolled by the real deck.

**Assertions (all pass)**: reached week 2; >= 1 Sunday; 0 errors/exceptions/asserts in the log; no night creatures
left at Morning; Morning NetworkObject count growth <= 40 (actual 0); wallet delta == sum of wallet txns (250 == 250);
memory growth < 512 MB (actual -28 MB).

## What happened each day

| Day | Weather | Start $ | End $ | Sold (units / $) | Disasters | Night (Stitches / Foxes) | Shop |
|---|---|---|---|---|---|---|---|
| W1 Mon | Clear | 100 | 183 | 3 / 83 | prize pumpkin bruised (Nobody) | yes (1 / 0) | browsed |
| W1 Tue | Rain | 183 | 286 | 3 / 103 | prize pumpkin bruised | yes (1 / 1) | bought Rubber Boots (-150) |
| W1 Wed | Rain | 136 | 225 | 3 / 89 | prize pumpkin bruised | yes (1 / 2) | browsed |
| W1 Thu | Fog | 225 | 356 | 3 / 71 | prize pumpkin bruised x2 | yes (1 / 2) | bought Better Nets (-200) |
| W1 Fri | Fog | 156 | 237 | 3 / 81 | prize pumpkin bruised x2 | yes (1 / 2) | Lantern Posts proposed, not bought |
| **Sunday W1** | -- | 237 (+353 dev top-up) | 50 carryover | -- | -- | -- | Rent 540 **PAID** ("The Landlord counts it twice, slowly."), MVD "The Weather" |
| W2 Mon | Heatwave | 50 | 350 | 3 / 180 | prize pumpkin bruised | yes (1 / 3) | browsed; Landlord rule **Quiet Hours** active |
| W2 Tue | Storm | 350 | -- | (run stopped at Morning) | | | |

Without the dev top-up the bots end week 1 **303 short** of the 540 rent and the run goes Sunday -> Collateral ->
Evicted (seen in two earlier runs, before the top-up was added). Bots only haul loose goods, never livestock, so this is
a lower bound on income rather than a balance verdict.

Stability: Morning NetworkObjects 24 every day (end 24); Unity allocated memory 319 MB on day 1 -> 291 MB on W2 Tue
(flat); Mono heap 577 -> 644 MB over 7 days (slow climb, editor heap; watch in a player build); critters 13 every
morning; goods reset to 5 every morning; 0 errors.

## Bugs / findings

| # | Severity | Finding | Evidence | Owner |
|---|---|---|---|---|
| 1 | S2 (balance) | **Week-1 rent looks out of reach with Muddlebury's daily goods.** 5 loose goods spawn per day and only 3 ever sell (~$85/day), so 5 days make ~$430 against a 540 quota (W2 quota 730). Bots skip livestock, so humans earn more, but the gap is large. | rent shortfall line, raw log | economy-designer |
| 2 | S3 | **2 of the 5 daily goods never sell.** Bots make ~15 pen drops a day but exactly 3 sales every day: the same goods get dropped in the pen, are not sold, and get picked up again. Probably the prize pumpkin (2-hand) and one crate. Could be a pen-trigger/goods-definition problem or a bot artifact (drop with no velocity). Needs triage with a human drop. | deliveries vs units columns | gameplay-programmer (economy) |
| 3 | S3 | **The prize pumpkin is bruised by "Nobody" every day, from day 1, before any player touches it.** It is the daily receipt headline on all 6 days. Looks like spawn/settle impact damage (or the night fox). | receipts section | gameplay-programmer (goods) |
| 4 | S3 | **No Landlord barks in 8 days.** `OnLandlordBark` never fired, even though a Landlord rule (Quiet Hours) was drawn for week 2 and the Sunday line printed. Either barks need the Landlord NPC near a player (the idle host was at spawn) or the visit plan never fires at day scale 0.05. | "Landlord barks" section is empty | gameplay-programmer (landlord) |
| 5 | S4 | Night fox count grows by about 1 per night (0 -> 3 by W2 Mon). Probably intended weekly escalation; confirm against the night-creatures GDD. | Night column | ai-programmer |
| 6 | S4 (test) | The last row of the raw log (W2D2) is a partial-day snapshot with negative delivery deltas. This is a log artifact, not a game bug. | raw log | perf/qa |

No crashes, exceptions, NetworkObject leaks or wallet desyncs were found.
