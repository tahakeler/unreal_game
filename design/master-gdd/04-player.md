# 4. The Farmer (Player Character & Controls)

> Sources: `design/gdd/player-character.md`, `physics-carry.md`, `character-customization.md`, `consistency-report.md`; code `Gameplay/Config/PlayerTuning.cs`, `Gameplay/Player/*`, `Customization/FarmerEmotes.cs`, `Prefabs/Player/Input/PlayerControls.inputactions`.
> Pillar: **Failure Is the Content**.

## 4.1 Design Intent

The farmer is a clumsy heir who is competent for about four seconds at a time. Moving around should feel responsive and fair. Falling over should feel earned and funny, never random. Getting back up is fast, so a fall is a joke and not a punishment.

Three rules shape everything in this chapter:

1. **The body is honest.** Every fall has a cause the player can name: "I was sprinting with the pumpkin, tired, in mud."
2. **Recovery is cheap; mistakes are expensive.** A knockdown costs seconds. The money cost comes from what you dropped, not from the fall itself.
3. **Your friends see the slapstick.** You play in first person, but other players see your full body, so every pratfall is a show for the rest of the crew.

Status in build: Built (the intent is met by the current controller).

## 4.2 Camera & View

| Item | Value | Notes |
|---|---|---|
| View | First person | Like R.E.P.O., Lethal Company and PEAK. The body turns with the camera. |
| Eye height | 0.64 m above the capsule centre (≈1.6 m from the ground) | `EyeAboveCentre` |
| Base FOV | 90° | Range 80–110 (an accessibility option) |
| Sprint FOV kick | +8° | Blends at speed 8/s |
| Head bob | 1.6 cm at 1.8 Hz (walk), scaled by ground speed | 0 in comfort mode (proposed) |
| Camera shake on impact | up to 4°, full at 900 N·s, decays 2/s | Strength = impulse / 900 |
| Pitch limit | ±85° | |
| Mouse sensitivity / pad look | 0.12 / 160°/s | Rebindable in options |

- **Local view:** you see your own first-person arms. You never see your own full body while you are standing.
- **Other players:** they always see your full third-person farmer with cosmetics, name tag and emotes.
- **Ragdoll and knockout camera:** the camera attaches to the ragdoll's head and tumbles with it, so you watch yourself fall. It snaps back to eye level when you get up.
- **Targeting:** there is a small dot in the centre of the screen. A trace runs 1.8 m forward from the camera and drives the interaction prompt (§4.9).

Status in build: Built.

## 4.3 Movement

| Parameter | Value | Code knob |
|---|---|---|
| Walk | 4.0 m/s | `WalkSpeed` |
| Sprint | 6.5 m/s | `SprintSpeed` |
| Crouch (quiet-walk) | 2.0 m/s | `CrouchSpeed` |
| Jump apex | 1.1 m | `JumpApex` |
| Gravity (controller) | 20 m/s² | Snappier than real gravity, on purpose |
| Ground / air acceleration | 40 / 8 m/s² | Responsive on the ground, limited steering in the air |
| Capsule height stand / crouch | 1.8 / 1.2 m, radius 0.35 m | Standing up from a crouch checks for headroom first |
| Body mass | 70 kg | Used when friends carry or throw you |

**Speed while carrying:** your speed is multiplied by the carry speed factor from Chapter 5 (0.35–1.0).

**How the speeds compare to the animals:** a pig sprints at 5.6 m/s and a chicken at 6.2 m/s, so a sprinting player can run both down. A bull sprints at 6.5 m/s, the same as the player, so you can never outrun it. This is intentional and still an open question for the owner (see consistency report Q5).

**Crouch = quiet-walk ("Shush"):** crouched footsteps make no noise. Walking makes a footstep noise of 0.10 every 1.5 m. Sprinting makes 0.25 every 2.2 m. Animals hear these sounds (Chapter 6, §6.4), so sneaking up on an animal is a real tactic.

**Dive (planned):** costs 20 stamina and lunges you 3 m forward. It ends in a 0.8 s belly-flop. If you touch an animal during the dive, you grab it. The dive is in the design but not in the code yet.

Status in build: Partial. Walk, sprint, crouch and jump are built and tuned. Dive is Planned.

## 4.4 Stamina

| Parameter | Value |
|---|---|
| Maximum | 100 |
| Sprint drain | 15/s |
| Carry drain | 8 × load_ratio per s (0–8/s; this adds to the sprint drain) |
| Regeneration | 25/s after a 1.0 s delay, but only when you are not sprinting |
| Exhaustion | At 0, sprint is disabled until stamina is back to 30 |
| Low-stamina weakness | Below 20 stamina, your ragdoll threshold is multiplied by 0.7 |
| Lasso tug (planned) | 10/s while pulling on a lassoed animal |
| Dive (planned) | 20 per dive |

The worst case is sprinting while fully loaded, which drains 23/s. That empties a full bar in about 4.3 s, which matches the "competent for four seconds" fantasy. Stamina is the main reason a heavy carry turns into a pratfall. A tired player is weaker, slower to recover and easier to knock over.

Status in build: Built (`StaminaModel`).

## 4.5 Knockdown: Ragdoll, Shove & Trip

### What makes you ragdoll

| Trigger | Rule |
|---|---|
| Hard impact | Impulse greater than the ragdoll threshold (formula below) |
| Loaded trip | Sprinting with load ≥ 70% on a slope steeper than 25° |
| Plank break | Standing on a plank when it breaks (Chapter 7) |
| Bull charge | 1,800 N·s, which is a straight knockout (§4.6) |
| Goat headbutt | 600 N·s. On its own this is below 900, but it knocks a tired, loaded farmer down |
| Faint emote | A voluntary 2 s ragdoll, and you drop whatever you are holding |

**Ragdoll threshold**
```
threshold = 900 N·s × (1 − 0.4 × load_ratio) × (stamina < 20 ? 0.7 : 1.0) × (in mud ? 0.8 : 1.0)
```
The threshold ranges from 302 to 900 N·s. A farmer who is fully loaded, tired and standing in mud is about three times easier to knock over. That is exactly where the bridge sits.

When you ragdoll, you are launched at 0.006 m/s per N·s of impulse, capped at 12 m/s. **Anything you were holding is released with your hand's velocity, so it flies.**

### Shove (proposed)

The source GDDs do not define a player-on-player shove. Proposed rule: tap Interact on a teammate who is upright to make a 300 N·s "nudge". This is below the ragdoll threshold of a fresh, empty-handed player, but it can topple a teammate who is tired and loaded. Cooldown 2 s. It is logged in the Blame Ledger as `YeetedTeammate` only if it causes a drop or a river fall. The goal is "help them along" comedy without free griefing. The design rule is unchanged: you can only *grab* a teammate who is knocked out.

### Recovery

- Get-up becomes available once the body moves slower than 0.5 m/s for 0.5 s. Press **Jump** to stand up (1.2 s animation). The prompt reads "SPACE: Get up".
- Button-mashing does nothing. This is an accessibility choice.
- If you get up inside geometry, you are moved to the nearest walkable point within 2 m.
- The server decides ragdoll state. The client predicts the start of a ragdoll, and the server corrects it within 200 ms.

Status in build: Partial. Ragdoll, the threshold formula, launch, get-up and faint are Built. Shove is Planned (proposed).

## 4.6 Knockout, Revive & Barn Respawn

**Knockout (KO):** you are knocked out by an impulse ≥ 1,800 N·s (2× the ragdoll threshold) or by 3 ragdolls within 20 s.

| KO rule | Value |
|---|---|
| Duration | 8 s, with stars over your head. Your voice still works but is muffled by 50% |
| Revive | A teammate holds E for 1.5 s. The prompt reads "Tap E: Carry / Hold E: Revive" (a tap is anything under 0.25 s) |
| Carry a KO'd friend | They count as a 70 kg body that needs 2 hands. You can carry them and throw them (yes, into the wagon) |
| Everyone KO'd | Everyone gets up after 8 s. There is no daytime wipe |
| Cost | **Free.** A daytime KO recovers where you fell |

**Barn respawn is the only thing that costs a fee.** You respawn at the barn after 5 s when:

| Cause | Blame verb |
|---|---|
| Falling into the river or below the kill height (−25 m) | `Drowned` |
| Being knocked out by a night creature | `KnockedOut` |
| Being stranded outside the barn or market zone at day end | `Stranded` |

The fee comes out of the shared wallet. It goes up each week: **$25 / 30 / 40 / 45 / 50 / 55**. It is charged to the player who died, so the Ledger can say "Dave drowned 4 times: −$100". If the wallet is short, it can go down to the −$200 overdraft floor. Anything beyond that becomes a Landlord IOU. If someone throws a KO'd friend into the river, the friend gets `Drowned` and the thrower gets `YeetedTeammate` (the throw counts for 6 s afterwards).

Status in build: Partial. KO, revive, carrying KO'd players, river/kill-height respawn and the fee hook are Built. Night-creature KO and stranding are Planned with their systems.

## 4.7 First-Person Arms

Your own arms are drawn as simple cartoon tubes. They are posed by code, not by animation clips, so they react to whatever you are holding.

| Pose | Behaviour | Key values |
|---|---|---|
| Idle | Loose fists, low in front of you | Hand at (±0.19, −0.25, 0.52) m in camera space |
| Reach | Hands move toward a grabbable you aim at | Up to 0.52 m reach, 65% blend |
| Grip | Palms press on the sides of the held object, near its top edge | Up to 0.42 m half-spread; arms can stretch to 1.6× for wide items |
| Throw | Wind back 0.16 m as you charge, then a 0.3 s swing | |
| Sway | Breathing, lag when you turn, bob when you walk | Look-sway capped at 3.5 cm |

The sleeve, cuff, glove and skin colours come from your cosmetic loadout. The arms cast no shadows on themselves. Held objects float 1.05 m ahead of your eyes and 0.3 m below your eye line. Tools are held rigidly at your right hand.

Status in build: Built (EditMode tests cover the arm IK, poses and grip).

## 4.8 Control Scheme

| Action | Keyboard & Mouse | Gamepad | Notes |
|---|---|---|---|
| Move | W A S D | Left stick | |
| Look | Mouse | Right stick | |
| Jump / Get up | Space | A / Cross (south) | Also stands you up from a ragdoll |
| Sprint | Left Shift (hold) | Left stick click | |
| Crouch / quiet-walk | Left Ctrl or C (hold) | Right stick click | |
| Interact: grab / drop / revive | E (tap; hold to revive) | X / Square (west) | Tap again while holding to drop |
| Throw | Left mouse (hold to charge, release to throw) | Right trigger | Full charge after 1.0 s |
| Use tool | Right mouse (hold) | Left trigger | Net swing, feed shake |
| Holler (shout) | H | Y / Triangle (north) | Noise 0.8, 2 s cooldown |
| Emote slots 1–4 | 5 / 6 / 7 / 8 | D-pad (proposed) | Wave, Point, Dance, Faint by default |
| Push-to-talk / open voice | V (proposed) | Back/View (proposed) | Proximity voice is open-mic by default |
| Tool slot swap | 1 / 2 (proposed, planned) | LB / RB (proposed) | Changing tools takes 0.4 s |
| Pause / menu | Esc | Start | |

You can rebind every action. Holding an action never requires mashing a button.

Status in build: Partial. Every binding without a "proposed" tag is in `PlayerControls.inputactions` and `FarmerEmotes`. The gamepad emote bindings, tool slots and voice key are Planned.

## 4.9 Interaction Prompts

There is one line of text under the crosshair. It comes from the carry system (`PromptFormat`).

| Situation | Prompt |
|---|---|
| Aiming at a grabbable | `E: Grab (have/need hands)`, e.g. `E: Grab (2/8 hands)` |
| Aiming at a KO'd friend | `Tap E: Carry  Hold E: Revive`, then `Reviving... 60%` |
| Holding something | `E: Drop  LMB: Throw` plus the tool's use prompt |
| Not enough hands | `(dragging 2/8 hands)` |
| Charging a throw | `[75%]` |
| Ragdolled at rest | `SPACE: Get up` |
| Knocked out | `Knocked out (5s)` |

Key names change to match the active device (proposed for gamepad glyphs).

Status in build: Built (text; device glyphs Planned).

## 4.10 Holler, Emotes & Ping

Players who do not want to use a microphone still need to be loud and expressive. Every one of these actions is also a game mechanic, because animals react to noise.

| Action | Duration / cooldown | Game effect |
|---|---|---|
| **Holler** (H) | 2 s cooldown | Noise 0.8 from your head. Scares nearby animals, and you can use it to call your friends |
| **Wave** | 1.5 s / 1.5 s | Noise 0.6, a friendly shout |
| **Point** | 1.2 s / 1.2 s | Places a coloured ping where you aim (up to the ping range), in your name-tag colour |
| **Dance** | Loops until you move / 1 s | Cosmetic only |
| **Faint** | 2 s / 5 s | A real ragdoll. You drop whatever you are holding. Strategic or self-sabotage |

The server enforces the cooldowns and a rate limit for each player. You can swap emotes in the barn wardrobe. Starter emotes are free, and more are unlocked through meta-progression.

Status in build: Built.

## 4.11 Edge Cases

- **You ragdoll while holding something:** the item flies off with your hand's velocity. If it bruises, it is your fault.
- **You ragdoll on a moving wagon:** your body keeps the wagon's velocity.
- **You join a game in progress:** you spawn at the barn with 2 s of protection.
- **Cheating on grabs:** the server rejects any grab farther than 1.8 + 0.75 m away.

## 4.12 Acceptance Criteria

- A fully loaded sprint down a 30° slope ragdolls you every time. An empty-handed sprint never does.
- Get-up is offered within 1.5 s of the body coming to rest.
- One teammate can pick up, carry and throw a KO'd player.
- A river fall in Week 1 respawns you at the barn after 5 s, takes $25 from the wallet and logs `Drowned`.
- Holler creates a Noise Event with loudness 0.8.
