using System;
using System.Collections.Generic;
using HarvestPanic.Core;

namespace HarvestPanic.Customization
{
    /// <summary>
    /// "Randomize": a fun, weighted roll that still looks like a farmer someone chose on purpose.
    /// - Items are picked by <see cref="CosmeticItem.randomWeight"/> among owned items only (silly hats come up more).
    /// - Colours follow a light harmony: shirts lean to light neutrals, boots to dark earth tones, hair to natural shades;
    ///   overalls and shirt never match. One roll in ~12 is "chaos" (anything goes, still never matching).
    /// - About one roll in four is a themed costume (Road Crew, Chef, Viking, Scarecrow...).
    /// Keeps the name-tag colour and the equipped emotes. Pure logic (EditMode tested).
    /// </summary>
    public static class LoadoutRandomizer
    {
        static readonly int[] LightNeutrals = { 14, 15, 16, 18, 19, 20, 7 };   // Cream, Snow, Sand, Wheat, Mint, Lemon, Sky
        static readonly int[] EarthTones = { 17, 13, 12, 10, 11, 4, 9 };       // Mud Brown, Charcoal, Navy, Moss, Rust, Barn Brick, Plum
        static readonly int[] NaturalHair = { 13, 17, 18, 11, 16, 15, 14 };    // Charcoal, Mud Brown, Wheat, Rust, Sand, Snow, Cream

        /// <summary>A themed costume: item keys (null = roll normally) and optional palette indices (-1 = roll).</summary>
        public sealed class Theme
        {
            public string name, hat, face, back, hair, outfit, head;
            public int primary = -1, secondary = -1, accent = -1, hairColor = -1;
        }

        /// <summary>Themed costumes (only applied when every named item exists and is owned; missing ones are rolled).</summary>
        public static readonly Theme[] Themes =
        {
            new Theme { name = "Road Crew", hat = "hat.cone", face = "face.goggles", outfit = "outfit.mud", primary = 1, secondary = 20, accent = 13 },
            new Theme { name = "Chef", hat = "hat.chef", face = "face.moustache", outfit = "outfit.gingham", secondary = 15 },
            new Theme { name = "Viking", hat = "hat.viking", hair = "hair.beard", back = "back.cape", accent = 17, hairColor = 11 },
            new Theme { name = "Scarecrow", hat = "hat.pumpkin", outfit = "outfit.patched", back = "back.pitchfork", secondary = 18 },
            new Theme { name = "Grandpa", hat = "hat.fedora", face = "face.moustache", hair = "hair.tufts", outfit = "outfit.pinstripe", secondary = 14, hairColor = 15 },
            new Theme { name = "Cowpoke", hat = "hat.cowboy", face = "face.moustache", outfit = "outfit.flannel", accent = 17 },
            new Theme { name = "Rainy Day", hat = "hat.bucket", outfit = "outfit.raincoat", primary = 20, accent = 13 },
            new Theme { name = "Flower Child", hat = "hat.flowercrown", face = "face.sunglasses", outfit = "outfit.polka", hair = "hair.afro" },
            new Theme { name = "Gym Farmer", hat = "hat.trucker", outfit = "outfit.tank", face = "face.sunglasses", hair = "hair.mohawk" },
        };

        /// <summary>Name of the theme used by the last <see cref="Roll"/> on this thread (null = none; UI flavour).</summary>
        [ThreadStatic] public static string LastTheme;

        /// <summary>Rolls a new loadout from <paramref name="keep"/> (name tag and emotes kept).</summary>
        public static FarmerLoadout Roll(CosmeticCatalog catalog, Random rng, FarmerLoadout keep)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var l = keep;
            LastTheme = null;
            int n = Math.Max(1, catalog.palette.Count);
            bool chaos = rng.NextDouble() < 0.08;

            l.Body = (byte)rng.Next(Math.Max(1, catalog.bodies.Count));
            l.Primary = (byte)rng.Next(n);
            l.Secondary = (byte)(chaos ? rng.Next(n) : PickIndex(rng, LightNeutrals, n, 0.55));
            if (l.Secondary == l.Primary) l.Secondary = (byte)((l.Primary + 1 + rng.Next(Math.Max(1, n - 1))) % n);
            l.Accent = (byte)(chaos ? rng.Next(n) : PickIndex(rng, EarthTones, n, 0.7));
            l.HairColor = (byte)(chaos ? rng.Next(n) : PickIndex(rng, NaturalHair, n, 0.8));
            l.Skin = PickSkin(catalog, rng);

            l.Head = (byte)(rng.NextDouble() < 0.5 ? 0 : Pick(catalog, CosmeticSlot.Head, rng));
            l.Hair = (byte)(rng.NextDouble() < 0.15 ? 0 : Pick(catalog, CosmeticSlot.Hair, rng));
            l.Outfit = (byte)(rng.NextDouble() < 0.4 ? 0 : Pick(catalog, CosmeticSlot.Outfit, rng));
            l.Hat = rng.NextDouble() < catalog.surpriseHatChance ? Pick(catalog, CosmeticSlot.Hat, rng) : (ushort)0;
            l.Face = rng.NextDouble() < 0.55 ? Pick(catalog, CosmeticSlot.Face, rng) : (ushort)0;
            l.Back = rng.NextDouble() < 0.45 ? Pick(catalog, CosmeticSlot.Back, rng) : (ushort)0;

            if (!chaos && rng.NextDouble() < 0.25) ApplyTheme(catalog, Themes[rng.Next(Themes.Length)], ref l, n);
            if (l.Secondary == l.Primary) l.Secondary = (byte)((l.Primary + 1) % n);
            return l;
        }

        static void ApplyTheme(CosmeticCatalog cat, Theme t, ref FarmerLoadout l, int n)
        {
            bool Set(string key, CosmeticSlot slot, ref FarmerLoadout lo)
            {
                if (key == null) return true;
                var it = cat.FindItem(key, out var s);
                if (it == null || s != slot || !cat.IsOwnedLocally(it)) return false;
                lo.SetItem(slot, it.id);
                return true;
            }
            var copy = l;
            bool ok = Set(t.hat, CosmeticSlot.Hat, ref copy) & Set(t.face, CosmeticSlot.Face, ref copy) & Set(t.back, CosmeticSlot.Back, ref copy)
                      & Set(t.hair, CosmeticSlot.Hair, ref copy) & Set(t.outfit, CosmeticSlot.Outfit, ref copy) & Set(t.head, CosmeticSlot.Head, ref copy);
            if (!ok) return; // a themed costume with a missing piece is just a random farmer
            if (t.primary >= 0 && t.primary < n) copy.Primary = (byte)t.primary;
            if (t.secondary >= 0 && t.secondary < n) copy.Secondary = (byte)t.secondary;
            if (t.accent >= 0 && t.accent < n) copy.Accent = (byte)t.accent;
            if (t.hairColor >= 0 && t.hairColor < n) copy.HairColor = (byte)t.hairColor;
            if (t.face == null && t.name != "Viking") copy.Face = 0;
            l = copy;
            LastTheme = t.name;
        }

        static int PickIndex(Random rng, int[] preferred, int n, double chance)
        {
            if (rng.NextDouble() < chance)
            {
                for (int tries = 0; tries < 4; tries++)
                {
                    int i = preferred[rng.Next(preferred.Length)];
                    if (i < n) return i;
                }
            }
            return rng.Next(n);
        }

        static byte PickSkin(CosmeticCatalog cat, Random rng)
        {
            var owned = new List<int>();
            for (int i = 0; i < cat.skinTones.Count; i++) if (cat.IsSkinOwnedLocally(i)) owned.Add(i);
            return owned.Count == 0 ? (byte)0 : (byte)owned[rng.Next(owned.Count)];
        }

        /// <summary>Weighted pick among owned items of <paramref name="slot"/> (0 when none).</summary>
        public static ushort Pick(CosmeticCatalog cat, CosmeticSlot slot, Random rng)
        {
            var items = cat.Items(slot);
            double total = 0;
            foreach (var it in items) if (Eligible(cat, slot, it)) total += it.randomWeight;
            if (total <= 0) return 0;
            double r = rng.NextDouble() * total;
            foreach (var it in items)
            {
                if (!Eligible(cat, slot, it)) continue;
                r -= it.randomWeight;
                if (r <= 0) return it.id;
            }
            for (int i = items.Count - 1; i >= 0; i--) if (Eligible(cat, slot, items[i])) return items[i].id;
            return 0;
        }

        static bool Eligible(CosmeticCatalog cat, CosmeticSlot slot, CosmeticItem it) =>
            it.randomWeight > 0f && it.id <= FarmerLoadout.MaxId(slot) && cat.IsOwnedLocally(it);

        // ---- Names ----------------------------------------------------------------------------------------------------

        static readonly string[] First = { "Muddy", "Lucky", "Sir", "Big", "Lil", "Captain", "Grumpy", "Sleepy", "Turbo", "Cousin", "Old", "Mighty", "Saucy", "Dusty", "Wobbly", "Farmer", "Auntie", "Uncle", "Doc" };
        static readonly string[] Second = { "Pete", "Turnip", "Mabel", "Haybale", "Cluck", "Gus", "Barnaby", "Spud", "Pickles", "Daisy", "Hank", "Clover", "Nugget", "Mo", "Biscuit", "Radish", "Goose", "Beans", "Marge", "Waffles" };

        /// <summary>A silly Farm Name (3-16 chars, passes <see cref="NameFilter"/>).</summary>
        public static string RandomFarmName(Random rng)
        {
            for (int i = 0; i < 20; i++)
            {
                string s = First[rng.Next(First.Length)] + " " + Second[rng.Next(Second.Length)];
                if (NameFilter.Check(s, out string clean) == NameCheck.Ok) return clean;
            }
            return "Farmer Spud";
        }
    }
}
