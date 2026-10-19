# HARVEST PANIC — Vertical Slice SFX Event List

> **Status**: Draft v0.1 · **Owner**: audio-director (detail pass delegated to sound-designer) · **Last Updated**: 2026-10-11
> Scope: Vertical Slice (pig + chicken, bridge, river, market, Workday, Payout, Blame Ledger, UI). Night creatures/Sunday are Alpha but listed in §12 as "VS-stretch" so the system is proven.

## 1. Conventions

**Priority**
- **P0** — gameplay-critical/readability; VS cannot ship without it.
- **P1** — core comedy/feel; VS target.
- **P2** — polish; placeholder acceptable in VS.

**Asset specs**
| Category | Format (source) | Runtime | Peak | Loudness target (short-term, at 1 m) | Budget |
|---|---|---|---|---|---|
| SFX one-shots | 48 kHz/24-bit WAV, mono | ADPCM/Ogg q0.6 | ≤ −1 dBTP | −18 to −12 LUFS-S | ≤ 150 KB each |
| Critter barks | 48 kHz/24-bit, mono | Ogg | ≤ −1 dBTP | −16 LUFS-S | ≤ 200 KB |
| Ambience loops | 48 kHz/24-bit, stereo | Ogg, stream | ≤ −3 dBTP | −28 to −24 LUFS-I | ≤ 2 MB |
| UI / stingers | 48 kHz/24-bit, stereo | Ogg | ≤ −1 dBTP | −16 LUFS-S | ≤ 300 KB |
| Music stems | see `music-brief.md` | Stream | | −18 LUFS-I mix | ≤ 40 MB VS total |
VS total SFX budget: ≤ 60 MB on disk.

**Sources** (verify license per file at import; log in `design/audio/licenses.csv` when created)
- **FS** = freesound.org — filter to **CC0** only (CC-BY acceptable with attribution log; no NC).
- **SON** = Sonniss "GameAudioGDC" free bundles (royalty-free, commercial use, no attribution required).
- **FAB** = Fab.com free audio packs (check Standard License).
- **SYN** = synthesize in MetaSounds or a DAW.
- **REC** = record in-house (phone/Zoom recorder is fine for Foley).

Naming: event `SFX_<Category>_<Subject>_<Action>` → assets `sfx_<category>_<subject>_<action>_<NN>.wav`.
Variations = number of distinct recorded files (pitch/vol randomization is on top: ±1.5 st default, ±3 st critters).
Noise column = Noise Event loudness emitted by gameplay (from `proximity-voice`), for reference only — audio does not emit Noise Events.

---

## 2. Player Foley

| Event | Trigger | Var | Pri | Noise | Source / synthesis note |
|---|---|---|---|---|---|
| SFX_Player_Footstep_Grass | Anim notify, PhysMat Grass | 8 | P0 | walk 0.10 / sprint 0.25 | SON GDC bundles (Foley footsteps grass); FS CC0 "footsteps grass" |
| SFX_Player_Footstep_Dirt | PhysMat Dirt/Path | 8 | P0 | same | SON; FS CC0 |
| SFX_Player_Footstep_Mud | PhysMat Mud (squelch layer) | 8 | P0 | same | REC (boot in wet soil) + FS CC0 "mud squelch" |
| SFX_Player_Footstep_Wood | PhysMat Wood (bridge, barn floor) | 8 | P0 | same | SON; plank-hollow layer on bridge |
| SFX_Player_Footstep_Straw | PhysMat Straw/Hay (barn) | 6 | P1 | same | REC (crumple hay/straw) |
| SFX_Player_Footstep_Gravel | PhysMat Gravel (market) | 6 | P1 | same | SON |
| SFX_Player_Footstep_Water | PhysMat ShallowWater | 6 | P1 | same | FS CC0 "wading" |
| SFX_Player_Footstep_Quiet | Quiet-walk modifier, any surface | (uses surface set) | P1 | none | MetaSound: −12 dB, LPF 4 kHz, heel layer removed |
| SFX_Player_Jump | Jump start | 4 | P2 | — | REC cloth whoosh + grunt-less breath |
| SFX_Player_Land | Land, scaled by fall speed | 4 per surface (reuse footstep + body layer) | P1 | 0.10–0.3 | footstep ×2 + cloth thump |
| SFX_Player_Slip | Slipped (mud/ice/banana) | 3 | P1 | 0.3 | cartoon: SYN rising whistle + cloth whoosh + thud |
| SFX_Player_KO | KnockedOut | 3 | P0 | — | cartoon "bonk" + birdies loop (SYN sine chirps) |
| SFX_Player_Respawn | Barn respawn | 1 | P1 | — | barn door creak + rooster "pfft" |
| SFX_Player_Stamina_Out | Stamina hits 0 | 2 | P2 | — | REC heavy breath (2D, own player only) |

## 3. Carry / Grab / Throw

| Event | Trigger | Var | Pri | Source / note |
|---|---|---|---|---|
| SFX_Carry_Grab_Light | Grab item ≤ 1-hand | 4 | P0 | REC cloth+hand slap; FS CC0 "grab" |
| SFX_Carry_Grab_Heavy | Grab multi-hand item | 3 | P0 | REC grunt-less strain + wood/rope creak |
| SFX_Carry_Grab_Critter | Grab living critter (pairs with species bark) | 3 | P0 | cloth + feather/hide rustle |
| SFX_Carry_Drop | Release without throw | 4 | P0 | routes to MS_Impact at low speed |
| SFX_Carry_Throw_Windup | Throw charge start | 2 | P1 | SYN rising whoosh (pitch follows charge) |
| SFX_Carry_Throw_Release | Throw | 4 | P0 | REC/SON whoosh, size by mass |
| SFX_Carry_Throw_Yeet | Max-charge throw | 2 | P1 | whoosh + cartoon "ptoo" whistle |
| SFX_Carry_Struggle | Critter struggles in hands (StruggleImpulse) | 4 | P1 | rustle + species struggle bark |
| SFX_Carry_GripSlip | Item slips from hands | 3 | P1 | SYN descending "fwip" + squeak |
| SFX_Carry_Handoff | Item passed between players | 2 | P2 | soft cloth + "pop" |

## 4. Impacts & Bruises ($-scaled, MS_Impact / MS_Bruise)

Single MetaSound driven by `Mass`, `ImpactSpeed`, `MaterialPair`, `DollarLoss`. List = sample banks.

| Event | Trigger | Var | Pri | Noise | Source / note |
|---|---|---|---|---|---|
| SFX_Impact_Wood | MaterialPair includes wood | 6 | P0 | light 0.3 / heavy 0.6 | SON wood impacts |
| SFX_Impact_Soft (produce/body) | flesh, produce, hay | 6 | P0 | same | REC (hit cabbage/melon with palm) |
| SFX_Impact_Dirt | dirt/grass ground | 6 | P0 | same | SON; REC sandbag drop |
| SFX_Impact_Metal | bucket, wagon, tools | 5 | P1 | same (Noise.Metal) | SON metal hits |
| SFX_Impact_Crate | wooden crates | 5 | P1 | same | SON |
| SFX_Bruise_Thud_Small | DollarLoss $2–10 | 4 | P0 | — | soft thud + tiny squeak |
| SFX_Bruise_Crunch | layer when DollarLoss > $10 | 4 | P0 | — | REC celery/lettuce snap, pitched down |
| SFX_Bruise_RegisterClunk | layer when DollarLoss > $40 | 3 | P0 | — | SON/FS CC0 old cash-register drawer slam, low-passed |
| SFX_Bruise_CoinTick | floor($/10) ticks, max 5, 60 ms apart | 4 | P0 | — | FS CC0 "coin drop"; SYN fallback (short metallic FM ping) |
| SFX_Bruise_Squelch | Bruise type Squashed (produce/eggs) | 4 | P1 | Noise.Squelch | REC tomato/egg crush |
| SFX_Bruise_Squeak | Bruise type Dented/Scuffed | 4 | P1 | — | rubber-toy squeak (FS CC0) |
| SFX_Item_Break | Item ruined (DollarLoss = full value) | 3 per material (wood/egg/glass jar) | P0 | 0.6 | SON breaks + cartoon "poof" |
| SFX_Item_EggCrack | Egg ruined | 4 | P0 | — | REC egg crack |
| SFX_Feathers_Fly | Chicken bruised (fragility "feathers fly") | 3 | P1 | — | REC pillow-feather puff / FS CC0 |

## 5. Critters — Pig ("Hamlet")

| Event | Trigger (critter-ai state) | Var | Pri | Noise | Source / note |
|---|---|---|---|---|---|
| SFX_Critter_Pig_Idle | Idle, random 8–15 s | 6 | P1 | — | SON/FS CC0 pig grunts |
| SFX_Critter_Pig_Alert | → Alert (rising) | 3 | P0 | — | grunt with upward pitch bend (MetaSound pitch env) |
| SFX_Critter_Pig_Squeal | Grabbed / Flee | 6 | P0 | 0.7 Noise.Squeal | FS CC0 pig squeal; cartoon +3 st; long variants for carry loop |
| SFX_Critter_Pig_Squeal_Loop | While carried & struggling | 2 loops | P1 | 0.7 | sustained squeal, seamless loop |
| SFX_Critter_Pig_Snuffle_Feed | Lured by feed bucket | 4 | P1 | — | REC/FS snuffle + oink |
| SFX_Critter_Pig_UnderFence | Squeezes under fence | 2 | P2 | — | wood scrape + strained "hnngh" grunt |
| SFX_Critter_Pig_Dramatic | Landed after thrown / in water ("dramatic") | 3 | P2 | — | exaggerated faint-grunt + sigh |

## 6. Critters — Chicken (flock)

| Event | Trigger | Var | Pri | Noise | Source / note |
|---|---|---|---|---|---|
| SFX_Critter_Chicken_Swarm | MS_Critter_Swarm, one per flock; Density/Panic params | 3 density layers | P0 | — | SON/FS CC0 coop recordings layered |
| SFX_Critter_Chicken_Cluck | Single Idle | 8 | P1 | — | FS CC0 |
| SFX_Critter_Chicken_Alert | → Alert | 4 | P0 | — | "bawk?" rising |
| SFX_Critter_Chicken_Panic | Flock scatter / Flee (burst) | 6 | P0 | — | FS CC0 panicked squawks, +2 st |
| SFX_Critter_Chicken_Flap | Short flight over fence | 4 | P1 | — | REC cloth/fan flutter + FS wing flaps |
| SFX_Critter_Chicken_Netted | Net catches | 3 | P1 | — | net swish + muffled squawk |
| SFX_Critter_Chicken_Regroup | Flock regroup (15 s) | 2 | P2 | — | calm cluck chorus |

## 7. Bridge — Planks

| Event | Trigger | Var | Pri | Noise | Source / note |
|---|---|---|---|---|---|
| SFX_Bridge_Plank_Creak | Load above safe threshold (MS_Plank Load param) | 6 | P0 | 0.3 Noise.Creak | SON wood creaks; REC old door/stair; pitch drops with Load |
| SFX_Bridge_Plank_Crack | Pre-break warning stage | 4 | P0 | 0.3 | SON wood crack/splinter |
| SFX_Bridge_Plank_Break | Plank breaks | 4 | P0 | 0.75 Noise.PlankBreak | SON wood break + splinter debris; tail to splash if over water |
| SFX_Bridge_Rope_Strain | Rope rails under load | 3 | P2 | — | REC rope twist |
| SFX_Bridge_Sway_Loop | Bridge sway (wind/load) | 1 loop | P2 | — | low wood groan loop |

## 8. River / Water

| Event | Trigger | Var | Pri | Noise | Source / note |
|---|---|---|---|---|---|
| AMB_River_Flow | MS_River_Bed near/far | 2 loops | P0 | — | SON river ambiences |
| SFX_Water_Splash_Small | Small item/critter enters water | 4 | P0 | 0.75 Noise.Splash | FS CC0 / SON splashes |
| SFX_Water_Splash_Big | Player/pig/large item | 4 | P0 | 0.75 | SON big splash; REC bath cannonball |
| SFX_Water_Swim | Player swimming stroke | 4 | P1 | — | REC bathtub hand strokes |
| SFX_Water_ItemCarriedAway | Item drifting downstream (bobbing loop) | 2 | P1 | — | gentle plop + bubble loop |
| SFX_Player_Drowned | Drowned verb | 1 | P0 | — | bubbles + cartoon "glub glub" (SYN) |
| SFX_Water_Emerge | Player climbs out | 2 | P2 | — | water drip shake |

## 9. Market

| Event | Trigger | Var | Pri | Source / note |
|---|---|---|---|---|
| SFX_Market_Pen_Enter | Item enters Sale Pen (2 s accept starts) | 2 | P1 | soft "tick-tick" (SYN) counting 2 s |
| SFX_Market_ChaChing | Item accepted/sold | 4 (by value tier: <$30, <$100, <$300, ≥$300) | P0 | FS CC0 vintage cash register; tiers add coin cascade |
| SFX_Market_ChaChing_Abort | Critter leaves pen before 2.0 s ("ka-ch…") | 2 | P1 | first half of ChaChing, cut + record "boop" |
| SFX_Market_SoldCrate | Teleported into SOLD crate | 2 | P2 | SYN "pop" + wood thunk |
| SFX_Market_Bell_Sundown | Market close bell at 0:00 | 1 | P0 | SON/FS CC0 handbell; Quartz-locked |
| SFX_Market_Bell_Warning | 0:30 warning | 1 | P0 | same bell, two short rings |
| AMB_Market_Bed | Market area ambience | 1 loop | P2 | murmur-less: wind flags, wood stalls (avoid speech-like walla vs voice chat) |

## 10. Blame Ledger, Disaster & Payout

| Event | Trigger | Var | Pri | Source / note |
|---|---|---|---|---|
| STG_Disaster_Cut | OnDisaster → 0.6 s music silence | — | P0 | system (Modulation), no asset |
| STG_Disaster_RecordScratch | Severity ≥ 80 | 2 | P0 | FS CC0 record scratch / REC vinyl |
| STG_Disaster_River | Verb DroppedInRiver / LassoedIntoRiver | 2 | P0 | slide-whistle (REC/FS CC0) + SFX_Water_Splash_Big |
| STG_Disaster_Plank | Verb BrokePlank (with item loss) | 2 | P0 | trombone wah-wah-waaah (FS CC0; else REC/SYN brass) |
| STG_Disaster_Wagon | Verb TippedWagon | 2 | P1 | cymbal crash + wood avalanche |
| STG_Disaster_Generic | any other disaster verb | 2 | P0 | sad tuba "bwomp" |
| STG_Disaster_Sweetener | 2nd+ disaster within 3 s | 3 | P1 | short boing / kazoo blip |
| STG_Music_ThroatClear | First music resume after disaster per day | 1 | P1 | REC "ahem" + one banjo strum (only voice-like asset; keep < 0.5 s) |
| SFX_Ledger_Entry | Blame event logged (HUD ticker) | 3 | P2 | pencil scribble (REC) |
| SFX_Receipt_Printer_Loop | Payout receipt printing (MS_Receipt) | 1 loop | P0 | FS CC0 thermal/dot-matrix printer; tempo-matched to 108 BPM |
| SFX_Receipt_Line | Each receipt line | 3 | P1 | printer chunk |
| SFX_Receipt_Tear | Receipt complete | 2 | P1 | REC paper tear |
| STG_Ledger_Headline | Daily headline reveal | 1 | P0 | snare roll + gavel bang (SON/FS CC0) |
| SFX_Receipt_Stamp_Loss | Negative player line | 2 | P1 | rubber stamp + "wah" |
| SFX_Receipt_Stamp_Gain | Positive (Rescue/StealBack) line | 2 | P2 | stamp + bell ding |
| STG_Order_LineComplete | Order line filled | 1 | P0 | banjo lick (music team), Beat-quantized |
| STG_Order_Complete | All 3 lines | 1 | P0 | full band hit |
| SFX_Wallet_KaChing | Wallet up (diegetic) | 2 | P1 | coin purse jingle |
| SFX_Wallet_Wah | Wallet down / fee | 2 | P1 | muted trumpet "wah" |

## 11. UI & Timer

| Event | Trigger | Var | Pri | Source / note |
|---|---|---|---|---|
| UI_Menu_Hover | Hover | 2 | P1 | SYN soft wood tick |
| UI_Menu_Confirm | Confirm | 2 | P0 | wood block "tok" + small cowbell |
| UI_Menu_Back | Back/cancel | 1 | P1 | lower wood block |
| UI_Menu_Error | Invalid | 1 | P1 | goat "meh" (short, pitched) |
| UI_Ready_Up | Player readies | 1 | P1 | banjo pluck (per-player pitch: 4 notes of a chord) |
| UI_Ready_All | All ready | 1 | P1 | strum of the chord |
| UI_TwistCard_Reveal | Morning twist card | 2 | P0 | paper flip + diary pen scratch |
| UI_Order_Board | Morning Order shown | 1 | P1 | chalk scribble |
| UI_DuskVote_Open / Cast / Result | Dusk vote | 1 / 2 / 2 | P1 | owl hoot / pebble in jar / gavel |
| UI_Timer_PocketWatch_Tick | Last 60 s, Quartz eighth notes | 2 (tick/tock) | P0 | FS CC0 pocket watch / REC |
| UI_Ping_Marker | Player ping | 2 | P1 | SYN two-note whistle |
| UI_Emote_Shout | Shout emote (voice fallback; drives ducking in no-voice lobbies) | 4 per character | P0 | REC "HEY!"-style yell (avoid words beyond "hey"/"oi" for localization) |
| UI_Shop_Purchase / Insufficient | Shop | 2 / 1 | P1 | register / empty-pocket fabric flap |

## 12. Ambience (VS)

| Event | Trigger | Var | Pri | Source |
|---|---|---|---|---|
| AMB_Farm_Day | Global day bed | 2 loops | P0 | SON countryside ambiences (birds, light wind) — HPF 150 Hz |
| AMB_Farm_Morning_Rooster | Morning start | 3 | P1 | FS CC0 rooster |
| AMB_Barn_Interior | Barn volume | 1 loop | P1 | SON barn/room tone, distant animals |
| AMB_Wind_Gust | Random 20–40 s | 4 | P2 | SON |
| AMB_Farm_Dusk | Dusk bed (crickets, frogs) | 2 loops | P1 (VS-stretch) | SON night ambiences |

## 13. VS-stretch: Night (Alpha content, implement if time)

| Event | Trigger | Var | Pri | Source / note |
|---|---|---|---|---|
| SFX_Creature_Scarecrow_Move | Scarecrow relocates while unobserved (heard, not seen) | 4 | P0 when shipped | straw rustle + wooden post creak (REC hay + SON wood), ATT_Night_Creature |
| SFX_Creature_Scarecrow_Near | Within 10 m & unobserved | 2 loops | P0 | close straw breath (REC breath through straw, LPF) |
| SFX_Creature_Scarecrow_Freeze | Lantern freeze | 1 | P1 | SYN glassy shimmer |
| SFX_Creature_Scarecrow_Grab | KO touch | 2 | P0 | straw whoosh + cartoon bonk (keeps it "light horror") |
| STG_Night_SummonedCreature | SummonedCreature verb | 1 | P1 | low organ chord + crow caw |

---

## 14. Totals & Delegation
- VS events: ~120 events, ~380 source files (estimated before pitch/random reuse).
- P0 count drives the first sound-designer pass: footsteps (Grass/Dirt/Mud/Wood), grab/drop/throw, MS_Impact core banks + bruise layers, pig squeal/alert, chicken swarm/panic/alert, plank creak/crack/break, splashes + river bed, cha-ching + bells, disaster stingers, receipt printer, headline, order stingers, pocket-watch, UI confirm, shout emote.
- Every bark and stinger needs a caption id in `DT_AudioEvents` (onboarding-accessibility).
- Detailed per-file spec and recording sessions: delegate to `sound-designer`.
