using System.Collections.Generic;
using HarvestPanic.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace HarvestPanic.Tests.EditMode.Player
{
    /// <summary>First-person arms math and the centre-HUD prompt parser (player-character 5b, 5f).</summary>
    public class FirstPersonPresentationTests
    {
        [Test] public void Ik_ReachableTarget_KeepsBoneLengths()
        {
            var s = new Vector3(-0.2f, -0.3f, 0f);
            var target = new Vector3(-0.2f, -0.25f, 0.45f);
            var elbow = ArmMath.SolveTwoBone(s, target, 0.3f, 0.32f, Vector3.down, 1.5f, out var wrist, out float stretch);
            Assert.That(stretch, Is.EqualTo(1f));
            Assert.That(Vector3.Distance(wrist, target), Is.LessThan(1e-4f));
            Assert.That(Vector3.Distance(s, elbow), Is.EqualTo(0.3f).Within(1e-4));
            Assert.That(Vector3.Distance(elbow, wrist), Is.EqualTo(0.32f).Within(1e-4));
            Assert.That(elbow.y, Is.LessThan(Mathf.Min(s.y, target.y)), "elbow bends toward the pole (down)");
        }

        [Test] public void Ik_FarTarget_StretchesUpToLimit()
        {
            var s = Vector3.zero;
            ArmMath.SolveTwoBone(s, new Vector3(0f, 0f, 0.8f), 0.3f, 0.3f, Vector3.down, 1.5f, out var w1, out float st1);
            Assert.That(st1, Is.GreaterThan(1f).And.LessThanOrEqualTo(1.5f));
            Assert.That(w1.z, Is.EqualTo(0.8f).Within(0.01f));
            ArmMath.SolveTwoBone(s, new Vector3(0f, 0f, 5f), 0.3f, 0.3f, Vector3.down, 1.5f, out var w2, out float st2);
            Assert.That(st2, Is.EqualTo(1.5f).Within(1e-4));
            Assert.That(w2.z, Is.LessThan(0.91f), "never reaches past the stretch limit");
        }

        [Test] public void PoseSelection_FollowsCarryState()
        {
            Assert.AreEqual(ArmPose.Idle, ArmMath.Select(false, false, 0, false, false));
            Assert.AreEqual(ArmPose.Reach, ArmMath.Select(false, false, 0, true, false));
            Assert.AreEqual(ArmPose.GripOneHand, ArmMath.Select(true, false, 1, false, false));
            Assert.AreEqual(ArmPose.GripTwoHands, ArmMath.Select(true, false, 2, false, false));
            Assert.AreEqual(ArmPose.Tool, ArmMath.Select(true, true, 1, false, false));
            Assert.AreEqual(ArmPose.ThrowSwing, ArmMath.Select(false, false, 0, true, true));
            Assert.That(ArmMath.Curl(ArmPose.Tool), Is.GreaterThan(ArmMath.Curl(ArmPose.Reach)));
        }

        [Test] public void SideGrip_PalmsOutsideTheFaces_Symmetric()
        {
            ArmMath.SideGrip(new Vector3(0f, -0.4f, 1f), new Vector3(0.3f, 0.3f, 0.3f), -0.5f, 0.02f, 0.42f, out var l, out var r);
            Assert.That(l.x, Is.EqualTo(-0.32f).Within(1e-4));
            Assert.That(r.x, Is.EqualTo(0.32f).Within(1e-4));
            Assert.That(l.y, Is.EqualTo(-0.25f).Within(1e-4), "negative GripLow grips above centre");
            Assert.That(l.z, Is.LessThan(1f), "near half of the box");
            ArmMath.SideGrip(Vector3.zero, new Vector3(2f, 0.3f, 0.3f), 0f, 0.02f, 0.42f, out l, out r);
            Assert.That(r.x, Is.EqualTo(0.42f), "very wide items clamp the spread");
        }

        [Test] public void SwingCurve_BellShaped()
        {
            Assert.That(ArmMath.SwingCurve(0f, 0.3f), Is.EqualTo(0f));
            Assert.That(ArmMath.SwingCurve(0.105f, 0.3f), Is.EqualTo(1f).Within(1e-3));
            Assert.That(ArmMath.SwingCurve(0.3f, 0.3f), Is.EqualTo(0f));
        }

        [Test] public void Prompt_SplitsIntoKeyCaps()
        {
            var list = new List<PromptAction>();
            PromptFormat.Parse("E: Grab (0/2 hands)", list);
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual("E", list[0].Key);
            Assert.AreEqual("Grab (0/2 hands)", list[0].Label);

            PromptFormat.Parse("E: Drop  LMB: Throw  RMB: Shake (7 left)  [40%]", list);
            Assert.AreEqual(4, list.Count);
            Assert.AreEqual("LMB", list[1].Key);
            Assert.AreEqual("Shake (7 left)", list[2].Label);
            Assert.AreEqual("", list[3].Key);
            Assert.AreEqual("[40%]", list[3].Label);

            PromptFormat.Parse("Tap E: Carry  Hold E: Revive", list);
            Assert.AreEqual("Tap E", list[0].Key);
            Assert.AreEqual("Hold E", list[1].Key);

            PromptFormat.Parse("Empty - refill at the barn", list);
            Assert.AreEqual("", list[0].Key);
            PromptFormat.Parse("", list);
            Assert.AreEqual(0, list.Count);
        }
    }
}
