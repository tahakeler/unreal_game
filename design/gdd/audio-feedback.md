# Audio & Comedy Feedback

> **Status**: Draft
> **Author**: game-designer + ux-designer (Batch D)
> **Last Updated**: 2026-10-10
> **Last Verified**: 2026-10-10
> **Implements Pillar**: Failure Is the Content · Everything Has Legs

## Summary
The sound of the farm: MetaSounds-driven impacts and bruises that sound worse the more money you lose, critter vocal "barks" that telegraph intent, adaptive music states that track the day's tension, and comedy stingers on disasters. It must stay readable under four people screaming on voice chat.

> **Quick reference** — Layer: `Presentation` · Priority: `Alpha` · Key deps: `goods-value`, `critter-ai`, `day-loop`, `blame-ledger`, `proximity-voice`

## Overview
Audio is organized into five buses (Voice, Critters, Impacts, Music, UI/Stingers) with a ducking matrix that always prioritizes proximity voice. Impacts are parameterized by mass, velocity and $ lost. Critters announce state changes (flee, charge, hide) with distinct barks. Music runs on a DSP-clock-scheduled state machine tied to day phase and chaos level.

## Player Fantasy
Every bonk is cartoon-crisp and tells you exactly how much it cost. You hear the bull winding up before you see it. When the pig hits the water, the band stops.

## Detailed Design

### Core Rules
1. **Mix priority (ducking):** Voice ducks Music −6 dB and Critters −3 dB while any player within 15 m is speaking. Stingers duck Music −9 dB for their length. Voice is never ducked.
2. **Impacts (MetaSound `MS_Impact`):** inputs `Mass (kg)`, `ImpactSpeed (m/s)`, `MaterialPair`, `DollarLoss`. Pitch down with mass; adds a "crunch" layer when DollarLoss > $10, and a "cash register clunk" sweetener when DollarLoss > $40. Max 12 concurrent impact voices; quietest stolen.
3. **Bruise feedback:** each bruise plays a short squeak/squelch tied to bruise type (goods-value bruise taxonomy) plus a coin-drop tick whose count = floor(DollarLoss / 10), max 5.
4. **Critter barks:** each Critter Personality state transition has a bark: Idle (ambient, 8–15 s random), Alert (rising), Flee (panic squeal), Charge wind-up (0.8 s telegraph, mandatory), Hide (muffled giggle for cursed crops). Bark cooldown per critter 2 s; global max 6 barks/s.
5. **Music states:** Morning (calm banjo), Roundup (bluegrass loop, intensity layers 1–3 by Chaos Level), Crunch (last 60 s: tempo +15%, fiddle), Dusk (sparse, creepy pump organ), Night Threat (when a night creature is within 30 m of any player), Market (accordion), Sunday (Landlord theme, oompah in minor). Transitions on bar boundaries (DSP-clock scheduling) except Disaster.
6. **Disaster stinger:** on `OnDisaster` music hard-cuts to silence for 0.6 s (record-scratch on Severity ≥ 80), then a 1.5 s comedic stinger by verb (river: slide-whistle + splash; plank: trombone wah-wah; wagon: cymbal crash), then music resumes at layer −1.
7. **Noise awareness:** sounds that the game counts as Noise Events (proximity-voice contract) get an audible "echo tail" so players learn loud = consequences.
8. **Diegetic UI:** pocket watch ticks in last 60 s; wallet "ka-ching" on sale, "wah" on loss.

### States and Transitions
Music: Morning → Roundup ↔ Crunch → (Dusk ↔ Night Threat) → Market/Payout; any → DisasterCut → previous. Sunday is exclusive.

### Interactions with Other Systems
- **In:** impact/bruise events (goods-value, physics-carry), critter state (critter-ai, livestock-roster, cursed-crops, night-creatures), day phase/timer (day-loop), OnDisaster (blame-ledger), voice activity (proximity-voice), settings (onboarding-accessibility).
- **Out:** none gameplay-affecting (audio is presentation only; Noise Events are emitted by gameplay systems, not by this one).

## Formulas
```
ChaosLevel = clamp( 0.4*FleeingCritters/8 + 0.3*ActiveMajorModifiers/2 + 0.3*DisastersLast60s/2 , 0, 1)
MusicLayer = 1 + floor(ChaosLevel * 2.99)     // 1..3
ImpactGain_dB = −24 + 18 * clamp(ImpactSpeed/12, 0, 1) + 6 * clamp(Mass/400, 0, 1)
```

## Edge Cases
| Scenario | Behavior |
|---|---|
| Multiple disasters within 3 s | One stinger; second plays only a sweetener |
| Voice disabled lobby | Ducking driven by emote shouts instead |
| Night Threat and Crunch simultaneously | Night Threat wins; Crunch adds ticking layer |
| 40 chickens barking | Swarm uses one group emitter with density parameter |
| Replay playback | Camcorder replays game audio; stingers re-trigger; voice excluded unless consented |

## Dependencies
| System | Direction |
|---|---|
| goods-value, physics-carry, critter-ai, day-loop, blame-ledger, proximity-voice | This depends on |
| onboarding-accessibility | Provides captions for barks/stingers from this |
| camcorder-replay, hud-menus | Use this |

## Tuning Knobs
| Knob | Default |
|---|---|
| VoiceDuckMusic | −6 dB |
| StingerDuck | −9 dB |
| MaxImpactVoices | 12 |
| BarkCooldown | 2 s |
| ChargeTelegraph | 0.8 s |
| DisasterSilence | 0.6 s |
| ChaosWeights | 0.4/0.3/0.3 |

## Acceptance Criteria
1. Bull charge bark is audible over 4-player voice in a blind test (≥ 8/10 testers react before impact).
2. Audio thread CPU ≤ 1.5 ms on base M1 in worst-case chicken swarm.
3. Music transitions land on bar boundaries (no audible seams) except disaster cuts.
4. Every critter state has a bark and a caption id.

## Comedy beats
- Music stops dead when the pig goes over the rail; the banjo player "clears his throat" before resuming.
- Cash-register clunk on expensive bonks makes losses audible as money.
- The Landlord's theme is a cheerful oompah that subtly modulates to minor if you're short.
