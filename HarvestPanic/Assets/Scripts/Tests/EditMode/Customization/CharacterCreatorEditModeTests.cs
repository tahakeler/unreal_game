using System;
using System.Collections.Generic;
using System.Linq;
using HarvestPanic.Core;
using HarvestPanic.Customization;
using HarvestPanic.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HarvestPanic.Tests.EditMode.Customization
{
    /// <summary>
    /// Character creator data: catalogue (slots, entries overriding/adding, defaults upgrade), loadout v2 wire + save format
    /// with v1 migration, server validation of the new fields, the weighted/themed randomizer, the GDD Farm Name filter and
    /// the creator's reserved colours matching Frontend's UiPalette in every colour-blind mode.
    /// </summary>
    public class CharacterCreatorEditModeTests
    {
        CosmeticCatalog _cat;
        readonly List<Object> _trash = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _cat = ScriptableObject.CreateInstance<CosmeticCatalog>();
            _cat.ResetToDefaults();
            NameFilter.SetWordList(null);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_cat);
            foreach (var o in _trash) if (o != null) Object.DestroyImmediate(o);
            _trash.Clear();
            NameFilter.SetWordList(null);
            CosmeticCatalog.LocalOwnedKeys = null;
        }

        ushort Id(string key) { var it = _cat.FindItem(key, out _); Assert.That(it, Is.Not.Null, key); return it.id; }

        // ---- Wire + save format -----------------------------------------------------------------------------------------

        static FarmerLoadout Sample() => new FarmerLoadout
        {
            Body = 3, Primary = 23, Secondary = 7, Accent = 3, Skin = 13, NameTag = 7, Head = 7, Hair = 9, HairColor = 21, Outfit = 9,
            Hat = 10, Face = 4, Back = 3, Emote0 = 7, Emote1 = 6, Emote2 = 5, Emote3 = 1
        };

        [Test]
        public void WireV2_FitsTheBudget_AndRoundTripsEveryField()
        {
            var bytes = Sample().ToBytes();
            Assert.That(bytes.Length, Is.EqualTo(FarmerLoadout.WireSize));
            Assert.That(FarmerLoadout.WireSize, Is.LessThanOrEqualTo(FarmerLoadout.WireBudget), "GDD AC7: <= 32 bytes");
            Assert.That(bytes[0], Is.EqualTo(FarmerLoadout.Format), "format byte first");
            Assert.That(FarmerLoadout.FromBytes(bytes), Is.EqualTo(Sample()));
        }

        [Test]
        public void WireV1_LegacyBytes_LoadWithTheBaseLook()
        {
            // v1 layout: body, primary, secondary, accent, skin, tag, then hat/face/back/e0..e3 as u16 little endian.
            var v1 = new byte[] { 1, 4, 14, 17, 3, 2, 8, 0, 3, 0, 2, 0, 1, 0, 2, 0, 3, 0, 4, 0 };
            var l = FarmerLoadout.FromBytes(v1);
            Assert.That(l.Body, Is.EqualTo(1)); Assert.That(l.Primary, Is.EqualTo(4)); Assert.That(l.Secondary, Is.EqualTo(14));
            Assert.That(l.Accent, Is.EqualTo(17)); Assert.That(l.Skin, Is.EqualTo(3)); Assert.That(l.NameTag, Is.EqualTo(2));
            Assert.That(l.Hat, Is.EqualTo(8)); Assert.That(l.Face, Is.EqualTo(3)); Assert.That(l.Back, Is.EqualTo(2));
            Assert.That(new[] { l.Emote0, l.Emote1, l.Emote2, l.Emote3 }, Is.EqualTo(new ushort[] { 1, 2, 3, 4 }));
            Assert.That(l.Head, Is.EqualTo(0)); Assert.That(l.Hair, Is.EqualTo(0)); Assert.That(l.Outfit, Is.EqualTo(0));
            Assert.That(l.HairColor, Is.EqualTo(FarmerLoadout.DefaultHairColor));
        }

        [Test]
        public void SaveV1Json_MigratesToV2_KeepingTheLook()
        {
            const string v1 = "{\"v\":1,\"body\":1,\"primary\":4,\"secondary\":14,\"accent\":17,\"skin\":3,\"tag\":2,\"hat\":8,\"face\":3,\"back\":2,\"e0\":1,\"e1\":2,\"e2\":3,\"e3\":4}";
            Assert.That(LoadoutStorage.TryFromJson(v1, out var l, out int version));
            Assert.That(version, Is.EqualTo(1));
            Assert.That(l.Hat, Is.EqualTo(8)); Assert.That(l.Body, Is.EqualTo(1)); Assert.That(l.NameTag, Is.EqualTo(2));
            Assert.That(l.Head, Is.EqualTo(0), "v1 had no head: base look");
            Assert.That(l.Hair, Is.EqualTo(0)); Assert.That(l.Outfit, Is.EqualTo(0));
            Assert.That(l.HairColor, Is.EqualTo(FarmerLoadout.DefaultHairColor), "missing hair colour -> default, not palette 0 (blue)");
            Assert.That(LoadoutValidator.Sanitize(l, _cat, null, out var report), Is.EqualTo(l), report);

            Assert.That(LoadoutStorage.TryFromJson("{\"body\":0,\"hat\":5}", out var ancient, out int vAncient), "oldest saves had no version");
            Assert.That(vAncient, Is.EqualTo(1)); Assert.That(ancient.Hat, Is.EqualTo(5));

            var json = LoadoutStorage.ToJson(Sample());
            StringAssert.Contains("\"v\":2", json);
            Assert.That(LoadoutStorage.TryFromJson(json, out var back, out int v2));
            Assert.That(v2, Is.EqualTo(2));
            Assert.That(back, Is.EqualTo(Sample()));
        }

        [Test]
        public void LoadOrCreate_ResavesAV1SaveAsV2()
        {
            string saved = PlayerPrefs.GetString(LoadoutStorage.PrefsKey, null);
            try
            {
                PlayerPrefs.SetString(LoadoutStorage.PrefsKey, "{\"v\":1,\"body\":1,\"primary\":4,\"secondary\":14,\"accent\":17,\"skin\":3,\"tag\":2,\"hat\":8,\"face\":3,\"back\":2,\"e0\":1,\"e1\":2,\"e2\":3,\"e3\":4}");
                var l = LoadoutStorage.LoadOrCreate(_cat);
                Assert.That(l.Hat, Is.EqualTo(8));
                Assert.That(LoadoutStorage.TryFromJson(PlayerPrefs.GetString(LoadoutStorage.PrefsKey), out var re, out int v));
                Assert.That(v, Is.EqualTo(LoadoutStorage.SaveVersion));
                Assert.That(re, Is.EqualTo(l));
            }
            finally
            {
                if (saved == null) PlayerPrefs.DeleteKey(LoadoutStorage.PrefsKey); else PlayerPrefs.SetString(LoadoutStorage.PrefsKey, saved);
            }
        }

        // ---- Catalogue -------------------------------------------------------------------------------------------------

        [Test]
        public void Catalog_HasEveryCreatorSlot_WithWireSafeUniqueIds()
        {
            Assert.That(_cat.bodies.Count, Is.InRange(2, 4));
            Assert.That(_cat.Items(CosmeticSlot.Head).Count, Is.GreaterThanOrEqualTo(4));
            Assert.That(_cat.Items(CosmeticSlot.Hair).Count, Is.GreaterThanOrEqualTo(6));
            Assert.That(_cat.Items(CosmeticSlot.Outfit).Count, Is.GreaterThanOrEqualTo(6));
            Assert.That(_cat.emotes.Count, Is.GreaterThan(4), "4 free + unlockables");
            foreach (CosmeticSlot slot in Enum.GetValues(typeof(CosmeticSlot)))
            {
                var items = _cat.Items(slot);
                Assert.That(items.All(i => i.id > 0 && i.id <= FarmerLoadout.MaxId(slot)), slot + ": ids fit the wire field");
                Assert.That(items.Select(i => i.id).Distinct().Count(), Is.EqualTo(items.Count), slot + ": unique ids");
                Assert.That(items.All(i => !string.IsNullOrEmpty(i.key) && i.key.StartsWith(slot.ToString().ToLowerInvariant() + ".")), slot + ": keys are slot-prefixed");
                Assert.That(items.Where(i => i.unlock != UnlockSource.Starter).All(i => !string.IsNullOrEmpty(CosmeticCatalog.UnlockHint(i.unlock, i.unlockHint))), slot + ": locked items explain themselves");
                Assert.That(CosmeticCatalog.BaseLookName(slot), Is.Not.Empty);
            }
            var allKeys = Enum.GetValues(typeof(CosmeticSlot)).Cast<CosmeticSlot>().SelectMany(s => _cat.Items(s)).Select(i => i.key).ToList();
            Assert.That(allKeys.Distinct().Count(), Is.EqualTo(allKeys.Count), "keys unique across slots");
            foreach (var outfit in _cat.Items(CosmeticSlot.Outfit))
                Assert.That(outfit.outfit.overallsPattern != OutfitPattern.Plain || outfit.outfit.shirtPattern != OutfitPattern.Plain || outfit.parts.Count > 0 || outfit.outfit.shirtTint != PartTint.Secondary,
                    outfit.key + " must look different from plain overalls");
            foreach (var head in _cat.Items(CosmeticSlot.Head))
                Assert.That(head.parts.Count > 0 || head.headShape != Vector3.one || head.prefab != null, head.key + " must change the head");
            Assert.That(_cat.FindEmote("chickendance").loopsUntilMove);
        }

        [Test]
        public void Entries_OverrideBuiltInsById_AndAddNewItems()
        {
            var straw = ScriptableObject.CreateInstance<CosmeticEntry>(); _trash.Add(straw);
            straw.slot = CosmeticSlot.Hat;
            straw.item = new CosmeticItem { id = CosmeticCatalog.StrawHatId, key = "hat.straw", displayName = "Straw Hat (Tripo)", flavour = "x", unlock = UnlockSource.Starter };
            var newHat = ScriptableObject.CreateInstance<CosmeticEntry>(); _trash.Add(newHat);
            newHat.slot = CosmeticSlot.Hat;
            newHat.item = new CosmeticItem { id = 42, key = "hat.lantern", displayName = "Lantern Cap", flavour = "y", unlock = UnlockSource.Trunk };
            int before = _cat.Items(CosmeticSlot.Hat).Count;
            _cat.entries.Add(straw);
            _cat.entries.Add(newHat);
            var hats = _cat.Items(CosmeticSlot.Hat);
            Assert.That(hats.Count, Is.EqualTo(before + 1), "same id replaces, new id adds");
            Assert.That(_cat.FindItem(CosmeticSlot.Hat, CosmeticCatalog.StrawHatId).displayName, Is.EqualTo("Straw Hat (Tripo)"));
            Assert.That(hats[0].displayName, Is.EqualTo("Straw Hat (Tripo)"), "replacement keeps the built-in's position");
            Assert.That(hats[hats.Count - 1].key, Is.EqualTo("hat.lantern"));
            Assert.That(_cat.FindItem(CosmeticSlot.Face, 42), Is.Null, "entries only join their own slot");

            var l = FarmerLoadout.Default; l.Hat = 42;
            _cat.unlockAllCosmetics = false;
            Assert.That(LoadoutValidator.Sanitize(l, _cat, null, out _).Hat, Is.EqualTo(CosmeticCatalog.StrawHatId), "unowned entry -> default");
            Assert.That(LoadoutValidator.Sanitize(l, _cat, new HashSet<string> { "hat.lantern" }, out _).Hat, Is.EqualTo(42));
        }

        [Test]
        public void EnsureDefaults_UpgradesAnOldCatalogAndKeepsEdits()
        {
            var old = ScriptableObject.CreateInstance<CosmeticCatalog>(); _trash.Add(old);
            old.ResetToDefaults();
            old.heads.Clear(); old.hairs.Clear(); old.outfits.Clear();
            old.bodies.RemoveRange(2, old.bodies.Count - 2);
            old.emotes.RemoveAll(e => e.id > 4);
            old.hats[0].displayName = "Owner Renamed Straw";
            old.hats[0].unlockHint = null;
            old.dataVersion = 1;
            int added = old.EnsureDefaults();
            Assert.That(added, Is.GreaterThan(20));
            Assert.That(old.Items(CosmeticSlot.Hair).Count, Is.EqualTo(_cat.Items(CosmeticSlot.Hair).Count));
            Assert.That(old.bodies.Count, Is.EqualTo(_cat.bodies.Count));
            Assert.That(old.emotes.Count, Is.EqualTo(_cat.emotes.Count));
            Assert.That(old.hats[0].displayName, Is.EqualTo("Owner Renamed Straw"), "owner edits survive");
            Assert.That(old.dataVersion, Is.EqualTo(CosmeticCatalog.DefaultsVersion));
            Assert.That(old.EnsureDefaults(), Is.EqualTo(0), "idempotent");
        }

        [Test]
        public void CatalogAsset_HasTheCreatorWiring()
        {
            var asset = AssetDatabase.LoadAssetAtPath<CosmeticCatalog>("Assets/Art/Cosmetics/CosmeticCatalog.asset");
            Assert.That(asset, Is.Not.Null);
            Assert.That(asset.creatorStyle, Is.Not.Null, "CharacterCreator.uss wired");
            Assert.That(asset.dataVersion, Is.EqualTo(CosmeticCatalog.DefaultsVersion), "asset upgraded by the builder");
            Assert.That(asset.Items(CosmeticSlot.Hair).Count, Is.GreaterThan(0));
            var link = Resources.Load<CustomizationLink>(CustomizationLink.ResourcePath);
            Assert.That(link != null && link.catalog == asset, "main menu can find the catalogue");
            var v = asset.farmerPrefab.GetComponent<FarmerVisual>();
            Assert.That(v.ChestAttachPoint, Is.Not.Null);
            Assert.That(v.HipsAttachPoint, Is.Not.Null);
            foreach (var smr in asset.farmerPrefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (smr.name.StartsWith("Farmer_")) Assert.That(smr.sharedMesh.uv.Any(u => u != Vector2.zero), smr.name + " has pattern UVs");
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Farmer/Test/FarmerNetTest.prefab"), Is.Not.Null);
        }

        // ---- Validator -----------------------------------------------------------------------------------------------------

        [Test]
        public void Validator_NewSlots_UnknownOrUnownedFallBackToTheBaseLook()
        {
            var l = FarmerLoadout.Default;
            l.Head = 200; l.Hair = 201; l.Outfit = 202; l.HairColor = 99;
            var clean = LoadoutValidator.Sanitize(l, _cat, null, out var report);
            Assert.That(clean.Head, Is.EqualTo(0)); Assert.That(clean.Hair, Is.EqualTo(0)); Assert.That(clean.Outfit, Is.EqualTo(0));
            Assert.That(clean.HairColor, Is.EqualTo(FarmerLoadout.DefaultHairColor));
            StringAssert.Contains("outfit", report);

            _cat.unlockAllCosmetics = false;
            l = FarmerLoadout.Default;
            l.Outfit = (byte)Id("outfit.raincoat"); // Trunk
            l.Hair = (byte)Id("hair.mohawk");       // Trunk
            l.Head = (byte)Id("head.long");         // Starter
            l.Emote2 = _cat.FindEmote("shrug").id;  // Trunk emote
            clean = LoadoutValidator.Sanitize(l, _cat, null, out _);
            Assert.That(clean.Outfit, Is.EqualTo(0)); Assert.That(clean.Hair, Is.EqualTo(0));
            Assert.That(clean.Head, Is.EqualTo(l.Head), "starter heads are always allowed");
            Assert.That(clean.Emote2, Is.EqualTo(FarmerLoadout.Default.Emote2));
            var owned = new HashSet<string> { "outfit.raincoat", "hair.mohawk", "emote.shrug" };
            Assert.That(LoadoutValidator.Sanitize(l, _cat, owned, out var r2), Is.EqualTo(l), r2);
        }

        // ---- Randomizer ----------------------------------------------------------------------------------------------------

        [Test]
        public void Randomizer_IsValidWeightedAndSometimesThemed()
        {
            var counts = new Dictionary<ushort, int>();
            int themed = 0, hats = 0;
            for (int seed = 0; seed < 1500; seed++)
            {
                var r = LoadoutRandomizer.Roll(_cat, new System.Random(seed), FarmerLoadout.Default);
                Assert.That(LoadoutValidator.Sanitize(r, _cat, null, out var report), Is.EqualTo(r), $"seed {seed}: {report}");
                Assert.That(r.Primary, Is.Not.EqualTo(r.Secondary), "overalls never match the shirt");
                Assert.That(r.NameTag, Is.EqualTo(FarmerLoadout.Default.NameTag));
                Assert.That(r.Emote3, Is.EqualTo(FarmerLoadout.Default.Emote3));
                if (LoadoutRandomizer.LastTheme != null) themed++;
                if (r.Hat != 0) { hats++; counts[r.Hat] = counts.TryGetValue(r.Hat, out int c) ? c + 1 : 1; }
            }
            Assert.That(hats, Is.GreaterThan(1300), "almost always a hat");
            Assert.That(themed, Is.InRange(150, 500), "about a quarter are themed costumes");
            Assert.That(counts[Id("hat.cone")], Is.GreaterThan(counts[Id("hat.fedora")]), "silly hats are weighted up");
        }

        [Test]
        public void Randomizer_OnlyPicksOwnedItemsAtTheShipGate()
        {
            _cat.unlockAllCosmetics = false;
            for (int seed = 0; seed < 300; seed++)
            {
                var r = LoadoutRandomizer.Roll(_cat, new System.Random(seed), FarmerLoadout.Default);
                foreach (CosmeticSlot slot in Enum.GetValues(typeof(CosmeticSlot)))
                {
                    var it = _cat.FindItem(slot, r.GetItem(slot));
                    Assert.That(it == null || it.unlock == UnlockSource.Starter, $"seed {seed}: {slot} {it?.key} is locked");
                }
                Assert.That(_cat.skinTones[r.Skin].fantasy, Is.False);
            }
        }

        [Test]
        public void RandomFarmNames_AlwaysPassTheFilter()
        {
            for (int seed = 0; seed < 200; seed++)
                Assert.That(NameFilter.Check(LoadoutRandomizer.RandomFarmName(new System.Random(seed)), out _), Is.EqualTo(NameCheck.Ok));
        }

        // ---- Name filter (GDD "Anti-abuse: names") ------------------------------------------------------------------------

        [TestCase("Sir Turnip")] [TestCase("Zoë_99")] [TestCase("Jean-Luc")] [TestCase("Bob")] [TestCase("Bass Hit")]
        [TestCase("Classic Cass")] [TestCase("Assassin")] [TestCase("Scunthorpe")] [TestCase("Grape Ape")] [TestCase("Peacock")]
        public void NameFilter_AllowsHonestNames(string name)
        {
            Assert.That(NameFilter.Check(name, out _), Is.EqualTo(NameCheck.Ok), name);
        }

        [TestCase("fuck")] [TestCase("FUUUCK")] [TestCase("5h1t head")] [TestCase("f u c k")] [TestCase("f_uck me")] [TestCase("Big Ass")]
        [TestCase("a55")] [TestCase("Fück")] [TestCase("Sh!tlord")] [TestCase("Dick")]
        public void NameFilter_CatchesProfanityThroughLeetspeakAndSpacing(string name)
        {
            Assert.That(NameFilter.ContainsProfanity(name), Is.True, name);
        }

        [Test]
        public void NameFilter_LengthCharsetAndFallback()
        {
            Assert.That(NameFilter.Check("  ", out _), Is.EqualTo(NameCheck.Empty));
            Assert.That(NameFilter.Check("Al", out _), Is.EqualTo(NameCheck.TooShort));
            Assert.That(NameFilter.Check("Abcdefghijklmnopq", out _), Is.EqualTo(NameCheck.TooLong));
            Assert.That(NameFilter.Check("Exactly16Chars__", out _), Is.EqualTo(NameCheck.Ok));
            Assert.That(NameFilter.Check("Bob!", out _), Is.EqualTo(NameCheck.BadCharacters));
            Assert.That(NameFilter.Check("<b>Bob", out _), Is.EqualTo(NameCheck.BadCharacters));
            Assert.That(NameFilter.Check("Farmer 🐷", out _), Is.EqualTo(NameCheck.BadCharacters));
            Assert.That(NameFilter.Check("  Sir   Turnip  ", out string clean), Is.EqualTo(NameCheck.Ok));
            Assert.That(clean, Is.EqualTo("Sir Turnip"), "trimmed, inner spaces collapsed");
            Assert.That(NameFilter.Resolve("shit", 7), Is.EqualTo("Farmhand #7"));
            Assert.That(NameFilter.Resolve("", 7), Is.EqualTo(""), "empty Farm Name = use the Steam name");
            Assert.That(NameFilter.Resolve("Mabel", 7), Is.EqualTo("Mabel"));
            Assert.That(NameFilter.Fallback(0), Is.EqualTo("Farmhand #1"));
        }

        [Test]
        public void NameFilter_IsDataDriven()
        {
            Assert.That(Resources.Load<TextAsset>(NameFilter.ResourcePath), Is.Not.Null, "DT_NameFilter data file");
            Assert.That(NameFilter.ContainsProfanity("Turnip"), Is.False);
            NameFilter.SetWordList("turnip\n=pip\n!turnipseed");
            Assert.That(NameFilter.ContainsProfanity("Sir Turnip"), Is.True);
            Assert.That(NameFilter.ContainsProfanity("Pip"), Is.True);
            Assert.That(NameFilter.ContainsProfanity("Pippin"), Is.False, "whole-word entry");
            Assert.That(NameFilter.ContainsProfanity("Turnipseed"), Is.False, "allow-list entry");
        }

        // ---- Accessibility -------------------------------------------------------------------------------------------------

        [Test]
        public void CreatorReservedColours_MatchUiPalette_InEveryMode()
        {
            foreach (ColorblindMode mode in Enum.GetValues(typeof(ColorblindMode)))
                foreach (ReservedColor c in Enum.GetValues(typeof(ReservedColor)))
                {
                    var mine = CreatorTheme.Get((CreatorReserved)(int)c, (UiColorblind)(int)mode);
                    Assert.That(mine, Is.EqualTo(UiPalette.Get(c, mode)), $"{mode} {c}");
                    Assert.That(CreatorTheme.RootClass((UiColorblind)(int)mode), Is.EqualTo(UiPalette.RootClass(mode)));
                }
            Assert.That(Enum.GetNames(typeof(CreatorReserved)), Is.EqualTo(Enum.GetNames(typeof(ReservedColor))));
            Assert.That(Enum.GetNames(typeof(UiColorblind)), Is.EqualTo(Enum.GetNames(typeof(ColorblindMode))));
        }

        [Test]
        public void TagShapes_HaveOutlinesForTheUi()
        {
            foreach (TagShape s in Enum.GetValues(typeof(TagShape)))
            {
                var pts = NameTag.ShapeOutline(s);
                Assert.That(pts.Count, Is.GreaterThanOrEqualTo(3), s.ToString());
                Assert.That(pts.All(p => Mathf.Abs(p.x) <= 0.7f && Mathf.Abs(p.y) <= 0.7f), s + " fits the unit box");
            }
        }
    }
}
