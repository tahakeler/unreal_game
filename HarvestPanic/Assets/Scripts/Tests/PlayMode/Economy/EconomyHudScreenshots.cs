using System.Collections;
using System.IO;
using HarvestPanic.Core;
using HarvestPanic.Economy;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace HarvestPanic.Tests.PlayMode.Economy
{
    /// <summary>
    /// QA capture (Explicit): plays the EconomyTest scene offline, renders camera + HUD panel off-screen and writes PNGs
    /// to $HP_QA_OUT (default Logs/qa). Run batchmode WITHOUT -nographics:
    /// <c>-runTests -testPlatform PlayMode -testFilter EconomyHudScreenshots</c>.
    /// </summary>
    [Explicit, Category("QA")]
    public class EconomyHudScreenshots
    {
        const int W = 1920, H = 1080;

        [UnityTest]
        public IEnumerator CaptureHudAndReceipts()
        {
#if UNITY_EDITOR
            string outDir = System.Environment.GetEnvironmentVariable("HP_QA_OUT");
            if (string.IsNullOrEmpty(outDir)) outDir = Path.GetFullPath("Logs/qa");
            Directory.CreateDirectory(outDir);
            Services.Clear();
            EconomyDevCvars.DayScale = 0.05f;
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/Dev/EconomyTest.unity", new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
            float end = Time.realtimeSinceStartup + 10f;
            while ((EconomyDirector.Instance == null || EconomyDirector.Instance.Phase != DayPhase.Workday) && Time.realtimeSinceStartup < end) yield return null;
            var dir = EconomyDirector.Instance;
            Assert.NotNull(dir);
            EconomyDevCvars.DayScale = 1f;
            yield return new WaitForSeconds(0.5f);

            // Stage a day: one pumpkin and a crate into the pen, some blame.
            var pen = Object.FindFirstObjectByType<MarketPen>();
            bool crateMoved = false;
            foreach (var g in Goods.All.ToArray())
            {
                bool pumpkin = g.GoodsId == "Crop.PrizePumpkin", crate = g.GoodsId == "Product.EggCrate" && !crateMoved;
                if (!pumpkin && !crate) continue;
                crateMoved |= crate;
                g.RecordTouch(0);
                g.transform.position = pen.transform.position + new Vector3(pumpkin ? -1.2f : 1.5f, 0.3f, 0.5f);
            }
            dir.EmitBlame(new BlameEvent { Instigators = new ulong[] { 1, 0 }, Verb = BlameVerb.Dropped, ItemId = "Livestock.Pig", DollarDelta = -24 });
            dir.EmitBlame(new BlameEvent { Instigators = new ulong[] { 1 }, Verb = BlameVerb.DroppedInRiver, ItemId = "Crop.PrizePumpkin", DollarDelta = -210 });
            dir.EmitBlame(new BlameEvent { Instigators = new ulong[] { 0 }, Verb = BlameVerb.Threw, ItemId = "Product.Egg", DollarDelta = -5 });
            dir.EmitBlame(new BlameEvent { Instigators = new ulong[] { 2 }, Verb = BlameVerb.LetEscape, ItemId = "Livestock.Chicken", DollarDelta = -15 });
            dir.EmitBlame(new BlameEvent { Instigators = new ulong[] { 2 }, Verb = BlameVerb.Rescue, ItemId = "Livestock.Pig", DollarDelta = 30 });
            dir.EmitBlame(new BlameEvent { Verb = BlameVerb.Bruised, ItemId = "Crop.Turnip", DollarDelta = -40, CauseTag = "Weather.Wind" });
            dir.ChargeRespawnFee(1, BlameVerb.Drowned);
            yield return new WaitForSeconds(2.6f);
            yield return Capture(Path.Combine(outDir, "economy_hud_workday.png"));

            while (dir.Phase != DayPhase.Payout) { dir.DebugSkipPhase(); yield return null; }
            yield return new WaitForSeconds(0.3f);
            yield return Capture(Path.Combine(outDir, "economy_receipt_payout.png"));

            for (int i = 0; i < 60 && dir.Phase != DayPhase.Sunday && dir.Phase != DayPhase.Evicted; i++) { dir.DebugSkipPhase(); yield return null; }
            yield return new WaitForSeconds(0.3f);
            yield return Capture(Path.Combine(outDir, "economy_receipt_sunday.png"));
            Debug.Log("[EconomyHudScreenshots] wrote " + outDir);
#else
            yield break;
#endif
        }

        static IEnumerator Capture(string path)
        {
            var cam = Camera.main;
            var doc = Object.FindFirstObjectByType<UIDocument>();
            var sceneRt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            var uiRt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            var original = doc.panelSettings;
            var ps = Object.Instantiate(original);
            ps.targetTexture = uiRt; ps.clearColor = true; ps.colorClearValue = Color.clear;
            doc.panelSettings = ps;
            yield return null; yield return null; yield return new WaitForEndOfFrame();

            cam.targetTexture = sceneRt; cam.Render(); cam.Render(); cam.targetTexture = null;
            var a = Read(sceneRt); var b = Read(uiRt);
            var px = a.GetPixels32(); var ui = b.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                float al = ui[i].a / 255f;
                px[i] = new Color32((byte)Mathf.Min(255, ui[i].r + px[i].r * (1 - al)), (byte)Mathf.Min(255, ui[i].g + px[i].g * (1 - al)), (byte)Mathf.Min(255, ui[i].b + px[i].b * (1 - al)), 255);
            }
            a.SetPixels32(px);
            File.WriteAllBytes(path, a.EncodeToPNG());
            doc.panelSettings = original;
            Object.Destroy(ps); Object.Destroy(a); Object.Destroy(b);
            sceneRt.Release(); uiRt.Release();
        }

        static Texture2D Read(RenderTexture rt)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var t = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            t.Apply();
            RenderTexture.active = prev;
            return t;
        }
    }
}
