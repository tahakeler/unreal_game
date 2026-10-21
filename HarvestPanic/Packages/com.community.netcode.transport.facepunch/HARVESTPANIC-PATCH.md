# HARVEST PANIC: embedded copy of the Facepunch transport

This folder is an **embedded package** (Unity Manual: "Embedded dependencies"). Unity loads it
instead of any registry or git copy of `com.community.netcode.transport.facepunch`.

- Upstream: https://github.com/Unity-Technologies/multiplayer-community-contributions
  `Transports/com.community.netcode.transport.facepunch` @ `0fab638470379ace12b0149dfb41c043d23dbce5`
- Every file here is byte-identical to upstream, `.meta` GUIDs included. `NetworkManager.prefab` references
  `FacepunchTransport` by GUID `44dd247a00edbfa44b77868d755af6ea`, so keep the `.meta` files.
- **One change:** `Runtime/Facepunch/` has been removed. It held Facepunch.Steamworks 2.3.2, and its native macOS
  library had only i386/x86_64 slices (no arm64). Its Linux managed DLL was also enabled for macOS players, so the
  macOS build shipped the Linux variant and no Steam native library at all.
  Facepunch.Steamworks now lives in `Assets/Plugins/Steamworks/`, with explicit per-platform import settings.

## Updating the transport

1. Copy the upstream `package.json`, `*.md` and `Runtime/*.cs`, `Runtime/*.asmdef` with their `.meta` files.
2. **Never copy `Runtime/Facepunch/`.** `HarvestPanic.Editor.SteamPluginCheck` fails every player build, and the
   EditMode test `SteamPluginCheckTests` fails, if any Facepunch.Steamworks or steam_api plugin is found outside
   `Assets/Plugins/Steamworks/`.
3. To change Facepunch.Steamworks itself, replace the managed DLLs and native libraries in
   `Assets/Plugins/Steamworks/` together, from one Facepunch.Steamworks release. The native flat-API exports must
   match the managed DLL's imports. Keep the existing `.meta` files.
