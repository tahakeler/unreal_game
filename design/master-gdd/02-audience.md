# 2. Audience, Player Experience & Session Shape

> Owner: Creative Director · Sources: `game-overview.md`, `day-loop.md`, `week-quota.md`, `region-campaign.md`, `onboarding-accessibility.md`, `frontend-lobby.md`, `meta-progression.md`

## 2.1 Target Players

| Segment | Who | What they want | What we give them |
|---|---|---|---|
| **Primary** | Friend groups aged 16–30 already playing R.E.P.O., PEAK, Lethal Company, Chained Together on voice chat | A new game to laugh at together on Friday night, cheap enough that the whole group buys it | A $9.99 premium price, a 4-pack bundle, a one-click Steam invite |
| **Secondary** | Streamers and content creators | Reliable laugh moments roughly every 3 minutes, readable at a glance by viewers | The Bridge as a fixed stage, the Camcorder, tabloid receipts, streamer mode |
| **Tertiary** | Cozy farm fans (Stardew, Overcooked crossover) | The farm fantasy with a chaotic twist | Storybook art, a cozy day, an opt-in night; a possible Cozy Mode later |

**Hardware:** must run on base M-series Macs and mid-range PCs at 1080p (Mac Medium target 60 fps). Stylized, light rendering in URP.

## 2.2 Player Motivations

| Motivation | Satisfied by |
|---|---|
| "I want to laugh with my friends" | Physics failure, friend-throwing, ragdolls, the receipt |
| "I want to beat the Landlord" | Weekly quota, the Sunday cliffhanger |
| "I want to prove it wasn't me" | Blame Ledger, MVD and Pig Whisperer awards |
| "I want to show people" | Camcorder export, Crew Photo |
| "I want to collect and dress up" | Grandpa's Letters, ~120 cosmetics, the Almanac |
| "I want a harder challenge" | Later regions, Eviction Mode leaderboards |

## 2.3 First 10 Minutes

Target: launch to first input in the Lobby Yard in **≤ 25 s**; a rookie hauling pigs within **2 minutes**.

| Time | What happens | Intended feeling |
|---|---|---|
| 0:00 | Title → Host (≤ 3 clicks). The Lobby Yard loads: Grandpa's porch, a mirror, a wardrobe, hay bales to throw | "Oh, I can already mess around" |
| 0:30 | Friends arrive by Steam invite. Someone throws a hay bale at someone. Players ready up by standing on the porch; the host rings the dinner bell | Play before the game starts |
| 1:00 | (First launch only) Grandpa's Tape #1, ~4 min solo: grab a slow pig, cross the bridge, a plank breaks, sell, receipt "−$30 for the plank". Skippable; friends who skipped get a 60 s Quick Tape in the lobby | "I get it" |
| 1:30 | Morning (30 s): the Morning Order (3 lines, e.g. "3 Pigs · 6 Eggs · 1 Prize Pumpkin") and the twist card | A clear goal |
| 2:00 | Workday starts (9:00). The crew splits at the fork in the road: pigs north, chickens south | Ownership, chaos |
| ~3:00 | **First laugh**: a pig wriggles free, a chicken swarm explodes, someone gets carried | The promise kept |
| 5–8 min | First bridge crossing with goods. Planks creak. The prize pumpkin needs 4 people | Tension, coordination |
| 9:30 | Sundown bell, a last-second dive at the Sale Pen with the 2 s photo finish | Climax |
| 10:00 | Payout receipt with named blame lines. "Next Day" | "One more" |

**Success criteria (from the vertical slice):** in a 4-player playtest players laugh within 3 minutes, at least one shareable bridge disaster happens unprompted, and at least 3 of 4 players ask to go again.

## 2.4 First Hour

- **Days 1–4 of Week 1.** The crew learns the pig and chicken personalities, the feed bucket lure and the net, and that a plank costs $30 to patch.
- **First shop visits** between days (Planned): one or two cheap upgrades from the ~$540 Week 1 slack.
- **First Dusk vote**: "Go out at night for 25% more money?" with a Scarecrow visible in the background.
- **First Landlord Sunday** (~60–75 min in): awards, the Golden Egg reveal if someone pocketed one, the slow abacus count. Target: **≥ 80% of first-time 4-player crews pay Week 1.**
- By the end of the hour the crew has a running joke (a nickname, a rival pig) and a reason to come back: a new Landlord rule and a higher quota.

## 2.5 First Week of Ownership (real-world)

| Sessions | Experience |
|---|---|
| Night 1–2 | Weeks 1–3 of Region 1. Cow, goat and bull arrive; weather cards start mattering. Probably a first eviction around Week 3–5 |
| Night 3–4 | Second run with Letters unlocked, a tool sidegrade and better upgrade choices. Median first run ends on the Week 4 or 5 Sunday |
| Night 5–7 | Region 1 clear for committed crews (median clear 5–8 h, ≥ 40% of teams fail at least once first). Deed Day celebration. Region 2 or Eviction Mode unlocks |

Targets: first-attempt Region 1 clear ≈ 25% of crews; ≥ 60% by the third attempt. A new cosmetic every ≤ 45 min in the first 10 h.

## 2.6 Session Length

| Unit | Length | Natural stopping point |
|---|---|---|
| Day | 9:30 without Dusk, ~12:40 with Dusk, ~14 min with payout and shop | The receipt (autosave at Shop) |
| Week | 5 Days + Sunday ≈ 1–2 h | After Sunday payment |
| Region | 5 paid weeks ≈ 5.6–6 h | Deed Day |
| Eviction ladder | 3-day weeks, endless | The eviction |

Every day ends on an autosave so a crew can stop after any receipt and resume later. Any roster player can host the Resume ("migration-lite").

## 2.7 Solo vs 2–4 Players

The game is designed for 4, tuned for 2–4, and playable solo.

| Crew | Quota scale S_p | Week 1 quota | Notes |
|---|---|---|---|
| 1 player | 0.45 | $540 | Playable, not a focus. Awards reduce to MVD + Pig Whisperer with self-deprecating copy |
| 2 players | 0.65 | $780 | Heavy goods are hard; the prize pumpkin (4 carriers) is a stretch goal |
| 3 players | 0.85 | $1,020 | The cow (3 carriers) fully staffs the crew |
| 4 players | 1.00 | $1,200 | The intended experience |

Rules: player count is sampled Monday morning. A mid-week leaver re-scales the quota pro-rata from the next day so a ragequit doesn't doom the others. Joiners can enter at any phase except the Sunday cutscene, spawning at the barn with 2 s protection. Order quantities also scale by S_p.

**Open question (proposed answer):** solo should be "supported, not balanced around" and marketed as such (proposed).

## 2.8 Emotional Curve of a Day

```
Intensity
  ^                                         * dive at the pen
  |                                *   *   * *
  |                      *  bridge*  *        \     payout laugh
  |              *    *   *                     \  *
  |      chase *   *                             \/  \
  |   *                                                 \ shop (calm)
  | * morning                                            *___
  +--------------------------------------------------------------> time
   0:00 0:30          5:00              9:00 9:30   ~12:40  ~14:00
   Order  Workday (spread out, then funnel)  Sundown  (Dusk)  Payout/Shop
```

1. **Morning: anticipation.** Read the order, joke about the twist.
2. **Early Workday: playful.** Low stakes, the crew spreads out.
3. **Mid Workday: tension.** Everything funnels to the bridge.
4. **Sundown: panic.** The music shifts, the town bell spooks critters.
5. **Dusk (optional): dread and greed.** ×1.25 value, Night Creatures awake.
6. **Payout: release.** The receipt is half celebration, half court case.
7. **Shop: calm planning.** Flow break before the next day.

## 2.9 Emotional Curve of a Week

| Day | Feeling | Driver |
|---|---|---|
| Monday | Confidence ("loads of time") | New quota on the chalkboard, the farm has moved |
| Tue–Wed | Rhythm and greed | Upgrades come online, Dusk gambles |
| Thursday | Doubt | Math against the quota |
| Friday | Heist-finale desperation | "The Landlord is coming and we are $60 short" |
| Sunday | Courtroom | Awards, egg reveal, slow count, then a hug or a gloriously catastrophic eviction |

Eviction is designed as a payoff, not a punishment: a cutscene where the barn is towed away with the pig on the roof, a Camcorder montage of the week, and Letters that reward failing.

**Status in build:** Partial. Lobby Yard with porch ready-up and dinner bell, the full day phases (Morning → Workday → Dusk → Payout) and the Sunday pay/evict receipt are Built. Tape #1 tutorial, shop, Dusk vote UI, Sunday cutscene and solo-specific award copy are Planned.
