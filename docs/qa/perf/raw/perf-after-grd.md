# Perf capture `after-grd` -- 2026-10-20 17:58 UTC

- Unity 6000.3.26f1, Metal on Apple M2 Pro, Apple M2 Pro, 16384 MB RAM
- Editor batchmode PlayMode, offscreen 1920x1080 RT, render mode: player loop
- Scene Muddlebury, host + 3 bot farmers, critters spawned 13, weather Rain, night True, stitches 1, foxes 0
- Duration 60 s, frames 15860, spawned NetworkObjects 25, bot deliveries 18, bot shouts 26, errors logged 0
- Quality level Ultra, shadow distance 150, URP asset HP_URP

| Metric | avg | p50 | p95 | p99 | max | samples |
|---|---|---|---|---|---|---|
| FrameMs (wall) | 3.78 | 3.6 | 4.34 | 4.98 | 950.3 | 15860 |
| MainThreadMs | 3.78 | 3.6 | 4.36 | 4.98 | 945.63 | 15860 |
| RenderThreadMs | n/a | | | | | 0 |
| DrawCalls | 507.96 | 507 | 546 | 549 | 550 | 15860 |
| Batches | 507.96 | 507 | 546 | 549 | 550 | 15860 |
| SetPass | 38.59 | 38 | 41 | 41 | 41 | 15860 |
| Triangles | 592306.51 | 589067 | 622713 | 623417 | 624149 | 15860 |
| ShadowCasters | 171.63 | 166 | 207 | 208 | 213 | 15860 |
| GcAllocKB | 1.94 | 1.66 | 1.66 | 1.95 | 2434.26 | 15860 |
| GcAllocCount | 38.28 | 38 | 38 | 41 | 4163 | 15860 |
| TotalUsedMB | 1075.15 | 1101.03 | 1112.27 | 1114.28 | 1118.78 | 15860 |
| SystemUsedMB | 1891.7 | 1892.91 | 1896.92 | 1898.42 | 1903.92 | 15860 |
| GcUsedMB | 621.75 | 644.24 | 659.12 | 660.45 | 663.23 | 15860 |
| TextureMB | 133.63 | 134.3 | 134.3 | 134.3 | 134.3 | 15860 |
| MeshMB | 10.04 | 10.04 | 10.04 | 10.04 | 10.04 | 15860 |
| PhysicsMs | 0.03 | 0 | 0.15 | 0.23 | 2.76 | 15860 |
| ScriptsUpdateMs | 0.12 | 0.11 | 0.18 | 0.22 | 7.54 | 15860 |
| ScriptsLateUpdateMs | 0.08 | 0.07 | 0.1 | 0.11 | 0.41 | 15860 |
| FixedUpdateMs | 0 | 0 | 0.02 | 0.1 | 0.33 | 15860 |
| ActiveBodies | 1.86 | 2 | 6 | 6 | 6 | 15860 |
