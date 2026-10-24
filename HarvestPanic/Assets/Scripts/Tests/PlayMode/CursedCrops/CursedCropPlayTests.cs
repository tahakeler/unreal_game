using System.Collections;
using HarvestPanic.AI.Crops;
using HarvestPanic.AI.Night;
using HarvestPanic.Core;
using HarvestPanic.Economy;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.TestTools;

namespace HarvestPanic.Tests.PlayMode.CursedCrops
{
    /// <summary>
    /// Host session with the real EconomyDirector: a pulled mandrake rests in the market pen and sells at its premium
    /// quote; a cursed-crop field obeys its unlock day and plants at Morning; the tumble melon rolls when set down.
    /// </summary>
    public class CursedCropPlayTests
    {
        GameObject _nmGo, _penGo, _ground;
        NetworkManager _nm;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Services.Clear();
            EconomyDevCvars.DayScale = 0.02f;
            _nmGo = new GameObject("NM");
            var transport = _nmGo.AddComponent<UnityTransport>();
            transport.SetConnectionData("127.0.0.1", (ushort)Random.Range(20000, 60000));
            _nm = _nmGo.AddComponent<NetworkManager>();
            _nm.NetworkConfig = new NetworkConfig { NetworkTransport = transport, EnableSceneManagement = false };
            foreach (var name in new[] { "NetworkPrefabs/Economy", "NetworkPrefabs/Critters" })
            {
                var list = Resources.Load<NetworkPrefabsList>(name);
                Assert.NotNull(list, name);
                _nm.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(list);
            }
            _ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            _ground.transform.localScale = Vector3.one * 4f;
            _penGo = new GameObject("Pen");
            _penGo.AddComponent<LevelMarker>().Configure(MarkerRole.MarketPen);
            yield return null;
            Assert.IsTrue(_nm.StartHost(), "StartHost");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var f in Object.FindObjectsByType<CursedCropField>(FindObjectsSortMode.None)) Object.Destroy(f.gameObject);
            foreach (var c in Object.FindObjectsByType<CursedCropGoods>(FindObjectsSortMode.None))
                if (c.IsSpawned) c.NetworkObject.Despawn(true); else Object.Destroy(c.gameObject);
            if (EconomyDirector.Instance != null && EconomyDirector.Instance.IsSpawned) EconomyDirector.Instance.NetworkObject.Despawn(true);
            if (_nm != null) _nm.Shutdown();
            yield return null;
            foreach (var hud in Object.FindObjectsByType<EconomyHudMarker>(FindObjectsSortMode.None)) Object.Destroy(hud.gameObject);
            Object.Destroy(_nmGo); Object.Destroy(_penGo); Object.Destroy(_ground);
            EconomyDevCvars.DayScale = 1f;
            yield return null;
        }

        static IEnumerator WaitFor(System.Func<bool> cond, float timeout, string what)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!cond())
            {
                if (Time.realtimeSinceStartup > end) Assert.Fail("Timed out waiting for " + what);
                yield return null;
            }
        }

        static EconomyDirector StartEconomy()
        {
            var director = Object.Instantiate(EconomyBootstrapper.FindDirectorPrefab()).GetComponent<EconomyDirector>();
            director.NetworkObject.Spawn(true);
            return director;
        }

        [UnityTest]
        public IEnumerator PulledMandrake_SellsInPen_AtPremiumValue()
        {
            var director = StartEconomy();
            yield return WaitFor(() => director.Phase == DayPhase.Morning, 2f, "Morning");
            Content.Goods.Invalidate();
            Assert.IsTrue(Content.Goods.TryGet("Crop.Mandrake", out var def));
            Assert.IsTrue(Content.Goods.TryGet("Crop.Turnip", out var turnipDef));
            var go = Object.Instantiate(def.WorldPrefab, new Vector3(0f, 0.3f, 0f), Quaternion.identity);
            go.GetComponent<NetworkObject>().Spawn(true);
            var crop = go.GetComponent<CursedCropGoods>();
            crop.GetComponent<MandrakeScream>().Sacked = true; // bagged: still screams, just quieter
            crop.OnGrabbed(NetworkManager.ServerClientId);    // pulled
            crop.OnReleased(NetworkManager.ServerClientId);   // set down in the pen
            Assert.IsFalse(crop.Planted);
            Assert.IsTrue(crop.UsesEconomyDefinition, "Goods_Mandrake data asset");
            Assert.IsTrue((crop.Tags & GoodsTag.Cursed) != 0);

            int sold = 0; WalletTxn sale = default;
            director.OnWalletTxn += t => { if (t.Type == WalletTxnType.Sale) { sold++; sale = t; } };
            yield return WaitFor(() => director.Phase == DayPhase.Workday, 5f, "Workday");
            int expected = director.QuoteValue(crop, true);
            Assert.GreaterOrEqual(expected, 40, "premium crop quote");
            yield return WaitFor(() => sold > 0, 6f, "sale");
            Assert.AreEqual(expected, sale.Amount);
            Assert.AreEqual(NetworkManager.ServerClientId, sale.PlayerId);
            Assert.Greater(sale.Amount, turnipDef.BaseValue * 3, "worth several turnips");
            Assert.IsTrue(crop == null || !crop.IsSpawned, "sold mandrake despawned");
        }

        [UnityTest]
        public IEnumerator Field_PlantsOnlyFromUnlockDay_AtMorning()
        {
            var director = StartEconomy();
            yield return WaitFor(() => director.Phase == DayPhase.Morning, 2f, "Morning");
            var fieldGo = new GameObject("Field_Mandrake");
            fieldGo.transform.position = new Vector3(6f, 0f, 6f);
            var field = fieldGo.AddComponent<CursedCropField>();
            field.Configure("Crop.Mandrake", 4, 1.6f, 2, 0.25f);
            int unlock = CursedCropTuning.Default.UnlockDay("Crop.Mandrake");
            Assert.Greater(unlock, 0);
            yield return new WaitForSeconds(1.2f);
            Assert.AreEqual(0, field.Crops.Count, "day 0: mandrakes not planted yet");

            for (int day = 1; day <= unlock; day++)
            {
                while (director.Phase != DayPhase.Payout) { director.DebugSkipPhase(); yield return null; }
                director.NextDay(); yield return null;   // Shop
                director.NextDay(); yield return null;   // Morning
                yield return WaitFor(() => director.Phase == DayPhase.Morning && director.DayIndex == day, 2f, "Morning " + day);
                yield return null; yield return null;
                if (day < unlock) Assert.AreEqual(0, field.Crops.Count, $"day {day}: still locked");
            }
            yield return WaitFor(() => field.PlantedCount == 4, 2f, "field planted on unlock day");
            foreach (var c in field.Crops)
            {
                Assert.IsTrue(c.IsSpawned, "networked");
                Assert.Less(c.transform.position.y, 0f, "sunk into the soil");
            }
            // Pull two; next Morning tops the field back up to 4 planted.
            field.Crops[0].OnGrabbed(NetworkManager.ServerClientId);
            field.Crops[1].OnGrabbed(NetworkManager.ServerClientId);
            Assert.AreEqual(2, field.PlantedCount);
            while (director.Phase != DayPhase.Payout) { director.DebugSkipPhase(); yield return null; }
            director.NextDay(); yield return null;
            director.NextDay(); yield return null;
            yield return WaitFor(() => field.PlantedCount == 4, 2f, "top-up");
            Assert.AreEqual(6, field.Crops.Count, "2 loose + 4 planted");
        }

        [UnityTest]
        public IEnumerator TumbleMelon_RollsAwayWhenSetDown()
        {
            Content.Goods.Invalidate();
            Assert.IsTrue(Content.Goods.TryGet("Crop.TumbleMelon", out var def));
            var go = Object.Instantiate(def.WorldPrefab, new Vector3(-4f, 0.05f, -4f), Quaternion.identity);
            var crop = go.GetComponent<CursedCropGoods>();
            var melon = go.GetComponent<TumbleMelon>();
            Assert.AreEqual(4, crop.HandsRequired, "2 carriers");
            yield return new WaitForFixedUpdate();
            Assert.AreEqual(TumbleMelon.Phase.Planted, melon.Current);
            crop.OnGrabbed(NetworkManager.ServerClientId);
            Assert.AreEqual(TumbleMelon.Phase.Held, melon.Current);
            crop.OnReleased(NetworkManager.ServerClientId);
            Vector3 start = go.transform.position;
            float t0 = Time.time;
            while (melon.RollCount == 0 && Time.time - t0 < 3f) yield return null;
            Assert.AreEqual(1, melon.RollCount, "rolls after settle + wobble");
            yield return new WaitForSeconds(0.6f);
            Vector3 moved = go.transform.position - start; moved.y = 0f;
            Assert.Greater(moved.magnitude, 0.5f, "it actually rolled away");
        }

        [Test]
        public void NightDirector_WeightsCropNoiseDouble()
        {
            // The mandrake scream is Source=Crop: the night director's noise map doubles it (attracts night creatures).
            Assert.AreEqual(2f, NightMath.SourceMult(NoiseSource.Crop));
            Assert.Greater(NightMath.SourceMult(NoiseSource.Crop), NightMath.SourceMult(NoiseSource.Shout));
        }
    }
}
