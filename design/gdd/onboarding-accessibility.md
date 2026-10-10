# Onboarding & Accessibility

> **Status**: Draft
> **Author**: ux-designer + game-designer (Batch D)
> **Last Updated**: 2026-10-10
> **Last Verified**: 2026-10-10
> **Implements Pillar**: Heavy Together, Blamed Together (everyone can play) · Failure Is the Content

## Summary
Teaches new players in under 5 minutes through "Grandpa's Tapes" (a playable first morning) and contextual tips, and makes the game playable without a microphone, without perfect hearing or vision, and with reduced motion. Streamers and mixed-skill friend groups are the main audience.

> **Quick reference** — Layer: `Polish` · Priority: `EA` · Key deps: `hud-menus`, `proximity-voice`, `audio-feedback`, `camcorder-replay`

## Overview
A first-launch tutorial day ("Grandpa's Tape #1") is a short scripted morning on Region 1 with one pig, one bridge crossing and one sale. Later concepts (wagon, tools, night, Sunday) are taught by just-in-time VHS "tape" popups (≤ 8 s, skippable) the first time they matter. Accessibility options cover voice-free play, subtitles/captions, colorblind modes, motion, input and difficulty assists.

## Player Fantasy
"I joined my friends' run with zero idea what to do and was hauling pigs in two minutes." For players without mics or with disabilities: "I'm a full member of the chaos, not a spectator."

## Detailed Design

### Core Rules: Onboarding
1. **Tape #1 (solo, offline, ~4 min):** steps: move/jump (20 s) → grab pig (pig flees slowly at 50% speed) → carry & throw into wagon → push wagon across bridge (one plank guaranteed to break, rescue prompt) → sell → mini receipt with "Grandpa: −$30 for the plank" (canonical patch price). Skippable at any time; auto-offered once, replayable from menu.
2. Joining a friend's lobby without Tape #1 done: offered "Quick Tape" (60 s video) while waiting in lobby; never blocks join.
3. **Just-in-time tapes (14 at EA):** wagon hitch, lasso, feed bucket, noise/whisper, night, Landlord Sunday, shop vote, Blame Ledger, camcorder, upgrades, weather, each new species (first sight). Shown once per account; max 1 per 90 s; never during carry or within 10 s of a disaster.
4. **New-player buddy:** in a lobby where one player has < 2 h played, their teammates see a small "rookie" sprout icon; the rookie's first 3 Blame events are captioned "(rookie)" on the receipt.

### Core Rules: Accessibility
| Area | Options (defaults in bold) |
|---|---|
| Voice-free play | **Holler** button (= player-character shout emote: Noise loudness 0.8, 2 s cooldown), **Shush** hold (quiet walk, voice loudness capped 0.2), **Ping wheel** (8 pings: "Here", "Help carry", "Pig!", "Danger", "Wagon", "Bridge", "Yes", "No"), quick-chat text with TTS option (off) |
| Mic sensitivity | Noise gain 0.5–1.5 (**1.0**); "Calm mode": voice never spooks animals (lobby-wide, host option, marks run unranked) |
| Subtitles/captions | Subtitles **on**; speaker names; critter barks and stingers captioned with direction arrows; size S/**M**/L/XL; background opacity 0–100% (**60%**) |
| Visual | Colorblind filters (Protan/Deutan/Tritan) + shape-coded bruise tiers; high-contrast outlines for goods; red-wearing warning (bull) shown as icon too |
| Motion | Camera shake 0–100% (**70%**); ragdoll camera "stable mode"; disable slow-mo zoom in replays; FOV 70–110 |
| Input | Full remapping, hold-to-toggle for grab/carry, aim assist for throws (**on** for gamepad), one-handed preset |
| Difficulty assists (unranked) | Critter speed −20%, sundown +20%, bruise damage −50% |
| Privacy | "Include my voice in clips" **off**; streamer mode hides lobby code and player names |
| Photosensitivity | Lightning/blood-moon flashes ≤ 3 per s, flash reduction toggle |

Assist use marks Eviction runs unranked; campaign unaffected (no penalties to meta-progression).

### States and Transitions
Tape #1: NotStarted → InProgress → Completed/Skipped. Each JIT tape: Unseen → Queued → Shown → Seen.

### Interactions with Other Systems
- **In/Out:** settings consumed by hud-menus, audio-feedback, camcorder-replay (voice consent, slow-mo), proximity-voice (Holler/Shush/Calm/gain), player-character (input), eviction-mode (ranked eligibility), critter-ai (assist speed scalar), day-loop (assist time scalar), goods-value (assist bruise scalar).

## Formulas
```
EffectiveVoiceLoudness = clamp(RawLoudness * MicGain, 0, 1); if Shush: min(.., 0.2); if CalmMode: critter-facing loudness = 0
TapeEligible = !Seen && time_since_last_tape ≥ 90 s && !Carrying && time_since_disaster ≥ 10 s
```

## Edge Cases
| Scenario | Behavior |
|---|---|
| Player skips Tape #1 then struggles | After 3 failed grabs in 60 s, a 4 s grab tip appears |
| Host enables Calm mode mid-run | Applies next day; run flagged unranked |
| Mic unavailable on Mac (EOS issue) | Auto-switches to voice-free kit with toast |
| All four players are rookies | Tapes still once each; day 1 quota −20% (week-quota hook, first run only) |
| Subtitles for overlapping barks | Max 3 caption lines, newest replaces oldest of lowest priority |

## Dependencies
| System | Direction |
|---|---|
| hud-menus, proximity-voice, audio-feedback | This depends on |
| camcorder-replay, eviction-mode, critter-ai, day-loop, goods-value, player-character, week-quota | Consume settings/assists |

## Tuning Knobs
| Knob | Default |
|---|---|
| Tape1 pig speed | 50% |
| JIT tape spacing | 90 s |
| Holler loudness / cooldown | 0.8 / 2 s (owned by player-character / proximity-voice) |
| Shush cap | 0.2 |
| Assist magnitudes | −20% / +20% / −50% |
| Rookie threshold | 2 h |

## Acceptance Criteria
1. 80% of first-time testers complete Tape #1 in ≤ 5 min; ≥ 90% deliver a good in their first real day.
2. A player with no mic can complete a full week using only Holler/Shush/pings (playtest).
3. Every audio cue that conveys gameplay has a caption (audit against audio-feedback bark list).
4. All options persist per account and apply without restart.
5. Meets Xbox Accessibility Guidelines for subtitles (size, contrast, speaker ID) as an internal bar.

## Comedy beats
- Tapes are Grandpa's 1994 home videos; he gets the instructions wrong and a younger Landlord corrects him off-camera.
- The Holler button plays a random rustic yell ("YEEHAW", "PIG!!!", "MY BACK").
- Rookie sprout icon wilts a bit with each Blame event, and blooms when they deliver a clean pig.
