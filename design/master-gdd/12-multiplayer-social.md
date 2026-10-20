# 12. Co-op, Multiplayer & Social Comedy

> **Sources**: `design/gdd/session-networking.md`, `proximity-voice.md`, `social-comedy-features.md`, `camcorder-replay.md`, `blame-ledger.md`, `frontend-lobby.md`; conflicts resolved per `consistency-report.md`. Built state checked against `HarvestPanic/Assets/Scripts/Networking/` and `UI/`.
> **Pillars served**: Heavy Together, Blamed Together · Failure Is the Content

HARVEST PANIC is a game about doing hard physical jobs badly with friends. Multiplayer is not a mode; it *is* the game. This chapter covers how players get into a farm together, how they talk, how the game turns their mistakes into jokes, and the rules that stop the jokes from turning into griefing.

---

## 12.1 Design Goals

| Goal | What it means for the player |
|---|---|
| **"Hey, jump in."** | A friend gets from a Steam invite to the farm in one click and under 30 seconds. |
| **Late is fine** | A friend who arrives mid-day lands in the chaos, not in a waiting room. |
| **Lag is never the reason the pig died** | The host decides every physics, money and AI result, so all four players see the same disaster. |
| **Blame always lands on a person** | Every costly accident is tied to a named farmer, so it can be laughed about. |
| **Bounded harm** | No player can cost the crew more than a small, capped amount through a prank or a joke. |

---

## 12.2 Hosting and Joining

### Topology
- One player **hosts** (runs the farm) and up to **three** others join, for a maximum of **4 players**. Solo play runs the same way with one player, so there is no separate solo code path.
- The host is the authority for physics objects (goods, critters, wagons, bridge planks), the shared wallet, the day clock, AI and Blame Events. Clients only control their own input and predict their own movement.
- Built on **Unity Netcode for GameObjects (NGO)**. Two transports:
  - **Steam** (ship): Facepunch.Steamworks lobbies, invites and peer-to-peer transport.
  - **LAN / direct address** (development and local play): Unity Transport with UDP broadcast discovery.

### Ways in

| Route | How it works | Status |
|---|---|---|
| **Host a Farm** | Title → Play → Host a Farm. Pick crew size (1–4) and transport, then "Open the Gate". Loads the Lobby Yard. | Built |
| **Steam invite** | Host invites from the Steam overlay or the pause menu ("Invite Friends"). Accepting launches the game straight into the lobby, skipping the title screen (`+connect_lobby` launch argument handled). | Built |
| **Steam Friends tab** | Join a friend's joinable lobby from inside the game. | Built (requires Steam running; otherwise a clear message) |
| **LAN list** | "Farms on your network" list, refreshed by UDP broadcast every 1.5 s while open. | Built |
| **Direct address** | Type an IP/host address. | Built |
| **Lobby code** | 6-letter code without ambiguous letters (no I/O). | Planned |
| **Public server browser** | Columns: host, farm name, players x/4, week/region, ping, language, in-progress icon. Filters: hide full, hide in-progress, ping < 150 ms. | Planned (Alpha) |

### Host privacy options (planned)
Friends Only (default) · Invite Only · Public · LAN. The lobby advertises run seed, region, week/day, build version and privacy flag.

### Join rules (built)
The host checks every join request in this order and shows the player a plain-language reason when refused:
1. **Malformed request** → rejected.
2. **Version mismatch** → "Your farm is a different version (vX vs vY)."
3. **Lobby locked** (for example, during the Sunday payout cutscene) → join is held.
4. **Full** → "The farm is full (4/4)."

Player names are cleaned on the host: control characters, invisible characters and rich-text tags are stripped, length is capped, and duplicates become "Dave", "Dave 2". The roster (name, ready, host flag, spawn slot) is kept by the host and mirrored to clients only when it changes (about 110 bytes for 4 players).

Status in build: **Partial** — Steam lobbies/invites, LAN discovery, direct join, approval rules, name cleanup and roster are built and tested; privacy options, lobby codes and server browser are planned.

---

## 12.3 Drop-in, Drop-out and Host Loss

| Situation | What happens |
|---|---|
| **Join mid-run** | Allowed in any phase except the Landlord's Sunday payout cutscene (joiner is queued until it ends). Joiner spawns at the barn with **2 s of spawn protection** and a "late for work" emote. |
| **Client leaves or drops** | Their farmer "faints" (ragdolls), drops whatever they were holding, then poofs into hay after 10 s. Their Blame Ledger entries stay. Their slot is reserved for **5 minutes** so a rejoin restores their Ledger stats and tools. |
| **Bad connection** | Ping over 250 ms shows a "bad connection" icon over that farmer. Over 1000 ms for 15 s → returned to the lobby with a rejoin prompt. |
| **Host leaves ("migration-lite")** | There is no live host migration. The run falls back to the last day-boundary checkpoint (the Shop-phase run save). That save is copied to every roster player when written, so **any** of them can host the Resume. Clients see "Host fled the farm" / "The host went to bed. The run was saved at the start of Day N." |
| **Everyone else leaves** | The host continues solo, and the quota scales down by the player-count scale (0.45 / 0.65 / 0.85 / 1.00 for 1–4 players). |
| **Two players grab the same item on the same frame** | The host gives it to the earlier request; the loser plays a "fumble" animation. |

**Comedy intent**: a disconnect becomes a joke ("Dave died of Wi-Fi"), and a host crash has its own line, so technical failure reads as part of the farm's bad luck rather than as a bug.

**Network budget**: 96–128 kbps from host to each client; physics objects near players update at 30 Hz, mid-distance at 10 Hz, far at 2 Hz. Target: host network cost ≤ 2 ms per frame at 4 players with 150 active physics bodies.

Status in build: **Partial** — joining in-progress and drop handling work at the session level; the faint-on-drop, 5-minute slot reservation, bad-ping icon and Resume-from-checkpoint flow are planned.

---

## 12.4 Proximity Voice and Noise

### The idea
"SHHH, it's right there." Players talk with **3D positional voice**: you hear friends from where they stand, and walls muffle them. Your voice is also a game mechanic. Loud talking creates **Noise Events** that animals, cursed crops, night creatures and rivals can hear. Whispering while sneaking up on a chicken works; screaming when the pig escapes summons the Scarecrow.

### Rules

| Rule | Value |
|---|---|
| Full volume | within 3 m |
| Silent | at 35 m |
| Walls | −9 dB per wall, up to 2 walls |
| Knocked-out player | muffled (−6 dB, low-pass) |
| Respawning player | heard by everyone at 30% for 5 s ("ghost voice") |
| Radio Set upgrade | global push-to-talk channel at 60% with static |
| Voice → Noise Event | when loudness ≥ 0.35 (normal talking), at most once per 0.5 s per player |
| Mic calibration | "speak normally for 3 s" sets your gain so normal speech reads 0.4–0.5 |
| Modes | open mic or push-to-talk; muted players make no voice noise |

Noise radius grows with loudness: `Radius = 4 + 46 × Loudness^1.5` metres (whisper ≈ 4 m, talking ≈ 20 m, yelling ≈ 50 m), adjusted by weather and night. Reference loudness: walk 0.10, sprint 0.25, talk 0.35–0.55, shout 0.8, whistle 0.7, pig squeal 0.7, plank break 0.75, screaming mandrake 0.9.

### Nobody needs a microphone
Every voice mechanic has a button version:
- **Holler** (shout emote): loudness 0.8, 2 s cooldown.
- **Shush** (hold): quiet walk, caps your loudness at 0.2.
- **Wave emote**: a small non-voice "hey" noise.
- **Ping wheel** (planned): Here, Help carry, Pig!, Danger, Wagon, Bridge, Yes, No.
- **Point emote**: places a 3 s world ping up to 60 m away, visible to everyone (built).
- **Host option "Voice draws critters"** (default ON) and **"Calm mode"** (voice never spooks animals; marks the run unranked).

### Text chat
A lobby text chat is part of the Crew overlay (Tab). The host cleans every line (no control or rich-text characters, length cap) and each sender has a rate limit (a small burst, then one line per few seconds). Planned: in-run quick-chat with optional text-to-speech.

### Voice backend
Voice is carried outside NGO gameplay traffic. The planned backend is **Steam Voice through Facepunch.Steamworks**, with loudness measured locally on each client from its own microphone. An alternative third-party voice service stays open as a fallback if Steam voice proves unreliable on macOS. Either way, no game system reads voice audio directly; everything goes through the Noise Event.

Status in build: **Partial** — Point ping, Wave noise hook and lobby text chat with host-side filtering are built; positional voice, loudness-to-noise, mic calibration and Holler/Shush are planned.

---

## 12.5 Social Comedy Features

A bundle of cheap features whose job is to produce laughs between the big systems. They are ranked by **fun per effort** and each one has a hard cap on how much harm it can do.

| # | Feature | What it does | Harm cap | Tier |
|---|---|---|---|---|
| 1 | **Carry & throw friends** | A friend is an 80 kg, 2-hand carry. Throw speed ≤ 9 m/s. A carried friend can still hold a chicken (the "totem"). | Escape by mashing Jump (6 presses in 2 s, or hold 3 s in accessibility mode); auto-drop after 20 s. | VS |
| 2 | **Ragdoll slapstick** | Slap (F): 1.5 m reach, ragdolls only a target below 30% stamina. Hats pop off. | Slap can't knock items out of hands; 4 s slap immunity after a ragdoll; ragdoll ≤ 3 s. | VS |
| 3 | **Blame nicknames** | At payout each player gets a nickname from their worst habit: Butterfingers, Lifeguard, Tenderizer, Gatekeeper, Structural Engineer, Suspiciously Innocent. Shown under the name tag next day. | Opt-out in Social settings. | VS |
| 4 | **Ghost Chicken** | Knocked-out players become a translucent chicken until respawn: Cluck (lures/scares critters) and Peck (makes a friend flinch). | No collision with critters; can't carry. | Alpha |
| 5 | **Crew Photo** | At day end, everyone gets 5 s to pose on the market steps; a Polaroid with day, earnings, nicknames and the "Most Valuable Disaster" pins to the barn wall. | — | Alpha |
| 6 | **Helium balloon** | $15 from the wallet: +7 semitone voice and lighter gravity for 60 s. | Max $60/day for 4 players. | Alpha |
| 7 | **Megaphone** | Voice radius ×2.5, but makes a 0.9 Noise Event, so it scares animals. | One per farm. | Alpha |
| 8 | **Chalkboard** | Draw in the barn and lobby; barn board also shows the quota and the "Blame Champion". | Anyone can wipe (host only in public lobbies); "Hide drawings" setting. | Alpha |
| 9 | **Bounded pranks** | Tie shoelaces, whoopee cushion, bucket on head, swap the pig for a scarecrow. | Never destroys goods, spends money or takes > 5 s of control. | EA |
| 10 | **Highlight reel** | Top 5 Camcorder tapes of the week at Landlord Sunday, 60–90 s, skippable by majority vote. | — | EA |

Lobby-only behaviour: ragdoll and slapping work in the Lobby Yard, but bruises and the Blame Ledger are off.

Target: at least one social-comedy moment (throw, slap ragdoll, peck, prank) per player every 3 minutes.

Status in build: **Planned** — emotes, Faint and the hat system they hook into exist (Chapter 13); none of the ten features above is implemented yet.

---

## 12.6 Blame, Receipts and Awards

The **Blame Ledger** is the social engine. Every costly accident becomes a Blame Event: who touched the item in the last **3 s** (up to the last 3 touchers, sharing blame 60% / 30% / 10%), what happened (one of 23 verbs such as Dropped, Bruised, DroppedInRiver, BrokePlank, Drowned, YeetedTeammate) and what it cost.

| Moment | What players see |
|---|---|
| During the day | A 2.5 s toast: "−$90 Dave → river". Several disasters within 2 s merge into one toast. |
| Payout | An itemised receipt per player, a daily headline ("LOCAL MAN LOSES PIG, DIGNITY"), nicknames. "Spotless. Suspicious." for zero events. |
| Landlord Sunday | Sequence: arrival → Sunday audit (5 awards, reveal of blame totals, optional Donate/Keep) → quota payment. |

Blame never moves money between players (the wallet is shared). It is reputation, not punishment. Rookies (under 2 h played) get their first 3 Blame events captioned "(rookie)".

Status in build: **Partial** — Blame Ledger logic, the day clock and the payout receipt run in the Muddlebury full-day PlayMode playtest; toasts, nicknames and Sunday awards UI are planned.

---

## 12.7 Grandpa's Camcorder (Replays and Clips)

"Wait, you have to see this from the cow's angle." Every client keeps a rolling **30-second replay buffer** of the world (positions and states of nearby networked objects, not video). When a disaster happens, the 30 s before plus 3 s after are saved as a **Tape**.

| Rule | Value |
|---|---|
| Buffer | 30 s, 20 Hz, objects within 60 m; ≤ 64 MB per client (drops to 10 Hz, then off, on low memory) |
| Trigger | Every disaster; 8 s cooldown, overlapping disasters extend one tape (max 45 s) |
| Manual save | F8 / gamepad View+Down, 3 per day |
| Kept | Top 6 by severity + manual tapes per day; "Keep" writes to disk (≤ 50 kept, 1 GB) |
| Shelf | VHS boxes on the payout screen with tabloid titles ("DAVE vs. THE RIVER, $−240"); the headline tape loops muted in the receipt corner |
| Viewer | Play/pause, 0.25×/0.5×/1×, scrub; cameras: Grandpa Cam (auto-director), Victim Cam, Item Cam, Free Cam |
| Export | One click: Vertical 1080×1920 or Horizontal 1920×1080, 30 fps, ≤ 45 s, VHS overlay ("REC", "JUN 12 1994", caption, toggleable watermark), encoded locally in the background |
| Privacy | Voices excluded by default; included only if every recorded speaker opted in. Nothing uploads automatically. |

Grandpa Cam rules: a wide shot of the instigator and the item, 0.5× slow-mo around impact (disabled by Reduced Motion), a reaction close-up at the end, no cut faster than every 2 s, and a 10% chance of zooming dramatically on an innocent chicken.

Unity note: replay is a custom snapshot ring buffer of networked transforms replayed on proxy objects; export needs a native encoder per platform. Both are open technical work.

Status in build: **Planned**.

---

## 12.8 Griefing and Safety Rules

| Risk | Rule |
|---|---|
| Endless friend-carry | Struggle-free always works; auto-drop at 20 s. |
| Slap-locking | 4 s slap-ragdoll immunity; slap never removes held items. |
| Faint to dodge blame | Faint drops your own carried items and the Ledger still blames you; 5 s cooldown. |
| Prank abuse | Pranks never touch money or goods, never cost > 5 s of control, and appear in the Ledger as $0 "Prank" events. |
| Offensive names | Host-side cleanup (built); profanity filter with "Farmhand #N" fallback (planned); "Show Steam names only / generic names"; Streamer Mode shows "Farmer 1–4". |
| Offensive chat/drawings | Chat filtered and rate-limited (built); per-player mute; board wipe; "Hide drawings"; Streamer Mode hides drawings. |
| Disruptive player | Host can **kick** from the lobby Crew overlay or pause menu (kick hook built in the session layer; UI planned). Every player can mute anyone. |
| Spoofed voice loudness | Host clamps to 1.0 and rate-limits; low stakes in co-op. |
| Cosmetic hiding | Wardrobe is locked during the active day (except in the lobby). |

Status in build: **Partial** — name and chat sanitising, rate limits and join validation are built; mute, kick UI, profanity filter and Streamer Mode are planned.
