using System.Collections;
using HarvestPanic.Core;
using HarvestPanic.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HarvestPanic.Tests.PlayMode.World
{
    public class MuddleburyPlayModeTests
    {
        const string ScenePath = "Assets/Scenes/Muddlebury.unity";

        IEnumerator Load()
        {
#if UNITY_EDITOR
            var op = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            while (!op.isDone) yield return null;
            yield return null;
#else
            Assert.Ignore("Editor-only scene loading");
            yield break;
#endif
        }

        [UnityTest] public IEnumerator SceneLoadsWithMarkers()
        {
            yield return Load();
            Assert.AreEqual(4, LevelMarker.FindAll(MarkerRole.PlayerStart).Count);
            Assert.AreEqual(20, LevelMarker.FindAll(MarkerRole.BridgePlank).Count);
            Assert.AreEqual(1, LevelMarker.FindAll(MarkerRole.MarketPen).Count);
            Assert.AreEqual(1, LevelMarker.FindAll(MarkerRole.River).Count);
            Assert.AreEqual(1, LevelMarker.FindAll(MarkerRole.RiverPile).Count);
        }

        [UnityTest] public IEnumerator HeavyBodyBreaksAPlank()
        {
            yield return Load();
            var plank = LevelMarker.FindAll(MarkerRole.BridgePlank)[10].GetComponent<BridgePlank>();
            var weight = GameObject.CreatePrimitive(PrimitiveType.Cube);
            weight.name = "400kg";
            weight.transform.position = plank.transform.position + Vector3.up * 0.58f; // resting on the deck (no impact damage)
            var rb = weight.AddComponent<Rigidbody>();
            rb.mass = 400f;
            float t = 0f;
            bool cracked = false;
            while (plank.State != PlankState.Broken && t < 8f)
            {
                cracked |= plank.State == PlankState.Cracked;
                t += Time.deltaTime;
                yield return null;
            }
            Assert.IsTrue(cracked, "plank should crack before it breaks");
            Assert.AreEqual(PlankState.Broken, plank.State, $"load {plank.LastLoadKg} kg");
            Assert.Less(t, 5f, "400 kg breaks a plank in about 2.8 s");
            Assert.IsFalse(plank.GetComponent<BoxCollider>().enabled);
            // Neighbours untouched, and the body falls through the gap.
            Assert.AreEqual(PlankState.Intact, LevelMarker.FindAll(MarkerRole.BridgePlank)[9].GetComponent<BridgePlank>().State);
            yield return new WaitForSeconds(0.6f);
            Assert.Less(weight.transform.position.y, plank.transform.position.y - 0.5f);
            Object.FindFirstObjectByType<BridgeController>().RepairAll();
            Assert.AreEqual(PlankState.Intact, plank.State);
        }

        [UnityTest] public IEnumerator LonePlayerWeightDoesNotDamage()
        {
            yield return Load();
            var plank = LevelMarker.FindAll(MarkerRole.BridgePlank)[3].GetComponent<BridgePlank>();
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.transform.position = plank.transform.position + Vector3.up * 1.2f;
            body.AddComponent<Rigidbody>().mass = 70f;
            yield return new WaitForSeconds(1.5f);
            Assert.AreEqual(100f, plank.Integrity, 1e-3f);
        }
    }
}
