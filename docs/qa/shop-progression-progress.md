# Shop / Upgrades / Week loop / Save — progress (feature/shop-progression)

## Done
- FarmShop logic, 13 upgrade SOs, Shop + Collateral phases, propose/host-confirm, sell-back 50%, run save (CRC, .bak, versioned), Continue Farm in main menu, run-over block on EVICTED receipt with Back to Lobby / New Run.
- Tests: EditMode 146/146, PlayMode 29 passed / 7 skipped (Explicit QA). Shop screenshot docs/qa/screens/unity-economy-shop.png.

## Gaps / next
- Vote is host-confirm (not 10 s majority); no Sunday "Give up" vote by all players (host button only).
- Single save slot (GDD: 3); no guest checkpoint copy; save not written on host quit outside Shop.
- No 2-peer PlayMode test of client proposal RPC; Collateral UI not screenshotted.
- Electric Fence / Guard Dog / Bigger Wagon not implementable yet (no consumers).
