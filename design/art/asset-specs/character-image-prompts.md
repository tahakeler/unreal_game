# HARVEST PANIC — Character Image Prompts (for image generators → Tripo image-to-3D)

Use these in any image generator (ChatGPT / Gemini / Midjourney / Tripo text-to-image). Then upload the
image to **Tripo image-to-3D**. Companion to `tripo-batch-02-character.md` (Tripo settings, tris, file names).

## How to get images Tripo converts well
- One character or one item per image, **full body in frame, nothing cropped**, plain white background.
- **Front view, A-pose** (arms ~45° down, legs slightly apart) for bodies — Tripo rigs these best.
- Soft even studio light, no strong shadows, no ground, no text.
- Square image, 1024×1024 or larger.
- If the generator adds a floor shadow or background props, regenerate — Tripo will model them.

**Style line (start every prompt with this):**
> Stylized 3D cartoon character render, soft toy-like vinyl figure look, smooth rounded chunky shapes, big head and big expressive eyes, warm friendly colours, clean flat colour areas with soft ambient occlusion, Pixar-meets-Fall-Guys charm, plain pure white background, soft even studio lighting, no shadow on the ground, full body visible, front view, A-pose with arms angled down 45 degrees, legs slightly apart

**Avoid line (add to every prompt, or the negative field):**
> no text, no logo, no background scenery, no floor, no props in hands, not realistic, no hair strands, no patterns on clothes, not cropped

---

## Part A — Full characters (hero concepts)

Use these to lock the look, or as a **single-mesh** farmer in Tripo if you want results fast.
For the in-game **character creator**, also make the separated parts in Part B.

### A1. "Sturdy" farmer — the default hero
> [Style line], a stocky cheerful farmer with a wide barrel body and short thick legs, big round head with huge round eyes and a goofy wide smile, small button nose, tan skin, wearing denim blue overalls with a bib and two straps over an off-white short-sleeve shirt, chunky dark rubber boots, thick tan work gloves with four fingers, a wide floppy honey-yellow straw hat with a red band. [Avoid line]

### A2. "Lanky" farmer
> [Style line], a tall skinny goofy farmer with long thin arms and legs and a slight slouch, long egg-shaped head with big droopy worried eyes and a crooked nervous smile, light skin with freckles, wearing faded green overalls over a cream shirt, tall rubber boots, oversized work gloves with four fingers, a red trucker cap. [Avoid line]

### A3. Farmer in raincoat (storm outfit)
> [Style line], a stocky farmer in a bright yellow rubber raincoat with big buttons and a hood down, rubber boots, four-finger gloves, round head with excited big eyes and a big grin, dark brown skin, a black fisherman bucket hat. [Avoid line]

### A4. Flannel farmer
> [Style line], a medium-build farmer in a red flannel shirt with solid colour (no plaid pattern) and blue jeans, brown work boots, four-finger gloves, round head with a huge bushy moustache and big friendly eyes, medium skin, a brown cowboy hat. [Avoid line]

---

## Part B — Character creator parts (what the game actually swaps)

Make each as its own image. Keep the clothing in **plain flat neutral colours** exactly as written —
the game recolours overalls/shirt/boots/skin to whatever the player picks.

### Bodies (headless — the game attaches heads separately)
**B1 Sturdy body**
> [Style line], a headless cartoon farmer body only, nothing above the collar, flat clean neck opening, stocky barrel torso, short thick legs, chunky hands with four fingers in thick work gloves, plain flat mid-grey overalls with bib and two straps, plain flat off-white short-sleeve shirt, chunky flat dark-grey rubber boots, small round brass buttons. [Avoid line], no head, no face, no hat

**B2 Lanky body**
> [Style line], a headless cartoon farmer body only, nothing above the collar, flat clean neck opening, tall thin body with long skinny arms and legs, oversized four-finger work gloves, plain flat mid-grey overalls with bib and straps, plain flat off-white short-sleeve shirt, tall flat dark-grey rubber boots. [Avoid line], no head, no face, no hat

### Heads (bald, front view, with a short neck stub)
Start each with: *Stylized 3D cartoon render, soft toy-like vinyl look, smooth rounded shapes, plain pure white background, soft even lighting, front view, a bald cartoon farmer head only with a short neck stub, flat neutral beige skin,*
- **B3 Round:** big round ball-shaped head, huge round eyes, small button nose, wide goofy smile, small round ears
- **B4 Long:** tall egg-shaped head with long chin, big droopy worried eyes, crooked mouth
- **B5 Freckles:** squarish rounded head, chubby cheeks with a few big freckle dots, huge excited eyes, gap-tooth grin
- **B6 Big nose:** round head with a huge potato nose, half-lidded smug eyes, thick simple eyebrows, small smug mouth

End each with: *no hair, no hat, no body, no shoulders, no text*

### Hair (item only, no head)
Start: *Stylized 3D cartoon render, soft chunky clay-like shapes, plain white background, front three-quarter view, a cartoon hairpiece only (no head),*
- **B7 Mop:** messy rounded mop of hair as big soft chunky clumps, warm brown
- **B8 Ponytail:** hair with a high bouncy ponytail as big smooth shapes, ginger
- **B9 Beard:** big bushy full beard and moustache as one chunky shape, grey

### Hats (item only, no head) — real colours
Start: *Stylized 3D cartoon render, soft toy-like chunky shapes, plain white background, three-quarter view, a single cartoon hat only, no head,*
- **Straw Hat:** very wide floppy round brim, honey-yellow straw in big soft bands, red cloth band
- **Trucker Cap:** red front panel, white mesh back as a flat colour, curved brim
- **Bucket Helmet:** a dented galvanised-grey metal bucket worn upside down, handle hanging
- **Pumpkin Head:** a carved orange pumpkin helmet with a happy jack-o-lantern face and green stem
- **Cowboy Hat:** brown cowboy hat with a rolled brim and a darker band
- **Chef Toque:** tall puffy white chef hat
- **Flower Crown:** a ring of big chunky daisies and pink flowers with green leaves
- **Traffic Cone:** a bright orange traffic cone with one white stripe, worn as a hat
- **Viking Horns:** a round grey metal helmet with two big curved cream horns
- **Grandpa's Fedora:** a worn dusty-green fedora with a feather in the band

End each with: *no head, no mannequin, no text*

### Face items (item only)
Start: *Stylized 3D cartoon render, chunky soft shapes, plain white background, front view, a single item only, no face,*
- **Moustache:** a huge curly handlebar moustache, dark brown
- **Goggles:** big round aviator goggles with amber lenses and a brown strap
- **Sunglasses:** chunky oversized black sunglasses

### Back items (item only)
Start: *Stylized 3D cartoon render, chunky soft shapes, plain white background, three-quarter back view, a single item only, no person,*
- **Backpack Basket:** a woven wicker backpack basket with two straps, honey-brown
- **Cape:** a short heroic red cape with a gold clasp, gently flowing

---

## After generating
1. Upload each image to Tripo **image-to-3D**. Bodies: enable **auto-rig → humanoid**.
2. Smart LowPoly to the tri target in `tripo-batch-02-character.md`, texture HD, PBR off, export **FBX**.
3. Save to `~/Desktop/HarvestPanic Tripo Models/<Bodies|Heads|Hair|Hats|Face|Back|Outfits>/` with the
   file names from `tripo-batch-02-character.md` (e.g. `Bodies/SK_Farmer_Sturdy.fbx`).
4. Tell the lead — the character creator imports them as menu items.
