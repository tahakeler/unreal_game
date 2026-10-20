using System.IO;
using System.Linq;
using HarvestPanic.Core;
using HarvestPanic.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HarvestPanic.Tests.EditMode.UI
{
    public class OnboardingAccessibilityTests
    {
        [SetUp] public void SetUp() { SettingsService.UseStore(new MemorySettingsStore()); InputBindings.UseStore(new MemorySettingsStore()); }
        [TearDown] public void TearDown() { SettingsService.UseStore(null); InputBindings.UseStore(null); CameraComfort.Reset(); }

        // ---------------------------------------------------------------- tip rules
        [Test]
        public void Tips_MoveOnDayStart_ThenGrabAfterDelay_InOrder()
        {
            var r = new TipRules(new MemoryTipStore());
            Assert.IsNull(r.Tick(0f, false), "nothing armed before the day starts");
            r.Signal(TipSignal.DayStarted);
            Assert.AreEqual("move", r.Tick(1f, false)?.Id);
            Assert.IsNull(r.Tick(15f, false), "grab waits 20 s after move");
            Assert.AreEqual("grab", r.Tick(21.5f, false)?.Id);
        }

        [Test]
        public void Tips_ShownOncePerProfile_SecondRunSilent()
        {
            var store = new MemoryTipStore();
            var first = new TipRules(store); first.Signal(TipSignal.DayStarted);
            Assert.AreEqual("move", first.Tick(0f, false)?.Id);
            var second = new TipRules(store); second.Signal(TipSignal.DayStarted);
            Assert.IsNull(second.Tick(100f, false));
        }

        [Test]
        public void Tips_RespectSpacing_Disaster_Modal_Disabled_AndPrereqs()
        {
            var store = new MemoryTipStore();
            var r = new TipRules(store);
            r.Signal(TipSignal.NearPen);
            Assert.IsNull(r.Tick(0f, false), "sell needs grab first");
            store.MarkSeen("move"); store.MarkSeen("grab");
            r.Signal(TipSignal.NearPen); r.Signal(TipSignal.NearBridge);
            Assert.IsNull(r.Tick(0f, true), "blocked by menu");
            r.NotifyDisaster(0f);
            Assert.IsNull(r.Tick(5f, false), "quiet after disaster");
            Assert.AreEqual("bridge", r.Tick(11f, false)?.Id);
            Assert.IsNull(r.Tick(15f, false), "spacing");
            Assert.AreEqual("sell", r.Tick(24f, false)?.Id);
            r.Enabled = false; r.Signal(TipSignal.ShopOpened);
            Assert.IsNull(r.Tick(100f, false));
        }

        [Test]
        public void Tips_LearnedAndSkipAll_SuppressTapes()
        {
            var store = new MemoryTipStore();
            var r = new TipRules(store);
            r.MarkLearned("shop"); r.Signal(TipSignal.ShopOpened);
            Assert.IsNull(r.Tick(0f, false));
            r.SkipAll();
            Assert.IsTrue(r.Defs.All(d => store.IsSeen(d.Id)));
        }

        [Test]
        public void Tips_ResolveTokens_UseCurrentBindings()
        {
            Assert.AreEqual("Hold [E] now", TipRules.Resolve("Hold {Interact} now", false));
            InputBindings.SetOverride("Interact", "<Keyboard>/e", "<Keyboard>/f");
            Assert.AreEqual("Hold [F] now", TipRules.Resolve("Hold {Interact} now", false));
            StringAssert.Contains("W/A/S/D", TipRules.Resolve("{MoveKeys}", false));
        }

        // ---------------------------------------------------------------- settings
        [Test]
        public void Settings_NewAccessFields_PersistAndClamp()
        {
            var store = new MemorySettingsStore();
            SettingsService.UseStore(store);
            var s = SettingsService.Current.Copy();
            Assert.IsTrue(s.Subtitles && s.TipsEnabled && s.CameraBob);
            Assert.AreEqual(0.7f, s.CameraShake, 1e-4);
            s.Subtitles = false; s.TipsEnabled = false; s.CameraBob = false; s.CameraShake = 3f; s.CaptionBackground = -1f; s.Fov = 105f;
            SettingsService.Apply(s);
            SettingsService.UseStore(store);
            var l = SettingsService.Current;
            Assert.IsFalse(l.Subtitles || l.TipsEnabled || l.CameraBob);
            Assert.AreEqual(1f, l.CameraShake); Assert.AreEqual(0f, l.CaptionBackground); Assert.AreEqual(105f, l.Fov);
        }

        [Test]
        public void Settings_OldJsonWithoutNewFields_GetsDefaults()
        {
            SettingsService.UseStore(new MemorySettingsStore { Json = "{\"Sensitivity\":2.0,\"Fov\":80}" });
            var s = SettingsService.Current;
            Assert.AreEqual(2f, s.Sensitivity); Assert.IsTrue(s.Subtitles && s.TipsEnabled); Assert.AreEqual(0.6f, s.CaptionBackground, 1e-4);
        }

        [Test]
        public void Comfort_ReducedMotion_DisablesBobAndKick()
        {
            var s = new GameSettings { Fov = 100f, CameraShake = 0.8f, ReducedMotion = true };
            UiRuntime.ApplyComfort(s);
            Assert.AreEqual(100f, CameraComfort.BaseFov); Assert.AreEqual(0f, CameraComfort.BobScale);
            Assert.AreEqual(0.25f, CameraComfort.ShakeScale, 1e-4); Assert.AreEqual(0f, CameraComfort.FovKickScale);
            s.ReducedMotion = false; s.CameraBob = false; UiRuntime.ApplyComfort(s);
            Assert.AreEqual(0f, CameraComfort.BobScale); Assert.AreEqual(0.8f, CameraComfort.ShakeScale, 1e-4);
        }

        // ---------------------------------------------------------------- bindings
        [Test]
        public void Bindings_SaveLoad_RoundTrip_AndApplyToAnyPlayerMap()
        {
            var store = new MemorySettingsStore();
            InputBindings.UseStore(store);
            InputBindings.SetOverride("Jump", "<Keyboard>/space", "<Keyboard>/k");
            InputBindings.SetOverride("Holler", "<Gamepad>/buttonNorth", "<Gamepad>/buttonEast");
            InputBindings.UseStore(store); // reload
            Assert.AreEqual(2, InputBindings.Current.Entries.Count);

            var asset = InputBindings.BuildTemplate();
            var map = asset.FindActionMap("Player");
            InputBindings.ApplyTo(map, new GameSettings { Sensitivity = 2f, InvertY = true });
            var jump = map.FindAction("Jump");
            Assert.AreEqual("<Keyboard>/k", jump.bindings.First(b => b.path == "<Keyboard>/space").effectivePath);
            var look = map.FindAction("Look").bindings[0];
            Assert.AreEqual("ScaleVector2(x=2,y=-2)", look.effectiveProcessors);

            InputBindings.SetOverride("Jump", "<Keyboard>/space", "<Keyboard>/space"); // back to default clears
            Assert.AreEqual(1, InputBindings.Current.Entries.Count);
            Object.DestroyImmediate(asset);
        }

        [Test]
        public void Bindings_CorruptJson_FallsBackToDefaults_AndConflictDetected()
        {
            InputBindings.UseStore(new MemorySettingsStore { Json = "{not json" });
            Assert.AreEqual(0, InputBindings.Current.Entries.Count);
            var jump = InputBindings.Defaults.First(d => d.Action == "Jump" && d.IsKeyboardMouse);
            Assert.IsTrue(InputBindings.TryFindConflict(jump, "<Keyboard>/e", out var other));
            Assert.AreEqual("Interact", other.Action);
        }

        [Test]
        public void Bindings_DefaultTable_MatchesPlayerControlsAsset()
        {
            var path = Path.Combine(Application.dataPath, "Prefabs/Player/Input/PlayerControls.inputactions");
            var asset = InputActionAsset.FromJson(File.ReadAllText(path));
            var map = asset.FindActionMap("Player", true);
            foreach (var d in InputBindings.Defaults)
            {
                var a = map.FindAction(d.Action, true);
                Assert.IsTrue(a.bindings.Any(b => b.path == d.DefaultPath && (b.groups ?? "").Contains(d.Group)), $"{d.Action} {d.DefaultPath} {d.Group}");
            }
            Object.DestroyImmediate(asset);
        }

        // ---------------------------------------------------------------- captions / palette
        [Test]
        public void Captions_MaxThree_LowestPriorityOldestReplaced()
        {
            var f = new CaptionFeed();
            f.Push("a", 1, 0); f.Push("b", 2, 0); f.Push("c", 1, 0);
            Assert.IsTrue(f.Push("d", 2, 0));
            CollectionAssert.AreEqual(new[] { "b", "c", "d" }, f.Lines.Select(l => l.Text).ToArray());
            f.Push("x", 3, 0); // replaces c
            Assert.IsFalse(f.Push("low", 1, 0), "every line outranks it");
            Assert.IsTrue(f.Expire(10f)); Assert.AreEqual(0, f.Lines.Count);
        }

        [Test]
        public void Captions_DescribeAndDirection()
        {
            var e = new NoiseEvent { Source = NoiseSource.Impact, Tags = NoiseTag.PlankBreak, Loudness = 1 };
            Assert.AreEqual("Plank snaps", CaptionFeed.Describe(e, out var p, out var danger)); Assert.AreEqual(3, p); Assert.IsTrue(danger);
            Assert.IsNull(CaptionFeed.Describe(new NoiseEvent { Source = NoiseSource.Footstep }, out _, out _));
            Assert.AreEqual(">", CaptionFeed.Direction(Vector3.zero, Vector3.forward, new Vector3(10, 0, 0)));
            Assert.AreEqual("<", CaptionFeed.Direction(Vector3.zero, Vector3.forward, new Vector3(-10, 0, 0)));
            Assert.AreEqual("v", CaptionFeed.Direction(Vector3.zero, Vector3.forward, new Vector3(0, 0, -10)));
        }

        [Test]
        public void Palette_ColourblindModes_KeepLossAndGoodDistinct()
        {
            foreach (ColorblindMode m in System.Enum.GetValues(typeof(ColorblindMode)))
            {
                var loss = UiPalette.Get(ReservedColor.Loss, m); var good = UiPalette.Get(ReservedColor.Good, m);
                Assert.Greater(Mathf.Abs(loss.r - good.r) + Mathf.Abs(loss.b - good.b), 0.5f, m.ToString());
            }
        }
    }
}
