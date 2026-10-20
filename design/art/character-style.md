# HARVEST PANIC — Character Style and Cosmetics Catalog

> **Status**: Draft v1 (Art Director, 2026-10-11). Follows `design/art/art-bible.md`. Unlock economy and pricing belong to meta-progression; this file specs look, slots and build source only.

## 1. The Farmer

**Fantasy:** a cheerful but slightly unqualified farmhand who inherited a mess. Think "first day on the job, borrowed overalls."

- **Base body**: Unity Humanoid mannequin rig with a stylized override: chunky proportions via bone scale (head 1.15x, hands 1.2x for first-person reads, feet 1.15x), a toon-flat material `MI_Farmer_Base` (no mannequin plastic look).
- **Uniform**: overalls (team colour, the largest area visible to others) over a neutral shirt, rubber boots, work gloves. Overalls are a **skeletal mesh merge** layer, or for the slice, a material zone on the mannequin plus a bib mesh.
- **Face**: no realistic face. A simple **decal face** (painted eyes + mouth) on a rounded head mask, swappable per cosmetic. This keeps it cheap and makes emote and ragdoll expressions funny (face swaps to "X_X" on KO, "O_O" on scare).
- **First-person view**: arms + gloves + sleeve cuffs in team colour. The player sees their own hands constantly, so **gloves and sleeves are a cosmetic slot**; hats show in the shadow and in the Camcorder replay.
- **Silhouette rule**: every hat must change the head outline by **≥ 30% of head height** or **≥ 40% width**. At 40 m, teammates should be identifiable by hat + team colour alone.
- **Team colours** (overalls, defaults; colourblind-safe pairing with icon):
  | Slot | Colour | Hex | Icon |
  |---|---|---|---|
  | P1 | Denim Blue | `#3B6FD1` | Circle |
  | P2 | Pumpkin Orange | `#F08A24` | Square |
  | P3 | Clover Green | `#3FAE5A` | Triangle |
  | P4 | Lilac | `#A77BE0` | Diamond |
  Red is **not** a team colour (bull trigger plus reserved loss colour).

## 2. Cosmetic Slots
| Slot | Attach | Notes |
|---|---|---|
| Hat | `head` socket | Primary silhouette slot. Must obey the silhouette rule. |
| Face | decal on head mask | Eyes/mouth/moustache/glasses as decal + optional small mesh |
| Back | `spine_03` socket | Must not clip the carry pose; ≤ 35 cm protrusion |
| Gloves | material zone | Visible in first person |
| Overalls Pattern | material param | Pattern over the team colour (team colour stays dominant ≥ 60%) |
| Skin Tone | material param | Free from the start, never locked |

**Build sources**: **[A]** achievable now with the mannequin + Emberlight props; **[K]** kitbash from free Fab/Quixel props with a re-material; **[N]** needs new art.

## 3. Skin Tones (all free, always available)
Eight painted tones on a warm-to-cool range, plus three fantasy tones unlocked via play (never paywalled):
`#F6D7C3` Buttermilk · `#EBC0A0` Peach Cobbler · `#D9A27C` Honeycomb · `#C08560` Maple · `#9E6A47` Pecan · `#7A4E33` Cocoa · `#5A3725` Coffee Bean · `#3E261A` Black Walnut.
Fantasy (unlock): `#9FD39A` "Pickled" (Almanac 25%), `#B3A3E6` "Moonstruck" (survive 10 nights), `#E8E3D3` "Spooked Pale" (looked at the Scarecrow 50 times).

## 4. Colour Palettes (overall patterns)
| Name | Look | Source | Flavour |
|---|---|---|---|
| Plain Jane | Solid team colour | [A] | "Factory default. Smells like the store." |
| Gingham Gamble | Check pattern | [A] | "Picnic-ready. Not pig-ready." |
| Patched Up | Random square patches | [A] | "Every patch is a story. Most are about the goat." |
| Mud Season | Team colour + baked mud splash gradient | [A] | "Pre-muddied for your convenience." |
| Debt Collector Pinstripe | Plum pinstripe tint | [A] | "Dress for the job you owe." |
| Golden Overalls | Gold metallic flecks | [A] | "Visible from space. And to crows." (Crows target the wearer, comedic risk) |
| Camo (Wheat) | Wheat-coloured camo | [A] | "Hide in the field. Your friends will lose you too." (Team colour kept on cuffs + nameplate for readability) |

## 5. Hats (primary silhouette slot)
| Hat | Silhouette | Source | Flavour |
|---|---|---|---|
| Grandpa's Straw Hat | Wide flat brim | **[A]** `SM_Strawhat` | "Smells like Grandpa. Smells like regret." (default) |
| Bucket Hat | Bucket, upside down | **[A]** kitbash from `SM_Bushel`/`SM_SquarePot` | "Holds water. Doesn't hold dignity." |
| Lantern Cap | Lantern on a strap | **[A]** `SM_OilLantern` | "Night shift approved. Moths approve too." (cosmetic only, the gameplay light comes from the base kit) |
| Seedling Sprout | A sprout poking up | **[A]** `SM_Seedling` | "Water daily. Talk to it." |
| Mushroom Cap | Big round cap | **[A]** `SM_Mushroom` scaled | "Don't lick it." |
| Watermelon Helmet | Half watermelon | **[A]** `SM_Watermelon` hollowed (material) | "Safety first. Snacks second." |
| Pumpkin Head | Full pumpkin | **[K]** Quixel/Fab pumpkin re-materialed | "The Scarecrow thinks you're cute." (Halloween) |
| Traffic Cone | Tall cone | **[K]** | "Road works ahead. The road is you." |
| Too-Tall Top Hat | Very tall stovepipe | **[K]** simple primitive | "Just like the Landlord's. He hates that." (unlock: pay off Region 1) |
| Red Cowboy Hat | Wide-brim cowboy | **[K]** | "The bull has opinions." (actually triggers the bull's red-hat charge, a deliberate streamer bait item) |
| Chicken Hat | A live-looking chicken nest | **[N]** needs chicken mesh | "She lays an egg every 10 minutes. It's worth $0." |
| Viking Helmet | Horns | **[N]** | "Raids barns. Only his own." (Fjord) |
| Rubber Duck Crown | Duck on a crown | **[N]** | "Sovereign of the pond." |
| Beekeeper Veil | Wide hat + mesh veil | **[N]** | "The bees remain unimpressed." |
| Cone of Shame | Vet cone on the neck | **[N]** | "Awarded automatically for Most Valuable Disaster x3." |

## 6. Face Items
| Item | Source | Flavour |
|---|---|---|
| Default Smile / Worried / Smug / Cross-Eyed decals | [A] | "Choose your expression for the inevitable." |
| Grandpa Moustache | [A] decal | "Inherited. Like the debt." |
| Hay Beard | [K] hay-card tufts from `SM_GrassCard` reskinned | "Itchy. Majestic." |
| Safety Goggles | [K] | "Mandatory around llamas." |
| Monocle | [K] | "For inspecting Landlord fine print." |
| Sunflower Glasses | [N] | "Pollinator-ready." |
| Cucumber Eye Slices | [A] `SM_Cucumber` sliced | "Spa day. On a farm. Mid-stampede." |

## 7. Back Items
| Item | Source | Flavour |
|---|---|---|
| Pitchfork Holster | [A] `SM_Pitchfork` | "For decoration. Allegedly." |
| Rope Coil | [A] `SM_RopeCoil` | "Lasso not included. Hope is." |
| Harvest Basket Backpack | [A] `SM_Harvest_Basket` | "Holds nothing. The physics team was very clear." |
| Hay Bale Backpack | [A] `SM_HayBalesRectangular` scaled 0.3 | "Emergency pillow." |
| Honey Jar Pack | [A] `SM_Honey` | "Sticky situations only." |
| Watering Can Jetpack | [A] `SM_WateringCan` x2 | "Does not fly. We tested." |
| Mini Windmill | [A] `SM_Windmill_Rotate` scaled, spins when running | "Generates zero power and infinite attention." |
| Scarecrow Buddy | [A] `SM_Scarecrow` scaled 0.25 | "He only moves when you aren't looking. That's normal, right?" |
| Grandpa's Camcorder | [N] | "Always recording. Always judging." |
| Crow Passenger | [N] needs crow mesh | "Steals nothing. Yet." |
| Inflatable Duck Floatie | [N] | "River-ready. Bridge-shy." |

## 8. Gloves
| Item | Source | Flavour |
|---|---|---|
| Work Gloves (default) | [A] | "Smell like pig. Forever." |
| Oven Mitts | [K] | "Grip rating: optimistic." |
| Rubber Dish Gloves (yellow) | [A] colour swap | "Squeaky. Chickens hate it." |
| Gardening Gloves (floral) | [A] pattern | "Grandma's. Do not tell Grandma." |
| Gold Rings | [A] material | "Paid for with the golden egg you 'lost'." |

## 9. Production Notes
- **Vertical slice**: ship Straw Hat + Plain Jane + 4 team colours + 8 skin tones only. Everything else is post-slice.
- **EA launch target**: ~40 cosmetics, ≥ 60% of them [A] or [K] (Emberlight props are cheap wins).
- **Clipping**: back items are tested against the 2-hand, 4-hand and pumpkin carry poses. Hats are tested against the ragdoll. All cosmetics hide in first person except gloves and sleeves (owner-no-see).
- **Budgets**: hat/back ≤ 1.5k tris, one shared 2048 cosmetics atlas, attached via `SkeletalMeshComponent` or static mesh to socket, no physics except optional AnimDynamics on dangly bits (feathers, veil).
- **Gameplay-affecting cosmetics** (Red Cowboy Hat, Golden Overalls) must be flagged in the UI with a small "chaos" icon so nobody equips them by accident. The game designer confirms these hooks (**[DECIDE]**).
