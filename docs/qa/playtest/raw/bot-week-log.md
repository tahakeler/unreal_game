# Bot week soak log -- 2026-10-21 07:07 UTC

- Day scale 0.05, host + 3 bots, reached W2D2 Morning, shop visits 6, Sundays 1
- Wallet: start 100, end 421, sum of txns 321
- Spawned NetworkObjects: first Morning 24, max at Morning 24, end 24
- Memory (Unity total allocated): first Morning 286 MB, end 279 MB
- Errors/exceptions logged: 0; issues: 0

| Day | Quota | Workday s | Weather | Start $ | End $ | Sales $ (units) | Bot deliveries | Shouts | Disasters | Night (Stitches/Foxes) | NetObjs@AM | Goods@AM | Critters@AM | Mem MB | Mono MB | Modifiers |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| W1D1 | 540 | 27 | Clear | 100 | 191 | 91 (3) | 3 | 19 | 0  | yes (1/0) | 24 | 5 | 13 | 286 | 644 | Mod.Weather.Day.Clear |
| W1D2 | 540 | 27 | Clear | 191 | 299 | 108 (3) | 3 | 20 | 0  | yes (1/1) | 24 | 5 | 13 | 291 | 648 | Mod.Weather.Day.Clear |
| W1D3 | 540 | 27 | Rain | 99 | 214 | 115 (3) | 3 | 14 | 0  | yes (1/2) | 24 | 5 | 13 | 291 | 662 | Mod.Upgrade.MarketBell, Mod.Weather.Day.Rain |
| W1D4 | 540 | 27 | Storm | 214 | 465 | 191 (3) | 3 | 14 | 2 Bruised:Crop.PrizePumpkin DroppedInRiver:Product.Crate | yes (1/2) | 24 | 5 | 13 | 287 | 726 | Mod.Upgrade.MarketBell, Mod.Weather.Day.Storm |
| W1D5 | 540 | 27 | Heatwave | 115 | 248 | 133 (3) | 3 | 18 | 2 DroppedInRiver:Product.Crate Bruised:Crop.PrizePumpkin | yes (1/2) | 24 | 5 | 13 | 283 | 650 | Mod.Upgrade.MarketBell, Mod.Upgrade.StallSign, Mod.Weather.Day.Heatwave |
| W2D1 | 730 | 27 | Fog | 50 | 421 | 251 (3) | 3 | 16 | 1 Bruised:Crop.PrizePumpkin | yes (1/3) | 24 | 5 | 13 | 275 | 687 | Mod.Upgrade.MarketBell, Mod.Upgrade.StallSign, Mod.Landlord.QuietHours, Mod.Weather.Day.Fog |
| W2D2 | 730 | 0 | Heatwave | 421 | 0 | 0 (0) | -18 | -102 | 0  | no | 24 | 5 | 13 | 280 | 732 | Mod.Upgrade.MarketBell, Mod.Upgrade.StallSign, Mod.Landlord.QuietHours, Mod.Landlord.Visit.Inspection, Mod.Weather.Day.Heatwave |

## Receipts
- W1D1: units 3, sales 91, headline "A quiet day. Grandpa is worried."
- W1D2: units 3, sales 108, headline "A quiet day. Grandpa is worried."
- W1D3: units 3, sales 115, headline "A quiet day. Grandpa is worried."
- W1D4: units 3, sales 191, headline "The Nobody: bruised the prize pumpkin ×2"
- W1D5: units 3, sales 133, headline "A quiet day. Grandpa is worried."
- W2D1: units 3, sales 251, headline "The Nobody: bruised the prize pumpkin"

## Goods unsold at market close
- W1D1: Crop.PrizePumpkin@106m, Product.Crate@7m
- W1D2: Crop.PrizePumpkin@176m, Product.Crate@44m
- W1D3: Crop.PrizePumpkin@176m, Product.Crate@43m
- W1D4: Product.Crate@60m, Product.Crate@85m
- W1D5: Crop.PrizePumpkin@165m, Product.Crate@44m
- W2D1: Product.Crate@63m, Product.Crate@85m

## Rent shortfalls (bots alone)
- W1: balance 248 vs due 540 (quota 540 + IOU 0); bots short by 292

## Shop / Sunday
- W1D1: browsed (6 offers), bought nothing
- W1D2: try buy Upgrade.MarketBell -> BOUGHT (balance 299 -> 99)
- W1D3: browsed (6 offers), bought nothing
- W1D4: try buy Upgrade.StallSign -> BOUGHT (balance 465 -> 115)
- W1D5: try buy Upgrade.GrandpasBoots -> not bought (balance 248 -> 248)
- W1D5: DEV TOP-UP +342 so the Sunday-paid path is exercised
- SUNDAY W1: due 540 (quota 540 + IOU 0), paid=True, carryover 50, MVD The Weather, line "PAID. The Landlord counts it twice, slowly."
- W2D1: browsed (6 offers), bought nothing

## Landlord barks
- W2D1 Morning: New house rule this week. Quiet Hours: voices carry further. Whisper.
- W2D2 Morning: Don't mind me. Just a friendly little look around.

## Issues

## Errors (deduped, first 40)

## Phase trace
W1D1:Morning->Workday | W1D1:Workday->Dusk | W1D1:Dusk->Payout | W1D1:Payout->Shop | W1D2:Shop->Morning | W1D2:Morning->Workday | W1D2:Workday->Dusk | W1D2:Dusk->Payout | W1D2:Payout->Shop | W1D3:Shop->Morning | W1D3:Morning->Workday | W1D3:Workday->Dusk | W1D3:Dusk->Payout | W1D3:Payout->Shop | W1D4:Shop->Morning | W1D4:Morning->Workday | W1D4:Workday->Dusk | W1D4:Dusk->Payout | W1D4:Payout->Shop | W1D5:Shop->Morning | W1D5:Morning->Workday | W1D5:Workday->Dusk | W1D5:Dusk->Payout | W1D5:Payout->Shop | W1D5:Shop->Sunday | W2D1:Sunday->Morning | W2D1:Morning->Workday | W2D1:Workday->Dusk | W2D1:Dusk->Payout | W2D1:Payout->Shop | W2D2:Shop->Morning
