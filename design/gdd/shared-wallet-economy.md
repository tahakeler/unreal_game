# Shared Wallet & Market Economy

> **Status**: Draft
> **Author**: economy-designer (Batch C)
> **Last Updated**: 2026-10-10
> **Implements Pillar**: Heavy Together, Blamed Together · The Farm Gets Worse, You Get Better
> **Quick reference**: Layer `Core` · Priority `VS` · Key deps `goods-value`, `day-loop`

## Summary

The whole crew shares one wallet. Market sales pour in; respawn fees, repairs, fines, upgrades and the Sunday quota pour out. This doc owns the wallet, market demand, every faucet and sink, and the master economy model that proves the week is tight but beatable.

## 1. Overview

A single host-authoritative integer wallet (whole dollars) is shared by all players in a run. Every transaction is a typed, attributed ledger entry so the Blame Ledger and payout screen can itemize who earned and who cost. The market sets a daily demand multiplier per GoodsId and saturates when one good is flooded. This doc also contains the canonical income-vs-quota model used to tune `week-quota`, `farm-upgrades` and `landlord-events`.

## 2. Player Fantasy

"It's *our* money and you just spent $40 of it dying." Players should feel joint ownership: cheering together at a $450 pumpkin sale, and turning on each other when the respawn fee ticks. Sunday should feel like a bill you *just* scraped together.

## 3. Detailed Rules

### Wallet
1. One wallet per run, `int32 Balance`, server-only writes, replicated. Starting balance **$100** (Week 1, Day 1).
2. Every change is a `FWalletTxn {Type, Amount, PlayerId|System, GoodsId?, DayIndex, Timestamp}`. Types: `Sale`, `OrderBonus`, `RespawnFee`, `Repair`, `Fine`, `Upgrade`, `SellBack`, `Quota`, `Theft`, `Refund`.
3. **Overdraft**: fees and fines may push the balance to a floor of **−$200**. Below $0 the shop is locked. Discretionary spends (upgrades) require Balance ≥ price.
4. If a fee would go below −$200, the fee is capped at the floor and the excess is logged as "Landlord IOU" and **added to this week's quota** (debt follows you).
5. Balance carries across days and weeks within a run. On a failed Sunday, the wallet is wiped with the run (`save-persistence`).

### Faucets (income)
| Faucet | Rule |
|---|---|
| Market sale | V from `goods-value` |
| Order Complete Bonus | $60 × week (`day-loop`) |
| Dusk bonus | ×1.25 on dusk sales (`day-loop`) |
| Steal-back | Recovered goods sell normally (`rival-farmers`) |
| Sell-back | Upgrades refund 50% (Shop and Sunday only) |

### Sinks
| Sink | Amount |
|---|---|
| Respawn fee | per knockout, see formula; charged to the wallet, attributed to the knocked-out player |
| Repairs | Bridge plank $30, wagon wheel $50, fence section $20 (`bridge-hazards`, `wagon-vehicles`) |
| Landlord fines | $25–$150 (`landlord-events`) |
| Upgrades | $100–$1,500 (`farm-upgrades`) |
| Sunday quota | `week-quota` |
| Theft | Crows steal **$10 per hit** from the wallet, max $50/day (`night-creatures`); rival farmers steal goods, not cash (`rival-farmers`) |

### Market demand
- Each morning, every GoodsId rolls `D_day ∈ [0.8, 1.2]` (seeded, shown on the order board as arrows: "Pigs ▲").
- **Saturation**: the first `N_sat = 6` units of a GoodsId sold that day get full demand; each further unit multiplies by 0.95, floor 0.6.
- `Market.DemandMult` Day Modifier applies on top.

## 4. Formulas

**Demand**
```
D(g, n) = clamp( D_day(g) × 0.95^max(0, n − N_sat) × Market.DemandMult , 0.6, 1.5 )
```
`n` = 1-based index of this unit among units of g sold today.

**Respawn fee**
```
Fee(w) = round_to_5( 25 × (1 + 0.25 (w − 1)) × Economy.RespawnFeeMult )
```
→ W1 $25, W2 $30, W3 $40, W4 $45, W5 $50, W6 $55. Applies to every knockout that sends a player to the barn respawn (`player-character`), including stranded-at-dusk.

**Income tax modifier** (Landlord rule): `Credit = round(V × (1 − Economy.IncomeTaxRate))`.

### Economy model (canonical, 4 players, median team)

Expected gross per day `G(w) = 400 × 1.25^(w−1)` (growth from upgrades, more species, order bonus and skill). Costs (respawns, repairs, fines) ≈ 15–20% of gross. Quota from `week-quota`: `Q(w) = 1200 × 1.35^(w−1)`.

| Week | Gross/day | Gross/week | Costs (~18%) | Net before upgrades | Quota | Net ÷ Quota |
|---|---|---|---|---|---|---|
| 1 | 400 | 2,000 | 360 | 1,640 | 1,200 | 1.37 |
| 2 | 500 | 2,500 | 450 | 2,050 | 1,620 | 1.27 |
| 3 | 625 | 3,125 | 565 | 2,560 | 2,190 | 1.17 |
| 4 | 780 | 3,900 | 700 | 3,200 | 2,950 | 1.08 |
| 5 | 975 | 4,880 | 880 | 4,000 | 3,990 | 1.00 |
| 6 (Eviction/Region 2+) | 1,220 | 6,100 | 1,100 | 5,000 | 5,380 | 0.93 |

Reading: Weeks 1–2 leave ~$400–450 of slack to buy upgrades (learning weeks). Week 4–5 the median team only clears if its earlier upgrades returned more than they cost and it banked a carryover. Quota grows 1.35×/week but median income only 1.25×/week, so **upgrade choices are the skill that closes the gap** (Pillar 3). Target outcomes: first-attempt Region 1 clear (5 weeks) ≈ 25% of crews; ≥ 60% by third attempt; median first run ends on Week 4 or 5 Sunday.

### Worked example: Week 3, 4 players (median-ish crew)

Quota W3 = **$2,190**. Respawn fee W3 = **$40**. Order bonus = **$180**. Carryover from Week 2 = **$300**. Landlord weekly rule: "Muddy Boots Are a Violation" (inspection fines).

| Day | Twist / events | Sales | Order bonus | Respawns | Repairs | Fines | Upgrades | Day net |
|---|---|---|---|---|---|---|---|---|
| Mon | Clear | 560 | — | 2 × 40 = 80 | 30 | — | Electric Fence −250 | +200 |
| Tue | Fog; crew takes dusk run | 510 + dusk 180 | — | 4 × 40 = 160 (two stranded at dusk) | — | — | — | +530 |
| Wed | Storm (bridge washed out at 4:00) | 340 | — | 2 × 40 = 80 | 2 planks 60 | Inspection 75 | — | +125 |
| Thu | Mandrake curse | 430 | 180 | 2 × 40 = 80 | 30 | — | — | +500 |
| Fri | Clear, fence keeps pigs penned | 540 | 180 | 1 × 40 = 40 | — | — | — | +680 |
| **Total** | | **2,560** | **360** | **−440** | **−150** | **−75** | **−250** | **+2,035** |

Sunday balance = 300 + 2,035 = **$2,335** vs quota **$2,190** → paid, **$145 carries into Week 4**.

Why this is tight but beatable:
- One more stranded dusk (3 players × $40 + lost cargo ≈ $200) fails the week. The dusk run on Tuesday was the swing: +$180 bonus vs +$80 extra respawns.
- Counterfactual: buying the **Bigger Wagon ($400)** on Wednesday instead of saving would add ≈ +$120/day for Thu–Fri (+$240) → $2,175 → **short by $15**. Good purchase, wrong week; it pays back in Week 4.
- The Electric Fence (−$250) returned ≈ +$90/day Wed–Fri (+$270): net +$20 this week, + ~$150/week after. Upgrade payback target ≤ 1.5 weeks (`farm-upgrades`).
- If short, Sunday Collateral (`week-quota`) would let them sell the Fence back for $125 and survive with ≈ −$0 margin: a dramatic but legal save.

## 5. Edge Cases
- **Two transactions same tick**: processed in server receive order; balance check per transaction.
- **Upgrade purchase race** (two players click last $300): first server-received wins; second gets "the wallet says no" bark.
- **Player leaves while owing**: fees already charged stay; Ledger keeps their name.
- **Negative balance at Sunday**: quota payment needs Balance ≥ Q; negative balance is effectively added to the shortfall.
- **Integer overflow** in Eviction: Balance clamped at 2,000,000,000; Eviction quota is capped below that (`eviction-mode`).
- **Saturation exploit** (selling 30 chickens): floor 0.6 keeps it viable but worse than diversified loads; order lines push diversity.
- **Solo**: same wallet rules; quota scales (`week-quota`).

## 6. Dependencies
- **Upstream**: `goods-value` (V, D consumer), `day-loop` (sale acceptance, OCB, day params).
- **Downstream**: `week-quota` (Sunday payment), `farm-upgrades` (spend, sell-back), `landlord-events` (fines, tax), `blame-ledger` (transactions → blame), `player-character` (respawn trigger), `bridge-hazards`/`wagon-vehicles` (repair costs), `night-creatures` (Crow theft), `rival-farmers`, `hud-menus` (wallet display, payout), `save-persistence` (balance, txn summary).

## 7. Tuning Knobs
| Knob | Default | Range | Effect |
|---|---|---|---|
| Starting balance | $100 | 0–300 | first-day shop option |
| Overdraft floor | −$200 | −500–0 | forgiveness for death spirals |
| Respawn base / growth | $25 / +25%/wk | 10–50 / 0–50% | death cost |
| `D_day` range | 0.8–1.2 | 0.9–1.1 to 0.6–1.4 | daily variety |
| `N_sat` / decay / floor | 6 / 0.95 / 0.6 | 4–10 / 0.9–0.98 / 0.4–0.8 | anti-farming |
| Gross growth target | 1.25×/wk | 1.2–1.3 | must stay < quota growth |
| Sell-back rate | 50% | 25–75% | Sunday safety valve |

## 8. Acceptance Criteria
1. Wallet balance identical on all clients within 1 replication tick after any transaction; only the server mutates it.
2. Sum of all `FWalletTxn.Amount` for a run equals final balance − starting balance (audit test).
3. 7th pig sold in a day at `D_day` 1.0 has D = 0.95; 20th pig has D = 0.6 (floor; 0.95^14 = 0.49).
4. Respawn fee table W1–W6 = 25/30/40/45/50/55.
5. A fee that would push balance from −$180 to −$220 sets balance to −$200 and adds $20 to this week's quota.
6. Simulation harness: replaying the Week 3 worked example inputs yields Sunday balance $2,335.
7. Playtest KPI: median 4-player crew Net ÷ Quota in Weeks 1–3 within ±10% of the model table.

## Comedy beats
- Respawn fees play a sad coin-drain sound attributed by name: "Dave −$40 (died of bull)".
- Overdraft: the wallet HUD turns into a red IOU note written by Grandpa.
- Crows physically fly off with coins from the wallet sign at the barn; you can chase them.
- Saturation: the market vendor's dialogue gets increasingly tired ("...another chicken. Wonderful.").
