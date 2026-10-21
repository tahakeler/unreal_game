using System.Collections.Generic;
using System.Linq;
using HarvestPanic.Customization;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HarvestPanic.Tests.EditMode.Customization
{
    /// <summary>Loadout validation, serialization size and catalog defaults (design/gdd/character-customization.md).</summary>
    public class CustomizationEditModeTests
    {
        CosmeticCatalog _cat;

        [SetUp]
        public void SetUp()
        {
            _cat = ScriptableObject.CreateInstance<CosmeticCatalog>();
            _cat.ResetToDefaults();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_cat);

        // ---- Serialization ----

        [Test]
        public void Loadout_WireSize_IsWithinGddBudget()
        {
            var bytes = FarmerLoadout.Default.ToBytes();
            Assert.That(bytes.Length, Is.EqualTo(FarmerLoadout.WireSize));
            Assert.That(bytes.Length, Is.LessThanOrEqualTo(32), "GDD AC7: payload <= 32 bytes");
        }

        [Test]
        public void Loadout_RoundTripsThroughNgoSerializer()
        {
            var l = new FarmerLoadout { Body = 1, Primary = 23, Secondary = 7, Accent = 3, Skin = 13, NameTag = 7, Hat = 10, Face = 4, Back = 3, Emote0 = 4, Emote1 = 3, Emote2 = 2, Emote3 = 1 };
            Assert.That(FarmerLoadout.FromBytes(l.ToBytes()), Is.EqualTo(l));
        }

        [Test]
        public void Loadout_DefaultIsStrawHatAndStarterEmotes()
        {
            var d = FarmerLoadout.Default;
            Assert.That(d.Hat, Is.EqualTo(CosmeticCatalog.StrawHatId));
            Assert.That(new[] { d.Emote0, d.Emote1, d.Emote2, d.Emote3 }.Select(id => _cat.FindEmote(id).key), Is.EqualTo(new[] { "wave", "point", "dance", "faint" }));
        }

        [Test]
        public void Storage_JsonRoundTrip_AndRejectsGarbage()
        {
            var l = WardrobeTestLineup.SetA[2];
            Assert.That(LoadoutStorage.TryFromJson(LoadoutStorage.ToJson(l), out var back), Is.True);
            Assert.That(back, Is.EqualTo(l));
            Assert.That(LoadoutStorage.TryFromJson("", out _), Is.False);
            Assert.That(LoadoutStorage.TryFromJson("{not json", out _), Is.False);
            Assert.That(LoadoutStorage.PrefsKey, Is.EqualTo("HP_Loadout"));
        }

        // ---- Catalog defaults ----

        [Test]
        public void Catalog_HasVsHatsFaceAndBackItems()
        {
            var hats = _cat.hats.Select(h => h.displayName).ToList();
            Assert.That(hats.Count, Is.GreaterThanOrEqualTo(10));
            foreach (var n in new[] { "Straw Hat", "Trucker Cap", "Bucket Helmet", "Pumpkin Head", "Cowboy Hat", "Chef Toque", "Flower Crown", "Traffic Cone", "Viking Horns", "Grandpa's Fedora" })
                Assert.That(hats, Does.Contain(n));
            var faces = string.Join("|", _cat.faces.Select(f => f.displayName));
            foreach (var n in new[] { "Moustache", "Goggles", "Sunglasses", "Clown Nose" }) StringAssert.Contains(n, faces);
            var backs = string.Join("|", _cat.backs.Select(f => f.displayName));
            foreach (var n in new[] { "Basket", "Cape", "Pitchfork" }) StringAssert.Contains(n, backs);
        }

        [Test]
        public void Catalog_ItemsHaveUniqueNonZeroIdsFlavourAndParts()
        {
            foreach (CosmeticSlot slot in System.Enum.GetValues(typeof(CosmeticSlot)))
            {
                var items = _cat.Items(slot);
                Assert.That(items.All(i => i.id != 0), slot + ": id 0 is reserved for None");
                Assert.That(items.Select(i => i.id).Distinct().Count(), Is.EqualTo(items.Count), slot + ": duplicate ids");
                Assert.That(items.Select(i => i.key).Distinct().Count(), Is.EqualTo(items.Count), slot + ": duplicate keys");
                foreach (var i in items)
                {
                    bool wearable = slot == CosmeticSlot.Hat || slot == CosmeticSlot.Face || slot == CosmeticSlot.Back || slot == CosmeticSlot.Hair && !i.hideBaseHair;
                    if (wearable) Assert.That(i.parts.Count, Is.GreaterThan(0), i.key);
                    Assert.That(i.flavour, Is.Not.Empty, i.key);
                }
            }
        }

        [Test]
        public void Catalog_ColoursMatchGdd()
        {
            Assert.That(_cat.palette.Count, Is.EqualTo(24), "24-swatch palette");
            Assert.That(_cat.skinTones.Count(s => !s.fantasy), Is.EqualTo(12));
            Assert.That(_cat.skinTones.Count(s => s.fantasy), Is.EqualTo(2));
            Assert.That(_cat.nameTags.Count, Is.EqualTo(8));
            Assert.That(_cat.nameTags.Select(t => t.shape).Distinct().Count(), Is.EqualTo(8), "each tag colour has its own shape");
            Assert.That(ColorUtility.ToHtmlStringRGB(_cat.palette[0].color), Is.EqualTo("3B6FD1"), "P1 Denim Blue");
            Assert.That(_cat.bodies.Count, Is.GreaterThanOrEqualTo(2), "GDD: 2 at VS, 4 at EA");
            Assert.That(_cat.bodies[0].name, Is.EqualTo("Sturdy"));
            Assert.That(_cat.bodies[1].name, Is.EqualTo("Lanky"));
        }

        [Test]
        public void Catalog_GatesAndEmoteTuning()
        {
            Assert.That(_cat.unlockAllCosmetics, Is.True, "bUnlockAllCosmetics defaults on in VS");
            var wave = _cat.FindEmote("wave");
            Assert.That(wave.noiseLoudness, Is.EqualTo(0.6f).Within(1e-4));
            var faint = _cat.FindEmote("faint");
            Assert.That(faint.kind, Is.EqualTo(EmoteKind.Faint));
            Assert.That(faint.duration, Is.EqualTo(2f));
            Assert.That(faint.cooldown, Is.EqualTo(5f));
            Assert.That(_cat.FindEmote("dance").loopsUntilMove, Is.True);
            Assert.That(_cat.FindEmote("point").placesPing, Is.True);
            Assert.That(_cat.emoteRateLimit, Is.EqualTo(2f));
            Assert.That(_cat.previewDistance, Is.EqualTo(2.5f));
        }

        [Test]
        public void CatalogAsset_IsBuiltAndWired()
        {
            var asset = AssetDatabase.LoadAssetAtPath<CosmeticCatalog>("Assets/Art/Cosmetics/CosmeticCatalog.asset");
            Assert.That(asset, Is.Not.Null, "run FarmerAssetBuilder.BuildAll");
            Assert.That(asset.farmerPrefab, Is.Not.Null);
            Assert.That(asset.baseMaterial, Is.Not.Null);
            Assert.That(asset.unlitMaterial, Is.Not.Null);
            Assert.That(asset.wardrobePanel, Is.Not.Null);
            Assert.That(asset.hats.Count, Is.GreaterThanOrEqualTo(10));
            var visual = asset.farmerPrefab.GetComponent<FarmerVisual>();
            Assert.That(visual, Is.Not.Null);
            Assert.That(visual, Is.InstanceOf<HarvestPanic.Core.IPlayerHooks>());
            Assert.That(visual.HeadAttachPoint, Is.Not.Null);
            Assert.That(visual.BodyAnimator, Is.Not.Null);
            Assert.That(visual.BodyRenderer, Is.Not.Null);
            Assert.That(asset.farmerPrefab.GetComponent<Unity.Netcode.NetworkObject>(), Is.Null, "FarmerBody must not carry its own NetworkObject (it nests under the Player's)");
        }

        // ---- Validation ----

        [Test]
        public void Validator_LeavesValidLoadoutAlone()
        {
            var l = WardrobeTestLineup.SetA[1];
            var clean = LoadoutValidator.Sanitize(l, _cat, null, out var report);
            Assert.That(clean, Is.EqualTo(l));
            Assert.That(report, Is.Empty);
        }

        [Test]
        public void Validator_ReplacesOutOfRangeAndUnknownWithDefaults()
        {
            var bad = new FarmerLoadout { Body = 9, Primary = 99, Secondary = 24, Accent = 200, Skin = 50, NameTag = 8, Hat = 999, Face = 77, Back = 12, Emote0 = 500, Emote1 = 2, Emote2 = 3, Emote3 = 4 };
            var clean = LoadoutValidator.Sanitize(bad, _cat, null, out var report);
            var d = FarmerLoadout.Default;
            Assert.That(clean.Body, Is.EqualTo(d.Body));
            Assert.That(clean.Primary, Is.EqualTo(d.Primary));
            Assert.That(clean.Secondary, Is.EqualTo(d.Secondary));
            Assert.That(clean.Accent, Is.EqualTo(d.Accent));
            Assert.That(clean.Skin, Is.EqualTo(d.Skin));
            Assert.That(clean.NameTag, Is.EqualTo(d.NameTag));
            Assert.That(clean.Hat, Is.EqualTo(d.Hat));
            Assert.That(clean.Face, Is.EqualTo(0));
            Assert.That(clean.Back, Is.EqualTo(0));
            Assert.That(clean.Emote0, Is.EqualTo(d.Emote0));
            Assert.That(report, Is.Not.Empty);
        }

        [Test]
        public void Validator_UnownedItemsReplacedWhenUnlockAllIsOff()
        {
            _cat.unlockAllCosmetics = false;
            var l = FarmerLoadout.Default;
            l.Hat = 8;  // Traffic Cone (Letter)
            l.Face = 1; // Moustache (Trunk)
            l.Skin = 12; // Pickled (fantasy)
            var clean = LoadoutValidator.Sanitize(l, _cat, null, out _);
            Assert.That(clean.Hat, Is.EqualTo(CosmeticCatalog.StrawHatId), "GDD AC3: unowned item -> default");
            Assert.That(clean.Face, Is.EqualTo(0));
            Assert.That(clean.Skin, Is.EqualTo(FarmerLoadout.Default.Skin));

            var owned = new HashSet<string> { "hat.cone", "face.moustache", "skin.12" };
            var ok = LoadoutValidator.Sanitize(l, _cat, owned, out var report);
            Assert.That(ok, Is.EqualTo(l), report);

            l.Hat = 5; // Cowboy Hat is a starter item
            Assert.That(LoadoutValidator.Sanitize(l, _cat, null, out _).Hat, Is.EqualTo(5));
        }

        [Test]
        public void ResolveNameTag_LaterJoinerGetsNextFreeColour()
        {
            Assert.That(LoadoutValidator.ResolveNameTag(2, 8, new HashSet<byte>()), Is.EqualTo(2));
            Assert.That(LoadoutValidator.ResolveNameTag(2, 8, new HashSet<byte> { 2 }), Is.EqualTo(3));
            Assert.That(LoadoutValidator.ResolveNameTag(7, 8, new HashSet<byte> { 7, 0 }), Is.EqualTo(1));
            var all = new HashSet<byte>(Enumerable.Range(0, 8).Select(i => (byte)i));
            Assert.That(LoadoutValidator.ResolveNameTag(5, 8, all), Is.EqualTo(5));
        }

        [Test]
        public void Randomize_AlwaysProducesValidSillyLoadouts()
        {
            int hats = 0;
            for (int seed = 0; seed < 300; seed++)
            {
                var r = LoadoutStorage.Randomize(_cat, new System.Random(seed), FarmerLoadout.Default);
                var clean = LoadoutValidator.Sanitize(r, _cat, null, out var report);
                Assert.That(clean, Is.EqualTo(r), $"seed {seed}: {report}");
                Assert.That(r.Primary, Is.Not.EqualTo(r.Secondary), "overalls and shirt should clash, not match");
                Assert.That(r.Emote0, Is.EqualTo(FarmerLoadout.Default.Emote0), "emotes are kept");
                if (r.Hat != 0) hats++;
            }
            Assert.That(hats, Is.GreaterThan(250), "almost always a hat");
        }

        [Test]
        public void ConeMesh_IsClosedAndUnitSized()
        {
            var m = AccessoryBuilder.Cone(0.5f);
            Assert.That(m.bounds.size.y, Is.EqualTo(1f).Within(1e-3));
            Assert.That(m.bounds.size.x, Is.EqualTo(1f).Within(1e-2));
            Assert.That(m.triangles.Length % 3, Is.EqualTo(0));
            foreach (TagShape s in System.Enum.GetValues(typeof(TagShape))) Assert.That(NameTag.ShapeMesh(s).vertexCount, Is.GreaterThan(3), s.ToString());
        }
    }
}
