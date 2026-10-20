# 13. Character Customization

> **Sources**: `design/gdd/character-customization.md`, `design/art/character-style.md`, `frontend-lobby.md`; built state checked against `HarvestPanic/Assets/Scripts/Customization/`.
> **Pillar**: Failure Is the Content (a well-dressed disaster makes a better clip). **Aesthetic**: Expression, Fellowship.

"My friends will recognise my idiot from across the field." The game is first-person: you see your own hands and gloves, and everyone else sees your whole farmer. Cosmetics exist mostly **for other people**, so the wardrobe preview, the lobby mirror, the Crew Photo and Camcorder replays are how you see yourself. Cosmetics never change gameplay, with two deliberate, flagged exceptions (13.6).

---

## 13.1 The Farmer

- **Base model**: the CC0 Quaternius animated Farmer (Idle, Walk, Run, Wave, Death, Point clips), imported as a Generic rig. Dance and Jump clips are authored in-house. The model's built-in straw hat was removed so the Hat slot owns the silhouette.
- **Fantasy**: a cheerful, slightly unqualified farmhand on day one in borrowed overalls.
- **Proportions**: chunky, toy-like; head about 1.15× and hands about 1.2× so they read well in first-person.
- **Uniform**: overalls (the largest coloured area, your team colour by default), shirt, boots and gloves. Each is a colour zone driven by the loadout.
- **Same collision for everyone**: body type is purely visual.

Status in build: **Built** — farmer prefab, colour zones, animations and both body types.

---

## 13.2 The Loadout

Each player has one **Farmer Loadout**. It is tiny (20 bytes on the network, under the 32-byte budget), saved locally, sent to the host when you join, checked, and shared with everyone.

| Slot | VS content (built) | EA target |
|---|---|---|
| **Body** | Sturdy ("Built like a hay bale. Falls like one too."), Lanky ("Can reach the top shelf. Cannot reach a decision.") | 4 |
| **Colours** | Overalls, Shirt, Boots/Gloves — each from a fixed **24-swatch palette** with names (Denim Blue, Pumpkin Orange, Clover Green, Lilac, Barn Brick, Mustard, Pond Teal, Sky, Bubblegum, Landlord Plum, Moss, Rust, Navy, Charcoal, Cream, Snow, Sand, Mud Brown, Wheat, Mint, Lemon, Coral, Slate, Hot Pink). No free colour picker, so colours stay readable on the farm. | + patterns |
| **Skin tone** | 12 tones (Buttermilk → Black Walnut), all free forever, plus 2 fantasy tones (Pickled, Moonstruck) unlocked by play | + Spooked Pale |
| **Hat** | 10 (13.3) | ~40 |
| **Face** | None, Grandpa Moustache, Safety Goggles, Sunglasses, Clown Nose | 15 |
| **Back** | None, Harvest Basket Backpack, Cape, Pitchfork Holster | 12 |
| **Name tag** | 8 colour + shape pairs (13.5) | 8 |
| **Emotes** | 4 equipped on keys 5–8 | 4 free + 10 unlockable |

Starter loadout: Sturdy, Denim Blue overalls, Cream shirt, Mud Brown boots, Straw Hat, Wave/Point/Dance/Faint. A first launch instead rolls a random, silly loadout so new players already look distinct.

Status in build: **Built** for all VS slots listed; gloves and overall patterns are **Planned**.

---

## 13.3 Hat Catalogue (VS, built)

Hats are the primary silhouette. **Silhouette rule**: every hat changes the head outline by at least 30% of head height or 40% of width, so teammates are identifiable at 40 m by hat + colour alone.

| Hat | Flavour | Unlock |
|---|---|---|
| Straw Hat (default) | "Smells like Grandpa. Smells like regret." | Starter |
| Trucker Cap (takes overalls colour) | "Free with any purchase of 400 lb of feed." | Starter |
| Cowboy Hat | "The bull has opinions." | Starter |
| Chef Toque | "Yes, chef. No, chef. Where's the pig, chef?" | Grandpa's Trunk |
| Flower Crown | "Picked from the cursed patch. Probably fine." | Trunk |
| Viking Horns | "Raids barns. Only his own." | Trunk |
| Bucket Helmet | "Holds water. Doesn't hold dignity." | Letter: fall in the river 10 times |
| Traffic Cone | "Road works ahead. The road is you." | Letter: blamed for $1,000 in one day |
| Grandpa's Fedora | "He wore it to every bank meeting. It never helped." | Letter: finish Day 1 |
| Pumpkin Head | "The Scarecrow thinks you're cute." | Clear Region 1 |

Pumpkin Head and Bucket Helmet would block the wearer's view, so in first person they show a light vignette instead of the mesh.

**Later catalogue** (character-style): Lantern Cap, Seedling Sprout, Mushroom Cap, Watermelon Helmet, Too-Tall Top Hat, Red Cowboy Hat, Chicken Hat, Rubber Duck Crown, Beekeeper Veil, Cone of Shame; face items Hay Beard, Monocle, Sunflower Glasses, Cucumber Eye Slices; back items Rope Coil, Hay Bale Backpack, Honey Jar Pack, Watering Can Jetpack, Mini Windmill (spins when running), Scarecrow Buddy, Grandpa's Camcorder, Crow Passenger, Duck Floatie; gloves Oven Mitts, Rubber Dish Gloves, Gardening Gloves, Gold Rings; overall patterns Gingham Gamble, Patched Up, Mud Season, Debt Collector Pinstripe, Golden Overalls, Wheat Camo.

**How they are made today**: VS accessories are built at runtime from simple primitive recipes (spheres, cylinders, cones) with shared materials, so four players cost only a handful of materials. Authored meshes (CC0 kits or owner-made Tripo models) can replace any recipe later. Accessories have no colliders and are never sellable or grabbable as goods.

Status in build: **Built** (10 hats, 4 face, 3 back items); extended catalogue **Planned**.

---

## 13.4 Unlocks

| Source | Grants |
|---|---|
| **Starter (free)** | Straw Hat, Trucker Cap, Cowboy Hat, all colours, all base skin tones, all name tags, None face/back, emotes Wave/Point/Dance/Faint |
| **Grandpa's Trunk** | Bought with Farm Memories (meta-progression): Common 60, Rare 120, Legendary 240. 6 items rotate each in-game week; duplicates never offered. |
| **Grandpa's Letters** (achievements) | Fixed rewards, never for sale (Bucket Helmet, Traffic Cone, Fedora, fantasy skins) |
| **Region clear** | 1 themed hat + 1 back + 1 emote per region |

Pacing: about 21 Farm Memories per good day, so a Common every ~3 days and a Legendary roughly every 2 weeks of play. Target: something new every 1–2 sessions. The full catalogue is a long-tail goal, not a requirement. Unlocks are account-wide and never lost when a run fails.

During the vertical slice, a build flag **unlocks everything** so the slice can show off the whole wardrobe.

Status in build: **Partial** — unlock sources are tagged on every item and the unlock-all flag is on; the Trunk, Letters and Farm Memories economy are planned.

---

## 13.5 How Others See You

### Name tags (built)
An overhead tag shows your name in your tag colour **plus a shape icon**, so colour is never the only cue. It faces the camera and is hidden for yourself.

| Tag | Colour | Shape |
|---|---|---|
| 1 | Denim Blue `#3B6FD1` | Circle |
| 2 | Pumpkin Orange `#F08A24` | Square |
| 3 | Clover Green `#3FAE5A` | Triangle |
| 4 | Lilac `#A77BE0` | Diamond |
| 5–8 | Sunflower, Teal, Pink, Snow | Star, Hexagon, Heart, Cross |

The tag colour is also your colour on the HUD, pings and the Blame Ledger. If two players pick the same tag, the later joiner gets the next free one for that session. Red is never a team colour (it is the loss colour and the bull's trigger). Planned: nameplate only beyond 20 m, a nickname line under the name (Chapter 12), and a "rookie" sprout.

### In first person
You see your arms, sleeves and gloves, coloured by your loadout (shirt, gloves/accent, skin). Your own hat, face and back items are hidden from your camera but still cast shadows and appear in replays. Goggles/Sunglasses add a subtle lens tint (off with "Reduce visual effects").

### Physics comedy
- **Hat pop-off** (planned): on a hard fall, `chance = (impact speed − 4 m/s) / 6`, so 50% at 7 m/s. The hat becomes a prop for 20 s or until picked up, then returns. A friend can wear your popped hat for 30 s ("Hat Theft"). Hats are never lost, even in the river.
- **Loadout change**: other players see a 0.3 s **poof of hay** when you confirm a new look (built).

Status in build: **Partial** — tags with shapes, colour-zoned first-person arms, hidden own-cosmetics and the hay poof are built; hat pop-off is planned.

---

## 13.6 Emotes

| Key | Emote | Behaviour | Status |
|---|---|---|---|
| 5 | **Wave** | 1.5 s; also a non-voice "hey" noise for no-mic players | Built |
| 6 | **Point** | Places a 3 s world ping up to 60 m away, seen by everyone | Built |
| 7 | **Dance** | Loops until you move | Built |
| 8 | **Faint** | Ragdoll for 2 s; **drops what you carry** (bruises apply, the Ledger still blames you); 5 s cooldown | Built |

Emotes are checked by the host (equipped, on cooldown, max 2 per second) before everyone plays them. Faint is sent reliably because it changes physics. Planned: emote wheel on hold **G** (all owned emotes), 10 unlockable emotes (Shrug, Facepalm, Chicken Dance, Salute, Cry, Flex, Slow Clap, Pig Squeal, Grandpa Shake-Fist, Sit), upper-body-only emotes while carrying.

**Gameplay-affecting cosmetics** (planned, flagged with a "chaos" icon): Red Cowboy Hat really triggers the bull's charge; Golden Overalls attract crows. Awaiting design confirmation.

Status in build: **Built** (4 emotes); wheel and unlockables **Planned**.

---

## 13.7 The Wardrobe

A physical wardrobe stands in the **Lobby Yard** and in the **barn** (the hub between days). Walk up and press **E** (or **C** from anywhere in the lobby).

```
+------------------------------------------------------------------+
|  WARDROBE                                                        |
|  [Body] [Colours] [Hat] [Face] [Back] [Tag] [Emotes]             |
|                                                                  |
|  +-----------------------+   Straw Hat            (equipped)     |
|  |                       |   Trucker Cap                         |
|  |    live 3D preview    |   Cowboy Hat                          |
|  |   (drag to orbit,     |   Chef Toque           [Trunk]        |
|  |    scroll to zoom)    |   Traffic Cone         [Letter]       |
|  |                       |   "Road works ahead. The road is     |
|  +-----------------------+    you."                              |
|                                                                  |
|  [Surprise me!]  [Revert]            [Cancel]   [Confirm]        |
+------------------------------------------------------------------+
```

- Preview: a separate copy of your farmer filmed by its own camera, 2.5 m away, orbit with mouse, zoom 1.2–3.5 m.
- Changes apply only on **Confirm**; Esc = Cancel. Surprise me! randomises; Revert restores.
- **The world keeps running.** Your body stands still and can be grabbed or knocked over; if you ragdoll, the wardrobe closes ("friends throw you out of the wardrobe").
- Locked during the active work day (barn wardrobe), so cosmetics can't be a hiding place.
- Planned: a full-length **mirror** in the Lobby Yard (only renders when someone is within 6 m).

### Safety and validation (built)
The host checks every loadout: unowned or out-of-range items are replaced by the slot default (no kick), duplicate tag colours are reassigned. Late joiners receive everyone's loadout. The last loadout is restored on launch and re-sent after every scene change.

Status in build: **Built** — wardrobe UI (UI Toolkit), live preview, interactable, validation, persistence; mirror **Planned**.

---

## 13.8 Accessibility

- Name tags pair colour with shape (built). Swatches have text names (built); high-contrast outline option (planned).
- Lens tints disabled by Reduce visual effects (planned).
- Emotes rebindable and on a single-hold wheel (planned).
- All four players in identical outfits is allowed; tags and shapes keep them distinguishable.

Status in build: **Partial**.
