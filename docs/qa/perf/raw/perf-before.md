# Perf capture `before` -- 2026-10-20 17:44 UTC

- Unity 6000.3.26f1, Metal on Apple M2 Pro, Apple M2 Pro, 16384 MB RAM
- Editor batchmode PlayMode, offscreen 1920x1080 RT, render mode: player loop
- Scene Muddlebury, host + 3 bot farmers, critters spawned 13, weather Rain, night True, stitches 1, foxes 0
- Duration 60 s, frames 11958, spawned NetworkObjects 25, bot deliveries 15, bot shouts 26, errors logged 0
- Quality level Ultra, shadow distance 150, URP asset HP_URP

| Metric | avg | p50 | p95 | p99 | max | samples |
|---|---|---|---|---|---|---|
| FrameMs (wall) | 5.02 | 4.9 | 5.75 | 6.85 | 67.98 | 11958 |
| MainThreadMs | 5.01 | 4.89 | 5.76 | 6.81 | 67.11 | 11958 |
| RenderThreadMs | n/a | | | | | 0 |
| DrawCalls | 6187.11 | 6177 | 6309 | 6384 | 6388 | 11958 |
| Batches | 632.01 | 627 | 705 | 716 | 717 | 11958 |
| SetPass | 49.4 | 48 | 53 | 56 | 56 | 11958 |
| Triangles | 585021.78 | 582872 | 622765 | 623483 | 623495 | 11958 |
| ShadowCasters | 6241.14 | 6225 | 6398 | 6404 | 6405 | 11958 |
| GcAllocKB | 3.49 | 3.21 | 3.21 | 4.62 | 2435.8 | 11958 |
| GcAllocCount | 76.23 | 76 | 76 | 79 | 372 | 11958 |
| TotalUsedMB | 1162.77 | 1153.17 | 1226.68 | 1228.63 | 1229.12 | 11958 |
| SystemUsedMB | 1974.8 | 1974.36 | 1977.89 | 1978.02 | 1979.94 | 11958 |
| GcUsedMB | 590.17 | 579.73 | 646.3 | 648.25 | 648.73 | 11958 |
| TextureMB | 131.15 | 131.63 | 131.63 | 131.63 | 131.63 | 11958 |
| MeshMB | 151.47 | 151.47 | 151.47 | 151.47 | 151.47 | 11958 |
| PhysicsMs | 0.03 | 0 | 0.16 | 0.19 | 3.39 | 11958 |
| ScriptsUpdateMs | 0.15 | 0.14 | 0.22 | 0.3 | 11.22 | 11958 |
| ScriptsLateUpdateMs | 0.08 | 0.07 | 0.1 | 0.12 | 0.28 | 11958 |
| FixedUpdateMs | 0.01 | 0 | 0.02 | 0.11 | 0.29 | 11958 |
| ActiveBodies | 1.7 | 1 | 6 | 6 | 6 | 11958 |
