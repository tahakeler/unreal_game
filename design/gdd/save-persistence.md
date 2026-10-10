# Save & Persistence

> **Status**: Draft
> **Author**: systems-designer (Batch C)
> **Last Updated**: 2026-10-10
> **Implements Pillar**: Failure Is the Content (roguelite reset with meaningful keepsakes)
> **Quick reference**: Layer `Core` · Priority `Alpha` · Key deps `week-quota`, `session-networking`

## Summary

Two kinds of save: a host-owned **Run Save** written at every day boundary and wiped on eviction, and a per-player **Account Profile** that keeps cosmetics, unlocks and Almanac forever. This makes failure sting (the run is gone) without making it pointless (your account remembers).

## 1. Overview

The host machine stores up to 3 Run Save slots (one per crew/region attempt). Saves are written only at safe boundaries (Shop phase after Payout, and after Sunday resolution), never mid-day, which keeps physics state out of save files. Each player's Account Profile is local to that player (Steam Cloud synced) and updated by `meta-progression` at Payout/Sunday/Region events.

## 2. Player Fantasy

"We can quit after Wednesday and pick it up tomorrow, and when we lose, we lose for real." Players should trust that nothing is lost between sessions, and feel that eviction is a real, dramatic ending.

## 3. Detailed Rules

### Run Save (host only)
- **Contents**: version, seed, region id, week w, day d, player roster (Steam IDs + S_p sampled Monday), wallet balance + IOU, weekly txn summary (not full log), owned upgrades + levels, active Week/Run Day Modifiers (tags + remaining duration), Landlord rules drawn + Double Down state, Barn Cellar goods (GoodsId, BP, Freshness), Blame Ledger week totals, pocketed-egg flags, Camcorder clip index refs.
- **Write points**: entering Shop phase; after Sunday Paid; on graceful host quit from Shop. Atomic write (temp file + rename), keep 1 backup (`.bak`).
- **Wipe points**: Evicted (week-quota), Region Cleared (after rewards written to profiles), player-chosen Abandon.
- **Guest copy**: on each write the host sends the Run Save to roster clients, who store it as a read-only resume checkpoint (same checksum rules).
- **Load**: host selects slot in lobby; guests not in the original roster may join (quota re-scaled per `week-quota`).
- Mid-day state is never saved: quitting mid-day resumes at that day's Morning (day replays; income from the partial day is lost).

### Account Profile (each player)
- Cosmetics, hats, unlocked shop-pool upgrades, Almanac entries, lifetime stats (pigs dropped, $ lost), region clears, Eviction Mode bests, settings.
- Written on: Payout (stats), Sunday (awards), Region Clear, settings change. Steam Cloud (Mac/PC).
- Host is authoritative for *what happened*; each client writes its own profile from replicated end-of-day award events.

## 4. Formulas
- **Run Save size budget**: ≤ 64 KB (`~2 KB header + 64 B × goods in cellar + 128 B × modifiers + ledger summary`).
- **Write time budget**: ≤ 50 ms on game thread (async serialize to buffer, async file write).
- **Checksum**: CRC32 over payload; mismatch → load `.bak`; both bad → slot marked corrupt (not deleted).
- **Version migration**: `SaveVersion` int; loader applies migrations `v_n → v_n+1` sequentially; unknown future version refuses to load.

## 5. Edge Cases
- **Host crashes mid-day**: on reload, resume from Shop save of the previous day (day replays).
- **Host crashes during Sunday**: Sunday re-runs from Friday Shop save (result is deterministic: same balance).
- **Crash during save write**: temp file discarded; previous save intact.
- **Different host wants to continue**: supported via host migration-lite: every roster client keeps a replicated read-only copy of the latest Run Save (`session-networking`), so any roster player can host the Resume. No live mid-day migration. "Export run code" for non-roster hosts is Post.
- **Save scumming eviction** (kill app on Sunday): Sunday result is written *before* the cutscene plays; reopening gives the evicted state.
- **Guest edits their profile file**: cosmetic-only impact; profiles are not trusted for gameplay; the shop pool uses the **host's** profile unlocks only, to prevent injection.
- **Disk full**: warn, keep playing, retry at next boundary.

## 6. Dependencies
- **Upstream**: `week-quota` (wipe/write triggers), `session-networking` (roster, host identity).
- **Data sources**: `shared-wallet-economy`, `farm-upgrades`, `day-loop` (Day Modifiers with Week/Run duration), `landlord-events`, `goods-value` (cellar items), `blame-ledger`, `camcorder-replay`.
- **Downstream**: `meta-progression`, `eviction-mode` (best scores), `region-campaign`, `hud-menus` (slot UI).

## 7. Tuning Knobs
| Knob | Default | Range |
|---|---|---|
| Run slots | 3 | 1–5 |
| Backup count | 1 | 1–3 |
| Autosave points | Shop + Sunday | — |
| Size budget | 64 KB | 32–256 KB |

## 8. Acceptance Criteria
1. Quit at Wednesday Shop, reload: wallet, upgrades, week/day, Landlord rules, cellar goods byte-identical.
2. Eviction removes the run slot; Account Profile retains new Almanac entries earned that run.
3. Killing the process during write leaves a loadable previous save (fault-injection test, 100 runs).
4. Save write ≤ 50 ms game-thread on M1 base model; file ≤ 64 KB with 6 cellar goods and 12 modifiers.
5. A v1 save loads in a v2 build via migration; a v3 save in a v2 build is refused with a message.

## Comedy beats
- Abandon-run confirmation: "Grandpa would be... honestly, not surprised."
- Slot thumbnails are the most recent Camcorder disaster, not a farm screenshot.
- Lifetime stat on the profile: "Total pigs drowned: 47".
