# Bot week soak log -- 2026-10-20 18:11 UTC

- Day scale 0.05, host + 3 bots, reached W2D2 Morning, shop visits 6, Sundays 1
- Wallet: start 100, end 350, sum of txns 250
- Spawned NetworkObjects: first Morning 24, max at Morning 24, end 24
- Memory (Unity total allocated): first Morning 319 MB, end 291 MB
- Errors/exceptions logged: 0; issues: 0

| Day | Quota | Workday s | Weather | Start $ | End $ | Sales $ (units) | Bot deliveries | Shouts | Disasters | Night (Stitches/Foxes) | NetObjs@AM | Goods@AM | Critters@AM | Mem MB | Mono MB | Modifiers |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| W1D1 | 540 | 27 | Clear | 100 | 183 | 83 (3) | 15 | 19 | 1 Bruised:Crop.PrizePumpkin | yes (1/0) | 24 | 5 | 13 | 319 | 577 | Mod.Weather.Day.Clear |
| W1D2 | 540 | 27 | Rain | 183 | 286 | 103 (3) | 16 | 20 | 1 Bruised:Crop.PrizePumpkin | yes (1/1) | 24 | 5 | 13 | 285 | 583 | Mod.Weather.Day.Rain |
| W1D3 | 540 | 27 | Rain | 136 | 225 | 89 (3) | 14 | 16 | 1 Bruised:Crop.PrizePumpkin | yes (1/2) | 24 | 5 | 13 | 290 | 597 | Mod.Upgrade.RubberBoots, Mod.Weather.Day.Rain |
| W1D4 | 540 | 27 | Fog | 225 | 356 | 71 (3) | 15 | 19 | 2 Bruised:Crop.PrizePumpkin Bruised:Crop.PrizePumpkin | yes (1/2) | 24 | 5 | 13 | 287 | 607 | Mod.Upgrade.RubberBoots, Mod.Weather.Day.Fog |
| W1D5 | 540 | 27 | Fog | 156 | 237 | 81 (3) | 14 | 18 | 2 Bruised:Crop.PrizePumpkin Bruised:Crop.PrizePumpkin | yes (1/2) | 24 | 5 | 13 | 292 | 617 | Mod.Upgrade.RubberBoots, Mod.Upgrade.BetterNets, Mod.Weather.Day.Fog |
| W2D1 | 730 | 27 | Heatwave | 50 | 350 | 180 (3) | 14 | 16 | 1 Bruised:Crop.PrizePumpkin | yes (1/3) | 24 | 5 | 13 | 287 | 628 | Mod.Upgrade.RubberBoots, Mod.Upgrade.BetterNets, Mod.Landlord.QuietHours, Mod.Weather.Day.Heatwave |
| W2D2 | 730 | 0 | Storm | 350 | 0 | 0 (0) | -88 | -109 | 0  | no | 24 | 5 | 13 | 292 | 644 | Mod.Upgrade.RubberBoots, Mod.Upgrade.BetterNets, Mod.Landlord.QuietHours, Mod.Weather.Day.Storm |

## Receipts
- W1D1: units 3, sales 83, headline "The Nobody: bruised the prize pumpkin"
- W1D2: units 3, sales 103, headline "The Nobody: bruised the prize pumpkin"
- W1D3: units 3, sales 89, headline "The Nobody: bruised the prize pumpkin"
- W1D4: units 3, sales 71, headline "The Nobody: bruised the prize pumpkin"
- W1D5: units 3, sales 81, headline "The Nobody: bruised the prize pumpkin"
- W2D1: units 3, sales 180, headline "The Nobody: bruised the prize pumpkin"

## Rent shortfalls (bots alone)
- W1: balance 237 vs due 540 (quota 540 + IOU 0); bots short by 303

## Shop / Sunday
- W1D1: browsed (6 offers), bought nothing
- W1D2: try buy Upgrade.RubberBoots -> BOUGHT (balance 286 -> 136)
- W1D3: browsed (6 offers), bought nothing
- W1D4: try buy Upgrade.BetterNets -> BOUGHT (balance 356 -> 156)
- W1D5: try buy Upgrade.LanternPosts -> not bought (balance 237 -> 237)
- W1D5: DEV TOP-UP +353 so the Sunday-paid path is exercised
- SUNDAY W1: due 540 (quota 540 + IOU 0), paid=True, carryover 50, MVD The Weather, line "PAID. The Landlord counts it twice, slowly."
- W2D1: browsed (6 offers), bought nothing

## Landlord barks

## Issues

## Errors (deduped, first 40)

## Phase trace
W1D1:Morning->Workday | W1D1:Workday->Dusk | W1D1:Dusk->Payout | W1D1:Payout->Shop | W1D2:Shop->Morning | W1D2:Morning->Workday | W1D2:Workday->Dusk | W1D2:Dusk->Payout | W1D2:Payout->Shop | W1D3:Shop->Morning | W1D3:Morning->Workday | W1D3:Workday->Dusk | W1D3:Dusk->Payout | W1D3:Payout->Shop | W1D4:Shop->Morning | W1D4:Morning->Workday | W1D4:Workday->Dusk | W1D4:Dusk->Payout | W1D4:Payout->Shop | W1D5:Shop->Morning | W1D5:Morning->Workday | W1D5:Workday->Dusk | W1D5:Dusk->Payout | W1D5:Payout->Shop | W1D5:Shop->Sunday | W2D1:Sunday->Morning | W2D1:Morning->Workday | W2D1:Workday->Dusk | W2D1:Dusk->Payout | W2D1:Payout->Shop | W2D2:Shop->Morning
