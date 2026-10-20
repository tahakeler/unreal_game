using System.Collections;
using HarvestPanic.AI;
using HarvestPanic.Core;
using HarvestPanic.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HarvestPanic.Tests.PlayMode.Livestock
{
    public class LivestockPlayTests
    {
        const string ScenePath = "Assets/Scenes/Dev/CritterTest.unity";

        sealed class FakePlayer : MonoBehaviour, IKnockable
        {
            public bool Down; public Vector3 Last; public int Hits;
            public bool IsDown => Down;
            public void ApplyImpulse(Vector3 impulse, ulong instigator, BlameVerb verb) { Last = impulse; Hits++; if (impulse.magnitude >= 900f) Down = true; }
            public void SendToBarn(BlameVerb reason, bool chargeFee) { }
        }

        [UnitySetUp]
        public IEnumerator Load()
        {
            CritterSpawnDirector.AutoSpawn = false;
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#endif
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown() { CritterSpawnDirector.AutoSpawn = true; yield return null; }

        static CritterBrain Spawn(string id, Vector3 pos)
        {
            Content.Critters.Invalidate();
            Assert.IsTrue(CritterSpawnDirector.TryResolve(id, out var p), id);
            return CritterSpawnDirector.Spawn(p, pos, false);
        }

        [UnityTest]
        public IEnumerator Cow_CannotBeLiftedSolo_TwoCarriersCan()
        {
            var cow = Spawn("Critter.Cow", new Vector3(-6f, 0f, 6f));
            yield return null;
            var carry = cow.GetComponent<CritterCarryable>();
            int hands = carry.HandsRequired;
            Assert.AreEqual(4, hands, "cow carry hands (2 carriers)");
            // physics-carry lift rule as implemented by Gameplay's CarryBody: each carrier contributes up to 2 hands.
            int one = CarryMath.HandsContributed(hands, 0);
            Assert.That(one, Is.LessThan(hands), "one carrier can only drag the cow");
            int two = one + CarryMath.HandsContributed(hands, one);
            Assert.That(two, Is.GreaterThanOrEqualTo(hands), "two carriers lift");
            Assert.IsFalse(LivestockRules.CanLift(1, hands)); Assert.IsTrue(LivestockRules.CanLift(2, hands));

            // AI side: a solo holder can never secure her (break-free chance > 0); two holders are secure.
            carry.OnGrabbed(1);
            Assert.AreEqual(CritterState.Carried, cow.State);
            float solo = CritterMath.BreakFreeChance(cow.Personality.SecureCarriers, 1, cow.Tuning);
            Assert.That(solo, Is.GreaterThan(0f));
            carry.OnGrabbed(2);
            Assert.AreEqual(0f, CritterMath.BreakFreeChance(cow.Personality.SecureCarriers, 2, cow.Tuning), 1e-5f);
            Assert.IsFalse(carry.Struggle(0f), "2 carriers: a worst-case roll still cannot break free");
            Assert.That(cow.GetComponent<CowBehaviour>().MooCount, Is.GreaterThanOrEqualTo(1), "complaint moo on grab");
        }

        [UnityTest]
        public IEnumerator Goat_HeadbuttKnocksPlayerDown()
        {
            var goat = Spawn("Critter.Goat", new Vector3(6f, 0f, -4f));
            goat.transform.rotation = Quaternion.identity;
            yield return null;
            var player = new GameObject("FakePlayer");
            player.layer = LayerMask.NameToLayer(GameLayers.Player);
            player.transform.position = goat.transform.position + goat.transform.forward * 1.2f + Vector3.up * 0.9f;
            player.AddComponent<CapsuleCollider>();
            var fp = player.AddComponent<FakePlayer>();
            yield return new WaitForFixedUpdate();
            var gb = goat.GetComponent<GoatBehaviour>();
            Assert.IsTrue(gb.TryStartHeadbutt(Time.time, gb.Tuning, force: true), "wind-up starts");
            Assert.IsTrue(gb.IsTelegraphing);
            Assert.AreEqual(0, fp.Hits, "telegraph first, no instant hit");
            float until = Time.time + gb.Tuning.HeadbuttTelegraph + 0.5f;
            while (Time.time < until && fp.Hits == 0) yield return null;
            Assert.AreEqual(1, fp.Hits);
            Assert.IsTrue(fp.Down, "knocked down");
            Assert.That(fp.Last.magnitude, Is.LessThan(1800f), "never a KO");
            Object.Destroy(player);
        }

        [UnityTest]
        public IEnumerator AllCritterPrefabs_HaveLodGroups()
        {
            Content.Critters.Invalidate();
            foreach (var id in new[] { "Critter.Pig", "Critter.Chicken", "Critter.Cow", "Critter.Goat" })
            {
                Assert.IsTrue(Content.Critters.TryGet(id, out var p), id);
                var lod = p.Prefab.GetComponent<LODGroup>();
                Assert.IsNotNull(lod, id + " LODGroup");
                var lods = lod.GetLODs();
                Assert.That(lods.Length, Is.GreaterThanOrEqualTo(2), id);
                Assert.That(lods[0].renderers.Length, Is.GreaterThan(0), id);
                Assert.That(lods[lods.Length - 1].screenRelativeTransitionHeight, Is.GreaterThan(0f), id + " culls when tiny");
            }
            yield return null;
        }
    }
}
