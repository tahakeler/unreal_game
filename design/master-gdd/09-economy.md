# 9. Economy

> **Owner**: systems-designer · **Sources**: `design/gdd/shared-wallet-economy.md`, `goods-value.md`, `week-quota.md`, `landlord-events.md`, `day-loop.md`, `consistency-report.md`; code `HarvestPanic/Assets/Scripts/Economy/` (`Wallet`, `Market`, `EconomyMath`, `EconomyTuning`, `Receipt`, `EconomyDirector`)
> **Convention**: canonical values from the GDDs/code; new values are marked **(proposed)**.

---

## 9.1 Design Intent

The whole crew shares **one wallet**. Money comes in from market sales and goes out through respawn fees, repairs, fines, upgrades and the Sunday rent. The economy has one job: make every bump, drop and death cost a visible, attributable dollar amount, while keeping rent **tight but beatable**.

The core lever: **rent grows ×1.35 per week, but median income grows only ×1.25 per week.** The gap is closed by good upgrade choices and banked carryover, which is pillar 3, "The Farm Gets Worse, You Get Better".

**Status in build:** Built (the wallet, market, sale value and quota are implemented and covered by EditMode/PlayMode tests).

---

## 9.2 Shared Wallet Rules

| Rule | Value |
|---|---|
| Wallets per run | 1, whole dollars, written only by the host and replicated to everyone |
| Starting balance | **$100** (Week 1, Day 1) |
| Transaction types | Sale, OrderBonus, RespawnFee, Repair, Fine, Upgrade, SellBack, Quota, Theft, Refund. Each one records amount, player, goods, day and time |
| Carryover | The balance carries across days and weeks; it is wiped on eviction |
| Audit | The sum of all transactions = final balance − starting balance |
| Balance cap | $2,000,000,000 (overflow guard) |

Because every transaction names a player, the receipt can say "Dave −$40 (died of bull)".

**Status in build:** Built (`Wallet.cs`, audit test).

---

## 9.3 Income (Faucets)

| Faucet | Rule | Typical size (4p) |
|---|---|---|
| Market sale | Sale value `V` (§9.4), credited the moment the pen accepts the item | 80–90% of income |
| Order Complete Bonus | `$60 × week`, once per day when all 3 lines are filled | $60–300/day |
| Dusk bonus | ×1.25 on Night Market sales | +$100–260 per dusk |
| Golden Egg (Donate) | $150 on Sunday if the pocketing player donates | Rare |
| Steal-back | Goods recovered from a rival cart sell normally; 30% chance of a bonus good worth $50–150 | Occasional |
| Sell-back | Upgrades refund `floor(0.5 × price)` (Shop or Sunday Collateral) | Emergency only |
| Deed Day | After the 5th paid Sunday all goods sell at 2× (no quota) | Celebration |

**Status in build:** Partial. Sales, the order ×1.5 multiplier, the OCB and the dusk bonus are Built. Golden eggs, steal-back, sell-back and Deed Day are Planned.

---

## 9.4 What an Item Is Worth

```
V = round( BaseValue × B × F × D × O × M )
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `BaseValue` | int $ | 5–400 | From the goods table (below) |
| `B` | float | 0–1 | Bruise multiplier: Pristine 1.00 · Scuffed 0.85 · Bruised 0.60 · Busted 0.30 · Ruined 0 (crops; sold as $1 compost) / 0.25 (livestock) |
| `F` | float | 0.5–1.0 | Freshness. Crops: full for 120 s after harvest, then linear to 0.5 over 480 s. Products: 180 s / 600 s. Livestock always 1.0 |
| `D` | float | 0.6–1.5 | Market demand (below) |
| `O` | float | 1.0 / 1.5 | 1.5 if the item fills an open order line |
| `M` | float | 0.25–3.0 | Day Modifier value multiplier, × DuskBonus (1.25) at night |
| `V` | int $ | $0–~2.8× base | Credited sale value |

Then, if the Landlord's Cut is active: `Credit = round(V × (1 − IncomeTaxRate))`.

**Bruise points** come from impacts: `ΔBP = max(0, I − 400) / 40 × Fragility × BruiseMult`, capped at 60 BP per hit. Tiers: <10 Pristine, 10–34 Scuffed, 35–64 Bruised, 65–99 Busted, 100 Ruined. Penned livestock heal 5 BP per 30 s.

**Demand**

```
D(g, n) = clamp( D_day(g) × 0.95^max(0, n − 6) × Market.DemandMult , 0.6, 1.5 )
```
`D_day` is rolled each morning in [0.8, 1.2] per good. `n` is the 1-based unit index of that good sold today. The first 6 units sell at full demand; each later unit costs 5% more, down to a floor of 0.6. This discourages flooding the market with one good.

**Worked examples**
- Pristine Prize Pumpkin, order line, D 1.0, fresh: `300 × 1 × 1 × 1 × 1.5 = $450`. The same sale at dusk: `450 × 1.25 = 562.5 → $563`.
- Scuffed pig, D_day 1.1, not on the order: `60 × 0.85 × 1.1 = 56.1 → $56`.
- 7th pig of the day at D_day 1.0: `D = 0.95`. 20th pig: `0.95^14 = 0.49 → floored to 0.6`.
- $450 pumpkin under Landlord's Cut (5%): `round(427.5) = $428`.

**Region 1 goods table (canonical)**

| Good | Base $ | Fragility | Hands (carriers) | Phase |
|---|---|---|---|---|
| Egg | 5 | 2.0 | 1 (1) | VS |
| Chicken | 15 | 0.6 | 1 (1) | VS |
| Pig | 60 | 0.8 | 2 (1) | VS |
| Prize Pumpkin | 300 | 1.5 | 8 (4) | VS |
| Turnip / Mandrake | 10 / 45 | 1.0 / 1.2 | 1 (1) | Alpha |
| Goat / Cow / Bull | 70 / 150 / 200 | 0.5 / 0.7 / 0.4 | 2 / 6 / 6 | Alpha |
| Golden Egg | 150 | 2.0 | 1 | Alpha |
| Growing Pumpkin | 40 × 1.05^(t/10 s), cap 2.5× | 1.3 | 2→8 | Alpha |
| Sheep / Wool, Duck, Tumble Melon, Moonberries, Golden Cob, Prize Llama | 80/20, 35, 80, 15, 40, 400 | — | — | EA |
| *Egg Crate (build only)* | *30* | *1.5* | *2* | *needs a canonical row* |
| *Carrot (build only)* | *8* | *0.8* | *1* | *needs a canonical row* |

**Status in build:** Built (formulas in `EconomyMath`; VS goods plus Turnip, Carrot and Egg Crate exist as assets). Egg Crate and Carrot need to be added to `goods-value.md`.

---

## 9.5 Costs (Sinks)

| Sink | Amount | Trigger |
|---|---|---|
| Respawn fee | $25–55 by week (§9.6) | Barn respawn only: drowning/out of bounds, night-creature knockout, stranded. Daytime knockouts recover in place for free |
| Repairs | Plank patch **$30**, wagon wheel **$50**, fence section **$20**, wagon river recovery **$40**, lost-tool restock **$15** | When used |
| Landlord fines/fees | $25–150 per event (§9.10) | Visits, rules |
| Region twist | Region 1 Bridge Toll **$5 per crossing with goods** (waived by Bridge Reinforcement); Region 5 Heating Bill 8% of quota per day | Region rule |
| Sunday Best hats | $5 each at market | Landlord rule |
| Upgrades | $20–1,100 | Shop vote |
| Theft | Crows $10/hit, max $50/day, never pushes the balance below $0; rivals steal goods, not cash | Night/rivals |
| Sunday rent | `Q(w) + IOU` | Sunday |

Target: day-to-day costs (fees, repairs, fines) ≈ **15–20% of gross**. The Landlord's share is ≈ 3–6% of weekly gross.

**Status in build:** Partial. Respawn fees are Built and charged by the player and river code. The wallet supports Fine/Theft/Upgrade rules. Repairs, fines, tolls, theft and the shop have no emitters yet.

---

## 9.6 Respawn Fee

```
Fee(w) = round_to_5( 25 × (1 + 0.25 × (w − 1)) × Economy.RespawnFeeMult )
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `w` | int | 1–6+ | Week |
| `RespawnFeeMult` | float | 0–5 | Modifier (Respawn Paperwork ×1.2; Insurance halves) |
| `Fee` | int $ | 0–~300 | Charged per barn respawn |

| Week | 1 | 2 | 3 | 4 | 5 | 6 |
|---|---|---|---|---|---|---|
| Fee | $25 | $30 | $40 | $45 | $50 | $55 |
| With Respawn Paperwork | $30 | $40 | $45 | $55 | $60 | $65 |

**Worked example.** Week 3 under Paperwork: `25 × 1.5 × 1.2 = 45 → $45`.

**Status in build:** Built.

---

## 9.7 Rent: The Weekly Quota

```
Q(w) = round_to_10( Q_base × G^(w − 1) × S_p × R_mult )
Due  = Q(w) + IOU_w (+ Double Down term)
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `Q_base` | $ | 900–1,600 | Default $1,200 |
| `G` | float | 1.25–1.45 | Default 1.35; must stay above income growth (1.25) |
| `S_p` | float | 0.45/0.65/0.85/1.00 | 1/2/3/4 players, sampled Monday |
| `R_mult` | float | 1.00–2.00 | Region difficulty 1.00 / 1.20 / 1.45 / 1.70 / 2.00 |
| `Q` | int $ | $540–~8,000 (campaign) | Rent due |

**Region 1 by player count**

| Week | 4p | 3p | 2p | 1p | Daily share (4p) |
|---|---|---|---|---|---|
| 1 | 1,200 | 1,020 | 780 | 540 | 240 |
| 2 | 1,620 | 1,380 | 1,050 | 730 | 324 |
| 3 | 2,190 | 1,860 | 1,420 | 980 | 438 |
| 4 | 2,950 | 2,510 | 1,920 | 1,330 | 590 |
| 5 | 3,990 | 3,390 | 2,590 | 1,790 | 798 |
| **Total** | **11,950** | 10,160 | 7,760 | 5,370 | — |

Regions 2–5 are listed in Ch. 10 §10.3.

**Worked example.** Week 3, 4p, Region 1: `1,200 × 1.8225 × 1 × 1 = 2,187 → $2,190`. A balance of $2,335 pays it with **$145 carryover**. Exactly equal pays with $0 ("the Landlord seems disappointed").

**Status in build:** Built. Region multiplier is fixed at 1.0 (no region select yet).

---

## 9.8 Wallet Floor, Debt and the IOU

- Fees and fines may push the balance **negative, down to −$200** (overdraft floor).
- Any cost below the floor is capped at −$200 and the excess becomes a **Landlord IOU** added to **this week's Due**. Debt follows you.
- While the balance is below $0 the shop is locked. Upgrades always require Balance ≥ price.
- Theft never takes the balance below $0.
- The IOU is cleared once Sunday is paid.

**Worked example.** Balance −$180, a $40 respawn fee is charged → balance set to −$200, IOU +$20. In Week 3 the Due becomes 2,190 + 20 = $2,210.

**Why:** a hard floor stops a death spiral (four drownings in a row can't bankrupt the run mid-week), while the IOU means debt is never forgiven.

**Status in build:** Built and tested (`Wallet.Apply`).

---

## 9.9 The Payout Receipt

The receipt is where the economy becomes comedy. It prints at Payout (25 s timeout, or Continue).

**Daily receipt (top to bottom)**
1. **Headline**: the day's highest-severity loss ("Dave: dropped the prize pumpkin into the river"). If nothing happened: "A quiet day. Grandpa is worried."
2. **Order board**: each line with filled/required, and the order bonus if complete.
3. **Money block**: Sales, Order bonus, Costs, **Net**, Balance, "Rent due Sunday" (`Q + IOU`).
4. **Blame blocks**, one per player: up to 5 lines (largest losses first, positive lines in green after), "+n more crimes", block total. A "Nature" block ("The Wind −$40") comes last.
5. Host buttons: **Next Day** / Restart.

**Sunday receipt**: "LANDLORD SUNDAY", **PAID** or **EVICTED** stamp, Due (quota + IOU), carryover, MVD, and a one-liner ("PAID. The Landlord counts it twice, slowly." / "EVICTED. The Landlord is towing the barn. The pig is on the roof."). Buttons: **Next Week** or **Try Again**.

**Status in build:** Built (`ReceiptBuilder`, `EconomyHud`; paper-ledger skin). The dot-matrix animation and the receipt timeout are Planned.

---

## 9.10 Landlord Events

The Landlord never hurts anyone. He makes things worse economically.

**Weekly rules**: one is drawn after each paid Sunday (none in Week 1), with no repeats. Up to 3 are active; the 4th replaces the oldest. They are Minor and do not count toward the chaos cap.

| Rule | Effect | Built |
|---|---|---|
| Landlord's Cut | Income tax +5% on every sale | Yes |
| Early Curfew | Workday −45 s | Yes |
| Respawn Paperwork | Respawn fee ×1.2 | Yes |
| Quiet Hours | Voice carries ×1.25 (at dusk) | Yes (applies all day in the build) |
| No Running on the Bridge | Plank break ×1.3 | Yes |
| Muddy Boots Are a Violation | Inspections fine $25 per muddy player | No |
| Sunday Best | Livestock sold without a hat −10% (hats $5) | No |
| One Pumpkin Per Family | No repeated goods in the order; +1 order line | No |

**Visits** (Major; 1/week from Week 2, 2/week from Week 4, on Tue–Thu)

| Visit | Rules | $ impact |
|---|---|---|
| Inspection | 120 s window, 3 checks; fine per failed check `25 × (1 + 0.2(w − 2))` capped at $150 → W2 $25, W3 $30, W4 $35, W5 $40 per check | −$50 to −$150 |
| Farm Walk | 90 s; $50 per hit on him, max 3 hits; opens one pen gate | $0 to −$150 + escapes |
| Helpful Visit | Plank break ×0.5 today; $100 fee at Payout | −$100 |
| Double Down | 20 s vote at Morning, once per run, never in the final week: this Due ×2, next week waived | ± a whole week |
| Missed appointment | A visit deferred past Friday | −$50 Sunday |

**Double Down check:** accept only if carryover + this week's net ≥ 2 × Q(w). Week 2 4p: Due $3,240 against a typical net of ~$2,050, so it is a trap unless the crew is carrying ≥ $1,200.

**Inspection worked example.** Week 4, 3 failed checks: `min(150, 3 × 35) = $105`.

**Status in build:** Partial. 5 of 8 weekly rules are Built, with draw, cap and replacement. Visits are Planned.

---

## 9.11 Balance Targets

**Canonical model (4 players, median crew):** gross per day `G(w) = 400 × 1.25^(w − 1)`, costs ≈ 18%.

| Week | Gross/day | Gross/week | Costs | Net | Quota | Net ÷ Quota |
|---|---|---|---|---|---|---|
| 1 | 400 | 2,000 | 360 | 1,640 | 1,200 | 1.37 |
| 2 | 500 | 2,500 | 450 | 2,050 | 1,620 | 1.27 |
| 3 | 625 | 3,125 | 565 | 2,560 | 2,190 | 1.17 |
| 4 | 780 | 3,900 | 700 | 3,200 | 2,950 | 1.08 |
| 5 | 975 | 4,880 | 880 | 4,000 | 3,990 | 1.00 |
| 6 | 1,220 | 6,100 | 1,100 | 5,000 | 5,380 | 0.93 |

**Expected earnings per day by player count (proposed):** `G_p(w) = 400 × 1.25^(w−1) × S_p`. Income scales with the same factor as rent, so Net ÷ Quota is the same for every crew size.

| Week | 4p | 3p | 2p | 1p |
|---|---|---|---|---|
| 1 | 400 | 340 | 260 | 180 |
| 2 | 500 | 425 | 325 | 225 |
| 3 | 625 | 531 | 406 | 281 |
| 4 | 781 | 664 | 508 | 352 |
| 5 | 977 | 830 | 635 | 439 |
| Per player per day, W1 | $100 | $113 | $130 | $180 |

The per-player target rises for small crews because fewer hands means more trips per person.

**KPIs:** ≥ 80% of first-time 4p crews pay Week 1; ≤ 50% pay Week 5 on the first attempt; ~25% clear Region 1 on the first run, ≥ 60% by the third; median first run ends on the Week 4–5 Sunday; measured Net ÷ Quota within ±10% of the model for Weeks 1–3.

**Worked week (Week 3, 4p, $300 carryover)**: sales $2,560 + order bonuses $360 − respawns $440 − repairs $150 − fines $75 − Electric Fence $250 = **+$2,035** → Sunday balance $2,335 vs $2,190 → paid, **$145** carried. One more stranded dusk (≈ −$200) would fail the week. Buying the Bigger Wagon ($400) on Wednesday instead would have left the crew $15 short: a good buy in the wrong week.

**Status in build:** Planned (the model is not yet validated in playtests; the `/balance-check` simulation is pending).

---

## 9.12 Sink / Faucet Diagram and Feedback Loops

```
                FAUCETS                                   SINKS
  Market sales (V) ───────┐                      ┌──► Respawn fees ($25–55)
  Order bonus ($60×w) ────┤                      ├──► Repairs ($15–50)
  Dusk ×1.25 ─────────────┼──►  SHARED WALLET ───┼──► Landlord fines/fees
  Golden egg donate ──────┤   (floor −$200,      ├──► Tolls / hats / Heating Bill
  Steal-back / sell-back ─┘    IOU → Due)        ├──► Crow theft (≥$0)
                                  │              ├──► UPGRADES ──┐
                                  │              └──► SUNDAY RENT Q(w)+IOU
                                  │                              │
                                  └──── more income ◄────────────┘
```

| Loop | Type | Intent |
|---|---|---|
| Upgrades → more income → more upgrades | Positive | Intended; capped by quota growth outpacing income |
| Market saturation (0.95 per unit after 6) | Negative | Stops one-good farming |
| Fee growth per week | Negative | Deaths matter more later |
| Deaths → negative balance → shop locked | Positive (spiral) | Damped by the −$200 floor and IOU |
| Carryover → safety → riskier dusks | Positive | Intended drama |

**Status in build:** Partial (the wallet loops exist; the upgrade loop is Planned).

---

## 9.13 Open Issues

1. Repair price: $30 plank (canonical) vs the cheaper $10 the original bridge doc proposed. Awaiting owner confirmation.
2. Per-player death surcharge on top of the weekly fee: the owner to decide.
3. Livestock values were halved during consistency (pig $60, cow $150). Re-check the $400/day model with real order mixes.
4. Upgrade price range: the economy doc says $25–1,100 but the catalogue's cheapest is $20 (Burlap Sack). Use $20–1,100.
5. Solo fees are not scaled: a $25 death is 4.6% of the solo W1 rent vs 2.1% for 4p. **Proposed:** scale the fee by `S_p^0.5`.

**Status in build:** n/a.
