using System.IO;
using System.Linq;
using HarvestPanic.Customization;
using HarvestPanic.Customization.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HarvestPanic.Tests.EditMode.Customization
{
    /// <summary>
    /// Tripo -> catalogue pipeline (HarvestPanic/Cosmetics/Import Tripo Cosmetic): name identification for spec and raw
    /// Tripo file names, and a real FBX import (a CC0 Tripo prop stands in for a cosmetic) into a throwaway catalogue.
    /// </summary>
    public class TripoCosmeticImporterTests
    {
        const string TestTripo = "Assets/ThirdParty/Tripo/_ImporterTest";
        const string TestItems = "Assets/Art/Cosmetics/_ImporterTest";
        CosmeticCatalog _cat;

        [SetUp]
        public void SetUp()
        {
            _cat = ScriptableObject.CreateInstance<CosmeticCatalog>();
            _cat.ResetToDefaults();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_cat);
            AssetDatabase.DeleteAsset(TestItems); // prefabs nest the FBX: delete them first
            AssetDatabase.DeleteAsset(TestTripo);
        }

        [Test]
        public void Identify_SpecNamesAndRawTripoNames()
        {
            var straw = TripoCosmeticImporter.Identify("SM_Hat_StrawHat");
            Assert.That(straw.kind, Is.EqualTo(TripoCosmeticImporter.Kind.Cosmetic));
            Assert.That(straw.slot, Is.EqualTo(CosmeticSlot.Hat));
            Assert.That(straw.key, Is.EqualTo("hat.straw"));

            var cowboy = TripoCosmeticImporter.Identify("cartoon+cowboy+hat+3d+model", "Hats");
            Assert.That(cowboy.key, Is.EqualTo("hat.cowboy"), "raw Tripo download in the Hats folder");
            Assert.That(cowboy.assetName, Is.EqualTo("SM_Hat_CowboyHat"));

            var beard = TripoCosmeticImporter.Identify("SM_Hair_Beard");
            Assert.That(beard.key, Is.EqualTo("hair.beard"));
            Assert.That(beard.attach, Is.EqualTo(AttachPoint.Face), "beards hang off the face");

            var round = TripoCosmeticImporter.Identify("SM_Head_Round");
            Assert.That(round.key, Is.EqualTo("head.round"));
            Assert.That(round.displayName, Is.EqualTo("Round"));

            var lantern = TripoCosmeticImporter.Identify("SM_Hat_LanternCap");
            Assert.That(lantern.key, Is.EqualTo("hat.lanterncap"), "unknown spec name = new item, not a keyword match");
            Assert.That(lantern.displayName, Is.EqualTo("Lantern Cap"));

            var shades = TripoCosmeticImporter.Identify("oversized+round+sunglasses+3d+model", "Face");
            Assert.That(shades.key, Is.EqualTo("face.sunglasses"));

            Assert.That(TripoCosmeticImporter.Identify("SK_Farmer_Sturdy").kind, Is.EqualTo(TripoCosmeticImporter.Kind.Rigged));
            Assert.That(TripoCosmeticImporter.Identify("SK_Outfit_Raincoat_Sturdy").kind, Is.EqualTo(TripoCosmeticImporter.Kind.Rigged));
            Assert.That(TripoCosmeticImporter.Identify("headless+farmer+body+3d+model", "Bodies").kind, Is.EqualTo(TripoCosmeticImporter.Kind.Rigged));
            Assert.That(TripoCosmeticImporter.Identify("Pig_Hamlet").kind, Is.EqualTo(TripoCosmeticImporter.Kind.NotCosmetic), "props in the Tripo folder are ignored");
        }

        static string Stage(string folder, string fileStem, bool withMask)
        {
            if (!AssetDatabase.IsValidFolder(TestTripo)) AssetDatabase.CreateFolder("Assets/ThirdParty/Tripo", "_ImporterTest");
            AssetDatabase.CreateFolder(TestTripo, folder);
            string dir = TestTripo + "/" + folder;
            string fbx = dir + "/" + fileStem + ".fbx";
            Assert.That(AssetDatabase.CopyAsset("Assets/ThirdParty/Tripo/EggBasket/EggBasket.fbx", fbx), "copy test FBX");
            Assert.That(AssetDatabase.CopyAsset("Assets/ThirdParty/Tripo/EggBasket/EggBasket_BaseColor.jpg", dir + "/" + fileStem + "_BaseColor.jpg"));
            if (withMask)
            {
                var t = new Texture2D(8, 8, TextureFormat.RGBA32, false);
                var px = Enumerable.Repeat(new Color(1f, 0f, 0f, 0f), 64).ToArray();
                t.SetPixels(px); t.Apply();
                File.WriteAllBytes(dir + "/" + fileStem + "_Mask.png", t.EncodeToPNG());
                Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(dir + "/" + fileStem + "_Mask.png");
            }
            AssetDatabase.Refresh();
            return fbx;
        }

        [Test]
        public void Import_NewItemFitsTheSlotAndUsesTheTintMask()
        {
            string fbx = Stage("SM_Back_TestCrate", "eggs+in+basket+3d+model", withMask: true);
            int before = _cat.Items(CosmeticSlot.Back).Count;
            var entry = TripoCosmeticImporter.Import(fbx, _cat, TestItems, out string report);
            Assert.That(entry, Is.Not.Null, report);
            Assert.That(entry.slot, Is.EqualTo(CosmeticSlot.Back));
            Assert.That(entry.item.key, Is.EqualTo("back.testcrate"));
            Assert.That(entry.item.id, Is.EqualTo(_cat.BuiltIn(CosmeticSlot.Back).Max(i => i.id) + 1), "next free id");
            Assert.That(entry.item.tintMode, Is.EqualTo(TintMode.MaskTexture));
            Assert.That(_cat.Items(CosmeticSlot.Back).Count, Is.EqualTo(before + 1));
            Assert.That(_cat.FindItem(CosmeticSlot.Back, entry.item.id), Is.SameAs(entry.item));

            var prefab = entry.item.prefab;
            Assert.That(prefab, Is.Not.Null);
            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers.Length, Is.GreaterThan(0));
            Assert.That(renderers.All(r => r.sharedMaterials.All(m => m != null && m.shader.name == TripoCosmeticImporter.ShaderName)), "tint-mask material");
            Assert.That(prefab.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(0), "cosmetics never collide");

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                var b = renderers.Length > 0 ? inst.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((x, y) => { x.Encapsulate(y); return x; }) : default;
                Assert.That(Mathf.Max(b.size.x, b.size.y, b.size.z), Is.EqualTo(0.45f).Within(0.01f), "fitted to the back-item size");
                Assert.That(b.max.z, Is.EqualTo(0f).Within(0.01f), "front face sits on the farmer's back");
            }
            finally { Object.DestroyImmediate(inst); }

            // Re-import keeps the id and the owner's edits.
            entry.item.displayName = "Owner Named Crate";
            var again = TripoCosmeticImporter.Import(fbx, _cat, TestItems, out _);
            Assert.That(again.item.id, Is.EqualTo(entry.item.id));
            Assert.That(again.item.displayName, Is.EqualTo("Owner Named Crate"));
            Assert.That(_cat.entries.Count(e => e == again), Is.EqualTo(1));
        }

        [Test]
        public void Import_SpecItemReplacesThePlaceholderById()
        {
            string fbx = Stage("Straw", "SM_Hat_StrawHat", withMask: false);
            var entry = TripoCosmeticImporter.Import(fbx, _cat, TestItems, out string report);
            Assert.That(entry, Is.Not.Null, report);
            Assert.That(entry.item.id, Is.EqualTo(CosmeticCatalog.StrawHatId), "same id: saves and the network loadout are unchanged");
            Assert.That(entry.item.displayName, Is.EqualTo("Straw Hat"));
            Assert.That(entry.item.flavour, Is.EqualTo("Smells like Grandpa. Smells like regret."));
            Assert.That(entry.item.tintMode, Is.EqualTo(TintMode.None), "hats are authored in real colours");
            Assert.That(_cat.FindItem(CosmeticSlot.Hat, CosmeticCatalog.StrawHatId).prefab, Is.SameAs(entry.item.prefab));
            Assert.That(_cat.Items(CosmeticSlot.Hat).Count, Is.EqualTo(_cat.BuiltIn(CosmeticSlot.Hat).Count), "replaced, not added");
            StringAssert.Contains("replaces the placeholder", report);
        }

        [Test]
        public void TintMaskShader_Compiles()
        {
            var shader = Shader.Find(TripoCosmeticImporter.ShaderName);
            Assert.That(shader, Is.Not.Null);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
        }
    }
}
