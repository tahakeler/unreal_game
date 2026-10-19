using System.Collections;
using System.IO;
using System.Linq;
using HarvestPanic.Customization;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace HarvestPanic.Tests.PlayMode.Customization
{
    /// <summary>Applies loadouts to FarmerBody in Scenes/Dev/WardrobeTest.unity; wardrobe + emote flows offline.</summary>
    public class CustomizationPlayModeTests
    {
        const string ScenePath = "Assets/Scenes/Dev/WardrobeTest.unity";
        string _savedPrefs;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _savedPrefs = PlayerPrefs.GetString(LoadoutStorage.PrefsKey, null);
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#endif
            yield return null;
            yield return null;
        }

        [TearDown]
        public void RestorePrefs()
        {
            if (WardrobeUI.Current != null) WardrobeUI.Current.Close(false);
            if (string.IsNullOrEmpty(_savedPrefs)) PlayerPrefs.DeleteKey(LoadoutStorage.PrefsKey);
            else PlayerPrefs.SetString(LoadoutStorage.PrefsKey, _savedPrefs);
        }

        static WardrobeTestLineup Lineup() => Object.FindFirstObjectByType<WardrobeTestLineup>();

        [UnityTest]
        public IEnumerator ApplyLoadout_DressesFarmerBody()
        {
            var lineup = Lineup();
            Assert.That(lineup, Is.Not.Null);
            Assert.That(lineup.Farmers.Count, Is.EqualTo(4));
            var f = lineup.Farmers[2];
            var cat = f.Catalog;
            var l = WardrobeTestLineup.SetA[2]; // Viking, goggles, pitchfork
            f.Apply(l);
            yield return null;

            Assert.That(f.Current, Is.EqualTo(l));
            Assert.That(f.Accessories.Count, Is.EqualTo(3));
            var hat = f.Accessories[0];
            Assert.That(hat.name, Is.EqualTo("Cosmetic_hat.viking"));
            Assert.That(hat.transform.IsChildOf(f.HeadAttachPoint), "hat attaches to the head bone");
            Assert.That(f.Accessories[2].transform.IsChildOf(f.BackAttachPoint));
            Assert.That(hat.GetComponentsInChildren<Collider>().Length, Is.EqualTo(0), "cosmetics are never grabbable");

            var primary = cat.PaletteColor(l.Primary);
            bool overallsTinted = f.GetComponentsInChildren<SkinnedMeshRenderer>().SelectMany(r => r.sharedMaterials)
                .Any(m => m != null && m.HasProperty("_BaseColor") && m.GetColor("_BaseColor") == primary);
            Assert.That(overallsTinted, "overalls use the primary palette colour");
            Assert.That(f.NameTag.Shape, Is.EqualTo(cat.NameTag(l.NameTag).shape));

            // Change hat: old accessory is replaced, not stacked.
            l.Hat = 0; l.Face = 0;
            f.Apply(l);
            yield return null;
            Assert.That(f.Accessories.Count, Is.EqualTo(1));
            Assert.That(f.HeadAttachPoint.GetComponentsInChildren<MeshRenderer>().Length, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator BodyVariant_ScalesModel()
        {
            var f = Lineup().Farmers[0];
            var l = FarmerLoadout.Default;
            l.Body = 1;
            f.Apply(l);
            yield return null;
            var model = f.BodyAnimator.transform;
            Assert.That(model.localScale, Is.EqualTo(f.Catalog.Body(1).bodyScale));
        }

        [UnityTest]
        public IEnumerator HatSitsOnTopOfHead()
        {
            var f = Lineup().Farmers[0];
            var l = FarmerLoadout.Default; l.Hat = CosmeticCatalog.StrawHatId;
            f.Apply(l);
            yield return null; yield return null;
            float hatY = f.HeadAttachPoint.position.y;
            Assert.That(hatY, Is.InRange(1.5f, 2.3f), "hat attach near the top of a ~1.8 m farmer");
        }

        [UnityTest]
        public IEnumerator Wardrobe_SurpriseAndConfirm_SavesAndApplies()
        {
            var f = Lineup().Farmers[1];
            f.OfflineIsLocal = true;
            var sync = f.GetComponent<FarmerLoadoutSync>();
            var ui = WardrobeUI.Open(sync);
            Assert.That(ui, Is.Not.Null);
            yield return null;
            Assert.That(WardrobeUI.IsOpen);
            Assert.That(ui.Preview, Is.Not.Null);
            Assert.That(ui.Preview.Farmer.IsPreview);
            var before = ui.Working;
            for (int i = 0; i < 5 && ui.Working.Equals(before); i++) ui.SurpriseMe();
            Assert.That(ui.Working, Is.Not.EqualTo(before));
            var chosen = ui.Working;
            ui.Close(true);
            yield return null;
            Assert.That(WardrobeUI.IsOpen, Is.False);
            Assert.That(f.Current, Is.EqualTo(chosen));
            Assert.That(LoadoutStorage.TryFromJson(PlayerPrefs.GetString(LoadoutStorage.PrefsKey), out var saved));
            Assert.That(saved, Is.EqualTo(chosen));
        }

        [UnityTest]
        public IEnumerator Wardrobe_Cancel_KeepsLoadout()
        {
            var f = Lineup().Farmers[3];
            f.OfflineIsLocal = true;
            var before = f.Current;
            var ui = WardrobeUI.Open(f.GetComponent<FarmerLoadoutSync>());
            yield return null;
            ui.SurpriseMe();
            ui.Close(false);
            yield return null;
            Assert.That(f.Current, Is.EqualTo(before));
        }

        [UnityTest]
        public IEnumerator Emotes_WaveAndFaint_PlayOffline()
        {
            var f = Lineup().Farmers[0];
            f.OfflineIsLocal = true;
            var emotes = f.GetComponent<FarmerEmotes>();
            Assert.That(emotes.TryEmote(1), Is.True, "wave");
            Assert.That(emotes.TryEmote(1), Is.False, "wave is on cooldown");
            yield return new WaitForSeconds(0.3f);
            Assert.That(f.BodyAnimator.GetCurrentAnimatorStateInfo(0).IsName("Wave") || f.BodyAnimator.GetNextAnimatorStateInfo(0).IsName("Wave"));

            Assert.That(emotes.TryEmote(4), Is.True, "faint");
            yield return new WaitForSeconds(0.2f);
            Assert.That(f.BodyAnimator.GetBool(FarmerVisual.FaintedParam), Is.True);
            yield return new WaitForSeconds(2.1f);
            Assert.That(f.BodyAnimator.GetBool(FarmerVisual.FaintedParam), Is.False, "gets up after 2 s");
        }

        [UnityTest]
        public IEnumerator WardrobeSpawnsAtMarker()
        {
            yield return null;
            Assert.That(Object.FindObjectsByType<WardrobeInteractable>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
        }

        // ---- QA captures (explicit: run with -testFilter Capture) ----

        static string EvidenceDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../../production/qa/evidence"));

        [UnityTest, Explicit("QA screenshot; needs a GPU (no -nographics)")]
        public IEnumerator Capture_Lineup()
        {
            var lineup = Lineup();
            lineup.ApplySet(WardrobeTestLineup.SetA, WardrobeTestLineup.Names);
            yield return new WaitForSeconds(0.5f);
            Save(RenderCamera(Camera.main, 1600, 900), "unity-customization-lineup-a.png");
            lineup.ApplySet(WardrobeTestLineup.SetB, new[] { "Pumpkin Pete", "Tex", "Daisy", "Grandpa" });
            lineup.Farmers[1].GetComponent<FarmerEmotes>(); // pose variety
            lineup.Farmers[0].PlayEmote("wave");
            lineup.Farmers[2].PlayEmote("dance");
            yield return new WaitForSeconds(0.6f);
            Save(RenderCamera(Camera.main, 1600, 900), "unity-customization-lineup-b.png");
        }

        [UnityTest, Explicit("QA screenshot; needs a GPU (no -nographics)")]
        public IEnumerator Capture_WardrobeUI()
        {
            var f = Lineup().Farmers[0];
            f.OfflineIsLocal = true;
            var sync = f.GetComponent<FarmerLoadoutSync>();
            var panel = Object.Instantiate(f.Catalog.wardrobePanel);
            var uiRt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
            panel.targetTexture = uiRt;
            panel.clearColor = true;
            panel.colorClearValue = new Color(0, 0, 0, 0);
            var ui = WardrobeUI.Open(sync, panel);
            ui.SetWorking(WardrobeTestLineup.SetA[1]);
            yield return new WaitForSeconds(0.6f);
            ui.Preview.RenderNow();
            yield return null;
            var world = RenderCamera(Camera.main, 1600, 900);
            var overlay = ReadRt(uiRt);
            Save(Composite(world, overlay), "unity-customization-wardrobe.png");
            ui.Close(false);
            uiRt.Release();
        }

        static Texture2D RenderCamera(Camera cam, int w, int h)
        {
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            var req = new RenderPipeline.StandardRequest { destination = rt };
            for (int i = 0; i < 2; i++)
            {
                if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
                else { cam.targetTexture = rt; cam.Render(); cam.targetTexture = null; }
            }
            var tex = ReadRt(rt);
            rt.Release();
            return tex;
        }

        static Texture2D ReadRt(RenderTexture rt)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            return tex;
        }

        static Texture2D Composite(Texture2D under, Texture2D over)
        {
            var a = under.GetPixels(); var b = over.GetPixels();
            for (int i = 0; i < a.Length; i++) a[i] = Color.Lerp(a[i], new Color(b[i].r, b[i].g, b[i].b, 1f), b[i].a);
            under.SetPixels(a); under.Apply();
            return under;
        }

        static void Save(Texture2D tex, string file)
        {
            Directory.CreateDirectory(EvidenceDir);
            string path = Path.Combine(EvidenceDir, file);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Debug.Log("[CustomizationQA] wrote " + path);
        }
    }
}
