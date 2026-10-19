using System.Linq;
using HarvestPanic.Core;
using HarvestPanic.World;
using HarvestPanic.World.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HarvestPanic.Tests.EditMode.World
{
    public class PlankMathTests
    {
        [Test] public void NoDamageAtOrBelowSafeLoad()
        {
            Assert.AreEqual(0f, PlankMath.DamagePerSecond(250f, false));
            Assert.AreEqual(0f, PlankMath.DamagePerSecond(70f, true));
        }

        [Test] public void DamageMatchesGddFormula()
        {
            Assert.AreEqual(8f, PlankMath.DamagePerSecond(290f, false), 1e-4f);    // 4-player cow carry example
            Assert.AreEqual(12f, PlankMath.DamagePerSecond(290f, true), 1e-4f);    // x1.5 cracked
            Assert.AreEqual(2f, PlankMath.DamagePerSecond(260f, false), 1e-4f);    // loaded wagon example
            Assert.AreEqual(30f, PlankMath.DamagePerSecond(400f, false), 1e-4f);
        }

        [Test] public void DayMultipliersApply()
        {
            Assert.AreEqual((290f - 175f) * 0.2f * 0.7f, PlankMath.DamagePerSecond(290f, false, 250f, 0.7f, 0.7f), 1e-4f);
        }

        [Test] public void CowCarryBreaksInTenToFifteenSeconds()
        {
            float t = PlankMath.SecondsToBreak(290f);
            Assert.That(t, Is.InRange(10f, 15f));
            Assert.IsTrue(float.IsPositiveInfinity(PlankMath.SecondsToBreak(70f)), "a lone player never damages a plank");
        }

        [Test] public void ImpactsUseDesignUnits()
        {
            Assert.AreEqual(0f, PlankMath.ImpactDamage(800f));
            Assert.AreEqual(25f, PlankMath.ImpactDamage(1000f), 1e-4f);
        }

        [Test] public void SagIsZeroAtEndsAndMaxMidspan()
        {
            Assert.AreEqual(0f, PlankMath.Sag(0f, 0.7f), 1e-5f);
            Assert.AreEqual(0f, PlankMath.Sag(1f, 0.7f), 1e-5f);
            Assert.AreEqual(-0.7f, PlankMath.Sag(0.5f, 0.7f), 1e-5f);
        }
    }

    public class MuddleburySceneTests
    {
        [OneTimeSetUp] public void Open()
        {
            Assume.That(System.IO.File.Exists(BuildMuddlebury.ScenePath), "run BuildMuddlebury.Build first");
            EditorSceneManager.OpenScene(BuildMuddlebury.ScenePath, OpenSceneMode.Single);
        }

        static int Count(MarkerRole r) => LevelMarker.FindAll(r).Count;

        [Test] public void MarkerCountsMatchContract()
        {
            Assert.AreEqual(4, Count(MarkerRole.PlayerStart));
            Assert.AreEqual(1, Count(MarkerRole.MarketPen));
            Assert.AreEqual(2, Count(MarkerRole.CritterSpawner));
            Assert.AreEqual(5, Count(MarkerRole.Goods));
            Assert.AreEqual(20, Count(MarkerRole.BridgePlank));
            Assert.AreEqual(1, Count(MarkerRole.River));
            Assert.AreEqual(1, Count(MarkerRole.RiverPile));
            Assert.AreEqual(1, Count(MarkerRole.BarnRespawn));
            Assert.AreEqual(1, Count(MarkerRole.Wardrobe));
        }

        [Test] public void SpawnerAndGoodsKeys()
        {
            var sp = LevelMarker.FindAll(MarkerRole.CritterSpawner);
            Assert.IsTrue(sp.Any(m => m.Key == "Critter.Pig" && m.Count == 3));
            Assert.IsTrue(sp.Any(m => m.Key == "Critter.Chicken" && m.Count == 8));
            var goods = LevelMarker.FindAll(MarkerRole.Goods);
            Assert.AreEqual(1, goods.Count(m => m.Key == "Crop.PrizePumpkin"));
            Assert.AreEqual(4, goods.Count(m => m.Key == "Product.Crate"));
        }

        [Test] public void MarkersCarryTheirTags()
        {
            foreach (var m in Object.FindObjectsByType<LevelMarker>(FindObjectsSortMode.None))
                Assert.AreEqual(MarkerTag.ForRole(m.Role), m.tag, m.name);
        }

        [Test] public void PlanksAreOrderedTwoMetresApartOnBridgeLayer()
        {
            var planks = LevelMarker.FindAll(MarkerRole.BridgePlank);
            for (int i = 0; i < planks.Count; i++)
            {
                Assert.AreEqual(i, planks[i].Index);
                Assert.AreEqual(LayerMask.NameToLayer(GameLayers.Bridge), planks[i].gameObject.layer);
                Assert.IsNotNull(planks[i].GetComponent<BridgePlank>());
                if (i > 0) Assert.AreEqual(2f, planks[i].transform.position.x - planks[i - 1].transform.position.x, 0.01f);
            }
            Assert.AreEqual(40f, planks[19].transform.position.x - planks[0].transform.position.x + 2f, 0.01f, "bridge length");
        }

        [Test] public void RiverIsAWaterTrigger()
        {
            var river = LevelMarker.FindAll(MarkerRole.River)[0];
            Assert.AreEqual(LayerMask.NameToLayer(GameLayers.Water), river.gameObject.layer);
            Assert.IsTrue(river.GetComponent<BoxCollider>().isTrigger);
            Assert.IsNotNull(river.GetComponent<RiverVolume>());
        }

        [Test] public void BarnToMarketDistanceInSpec()
        {
            var barn = LevelMarker.FindAll(MarkerRole.BarnRespawn)[0].transform.position;
            var pen = LevelMarker.FindAll(MarkerRole.MarketPen)[0].transform.position;
            Assert.That(Vector3.Distance(barn, pen), Is.InRange(70f, 140f));
        }

        [Test] public void NavMeshBakedAndBridgeIsNetworked()
        {
            Assert.IsNotNull(Object.FindFirstObjectByType<Unity.AI.Navigation.NavMeshSurface>()?.navMeshData);
            Assert.IsNotNull(Object.FindFirstObjectByType<BridgeNetSync>());
        }
    }
}
