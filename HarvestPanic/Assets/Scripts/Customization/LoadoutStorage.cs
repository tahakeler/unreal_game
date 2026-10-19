using System;
using UnityEngine;

namespace HarvestPanic.Customization
{
    /// <summary>
    /// Local persistence of the last-used loadout (GDD "Persistence: ... The last-used loadout is restored on launch").
    /// Stored as JSON in PlayerPrefs key <see cref="PrefsKey"/>. A first launch gets a random, silly loadout.
    /// Unlock ownership is not stored here (meta-progression/save-persistence own the profile).
    /// </summary>
    public static class LoadoutStorage
    {
        /// <summary>PlayerPrefs key.</summary>
        public const string PrefsKey = "HP_Loadout";

        [Serializable]
        class Dto
        {
            public int v = 1;
            public int body, primary, secondary, accent, skin, tag, hat, face, back, e0, e1, e2, e3;
        }

        /// <summary>Serialises to the JSON stored in PlayerPrefs.</summary>
        public static string ToJson(FarmerLoadout l) => JsonUtility.ToJson(new Dto
        {
            body = l.Body, primary = l.Primary, secondary = l.Secondary, accent = l.Accent, skin = l.Skin, tag = l.NameTag,
            hat = l.Hat, face = l.Face, back = l.Back, e0 = l.Emote0, e1 = l.Emote1, e2 = l.Emote2, e3 = l.Emote3
        });

        /// <summary>Parses JSON written by <see cref="ToJson"/>; false on empty or malformed input.</summary>
        public static bool TryFromJson(string json, out FarmerLoadout loadout)
        {
            loadout = FarmerLoadout.Default;
            if (string.IsNullOrEmpty(json)) return false;
            try
            {
                var d = JsonUtility.FromJson<Dto>(json);
                if (d == null) return false;
                loadout = new FarmerLoadout
                {
                    Body = B(d.body), Primary = B(d.primary), Secondary = B(d.secondary), Accent = B(d.accent), Skin = B(d.skin), NameTag = B(d.tag),
                    Hat = U(d.hat), Face = U(d.face), Back = U(d.back), Emote0 = U(d.e0), Emote1 = U(d.e1), Emote2 = U(d.e2), Emote3 = U(d.e3)
                };
                return true;
            }
            catch (ArgumentException) { return false; }
        }

        static byte B(int v) => (byte)Mathf.Clamp(v, 0, 255);
        static ushort U(int v) => (ushort)Mathf.Clamp(v, 0, ushort.MaxValue);

        /// <summary>True when a loadout has been saved before.</summary>
        public static bool HasSaved => PlayerPrefs.HasKey(PrefsKey);

        /// <summary>Saves <paramref name="loadout"/> as the last-used loadout.</summary>
        public static void Save(FarmerLoadout loadout)
        {
            PlayerPrefs.SetString(PrefsKey, ToJson(loadout));
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Loads the saved loadout, sanitised against <paramref name="catalog"/>. On first launch (or a corrupt save)
        /// generates a random silly loadout and saves it.
        /// </summary>
        public static FarmerLoadout LoadOrCreate(CosmeticCatalog catalog)
        {
            if (TryFromJson(PlayerPrefs.GetString(PrefsKey, null), out var saved))
                return catalog != null ? LoadoutValidator.Sanitize(saved, catalog, null, out _) : saved;
            var fresh = catalog != null ? Randomize(catalog, new System.Random(), FarmerLoadout.Default) : FarmerLoadout.Default;
            Save(fresh);
            return fresh;
        }

        /// <summary>
        /// "Surprise me!": random body, clashing colours, a hat (almost always), maybe a face and back item.
        /// Keeps the name-tag colour and equipped emotes of <paramref name="keep"/>. Only owned items are picked.
        /// </summary>
        public static FarmerLoadout Randomize(CosmeticCatalog catalog, System.Random rng, FarmerLoadout keep)
        {
            var l = keep;
            l.Body = (byte)rng.Next(Math.Max(1, catalog.bodies.Count));
            int n = Math.Max(1, catalog.palette.Count);
            l.Primary = (byte)rng.Next(n);
            l.Secondary = (byte)((l.Primary + 1 + rng.Next(n - 1)) % n);
            l.Accent = (byte)rng.Next(n);
            int skins = 0;
            foreach (var s in catalog.skinTones) if (!s.fantasy || catalog.unlockAllCosmetics) skins++;
            l.Skin = (byte)rng.Next(Math.Max(1, skins));
            l.Hat = rng.NextDouble() < catalog.surpriseHatChance ? PickOwned(catalog, CosmeticSlot.Hat, rng) : (ushort)0;
            l.Face = rng.NextDouble() < 0.6 ? PickOwned(catalog, CosmeticSlot.Face, rng) : (ushort)0;
            l.Back = rng.NextDouble() < 0.5 ? PickOwned(catalog, CosmeticSlot.Back, rng) : (ushort)0;
            return l;
        }

        static ushort PickOwned(CosmeticCatalog catalog, CosmeticSlot slot, System.Random rng)
        {
            var items = catalog.Items(slot);
            int owned = 0;
            foreach (var it in items) if (catalog.IsOwned(it.unlock, it.key)) owned++;
            if (owned == 0) return 0;
            int pick = rng.Next(owned);
            foreach (var it in items)
                if (catalog.IsOwned(it.unlock, it.key) && pick-- == 0) return it.id;
            return 0;
        }
    }
}
