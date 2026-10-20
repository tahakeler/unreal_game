using System.Collections;
using HarvestPanic.AI;
using HarvestPanic.Core;
using HarvestPanic.Customization;
using HarvestPanic.Economy;
using HarvestPanic.Gameplay;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HarvestPanic.Tests.PlayMode.Integration
{
    /// <summary>Cross-team slice: Muddlebury played directly (solo editor host) must have every system live.</summary>
    public class MuddleburyIntegrationTests
    {
        const string ScenePath = "Assets/Scenes/Muddlebury.unity";

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null)
            {
                nm.Shutdown();
                yield return null;
                Object.Destroy(nm.gameObject);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator SoloHost_PlayerCrittersGoodsEconomyHudWardrobeAllPresent()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            // The test runner's InitTestScene suppresses NetworkBootstrap.AutoSpawn, so do what it does for a real scene.
            var go = Object.Instantiate(HarvestPanic.Networking.NetworkingConfig.Load().networkManagerPrefab);
            go.GetComponent<HarvestPanic.Networking.SessionService>().HostInPlaceForEditor();
#else
            Assert.Ignore("Editor-only");
            yield break;
#endif
            float until = Time.realtimeSinceStartup + 20f;
            while ((PlayerCharacter.Local == null || EconomyDirector.Instance == null) && Time.realtimeSinceStartup < until) yield return null;
            yield return new WaitForSeconds(3f);

            var pc = PlayerCharacter.Local;
            var dnm = NetworkManager.Singleton;
            Assert.That(pc, Is.Not.Null, "host player did not spawn");
            Assert.That(pc.GetComponentInChildren<FarmerVisual>(true), Is.Not.Null, "player has no FarmerVisual body");
            var start = LevelMarker.FindAll(MarkerRole.PlayerStart)[0].transform.position;
            Assert.That(Vector3.Distance(pc.transform.position, start), Is.LessThan(3f), "player not at PlayerStart 0");

            int expected = 0;
            foreach (var m in LevelMarker.FindAll(MarkerRole.CritterSpawner)) expected += m.Count;
            Assert.That(expected, Is.GreaterThan(0));
            Assert.That(CritterRegistry.All.Count, Is.EqualTo(expected), "critters spawned vs marker counts");

            var eco = EconomyDirector.Instance;
            Assert.That(eco, Is.Not.Null, "EconomyDirector missing");
            Assert.That(Object.FindObjectsByType<Goods>(FindObjectsSortMode.None).Length,
                Is.EqualTo(LevelMarker.FindAll(MarkerRole.Goods).Count), "goods props vs Goods markers");
            Assert.That(Object.FindFirstObjectByType<EconomyHud>(), Is.Not.Null, "Economy HUD missing");
            Assert.That(Object.FindFirstObjectByType<WardrobeInteractable>(), Is.Not.Null, "wardrobe missing");
            Assert.That(CritterRegistry.All[0].GetComponent<IGoods>(), Is.Not.Null, "critters must be sellable");
        }
    }
}
