# Engine Migration Notes — Unreal → Unity

HARVEST PANIC moved from Unreal Engine 5.8 to **Unity 6.3 LTS (URP, C#)** on
2026-10-19. The GDDs under `design/` were written against Unreal and are **not**
rewritten; read Unreal terms in them through this table. The Unreal build is
archived on branch `archive/unreal-final`.

| Unreal term in GDDs | Unity equivalent for HARVEST PANIC |
|---------------------|------------------------------------|
| `UPROPERTY` / tunable property | `[SerializeField]` field; shared tuning in a `ScriptableObject` asset |
| Data Asset / Data Table | `ScriptableObject` (or JSON/CSV under `Assets/**/Data/`) |
| Subsystem (Game Instance / World) | Plain C# service registered at bootstrap, or a `ScriptableObject` service; avoid static singletons for game state |
| Actor / Actor Component | `GameObject` + `MonoBehaviour` components; prefabs |
| Blueprint (gameplay tuning) | Prefab variants + `ScriptableObject` data; no visual scripting planned |
| Level / Map | Scene (`.unity`) |
| StateTree / Behavior Tree | Custom C# FSM (explicit transition table) or Unity Behavior package |
| AI Perception | Custom sensing (overlap queries + line-of-sight raycasts, NonAlloc) |
| NavMesh / Nav Mesh Bounds | AI Navigation package (`NavMeshSurface`, `NavMeshAgent`) |
| Chaos physics | Built-in PhysX 3D physics (Unity Physics only if DOTS is adopted) |
| Gameplay Ability System (GAS) | n/a — custom ability/effect components in C# |
| Gameplay Tags | Enums or `ScriptableObject` tag assets |
| UMG / CommonUI | UI Toolkit (UXML/USS) for menus/HUD; uGUI where world-space UI is simpler |
| Enhanced Input | Input System package (`.inputactions`) |
| OnlineSubsystem (Steam) / Sessions | Steamworks via Facepunch.Steamworks + Netcode for GameObjects transport |
| Replication / RPCs | NGO `NetworkVariable`, `[Rpc]` methods, `NetworkObject` ownership |
| Niagara | VFX Graph (or Shuriken particles for simple effects) |
| Lumen / Nanite | URP lighting (baked/mixed + light probes); LODs |
| MetaSounds / Sound Cue | `AudioSource`/`AudioMixer` (or FMOD/Wwise if later approved) |
| Unreal Automation tests | Unity Test Framework (NUnit) EditMode/PlayMode |
