# 15. Business, Platforms & Marketing

> Owner: Creative Director · Sources: `game-concept.md` (market research), `game-overview.md` (monetization, roadmap), `camcorder-replay.md`, `meta-progression.md`, `onboarding-accessibility.md`
> Values marked **(proposed)** are new in this chapter and need owner sign-off.

## 15.1 Market Thesis

The 2025–2026 "friendslop" wave (R.E.P.O., PEAK, Lethal Company, Schedule I, Meccha Chameleon, Gamble With Your Friends) shows a repeatable formula:

1. Physics + proximity voice + failure is the comedy engine. Failure is the clip.
2. A shared resource (quota, bank, tether) creates blame, and blame is funny.
3. Escalating quotas give the one-more-run pull; updates every 2–4 months keep it.
4. Low price ($8–15), low hardware requirements, goofy tone. **Streamers drive discovery.**
5. Systemic or daily variation instead of handcrafted content.

HARVEST PANIC fits every point, and adds the gap no hit owns: **loot that runs away.**

## 15.2 Platforms

| Platform | Status | Notes |
|---|---|---|
| Windows (Steam) | Launch | Primary audience |
| macOS Apple Silicon (Steam) | Launch | Development platform; an under-served co-op audience and a store differentiator |
| Steam Deck | Target "Playable" (proposed) | Gamepad support and readable UI are already in the accessibility spec |
| Consoles | Not planned | Revisit only after a hit; first-person physics co-op ports well |

Tech: Unity 6.3 LTS + URP, Netcode for GameObjects, Steam lobbies and transport (Facepunch.Steamworks). Hardware target: base M-series Mac and mid-range PC at 1080p.

## 15.3 Price and Packaging

| Item | Value |
|---|---|
| Base price | **$9.99** premium (canonical in `game-overview`) |
| Launch discount | 10–15% for the first week (proposed) |
| Friend 4-pack | Buy 4 copies at ~25% off (proposed: $29.99) |
| Release model | Early Access, then 1.0 after Region 3 (proposed) |
| Regional pricing | Steam recommended tiers |

**Monetization rules:** cosmetics only, most earned in game. Optional paid **Supporter Packs** after launch contain cosmetics only, clearly marked as purchases. No loot boxes, no paid currency, no gameplay power for sale. Free content updates (regions) drive retention and press.

Why $9.99: it sits between PEAK ($7.99) and R.E.P.O.-tier prices, low enough that a whole friend group buys it in one evening, which is the real unit of sale for a 4-player game.

## 15.4 Steam Page Hooks

**Short description (draft):** *Inherit Grandpa's cursed farm, catch livestock that won't stop running, haul it across a bridge that hates you, and pay the Landlord every Sunday. 1–4 player online co-op chaos.*

Capsule and trailer beats, in order of priority:
1. **The bridge shot.** Four farmers carrying a $300 pumpkin, a plank snaps, everything goes in the river. This is the capsule moment.
2. **The receipt.** "Dave: dropped pig into river ×3, −$240." Readable without sound.
3. **The pig walking back out** of the Sale Pen during the 2 s sale ("ka-ch..." stops).
4. **The Landlord's abacus** on Sunday, and the barn towed away with the pig on the roof.
5. **Night**: the Scarecrow that only moves when nobody looks.

Tags: Co-op, Online Co-op, Physics, Funny, First-Person, Farming, Party, Roguelite, Proximity Chat.

Trailer rule: the first 5 seconds must show a valuable good fleeing and a friend failing. Every shot passes the art test: *funny as a 15-second vertical clip with no sound.*

## 15.5 Demo and Steam Next Fest Plan

| Step | Content | Timing (proposed) |
|---|---|---|
| Closed playtest (Steam Playtest) | Muddlebury slice, Week 1, 4 players | Alpha |
| Public demo | Week 1 of Region 1 (5 days + one Sunday), pig, chicken, cow, prize pumpkin, Camcorder export on | ~2 months before Next Fest |
| Steam Next Fest | Same demo, with a streamer kit and a live dev stream showing bridge disasters | The Next Fest before EA launch |
| Demo end | Sunday payment, then "The Landlord wants more. Wishlist to continue." | |

The demo deliberately ends at the first Sunday, the strongest cliffhanger in the game. Demo progress does not carry over (proposed) to keep saves simple. KPI: wishlist conversion and the number of exported clips with the watermark.

## 15.6 Viral and Streamer Strategy

**Built-in clip moments.** The design targets a laugh roughly every 3 minutes and at least one bridge disaster per day. Streamers don't need to manufacture content; the bridge, the receipt and Sunday do it for them.

**Grandpa's Camcorder** is the marketing engine:
- Auto-saves the 30 s before every disaster (plus 3 s after).
- One-click export, **Vertical 1080×1920** (TikTok/Shorts) or **Horizontal 1920×1080**, max 45 s, encoded locally in under 20 s on a base M1.
- Burned-in VHS overlay, tabloid caption from the receipt and a small "HARVEST PANIC" watermark (toggleable). Every shared clip is an ad.
- Voices are excluded by default; including them needs every speaker's consent.

**Streamer support:**
- Streamer mode hides lobby codes and player names and hides chalkboard drawings.
- Calm mode and difficulty assists for family or cozy streams (unranked).
- Daily Eviction seed: every streamer plays the same farm, which creates comparison content ("the Swedish team got Rung 14").
- Streamer key program and a press kit with a pre-cut clip pack (proposed).

**Social:** a "Clip of the Week" community feature using the `#HarvestPanic` tag (proposed), reposted on the game's channels.

## 15.7 Community

| Channel | Purpose |
|---|---|
| Discord | Playtests, LFG, clip sharing, feedback, community votes |
| Steam forums and in-game feedback | Bug reports |
| TikTok / YouTube Shorts | Weekly clips from the community and the team |
| Community votes | Pick the next creature (e.g. Tax Goose vs Mimic Melon) |

Moderation: chalkboard wipe, hide-drawing settings and name sanitizing are already part of the design. Public lobbies stay host-moderated.

## 15.8 Post-Launch Content Cadence

Target: a meaningful update every **2–4 months**, in line with the genre.

| Update | Timing | Content |
|---|---|---|
| Early Access launch | — | Region 1 complete: 8 species, 7 curses, 3 night creatures, Rival Farmers, ~40 upgrades, ~120 cosmetics, Almanac, Eviction Mode |
| Update 1: Misty Riverlands | EA +2–3 months | Region 2, rafts, catfish, goose, otter, the Bog Lantern |
| Update 2: Haunted Orchard | EA +5–6 months | Region 3, night focus, 3 new creatures |
| Seasonal events | Ongoing | Harvest Fest (autumn), Halloween (Blood Moon Harvest), winter event (proposed) |
| Ongoing | — | Community-voted creatures, new modes (Bridge Builder Party, Saboteur Week, Daily Disaster), mod support |
| Expansion ideas (paid DLC candidates) | After 1.0 | Region 4 Sunburnt Ranch, Region 5 Frostbite Fjord (proposed: free vs paid to be decided) |

Rule: new regions and gameplay stay free during Early Access so the player base is never split. Paid add-ons are cosmetic Supporter Packs, and any paid region decision is deferred until after 1.0 (proposed).

## 15.9 Ratings and Age

| Item | Target |
|---|---|
| ESRB / PEGI | Aim for **E10+ / PEGI 7** (proposed): cartoon slapstick, no blood, no animal harm on screen, mild cartoon scares at night |
| Online interactions | Proximity voice and chalkboard are user-generated: IARC "Users Interact" descriptor |
| Steam content survey | No mature content; voice chat disclosed |
| Content rules that protect the rating | No gore VFX (art bible), animals are bruised with cartoon stars rather than injured, scares resolve into gags, flash limits for photosensitivity |

## 15.10 Success Metrics (proposed)

| Metric | Target |
|---|---|
| Wishlists before EA | 50k+ |
| Demo: players who reach the first Sunday | ≥ 60% |
| Clips exported per 100 play hours | Tracked from day one |
| Week 1 Sunday paid (first-time 4p) | ≥ 80% (design KPI) |
| Steam review score | "Very Positive" |
| Median session | ≥ 1 full week (1–2 h) |

**Status in build:** Planned. Steam lobbies and transport, LAN, and the cosmetics/wardrobe system are Built. Camcorder export, Steam page, demo build, leaderboards, streamer mode and Supporter Packs are Planned.
