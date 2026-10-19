using System;
using HarvestPanic.Core;
using NUnit.Framework;
using UnityEditorInternal;
using UnityEngine;

namespace HarvestPanic.Tests.EditMode
{
    /// <summary>Compiles against every Core contract and checks their invariants and the project layer/tag setup.</summary>
    public class ContractsTests
    {
        sealed class StubCarry : ICarryable
        {
            public Transform CarryTransform => null;
            public int HandsRequired => 2;
            public float MassKg => 40f;
            public bool IsKinematicCarry => false;
            public ulong Holder = PlayerIds.None;
            public Vector3 Thrown;
            public void OnGrabbed(ulong clientId) => Holder = clientId;
            public void OnReleased(ulong clientId) => Holder = PlayerIds.None;
            public void ApplyThrow(Vector3 velocity, ulong thrower) => Thrown = velocity;
        }

        sealed class StubNoise : INoiseService
        {
            public event Action<NoiseEvent> OnNoise;
            public bool Emit(NoiseSource s, float l, Vector3 p, ulong i, ulong b, NoiseTag t)
            {
                OnNoise?.Invoke(new NoiseEvent { Source = s, Loudness = l, Radius = 20f, Location = p, Tags = t });
                return true;
            }
        }

        sealed class StubKnockable : MonoBehaviour, IKnockable
        {
            public bool IsDown { get; private set; }
            public void ApplyImpulse(Vector3 impulse, ulong instigator, BlameVerb verb) => IsDown = true;
            public void SendToBarn(BlameVerb reason, bool chargeFee) => IsDown = false;
        }

        [Test]
        public void ImpactUnits_ConvertsBothWays()
        {
            Assert.That(ImpactUnits.ToDesign(40f), Is.EqualTo(400f));
            Assert.That(ImpactUnits.ToUnity(ImpactUnits.ToDesign(7f)), Is.EqualTo(7f).Within(1e-5f));
        }

        [Test]
        public void Knockables_FindsInterfaceOnParent()
        {
            var root = new GameObject("p");
            var child = new GameObject("c");
            child.transform.SetParent(root.transform);
            var k = root.AddComponent<StubKnockable>();
            try
            {
                Assert.That(Knockables.TryGetFor(child, out var found), Is.True);
                found.ApplyImpulse(Vector3.up, 1, BlameVerb.KnockedOut);
                Assert.That(k.IsDown, Is.True);
                Assert.That(Knockables.TryGetFor(null, out _), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        sealed class StubFighter : ICarryableEvents
        {
            public event Action<ulong[]> OnForcedRelease;
            public event Action<Vector3> OnStruggle;
            public void Break(ulong[] ids) => OnForcedRelease?.Invoke(ids);
            public void Wriggle(Vector3 v) => OnStruggle?.Invoke(v);
        }

        [Test]
        public void CarryableEvents_AreOptionalAndRaiseToSubscribers()
        {
            object plain = new StubCarry();
            Assert.That(plain is ICarryableEvents, Is.False, "plain carryables stay valid without the events");
            var f = new StubFighter();
            ulong[] released = null; var push = Vector3.zero;
            f.OnForcedRelease += ids => released = ids;
            f.OnStruggle += v => push = v;
            f.Break(new ulong[] { 1, 2 }); f.Wriggle(Vector3.right * 120f);
            Assert.That(released.Length, Is.EqualTo(2));
            Assert.That(push.x, Is.EqualTo(120f));
        }

        sealed class Zone : IMovementModifierSource
        {
            public float Mult;
            public float GetSpeedMultiplier(Vector3 position) => position.x > 0 ? Mult : 1f;
        }

        sealed class Soakable : IGoodsSoakable
        {
            public ulong Last;
            public void ApplySoak(ulong instigator) => Last = instigator;
        }

        [Test]
        public void MovementModifiers_MultiplyAllSources()
        {
            MovementModifiers.Clear();
            var a = new Zone { Mult = 0.5f }; var b = new Zone { Mult = 0.8f };
            MovementModifiers.Register(a); MovementModifiers.Register(a); MovementModifiers.Register(b);
            Assert.That(MovementModifiers.Evaluate(Vector3.right), Is.EqualTo(0.4f).Within(1e-5f));
            Assert.That(MovementModifiers.Evaluate(Vector3.left), Is.EqualTo(1f));
            MovementModifiers.Unregister(a);
            Assert.That(MovementModifiers.Evaluate(Vector3.right), Is.EqualTo(0.8f).Within(1e-5f));
            MovementModifiers.Clear();
        }

        [Test]
        public void DayParamsAndSoak_ContractsCompile()
        {
            Assert.That(DayParams.MudSpeedMult, Is.EqualTo("Mud.SpeedMult"));
            Assert.That(DayParams.BridgeSafeLoadMult, Is.EqualTo("Bridge.SafeLoadMult"));
            Assert.That(DayParams.BridgePlankBreakMult, Is.EqualTo("Bridge.PlankBreakMult"));
            var s = new Soakable();
            ((IGoodsSoakable)s).ApplySoak(PlayerIds.Nature);
            Assert.That(s.Last, Is.EqualTo(PlayerIds.Nature));
            Assert.That(BruiseKind.Soaked, Is.Not.EqualTo(BruiseKind.Impact));
        }

        [TearDown] public void TearDown() => Services.Clear();

        [Test]
        public void Carryable_StubFollowsGrabReleaseThrowContract()
        {
            var c = new StubCarry();
            c.OnGrabbed(3);
            Assert.That(c.Holder, Is.EqualTo(3UL));
            c.ApplyThrow(Vector3.forward * 4f, 3);
            Assert.That(c.Thrown.z, Is.EqualTo(4f));
            c.OnReleased(3);
            Assert.That(PlayerIds.IsPlayer(c.Holder), Is.False);
        }

        [Test]
        public void Services_RegisterGetUnregisterAndWhenAvailable()
        {
            Assert.That(Services.TryGet(out INoiseService _), Is.False);
            INoiseService got = null;
            Services.WhenAvailable<INoiseService>(s => got = s);
            var noise = new StubNoise();
            Services.Register<INoiseService>(noise);
            Assert.That(got, Is.SameAs(noise));
            Assert.That(Services.Get<INoiseService>(), Is.SameAs(noise));
            Services.Unregister<INoiseService>(new StubNoise());
            Assert.That(Services.TryGet(out INoiseService _), Is.True, "unregister must ignore a non-owner");
            Services.Unregister<INoiseService>(noise);
            Assert.Throws<InvalidOperationException>(() => Services.Get<INoiseService>());
        }

        [Test]
        public void Noise_PerceivedStrengthFallsOffLinearlyAndZeroOutOfRange()
        {
            var e = new NoiseEvent { Loudness = 0.8f, Radius = 20f };
            Assert.That(e.PerceivedStrength(0f), Is.EqualTo(0.8f).Within(1e-5f));
            Assert.That(e.PerceivedStrength(10f), Is.EqualTo(0.4f).Within(1e-5f));
            Assert.That(e.PerceivedStrength(21f), Is.EqualTo(0f));
            var heard = default(NoiseEvent);
            var svc = new StubNoise();
            svc.OnNoise += n => heard = n;
            svc.Emit(NoiseSource.Shout, 1f, Vector3.one, 0, PlayerIds.None, NoiseTag.Scream | NoiseTag.Squeal);
            Assert.That(heard.Tags.HasFlag(NoiseTag.Scream), Is.True);
        }

        [Test]
        public void BlameVerb_HasTwentyThreeVerbs()
        {
            Assert.That(Enum.GetValues(typeof(BlameVerb)).Length, Is.EqualTo(23));
            Assert.That(Enum.IsDefined(typeof(BlameVerb), "LassoedIntoRiver"), Is.True);
        }

        [Test]
        public void TouchHistory_KeepsNewestFirstWithinWindow()
        {
            var h = new TouchHistory(3f, 3);
            h.Record(1, 0f); h.Record(2, 1f); h.Record(3, 2f); h.Record(1, 2.5f);
            var buf = new ulong[4];
            int n = h.Snapshot(2.5f, buf);
            Assert.That(n, Is.EqualTo(3));
            Assert.That(new[] { buf[0], buf[1], buf[2] }, Is.EqualTo(new ulong[] { 1, 3, 2 }));
            Assert.That(h.Snapshot(4.5f, buf), Is.EqualTo(2), "player 2 (t=1) expired; players 1 and 3 remain");
            Assert.That(h.LastToucher(10f), Is.EqualTo(PlayerIds.None));
        }

        [Test]
        public void GoodsDefinition_DefaultsAndCarriersNeeded()
        {
            var g = ScriptableObject.CreateInstance<GoodsDefinition>();
            try
            {
                Assert.That(g.CarryHands, Is.EqualTo(1));
                Assert.That(g.CarriersNeeded, Is.EqualTo(1));
                Assert.That(g.Id, Is.EqualTo(g.GoodsId));
            }
            finally { UnityEngine.Object.DestroyImmediate(g); }
        }

        [Test]
        public void CritterPersonality_DefaultsMatchContract()
        {
            var p = ScriptableObject.CreateInstance<CritterPersonality>();
            try
            {
                Assert.That(p.FleeThreshold, Is.EqualTo(0.55f));
                Assert.That(p.CorneredResponse, Is.EqualTo(CorneredResponse.Juke));
                Assert.That(p.GetNoiseSourceMult(NoiseSource.Voice), Is.EqualTo(1f));
                Assert.That(p.TryGetNoiseResponse(NoiseTag.Whistle, out _), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(p); }
        }

        [Test]
        public void DayModifier_AssetCreatesWithUniqueDefaults()
        {
            var m = ScriptableObject.CreateInstance<DayModifier>();
            try
            {
                Assert.That(m.StackRule, Is.EqualTo(DayModifierStackRule.Unique));
                Assert.That(m.Announced, Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(m); }
        }

        [Test]
        public void MarkerTag_ParsesSpawnerStringAndMapsEveryRoleToDefinedTag()
        {
            Assert.That(MarkerTag.TryParseSpawner("HP_CritterSpawner_Pig:3", out var sp, out var n), Is.True);
            Assert.That((sp, n), Is.EqualTo(("Pig", 3)));
            Assert.That(MarkerTag.TryParseSpawner("HP_Goods_Crate", out _, out _), Is.False);
            Assert.That(MarkerTag.FormatSpawner("Chicken", 8), Is.EqualTo("HP_CritterSpawner_Chicken:8"));
            var defined = new System.Collections.Generic.HashSet<string>(InternalEditorUtility.tags);
            foreach (MarkerRole r in Enum.GetValues(typeof(MarkerRole)))
                Assert.That(defined.Contains(MarkerTag.ForRole(r)), $"Tag for {r} missing from TagManager");
        }

        [Test]
        public void LevelMarker_FindAllReturnsOrderedByIndex()
        {
            var a = new GameObject("a").AddComponent<LevelMarker>();
            var b = new GameObject("b").AddComponent<LevelMarker>();
            try
            {
                a.Configure(MarkerRole.BridgePlank, orderIndex: 2);
                b.Configure(MarkerRole.BridgePlank, orderIndex: 1);
                var found = LevelMarker.FindAll(MarkerRole.BridgePlank);
                Assert.That(found.Count, Is.EqualTo(2));
                Assert.That(found[0], Is.SameAs(b));
            }
            finally { UnityEngine.Object.DestroyImmediate(a.gameObject); UnityEngine.Object.DestroyImmediate(b.gameObject); }
        }

        [Test]
        public void Layers_AreDefinedAndCollisionMatrixIsSensible()
        {
            foreach (var n in new[] { GameLayers.Player, GameLayers.Critter, GameLayers.Carryable, GameLayers.Bridge,
                                      GameLayers.Interactable, GameLayers.Ragdoll, GameLayers.Water })
                Assert.That(GameLayers.IndexOf(n), Is.GreaterThanOrEqualTo(0), n);
            int pl = GameLayers.IndexOf(GameLayers.Player), ca = GameLayers.IndexOf(GameLayers.Carryable);
            int cr = GameLayers.IndexOf(GameLayers.Critter), ra = GameLayers.IndexOf(GameLayers.Ragdoll);
            int ia = GameLayers.IndexOf(GameLayers.Interactable), br = GameLayers.IndexOf(GameLayers.Bridge);
            Assert.That(Physics.GetIgnoreLayerCollision(pl, ca), Is.False);
            Assert.That(Physics.GetIgnoreLayerCollision(ra, br), Is.False);
            Assert.That(Physics.GetIgnoreLayerCollision(ca, ia), Is.False);
            Assert.That(Physics.GetIgnoreLayerCollision(cr, ia), Is.False);
            Assert.That(Physics.GetIgnoreLayerCollision(pl, ra), Is.True);
            Assert.That(Physics.GetIgnoreLayerCollision(ra, ra), Is.True);
            Assert.That(Physics.GetIgnoreLayerCollision(ia, ia), Is.True);
        }
    }
}
