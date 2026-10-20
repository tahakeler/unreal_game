# 1. Vision & Pitch

> Owner: Creative Director · Sources: `design/game-concept.md`, `design/gdd/game-overview.md`, `design/gdd/consistency-report.md`, `design/art/art-bible.md` · Engine: Unity 6.3 LTS + URP + Netcode for GameObjects + Steam

## 1.1 Elevator Pitch

**HARVEST PANIC** is a first-person online co-op physics comedy for 2–4 friends. Your crew inherits Grandpa's cursed farm, and his debts come with it. Every day you chase runaway livestock and crops that have grown legs, drag them across one terrible rope bridge to market, and try to sell them before sundown. Every Sunday the Landlord collects rent. The goods fight back, the weather fights back, the night fights back, and your friends drop the pig in the river.

## 1.2 Logline

*Four incompetent heirs, one shared wallet, one rickety bridge, and a Landlord who wants his money every Sunday.*

## 1.3 High Concept

| Field | Value |
|---|---|
| Genre | Co-op "friendslop" physics comedy with a roguelite debt loop |
| Camera | First-person; other players see your full body (user decision, 2026-10-11) |
| Players | 2–4 online (Steam lobbies, listen server); solo is playable with scaled quotas but is not the focus |
| Session | One Day = 10–15 min · one Week = 1–2 h · one Region ≈ 5.6–6 h |
| Platforms | Windows + macOS on Steam |
| Price | Premium, ~$9.99 (see Chapter 15) |
| Art | "A storybook farm that gets dented": warm, toy-like and hand-painted by day, cold-lit and crooked by night |
| Core verbs | Chase, grab, carry together, throw, cross, sell, blame |

**Player fantasy:** *"We are four incompetent heirs barely keeping a haunted farm afloat, and it is everyone's fault but mine."*

## 1.4 The "And Also" Test

> It's like **R.E.P.O.** (carry fragile loot together for a quota), **and also the loot runs away from you, screams, and has to be carried across a bridge that wants you dead.**

The difference from other quota-co-op games is that the valuables have their own goals. In R.E.P.O. and Lethal Company the loot is passive and the monsters are the threat. In HARVEST PANIC the loot is also the threat. A pig is worth $60, runs at 5.6 m/s, squeezes through fence gaps, and struggles in your arms. Every sale is a small chase followed by a small heist.

## 1.5 Comparable Titles and Positioning

| Title | What we take | What we do differently |
|---|---|---|
| **Lethal Company** | Escalating quota, proximity voice that makes death funny, light horror spikes | We are not a horror game. Night is optional (the Dusk vote) and is played for comedy. The quota is paid with money you earned on camera, not with scrap. |
| **R.E.P.O.** | Physics carrying of fragile valuables, bruise = lost value, upgrades between rounds | Goods flee and fight back. One fixed chokepoint (the Bridge) becomes the daily stage. |
| **PEAK** | Low price, cheap hardware, ragdoll failure, rescuing friends | We are a horizontal hauling game, not a climbing game. Our tension comes from the clock and the rent, not from a summit. |
| **Chained Together** | One person's mistake drops everyone | Multi-hand carries (a cow needs 3 carriers) and a shared wallet create the same "we all fall" feeling without a literal chain. |
| **Overcooked** | Time pressure and shouting-based coordination | Physics plus proximity voice instead of a recipe grid. The chaos is systemic, not hand-authored. |
| **Content Warning** | Filming your friends' failures is part of the product | Grandpa's Camcorder records automatically. You never have to choose between playing and filming. |
| **Meccha Chameleon** (2026 viral hit) | A single readable, clip-able mechanic that makes strangers laugh in 10 s | Our clip mechanic is "something valuable went into the river and here is who did it." |
| **Goat Simulator** | Physics absurdity as the point | We give the absurdity stakes: the rent. Failure is funny because it costs something. |
| **Gamble With Your Friends** (2026) | One shared bank account creates blame and betrayal | Our Blame Ledger itemizes exactly who cost what, and the optional Golden Egg betrayal is revealed on Sunday. |

### Positioning map

```
                  STAKES / STRUCTURE (quota, progression)
                               ^
          Lethal Company   R.E.P.O.        HARVEST PANIC
                               |           (high stakes, goods fight back)
   PASSIVE LOOT  <-------------+------------->  LOOT HAS LEGS
                               |
          Goat Simulator   PEAK            Overcooked (orders, not physics)
                               v
                     SANDBOX / LOW STAKES
```

Our corner is **high stakes + loot that fights back**. No current hit owns it.

## 1.6 Design Pillars

Four pillars, all falsifiable, all binding on every department.

### Pillar 1: Everything Has Legs
Every valuable thing fights to escape.
*Design test:* if an item just sits there, give it a personality.

| Do | Don't |
|---|---|
| Pigs squeeze through gaps, chickens scatter as a swarm, the prize pumpkin needs 4 carriers and splats | Add a valuable crate that just waits to be picked up (the build's generic crate is a placeholder, not a model) |
| A pig can walk back out of the Sale Pen during its 2 s acceptance | Make selling a menu click |
| Art: goods are more saturated than the world (Art V2 "Loot Pops, World Rests") | Let a valuable blend into the background |

### Pillar 2: Heavy Together, Blamed Together
Shared loads, a shared wallet and shared failure.
*Design test:* does this make players need or blame each other?

| Do | Don't |
|---|---|
| Carry speed scales with hands; a cow needs 6 hands (3 players) | Give a solo upgrade that removes the need to coordinate |
| One wallet; the receipt itemizes "Dave: dropped pig into river ×3, −$240" | Give private wallets or per-player scores that compete |
| Audio: the mix leaves 300 Hz–2 kHz clear for voice | Bury voice chat under music |

### Pillar 3: The Farm Gets Worse, You Get Better
Escalating quotas and curses against your growing toolkit.
*Design test:* does the week end harder than it started, and did the crew's choices close the gap?

| Do | Don't |
|---|---|
| Quota grows ×1.35 per week while median income grows ×1.25; upgrade choices close the gap | Make the quota flat or let grinding money solve everything |
| Chaos cap rises: 2 major modifiers in Week 1, 3 in Week 2, 4 from Week 3 | Throw every hazard at a Week 1 crew |
| Account unlocks are sidegrades and cosmetics | Sell or grant permanent stat power |

### Pillar 4: Failure Is the Content
Every disaster is funny, recorded and cheap to recover from.
*Design test:* is losing here a clip, or just a chore?

| Do | Don't |
|---|---|
| Daytime knockouts recover in place for free; barn respawn costs $25–55 depending on the week | Permadeath of a character, long respawn timers |
| Camcorder auto-saves the 30 s before every disaster | Ask players to remember to hit record |
| Disaster-rewarding Letters outnumber success Letters | Punish the player who caused the funniest moment beyond a sting on the receipt |

### Anti-pillars (what this game is NOT)
- **Not a combat game.** You herd, you don't fight. No weapons, no health bars on animals.
- **Not punishing.** No character permadeath, no lost account progress on a failed run.
- **Not a grind.** No farming-sim crop timers, no chores for their own sake.
- **Not pay-to-win.** No loot boxes, no paid currency, no gameplay power for sale.
- **Not gore horror.** Horror comes from lighting, never from blood (Art V4).

## 1.7 Tone and Humour Rules

The tone is **cozy slapstick with a crooked night**: Looney Tunes physics on a picture-book farm, with a Scooby-Doo level of scare after dark.

1. **Deadpan institutions, panicking people.** The Landlord, the receipt and Grandpa's notes are calm and bureaucratic. The players are the ones screaming. ("Storm today. Bridge will leave. Do not follow bridge.")
2. **Punch up at the debt, never down at a player.** Blame is a joke about a moment, not about skill. Nicknames ("Butterfingers", "Structural Engineer") rotate daily and can be disabled.
3. **Animals are never harmed on screen.** A bruised pig shows cartoon stars and a bandage decal. A "Ruined" good gets a sad sound, not a corpse. No blood VFX anywhere.
4. **Every scare resolves into a gag.** The Scarecrow only moves when nobody is looking, and it's comic, not traumatic. Night is opt-in.
5. **Bounded harm for pranks.** No social feature can spend wallet money, destroy goods, or take more than 5 s of control from a player.
6. **Writing voice:** short, specific, numerical. "−$240" is funnier than "a lot of money." Tabloid camcorder titles: "LOCAL MAN LOSES PIG, DIGNITY."
7. **Cultural care.** Myth-derived creatures are renamed if they borrow a sacred concept (e.g. "Copycat Coyote").

## 1.8 The "Laugh Moment" Philosophy

**Target: a laugh-worthy moment roughly every 3 minutes per crew**, and at least one shareable bridge disaster per day without prompting. This is a production target, not a hope.

How we manufacture clip-able chaos:

| Ingredient | Why it produces clips | System |
|---|---|---|
| A visible stake | Viewers instantly understand "that pumpkin was worth $300" | Goods value, floating price tags |
| A single chokepoint | Every haul passes the same camera-friendly stage | The Bridge (20 planks, mid-span sag) |
| Physics that can fail | Outcomes are surprising even to the players | Carry, ragdoll, plank breaking |
| A named culprit | The joke has a punchline: who did it | Blame Ledger |
| Automatic recording | The clip exists before anyone thinks to film | Grandpa's Camcorder |
| A voice reaction | Friends screaming is half the clip | Proximity voice |

**The clip formula:** *setup (something valuable) → tension (the bridge, the clock) → physics failure → named blame → replay.* Every new feature should strengthen at least one link in this chain. The art test applies too: *would it look funny in a 15-second vertical clip with no sound?*

## 1.9 Target Fantasy and Aesthetics (MDA)

| Rank | Aesthetic | How it shows up |
|---|---|---|
| 1 | **Fellowship** | Shared wallet, multi-hand carries, voice, blame and awards |
| 2 | **Sensation** | Physics slapstick, ragdolls, splats, the bridge sway |
| 3 | **Challenge** | The escalating weekly quota |
| 4 | **Discovery** | New species, curses, regions, the Almanac |
| 5 | **Expression** | Cosmetics, emotes, chalkboard, Crew Photo |
| 6 | Narrative | Light: Grandpa's letters and tapes, the Landlord |

Self-Determination Theory check: **Relatedness** through shared failure, **Competence** through beating harder Sundays, **Autonomy** through route, upgrade and Dusk choices.

**Status in build:** Partial. Pillars 1, 2 and 4 are playable in the Muddlebury slice (fleeing pig and chickens, multi-hand carry, shared wallet, receipt with blame lines, breakable bridge). Pillar 3 is partial (weekly quota and Landlord rules exist; upgrades do not). Camcorder and proximity voice are Planned.
