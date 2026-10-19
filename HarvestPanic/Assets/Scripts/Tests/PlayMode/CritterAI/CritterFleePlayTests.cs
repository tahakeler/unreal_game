using System;
using System.Collections;
using HarvestPanic.AI;
using HarvestPanic.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HarvestPanic.Tests.PlayMode.CritterAI
{
    public class CritterFleePlayTests
    {
        const string ScenePath = "Assets/Scenes/Dev/CritterTest.unity";

        sealed class FakeNoiseService : INoiseService
        {
            public event Action<NoiseEvent> OnNoise;
            public bool Emit(NoiseSource source, float loudness, Vector3 location, ulong instigatorId, ulong blamedPlayerId, NoiseTag tags)
            {
                OnNoise?.Invoke(new NoiseEvent
                {
                    EventId = Guid.NewGuid(), Source = source, Loudness = loudness, Radius = 25f * loudness, Location = location,
                    InstigatorId = instigatorId, BlamedPlayerId = blamedPlayerId, Tags = tags, ServerTime = Time.timeAsDouble
                });
                return true;
            }
        }

        FakeNoiseService _noise;

        [UnitySetUp]
        public IEnumerator Load()
        {
            _noise = new FakeNoiseService();
            Services.Register<INoiseService>(_noise);
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return null;
            Assert.Ignore("Editor-only scene load");
#endif
            yield return new WaitForSeconds(0.5f);
        }

        [TearDown]
        public void Cleanup() => Services.Unregister<INoiseService>(_noise);

        static CritterBrain FindPig()
        {
            foreach (var b in CritterRegistry.All)
                if (b != null && b.Personality != null && b.Personality.CritterId == "Critter.Pig") return b;
            return null;
        }

        [UnityTest]
        public IEnumerator Shout_MakesPigFleeAtLeast8mWithin3s()
        {
            var pig = FindPig();
            Assert.That(pig, Is.Not.Null, "spawner should have spawned the pig from its LevelMarker");
            Vector3 noiseAt = pig.transform.position + new Vector3(1.5f, 0f, 0f);
            _noise.Emit(NoiseSource.Shout, 1f, noiseAt, 0, PlayerIds.None, NoiseTag.Scream);
            Assert.That(pig.Threat, Is.GreaterThanOrEqualTo(pig.Personality.FleeThreshold));

            float start = Time.time, best = 0f;
            bool sawAlert = false;
            while (Time.time - start < 3f)
            {
                sawAlert |= pig.State == CritterState.Alert;
                best = Mathf.Max(best, Vector3.Distance(Flat(pig.transform.position), Flat(noiseAt)));
                if (best >= 8f) break;
                yield return null;
            }
            Assert.That(sawAlert, Is.True, "0.4 s alert tell happens before fleeing");
            Assert.That(best, Is.GreaterThanOrEqualTo(8f), $"pig only got {best:0.0} m away in {Time.time - start:0.00} s (state {pig.State})");
        }

        [UnityTest]
        public IEnumerator FoodNoise_LuresCalmPig()
        {
            var pig = FindPig();
            Assert.That(pig, Is.Not.Null);
            Vector3 bucket = pig.transform.position + new Vector3(0f, 0f, 6f);
            _noise.Emit(NoiseSource.Tool, 0.4f, bucket, 0, PlayerIds.None, NoiseTag.Food);
            float start = Time.time;
            while (Time.time - start < 5f && Vector3.Distance(Flat(pig.transform.position), Flat(bucket)) > 1.6f) yield return null;
            Assert.That(Vector3.Distance(Flat(pig.transform.position), Flat(bucket)), Is.LessThanOrEqualTo(1.6f), $"state {pig.State}");
            Assert.That(pig.Threat, Is.LessThan(pig.Personality.AlertThreshold));
        }

        [UnityTest]
        public IEnumerator Carry_PigHeldByOneBreaksFree_HeldByTwoNever()
        {
            var pig = FindPig();
            var carry = pig.GetComponent<CritterCarryable>();
            carry.OnGrabbed(0);
            Assert.That(pig.State, Is.EqualTo(CritterState.Carried));
            carry.OnGrabbed(1);
            for (int i = 0; i < 50; i++) Assert.That(carry.Struggle(0f), Is.False, "two holders = secure");
            carry.OnReleased(1);
            Assert.That(carry.Struggle(0.2f), Is.True, "0.2 < 0.25 break chance");
            Assert.That(pig.State, Is.EqualTo(CritterState.Stunned));
            yield return new WaitForSeconds(2f);
            Assert.That(pig.State, Is.Not.EqualTo(CritterState.Stunned));
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
