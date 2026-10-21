# Bot week soak log -- 2026-10-21 07:26 UTC

- Day scale 0.05, host + 3 bots, reached W2D2 Morning, shop visits 6, Sundays 1
- Wallet: start 100, end 392, sum of txns 292
- Spawned NetworkObjects: first Morning 24, max at Morning 24, end 24
- Memory (Unity total allocated): first Morning 283 MB, end 277 MB
- Errors/exceptions logged: 0; issues: 0

| Day | Quota | Workday s | Weather | Start $ | End $ | Sales $ (units) | Bot deliveries | Shouts | Disasters | Night (Stitches/Foxes) | NetObjs@AM | Goods@AM | Critters@AM | Mem MB | Mono MB | Modifiers |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| W1D1 | 540 | 27 | Rain | 100 | 199 | 99 (3) | 3 | 19 | 0  | yes (1/0) | 24 | 5 | 13 | 283 | 635 | Mod.Weather.Day.Rain |
| W1D2 | 540 | 27 | Wind | 199 | 301 | 102 (3) | 3 | 20 | 0  | yes (1/1) | 24 | 5 | 13 | 290 | 659 | Mod.Weather.Day.Wind |
| W1D3 | 540 | 27 | Fog | 101 | 203 | 102 (3) | 3 | 13 | 0  | yes (1/2) | 24 | 5 | 13 | 290 | 692 | Mod.Upgrade.MarketBell, Mod.Weather.Day.Fog |
| W1D4 | 540 | 27 | Heatwave | 203 | 476 | 213 (3) | 3 | 13 | 1 Bruised:Crop.PrizePumpkin | yes (1/2) | 24 | 5 | 13 | 285 | 719 | Mod.Upgrade.MarketBell, Mod.Weather.Day.Heatwave |
| W1D5 | 540 | 27 | Storm | 326 | 416 | 90 (3) | 3 | 18 | 4 DroppedInRiver:Product.Crate DroppedInRiver:Crop.PrizePumpkin Bruised:Crop.PrizePumpkin DroppedInRiver:Product.Crate | yes (1/2) | 24 | 5 | 13 | 290 | 643 | Mod.Upgrade.MarketBell, Mod.Upgrade.Megaphone, Mod.Weather.Day.Storm |
| W2D1 | 730 | 27 | Clear | 50 | 392 | 222 (3) | 3 | 16 | 1 Bruised:Crop.PrizePumpkin | yes (1/3) | 24 | 5 | 13 | 277 | 685 | Mod.Upgrade.MarketBell, Mod.Upgrade.Megaphone, Mod.Upgrade.GrandpasBoots, Mod.Landlord.OnePumpkinPerFamily, Mod.Weather.Day.Clear |
| W2D2 | 730 | 0 | Clear | 392 | 0 | 0 (0) | -18 | -100 | 0  | no | 24 | 5 | 13 | 278 | 730 | Mod.Upgrade.MarketBell, Mod.Upgrade.Megaphone, Mod.Upgrade.GrandpasBoots, Mod.Landlord.OnePumpkinPerFamily, Mod.Landlord.Visit.Inspection, Mod.Weather.Day.Clear |

## Receipts
- W1D1: units 3, sales 99, headline "A quiet day. Grandpa is worried."
- W1D2: units 3, sales 102, headline "A quiet day. Grandpa is worried."
- W1D3: units 3, sales 102, headline "A quiet day. Grandpa is worried."
- W1D4: units 3, sales 213, headline "The Nobody: bruised the prize pumpkin ×2"
- W1D5: units 3, sales 90, headline "The River: dropped the prize pumpkin into the river"
- W2D1: units 3, sales 222, headline "The Nobody: bruised the prize pumpkin ×2"

## Goods unsold at market close
- W1D1: Crop.PrizePumpkin@107m, Product.Crate@7m
- W1D2: Crop.PrizePumpkin@176m, Product.Crate@44m
- W1D3: Crop.PrizePumpkin@176m, Product.Crate@43m
- W1D4: Product.Crate@60m, Product.Crate@85m
- W1D5: Crop.PrizePumpkin@165m, Product.Crate@44m
- W2D1: Product.Crate@62m, Product.Crate@84m

## Rent shortfalls (bots alone)
- W1: balance 166 vs due 540 (quota 540 + IOU 0); bots short by 374

## Shop / Sunday
- W1D1: browsed (6 offers), bought nothing
- W1D2: try buy Upgrade.MarketBell -> BOUGHT (balance 301 -> 101)
- W1D3: browsed (6 offers), bought nothing
- W1D4: try buy Upgrade.Megaphone -> BOUGHT (balance 476 -> 326)
- W1D5: try buy Upgrade.GrandpasBoots -> BOUGHT (balance 416 -> 166)
- W1D5: DEV TOP-UP +424 so the Sunday-paid path is exercised
- SUNDAY W1: due 540 (quota 540 + IOU 0), paid=True, carryover 50, MVD The Weather, line "PAID. The Landlord counts it twice, slowly."
- W2D1: browsed (6 offers), bought nothing

## Landlord barks
- W2D1 Morning: New house rule this week. One Pumpkin Per Family: one of each item per order, but one more line.
- W2D2 Morning: Don't mind me. Just a friendly little look around.

## Issues

## Errors (deduped, first 40)

## Phase trace
W1D1:Morning->Workday | W1D1:Workday->Dusk | W1D1:Dusk->Payout | W1D1:Payout->Shop | W1D2:Shop->Morning | W1D2:Morning->Workday | W1D2:Workday->Dusk | W1D2:Dusk->Payout | W1D2:Payout->Shop | W1D3:Shop->Morning | W1D3:Morning->Workday | W1D3:Workday->Dusk | W1D3:Dusk->Payout | W1D3:Payout->Shop | W1D4:Shop->Morning | W1D4:Morning->Workday | W1D4:Workday->Dusk | W1D4:Dusk->Payout | W1D4:Payout->Shop | W1D5:Shop->Morning | W1D5:Morning->Workday | W1D5:Workday->Dusk | W1D5:Dusk->Payout | W1D5:Payout->Shop | W1D5:Shop->Sunday | W2D1:Sunday->Morning | W2D1:Morning->Workday | W2D1:Workday->Dusk | W2D1:Dusk->Payout | W2D1:Payout->Shop | W2D2:Shop->Morning
