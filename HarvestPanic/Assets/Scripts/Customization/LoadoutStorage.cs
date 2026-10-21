using System;
using UnityEngine;

namespace HarvestPanic.Customization
{
    /// <summary>
    /// Local persistence of the last-used loadout (GDD "Persistence: ... The last-used loadout is restored on launch").
    /// Stored as JSON in PlayerPrefs key <see cref="PrefsKey"/>; format v2 adds head, hair, hair colour and outfit.
    /// v1 saves (no "v" or v=1) load with the base head/hair/outfit and the default hair colour, so nobody's farmer changes.
    /// A first launch gets a random, silly loadout. <see cref="FarmerCreated"/> records that the player confirmed a farmer
    /// in the character creator (the main menu opens the creator once until then).
    /// Unlock ownership is not stored here (meta-progression/save-persistence own the profile).
    /// </summary>
    public static class LoadoutStorage
    {
        /// <summary>PlayerPrefs key of the loadout JSON.</summary>
        public const string PrefsKey = "HP_Loadout";
        /// <summary>PlayerPrefs key set to 1 once a farmer was confirmed in the creator.</summary>
        public const string CreatedKey = "HP_FarmerCreated";
        /// <summary>PlayerPrefs key of the Farm Name when no IPlayerNameService is registered.</summary>
        public const string NameKey = "HP_FarmName";
        /// <summary>Save format written by <see cref="ToJson"/>.</summary>
        public const int SaveVersion = 2;

        [Serializable]
        class Dto
        {
            public int v = 1; // absent in the oldest saves -> 1
            public int body, primary, secondary, accent, skin, tag, hat, face, back, e0, e1, e2, e3;
            // v2
            public int head, hair, outfit;
            public int hairColor = -1;
        }

        /// <summary>Serialises to the JSON stored in PlayerPrefs (format v2).</summary>
        public static string ToJson(FarmerLoadout l) => JsonUtility.ToJson(new Dto
        {
            v = SaveVersion,
            body = l.Body, primary = l.Primary, secondary = l.Secondary, accent = l.Accent, skin = l.Skin, tag = l.NameTag,
            hat = l.Hat, face = l.Face, back = l.Back, e0 = l.Emote0, e1 = l.Emote1, e2 = l.Emote2, e3 = l.Emote3,
            head = l.Head, hair = l.Hair, hairColor = l.HairColor, outfit = l.Outfit,
        });

        /// <summary>Parses JSON written by <see cref="ToJson"/> (v2) or by the v1 build; false on empty or malformed input.</summary>
        public static bool TryFromJson(string json, out FarmerLoadout loadout) => TryFromJson(json, out loadout, out _);

        /// <summary>As <see cref="TryFromJson(string, out FarmerLoadout)"/>, also returning the save version that was read.</summary>
        public static bool TryFromJson(string json, out FarmerLoadout loadout, out int version)
        {
            loadout = FarmerLoadout.Default;
            version = 0;
            if (string.IsNullOrEmpty(json)) return false;
            try
            {
                var d = JsonUtility.FromJson<Dto>(json);
                if (d == null) return false;
                version = d.v <= 1 ? 1 : d.v;
                loadout = new FarmerLoadout
                {
                    Body = B(d.body), Primary = B(d.primary), Secondary = B(d.secondary), Accent = B(d.accent), Skin = B(d.skin), NameTag = B(d.tag),
                    Hat = U(d.hat), Face = U(d.face), Back = U(d.back), Emote0 = U(d.e0), Emote1 = U(d.e1), Emote2 = U(d.e2), Emote3 = U(d.e3),
                    // v1 migration: fields absent -> base look; hair colour absent (-1) -> default.
                    Head = version >= 2 ? B(d.head) : (byte)0,
                    Hair = version >= 2 ? B(d.hair) : (byte)0,
                    Outfit = version >= 2 ? B(d.outfit) : (byte)0,
                    HairColor = version >= 2 && d.hairColor >= 0 ? B(d.hairColor) : FarmerLoadout.DefaultHairColor,
                };
                return true;
            }
            catch (ArgumentException) { return false; }
        }

        static byte B(int v) => (byte)Mathf.Clamp(v, 0, 255);
        static ushort U(int v) => (ushort)Mathf.Clamp(v, 0, ushort.MaxValue);

        /// <summary>True when a loadout has been saved before.</summary>
        public static bool HasSaved => PlayerPrefs.HasKey(PrefsKey);

        /// <summary>True once the player confirmed a farmer in the character creator on this machine.</summary>
        public static bool FarmerCreated
        {
            get => PlayerPrefs.GetInt(CreatedKey, 0) == 1;
            set { PlayerPrefs.SetInt(CreatedKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Saves <paramref name="loadout"/> as the last-used loadout.</summary>
        public static void Save(FarmerLoadout loadout)
        {
            PlayerPrefs.SetString(PrefsKey, ToJson(loadout));
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Loads the saved loadout, sanitised against <paramref name="catalog"/>. A v1 save is migrated and re-saved as v2.
        /// On first launch (or a corrupt save) generates a random silly loadout and saves it.
        /// </summary>
        public static FarmerLoadout LoadOrCreate(CosmeticCatalog catalog)
        {
            if (TryFromJson(PlayerPrefs.GetString(PrefsKey, null), out var saved, out int version))
            {
                var clean = catalog != null ? LoadoutValidator.Sanitize(saved, catalog, CosmeticCatalog.LocalOwnedKeys, out _) : saved;
                if (version < SaveVersion) Save(clean);
                return clean;
            }
            var fresh = catalog != null ? Randomize(catalog, new System.Random(), FarmerLoadout.Default) : FarmerLoadout.Default;
            Save(fresh);
            return fresh;
        }

        /// <summary>"Randomize": see <see cref="LoadoutRandomizer.Roll"/>. Keeps the name-tag colour and emotes of <paramref name="keep"/>.</summary>
        public static FarmerLoadout Randomize(CosmeticCatalog catalog, System.Random rng, FarmerLoadout keep) => LoadoutRandomizer.Roll(catalog, rng, keep);
    }
}
