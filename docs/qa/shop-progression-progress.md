# Shop / Upgrades / Week loop / Save — progress (feature/shop-progression)

## Done
- Logic: `Economy/Shop/FarmShop.cs` (offers, buy/sell rules, 50% floor refund, proposal queue), `UpgradeDefinition` SO + `UpgradeCatalog`, `RunSave.cs` (versioned JSON, CRC32, tmp+rename, .bak).
- Phases: `DayPhase.Shop` (Payout -> Shop -> Morning/Sunday), `DayPhase.Collateral` (Sunday short + owned upgrades, 60 s sell-back, auto-pay).
- Director partial `EconomyDirector.Shop.cs`: propose (client RPC) / host confirm, server re-validation, modifiers applied/removed, saves on Shop entry, each purchase, paid Sunday; eviction wipes save; Continue via `IRunSaveService` (new Core contract).
- Core add-only: `IRunSaveService`, `DayParams.PlayerStaminaMult`, `DayParams.ToolsNetReachMult` (consumed in Gameplay StaminaModel/PlayerMotor/NetTool).
- 13 upgrade assets via `UpgradeAssetBuilder.BuildUpgrades`.

## Next
- Shop UI (UI Toolkit, EconomyHud style), Collateral + run-over screen, HUD week/day.
- MainMenu "Continue Farm" button (host screen).
- EditMode + PlayMode tests, shop screenshot, full test run, self-review, contracts doc update.
