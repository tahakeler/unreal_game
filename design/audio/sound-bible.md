# HARVEST PANIC — Sound Bible

> **Status**: Draft v0.1 · **Owner**: audio-director · **Last Updated**: 2026-10-11
> **Engine**: UE 5.8.3 (MetaSounds, Submixes, Sound Classes/Mixes, Quartz, Audio Modulation)
> **Source GDDs**: `design/gdd/audio-feedback.md` (authoritative for rules/knobs), `proximity-voice.md`, `livestock-roster.md`, `night-creatures.md`, `day-loop.md`, `blame-ledger.md`
> **Companion docs**: `design/audio/sfx-list.md`, `design/audio/music-brief.md`

This document translates the `audio-feedback` GDD into a sonic identity and an implementation recipe. Where a number appears here and in the GDD, **the GDD wins**; this doc only adds numbers the GDD does not define (marked *[AD]* = audio-director proposal, tunable).

---

## 1. Sonic Identity

### 1.1 One-line identity
**"A Saturday-morning cartoon recorded on a real farm, with the lights going out at the end."**

Real, dirty, close-miked farm Foley (mud, wood, straw, real animals) cut with cartoon timing and exaggeration (slide whistles, boings, trombone wah-wahs). At night the cartoon layer drops away and the real layer gets very quiet and very close.

### 1.2 Palette axes

| Axis | Day (Morning/Workday/Payout) | Night (Dusk/Night Threat) |
|---|---|---|
| Acoustic vs synthetic | 90% acoustic (Foley + folk instruments); synthesis only for parameterized impacts/UI | 70% acoustic; synthetic sub-drones, granular wind, reversed textures |
| Clean vs distorted | Clean, bright, punchy transients | Soft, filtered, detuned (pump organ wobble, tape-warble) |
| Sparse vs dense | Dense but *rhythmically readable* (chaos = more layers, not more mush) | Sparse; silence is a tool; single sounds carry |
| Realism vs cartoon | Real source, cartoon timing/pitch (+2 to +5 st on critters, exaggerated impact tails) | Real source, *no* cartoon sweeteners (except on KO, to keep it "light horror") |
| Stereo field | Wide music, tight point-source SFX | Narrow music, wide ambience, very present close sounds |

### 1.3 Pillar check
- **Failure Is the Content** — audio makes failure *legible and funny*: $-scaled bruise layers (crunch > $10, register clunk > $40, coin ticks), music hard-cut + stinger on disasters, the receipt printer at Payout. Every loss is heard as money.
- **Everything Has Legs** — critters are characters: every state transition has a distinct bark (idle/alert/flee/charge/hide), and the bull's 0.8 s wind-up is the most protected sound in the mix. Gap to watch: the goat and llama (Alpha/EA) need their own vocal identity before they ship; VS covers pig + chicken only.
- **Co-op comedy under voice chat** (implicit pillar from `proximity-voice`) — the mix is designed *around* four people yelling: SFX live in the 2–6 kHz "click/transient" band and below 300 Hz "thump" band, leaving 300 Hz–2 kHz speech intelligibility zone as clear as possible for Voice.

### 1.4 Do / Don't
- DO: give every important event a transient you can identify with eyes closed in < 150 ms.
- DO: let silence land. The 0.6 s DisasterCut is the funniest sound in the game.
- DO: pitch-randomize everything (±1.5 st default, ±3 st critters) and never repeat the same variant twice in a row (MetaSound `Random Get` with no-repeats).
- DON'T: use laugh tracks, voiced announcer quips, or meme sounds (licensing + they age badly + they fight the players' own laughter).
- DON'T: use real jump-scare screams at night. Night creatures are "light horror": dread through creaks, breath, rustle, and pitched silence; KO resolves with a cartoon "bonk + birdies" sting.
- DON'T: let music carry information that only music carries (accessibility: every music-borne cue also has a caption/HUD/diegetic SFX).

---

## 2. Music Direction per Phase

Instrument core ("The Porch Band"): banjo, fiddle, upright bass, washboard, jug, harmonica, acoustic guitar, accordion, tuba (Landlord), pump organ (night). Small, recognizable, cheap to stem.

All gameplay cues are **stemmed** and **loop on bar boundaries** at a fixed tempo per cue so Quartz can sync transitions. Full briefs in `music-brief.md`.

| Phase (day-loop) | Music state (GDD) | Cue | Character | Tempo / Key *[AD]* |
|---|---|---|---|---|
| Morning (30 s) | Morning | `MUS_Morning_Porch` | Calm solo banjo + harmonica, rooster on the downbeat, hopeful-lazy | 92 BPM, G major |
| Workday (540 s) | Roundup L1–L3 | `MUS_Workday_Roundup` | Bluegrass breakdown; layers added by Chaos Level | 132 BPM, D major |
| Last 60 s + Sundown 30 s ("dusk panic") | Crunch | `MUS_Workday_Roundup` Crunch variant | Same tune, tempo +15% (152 BPM), fiddle lead, pocket-watch tick layer | 152 BPM, D major → E major key-lift on the final 30 s bell |
| Dusk (180 s, optional) | Dusk / Night Threat | `MUS_Night_Dusk` | Sparse creepy pump organ, bowed saw, low bass drone; "Night Threat" adds a heartbeat kick + tremolo strings when a creature is within 30 m | 70 BPM, D minor (Dorian) |
| Payout (~30 s) | Market | `MUS_Payout_Market` | Accordion waltz, receipt-printer rhythm locks to tempo | 108 BPM 3/4, C major |
| Sunday | Sunday | `MUS_Sunday_Landlord` | Cheerful oompah (tuba, brass, bass drum); modulates to parallel minor if the crew is short | 100 BPM, Bb major ↔ Bb minor |

Shop phase reuses `MUS_Payout_Market` stems at layer "porch only" (accordion + guitar), lowered −6 dB.

---

## 3. Adaptive Music Rules

### 3.1 State machine (from GDD, with transition timing)
```
Morning ──(Workday start, next bar)──► Roundup[L1..L3]
Roundup ──(Timer ≤ 60 s, next bar)──► Crunch
Crunch  ──(Market close bell)───────► Dusk (if voted) | Market
Dusk    ◄─(creature ≤ 30 m of any player / none for 6 s [AD])─► NightThreat
Dusk/NightThreat ──(dusk end)───────► Market
any     ──(OnDisaster)──► DisasterCut (immediate) ──► previous state @ layer−1
Sunday  : exclusive, entered from Shop/week-quota
```

### 3.2 Chaos Level → layers
`ChaosLevel` and `MusicLayer = 1 + floor(ChaosLevel * 2.99)` are defined in the GDD. Audio-side rules *[AD]*:

| Layer | Stems playing (Roundup) | Feel |
|---|---|---|
| L1 | bass, guitar strum, light washboard, banjo melody | Busy farm chores |
| L2 | + fiddle countermelody, full washboard, jug | Things are getting away |
| L3 | + second banjo rolls, harmonica stabs, kick/stomp | Full hoedown panic |

- **Hysteresis:** layer goes **up** on the next bar after ChaosLevel crosses the threshold; goes **down** only after ChaosLevel has stayed below the threshold for **8 s** (prevents flapping) *[AD]*.
- **Layer fade:** stems fade in over 1 bar, fade out over 2 bars, via Audio Modulation Control Bus per stem.
- **Post-disaster:** resume at `layer − 1` (min L1), then re-evaluate after 4 bars.
- **Night Threat + Crunch overlap** (GDD edge case): Night Threat wins; Crunch adds only the pocket-watch tick stem.

### 3.3 Disaster cut (GDD rule 6)
1. `OnDisaster` → music bus hard-cut (5 ms fade) to silence for `DisasterSilence` 0.6 s. Severity ≥ 80: record-scratch on cut.
2. Stinger by verb, played **immediately** (not quantized; it must sync to the visual) — slide-whistle+splash (river), trombone wah-wah (plank), cymbal crash (wagon), generic "sad tuba" fallback.
3. Music re-enters **Quartz-quantized to the next beat** (not bar) after the stinger, with the "banjo player clears his throat" pickup (`STG_Music_ThroatClear`) on the first resume of the day only (keep it special) *[AD]*.
4. Multiple disasters within 3 s: one stinger; later ones get only the sweetener.

### 3.4 Diegetic overlays
- Pocket-watch tick (last 60 s): Quartz-locked to the Crunch tempo (152 BPM eighth notes = tick), so it reads as both a clock and part of the band.
- Sundown bell at 0:00 is sample-accurate on the Quartz clock downbeat (the music *lands* on the bell).

---

## 4. Mix: Buses, Priority, Ducking

### 4.1 Submix tree
```
Master
├── SM_Voice              (EOS / VoIP talkers; never ducked; own limiter)
├── SM_Game
│   ├── SM_Critters       (barks, swarms)
│   ├── SM_Impacts        (MS_Impact, bruises, breaks, splashes)
│   ├── SM_Player         (footsteps, grab/throw, own body)
│   └── SM_Ambience       (beds, river, wind, night)
├── SM_Music
└── SM_UIStingers         (UI, stingers, cash, receipt; 2D)
Reverb: SM_Reverb_Outdoor / SM_Reverb_Barn (send submixes)
```
The GDD's 5 buses map 1:1: Voice=SM_Voice, Critters, Impacts (+Player, Ambience as children of Game), Music, UI/Stingers.

### 4.2 Volume hierarchy (target relative loudness, momentary, at listener) *[AD]*
1. Voice (reference 0 dB)
2. Bull charge telegraph / creature-close warnings: −2 dB (protected)
3. UI/Stingers: −4 dB
4. Impacts & bruises: −6 dB (scaled by `ImpactGain_dB`)
5. Critter barks: −8 dB
6. Player Foley: −12 dB
7. Music: −14 dB (−20 dB with voice duck)
8. Ambience: −18 dB

### 4.3 Ducking matrix (GDD values + [AD] attack/release)

| Trigger | Ducks | Amount | Attack / Release |
|---|---|---|---|
| Any player ≤ 15 m speaking (voice activity) | Music | −6 dB | 80 ms / 600 ms |
| same | Critters | −3 dB | 80 ms / 600 ms |
| same | Ambience *[AD]* | −3 dB | 80 ms / 600 ms |
| Stinger playing | Music | −9 dB (cut to silence for disaster) | 10 ms / stinger length |
| Charge telegraph / Scarecrow-close | Critters (other), Impacts *[AD]* | −4 dB | 20 ms / 400 ms |
| Voice disabled lobby | ducking driven by shout emotes | same table | |

**Voice is never ducked.** Voice also gets a gentle dynamic EQ dip on SM_Game (−2 dB at 1–3 kHz while voice active) *[AD]* — cheaper in perceived loudness than more broadband ducking.

### 4.4 Implementation of ducking
- Use **Audio Modulation**: a `Control Bus` per ducking target (`CB_Duck_Music`, `CB_Duck_Critters`, …) driven by `Control Bus Mix` activated from code (voice activity from `proximity-voice` VAD at 20 Hz; stinger start/end). Avoid submix sidechain envelope followers for voice, because EOS voice may not route through a submix we control on Mac/Steam fallback.
- Concurrency: `SC_Impacts` max 12 (StopQuietest), `SC_CritterBarks` max 6/s global + 2 s per-critter cooldown (in code), `SC_Footsteps` max 8 (StopFarthest), `SC_Stingers` max 1 (StopOldest disallowed: new is rejected and downgraded to sweetener).

### 4.5 Frequency balance
- Speech band 300 Hz–3 kHz: SFX are high-passed/shelved so no constant bed lives here; ambience beds HPF at 150 Hz and get −3 dB at 1–2 kHz.
- Music mix stems are delivered with a pre-carved 1–3 kHz pocket (−2 to −3 dB) when dense (L3/Crunch).
- Low end reserved for impacts, bull/cow, and night drone. Music bass HPF at 40 Hz.

### 4.6 Loudness & platform
- Integrated target for the full game mix: **−16 LUFS** (PC/console norm; ≤ −1 dBTP). Asset targets in `sfx-list.md` §1.
- Master limiter (Submix Effect: Dynamics, Limiter, −1 dBTP).
- Settings sliders: Master, Music, SFX, Voice, UI; plus "Voice priority boost" toggle (adds −3 dB extra duck) and "Reduce night ambience" accessibility option.

---

## 5. UE 5.8 Implementation Recipe

> Audio code is owned by gameplay-programmer/engine-programmer; this is the contract the audio team authors assets against.

### 5.1 MetaSounds
| Asset | Type | Inputs | Notes |
|---|---|---|---|
| `MS_Impact` | MetaSound Source (one-shot) | `Mass`(kg), `ImpactSpeed`(m/s), `MaterialPair`(int enum), `DollarLoss`($) | Wave Player per material bank; pitch = −12·clamp(Mass/400) st; crunch layer if `DollarLoss > 10`; register clunk if `> 40`; gain per GDD formula |
| `MS_Bruise` | Source | `BruiseType`, `DollarLoss` | squeak/squelch + `floor(DollarLoss/10)` coin ticks (max 5) scheduled 60 ms apart via Trigger Repeat |
| `MS_Critter_Bark` | Source (preset per species) | `State`(enum), `Excitement`(0–1) | Random Get (no repeat) per state array; pitch ±3 st; Excitement adds pitch + gain |
| `MS_Critter_Swarm` | Source (looping) | `Density`(0–1), `Panic`(0–1) | One emitter for chicken flocks (GDD 40-chicken case): crossfades 3 layers (few/several/many clucks) + granular flutter |
| `MS_Footstep` | Source | `Surface`(PhysMat), `Speed`(walk/sprint/quiet), `Wet` | quiet-walk = −12 dB, softer layer |
| `MS_Plank` | Source | `Load`(0–1), `Stage`(creak/crack/break) | creak pitch drops & duration grows with Load |
| `MS_River_Bed` | Source (looping) | `Distance`, `Flow` | 2D near/far blend |
| `MS_Music_<Cue>` | Source (looping, stemmed) | per-stem `Layer` gains via Modulation | One MetaSound per cue, stems as parallel Wave Players started on same Quartz tick |
| `MS_Receipt` | Source | `LineCount`, `IsNegative` | printer chatter loop + per-line tear; "wah" for negative totals |

Use **MetaSound Presets** for per-species/per-surface variation so the graph is authored once.

### 5.2 Sound Classes & Submixes
- Sound Classes: `SC_Master` → `SC_Voice`, `SC_Music`, `SC_SFX` (→ `SC_Critters`, `SC_Impacts`, `SC_Player`, `SC_Ambience`), `SC_UI`. Sound Classes carry user volume sliders (via Audio Modulation `Control Bus` per slider; preferred in 5.x over Sound Class Mixes).
- Each MetaSound sets its `Default Submix` to the matching submix in §4.1.
- `SM_Reverb_Barn` (Convolution, small wooden room IR) applied by an Audio Volume in the barn; outdoor uses a light algorithmic plate send at −20 dB.

### 5.3 Quartz
- One Quartz clock: `QC_Music`, owned by the music manager (client-local; music is not replicated; phase changes come from replicated day-loop state).
- Tempo/time-signature set per cue on entering a state.
- Quantization:
  - State transitions: `Bar`
  - Layer up/down: `Bar`
  - Music resume after disaster: `Beat`
  - Sundown bell: scheduled on `Bar` when timer hits 0 ± 0.25 s (bell is diegetic, tolerance acceptable)
  - Pocket-watch tick: `EighthNote`
  - Disaster stinger: **not quantized** (sync to picture) — the cut itself is the musical gesture.
  - Positive stingers (Order Line Complete, Big Sale): `Beat` so they sit in the groove.
- Clients compute transitions locally from the replicated timer; ±0.1 s clock agreement (day-loop AC1) is fine because music is not shared audio.

### 5.4 Attenuation presets (first person)
| Preset | Inner radius | Falloff to | Shape / Curve | Spatialization | Occlusion | Used by |
|---|---|---|---|---|---|---|
| `ATT_Player_Self` | — | — | 2D (non-spatialized), slight stereo | — | — | own footsteps, own grab/throw, own breath |
| `ATT_Player_Other` | 2 m | 25 m | Log | Binaural (HRTF) optional, panning default | −6 dB, LPF 3 kHz | other players' Foley |
| `ATT_Critter_Small` | 2 m | 30 m | Natural Sound | Panning | −6 dB, LPF 2.5 kHz | chicken, duck |
| `ATT_Critter_Large` | 4 m | 60 m | Natural Sound | Panning + 0.5 spread at < 4 m | −4 dB | pig, cow, bull |
| `ATT_Telegraph` | 6 m | 80 m | Linear (flat-ish, stays loud) | Panning, focus 100% | **none** | bull charge wind-up, creature-close warnings (must survive the mix) |
| `ATT_Impact` | 3 m | 50 m | Log | Panning | −6 dB | MS_Impact, breaks |
| `ATT_Hazard_Loud` | 5 m | 90 m | Natural Sound | Panning | −3 dB | plank break, splash, wagon crash |
| `ATT_Ambience_Point` | 5 m | 40 m | Log | spread 0.7 | LPF only | river, windmill, coop |
| `ATT_Night_Creature` | 1 m | 25 m | Log, steep | Binaural preferred (headphones), panning fallback | −6 dB | scarecrow rustle, breath |
| Voice | per `proximity-voice` (3 m full → 35 m silent, −9 dB/wall) | | | | | not our asset; listed for consistency |

Rules: listener = camera; `bEnableListenerFocus` on critters so off-screen critters are slightly quieter (focus −3 dB), **except** `ATT_Telegraph`. Noise-Event-tagged sounds add a short "echo tail" (send to `SM_Reverb_Outdoor` +6 dB for 1 s) per GDD rule 7.

### 5.5 Event naming & routing
- Runtime event names: `SFX_<Category>_<Subject>_<Action>` (e.g. `SFX_Critter_Pig_Squeal`), `MUS_<Phase>_<Name>`, `STG_<Context>_<Name>`, `AMB_<Area>_<Name>`, `UI_<Screen>_<Action>`.
- Asset names follow the studio convention: `[category]_[context]_[name]_[variant].[ext]` → `sfx_critter_pig_squeal_01.wav`.
- A `DT_AudioEvents` DataTable maps event name → MetaSound/preset, submix, attenuation, concurrency, caption id (onboarding-accessibility requires a caption id per bark/stinger).

### 5.6 Performance budget
- Audio thread ≤ 1.5 ms on base M1 worst-case (GDD AC2). Hard caps: 64 voices total *[AD]*, 12 impacts, 8 footsteps, 6 barks/s, swarms as single emitters.
- Streaming: music stems Stream (Force Stream), ambience loops Retain-on-Load for the level, short SFX Inline/Prime.
- Compression: Ogg Vorbis/ADPCM per platform (Bink Audio for music in 5.x is fine). See `sfx-list.md` §1.

---

## 6. Open Questions (for creative-director / technical-director)
1. Is binaural (HRTF) acceptable on by default for night creatures (headphone-heavy audience), or opt-in only?
2. Music sourcing: commissioned composer vs. CC0/royalty-free library + stem edits for VS? (Briefs are written to work for both.)
3. Does Camcorder replay need a separate music render (to avoid replaying a DisasterCut twice)? GDD says stingers re-trigger; confirm desired.
4. Confirm EOS voice on Mac exposes a VAD signal we can drive ducking from; otherwise drive from the local loudness value already computed for Noise Events.
