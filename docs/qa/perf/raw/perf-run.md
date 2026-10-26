# Perf capture `run` -- 2026-10-25 16:55 UTC

- Unity 6000.3.26f1, Null on Null Device, Apple M2 Pro, 16384 MB RAM
- Editor batchmode PlayMode, offscreen 1920x1080 RT, render mode: manual Camera.Render per frame
- Scene Muddlebury, host + 3 bot farmers, critters spawned 140, weather Rain, night True, stitches 1, foxes 0
- Duration 60 s, frames 7034, spawned NetworkObjects 30, bot deliveries 15, bot shouts 0, errors logged 72113
- Quality level Ultra, shadow distance 150, URP asset HP_URP

| Metric | avg | p50 | p95 | p99 | max | samples |
|---|---|---|---|---|---|---|
| FrameMs (wall) | 8.53 | 8.13 | 10.57 | 12.55 | 139.85 | 7034 |
| MainThreadMs | 0 | 0 | 0 | 0 | 0 | 7034 |
| RenderThreadMs | n/a | | | | | 0 |
| DrawCalls | 0 | 0 | 0 | 0 | 0 | 7034 |
| Batches | 0 | 0 | 0 | 0 | 0 | 7034 |
| SetPass | 0 | 0 | 0 | 0 | 0 | 7034 |
| Triangles | 0 | 0 | 0 | 0 | 0 | 7034 |
| ShadowCasters | 0 | 0 | 0 | 0 | 0 | 7034 |
| GcAllocKB | 0 | 0 | 0 | 0 | 0 | 7034 |
| GcAllocCount | 0 | 0 | 0 | 0 | 0 | 7034 |
| TotalUsedMB | 0 | 0 | 0 | 0 | 0 | 7034 |
| SystemUsedMB | 0 | 0 | 0 | 0 | 0 | 7034 |
| GcUsedMB | 0 | 0 | 0 | 0 | 0 | 7034 |
| TextureMB | 0 | 0 | 0 | 0 | 0 | 7034 |
| MeshMB | 0 | 0 | 0 | 0 | 0 | 7034 |
| PhysicsMs | 0 | 0 | 0 | 0 | 0 | 7034 |
| ScriptsUpdateMs | 0 | 0 | 0 | 0 | 0 | 7034 |
| ScriptsLateUpdateMs | 0 | 0 | 0 | 0 | 0 | 7034 |
| FixedUpdateMs | 0 | 0 | 0 | 0 | 0 | 7034 |
| ActiveBodies | 0 | 0 | 0 | 0 | 0 | 7034 |
