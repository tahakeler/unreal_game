using HarvestPanic.Core;
using HarvestPanic.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace HarvestPanic.Tests.EditMode.Player
{
    /// <summary>Pins the GDD examples in physics-carry, player-character and proximity-voice.</summary>
    public class PlayerLogicTests
    {
        [Test] public void CarrySpeed_GddExamples()
        {
            Assert.That(CarryMath.CarrySpeed(5f, 1), Is.EqualTo(0.9f).Within(1e-4));    // chicken
            Assert.That(CarryMath.CarrySpeed(300f, 8), Is.EqualTo(0.35f).Within(1e-4)); // cow floor
        }

        [Test] public void SpeedMultiplier_GroupFactorClampsUnderCrewedLoads()
        {
            Assert.That(CarryMath.GroupFactor(2, 6), Is.EqualTo(0.35f).Within(1e-4));
            Assert.That(CarryMath.GroupFactor(4, 6), Is.EqualTo(4f / 6f).Within(1e-4));
            Assert.That(CarryMath.GroupFactor(6, 6), Is.EqualTo(1f));
            Assert.That(CarryMath.SpeedMultiplier(12f, 2, 2), Is.EqualTo(0.88f).Within(1e-4));
            Assert.That(CarryMath.SpeedMultiplier(10f, 0, 2), Is.EqualTo(1f));
            Assert.That(CarryMath.SpeedMultiplier(300f, 2, 6), Is.EqualTo(0.35f).Within(1e-4));
        }

        [Test] public void LoadRatio_IsClamped()
        {
            Assert.That(CarryMath.LoadRatio(25f, 1), Is.EqualTo(1f));
            Assert.That(CarryMath.LoadRatio(100f, 1), Is.EqualTo(1f));
            Assert.That(CarryMath.LoadRatio(10f, 2), Is.EqualTo(0.2f).Within(1e-4));
            Assert.That(CarryMath.LoadRatio(10f, 0), Is.EqualTo(0f));
        }

        [Test] public void ThrowSpeed_GddExamples()
        {
            Assert.That(CarryMath.ThrowSpeed(1f, 2f), Is.EqualTo(14f).Within(1e-4));   // turnip
            Assert.That(CarryMath.ThrowSpeed(1f, 40f), Is.EqualTo(4.2f).Within(1e-4)); // pig
            Assert.That(CarryMath.ThrowSpeed(0f, 2f), Is.EqualTo(4f).Within(1e-4));
            Assert.That(CarryMath.ThrowCharge(0.5f), Is.EqualTo(0.5f).Within(1e-4));
            Assert.That(CarryMath.ThrowCharge(3f), Is.EqualTo(1f));
        }

        [Test] public void CanThrow_HeaveRule()
        {
            Assert.That(CarryMath.CanThrow(1, 2, 2, 40f), Is.True);
            Assert.That(CarryMath.CanThrow(1, 2, 2, 41f), Is.False);
            Assert.That(CarryMath.CanThrow(2, 4, 4, 10f), Is.False);
            Assert.That(CarryMath.CanThrow(1, 1, 1, 70f), Is.True);
            Assert.That(CarryMath.HandsContributed(6, 4), Is.EqualTo(2));
            Assert.That(CarryMath.HandsContributed(1, 0), Is.EqualTo(1));
        }

        [Test] public void RagdollThreshold_RangeAndKnockout()
        {
            Assert.That(CarryMath.RagdollThreshold(900f, 0f, 100f), Is.EqualTo(900f).Within(1e-3));
            Assert.That(CarryMath.RagdollThreshold(900f, 1f, 10f, 0.8f), Is.EqualTo(302.4f).Within(1e-2));
            Assert.That(CarryMath.IsKnockoutImpulse(1800f, 900f), Is.True);
            Assert.That(CarryMath.IsKnockoutImpulse(1799f, 900f), Is.False);
        }

        [Test] public void KnockoutTracker_ThirdRagdollInWindow()
        {
            var k = new KnockoutTracker(3, 20f);
            Assert.That(k.RegisterRagdoll(0f), Is.False);
            Assert.That(k.RegisterRagdoll(5f), Is.False);
            Assert.That(k.RegisterRagdoll(19f), Is.True);
            Assert.That(k.RegisterRagdoll(30f), Is.False);
            Assert.That(k.RegisterRagdoll(31f), Is.False);
            Assert.That(k.RegisterRagdoll(60f), Is.False); // first two expired
        }

        [Test] public void Stamina_DrainRegenAndExhaustion()
        {
            var t = ScriptableObject.CreateInstance<PlayerTuning>();
            var s = new StaminaModel(t);
            for (int i = 0; i < 70; i++) s.Tick(0.1f, true, 0f); // 7 s * 15 = 105
            Assert.That(s.Value, Is.EqualTo(0f));
            Assert.That(s.CanSprint, Is.False);
            s.Tick(0.5f, false, 0f);
            Assert.That(s.Value, Is.EqualTo(0f), "regen waits 1 s");
            for (int i = 0; i < 10; i++) s.Tick(0.1f, false, 0f); // passes delay, starts regen
            Assert.That(s.Value, Is.GreaterThan(0f).And.LessThan(30f));
            Assert.That(s.CanSprint, Is.False);
            for (int i = 0; i < 20; i++) s.Tick(0.1f, false, 0f);
            Assert.That(s.CanSprint, Is.True);
            float before = s.Value;
            s.Tick(1f, false, 1f); // full load drains 8/s
            Assert.That(s.Value, Is.EqualTo(before - 8f).Within(1e-3));
            Object.DestroyImmediate(t);
        }

        [Test] public void StateMachine_TransitionTable()
        {
            Assert.That(PlayerStateMachine.CanTransition(PlayerState.Normal, PlayerState.Ragdoll), Is.True);
            Assert.That(PlayerStateMachine.CanTransition(PlayerState.Ragdoll, PlayerState.KnockedOut), Is.True);
            Assert.That(PlayerStateMachine.CanTransition(PlayerState.KnockedOut, PlayerState.Ragdoll), Is.False);
            Assert.That(PlayerStateMachine.CanTransition(PlayerState.Respawning, PlayerState.Ragdoll), Is.False);
            Assert.That(PlayerStateMachine.CanTransition(PlayerState.Normal, PlayerState.Normal), Is.False);
        }

        [Test] public void SlipTracker_BreaksAfterSlipTime()
        {
            var s = new SlipTracker(1.2f, 0.3f);
            Assert.That(s.Tick(1.3f, 0.2f), Is.False);
            Assert.That(s.Tick(1.0f, 0.2f), Is.False); // reset
            Assert.That(s.Tick(1.3f, 0.2f), Is.False);
            Assert.That(s.Tick(1.3f, 0.2f), Is.True);
        }

        [Test] public void TouchLog_NewestFirstWindowAndVerbs()
        {
            var log = new TouchLog(3f, 3, 2f);
            log.Handle(1, HandlingMode.Held, 0f);
            log.Touch(2, 0.5f);
            log.Handle(3, HandlingMode.Thrown, 1f);
            var buf = new ulong[4];
            Assert.That(log.Snapshot(1f, buf), Is.EqualTo(3));
            Assert.That(buf[0], Is.EqualTo(3UL));
            Assert.That(buf[2], Is.EqualTo(1UL));
            Assert.That(log.Instigator(1.5f), Is.EqualTo(3UL));
            Assert.That(log.VerbForImpact(), Is.EqualTo(BlameVerb.Threw));
            Assert.That(log.IsReporting(2.9f), Is.True);
            Assert.That(log.IsReporting(3.1f), Is.False);
            Assert.That(log.Instigator(10f), Is.EqualTo(PlayerIds.None));
        }

        [Test] public void Noise_RadiusRateLimitAndCap()
        {
            Assert.That(NoiseBroadcaster.RadiusFor(0f), Is.EqualTo(4f).Within(1e-3));
            Assert.That(NoiseBroadcaster.RadiusFor(1f), Is.EqualTo(50f).Within(1e-3));
            Assert.That(NoiseBroadcaster.RadiusFor(0.5f), Is.EqualTo(4f + 46f * Mathf.Pow(0.5f, 1.5f)).Within(1e-3));

            var b = new NoiseBroadcaster(globalCap: 3);
            int raised = 0;
            b.OnNoise += _ => raised++;
            Assert.That(b.TryEmit(NoiseSource.Shout, 0.8f, Vector3.zero, 7, 0, NoiseTag.None, 0.0, 1f, out var e), Is.True);
            Assert.That(e.Radius, Is.EqualTo(NoiseBroadcaster.RadiusFor(0.8f)).Within(1e-3));
            Assert.That(b.TryEmit(NoiseSource.Shout, 0.5f, Vector3.zero, 7, 0, NoiseTag.None, 0.1, 1f, out _), Is.False, "quieter in window");
            Assert.That(b.TryEmit(NoiseSource.Shout, 0.9f, Vector3.zero, 7, 0, NoiseTag.None, 0.1, 1f, out _), Is.True, "louder replaces");
            Assert.That(b.TryEmit(NoiseSource.Impact, 0.3f, Vector3.zero, 8, 0, NoiseTag.None, 0.2, 0.5f, out var half), Is.True);
            Assert.That(half.Radius, Is.EqualTo(NoiseBroadcaster.RadiusFor(0.3f) * 0.5f).Within(1e-3));
            Assert.That(b.TryEmit(NoiseSource.Impact, 0.3f, Vector3.zero, 9, 0, NoiseTag.None, 0.3, 1f, out _), Is.False, "global cap");
            Assert.That(b.TryEmit(NoiseSource.Impact, 0.3f, Vector3.zero, 9, 0, NoiseTag.None, 1.5, 1f, out _), Is.True);
            Assert.That(raised, Is.EqualTo(4));
        }
    }
}
