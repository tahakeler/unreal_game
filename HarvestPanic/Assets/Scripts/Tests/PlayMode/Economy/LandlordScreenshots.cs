using System.Collections;
using System.IO;
using HarvestPanic.Core;
using HarvestPanic.Economy;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HarvestPanic.Tests.PlayMode.Economy
{
    /// <summary>
    /// QA capture (Explicit): receipt with awards, Landlord rule memos, and the Landlord Sunday walk-in. Writes PNGs to
    /// $HP_QA_DIR (or $HP_QA_OUT, default Logs/qa). Run batchmode WITHOUT -nographics, -testFilter LandlordScreenshots.
    /// </summary>
    [Explicit, Category("QA")]
    public class LandlordScreenshots
    {
        [UnityTest]
        public IEnumerator CaptureLandlord()
        {
#if UNITY_EDITOR
            string outDir = System.Environment.GetEnvironmentVariable("HP_QA_DIR");
            if (string.IsNullOrEmpty(outDir)) outDir = System.Environment.GetEnvironmentVariable("HP_QA_OUT");
            if (string.IsNullOrEmpty(outDir)) outDir = Path.GetFullPath("Logs/qa");
            Directory.CreateDirectory(outDir);
            Services.Clear();
            RunSaveStore.DirectoryOverride = Path.Combine(outDir, "save");
            RunSaveStore.Delete();
            EconomyDevCvars.DayScale = 0.05f;
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/Dev/EconomyTest.unity", new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
            float end = Time.realtimeSinceStartup + 10f;
            while ((EconomyDirector.Instance == null || EconomyDirector.Instance.Phase != DayPhase.Workday) && Time.realtimeSinceStartup < end) yield return null;
            var dir = EconomyDirector.Instance;
            Assert.NotNull(dir);
            dir.EmitBlame(new BlameEvent { Instigators = new ulong[] { 1 }, Verb = BlameVerb.DroppedInRiver, ItemId = "Crop.PrizePumpkin", DollarDelta = -210 });
            dir.EmitBlame(new BlameEvent { Instigators = new ulong[] { 0 }, Verb = BlameVerb.BrokePlank, DollarDelta = -30 });
            dir.EmitBlame(new BlameEvent { Instigators = new ulong[] { 0 }, Verb = BlameVerb.BrokePlank, DollarDelta = -30, Timestamp = 50f });
            dir.EmitBlame(new BlameEvent { Instigators = new ulong[] { 2 }, Verb = BlameVerb.Rescue, ItemId = "Livestock.Pig", DollarDelta = 45 });
            dir.AddWalletTxn(new WalletTxn { Type = WalletTxnType.Sale, Amount = 900, GoodsId = "Crop.PrizePumpkin" });
            while (dir.Phase != DayPhase.Payout) { dir.DebugSkipPhase(); yield return null; }
            yield return new WaitForSeconds(0.3f);
            yield return EconomyHudScreenshots.Capture(Path.Combine(outDir, "landlord-receipt-awards.png"));

            for (int i = 0; i < 80 && dir.Phase != DayPhase.Sunday && dir.Phase != DayPhase.Evicted; i++) { dir.DebugSkipPhase(); yield return null; }
            var v = LandlordVisitor.Current;
            Assert.NotNull(v);
            var cam = Camera.main;
            float until = Time.realtimeSinceStartup + 25f;
            bool shotCount = false;
            while (v.IsPlaying && Time.realtimeSinceStartup < until)
            {
                cam.transform.position = v.transform.position + new Vector3(-2.5f, 2.4f, -6.5f);
                cam.transform.LookAt(v.transform.position + Vector3.up * 2.4f);
                if (!shotCount && v.Script.State == LandlordVisitState.Counting) { shotCount = true; yield return EconomyHudScreenshots.Capture(Path.Combine(outDir, "landlord-visit-count.png")); }
                if (v.Script.State == LandlordVisitState.Verdict) break;
                yield return null;
            }
            yield return new WaitForSeconds(0.9f);
            yield return EconomyHudScreenshots.Capture(Path.Combine(outDir, "landlord-visit-verdict.png"));
            while (v != null && v.IsPlaying) yield return null;
            yield return new WaitForSeconds(0.3f);
            yield return EconomyHudScreenshots.Capture(Path.Combine(outDir, "landlord-sunday-receipt.png"));

            if (dir.Phase == DayPhase.Sunday)
            {
                dir.NextDay();
                yield return new WaitForSeconds(0.5f);
                yield return EconomyHudScreenshots.Capture(Path.Combine(outDir, "landlord-rule-memos.png"));
            }
            RunSaveStore.Delete();
            RunSaveStore.DirectoryOverride = null;
            EconomyDevCvars.DayScale = 1f;
#else
            yield break;
#endif
        }
    }
}
