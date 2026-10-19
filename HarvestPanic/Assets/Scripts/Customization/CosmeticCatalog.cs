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

    /// <summary>Where a part takes its colour from.</summary>
    public enum PartTint : byte { Fixed, Primary, Secondary, Accent, NameTag }

    /// <summary>Shape icon paired with each name-tag colour (colour-blind redundancy).</summary>
    public enum TagShape : byte { Circle, Square, Triangle, Diamond, Star, Hexagon, Heart, Cross }

    /// <summary>What playing an emote does beyond its animation.</summary>
    public enum EmoteKind : byte { Animation, Faint }

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
    }

    /// <summary>A hat, face or back item.</summary>
    [Serializable]
    public class CosmeticItem
    {
        /// <summary>Stable id within its slot (never reuse; 0 is reserved for None).</summary>
        public ushort id;
        /// <summary>Stable string key (e.g. "hat.straw").</summary>
        public string key;
        /// <summary>Display name.</summary>
        public string displayName;
        /// <summary>Flavour text shown in the wardrobe.</summary>
        [TextArea] public string flavour;
        /// <summary>Unlock source.</summary>
        public UnlockSource unlock;
        /// <summary>Hats that cover the wearer's view (Pumpkin Head, Bucket Helmet) draw a vignette instead of the mesh in first person.</summary>
        public bool blocksFirstPersonView;
        /// <summary>Optional authored prefab; when null the item is built from <see cref="parts"/>.</summary>
        public GameObject prefab;
        /// <summary>Primitive recipe.</summary>
        public List<CosmeticPart> parts = new List<CosmeticPart>();
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
    }

    /// <summary>
    /// Data-driven cosmetics catalogue and customization tuning (GDD "Tuning Knobs": DA_CustomizationTuning + DT_Cosmetics).
    /// Asset: Assets/Art/Cosmetics/CosmeticCatalog.asset, referenced by FarmerBody.prefab.
    /// </summary>
    [CreateAssetMenu(menuName = "HarvestPanic/Cosmetic Catalog", fileName = "CosmeticCatalog")]
    public partial class CosmeticCatalog : ScriptableObject
    {
        [Header("Gate")]
        [Tooltip("bUnlockAllCosmetics: true in VS builds so everything can be shown off.")]
        public bool unlockAllCosmetics = true;

        [Header("Colours")]
        public List<Swatch> palette = new List<Swatch>();
        public List<Swatch> skinTones = new List<Swatch>();
        public List<NameTagStyle> nameTags = new List<NameTagStyle>();

        [Header("Items")]
        public List<BodyVariant> bodies = new List<BodyVariant>();
        public List<CosmeticItem> hats = new List<CosmeticItem>();
        public List<CosmeticItem> faces = new List<CosmeticItem>();
        public List<CosmeticItem> backs = new List<CosmeticItem>();
        public List<EmoteDefinition> emotes = new List<EmoteDefinition>();

        [Header("Tuning")]
        [Tooltip("EmoteRateLimit (per second, server).")] [Range(1, 5)] public float emoteRateLimit = 2f;
        [Tooltip("Ping lifetime for Point (s).")] public float pingLifetime = 3f;
        [Tooltip("Ping max distance for Point (m).")] public float pingMaxDistance = 60f;
        [Tooltip("WardrobePreviewDistance (m).")] [Range(1.2f, 3.5f)] public float previewDistance = 2.5f;
        public float previewMinDistance = 1.2f;
        public float previewMaxDistance = 3.5f;
        [Tooltip("Hay poof shown to others when a loadout changes (s).")] public float poofDuration = 0.3f;
        [Tooltip("Hat-or-not chance weights for 'Surprise me!' (0..1 chance of a hat).")] [Range(0, 1)] public float surpriseHatChance = 0.95f;

        [Header("References")]
        [Tooltip("Lit material cloned for body zones and accessories.")] public Material baseMaterial;
        [Tooltip("Unlit material for name tags and pings.")] public Material unlitMaterial;
        [Tooltip("FarmerBody prefab (wardrobe preview).")] public GameObject farmerPrefab;
        [Tooltip("Panel settings for the wardrobe UI.")] public PanelSettings wardrobePanel;

        /// <summary>Item list for <paramref name="slot"/>.</summary>
        public List<CosmeticItem> Items(CosmeticSlot slot) => slot == CosmeticSlot.Hat ? hats : slot == CosmeticSlot.Face ? faces : backs;

        /// <summary>Item with <paramref name="id"/> in <paramref name="slot"/>, or null (None / unknown).</summary>
        public CosmeticItem FindItem(CosmeticSlot slot, ushort id)
        {
            if (id == 0) return null;
            foreach (var it in Items(slot)) if (it.id == id) return it;
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

        /// <summary>True when the item may be worn by someone whose unlocks are <paramref name="ownedKeys"/> (may be null).</summary>
        public bool IsOwned(UnlockSource unlock, string key, ICollection<string> ownedKeys = null) =>
            unlockAllCosmetics || unlock == UnlockSource.Starter || (ownedKeys != null && key != null && ownedKeys.Contains(key));

        /// <summary>Palette colour, clamped.</summary>
        public Color PaletteColor(int index) => palette.Count == 0 ? Color.gray : palette[Mathf.Clamp(index, 0, palette.Count - 1)].color;

        /// <summary>Skin colour, clamped.</summary>
        public Color SkinColor(int index) => skinTones.Count == 0 ? new Color(0.85f, 0.64f, 0.49f) : skinTones[Mathf.Clamp(index, 0, skinTones.Count - 1)].color;

        /// <summary>Name tag style, clamped.</summary>
        public NameTagStyle NameTag(int index) => nameTags.Count == 0 ? new NameTagStyle { name = "White", color = Color.white } : nameTags[Mathf.Clamp(index, 0, nameTags.Count - 1)];

        /// <summary>Body variant, clamped.</summary>
        public BodyVariant Body(int index) => bodies.Count == 0 ? new BodyVariant { name = "Default", bodyScale = Vector3.one, headScale = 1f } : bodies[Mathf.Clamp(index, 0, bodies.Count - 1)];
    }
}
