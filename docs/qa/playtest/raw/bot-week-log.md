# Bot week soak log -- 2026-10-25 16:54 UTC

- Day scale 0.05, host + 3 bots, reached W2D2 Morning, shop visits 6, Sundays 1
- Wallet: start 100, end 155, sum of txns 55
- Spawned NetworkObjects: first Morning 29, max at Morning 40, end 40
- Memory (Unity total allocated): first Morning 283 MB, end 270 MB
- Errors/exceptions logged: 0; issues: 0

| Day | Quota | Workday s | Weather | Start $ | End $ | Sales $ (units) | Bot deliveries | Shouts | Disasters | Night (Stitches/Foxes) | NetObjs@AM | Goods@AM | Critters@AM | Mem MB | Mono MB | Modifiers |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| W1D1 | 540 | 27 | Heatwave | 100 | 181 | 81 (3) | 15 | 19 | 1 Bruised:Crop.PrizePumpkin | yes (1/0) | 29 | 5 | 18 | 283 | 592 | Mod.Weather.Day.Heatwave |
| W1D2 | 540 | 27 | Clear | 181 | 291 | 110 (3) | 17 | 20 | 1 Bruised:Crop.PrizePumpkin | yes (1/1) | 29 | 5 | 18 | 284 | 622 | Mod.Weather.Day.Clear |
| W1D3 | 540 | 27 | Clear | 41 | 150 | 109 (3) | 13 | 17 | 1 Bruised:Crop.PrizePumpkin | yes (1/2) | 35 | 5 | 18 | 289 | 641 | Mod.Upgrade.LanternPosts, Mod.Weather.Day.Clear |
| W1D4 | 540 | 27 | Rain | 150 | 270 | 60 (3) | 25 | 18 | 2 Bruised:Crop.PrizePumpkin Bruised:Crop.PrizePumpkin | yes (1/2) | 38 | 5 | 18 | 284 | 656 | Mod.Upgrade.LanternPosts, Mod.Weather.Day.Rain |
| W1D5 | 540 | 27 | Rain | 70 | 169 | 99 (3) | 9 | 19 | 2 Bruised:Crop.PrizePumpkin Bruised:Crop.PrizePumpkin | yes (1/2) | 39 | 5 | 18 | 290 | 668 | Mod.Upgrade.LanternPosts, Mod.Upgrade.MarketBell, Mod.Weather.Day.Rain |
| W2D1 | 730 | 27 | Wind | 50 | 155 | 105 (3) | 10 | 17 | 1 Bruised:Crop.PrizePumpkin | yes (1/3) | 39 | 5 | 18 | 286 | 676 | Mod.Upgrade.LanternPosts, Mod.Upgrade.MarketBell, Mod.Upgrade.RubberBoots, Mod.Landlord.RespawnPaperwork, Mod.Weather.Day.Wind |
| W2D2 | 730 | 0 | Wind | 155 | 0 | 0 (0) | -89 | -111 | 0  | no | 40 | 5 | 18 | 271 | 597 | Mod.Upgrade.LanternPosts, Mod.Upgrade.MarketBell, Mod.Upgrade.RubberBoots, Mod.Landlord.RespawnPaperwork, Mod.Landlord.Visit.Inspection, Mod.Weather.Day.Wind |

## Receipts
- W1D1: units 3, sales 81, headline "The Nobody: bruised the prize pumpkin"
- W1D2: units 3, sales 110, headline "The Nobody: bruised the prize pumpkin"
- W1D3: units 3, sales 109, headline "The Nobody: bruised the prize pumpkin"
- W1D4: units 3, sales 60, headline "The Nobody: bruised the prize pumpkin"
- W1D5: units 3, sales 99, headline "The Nobody: bruised the prize pumpkin"
- W2D1: units 3, sales 105, headline "The Nobody: bruised the prize pumpkin"

## Rent shortfalls (bots alone)
- W1: balance 19 vs due 540 (quota 540 + IOU 0); bots short by 521

## Shop / Sunday
- W1D1: browsed (6 offers), bought nothing
- W1D2: try buy Upgrade.LanternPosts -> BOUGHT (balance 291 -> 41)
- W1D3: browsed (6 offers), bought nothing
- W1D4: try buy Upgrade.MarketBell -> BOUGHT (balance 270 -> 70)
- W1D5: try buy Upgrade.RubberBoots -> BOUGHT (balance 169 -> 19)
- W1D5: DEV TOP-UP +571 so the Sunday-paid path is exercised
- SUNDAY W1: due 540 (quota 540 + IOU 0), paid=True, carryover 50, MVD The Weather, line "PAID. The Landlord counts it twice, slowly."
- W2D1: browsed (6 offers), bought nothing

## Landlord barks
- W2D2 Morning: Don't mind me. Just a friendly little look around.

## Issues

## Errors (deduped, first 40)

## Phase trace
W1D1:Morning->Workday | W1D1:Workday->Dusk | W1D1:Dusk->Payout | W1D1:Payout->Shop | W1D2:Shop->Morning | W1D2:Morning->Workday | W1D2:Workday->Dusk | W1D2:Dusk->Payout | W1D2:Payout->Shop | W1D3:Shop->Morning | W1D3:Morning->Workday | W1D3:Workday->Dusk | W1D3:Dusk->Payout | W1D3:Payout->Shop | W1D4:Shop->Morning | W1D4:Morning->Workday | W1D4:Workday->Dusk | W1D4:Dusk->Payout | W1D4:Payout->Shop | W1D5:Shop->Morning | W1D5:Morning->Workday | W1D5:Workday->Dusk | W1D5:Dusk->Payout | W1D5:Payout->Shop | W1D5:Shop->Sunday | W2D1:Sunday->Morning | W2D1:Morning->Workday | W2D1:Workday->Dusk | W2D1:Dusk->Payout | W2D1:Payout->Shop | W2D2:Shop->Morning
