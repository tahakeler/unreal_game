# Proximity Voice & Noise

> **Status**: Draft · **Author**: game-designer + network-programmer (Batch A) · **Last Updated**: 2026-10-10
> **Implements Pillar**: Failure Is the Content, Everything Has Legs
> **Quick reference**: Layer `Foundation` · Priority `VS` · Key deps: session-networking · **Owns contract: Noise Event**

## 1. Overview
Players talk with 3D positional voice through EOS Voice (RTC). Volume falls off with distance and is muffled by walls. How loud you speak matters: the system measures each player's mic loudness and turns loud speech into **Noise Events** that critters, cursed crops, night creatures and rivals hear. Impacts, shouts and footsteps create Noise Events too. Every voice mechanic has a non-voice fallback (the shout emote and quiet-walk).

## 2. Player Fantasy
"SHHH — it's right there." Whispering while sneaking up on a chicken, then screaming when the pig escapes and hearing it attract the Scarecrow. Your voice is both how you coordinate and how you sabotage yourselves.

## 3. Detailed Rules
1. **Voice backend:** EOS RTC voice, one room per session. 3D attenuation applied locally from the speaker's head position. Fallback: Steam voice with UE VoIP talker 3D (no RMS-from-backend; compute locally).
2. **Attenuation:** full volume ≤ 3 m, linear-log falloff to silent at 35 m. Occlusion: −9 dB per blocking wall (max 2 walls, line traces at 10 Hz).
3. **Special channels:** a KO'd player is muffled (−6 dB, low-pass 1 kHz). A respawning player is heard by everyone at 30% (ghost voice) for 5 s. A **radio** upgrade (`farm-upgrades`) gives a global push-to-talk channel at 60% with static.
4. **Loudness measurement:** each client computes mic RMS in dBFS on its own captured audio, at 20 Hz, smoothed (attack 50 ms, release 300 ms). It sends a quantized loudness (0–1, 8 bits) to the server at 10 Hz only when above the noise threshold.
5. **Voice → Noise Event:** when loudness ≥ 0.35 (≈ normal talking), the server emits a Noise Event at the speaker's position, at most once per 0.5 s per player.
6. **Non-voice noise sources** (server-side): impacts (from `physics-carry`), footsteps when sprinting, shout emote, tools (whistle, `farm-tools`), plank breaks (`bridge-hazards`), wagon rattle, critter squeals, mandrakes (`cursed-crops`).
7. **Calibration:** a per-player mic calibration in settings (speak normally for 3 s) sets `mic_gain_offset` so that normal speech maps to 0.4–0.5 loudness. Push-to-talk and open-mic are both supported. Muted players emit no voice noise.
8. **Privacy:** no voice is recorded except into the local Camcorder buffer (`camcorder-replay`), and only for players who opt in.

## Contract: Noise Event
The single message by which any sound in the world becomes an AI stimulus. **Owner:** proximity-voice. **Consumers:** `critter-ai`, `night-creatures`, `cursed-crops`, `rival-farmers`; also read by `blame-ledger` and `camcorder-replay`.

```
struct FNoiseEvent
{
    FGuid        EventId;      // unique per event
    ENoiseSource Source;       // Voice, Shout, Footstep, Impact, Tool, Hazard, Critter, Crop, Vehicle, Scripted
    float        Loudness;     // 0.0–1.0 normalized (see table)
    float        Radius;       // metres, derived: Radius = RadiusForLoudness(Loudness) * env_mult
    FVector      Location;     // world position (server)
    AActor*      Instigator;   // player pawn, critter, item or null; the "who made the noise"
    APlayerState* BlamedPlayer;// player responsible (or null), for blame-ledger
    FGameplayTagContainer Tags;// e.g. Noise.Scream, Noise.Splash, Noise.Metal, Noise.Mandrake
    double       ServerTime;   // seconds, server clock
};
```
**Rules**
1. Created **only on the server** via `UNoiseSubsystem::Emit(FNoiseEvent)`. Clients request (voice loudness, emote) and the server validates.
2. Delivered to listeners in range via UE AI Perception (Hearing sense, `ReportNoiseEvent` with `Loudness` and `MaxRange = Radius`) **and** a multicast delegate `OnNoise(const FNoiseEvent&)` for non-AI systems. Not replicated to clients by default (debug view only).
3. **Radius**: `Radius = 4 + 46 * Loudness^1.5` metres (0 → 4 m, 0.5 → 20 m, 1.0 → 50 m), times `env_mult` (fog 0.8, storm 0.6, night 1.25 — supplied by `weather-twists` / `day-loop` Day Modifiers).
4. **Listener hearing**: a listener hears the event if `distance ≤ Radius * listener_hearing_mult`. Perceived strength = `Loudness * (1 - distance/Radius)`. Each consumer decides its reaction (flee, attract, wake) from perceived strength and Tags; this contract does not.
5. **Rate limit**: max 1 event per Instigator per 0.25 s per Source; louder events within the window replace quieter ones. Global cap 60 events/s (oldest-quietest dropped).
6. **Reference loudness table** (consumers tune against these):

| Source | Loudness |
|---|---|
| Quiet-walk footstep | none |
| Walk footstep | 0.10 |
| Sprint footstep | 0.25 |
| Normal talk (voice) | 0.35–0.55 |
| Shout emote / yelling voice | 0.8 |
| Whistle tool | 0.7 |
| Light impact (300–800 N·s) | 0.3 |
| Heavy impact / item break (> 800 N·s) | 0.6 |
| Plank break / splash | 0.75 |
| Screaming mandrake | 0.9 |
| Scripted (Landlord bell, etc.) | 1.0 |

7. **Versioning**: new Sources and Tags may be added; fields may not be removed or re-typed without updating every consumer GDD.

## 4. Formulas
**Voice loudness**
```
loudness = clamp((rms_dbfs + mic_gain_offset - (-50)) / 40, 0, 1)
```
−50 dBFS → 0; −10 dBFS → 1. Normal speech (≈ −30 dBFS) → 0.5. mic_gain_offset range −15 to +15 dB.

**Voice attenuation**: `gain_db = dist ≤ 3 ? 0 : -20*log10(dist/3)`, silenced at 35 m (≈ −21 dB, then fade to −∞ over the last 3 m) plus occlusion −9 dB per wall.

## 5. Edge Cases
- **No mic / muted:** the player is noise-silent by voice. The shout emote gives the same gameplay access. No mechanic requires voice (assumption 5).
- **Streamer music or keyboard noise on open mic:** the noise gate (−50 dBFS) and the calibration step suppress it. A "voice draws critters" toggle exists in host settings (default ON).
- **Mac EOS voice risk:** if EOS RTC fails to init on macOS, fall back to Steam voice and compute loudness locally. Flagged as a VS risk — prototype in week 1.
- **Spoofed loudness (modded client):** the server clamps reports to 1.0 and rate-limits. Low cheat stakes (co-op).
- **Spectating/respawning players:** their voice does not emit Noise Events.
- **Event storm (cow stampede on the bridge):** the global cap of 60/s drops the quietest events first.

## 6. Dependencies
- **Upstream:** `session-networking`; EOS SDK / Steam voice.
- **Producers feeding Noise Events:** `player-character`, `physics-carry`, `farm-tools`, `wagon-vehicles`, `bridge-hazards`, plus Batch B critters/crops.
- **Consumers:** `critter-ai`, `night-creatures`, `cursed-crops`, `rival-farmers`, `blame-ledger`, `camcorder-replay`, `onboarding-accessibility`, `weather-twists` (env_mult).

## 7. Tuning Knobs
| Knob | Default | Range |
|---|---|---|
| VoiceFullRadius / SilentRadius | 3 / 35 m | 2–5 / 20–50 |
| OcclusionPerWall | −9 dB | −3 to −15 |
| VoiceNoiseThreshold | 0.35 | 0.2–0.6 |
| VoiceNoiseInterval | 0.5 s | 0.25–2 |
| NoiseRadiusMin / Max | 4 / 50 m | — |
| NoiseExponent | 1.5 | 1–2.5 |
| GlobalNoiseCap | 60/s | 30–120 |
| VoiceDrawsCritters (host toggle) | ON | ON/OFF |

## 8. Acceptance Criteria
- Two players: voice audible at 10 m, inaudible at 36 m, quieter behind a barn wall (measured −9 dB ± 2).
- Yelling (≥ −15 dBFS) next to a chicken with Hearing sense makes it perceive a Noise Event with Loudness ≥ 0.8 within 200 ms.
- Normal-volume talk after calibration lands at 0.35–0.55 loudness for 4/5 testers.
- Shout emote and whistle produce events with the table loudness ± 0.05.
- No consumer system reads voice audio directly; all go through FNoiseEvent (code review check).
- EOS voice works on Apple Silicon or the Steam fallback is shipped in the VS build.

## Comedy beats
The "everybody SHUT UP" moment: someone laughs at a fall and the chickens scatter. Screaming because the Scarecrow moved summons more creatures. Ghost voice of a drowned friend narrating from beyond. Whispered sneak-ups that end in a shriek are perfect 10-second clips.
