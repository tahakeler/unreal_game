using System;
using System.Collections;
using System.IO;
using HarvestPanic.Core;
using HarvestPanic.Customization;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace HarvestPanic.Tests.PlayMode.Customization
{
    /// <summary>
    /// QA captures for feature/character-creator (set HP_QA_DIR; run batchmode WITHOUT -nographics, skipped otherwise):
    /// every creator tab at 1920x1080, the turntable from several angles and mid-emote, locked items, a colour-blind mode,
    /// the in-lobby wardrobe context, and the dev lineup wearing the new heads/hair/outfits.
    /// </summary>
    public class CharacterCreatorScreenshots
    {
        const string ScenePath = "Assets/Scenes/Dev/WardrobeTest.unity";
        PrefsBackup _prefs;

        static string Dir()
        {
            string dir = Environment.GetEnvironmentVariable("HP_QA_DIR");
            if (string.IsNullOrEmpty(dir) || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("QA screenshot; set HP_QA_DIR and run with a GPU");
            Directory.CreateDirectory(dir);
            return dir;
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _prefs = PrefsBackup.Take();
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#endif
            yield return null; yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (WardrobeUI.Current != null) WardrobeUI.Current.Close(false);
            UiAccessibility.Reset();
            _prefs.Restore();
            yield return null;
        }

        static CosmeticCatalog Catalog()
        {
            var lineup = Object.FindFirstObjectByType<WardrobeTestLineup>();
            return lineup.Farmers[0].Catalog;
        }

        static (PanelSettings panel, RenderTexture rt) OffscreenPanel(CosmeticCatalog cat)
        {
            var panel = Object.Instantiate(cat.wardrobePanel);
            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            panel.targetTexture = rt;
            panel.clearColor = true;
            panel.colorClearValue = new Color(0, 0, 0, 0);
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            return (panel, rt);
        }

        static IEnumerator Settle(WardrobeUI ui, float min = 0.7f)
        {
            float end = Time.realtimeSinceStartup + 3f, minEnd = Time.realtimeSinceStartup + min;
            while ((ui.Icons != null && ui.Icons.Pending > 0 && Time.realtimeSinceStartup < end) || Time.realtimeSinceStartup < minEnd) yield return null;
            yield return null; yield return null;
        }

        [UnityTest]
        public IEnumerator Capture_CreatorTabsAndTurntable()
        {
            string dir = Dir();
            var cat = Catalog();
            LoadoutStorage.Save(WardrobeTestLineup.SetA[0]);
            var (panel, rt) = OffscreenPanel(cat);
            var ui = WardrobeUI.OpenStandalone(cat, CharacterCreatorContext.FirstRun, panel);
            Assert.That(ui, Is.Not.Null);
            ui.SetFarmName("Grandpa Junior");
            yield return Settle(ui, 1.2f);

            int i = 0;
            foreach (CreatorTab tab in Enum.GetValues(typeof(CreatorTab)))
            {
                ui.ShowTab(tab);
                yield return Settle(ui, 0.9f);
                Save(ReadRt(rt), Path.Combine(dir, $"creator_{i++:00}_{tab.ToString().ToLowerInvariant()}.png"));
            }

            // Turntable close-ups straight from the preview render texture.
            ui.ShowTab(CreatorTab.Outfit);
            yield return Settle(ui, 1.0f);
            Save(ReadRt(ui.Preview.Texture), Path.Combine(dir, "turntable_1_front.png"));
            ui.Preview.Orbit(-70f, 0f);
            yield return new WaitForSeconds(0.5f);
            Save(ReadRt(ui.Preview.Texture), Path.Combine(dir, "turntable_2_side.png"));
            ui.ShowTab(CreatorTab.Back);
            yield return Settle(ui, 1.0f);
            Save(ReadRt(ui.Preview.Texture), Path.Combine(dir, "turntable_3_back.png"));
            ui.ShowTab(CreatorTab.Hat);
            yield return Settle(ui, 1.0f);
            Save(ReadRt(ui.Preview.Texture), Path.Combine(dir, "turntable_4_head.png"));
            ui.ShowTab(CreatorTab.Emotes);
            yield return Settle(ui, 0.6f);
            foreach (var key in new[] { "wave", "dance", "shrug", "chickendance", "salute" })
            {
                ui.Preview.PlayEmote(key);
                yield return new WaitForSeconds(key == "wave" ? 0.6f : 0.75f);
                Save(ReadRt(ui.Preview.Texture), Path.Combine(dir, "turntable_emote_" + key + ".png"));
                yield return new WaitForSeconds(0.9f);
            }

            // Randomize a few times (themes, chaos) for the gallery.
            for (int r = 0; r < 3; r++)
            {
                ui.ShowTab(CreatorTab.Body);
                ui.Randomize();
                yield return Settle(ui, 1.0f);
                Save(ReadRt(rt), Path.Combine(dir, $"creator_random_{r + 1}.png"));
            }
            ui.Close(false);
            yield return null;
            rt.Release();
        }

        [UnityTest]
        public IEnumerator Capture_LockedColourblindAndWardrobe()
        {
            string dir = Dir();
            var cat = Catalog();
            LoadoutStorage.Save(WardrobeTestLineup.SetB[1]);
            bool unlockAll = cat.unlockAllCosmetics;
            try
            {
                cat.unlockAllCosmetics = false; // ship gate: show locks and unlock hints
                var (panel, rt) = OffscreenPanel(cat);
                var ui = WardrobeUI.OpenStandalone(cat, CharacterCreatorContext.MainMenu, panel);
                ui.ShowTab(CreatorTab.Hat);
                yield return Settle(ui, 1.2f);
                Save(ReadRt(rt), Path.Combine(dir, "creator_locked_hats.png"));
                ui.ShowTab(CreatorTab.Emotes);
                yield return Settle(ui, 0.8f);
                Save(ReadRt(rt), Path.Combine(dir, "creator_locked_emotes.png"));
                ui.Close(false);
                yield return null;
                cat.unlockAllCosmetics = unlockAll;

                UiAccessibility.Set(UiColorblind.Deuteranopia, false, 1f, 1);
                ui = WardrobeUI.OpenStandalone(cat, CharacterCreatorContext.MainMenu, panel);
                ui.ShowTab(CreatorTab.Colours);
                yield return Settle(ui, 1.0f);
                Save(ReadRt(rt), Path.Combine(dir, "creator_colourblind_deutan_colours.png"));
                ui.ShowTab(CreatorTab.Name);
                ui.SetFarmName("x");
                yield return Settle(ui, 0.6f);
                Save(ReadRt(rt), Path.Combine(dir, "creator_colourblind_deutan_name_invalid.png"));
                ui.Close(false);
                yield return null;
                UiAccessibility.Reset();

                // Wardrobe context over the running world (lineup farmer as the local player).
                var lineup = Object.FindFirstObjectByType<WardrobeTestLineup>();
                var f = lineup.Farmers[1];
                f.OfflineIsLocal = true;
                ui = WardrobeUI.Open(f.GetComponent<FarmerLoadoutSync>(), CharacterCreatorContext.Wardrobe, panel);
                yield return Settle(ui, 1.2f);
                var world = RenderCamera(Camera.main, 1920, 1080);
                Save(Composite(world, ReadRt(rt)), Path.Combine(dir, "creator_wardrobe_context.png"));
                ui.Close(false);
                rt.Release();
            }
            finally { cat.unlockAllCosmetics = unlockAll; }
        }

        [UnityTest]
        public IEnumerator Capture_LineupNewLooks()
        {
            string dir = Dir();
            var lineup = Object.FindFirstObjectByType<WardrobeTestLineup>();
            lineup.ApplySet(WardrobeTestLineup.SetC, new[] { "Rusty", "Pip", "Big Bert", "Marge" });
            yield return new WaitForSeconds(0.5f);
            Save(RenderCamera(Camera.main, 1600, 900), Path.Combine(dir, "lineup_c_heads_hair_outfits.png"));
            lineup.ApplySet(WardrobeTestLineup.SetB, new[] { "Pumpkin Pete", "Tex", "Daisy", "Grandpa" });
            yield return new WaitForSeconds(0.5f);
            Save(RenderCamera(Camera.main, 1600, 900), Path.Combine(dir, "lineup_b_bodies.png"));
        }

        // ---- capture helpers ----

        internal static Texture2D RenderCamera(Camera cam, int w, int h)
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

        internal static Texture2D ReadRt(RenderTexture rt)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            return tex;
        }

        internal static Texture2D Composite(Texture2D under, Texture2D over)
        {
            var a = under.GetPixels(); var b = over.GetPixels();
            for (int i = 0; i < a.Length && i < b.Length; i++) a[i] = Color.Lerp(a[i], new Color(b[i].r, b[i].g, b[i].b, 1f), b[i].a);
            under.SetPixels(a); under.Apply();
            return under;
        }

        internal static void Save(Texture2D tex, string path)
        {
            var px = tex.GetPixels32();
            for (int i = 0; i < px.Length; i++) px[i].a = 255;
            tex.SetPixels32(px); tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Debug.Log("[CreatorQA] wrote " + path);
        }
    }

    /// <summary>Saves and restores the PlayerPrefs keys the creator writes.</summary>
    public sealed class PrefsBackup
    {
        static readonly string[] Keys = { LoadoutStorage.PrefsKey, LoadoutStorage.NameKey };
        readonly string[] _values = new string[Keys.Length];
        int _created = -1;

        public static PrefsBackup Take()
        {
            var b = new PrefsBackup();
            for (int i = 0; i < Keys.Length; i++) b._values[i] = PlayerPrefs.HasKey(Keys[i]) ? PlayerPrefs.GetString(Keys[i]) : null;
            b._created = PlayerPrefs.HasKey(LoadoutStorage.CreatedKey) ? PlayerPrefs.GetInt(LoadoutStorage.CreatedKey) : -1;
            return b;
        }

        public void Restore()
        {
            for (int i = 0; i < Keys.Length; i++)
            {
                if (_values[i] == null) PlayerPrefs.DeleteKey(Keys[i]); else PlayerPrefs.SetString(Keys[i], _values[i]);
            }
            if (_created < 0) PlayerPrefs.DeleteKey(LoadoutStorage.CreatedKey); else PlayerPrefs.SetInt(LoadoutStorage.CreatedKey, _created);
            PlayerPrefs.Save();
        }
    }
}
