# Engine Notes — Unity Implementation Map

HARVEST PANIC is built on **Unity 6.3 LTS (URP, C#)** (engine switch made on
2026-10-19). The GDDs under `design/` use Unity terms; where a design doc still
uses a generic engine concept, read it through this table. The previous engine
build is archived on branch `archive/unreal-final`.

| Engine concept in GDDs | Unity equivalent for HARVEST PANIC |
|------------------------|------------------------------------|
| Tunable property | `[SerializeField]` field; shared tuning in a `ScriptableObject` asset |
| Data asset / data table | `ScriptableObject` (or JSON/CSV under `Assets/**/Data/`) |
| Global service (game-instance / world scope) | Plain C# service registered at bootstrap, or a `ScriptableObject` service; avoid static singletons for game state |
| Gameplay object / component | `GameObject` + `MonoBehaviour` components; prefabs |
| Gameplay tuning without code | Prefab variants + `ScriptableObject` data; no visual scripting planned |
| Level / map | Scene (`.unity`) |
| AI state tree / behavior tree | Custom C# FSM (explicit transition table) or Unity Behavior package |
| AI perception | Custom sensing (overlap queries + line-of-sight raycasts, NonAlloc) |
| Navigation mesh | AI Navigation package (`NavMeshSurface`, `NavMeshAgent`) |
| Physics | Built-in PhysX 3D physics (Unity Physics only if DOTS is adopted) |
| Ability / effect system | n/a — custom ability/effect components in C# |
| Gameplay tags | Enums or `ScriptableObject` tag assets |
| UI framework | UI Toolkit (UXML/USS) for menus/HUD; uGUI where world-space UI is simpler |
| Input mapping | Input System package (`.inputactions`) |
| Online platform / sessions | Steamworks via Facepunch.Steamworks + Netcode for GameObjects transport |
| Replication / RPCs | NGO `NetworkVariable`, `[Rpc]` methods, `NetworkObject` ownership |
| VFX | VFX Graph (or Shuriken particles for simple effects) |
| Global illumination / high-density geometry | URP lighting (baked/mixed + light probes); LODs |
| Procedural audio / sound cues | `AudioSource`/`AudioMixer` (or FMOD/Wwise if later approved) |
| Automated tests | Unity Test Framework (NUnit) EditMode/PlayMode |
