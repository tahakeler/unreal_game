# Marker Contract (level -> gameplay)

The level places **plain actors** (TargetPoint, StaticMeshActor, ...) and adds **actor Tags** (Details > Actor > Tags).
`UHPMarkerResolver` (a UWorldSubsystem, `Source/HarvestPanic/World/HPMarkerResolver.*`) scans the world on BeginPlay
on the server/standalone (never on pure clients) and spawns the gameplay actor at the marker's location + rotation.
The level never references gameplay classes. Tags are case-sensitive; a `:N` suffix is an integer count.
Unknown `HP_*` tags are ignored. Each marker actor is processed once. Marker actors stay in the world (hide them yourself if wanted).

| Tag | Marker actor | Result |
|---|---|---|
| `HP_PlayerStart` | any actor (APlayerStart also fine) | Players spawn here. `AHPGameMode::ChoosePlayerStart` prefers tagged actors over APlayerStart; player N uses marker N mod count. No spawn needed. |
| `HP_MarketPen` | box-ish actor | `AHPMarketPen`. Half-extent = the actor's `UBoxComponent` scaled extent if present, else **50 cm x actor scale** (a 100 cm cube mesh scaled 6,6,3 = 600x600x300 cm pen). |
| `HP_CritterSpawner_Pig:<n>` | any | `AHPCritterSpawner` spawning n pigs (default 1) in a ring radius = max(300 cm, 50 cm x max(scaleX, scaleY)). |
| `HP_CritterSpawner_Chicken:<n>` | any | same, chickens. |
| `HP_Goods_PrizePumpkin` | any | `AHPGoodsSpawner` -> one Prize Pumpkin (4 hands, $300 base). Use one marker per pumpkin. |
| `HP_Goods_Crate` | any | one crate (2 hands, $40 base). |
| `HP_BridgePlank` | **one StaticMeshActor per plank** | `AHPBridgePlank` wraps that mesh: SafeLoad 250 kg, 0.2 dmg/kg/s over, cracks at integrity 50, breaks at 0 (mesh falls, $30 Blame, noise 0.75), free repair each Morning, $30 patch via `HPPatch` console exec. Mesh must be on a plank-sized actor (load sensor = actor bounds + 40 cm slab on top). |
| `HP_River` | box-ish actor (scale or UBoxComponent) | `AHPRiverVolume`; flow direction = actor **+X**. Players drown after 1 s (barn respawn + fee + Blame `Drowned`); goods/carryables are pushed downstream and delivered to the nearest pile after 15-25 s marked Soaked; critters swim to the nearest bank. Make the box tall enough to cover the player's capsule in the water. |
| `HP_RiverPile` | any | Spit-out location; assigned to the nearest river. Without one the river's downstream end is used. |
| `HP_Wardrobe` | any | `AHPWardrobe` (Customization module) at the marker. |
| `HP_BarnRespawn` | any | `AHPBarnRespawn`; up to 4 slots on a 120 cm ring (+100 cm Z), facing the marker's rotation. |

Reference level generator: `docs/engine/tools/make_smoke_map.py` (builds `/Game/ThirdPerson/Maps/Lvl_SliceSmoke`).
Headless smoke: `UnrealEditor-Cmd HarvestPanic.uproject /Game/ThirdPerson/Maps/Lvl_SliceSmoke -game -nullrhi -NOSTEAM -ExecCmds="hp.RunSmoke"`.

## Bridge dimensions (canonical)
The level's bridge is **40 m long, 20 planks of 2 m each** (this supersedes the GDD's 24 m / 1.2 m planks; design doc to follow).
Plank behaviour is per mesh actor, so plank length does not matter to the code: SafeLoad, damage rate and the $30 patch apply per plank
regardless of size. Place one `HP_BridgePlank` StaticMeshActor per 2 m plank.
