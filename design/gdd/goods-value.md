# Goods Value & Bruising

> **Status**: Draft
> **Author**: economy-designer + systems-designer (Batch C)
> **Last Updated**: 2026-10-10
> **Implements Pillar**: Heavy Together, Blamed Together · Failure Is the Content
> **Quick reference**: Layer `Core` · Priority `VS` · Key deps `physics-carry` · **Owns contract: Goods Item**

## Summary

Every pig, chicken, egg, turnip and prize pumpkin is a **Goods Item** with a base dollar value. Physics impacts add bruise points, crops lose freshness over time, and both shrink the sale price. This system turns "Dave dropped the pumpkin" into an exact dollar loss that the Blame Ledger can print.

## 1. Overview

Goods Value is the single source of truth for what any sellable object is worth at the moment it crosses into the market. It owns the Goods Item data contract, the bruise model (impulse → bruise points → bruise state → value multiplier), the freshness model for crops and products, and the final sale-value formula used by `day-loop` and `shared-wallet-economy`. Every value loss is emitted as a Blame Event (owned by `blame-ledger`) with the responsible player attached.

## 2. Player Fantasy

"We are carrying money, and money is screaming and trying to leave." Players should feel every bump as a cash register going *ka-chunk* backwards. The value floating above a pig should be the most-watched number in the game, and a pristine prize pumpkin arriving at market should feel like landing a plane on fire.

## 3. Detailed Rules

### Contract: Goods Item

Owned here. All consumers (`physics-carry`, `wagon-vehicles`, `livestock-roster`, `cursed-crops`, `blame-ledger`, `shared-wallet-economy`, `rival-farmers`) read/write only through these fields. Implemented as a replicated `UGoodsItemComponent` on the actor plus a `UGoodsDefinition` primary data asset.

| Field | Type | Owner (writes) | Notes |
|---|---|---|---|
| `GoodsId` | FName | data asset | e.g. `Livestock.Pig`, `Crop.PrizePumpkin` |
| `Category` | enum {Livestock, Crop, Product, Special} | data asset | Product = eggs, milk, wool |
| `BaseValue` | int32 ($) | data asset | before any multiplier |
| `Fragility` | float 0.25–2.0 | data asset | scales bruise gain |
| `CarryHands` | int32 1–8 | data asset | **hands** required to lift (each player = 2 hands; carriers = ceil(CarryHands/2)); consumed by physics-carry |
| `MassKg` | float | data asset | physics mass |
| `BruisePoints` | float 0–100 | goods-value (server) | replicated |
| `BruiseState` | enum {Pristine, Scuffed, Bruised, Busted, Ruined} | goods-value (derived) | replicated |
| `Freshness` | float 0.5–1.0 | goods-value (server) | 1.0 for Livestock |
| `FreshnessClockStart` | float (server time) | goods-value | set on harvest/lay; −1 = not started |
| `bIsAlive` | bool | livestock/crops | Living goods can flee |
| `LastHandlerId` | PlayerId | physics-carry | last player to grab/throw/drop; physics-carry also supplies the touch history (last 3 touchers within the 3 s TouchWindow owned by `blame-ledger`) |
| `Tags` | GameplayTagContainer | any (additive) | `Goods.Golden`, `Goods.Cursed`, `Goods.OrderItem`, `Goods.Pocketed`, `Goods.Stolen` |
| `ValueModifierMult` | float | day-loop modifiers | product of active item-scoped Day Modifiers, default 1.0 |

Events emitted: `OnBruised(Item, DeltaBP, DeltaDollars, InstigatorId)`, `OnStateChanged(Item, Old, New)`, `OnSold(Item, FinalValue)`. Every `OnBruised` with `DeltaDollars ≥ $1` raises a Blame Event (verb = `Bruised` (generic hit) / `Dropped` / `Threw` / `Crashed` (vehicle impact) / `DroppedInRiver`; the Ledger's $2 noise floor then applies).

### Starting value table (Region 1)

Canonical for **all** docs (consistency pass 2026-10-10): `livestock-roster` and `cursed-crops` mirror these values. Hands = `CarryHands` in hands (carriers = ceil(hands/2)).

| GoodsId | Category | Base $ | Fragility | Hands (carriers) | Phase |
|---|---|---|---|---|---|
| Chicken | Livestock | 15 | 0.6 | 1 (1) | VS |
| Egg | Product | 5 | 2.0 | 1 (1) | VS |
| Golden Egg | Special | 150 | 2.0 | 1 (1) | Alpha |
| Pig | Livestock | 60 | 0.8 | 2 (1) | VS |
| Prize Pumpkin | Crop | 300 | 1.5 | 8 (4) | VS |
| Goat | Livestock | 70 | 0.5 | 2 (1) | Alpha |
| Cow | Livestock | 150 | 0.7 | 6 (3) | Alpha |
| Bull | Livestock | 200 | 0.4 | 6 (3) | Alpha |
| Turnip | Crop | 10 | 1.0 | 1 (1) | Alpha |
| Mandrake | Crop | 45 | 1.2 | 1 (1) | Alpha |
| Growing Pumpkin | Crop | 40 × 1.05^(t_held/10), cap 2.5× (`cursed-crops`) | 1.3 | 2→8 (1→4) | Alpha |
| Golden Cob (Maze Corn) | Crop | 40 | 1.0 | 1 (1) | EA |
| Tumble Melon | Crop | 80 | 1.4 | 4 (2) | EA |
| Moonberries (handful) | Crop | 15 | 1.8 | 1 (1) | EA |
| Sheep / Wool | Livestock / Product | 80 / 20 | 0.6 / 0.3 | 2 (1) / 1 (1) | EA |
| Duck | Livestock | 35 | 0.6 | 1 (1) | EA |
| Prize Llama | Livestock | 400 | 0.9 | 4 (2) | EA |

### Bruise rules
1. Server evaluates every physics hit on a goods actor (from `physics-carry` hit callbacks) and every damage source (river, creature, bull, fall).
2. Hits below the impulse threshold do nothing. Bruise points accumulate; they never heal **except** livestock, which recover 5 BP per 30 s while in a pen (animals calm down; crops don't).
3. Bruise state is derived from BP: Pristine <10, Scuffed 10–34, Bruised 35–64, Busted 65–99, Ruined 100.
4. **Ruined** crops/products are worth $0 but still sellable as "compost" for $1 (so the Ledger can still say "sold the pumpkin for $1"). Ruined livestock instead become **Grumpy**: value floored at 25% and they gain +50% flee speed (handled by `critter-ai` via tag `Goods.Grumpy`).
5. Water: an item fully submerged takes 15 BP/s (crops) or 5 BP/s (livestock, except Duck = 0).
6. Attribution: BP delta is attributed via the touch history (`LastHandlerId` primary) if within the 3 s TouchWindow of release, otherwise to the environmental source (`Bridge`, `Bull`, `Scarecrow`, `Nobody`).

### Freshness rules
1. Crops start the clock when harvested (pulled from ground); Products when laid/produced. Livestock never lose freshness.
2. Grace period, then linear decay to a floor of 0.5. Night (dusk phase) pauses decay ("it's cold").
3. Freshness persists across days only for goods stored in the Barn Cellar upgrade; otherwise unsold goods left in the field at day end despawn (livestock return to the wild spawn pool).

### Sale value
When an item is accepted by a market zone (`day-loop`), the sale value is computed once, rounded to the nearest whole dollar, and credited to the wallet by `shared-wallet-economy`.

## 4. Formulas

**Bruise gain per hit**
```
ΔBP = max(0, I − I_min) / I_scale × Fragility × M_bruise
```
- `I` = impulse magnitude (N·s, from Chaos hit), `I_min` = 400 N·s, `I_scale` = 40 N·s per BP, `M_bruise` = Day Modifier param `Goods.BruiseMult` (default 1.0, range 0.5–2.0).
- Example: a pig (Frag 0.8) dropped from shoulder height, I ≈ 1200 → (800/40)×0.8 = **16 BP** → Scuffed. Prize pumpkin (1.5) off the bridge onto rocks, I ≈ 3000 → 65 × 1.5 = **97.5 BP** → Busted.

**Bruise multiplier** `B(state)`: Pristine 1.00 · Scuffed 0.85 · Bruised 0.60 · Busted 0.30 · Ruined 0.00 (crop/product) / 0.25 (livestock).

**Freshness**
```
F(t) = clamp(1 − max(0, t − T_grace) / T_decay, 0.5, 1.0)
```
- Crops: `T_grace` = 120 s, `T_decay` = 480 s (→ 0.5 floor 10 minutes after harvest). Products: `T_grace` = 180 s, `T_decay` = 600 s. Golden Egg never decays.

**Sale value**
```
V = round( BaseValue × B × F × D × O × ValueModifierMult )
```
- `D` = market demand multiplier (owned by `shared-wallet-economy`, range 0.6–1.5).
- `O` = order bonus: 1.5 if the item fills an open line on the day's Morning Order (`day-loop`), else 1.0.
- Output range: $0 – BaseValue × 1.5 × 1.5 × 1.25 (dusk) ≈ 2.8× base.

**Dollar loss for Blame Event**: `ΔDollars = BaseValue × D_expected × (B_old − B_new)` using today's demand for the GoodsId (so the Ledger shows a real-feeling number at the moment of the hit).

## 5. Edge Cases
- **Hit by two players in same frame**: attribution goes to the player with the larger contributed impulse; tie → both get half the $ in the Ledger.
- **Item thrown into market zone while bruising mid-air**: final hit resolves first, then sale. Sale value locked at acceptance.
- **Livestock heals back into Pristine** in a pen: allowed; the Ledger keeps the original loss entry ("damage done") but shows a "+$X recovered" footnote.
- **Pocketed Golden Egg** (`Goods.Pocketed`): value is not added to wallet; tracked by `blame-ledger` for the Sunday reveal. If the pocketing player is knocked out, the egg drops and its tag is cleared.
- **Growing pumpkin** reaching its 8-hand (4-carrier) stage while one player holds it: player is pinned, ΔBP from the drop is attributed to "Pumpkin" (nobody), because it's funnier when nobody is to blame.
- **Item destroyed by out-of-bounds / despawn**: logged as full value loss to `LastHandlerId` if within 3 s, else `Nobody`.
- **Impulse spikes from physics explosions (Chaos jitter)**: per-hit ΔBP capped at 60 BP; ignore hits with I > 20,000 as solver errors.
- **Client prediction**: clients show predicted value float; server state wins, correction tweens over 0.3 s.

## 6. Dependencies
- **Upstream**: `physics-carry` (hit events, LastHandlerId, CarryHands use), `critter-ai` (alive/grumpy behaviour via tags).
- **Downstream**: `day-loop` (sale acceptance, order matching), `shared-wallet-economy` (D, credit), `blame-ledger` (Blame Event), `livestock-roster` and `cursed-crops` (author `UGoodsDefinition` rows), `wagon-vehicles` (transfers hits to cargo), `rival-farmers` (`Goods.Stolen`), `hud-menus` (value float), `audio-feedback` (bruise stingers), `save-persistence` (Barn Cellar contents).

## 7. Tuning Knobs
| Knob | Default | Safe range | Effect |
|---|---|---|---|
| `I_min` | 400 N·s | 250–700 | Lower = everything bruises, frustration |
| `I_scale` | 40 | 25–60 | Bruise sensitivity |
| State thresholds | 10/35/65/100 | ±10 | Width of each tier |
| B multipliers | 1/.85/.6/.3/0 | — | Value cliff steepness |
| Crop `T_grace` / `T_decay` | 120 / 480 s | 60–240 / 300–900 | Urgency of hauling crops |
| Livestock pen heal | 5 BP / 30 s | 0–10 | Forgiveness |
| Water BP/s | 15 / 5 | 5–30 | River lethality |
| Per-hit cap | 60 BP | 40–100 | One-hit ruin protection |
| Base values | table | ±30% | Primary income lever (see economy model in `shared-wallet-economy`) |

## 8. Acceptance Criteria
1. Dropping a pig (Frag 0.8) with measured impulse 1200 N·s produces 16 ±0.1 BP and state Scuffed on server and all clients within 200 ms.
2. A Pristine prize pumpkin filling an order line at demand 1.0, freshness 1.0 sells for exactly $450.
3. Crop freshness: F(100 s) = 1.0, F(240 s) = 0.75, F(600 s) = 0.5, F(900 s) = 0.5 (floor); decay is paused during Dusk.
4. Every value loss ≥ $2 (Ledger noise floor) creates exactly one Blame Event with the correct player within 3 s window; environment-caused losses list a non-player source.
5. No Goods Item consumer writes `BruisePoints`/`BruiseState`/`Freshness` directly (static check: only `UGoodsItemComponent` setters, server-only).
6. 200 goods actors bruising simultaneously costs < 0.3 ms server game-thread per frame.

## Comedy beats
- The floating price tag *drops* with a slot-machine tick on every hit; a prize pumpkin going $450 → $135 in one bounce is a clip.
- "Sold for $1 (compost)" is a valid, celebrated Ledger line.
- Grumpy (ruined) livestock run faster and angrier: the damage you did now chases you.
- Nobody-blame from the growing pumpkin lets the crew argue about whether the pumpkin did it.
