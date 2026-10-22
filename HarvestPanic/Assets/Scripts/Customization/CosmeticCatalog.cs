using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace HarvestPanic.Customization
{
    /// <summary>How an item is earned (design/gdd/character-customization.md, "Unlock sources").</summary>
    public enum UnlockSource : byte { Starter, Trunk, Letter, RegionClear }

    /// <summary>Primitive used to build an accessory part.</summary>
    public enum PartShape : byte { Cube, Sphere, Cylinder, Capsule, Cone }

    /// <summary>Where a part takes its colour from. Add-only.</summary>
    public enum PartTint : byte { Fixed, Primary, Secondary, Accent, NameTag, Skin, Hair, SkinShade }

    /// <summary>Shape icon paired with each name-tag colour (colour-blind redundancy).</summary>
    public enum TagShape : byte { Circle, Square, Triangle, Diamond, Star, Hexagon, Heart, Cross }

    /// <summary>What playing an emote does beyond its animation.</summary>
    public enum EmoteKind : byte { Animation, Faint }

    /// <summary>Farmer attach point (Auto = the slot's default: Hat/Hair on the head, Face/Head on the face, Back on the back, Outfit on the chest).</summary>
    public enum AttachPoint : byte { Auto, Head, Face, Back, Chest, Hips }

    /// <summary>How an authored (prefab) item is coloured from the loadout.</summary>
    public enum TintMode : byte
    {
        /// <summary>Keep the prefab's own materials (hats are authored in real colours at VS).</summary>
        None,
        /// <summary>Materials use the HarvestPanic/Cosmetics/TintMask shader: RGBA mask = Primary/Secondary/Accent/Skin.</summary>
        MaskTexture,
        /// <summary>Each material slot follows <see cref="CosmeticItem.materialTints"/> (base colour multiplied).</summary>
        PerMaterial,
    }

    /// <summary>Procedural cloth pattern drawn over an outfit zone (character-style "Colour Palettes"). Add-only.</summary>
    public enum OutfitPattern : byte { Plain, Gingham, Pinstripe, Patches, Mud, Flannel, Denim, Polka, Stripes }

    /// <summary>A named colour swatch.</summary>
    [Serializable]
    public struct Swatch
    {
        /// <summary>Display name (shown under the swatch; accessibility).</summary>
        public string name;
        /// <summary>sRGB colour.</summary>
        public Color color;
        /// <summary>Locked until earned (fantasy skins); ignored when unlock-all is on.</summary>
        public bool fantasy;

        /// <summary>Creates a swatch from a hex string like "#3B6FD1".</summary>
        public static Swatch Hex(string name, string hex, bool fantasy = false)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return new Swatch { name = name, color = c, fantasy = fantasy };
        }
    }

    /// <summary>Name-tag colour + redundant shape.</summary>
    [Serializable]
    public struct NameTagStyle
    {
        /// <summary>Display name.</summary>
        public string name;
        /// <summary>Tag colour.</summary>
        public Color color;
        /// <summary>Shape icon.</summary>
        public TagShape shape;
    }

    /// <summary>Body variant: same capsule, different proportions (no gameplay difference).</summary>
    [Serializable]
    public struct BodyVariant
    {
        /// <summary>Display name.</summary>
        public string name;
        /// <summary>Flavour line.</summary>
        public string flavour;
        /// <summary>Scale applied to the model root.</summary>
        public Vector3 bodyScale;
        /// <summary>Extra uniform scale on the head bone (art: 1.15x chunky head).</summary>
        public float headScale;
        /// <summary>Optional card icon (otherwise rendered at runtime).</summary>
        public Texture2D icon;
    }

    /// <summary>One primitive of an accessory, in attach-point space (metres, +Y up, +Z character forward).</summary>
    [Serializable]
    public struct CosmeticPart
    {
        /// <summary>Primitive.</summary>
        public PartShape shape;
        /// <summary>Local position.</summary>
        public Vector3 position;
        /// <summary>Local euler rotation (degrees).</summary>
        public Vector3 euler;
        /// <summary>Size in metres (Cube/Sphere/Cylinder/Cone: full extents; Cone: x/z = base diameter).</summary>
        public Vector3 size;
        /// <summary>Colour when <see cref="tint"/> is Fixed.</summary>
        public Color color;
        /// <summary>Colour source.</summary>
        public PartTint tint;
        /// <summary>Cone only: top radius as a fraction of the base radius (0 = point).</summary>
        public float taper;
        /// <summary>Hair: hidden while a hat is worn (keeps the hat from floating on a big hairdo).</summary>
        public bool hideUnderHat;
        /// <summary>Overrides the item's attach point for this part (outfits use Chest and Hips).</summary>
        public AttachPoint attach;
    }

    /// <summary>Outfit-only data: patterns per colour zone and what colour the sleeves show (also on first-person arms).</summary>
    [Serializable]
    public struct OutfitStyle
    {
        /// <summary>Pattern on the overalls (Primary) zone.</summary>
        public OutfitPattern overallsPattern;
        /// <summary>Pattern on the shirt (Secondary) zone.</summary>
        public OutfitPattern shirtPattern;
        /// <summary>Colour the shirt zone takes (Secondary normally; a raincoat paints it Primary).</summary>
        public PartTint shirtTint;
        /// <summary>Colour of the sleeves on the first-person arms (Secondary normally; Skin = bare arms).</summary>
        public PartTint sleeveTint;
    }

    /// <summary>A catalogue item for any <see cref="CosmeticSlot"/>.</summary>
    [Serializable]
    public class CosmeticItem
    {
        /// <summary>Stable id within its slot (never reuse; 0 is reserved for None / the base look).</summary>
        public ushort id;
        /// <summary>Stable string key (e.g. "hat.straw").</summary>
        public string key;
        /// <summary>Display name.</summary>
        public string displayName;
        /// <summary>Flavour text shown in the wardrobe.</summary>
        [TextArea] public string flavour;
        /// <summary>Unlock source.</summary>
        public UnlockSource unlock;
        /// <summary>Shown on the locked card, e.g. "Grandpa's Letter: fall in the river 10 times".</summary>
        public string unlockHint;
        /// <summary>Hats that cover the wearer's view (Pumpkin Head, Bucket Helmet) draw a vignette instead of the mesh in first person.</summary>
        public bool blocksFirstPersonView;
        /// <summary>Full-cover hats hide hairstyles completely.</summary>
        public bool hidesHair;
        /// <summary>Relative chance in Randomize (0 = never picked at random).</summary>
        public float randomWeight = 1f;
        /// <summary>Optional card icon (otherwise rendered at runtime from the item on your farmer).</summary>
        public Texture2D icon;
        /// <summary>Attach point (Auto = slot default).</summary>
        [Header("Placement")]
        public AttachPoint attach;
        /// <summary>Offset from the attach point (metres).</summary>
        public Vector3 offset;
        /// <summary>Extra rotation (degrees).</summary>
        public Vector3 rotation;
        /// <summary>Uniform scale (authored meshes: 1 unit = 1 m, sized for a 32 cm head).</summary>
        public float scale = 1f;
        /// <summary>Optional authored prefab; when null the item is built from <see cref="parts"/>.</summary>
        [Header("Authored mesh (Tripo / CC0)")]
        public GameObject prefab;
        /// <summary>How the prefab is coloured.</summary>
        public TintMode tintMode;
        /// <summary>PerMaterial: colour source per material slot (missing entries = Fixed).</summary>
        public List<PartTint> materialTints = new List<PartTint>();
        /// <summary>MaskTexture: what the mask's red channel follows (Primary; Hair for hair and beards).</summary>
        public PartTint maskRed = PartTint.Primary;
        /// <summary>Primitive recipe.</summary>
        [Header("Procedural placeholder")]
        public List<CosmeticPart> parts = new List<CosmeticPart>();
        /// <summary>Head slot: non-uniform head-bone scale (1,1,1 = base).</summary>
        [Header("Slot specific")]
        public Vector3 headShape = Vector3.one;
        /// <summary>Hair slot: paint the base mesh hair in skin colour (bald styles).</summary>
        public bool hideBaseHair;
        /// <summary>Outfit slot: patterns and sleeve colour.</summary>
        public OutfitStyle outfit = new OutfitStyle { shirtTint = PartTint.Secondary, sleeveTint = PartTint.Secondary };
    }

    /// <summary>An emote (design/gdd/character-customization.md, "Emotes").</summary>
    [Serializable]
    public class EmoteDefinition
    {
        /// <summary>Stable id (0 reserved for none).</summary>
        public ushort id;
        /// <summary>Id passed to <c>IPlayerHooks.PlayEmote</c> ("wave", "point", "dance", "faint").</summary>
        public string key;
        /// <summary>Display name.</summary>
        public string displayName;
        /// <summary>Animator state on FarmerBody.controller.</summary>
        public string animState;
        /// <summary>Kind.</summary>
        public EmoteKind kind;
        /// <summary>Seconds the emote lasts (Faint: ragdoll time).</summary>
        public float duration = 1.5f;
        /// <summary>Seconds before the same emote can be used again.</summary>
        public float cooldown = 1.5f;
        /// <summary>Loops until the farmer moves (Dance).</summary>
        public bool loopsUntilMove;
        /// <summary>Server noise loudness 0..1 (0 = silent). Wave is the non-voice shout.</summary>
        public float noiseLoudness;
        /// <summary>Places a world ping at the aim point (Point).</summary>
        public bool placesPing;
        /// <summary>Free from the start.</summary>
        public UnlockSource unlock;
        /// <summary>Flavour line in the Emotes tab.</summary>
        public string flavour;
        /// <summary>Shown when locked.</summary>
        public string unlockHint;
    }

    /// <summary>
    /// Data-driven cosmetics catalogue and customization tuning (GDD "Tuning Knobs": DA_CustomizationTuning + DT_Cosmetics).
    /// Asset: Assets/Art/Cosmetics/CosmeticCatalog.asset, referenced by FarmerBody.prefab.
    /// Items come from two places: the built-in procedural placeholders (lists below, filled by <see cref="ResetToDefaults"/>)
    /// and <see cref="entries"/> (one <see cref="CosmeticEntry"/> asset per authored item, e.g. an imported Tripo FBX).
    /// An entry with the same slot + id as a built-in item replaces it; new ids add items. See docs/art/adding-cosmetics.md.
    /// </summary>
    [CreateAssetMenu(menuName = "HarvestPanic/Cosmetic Catalog", fileName = "CosmeticCatalog")]
    public partial class CosmeticCatalog : ScriptableObject
    {
        [Header("Gate")]
        [Tooltip("bUnlockAllCosmetics: true in VS builds so everything can be shown off.")]
        public bool unlockAllCosmetics = true;

        [Tooltip("Defaults version last merged by EnsureDefaults (editor builder).")]
        public int dataVersion;

        [Header("Colours")]
        public List<Swatch> palette = new List<Swatch>();
        public List<Swatch> skinTones = new List<Swatch>();
        public List<NameTagStyle> nameTags = new List<NameTagStyle>();

        [Header("Built-in items (procedural placeholders)")]
        public List<BodyVariant> bodies = new List<BodyVariant>();
        public List<CosmeticItem> hats = new List<CosmeticItem>();
        public List<CosmeticItem> faces = new List<CosmeticItem>();
        public List<CosmeticItem> backs = new List<CosmeticItem>();
        public List<CosmeticItem> heads = new List<CosmeticItem>();
        public List<CosmeticItem> hairs = new List<CosmeticItem>();
        public List<CosmeticItem> outfits = new List<CosmeticItem>();
        public List<EmoteDefinition> emotes = new List<EmoteDefinition>();

        [Header("Authored items (one asset per item; override built-ins by id)")]
        public List<CosmeticEntry> entries = new List<CosmeticEntry>();

        [Header("Tuning")]
        [Tooltip("EmoteRateLimit (per second, server).")] [Range(1, 5)] public float emoteRateLimit = 2f;
        [Tooltip("Ping lifetime for Point (s).")] public float pingLifetime = 3f;
        [Tooltip("Ping max distance for Point (m).")] public float pingMaxDistance = 60f;
        [Tooltip("WardrobePreviewDistance (m).")] [Range(1.2f, 3.5f)] public float previewDistance = 2.5f;
        public float previewMinDistance = 1.2f;
        public float previewMaxDistance = 3.5f;
        [Tooltip("Hay poof shown to others when a loadout changes (s).")] public float poofDuration = 0.3f;
        [Tooltip("Hat-or-not chance weights for Randomize (0..1 chance of a hat).")] [Range(0, 1)] public float surpriseHatChance = 0.95f;

        [Header("References")]
        [Tooltip("Lit material cloned for body zones and accessories.")] public Material baseMaterial;
        [Tooltip("Unlit material for name tags, pings and the preview backdrop.")] public Material unlitMaterial;
        [Tooltip("FarmerBody prefab (wardrobe preview).")] public GameObject farmerPrefab;
        [Tooltip("Panel settings for the wardrobe UI.")] public PanelSettings wardrobePanel;
        [Tooltip("Character creator stylesheet (Art/Cosmetics/UI/CharacterCreator.uss).")] public StyleSheet creatorStyle;

        /// <summary>Unlocked keys of the local player (null until a profile/unlock service exists: only starter items count).</summary>
        public static ICollection<string> LocalOwnedKeys;

        // ---- Item lookup ------------------------------------------------------------------------------------------

        readonly Dictionary<CosmeticSlot, List<CosmeticItem>> _merged = new Dictionary<CosmeticSlot, List<CosmeticItem>>();
        readonly Dictionary<CosmeticSlot, (List<CosmeticItem> list, int count, int entries)> _mergedKey = new Dictionary<CosmeticSlot, (List<CosmeticItem>, int, int)>();

        /// <summary>Built-in list for <paramref name="slot"/> (editable; procedural placeholders).</summary>
        public List<CosmeticItem> BuiltIn(CosmeticSlot slot)
        {
            switch (slot)
            {
                case CosmeticSlot.Hat: return hats;
                case CosmeticSlot.Face: return faces;
                case CosmeticSlot.Back: return backs;
                case CosmeticSlot.Head: return heads;
                case CosmeticSlot.Hair: return hairs;
                default: return outfits;
            }
        }

        /// <summary>Every item of <paramref name="slot"/>: built-ins (in order, replaced by entries with the same id) then new entries by id.</summary>
        public IReadOnlyList<CosmeticItem> Items(CosmeticSlot slot)
        {
            var builtIn = BuiltIn(slot);
            int entryCount = entries != null ? entries.Count : 0;
            if (_merged.TryGetValue(slot, out var cached) && _mergedKey.TryGetValue(slot, out var key)
                && ReferenceEquals(key.list, builtIn) && key.count == builtIn.Count && key.entries == entryCount)
                return cached;

            var list = new List<CosmeticItem>(builtIn);
            var extra = new List<CosmeticItem>();
            if (entries != null)
                foreach (var e in entries)
                {
                    if (e == null || e.slot != slot || e.item == null || e.item.id == 0) continue;
                    int at = list.FindIndex(i => i.id == e.item.id);
                    if (at >= 0) list[at] = e.item; else extra.Add(e.item);
                }
            extra.Sort((a, b) => a.id.CompareTo(b.id));
            list.AddRange(extra);
            _merged[slot] = list;
            _mergedKey[slot] = (builtIn, builtIn.Count, entryCount);
            return list;
        }

        /// <summary>Forces the merged item lists to rebuild (after editing entries in place).</summary>
        public void MarkItemsDirty() { _merged.Clear(); _mergedKey.Clear(); }

        void OnValidate() => MarkItemsDirty();
        void OnEnable() => MarkItemsDirty();

        /// <summary>Item with <paramref name="id"/> in <paramref name="slot"/>, or null (None / base look / unknown).</summary>
        public CosmeticItem FindItem(CosmeticSlot slot, ushort id)
        {
            if (id == 0) return null;
            var items = Items(slot);
            for (int i = 0; i < items.Count; i++) if (items[i].id == id) return items[i];
            return null;
        }

        /// <summary>Item by key in any slot (null when unknown).</summary>
        public CosmeticItem FindItem(string key, out CosmeticSlot slot)
        {
            foreach (CosmeticSlot s in Enum.GetValues(typeof(CosmeticSlot)))
                foreach (var it in Items(s))
                    if (string.Equals(it.key, key, StringComparison.OrdinalIgnoreCase)) { slot = s; return it; }
            slot = CosmeticSlot.Hat;
            return null;
        }

        /// <summary>Emote with <paramref name="id"/>, or null.</summary>
        public EmoteDefinition FindEmote(ushort id)
        {
            if (id == 0) return null;
            foreach (var e in emotes) if (e.id == id) return e;
            return null;
        }

        /// <summary>Emote by key ("wave"), or null.</summary>
        public EmoteDefinition FindEmote(string key)
        {
            foreach (var e in emotes) if (string.Equals(e.key, key, StringComparison.OrdinalIgnoreCase)) return e;
            return null;
        }

        // ---- Ownership ----------------------------------------------------------------------------------------------

        /// <summary>True when the item may be worn by someone whose unlocks are <paramref name="ownedKeys"/> (may be null).</summary>
        public bool IsOwned(UnlockSource unlock, string key, ICollection<string> ownedKeys = null) =>
            unlockAllCosmetics || unlock == UnlockSource.Starter || (ownedKeys != null && key != null && ownedKeys.Contains(key));

        /// <summary>Local player owns <paramref name="item"/> (null = None / base look, always owned).</summary>
        public bool IsOwnedLocally(CosmeticItem item) => item == null || IsOwned(item.unlock, item.key, LocalOwnedKeys);

        /// <summary>Local player owns <paramref name="emote"/>.</summary>
        public bool IsOwnedLocally(EmoteDefinition emote) => emote == null || IsOwned(emote.unlock, "emote." + emote.key, LocalOwnedKeys);

        /// <summary>Local player owns skin tone <paramref name="index"/>.</summary>
        public bool IsSkinOwnedLocally(int index) =>
            index >= 0 && index < skinTones.Count && (!skinTones[index].fantasy || IsOwned(UnlockSource.Letter, "skin." + index, LocalOwnedKeys));

        /// <summary>Player-facing unlock hint for a locked item.</summary>
        public static string UnlockHint(UnlockSource unlock, string hint)
        {
            if (!string.IsNullOrEmpty(hint)) return hint;
            switch (unlock)
            {
                case UnlockSource.Trunk: return "Grandpa's Trunk (Farm Memories)";
                case UnlockSource.Letter: return "A letter from Grandpa (achievement)";
                case UnlockSource.RegionClear: return "Clear a region";
                default: return "";
            }
        }

        // ---- Colours ------------------------------------------------------------------------------------------------

        /// <summary>Palette colour, clamped.</summary>
        public Color PaletteColor(int index) => palette.Count == 0 ? Color.gray : palette[Mathf.Clamp(index, 0, palette.Count - 1)].color;

        /// <summary>Palette swatch name, clamped.</summary>
        public string PaletteName(int index) => palette.Count == 0 ? "" : palette[Mathf.Clamp(index, 0, palette.Count - 1)].name;

        /// <summary>Skin colour, clamped.</summary>
        public Color SkinColor(int index) => skinTones.Count == 0 ? new Color(0.85f, 0.64f, 0.49f) : skinTones[Mathf.Clamp(index, 0, skinTones.Count - 1)].color;

        /// <summary>Name tag style, clamped.</summary>
        public NameTagStyle NameTag(int index) => nameTags.Count == 0 ? new NameTagStyle { name = "White", color = Color.white } : nameTags[Mathf.Clamp(index, 0, nameTags.Count - 1)];

        /// <summary>Body variant, clamped.</summary>
        public BodyVariant Body(int index) => bodies.Count == 0 ? new BodyVariant { name = "Default", bodyScale = Vector3.one, headScale = 1f } : bodies[Mathf.Clamp(index, 0, bodies.Count - 1)];

        /// <summary>Default attach point of a slot.</summary>
        public static AttachPoint DefaultAttach(CosmeticSlot slot)
        {
            switch (slot)
            {
                case CosmeticSlot.Hat: case CosmeticSlot.Hair: return AttachPoint.Head;
                case CosmeticSlot.Face: case CosmeticSlot.Head: return AttachPoint.Face;
                case CosmeticSlot.Back: return AttachPoint.Back;
                default: return AttachPoint.Chest;
            }
        }
    }
}
