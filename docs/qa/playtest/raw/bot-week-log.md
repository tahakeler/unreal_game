# Bot week soak log -- 2026-10-20 18:27 UTC

- Day scale 0.05, host + 3 bots, reached W2D2 Morning, shop visits 6, Sundays 1
- Wallet: start 100, end 339, sum of txns 239
- Spawned NetworkObjects: first Morning 24, max at Morning 24, end 24
- Memory (Unity total allocated): first Morning 281 MB, end 284 MB
- Errors/exceptions logged: 0; issues: 0

| Day | Quota | Workday s | Weather | Start $ | End $ | Sales $ (units) | Bot deliveries | Shouts | Disasters | Night (Stitches/Foxes) | NetObjs@AM | Goods@AM | Critters@AM | Mem MB | Mono MB | Modifiers |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| W1D1 | 540 | 27 | Clear | 100 | 176 | 76 (3) | 15 | 19 | 1 Bruised:Crop.PrizePumpkin | yes (1/0) | 24 | 5 | 13 | 281 | 576 | Mod.Weather.Day.Clear |
| W1D2 | 540 | 27 | Storm | 176 | 265 | 89 (3) | 16 | 20 | 3 Bruised:Crop.PrizePumpkin DroppedInRiver:Crop.PrizePumpkin DroppedInRiver:Product.Crate | yes (1/1) | 24 | 5 | 13 | 283 | 601 | Mod.Weather.Day.Storm |
| W1D3 | 540 | 29 | Fog | 65 | 145 | 80 (3) | 14 | 16 | 1 Bruised:Crop.PrizePumpkin | yes (1/2) | 24 | 5 | 13 | 283 | 646 | Mod.Upgrade.MorningCoffee, Mod.Weather.Day.Fog |
| W1D4 | 540 | 29 | Wind | 145 | 263 | 58 (3) | 19 | 19 | 2 Bruised:Crop.PrizePumpkin Bruised:Crop.PrizePumpkin | yes (1/2) | 24 | 5 | 13 | 284 | 592 | Mod.Upgrade.MorningCoffee, Mod.Weather.Day.Wind |
| W1D5 | 540 | 29 | Wind | 113 | 204 | 91 (3) | 13 | 20 | 3 Bruised:Crop.PrizePumpkin Bruised:Crop.PrizePumpkin DroppedInRiver:Product.Crate | yes (1/2) | 24 | 5 | 13 | 284 | 641 | Mod.Upgrade.MorningCoffee, Mod.Upgrade.Megaphone, Mod.Weather.Day.Wind |
| W2D1 | 730 | 29 | Rain | 50 | 339 | 169 (3) | 15 | 17 | 1 Bruised:Crop.PrizePumpkin | yes (1/3) | 24 | 5 | 13 | 289 | 592 | Mod.Upgrade.MorningCoffee, Mod.Upgrade.Megaphone, Mod.Landlord.MuddyBoots, Mod.Weather.Day.Rain |
| W2D2 | 730 | 0 | Clear | 339 | 0 | 0 (0) | -92 | -111 | 0  | no | 24 | 5 | 13 | 284 | 653 | Mod.Upgrade.MorningCoffee, Mod.Upgrade.Megaphone, Mod.Landlord.MuddyBoots, Mod.Weather.Day.Clear |

## Receipts
- W1D1: units 3, sales 76, headline "The Nobody: bruised the prize pumpkin"
- W1D2: units 3, sales 89, headline "The Nobody: bruised the prize pumpkin"
- W1D3: units 3, sales 80, headline "The Nobody: bruised the prize pumpkin"
- W1D4: units 3, sales 58, headline "The Nobody: bruised the prize pumpkin"
- W1D5: units 3, sales 91, headline "The Nobody: bruised the prize pumpkin"
- W2D1: units 3, sales 169, headline "The Nobody: bruised the prize pumpkin"

## Rent shortfalls (bots alone)
- W1: balance 204 vs due 540 (quota 540 + IOU 0); bots short by 336

## Shop / Sunday
- W1D1: browsed (6 offers), bought nothing
- W1D2: try buy Upgrade.MorningCoffee -> BOUGHT (balance 265 -> 65)
- W1D3: browsed (6 offers), bought nothing
- W1D4: try buy Upgrade.Megaphone -> BOUGHT (balance 263 -> 113)
- W1D5: try buy Upgrade.StallSign -> not bought (balance 204 -> 204)
- W1D5: DEV TOP-UP +386 so the Sunday-paid path is exercised
- SUNDAY W1: due 540 (quota 540 + IOU 0), paid=True, carryover 50, MVD The Weather, line "PAID. The Landlord counts it twice, slowly."
- W2D1: browsed (6 offers), bought nothing

## Landlord barks

## Issues

## Errors (deduped, first 40)

## Phase trace
W1D1:Morning->Workday | W1D1:Workday->Dusk | W1D1:Dusk->Payout | W1D1:Payout->Shop | W1D2:Shop->Morning | W1D2:Morning->Workday | W1D2:Workday->Dusk | W1D2:Dusk->Payout | W1D2:Payout->Shop | W1D3:Shop->Morning | W1D3:Morning->Workday | W1D3:Workday->Dusk | W1D3:Dusk->Payout | W1D3:Payout->Shop | W1D4:Shop->Morning | W1D4:Morning->Workday | W1D4:Workday->Dusk | W1D4:Dusk->Payout | W1D4:Payout->Shop | W1D5:Shop->Morning | W1D5:Morning->Workday | W1D5:Workday->Dusk | W1D5:Dusk->Payout | W1D5:Payout->Shop | W1D5:Shop->Sunday | W2D1:Sunday->Morning | W2D1:Morning->Workday | W2D1:Workday->Dusk | W2D1:Dusk->Payout | W2D1:Payout->Shop | W2D2:Shop->Morning
