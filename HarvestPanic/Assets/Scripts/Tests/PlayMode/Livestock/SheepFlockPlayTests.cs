using System;
using System.Collections;
using System.Collections.Generic;
using HarvestPanic.AI;
using HarvestPanic.AI.Crops;
using HarvestPanic.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HarvestPanic.Tests.PlayMode.Livestock
{
    /// <summary>Sheep flock (livestock-roster §4/§8 AC4) and the mandrake scream spooking livestock (cursed-crops AC2).</summary>
    public class SheepFlockPlayTests
    {
        const string ScenePath = "Assets/Scenes/Dev/CritterTest.unity";

        sealed class FakeNoiseService : INoiseService
        {
            public readonly List<NoiseEvent> Heard = new List<NoiseEvent>();
            public event Action<NoiseEvent> OnNoise;
            public bool Emit(NoiseSource source, float loudness, Vector3 location, ulong instigatorId, ulong blamedPlayerId, NoiseTag tags)
            {
                var e = new NoiseEvent
                {
                    EventId = Guid.NewGuid(), Source = source, Loudness = loudness, Radius = 48f * loudness, Location = location,
                    InstigatorId = instigatorId, BlamedPlayerId = blamedPlayerId, Tags = tags, ServerTime = Time.timeAsDouble
                };
                Heard.Add(e);
                OnNoise?.Invoke(e);
                return true;
            }
        }

        FakeNoiseService _noise;

        [UnitySetUp]
        public IEnumerator Load()
        {
            CritterSpawnDirector.AutoSpawn = false;
            _noise = new FakeNoiseService();
            Services.Register<INoiseService>(_noise);
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#endif
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Services.Unregister<INoiseService>(_noise);
            CritterSpawnDirector.AutoSpawn = true;
            yield return null;
        }

        static List<CritterBrain> SpawnFlock(Vector3 centre, int n)
        {
            Content.Critters.Invalidate();
            Assert.IsTrue(CritterSpawnDirector.TryResolve("Critter.Sheep", out var p), "Critter.Sheep personality");
            var list = new List<CritterBrain>();
            for (int i = 0; i < n; i++)
            {
                float a = i * 72f * Mathf.Deg2Rad;
                list.Add(CritterSpawnDirector.Spawn(p, centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 1.2f, false));
            }
            return list;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        [UnityTest]
        public IEnumerator Flock_JoinsOneFollowHerd_WithOneBellWether()
        {
            var flock = SpawnFlock(new Vector3(-8f, 0f, -8f), 5);
            yield return new WaitForSeconds(0.5f);
            Assert.NotNull(flock[0].Flock);
            Assert.AreEqual(HerdMode.Follow, flock[0].Flock.Mode);
            foreach (var s in flock) Assert.AreSame(flock[0].Flock, s.Flock, "one flock");
            Assert.AreSame(flock[0], flock[0].FlockLeader, "oldest member leads");
            int bells = 0;
            foreach (var s in flock) if (s.GetComponent<SheepBehaviour>().IsLeader) bells++;
            Assert.AreEqual(1, bells, "exactly one bell-wether");
            Assert.IsTrue(flock[0].transform.Find("Bell").gameObject.activeSelf, "leader shows the bell");
        }

        [UnityTest]
        public IEnumerator Flock_PanicsTogether_SharedHeading()
        {
            var flock = SpawnFlock(new Vector3(-8f, 0f, -8f), 5);
            yield return new WaitForSeconds(0.5f);
            Vector3 c0 = Vector3.zero; foreach (var s in flock) c0 += Flat(s.transform.position); c0 /= flock.Count;
            // A shout right next to ONE edge sheep: the rest only learn of it through the flock alarm.
            var edge = flock[2];
            Vector3 shoutAt = Flat(edge.transform.position) + (Flat(edge.transform.position) - c0).normalized * 1.5f;
            _noise.Emit(NoiseSource.Shout, 1f, shoutAt, 0, PlayerIds.None, NoiseTag.Scream);

            var fled = new HashSet<CritterBrain>();
            float start = Time.time;
            while (Time.time - start < 2.5f && fled.Count < flock.Count)
            {
                foreach (var s in flock) if (s.State == CritterState.Flee || s.State == CritterState.Cornered) fled.Add(s);
                yield return null;
            }
            Assert.AreEqual(flock.Count, fled.Count, "whole flock panics within 2.5 s");
            yield return new WaitForSeconds(1.5f);

            Vector3 c1 = Vector3.zero; foreach (var s in flock) c1 += Flat(s.transform.position); c1 /= flock.Count;
            Vector3 flockMove = (c1 - c0);
            Assert.Greater(flockMove.magnitude, 3f, "flock stampedes away");
            int aligned = 0;
            foreach (var s in flock)
            {
                var v = s.GetComponent<UnityEngine.AI.NavMeshAgent>().velocity; v.y = 0f;
                if (v.sqrMagnitude > 0.25f && Vector3.Dot(v.normalized, flockMove.normalized) > 0.3f) aligned++;
            }
            Assert.GreaterOrEqual(aligned, 4, "stampede together (one heading), not a chicken-style scatter");
            float spread = 0f; foreach (var s in flock) spread = Mathf.Max(spread, Vector3.Distance(Flat(s.transform.position), c1));
            Assert.Less(spread, 9f, "flock stays bunched while it runs");
        }

        [UnityTest]
        public IEnumerator CarriedLeader_PullsFollowers_RosterAc4()
        {
            var flock = SpawnFlock(new Vector3(-8f, 0f, -8f), 5);
            yield return new WaitForSeconds(0.5f);
            var leader = flock[0].FlockLeader;
            Assert.NotNull(leader);
            leader.GetComponent<CritterCarryable>().OnGrabbed(1);
            Assert.AreEqual(CritterState.Carried, leader.State);
            // Walk the "carrier" 12 m east at a player's walk speed (4 m/s), then wait.
            Vector3 from = leader.transform.position, to = from + new Vector3(12f, 0f, 2f);
            float t0 = Time.time;
            while (Time.time - t0 < 3f) { leader.transform.position = Vector3.Lerp(from, to, (Time.time - t0) / 3f) + Vector3.up * 0.8f; yield return null; }
            float t1 = Time.time;
            var followers = new List<Vector3>();
            while (Time.time - t1 < 7f)
            {
                followers.Clear();
                foreach (var s in flock) if (s != leader) followers.Add(s.transform.position);
                if (FlockRules.ShareWithin(Flat(to), followers.ToArray(), 10f) >= 0.8f) break;
                yield return null;
            }
            float share = FlockRules.ShareWithin(Flat(to), followers.ToArray(), 10f);
            Assert.GreaterOrEqual(share, 0.8f, $">= 80% of followers within 10 m inside 10 s (got {share:P0})");
            foreach (var s in flock) if (s != leader) Assert.AreNotEqual(CritterState.Flee, s.State, "followers trail the carrier instead of fleeing");
        }

        [UnityTest]
        public IEnumerator MandrakePull_ScreamsLoud_SpooksLivestockWithin20m()
        {
            var flock = SpawnFlock(new Vector3(-8f, 0f, -8f), 5);
            Assert.IsTrue(CritterSpawnDirector.TryResolve("Critter.Cow", out var cowP));
            var cow = CritterSpawnDirector.Spawn(cowP, new Vector3(4f, 0f, -6f), false); // 'barely flees' cow, ~12 m away
            yield return new WaitForSeconds(0.5f);
            Content.Goods.Invalidate();
            Assert.IsTrue(Content.Goods.TryGet("Crop.Mandrake", out var def));
            var crop = Object.Instantiate(def.WorldPrefab, new Vector3(-2f, 0f, -12f), Quaternion.identity);
            var goods = crop.GetComponent<CursedCropGoods>();
            var scream = crop.GetComponent<MandrakeScream>();
            yield return null;
            Assert.IsTrue(goods.Planted);
            _noise.Heard.Clear();

            goods.OnGrabbed(1); // pull it out of the soil
            Assert.IsFalse(goods.Planted);
            Assert.AreEqual(1, scream.ScreamCount);
            var e = _noise.Heard.Find(n => (n.Tags & NoiseTag.Mandrake) != 0);
            Assert.AreEqual(NoiseSource.Crop, e.Source, "night director weights Crop noise x2");
            Assert.AreEqual(0.9f, e.Loudness, 1e-4f, "loudness 0.9");
            Assert.AreEqual(1UL, e.BlamedPlayerId, "the puller is blamed");
            Assert.IsTrue(scream.IsScreaming);

            float start = Time.time; bool allFled = false, cowFled = false;
            while (Time.time - start < 2f)
            {
                allFled = flock.TrueForAll(s => s.State == CritterState.Flee || s.State == CritterState.Cornered);
                cowFled |= cow.State == CritterState.Flee;
                if (allFled && cowFled) break;
                yield return null;
            }
            Assert.IsTrue(allFled, "sheep within 20 m flee");
            Assert.IsTrue(cowFled, "even the cow flees a mandrake (AC 2)");

            Assert.IsFalse(scream.TryScream(1), "8 s cooldown");
            yield return new WaitForSeconds(scream.Tuning.ScreamSeconds + 0.1f);
            Assert.IsFalse(scream.IsScreaming, "3 s scream");
            Object.Destroy(crop);
        }
    }
}
