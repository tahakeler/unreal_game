using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarvestPanic.AI;
using HarvestPanic.Core;
using HarvestPanic.Economy;
using HarvestPanic.Gameplay;
using HarvestPanic.World;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HarvestPanic.Tests.PlayMode.Tools
{
    /// <summary>Shared Muddlebury setup for the farm-tools / wagon tests (solo editor host, real networking stack).</summary>
    public static class Farm
    {
        public static IEnumerator Load()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Muddlebury.unity", new LoadSceneParameters(LoadSceneMode.Single));
            var go = Object.Instantiate(HarvestPanic.Networking.NetworkingConfig.Load().networkManagerPrefab);
            go.GetComponent<HarvestPanic.Networking.SessionService>().HostInPlaceForEditor();
#else
            Assert.Ignore("Editor-only");
            yield break;
#endif
            float until = Time.realtimeSinceStartup + 25f;
            while ((PlayerCharacter.Local == null || EconomyDirector.Instance == null) && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(PlayerCharacter.Local, Is.Not.Null, "host player did not spawn");
            var belt = PlayerCharacter.Local.GetComponent<PlayerToolbelt>();
            Assert.That(belt, Is.Not.Null, "Player prefab has no PlayerToolbelt");
            while ((belt.PocketTool((int)ToolSlot.Whistle) == null || WagonCart.All.Count == 0) && Time.realtimeSinceStartup < until) yield return null;
            yield return new WaitForSeconds(2f);
        }

        public static IEnumerator Shutdown()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null) { nm.Shutdown(); yield return null; Object.Destroy(nm.gameObject); }
            yield return null;
        }

        static int GroundMask => ~LayerMask.GetMask(GameLayers.Player, GameLayers.Carryable, GameLayers.Critter, GameLayers.Ragdoll, GameLayers.Interactable, GameLayers.Water, "Ignore Raycast");

        public static Vector3 Ground(Vector3 p)
        {
            if (Physics.Raycast(new Vector3(p.x, 40f, p.z), Vector3.down, out var h, 80f, GroundMask, QueryTriggerInteraction.Ignore)) p.y = h.point.y;
            return p;
        }

        public static IEnumerator Teleport(PlayerCharacter pc, Vector3 pos, float yaw)
        {
            pc.OwnerTeleport(pos + Vector3.up * 0.05f, Quaternion.Euler(0f, yaw, 0f));
            pc.Motor.SetLook(yaw, 0f);
            for (int i = 0; i < 4; i++) yield return new WaitForFixedUpdate();
        }

        public static List<CritterBrain> Critters(string id) =>
            CritterRegistry.All.Where(c => c != null && c.Personality != null && c.Personality.CritterId == id).ToList();

        public static void Warp(CritterBrain c, Vector3 p)
        {
            var agent = c.GetComponent<NavMeshAgent>();
            if (NavMesh.SamplePosition(p, out var hit, 4f, NavMesh.AllAreas)) p = hit.position;
            if (agent != null && agent.enabled) agent.Warp(p); else c.transform.position = p;
            c.Home = p;
        }

        /// <summary>
        /// One-shot scripted input: set right after a fixed step so this frame's Update reads it (a coroutine resuming
        /// after <c>yield return null</c> runs after Update, and PlayerInputReader clears one-shots in LateUpdate).
        /// </summary>
        public static IEnumerator Tap(PlayerCharacter pc, Action<PlayerInputReader> set)
        {
            pc.Input.UseScripted = true;
            yield return new WaitForFixedUpdate();
            set(pc.Input);
            yield return null;
        }

        public static IEnumerator Equip(PlayerCharacter pc, ToolSlot slot)
        {
            var belt = pc.GetComponent<PlayerToolbelt>();
            yield return Tap(pc, i => i.Scripted.ToolSlot = (int)slot + 1); // through the input path (hotbar action)
            ulong want = belt.PocketId((int)slot);
            float until = Time.time + 2f;
            while (pc.Carry.HeldId != want && Time.time < until) yield return null;
            Assert.That(pc.Carry.HeldId, Is.EqualTo(want), slot + " not in hand");
        }

        public static IEnumerator Press(PlayerCharacter pc, bool down)
        {
            if (down) yield return Tap(pc, i => { i.Scripted.UsePressed = true; i.Scripted.UseHeld = true; });
            else yield return Tap(pc, i => { i.Scripted.UseReleased = true; i.Scripted.UseHeld = false; });
            yield return null;
        }

        public static Goods Goods(string id, int skip = 0) => HarvestPanic.Economy.Goods.All.Where(g => g != null && g.GoodsId == id).Skip(skip).FirstOrDefault();

        public static void Place(Component body, Vector3 p)
        {
            var rb = body.GetComponent<Rigidbody>();
            body.transform.position = p;
            if (rb != null) { rb.position = p; if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; } }
        }
    }

    /// <summary>
    /// PlayMode: lasso catches and reels a pig, whistle scatters chickens, the hotbar equips every tool, the wagon carries
    /// three crates across the bridge (planks count the wagon and each crate), tipping spills and is blamed, and a
    /// lassoed crate pulled into the river blames the lasser (design/gdd/farm-tools.md, wagon-vehicles.md).
    /// </summary>
    public class ToolsPlayModeTests
    {
        readonly List<BlameEvent> _blames = new List<BlameEvent>();
        void OnBlame(BlameEvent e) => _blames.Add(e);

        [UnitySetUp] public IEnumerator SetUp() { _blames.Clear(); ToolBlame.Emitted += OnBlame; yield return Farm.Load(); }

        [UnityTearDown] public IEnumerator TearDown()
        {
            ToolBlame.Emitted -= OnBlame;
            if (PlayerCharacter.Local != null) PlayerCharacter.Local.Input.UseScripted = false;
            yield return Farm.Shutdown();
        }

        // ------------------------------------------------------------------------------------------------- lasso

        [UnityTest]
        public IEnumerator Lasso_CatchesPig_HoldsIt_ThenReelsInAndGrabs()
        {
            var pc = PlayerCharacter.Local;
            var belt = pc.GetComponent<PlayerToolbelt>();
            Vector3 spot = Farm.Ground(new Vector3(2f, 0f, -10f));
            yield return Farm.Teleport(pc, spot, 90f);
            yield return Farm.Equip(pc, ToolSlot.Lasso);
            var lasso = belt.PocketTool<LassoTool>(ToolSlot.Lasso);
            Assert.That(lasso, Is.Not.Null);
            Assert.That(lasso.IsHolstered, Is.False, "lasso out of the belt");

            var pig = Farm.Critters("Critter.Pig").FirstOrDefault();
            Assert.That(pig, Is.Not.Null, "no pig in Muddlebury");
            bool caught = false;
            for (int attempt = 0; attempt < 3 && !caught; attempt++)
            {
                Farm.Warp(pig, spot + Vector3.right * 6.5f);
                yield return new WaitForFixedUpdate();
                pc.Motor.LookAt(pig.transform.position + Vector3.up * 0.35f);
                yield return Farm.Press(pc, true);
                Assert.That(lasso.Phase, Is.EqualTo(LassoPhase.Aiming), "hold Use = aim");
                Assert.That(lasso.ArmPose, Is.EqualTo(ArmPose.LassoTwirl));
                yield return new WaitForSeconds(0.2f);
                pc.Motor.LookAt(pig.transform.position + Vector3.up * 0.35f);
                yield return Farm.Press(pc, false); // release = throw
                float until = Time.time + 2f;
                while (lasso.Phase == LassoPhase.Flying && Time.time < until) yield return null;
                caught = lasso.Phase == LassoPhase.Tethered && lasso.TargetId == pig.NetworkObjectId;
                if (!caught) { Debug.Log($"[ToolsQA] throw {attempt} missed: {lasso.LastVerdict} phase={lasso.Phase}"); yield return new WaitForSeconds(0.6f); }
            }
            Assert.That(caught, Is.True, "lasso did not catch the pig");
            Assert.That(lasso.Lasser, Is.EqualTo(pc.OwnerClientId));

            // One player (600 N) out-anchors a pig (500 N) on grass: the rope holds it near its length, no water-ski.
            float worst = 0f, t0 = Time.time;
            while (Time.time - t0 < 2.5f)
            {
                worst = Mathf.Max(worst, LassoMath.FlatDistance(pig.transform.position, lasso.transform.position));
                Assert.That(lasso.IsTethered, Is.True, "rope came off while holding");
                yield return null;
            }
            Debug.Log($"[ToolsQA] pig held: worst distance {worst:0.00} m, rope {lasso.RopeLength:0.00} m, pig state {pig.State}");
            Assert.That(lasso.IsWaterSkiing, Is.False, "a pig must not drag one player on grass");
            Assert.That(worst, Is.LessThan(lasso.Tuning.LassoLength + lasso.Tuning.RopeSnapSlack), "the rope never let it run off");

            // Reel in (hold Use): the rope shortens, stamina drains, and at 1.5 m the pig is grabbed.
            yield return Farm.Press(pc, true);
            bool drained = false, reeled = false;
            float until2 = Time.time + 8f;
            while (pc.Carry.HeldId != pig.NetworkObjectId && Time.time < until2)
            {
                drained |= pc.Carry.ExtraStaminaDrain > 0f;
                reeled |= lasso.IsReeling && lasso.RopeLength < lasso.Tuning.LassoLength - 0.5f;
                yield return null;
            }
            yield return Farm.Press(pc, false);
            Assert.That(reeled, Is.True, "rope shortened while reeling");
            Assert.That(drained, Is.True, "pulling costs stamina (10/s)");
            Assert.That(pc.Carry.HeldId, Is.EqualTo(pig.NetworkObjectId), "auto-grab at 1.5 m");
            yield return null;
            Assert.That(pig.State, Is.EqualTo(CritterState.Carried));
            Assert.That(lasso.Phase, Is.EqualTo(LassoPhase.Idle));
            Assert.That(lasso.IsHolstered, Is.True, "lasso back on the belt");
            Assert.That(belt.Selected, Is.EqualTo((int)ToolSlot.Hands));
            Assert.That(pc.Carry.ExtraStaminaDrain, Is.EqualTo(0f));
        }

        [UnityTest]
        public IEnumerator Lasso_TiedToHitchingPost_HoldsPig_UntiedByPickingUp()
        {
            var pc = PlayerCharacter.Local;
            var belt = pc.GetComponent<PlayerToolbelt>();
            var post = Object.FindFirstObjectByType<HitchingPost>();
            Assert.That(post, Is.Not.Null, "the bootstrapper spawns a hitching post at the farm");
            Vector3 knot = post.Tie.transform.position;
            Vector3 spot = Farm.Ground(knot + new Vector3(1.2f, 0f, -1.2f));
            yield return Farm.Teleport(pc, spot, 180f);
            yield return Farm.Equip(pc, ToolSlot.Lasso);
            var lasso = belt.PocketTool<LassoTool>(ToolSlot.Lasso);
            var pig = Farm.Critters("Critter.Pig").Skip(1).FirstOrDefault() ?? Farm.Critters("Critter.Pig").First();
            bool caught = false;
            for (int attempt = 0; attempt < 3 && !caught; attempt++)
            {
                Farm.Warp(pig, spot + Vector3.back * 5f);
                yield return new WaitForFixedUpdate();
                pc.Motor.LookAt(pig.transform.position + Vector3.up * 0.35f);
                yield return Farm.Press(pc, true);
                yield return new WaitForSeconds(0.15f);
                pc.Motor.LookAt(pig.transform.position + Vector3.up * 0.35f);
                yield return Farm.Press(pc, false);
                float until = Time.time + 2f;
                while (lasso.Phase == LassoPhase.Flying && Time.time < until) yield return null;
                caught = lasso.IsTethered;
                if (!caught) { Debug.Log($"[ToolsQA] tie throw {attempt} missed: {lasso.LastThrowInfo} pig at {pig.transform.position} me {pc.transform.position}"); yield return new WaitForSeconds(0.6f); }
            }
            Assert.That(caught, Is.True, "no catch to tie");
            yield return null;
            Assert.That(lasso.InteractPrompt, Does.StartWith("E: Tie to"), "prompt near the post");
            yield return Farm.Tap(pc, i => i.Scripted.InteractPressed = true);
            float t0 = Time.time;
            while (!lasso.IsTied && Time.time - t0 < 1f) yield return null;
            Assert.That(lasso.IsTied, Is.True, "E ties the rope to the post");
            Assert.That(pc.Carry.IsHoldingReplicated, Is.False, "hands free after tying");
            Assert.That(lasso.IsHolstered, Is.False, "the coil stays on the post");

            // Walk away: the pig stays within the 3 m hitch of the post.
            yield return Farm.Teleport(pc, Farm.Ground(knot + new Vector3(12f, 0f, 0f)), 270f);
            float worst = 0f; t0 = Time.time;
            while (Time.time - t0 < 3f) { worst = Mathf.Max(worst, LassoMath.FlatDistance(pig.transform.position, knot)); yield return null; }
            Debug.Log($"[ToolsQA] tied pig worst {worst:0.00} m from the post (rope {lasso.RopeLength:0.00})");
            Assert.That(lasso.IsTethered, Is.True);
            Assert.That(lasso.RopeLength, Is.LessThanOrEqualTo(lasso.Tuning.LassoLength + 0.01f));
            Assert.That(worst, Is.LessThan(lasso.Tuning.LassoLength + 2f), "tied rope holds the pig at the post");
        }

        [UnityTest]
        public IEnumerator Lasso_CrateIntoRiver_BlamesLasser_LassoedIntoRiver()
        {
            var pc = PlayerCharacter.Local;
            var belt = pc.GetComponent<PlayerToolbelt>();
            // Mid-bridge, facing the downstream side.
            var planks = Object.FindObjectsByType<BridgePlank>(FindObjectsSortMode.None).OrderBy(p => p.Index).ToArray();
            Assert.That(planks.Length, Is.GreaterThan(10));
            var mid = planks[planks.Length / 2].transform.position;
            yield return Farm.Teleport(pc, mid + Vector3.up * 0.1f, 0f);
            yield return Farm.Equip(pc, ToolSlot.Lasso);
            var lasso = belt.PocketTool<LassoTool>(ToolSlot.Lasso);
            var crate = Farm.Goods("Product.Crate");
            Assert.That(crate, Is.Not.Null);
            Farm.Place(crate, mid + new Vector3(0f, 0.5f, 0.9f));
            yield return new WaitForSeconds(0.6f);
            pc.Motor.LookAt(crate.transform.position);
            yield return Farm.Press(pc, true);
            yield return new WaitForSeconds(0.15f);
            pc.Motor.LookAt(crate.transform.position);
            yield return Farm.Press(pc, false);
            float until = Time.time + 2f;
            while (lasso.Phase == LassoPhase.Flying && Time.time < until) yield return null;
            Assert.That(lasso.IsTethered && lasso.TargetId == crate.NetworkObjectId, Is.True, $"crate not roped ({lasso.LastVerdict})");
            Assert.That(crate.GetComponent<ConfigurableJoint>(), Is.Not.Null, "goods are towed by a rope joint");

            // Over the side: it swings down on the rope into the river.
            Farm.Place(crate, mid + new Vector3(0f, -0.6f, 2.8f));
            until = Time.time + 6f;
            while (!_blames.Any(b => b.Verb == BlameVerb.LassoedIntoRiver) && Time.time < until) yield return null;
            var e = _blames.FirstOrDefault(b => b.Verb == BlameVerb.LassoedIntoRiver);
            Assert.That(e.Verb, Is.EqualTo(BlameVerb.LassoedIntoRiver), "no LassoedIntoRiver blame");
            Assert.That(e.PrimaryInstigator, Is.EqualTo(pc.OwnerClientId), "the lasser is blamed");
            Assert.That(e.ItemId, Is.EqualTo("Product.Crate"));
            Assert.That(lasso.IsTethered, Is.False, "the river takes it; the rope comes off");
            Assert.That(crate.GetComponent<ConfigurableJoint>(), Is.Null);
            Assert.That(crate.LastHandlerId, Is.EqualTo(pc.OwnerClientId), "the river's soak loss is booked on the lasser too");
        }

        // ------------------------------------------------------------------------------------------------- whistle

        [UnityTest]
        public IEnumerator Whistle_ShortBlowScattersChickens_LongBlowIsRallyOnly_Cooldown()
        {
            var pc = PlayerCharacter.Local;
            var chickens = Farm.Critters("Critter.Chicken");
            Assert.That(chickens.Count, Is.GreaterThanOrEqualTo(4));
            // A calm flock on the open road, the farmer 10 m away (out of their 8 m sight, inside the 31 m whistle).
            Vector3 flock = Farm.Ground(new Vector3(28f, 0f, 3f));
            for (int i = 0; i < chickens.Count; i++)
                Farm.Warp(chickens[i], flock + Quaternion.Euler(0f, i * 360f / chickens.Count, 0f) * Vector3.forward * 1.2f);
            Vector3 me = Farm.Ground(flock + new Vector3(0f, 0f, -10f));
            yield return Farm.Teleport(pc, me, 0f);
            yield return Farm.Equip(pc, ToolSlot.Whistle);
            var whistle = pc.GetComponent<PlayerToolbelt>().PocketTool<WhistleTool>(ToolSlot.Whistle);
            yield return new WaitForSeconds(2.5f);

            var fled = new HashSet<CritterBrain>();
            var handlers = new List<(CritterBrain, Action<CritterState, CritterState>)>();
            foreach (var c in chickens)
            {
                var cc = c;
                Action<CritterState, CritterState> h = (a, b) => { if (b == CritterState.Flee) fled.Add(cc); };
                c.StateChanged += h; handlers.Add((c, h));
            }
            var noises = new List<NoiseEvent>();
            Assert.That(Services.TryGet(out INoiseService noise), Is.True);
            Action<NoiseEvent> nh = e => { if ((e.Tags & (NoiseTag.Whistle | NoiseTag.WhistleLong)) != 0) noises.Add(e); };
            noise.OnNoise += nh;
            try
            {
                float before = chickens.Average(c => LassoMath.FlatDistance(c.transform.position, pc.transform.position));
                int fledBefore = chickens.Count(c => c.State == CritterState.Flee);

                yield return Farm.Press(pc, true);
                Assert.That(whistle.ArmPose, Is.EqualTo(ArmPose.WhistleBlow), "whistle to the lips");
                yield return Farm.Press(pc, false); // short blow
                Assert.That(noises.Count, Is.EqualTo(1), "one short blow");
                var n0 = noises[0];
                Assert.That(n0.Tags, Is.EqualTo(NoiseTag.Whistle));
                Assert.That(n0.Source, Is.EqualTo(NoiseSource.Tool));
                Assert.That(n0.Loudness, Is.EqualTo(0.7f).Within(1e-3));
                Assert.That(n0.BlamedPlayerId, Is.EqualTo(pc.OwnerClientId));
                Assert.That(whistle.Ready, Is.False, "3 s cooldown");

                yield return new WaitForSeconds(3f);
                float after = chickens.Average(c => LassoMath.FlatDistance(c.transform.position, pc.transform.position));
                Debug.Log($"[ToolsQA] whistle: {fled.Count}/{chickens.Count} fled (before {fledBefore}), mean distance {before:0.0} -> {after:0.0} m");
                Assert.That(fled.Count, Is.GreaterThanOrEqualTo(Mathf.CeilToInt(chickens.Count * 0.5f)), "the flock scatters");
                Assert.That(after, Is.GreaterThan(before + 2f), "chickens ran away from the whistler");

                // Long blow (hold 1 s): a WhistleLong-only noise, no scare tag; then the cooldown blocks a third blow.
                noises.Clear();
                yield return Farm.Press(pc, true);
                yield return new WaitForSeconds(1.2f);
                Assert.That(noises.Count, Is.EqualTo(1), "long blow fires at 1 s");
                Assert.That(noises[0].Tags, Is.EqualTo(NoiseTag.WhistleLong), "rally only");
                Assert.That(whistle.LastBlow, Is.EqualTo(WhistleBlow.Long));
                yield return Farm.Press(pc, false);
                yield return Farm.Press(pc, true);
                yield return Farm.Press(pc, false);
                Assert.That(noises.Count, Is.EqualTo(1), "cooldown: no blow");
            }
            finally
            {
                noise.OnNoise -= nh;
                foreach (var (c, h) in handlers) if (c != null) c.StateChanged -= h;
            }
        }

        // ------------------------------------------------------------------------------------------------- toolbelt

        [UnityTest]
        public IEnumerator Toolbelt_HotbarEquipsEveryTool_AndEGrabsWithAutoHolster()
        {
            var pc = PlayerCharacter.Local;
            var belt = pc.GetComponent<PlayerToolbelt>();
            var overlay = pc.GetComponent<PlayerPromptOverlay>();
            foreach (var slot in new[] { ToolSlot.Net, ToolSlot.Bucket, ToolSlot.Lasso, ToolSlot.Whistle })
            {
                float t0 = Time.time;
                yield return Farm.Equip(pc, slot);
                Assert.That(Time.time - t0, Is.GreaterThanOrEqualTo(0.35f), "0.4 s equip time");
                Assert.That(pc.Carry.HoldingTool, Is.True);
                Assert.That(belt.Selected, Is.EqualTo((int)slot));
                yield return null;
                Assert.That(overlay.HotbarSelected, Is.EqualTo((int)slot), "HUD hotbar follows");
                foreach (var other in new[] { ToolSlot.Net, ToolSlot.Bucket, ToolSlot.Lasso, ToolSlot.Whistle })
                    if (other != slot) Assert.That(belt.PocketTool((int)other).IsHolstered, Is.True, other + " should be on the belt");
            }
            yield return Farm.Tap(pc, i => i.Scripted.ToolCycle = 1); // wheel: Whistle -> Hands
            yield return new WaitForSeconds(0.2f);
            Assert.That(belt.Selected, Is.EqualTo(0));
            Assert.That(pc.Carry.IsHoldingReplicated, Is.False);

            // Holding the net, E on a crate puts the net away and grabs the crate.
            yield return Farm.Equip(pc, ToolSlot.Net);
            var crate = Farm.Goods("Product.Crate");
            Vector3 fwd = pc.transform.forward;
            Farm.Place(crate, pc.transform.position + fwd * 1.3f + Vector3.up * 0.4f);
            yield return new WaitForSeconds(0.8f);
            pc.Motor.LookAt(crate.GetComponentInChildren<Collider>().bounds.center);
            yield return null;
            Assert.That(pc.Carry.Prompt, Does.Contain("E: Grab"), "prompt offers the swap");
            yield return Farm.Tap(pc, i => i.Scripted.InteractPressed = true);
            float until = Time.time + 2f;
            while (pc.Carry.HeldId != crate.NetworkObjectId && Time.time < until) yield return null;
            Assert.That(pc.Carry.HeldId, Is.EqualTo(crate.NetworkObjectId));
            Assert.That(belt.PocketTool((int)ToolSlot.Net).IsHolstered, Is.True, "net auto-holstered");
        }

        // ------------------------------------------------------------------------------------------------- wagon

        static bool OwnsCentre(BridgePlank plank, Vector3 p)
        {
            var box = plank.GetComponent<BoxCollider>();
            Vector3 local = plank.transform.InverseTransformPoint(p) - box.center;
            return Mathf.Abs(local.x) <= box.size.x * 0.5f && Mathf.Abs(local.z) <= box.size.z * 0.5f;
        }

        IEnumerator GrabHandle(PlayerCharacter pc, WagonCart wagon, int handle)
        {
            var h = wagon.Handles[handle];
            for (int i = 0; i < 3 && pc.Carry.HeldId != wagon.NetworkObjectId; i++)
            {
                pc.Motor.LookAt(h.position);
                yield return null;
                if (!pc.Carry.TryGrabAimed()) Debug.Log($"[ToolsQA] nothing aimed at handle {handle}");
                float until = Time.time + 1f;
                while (pc.Carry.HeldId != wagon.NetworkObjectId && Time.time < until) yield return null;
            }
            Assert.That(pc.Carry.HeldId, Is.EqualTo(wagon.NetworkObjectId), "wagon handle not grabbed");
        }

        IEnumerator LoadCrates(WagonCart wagon, int count, List<Goods> into)
        {
            for (int i = 0; i < count; i++)
            {
                var crate = Farm.Goods("Product.Crate", i);
                Assert.That(crate, Is.Not.Null, "need " + count + " crates");
                into.Add(crate);
                // Drop it into the bed from above (the bed scan loads free items that land there).
                Farm.Place(crate, wagon.Bed.TransformPoint(new Vector3(i % 2 == 0 ? -0.3f : 0.3f, 0.8f, 0.6f - i * 0.6f)));
                yield return new WaitForSeconds(0.9f);
            }
            float until = Time.time + 2f;
            while (wagon.CargoCount < count && Time.time < until) yield return null;
            Assert.That(wagon.CargoCount, Is.EqualTo(count), "crates dropped in the bed load");
        }

        [UnityTest]
        public IEnumerator Wagon_SpawnsAtFarm_CarriesThreeCratesAcrossBridge_PlanksCountWagonAndCargo()
        {
            var pc = PlayerCharacter.Local;
            Assert.That(WagonCart.All.Count, Is.EqualTo(1), "one wagon per farm");
            var wagon = WagonCart.All[0];
            var barn = LevelMarker.FindAll(MarkerRole.BarnRespawn)[0].transform;
            var homePose = WagonBootstrapper.Home(barn, WagonBootstrapper.HomeOffset);
            foreach (var w in WagonCart.All) Debug.Log($"[ToolsQA] wagon {w.name} spawned={w.IsSpawned} id={(w.IsSpawned ? w.NetworkObjectId : 0)} at {w.transform.position} scene={w.gameObject.scene.name} kin={w.GetComponent<Rigidbody>().isKinematic}");
            Debug.Log($"[ToolsQA] wagon at {wagon.transform.position} up {wagon.transform.up} v {wagon.GetComponent<Rigidbody>().linearVelocity} home {homePose.position}");
            Assert.That(Vector3.Distance(wagon.transform.position, homePose.position), Is.LessThan(2.5f), "spawned at the barn yard");
            Assert.That(wagon.IsTipped, Is.False);

            var planks = Object.FindObjectsByType<BridgePlank>(FindObjectsSortMode.None).OrderBy(p => p.Index).ToArray();
            float west = planks.Min(p => p.transform.position.x) - 1f, east = planks.Max(p => p.transform.position.x) + 1f;
            Vector3 start = Farm.Ground(new Vector3(west - 6f, 0f, 0f)) + Vector3.up * 0.1f;
            wagon.ServerPlace(start, Quaternion.Euler(0f, 90f, 0f));
            yield return new WaitForSeconds(1f);
            var crates = new List<Goods>();
            yield return LoadCrates(wagon, 3, crates);
            float cargoKg = crates.Sum(c => c.MassKg);
            Assert.That(wagon.CargoKg, Is.EqualTo(cargoKg).Within(0.01f));

            // Push from the rear bar.
            yield return Farm.Teleport(pc, Farm.Ground(wagon.transform.TransformPoint(new Vector3(0f, 0f, -2.3f))), 90f);
            yield return GrabHandle(pc, wagon, 2);
            yield return new WaitForSeconds(0.3f);
            Assert.That(pc.Carry.SpeedMultiplier, Is.GreaterThan(0.5f), "pushing a light wagon is not a 120 kg lift");
            pc.Input.Scripted.Move = new Vector2(0f, 1f);
            float midX = (west + east) * 0.5f;
            float until = Time.time + 25f, nextLog = 0f;
            var wrb = wagon.GetComponent<Rigidbody>();
            while (wagon.transform.position.x < midX && Time.time < until)
            {
                if (Time.time >= nextLog)
                {
                    nextLog = Time.time + 0.5f;
                    Debug.Log($"[ToolsQA] push: me {pc.transform.position} cc {pc.GetComponent<CharacterController>().velocity} wagon {wagon.transform.position} v {wrb.linearVelocity} held {pc.Carry.HeldId == wagon.NetworkObjectId} mult {pc.Carry.SpeedMultiplier:0.00} state {pc.State}");
                }
                yield return null;
            }
            pc.Input.Scripted.Move = Vector2.zero;
            Assert.That(wagon.transform.position.x, Is.GreaterThanOrEqualTo(midX), $"wagon stuck at {wagon.transform.position}");

            // Stop on the bridge: let go and let the planks sample a still load.
            yield return new WaitForSeconds(0.4f);
            pc.Carry.RequestRelease();
            yield return new WaitForSeconds(1.2f);
            float wagonFrame = wagon.Tuning.WagonMassKg;
            int checkedPlanks = 0; bool sawCargoOnWagonPlank = false;
            foreach (var p in planks)
            {
                float expected = 0f;
                bool wagonHere = OwnsCentre(p, wagon.transform.position);
                if (wagonHere) expected += wagonFrame;
                int cratesHere = 0;
                foreach (var c in crates) if (OwnsCentre(p, c.transform.position)) { expected += c.MassKg; cratesHere++; }
                if (OwnsCentre(p, pc.transform.position)) expected += pc.MassKg;
                if (expected <= 0f && p.LastLoadKg <= 0f) continue;
                checkedPlanks++;
                if (wagonHere && cratesHere > 0) sawCargoOnWagonPlank = true;
                Debug.Log($"[ToolsQA] plank {p.Index}: load {p.LastLoadKg:0.0} kg expected {expected:0.0} (wagon {wagonHere}, crates {cratesHere})");
                Assert.That(p.LastLoadKg, Is.EqualTo(expected).Within(0.6f), $"plank {p.Index} load");
            }
            Assert.That(checkedPlanks, Is.GreaterThanOrEqualTo(1));
            Assert.That(planks.Sum(p => p.LastLoadKg), Is.GreaterThanOrEqualTo(wagonFrame + cargoKg + pc.MassKg - 0.6f), "the full wagon + cargo + farmer is on the bridge, nothing double counted");
            Assert.That(planks.Sum(p => p.LastLoadKg), Is.LessThanOrEqualTo(wagonFrame + cargoKg + pc.MassKg + 0.6f));
            Debug.Log($"[ToolsQA] cargo on the wagon's own plank seen: {sawCargoOnWagonPlank}");

            // On to the far bank.
            yield return Farm.Teleport(pc, Farm.Ground(wagon.transform.TransformPoint(new Vector3(0f, 0f, -2.3f))) + Vector3.up * 0.3f, 90f);
            yield return GrabHandle(pc, wagon, 2);
            pc.Input.Scripted.Move = new Vector2(0f, 1f);
            until = Time.time + 25f;
            while (wagon.transform.position.x < east + 2f && Time.time < until) yield return null;
            pc.Input.Scripted.Move = Vector2.zero;
            Debug.Log($"[ToolsQA] wagon crossed to {wagon.transform.position}, cargo {wagon.CargoCount}");
            Assert.That(wagon.transform.position.x, Is.GreaterThan(east), "wagon crossed the bridge");
            Assert.That(wagon.CargoCount, Is.EqualTo(3), "all three crates arrived");
            Assert.That(wagon.IsTipped, Is.False);
            Assert.That(planks.All(p => p.State != PlankState.Broken), Is.True, "132 kg never breaks a plank");
        }

        [UnityTest]
        public IEnumerator Wagon_TippingSpillsCargo_BlamesDriver_AndSoloPlayerRightsIt()
        {
            var pc = PlayerCharacter.Local;
            var wagon = WagonCart.All[0];
            Vector3 home = Farm.Ground(wagon.transform.position) + Vector3.up * 0.1f;
            wagon.ServerPlace(home, Quaternion.Euler(0f, 90f, 0f));
            yield return new WaitForSeconds(1f);
            var crates = new List<Goods>();
            yield return LoadCrates(wagon, 2, crates);
            foreach (var c in crates) Assert.That(c.GetComponent<Rigidbody>().isKinematic, Is.True, "cargo rides kinematically");

            // Driver at the front handle.
            yield return Farm.Teleport(pc, Farm.Ground(wagon.transform.TransformPoint(new Vector3(0f, 0f, 2.7f))), 270f);
            yield return GrabHandle(pc, wagon, 0);
            yield return new WaitForSeconds(0.4f);
            Assert.That(wagon.Driver, Is.EqualTo(pc.OwnerClientId), "front handle holder drives");
            var bruiseBefore = crates.Select(c => c.BruisePoints).ToArray();

            // Roll it onto its side.
            var rb = wagon.GetComponent<Rigidbody>();
            rb.position = wagon.transform.position + Vector3.up * 0.6f;
            rb.rotation = Quaternion.Euler(0f, 90f, 80f);
            rb.angularVelocity = Vector3.zero; rb.linearVelocity = Vector3.zero;
            float until = Time.time + 2f;
            while (!wagon.IsTipped && Time.time < until) yield return null;
            Assert.That(wagon.IsTipped, Is.True, "past 50 degrees it tips");
            Assert.That(wagon.CargoCount, Is.EqualTo(0), "everything spills");
            foreach (var c in crates) Assert.That(c.GetComponent<Rigidbody>().isKinematic, Is.False, "spilled cargo is loose again");
            var tip = _blames.FirstOrDefault(b => b.Verb == BlameVerb.TippedWagon);
            Assert.That(tip.Verb, Is.EqualTo(BlameVerb.TippedWagon), "TippedWagon incident");
            Assert.That(tip.PrimaryInstigator, Is.EqualTo(pc.OwnerClientId), "the driver is blamed");
            yield return new WaitForSeconds(0.5f);
            Assert.That(crates.Zip(bruiseBefore, (c, b) => c.BruisePoints > b).Any(x => x), Is.True, "the spill bruises the cargo");
            var ledger = EconomyDirector.Instance.Ledger.Day;
            Assert.That(ledger.Any(e => e.Event.Verb == BlameVerb.TippedWagon && e.Event.PrimaryInstigator == pc.OwnerClientId), Is.True,
                "the spill loss is on the receipt as TippedWagon");

            // Solo: one player holding the handle for 2 s rights it.
            if (pc.Carry.HeldId != wagon.NetworkObjectId)
            {
                yield return Farm.Teleport(pc, Farm.Ground(wagon.transform.position + Vector3.forward * 1.6f), 180f);
                pc.Motor.LookAt(wagon.transform.position + Vector3.up * 0.4f);
                yield return null;
                pc.Carry.TryGrabAimed();
                yield return new WaitForSeconds(0.3f);
            }
            Assert.That(pc.Carry.HeldId, Is.EqualTo(wagon.NetworkObjectId), "holding the tipped wagon");
            until = Time.time + 4f;
            while (wagon.IsTipped && Time.time < until) yield return null;
            Assert.That(wagon.IsTipped, Is.False, "righted after 2 s");
            yield return new WaitForSeconds(0.5f);
            Assert.That(Vector3.Dot(wagon.transform.up, Vector3.up), Is.GreaterThan(0.9f), "back on its wheels");
        }
    }
}
