# HARVEST PANIC — Music Briefs (6 cues)

> **Status**: Draft v0.1 · **Owner**: audio-director · **Last Updated**: 2026-10-11
> See `sound-bible.md` §2–3 for the adaptive system these cues plug into.

## Global delivery spec (all cues)
- 48 kHz / 24-bit WAV, stems + full mix. Loops must be **sample-accurate bar-length** with no reverb tail baked across the loop point (deliver tails as a separate `_tail` file).
- Constant tempo per cue, tempo map + bar count in filename/metadata (e.g. `mus_workday_roundup_L2_fiddle_132bpm_32bar.wav`).
- Stem mix target: full mix −18 LUFS integrated, ≤ −1 dBTP. Leave a −2 to −3 dB pocket at 1–3 kHz in dense sections (voice chat lives there).
- Instrumentation family ("The Porch Band"): banjo, fiddle, upright bass, washboard, jug, harmonica, acoustic guitar, accordion, tuba/oompah brass, pump organ, bowed saw. No synth leads, no drum kit (stomps/washboard/bass drum only).
- Sourcing for VS: commissioned or CC0/royalty-free with stems (e.g. Kevin MacLeod CC-BY bluegrass for placeholder only — attribution required, replace before release). Final must be original or bought-out.
- References are for *feel*, not for copying.

---

## 1. `MUS_Morning_Porch` — Morning
| Field | Value |
|---|---|
| Phase | Morning (30 s, skippable) |
| Length | 16-bar loop (~42 s) + 2-bar "let's go" outro used as the transition into Workday |
| Tempo / meter / key | 92 BPM, 4/4, G major |
| Instrumentation | Solo clawhammer banjo, harmonica, light upright bass, distant rooster on bar 1 (diegetic-ish), birdsong bed is ambience (not in music) |
| Mood | Lazy optimism; "Grandpa's diary" warmth; the calm before chaos |
| Stems | banjo, harmonica, bass |
| Adaptive | None. Outro triggered on Workday start, Quartz Bar |
| References | Animal Crossing morning hours (calm loops); Stardew Valley "Spring" (warm acoustic); classic Looney Tunes barnyard openers (timing) |

## 2. `MUS_Workday_Roundup` — Workday (Roundup L1–L3)
| Field | Value |
|---|---|
| Phase | Workday 0:00–8:00 |
| Length | 32-bar loop (~58 s) with A/B sections so 9 minutes doesn't fatigue; ideally 2 alternate B sections chosen at random per loop |
| Tempo / meter / key | 132 BPM, 4/4 (cut-time feel), D major |
| Instrumentation | L1: upright bass, guitar boom-chuck, light washboard, banjo melody · L2: + fiddle counter, full washboard, jug · L3: + second banjo rolls, harmonica stabs, foot stomps |
| Mood | Cheerful competence → barely-controlled hoedown |
| Stems | 7–8 stems grouped into 3 layer buses |
| Adaptive | Layer by ChaosLevel (hysteresis 8 s down), resume at layer−1 after disaster |
| References | Overcooked 2 (escalating kitchen music), Untitled Goose Game (reactive piano, mischief), Earl Scruggs "Foggy Mountain Breakdown" (energy only) |

## 3. `MUS_Workday_Crunch` — Dusk panic (last 60 s + Sundown)
| Field | Value |
|---|---|
| Phase | Workday ≤ 60 s remaining → Sundown bell |
| Length | 8-bar loop (~12.6 s) + 2-bar "bell landing" ending synced to 0:00 |
| Tempo / meter / key | 152 BPM (+15%), 4/4, D major; final 30 s key-lift to E major |
| Instrumentation | Roundup arrangement re-recorded at tempo (not time-stretched), fiddle lead with frantic double-stops, pocket-watch tick layer (eighths), cowbell/anvil hits on bar 1 |
| Mood | Cooking-show countdown run by farmers who can't cook |
| Stems | Rhythm, fiddle lead, banjo, tick (tick separable for Night Threat overlap) |
| Adaptive | Entered on Bar; ends exactly on bell (Quartz-scheduled); photo-finish grace 2 s = sustained final chord + crowd-less "ta-da" or "wah" depending on whether the last item sold |
| References | Overcooked final-minute music, game-show countdown cues, "Orange Blossom Special" fiddle energy |

## 4. `MUS_Night_Dusk` — Night (Dusk + Night Threat)
| Field | Value |
|---|---|
| Phase | Dusk (180 s, optional) |
| Length | 24-bar loop (~82 s) base + Night Threat overlay loop 8 bars |
| Tempo / meter / key | 70 BPM, 4/4 (rubato feel, but gridded), D Dorian / minor |
| Instrumentation | Base: wheezy pump organ (detuned), bowed saw, plucked low bass, sparse music-box banjo harmonics · Threat overlay: heartbeat kick (bass drum, felt), tremolo fiddle, reversed banjo swells |
| Mood | Light horror — creepy-funny, Halloween hayride not slasher. Must leave space for footsteps/rustles (creature tells) |
| Stems | organ, saw, bass, harmonics, threat-heartbeat, threat-strings |
| Adaptive | Threat overlay in on Bar when a creature is ≤ 30 m of any player; out after 6 s clear. Very low music level overall (−4 dB vs day) |
| References | Luigi's Mansion (spooky-comedic), Don't Starve dusk, Scooby-Doo chase underscores (minus the pop) |

## 5. `MUS_Payout_Market` — Payout (and Shop)
| Field | Value |
|---|---|
| Phase | Payout (~30 s), Shop (untimed, reduced arrangement) |
| Length | 16-bar loop (~27 s at 3/4) + positive ending (2 bars) and negative ending (2 bars, deflating tuba "wah") |
| Tempo / meter / key | 108 BPM, 3/4 waltz, C major |
| Instrumentation | Accordion lead, acoustic guitar, upright bass, light triangle; receipt printer SFX intentionally sits on beats 2 and 3 |
| Mood | Half celebration, half court case: jaunty with a sly, accusatory undertone for the Blame Ledger reveal |
| Stems | accordion, guitar, bass, perc |
| Adaptive | "Headline" (highest-Severity blame line) reveal drops to accordion only for 2 bars + `STG_Ledger_Headline`; Shop = accordion + guitar only, −6 dB |
| References | Papers, Please end-of-day tally (structure), Overcooked level-complete, French musette waltz |

## 6. `MUS_Sunday_Landlord` — Sunday Landlord
| Field | Value |
|---|---|
| Phase | Sunday (exclusive: Landlord visit / week-quota) |
| Length | 16-bar loop (~38 s) in major + matching 16-bar minor version (same tempo/structure, crossfade on Bar) + 2 endings (paid / short) |
| Tempo / meter / key | 100 BPM, 2/4 oompah, Bb major ↔ Bb minor |
| Instrumentation | Tuba, euphonium/baritone horn, trumpet, bass drum + cymbal, clarinet countermelody; "short" version detunes brass slightly and adds a sly bassoon |
| Mood | Cheerful bureaucratic menace; a smiling man with a clipboard |
| Stems | brass melody, tuba, perc, clarinet (Major and Minor sets share stem layout) |
| Adaptive | If projected wallet < quota → crossfade to minor on next Bar (subtle modulation, per GDD comedy beat). Paid → major ending with cymbal; Short → minor ending, single tuba note |
| References | Theme-park oompah bands, "Bavarian" polka, Monkey Island's jaunty-menacing pirate themes, Grim Fandango brass irony |

---

## Stingers (short-form, same palette; full list in `sfx-list.md`)
River (slide-whistle + splash), Plank (trombone wah-wah), Wagon (cymbal crash), Generic disaster (sad tuba), Record scratch (Severity ≥ 80), Throat-clear resume, Order Line Complete (banjo lick, Beat-quantized), Order Complete (full band hit), Big Sale (accordion flourish), Ledger Headline (snare roll + gavel).
