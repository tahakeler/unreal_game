using System.Linq;
using HarvestPanic.Core;
using HarvestPanic.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HarvestPanic.Tests.EditMode.Player
{
    /// <summary>
    /// Pins the GDD numbers of the lasso, whistle, wagon and toolbelt (design/gdd/farm-tools.md, wagon-vehicles.md,
    /// physics-carry.md rule 7, GDD 5.8-5.10).
    /// </summary>
    public class ToolsLogicTests
    {
        // ---------------------------------------------------------------------------------------- lasso: targets / range

        [Test] public void Lasso_TargetRules_MediumCrittersGoodsAndKnockedOutTeammatesOnly()
        {
            Assert.That(LassoMath.Evaluate(LassoTargetKind.Critter, SizeClass.Medium, false, false, 8f, 12f), Is.EqualTo(LassoVerdict.Hit), "pig");
            Assert.That(LassoMath.Evaluate(LassoTargetKind.Critter, SizeClass.Large, false, false, 8f, 12f), Is.EqualTo(LassoVerdict.Hit), "cow");
            Assert.That(LassoMath.Evaluate(LassoTargetKind.Critter, SizeClass.Small, false, false, 3f, 12f), Is.EqualTo(LassoVerdict.TooSmall), "chickens are net targets");
            Assert.That(LassoMath.Evaluate(LassoTargetKind.Goods, SizeClass.Tiny, false, false, 5f, 12f), Is.EqualTo(LassoVerdict.Hit), "goods: tow rope");
            Assert.That(LassoMath.Evaluate(LassoTargetKind.Player, SizeClass.Medium, false, false, 5f, 12f), Is.EqualTo(LassoVerdict.AwakePlayer), "awake friend: the loop bounces");
            Assert.That(LassoMath.Evaluate(LassoTargetKind.Player, SizeClass.Medium, true, false, 5f, 12f), Is.EqualTo(LassoVerdict.Hit), "knocked-out friend");
            Assert.That(LassoMath.Evaluate(LassoTargetKind.Player, SizeClass.Medium, true, true, 0f, 12f), Is.EqualTo(LassoVerdict.Self));
            Assert.That(LassoMath.Evaluate(LassoTargetKind.Goods, SizeClass.Medium, false, false, 2f, 12f, SizeClass.Medium, true), Is.EqualTo(LassoVerdict.Held));
            Assert.That(LassoMath.Evaluate(LassoTargetKind.None, SizeClass.Medium, false, false, 2f, 12f), Is.EqualTo(LassoVerdict.Nothing));
        }

        [Test] public void Lasso_Range_Is12mPerGdd()
        {
            var t = ScriptableObject.CreateInstance<ToolTuning>();
            Assert.That(t.LassoRange, Is.EqualTo(12f));
            Assert.That(LassoMath.Evaluate(LassoTargetKind.Critter, SizeClass.Medium, false, false, 12f, t.LassoRange), Is.EqualTo(LassoVerdict.Hit));
            Assert.That(LassoMath.Evaluate(LassoTargetKind.Critter, SizeClass.Medium, false, false, 12.01f, t.LassoRange), Is.EqualTo(LassoVerdict.OutOfRange));
            Assert.That(LassoMath.FlightTime(12f, t.LassoLoopSpeed), Is.EqualTo(0.5f).Within(1e-4));
            Object.DestroyImmediate(t);
        }

        [Test] public void Lasso_TuningDefaults_MatchGdd()
        {
            var t = ScriptableObject.CreateInstance<ToolTuning>();
            Assert.That(t.LassoLength, Is.EqualTo(6f));
            Assert.That(t.HitchLength, Is.EqualTo(3f));
            Assert.That(t.RopeBreakForce, Is.EqualTo(4000f));
            Assert.That(t.PlayerAnchorBase, Is.EqualTo(600f));
            Assert.That(t.LassoStaminaPerSecond, Is.EqualTo(10f));
            Assert.That(t.LassoAutoGrab, Is.EqualTo(1.5f));
            Assert.That(t.LassoMinSize, Is.EqualTo(SizeClass.Medium));
            Assert.That(t.EquipSeconds, Is.EqualTo(0.4f));
            Object.DestroyImmediate(t);
        }

        // ---------------------------------------------------------------------------------------- lasso: tug-of-war

        [Test] public void Lasso_PlayerAnchor_Formula()
        {
            Assert.That(LassoMath.PlayerAnchor(600f, 80f, 0, RopeGround.Grass), Is.EqualTo(600f).Within(1e-3));
            Assert.That(LassoMath.PlayerAnchor(600f, 0f, 0, RopeGround.Grass), Is.EqualTo(240f).Within(1e-3), "exhausted x0.4");
            Assert.That(LassoMath.PlayerAnchor(600f, 80f, 0, RopeGround.Mud), Is.EqualTo(360f).Within(1e-3));
            Assert.That(LassoMath.PlayerAnchor(600f, 80f, 0, RopeGround.Plank), Is.EqualTo(480f).Within(1e-3));
            Assert.That(LassoMath.PlayerAnchor(600f, 80f, 2, RopeGround.Grass), Is.EqualTo(1800f).Within(1e-3), "three on the rope");
        }

        [Test] public void Lasso_TugOfWar_GddCases()
        {
            float one = LassoMath.PlayerAnchor(600f, 100f, 0, RopeGround.Grass);
            Assert.That(LassoMath.IsDragged(500f, one), Is.False, "one player can hold a pig");
            Assert.That(LassoMath.IsDragged(900f, one), Is.True, "a cow drags one player");
            Assert.That(LassoMath.IsDragged(1600f, LassoMath.PlayerAnchor(600f, 100f, 1, RopeGround.Grass)), Is.True, "two can't hold a bull");
            Assert.That(LassoMath.IsDragged(1600f, LassoMath.PlayerAnchor(600f, 100f, 2, RopeGround.Grass)), Is.False, "it takes three to hold a bull on grass");
            Assert.That(LassoMath.IsDragged(500f, LassoMath.PlayerAnchor(600f, 100f, 0, RopeGround.Mud)), Is.True, "pig water-skis you through the mud");
        }

        [Test] public void Lasso_GroundClass()
        {
            Assert.That(LassoMath.GroundFor(true, 1f), Is.EqualTo(RopeGround.Plank));
            Assert.That(LassoMath.GroundFor(false, 0.6f), Is.EqualTo(RopeGround.Mud));
            Assert.That(LassoMath.GroundFor(false, 0.6f * 0.9f), Is.EqualTo(RopeGround.Mud), "mud in a storm");
            Assert.That(LassoMath.GroundFor(false, 1f), Is.EqualTo(RopeGround.Grass));
            Assert.That(LassoMath.GroundFor(false, 0.92f), Is.EqualTo(RopeGround.Grass), "rain's global ground slow-down is not mud");
        }

        [Test] public void Lasso_ReelAutoGrabAndSnap()
        {
            Assert.That(LassoMath.Reel(6f, 2f, 1f, 1.35f), Is.EqualTo(4f).Within(1e-4));
            Assert.That(LassoMath.Reel(1.5f, 2f, 1f, 1.35f), Is.EqualTo(1.35f).Within(1e-4), "never below the minimum");
            Assert.That(LassoMath.ShouldAutoGrab(1.5f, 1.5f), Is.True);
            Assert.That(LassoMath.ShouldAutoGrab(1.6f, 1.5f), Is.False);
            Assert.That(LassoMath.RopeSnaps(6f, 6f, 5f, 3999f, 4000f), Is.False);
            Assert.That(LassoMath.RopeSnaps(6f, 6f, 5f, 4001f, 4000f), Is.True, "4000 N break force");
            Assert.That(LassoMath.RopeSnaps(11.5f, 6f, 5f, 0f, 4000f), Is.True, "stretched far past its length");
        }

        [Test] public void Lasso_RopeClampsOnTheGroundPlane()
        {
            var p = LassoMath.ClampToRopeFlat(Vector3.zero, new Vector3(10f, 0.5f, 0f), 6f);
            Assert.That(p.x, Is.EqualTo(6f).Within(1e-4));
            Assert.That(p.y, Is.EqualTo(0.5f).Within(1e-4), "keeps the target's height");
            var inside = new Vector3(3f, 1f, 2f);
            Assert.That(LassoMath.ClampToRopeFlat(Vector3.zero, inside, 6f), Is.EqualTo(inside), "slack rope: free");
            Assert.That(LassoMath.FlatDistance(new Vector3(0, 5, 0), new Vector3(3, 0, 4)), Is.EqualTo(5f).Within(1e-4));
            Assert.That(LassoMath.Sag(6f, 2f), Is.GreaterThan(LassoMath.Sag(6f, 5.9f)), "slack hangs lower");
            Assert.That(LassoMath.LoopArc(Vector3.zero, Vector3.forward * 10f, 0.5f, 2f).y, Is.EqualTo(2f).Within(1e-4), "arc apex");
        }

        // ---------------------------------------------------------------------------------------- whistle

        static CritterPersonality Critter(string id)
        {
            Assert.That(Content.Critters.TryGet(id, out var p), Is.True, id + " asset missing");
            return p;
        }

        [Test] public void Whistle_ShortBlow_ReactionsFromRealPersonalities()
        {
            Assert.That(WhistleMath.ReactionFor(Critter("Critter.Chicken"), WhistleBlow.Short), Is.EqualTo(WhistleReaction.Away), "chickens scatter");
            Assert.That(WhistleMath.ReactionFor(Critter("Critter.Pig"), WhistleBlow.Short), Is.EqualTo(WhistleReaction.Away), "pigs flee");
            Assert.That(WhistleMath.ReactionFor(Critter("Critter.Cow"), WhistleBlow.Short), Is.EqualTo(WhistleReaction.None), "cows ignore it");
            Assert.That(WhistleMath.ReactionFor(Critter("Critter.Goat"), WhistleBlow.Short), Is.EqualTo(WhistleReaction.None), "goats ignore it");
        }

        [Test] public void Whistle_LongBlow_RalliesOnlyRallyAndLureResponders()
        {
            foreach (var id in new[] { "Critter.Chicken", "Critter.Pig", "Critter.Cow", "Critter.Goat" })
                Assert.That(WhistleMath.ReactionFor(Critter(id), WhistleBlow.Long), Is.EqualTo(WhistleReaction.None), id + " does not rally (no sheep/dog yet)");
            var sheep = ScriptableObject.CreateInstance<CritterPersonality>();
            var so = new SerializedObject(sheep);
            var arr = so.FindProperty("<NoiseTagResponses>k__BackingField");
            arr.arraySize = 1;
            var e = arr.GetArrayElementAtIndex(0);
            e.FindPropertyRelative("Tag").intValue = (int)NoiseTag.WhistleLong;
            e.FindPropertyRelative("Reaction").enumValueIndex = (int)NoiseReaction.Rally;
            e.FindPropertyRelative("Strength").floatValue = 1f;
            so.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(WhistleMath.ReactionFor(sheep, WhistleBlow.Long), Is.EqualTo(WhistleReaction.Toward), "rally");
            Assert.That(WhistleMath.ReactionFor(sheep, WhistleBlow.Short), Is.EqualTo(WhistleReaction.Away), "no Whistle entry: a loud noise scares");
            Object.DestroyImmediate(sheep);
        }

        [Test] public void Whistle_TagsClassifyCooldownAndStruggle()
        {
            Assert.That(WhistleMath.TagsFor(WhistleBlow.Short), Is.EqualTo(NoiseTag.Whistle));
            Assert.That(WhistleMath.TagsFor(WhistleBlow.Long), Is.EqualTo(NoiseTag.WhistleLong), "long blow must not also carry Whistle (CritterBrain uses the first matching tag)");
            Assert.That(WhistleMath.Classify(0.3f, 1f), Is.EqualTo(WhistleBlow.Short));
            Assert.That(WhistleMath.Classify(1f, 1f), Is.EqualTo(WhistleBlow.Long));
            Assert.That(WhistleMath.Ready(2.9f, 3f), Is.False);
            Assert.That(WhistleMath.Ready(3f, 3f), Is.True);
            Assert.That(WhistleMath.BoostedStruggle(1f, 0.5f), Is.EqualTo(1.5f).Within(1e-4), "+50% struggle");
            var t = ScriptableObject.CreateInstance<ToolTuning>();
            Assert.That(t.WhistleLoudness, Is.EqualTo(0.7f));
            Assert.That(t.WhistleCooldown, Is.EqualTo(3f));
            Assert.That(t.WhistleLongSeconds, Is.EqualTo(1f));
            Assert.That(t.WhistleStruggleBoost, Is.EqualTo(0.5f));
            Assert.That(t.WhistleStruggleSeconds, Is.EqualTo(2f));
            float r = NoiseBroadcaster.RadiusFor(t.WhistleLoudness);
            Assert.That(r, Is.EqualTo(30.94f).Within(0.05f), "earshot of a 0.7 blow");
            Assert.That(WhistleMath.InEarshot(30f, r), Is.True);
            Assert.That(WhistleMath.InEarshot(31f, r), Is.False);
            Object.DestroyImmediate(t);
        }

        // ---------------------------------------------------------------------------------------- wagon

        [Test] public void Wagon_VMax_GddExamples()
        {
            Assert.That(WagonMath.VMax(1800f, 220f, WagonMath.RollingK(WagonGround.Dirt)), Is.EqualTo(5.4545f).Within(1e-3));
            Assert.That(WagonMath.VMax(1800f, 220f, WagonMath.RollingK(WagonGround.Mud)), Is.EqualTo(2.7273f).Within(1e-3));
            Assert.That(WagonMath.VMax(5000f, 120f, 1.2f), Is.EqualTo(6f), "capped");
            Assert.That(WagonMath.VMax(1800f, 220f, 1.5f), Is.GreaterThanOrEqualTo(5f), "acceptance: 2 players, 100 kg cargo, dirt >= 5 m/s");
            Assert.That(WagonMath.VMax(900f, 220f, 1.5f), Is.LessThanOrEqualTo(3f), "acceptance: 1 player <= 3 m/s");
            Assert.That(WagonMath.RollingDrag(220f, 1.5f, 5.4545f), Is.EqualTo(1800f).Within(0.5f), "drag balances the push at v_max");
            Assert.That(WagonMath.GroundFor(true, 1f), Is.EqualTo(WagonGround.Plank));
            Assert.That(WagonMath.GroundFor(false, 0.6f), Is.EqualTo(WagonGround.Mud));
        }

        [Test] public void Wagon_TipThreshold_50Degrees()
        {
            Assert.That(WagonMath.IsTipped(Quaternion.Euler(0f, 0f, 49f) * Vector3.up, 50f), Is.False);
            Assert.That(WagonMath.IsTipped(Quaternion.Euler(0f, 0f, 51f) * Vector3.up, 50f), Is.True);
            Assert.That(WagonMath.IsTipped(Quaternion.Euler(52f, 30f, 0f) * Vector3.up, 50f), Is.True, "pitch counts too");
            Assert.That(WagonMath.RightingPlayers(4), Is.EqualTo(2));
            Assert.That(WagonMath.RightingPlayers(1), Is.EqualTo(1), "solo can right it alone");
        }

        [Test] public void Wagon_CargoCapacity_SlotsAndSoftCap()
        {
            Assert.That(WagonMath.SlotCost(1), Is.EqualTo(1));
            Assert.That(WagonMath.SlotCost(2), Is.EqualTo(1), "crate / pig");
            Assert.That(WagonMath.SlotCost(6), Is.EqualTo(3), "cow");
            Assert.That(WagonMath.SlotCost(8), Is.EqualTo(4), "prize pumpkin");
            Assert.That(WagonMath.CanLoad(5, 6, 20f, 2, 400f), Is.True);
            Assert.That(WagonMath.CanLoad(6, 6, 20f, 1, 400f), Is.False, "bed full");
            Assert.That(WagonMath.CanLoad(3, 6, 20f, 8, 400f), Is.False, "pumpkin needs 4 free slots");
            Assert.That(WagonMath.CanLoad(0, 6, 399f, 8, 400f), Is.True, "soft cap: the item that crosses 400 kg still goes in");
            Assert.That(WagonMath.CanLoad(0, 6, 400f, 1, 400f), Is.False, "nothing after the soft cap");
            Assert.That(WagonMath.BounceThreshold(0.4f), Is.EqualTo(9.8f * 1.7f).Within(1e-3));
            Assert.That(WagonMath.CargoBruise(1000f, 0.5f), Is.EqualTo(500f));
        }

        [Test] public void Wagon_Slots_TakeFreeAndCentre()
        {
            var s = new WagonSlots(6);
            for (int i = 0; i < 6; i++) Assert.That(s.TryTake(1, out _), Is.True);
            Assert.That(s.TryTake(1, out _), Is.False, "seventh item");
            s.Free(2, 1);
            Assert.That(s.TryTake(1, out int f), Is.True); Assert.That(f, Is.EqualTo(2));
            s.Clear();
            Assert.That(s.TryTake(4, out int p), Is.True); Assert.That(p, Is.EqualTo(0));
            Assert.That(s.TryTake(3, out _), Is.False, "only 2 left");
            Assert.That(s.TryTake(2, out int q), Is.True); Assert.That(q, Is.EqualTo(4));
            var c0 = WagonSlots.Centre(0, 1, 6, 2, 1.2f, 2f);
            Assert.That(c0.x, Is.EqualTo(-0.3f).Within(1e-4)); Assert.That(c0.y, Is.EqualTo(2f / 3f).Within(1e-4), "front-left");
            var row = WagonSlots.Centre(0, 2, 6, 2, 1.2f, 2f);
            Assert.That(row.x, Is.EqualTo(0f).Within(1e-4));
            var back = WagonSlots.Centre(5, 1, 6, 2, 1.2f, 2f);
            Assert.That(back.y, Is.EqualTo(-2f / 3f).Within(1e-4), "back-right");
        }

        [Test] public void Wagon_HandleTowBar_PushesAndPulls()
        {
            Vector3 handle = new Vector3(0f, 1f, 0f);
            // Player 0.5 m behind a handle that wants 1 m: pushing, force away from the player (+Z).
            var push = WagonMath.HandleForce(handle, new Vector3(0f, 1f, -0.5f), Vector3.zero, 1f, 1800f, 300f, 900f);
            Assert.That(push.z, Is.GreaterThan(0f));
            // Player 2 m away: pulling, force toward the player, capped at 900 N.
            var pull = WagonMath.HandleForce(handle, new Vector3(0f, 1f, 2f), Vector3.zero, 1f, 1800f, 300f, 900f);
            Assert.That(pull.z, Is.GreaterThan(0f));
            Assert.That(pull.magnitude, Is.EqualTo(900f).Within(1e-2), "900 N per player");
            Assert.That(push.y, Is.EqualTo(0f), "never lifts");
        }

        [Test] public void Wagon_TuningDefaults_MatchGdd()
        {
            var t = ScriptableObject.CreateInstance<WagonTuning>();
            Assert.That(t.WagonMassKg, Is.EqualTo(120f));
            Assert.That(t.CargoSoftCapKg, Is.EqualTo(400f));
            Assert.That(t.PushForcePerPlayer, Is.EqualTo(900f));
            Assert.That(t.VMax, Is.EqualTo(6f));
            Assert.That(new[] { t.RollingKDirt, t.RollingKMud, t.RollingKPlank }, Is.EqualTo(new[] { 1.5f, 3f, 1.2f }));
            Assert.That(t.TipAngle, Is.EqualTo(50f));
            Assert.That(t.RightingSeconds, Is.EqualTo(2f));
            Assert.That(t.RightingPlayers, Is.EqualTo(2));
            Assert.That(t.WallHeight, Is.EqualTo(0.4f));
            Assert.That(t.BedWidth * t.BedLength, Is.EqualTo(2.4f).Within(1e-4), "bed 2.0 x 1.2 m");
            Object.DestroyImmediate(t);
        }

        // ---------------------------------------------------------------------------------------- toolbelt / input / arms

        [Test] public void Toolbelt_CyclesAndNames()
        {
            Assert.That(PlayerToolbelt.SlotCount, Is.EqualTo(5));
            Assert.That(PlayerToolbelt.Cycle(0, -1), Is.EqualTo(4));
            Assert.That(PlayerToolbelt.Cycle(4, 1), Is.EqualTo(0));
            Assert.That(PlayerToolbelt.Cycle(2, 1), Is.EqualTo(3));
            Assert.That(Enumerable.Range(0, 5).Select(PlayerToolbelt.SlotName), Is.EqualTo(new[] { "Hands", "Net", "Bucket", "Lasso", "Whistle" }));
        }

        [Test] public void Input_HotbarActionsAreBound()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Prefabs/Player/Input/PlayerControls.inputactions");
            Assert.That(asset, Is.Not.Null);
            var map = asset.FindActionMap("Player", true);
            string[] keys = { "<Keyboard>/1", "<Keyboard>/2", "<Keyboard>/3", "<Keyboard>/4", "<Keyboard>/q" };
            for (int i = 0; i < 5; i++)
            {
                var a = map.FindAction("ToolSlot" + (i + 1));
                Assert.That(a, Is.Not.Null, "ToolSlot" + (i + 1));
                Assert.That(a.bindings.Any(b => b.path == keys[i]), Is.True, keys[i]);
            }
            Assert.That(map.FindAction("ToolNext").bindings.Any(b => b.path == "<Gamepad>/rightShoulder"), Is.True);
            Assert.That(map.FindAction("ToolPrev").bindings.Any(b => b.path == "<Gamepad>/leftShoulder"), Is.True);
            Assert.That(map.FindAction("ToolScroll").bindings.Any(b => b.path == "<Mouse>/scroll/y"), Is.True);
            // Emote keys 5-8 stay free for FarmerEmotes.
            Assert.That(map.bindings.Any(b => b.path == "<Keyboard>/5" || b.path == "<Keyboard>/8"), Is.False);
        }

        [Test] public void Arms_ToolPosesOverrideTheGenericGrip()
        {
            Assert.That(ArmMath.Select(true, true, 1, false, false, ArmPose.LassoTwirl), Is.EqualTo(ArmPose.LassoTwirl));
            Assert.That(ArmMath.Select(true, false, 8, false, false, ArmPose.WagonPush), Is.EqualTo(ArmPose.WagonPush));
            Assert.That(ArmMath.Select(true, true, 1, false, false, ArmPose.Tool), Is.EqualTo(ArmPose.Tool), "no override");
            Assert.That(ArmMath.Select(false, false, 0, true, false, ArmPose.WhistleBlow), Is.EqualTo(ArmPose.Reach), "override only while holding");
            Assert.That(ArmMath.Select(true, true, 1, false, true, ArmPose.LassoReel), Is.EqualTo(ArmPose.ThrowSwing), "throw follow-through wins");
            Assert.That(ArmMath.Select(true, false, 2, false, false), Is.EqualTo(ArmPose.GripTwoHands), "old overload unchanged");
            foreach (var p in new[] { ArmPose.LassoTwirl, ArmPose.LassoHold, ArmPose.LassoReel, ArmPose.WhistleBlow, ArmPose.WagonPush })
                Assert.That(ArmMath.Curl(p), Is.InRange(0.5f, 1f), p.ToString());
            ArmMath.ReelHands(0f, 0.2f, out float l0, out float r0);
            ArmMath.ReelHands(0.5f, 0.2f, out float l1, out float r1);
            Assert.That(l0, Is.EqualTo(0f).Within(1e-4)); Assert.That(r0, Is.EqualTo(0.2f).Within(1e-4));
            Assert.That(l1, Is.EqualTo(0.2f).Within(1e-4)); Assert.That(r1, Is.EqualTo(0f).Within(1e-4), "hand over hand");
        }

        [Test] public void Stamina_ExtraDrain_LassoPull10PerSecond()
        {
            var t = ScriptableObject.CreateInstance<PlayerTuning>();
            var s = new StaminaModel(t);
            float start = s.Value;
            s.Tick(1f, false, 0f, 10f);
            Assert.That(s.Value, Is.EqualTo(start - 10f).Within(1e-3));
            s.Tick(1f, false, 0f); // old signature still works and regenerates after the delay
            Object.DestroyImmediate(t);
        }
    }
}
