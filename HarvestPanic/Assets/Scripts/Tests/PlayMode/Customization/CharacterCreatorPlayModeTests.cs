using System;
using System.Collections;
using System.Linq;
using HarvestPanic.Core;
using HarvestPanic.Customization;
using HarvestPanic.UI;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HarvestPanic.Tests.PlayMode.Customization
{
    /// <summary>
    /// Character creator flows: first run from the main menu -> Confirm -> loadout v2 + Farm Name persisted -> the saved
    /// loadout replicates host -> client through an in-process pair of NetworkManagers (own random UDP port); every tab
    /// builds; locked items cannot be picked; Revert; full-cover hats hide hair; outfits reach the first-person arms.
    /// </summary>
    public class CharacterCreatorPlayModeTests
    {
        const string WardrobeScene = "Assets/Scenes/Dev/WardrobeTest.unity";
        PrefsBackup _prefs;
        NetworkManager _host, _client;

        [SetUp]
        public void SetUp()
        {
            _prefs = PrefsBackup.Take();
            SettingsService.UseStore(new MemorySettingsStore());
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (WardrobeUI.Current != null) WardrobeUI.Current.Close(false);
            CharacterCreatorService.AllowFirstRunInBatchMode = false;
            if (_client != null) { _client.Shutdown(); yield return null; Object.Destroy(_client.gameObject); _client = null; }
            if (_host != null) { _host.Shutdown(); yield return null; Object.Destroy(_host.gameObject); _host = null; }
            yield return null;
            SettingsService.UseStore(null);
            _prefs.Restore();
            InputModal.Clear();
        }

        static IEnumerator LoadScene(string path)
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
#endif
            yield return null; yield return null;
        }

        static IEnumerator WaitFor(Func<bool> cond, float timeout, string what)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!cond())
            {
                if (Time.realtimeSinceStartup > end) Assert.Fail("Timed out waiting for " + what);
                yield return null;
            }
        }

        static CosmeticCatalog Catalog() => CustomizationLink.Catalog;

        static ushort IdOf(CosmeticCatalog cat, string key)
        {
            var it = cat.FindItem(key, out _);
            Assert.That(it, Is.Not.Null, key);
            return it.id;
        }

        // ---- First run -> confirm -> persist -> replicate ----------------------------------------------------------------

        [UnityTest]
        public IEnumerator FirstRun_MainMenuOpensCreator_ConfirmPersists_AndReplicatesToClient()
        {
            LogAssert.ignoreFailingMessages = true; // menu backdrop / session warnings are irrelevant here
            PlayerPrefs.DeleteKey(LoadoutStorage.CreatedKey);
            PlayerPrefs.DeleteKey(LoadoutStorage.PrefsKey);
            PlayerPrefs.DeleteKey(LoadoutStorage.NameKey);
            Services.Clear();
            Services.Register<ISessionService>(new FakeSessionService { ConnectDelay = 0f, LocalName = "Tester" });
            CharacterCreatorService.EnsureRegistered();
            CharacterCreatorService.AllowFirstRunInBatchMode = true;
            Assert.That(Services.Get<ICharacterCreator>().NeedsFirstRun, "fresh machine needs a farmer");

            yield return LoadScene("Assets/Scenes/MainMenu.unity");
            yield return WaitFor(() => WardrobeUI.Current != null, 5f, "first-run creator to open from the main menu");
            var ui = WardrobeUI.Current;
            Assert.That(ui.Context, Is.EqualTo(CharacterCreatorContext.FirstRun));
            Assert.That(Services.Get<ICharacterCreator>().IsOpen);
            Assert.That(InputModal.IsBlocking, "creator holds input");
            Assert.That(ui.TabCount, Is.EqualTo(Enum.GetValues(typeof(CreatorTab)).Length));

            var cat = ui.Catalog;
            Assert.That(ui.Select(CosmeticSlot.Hat, IdOf(cat, "hat.cone")));
            Assert.That(ui.Select(CosmeticSlot.Hair, IdOf(cat, "hair.mohawk")));
            Assert.That(ui.Select(CosmeticSlot.Outfit, IdOf(cat, "outfit.raincoat")));
            Assert.That(ui.Select(CosmeticSlot.Head, IdOf(cat, "head.potatonose")));
            Assert.That(ui.Select(CosmeticSlot.Back, IdOf(cat, "back.cape")));
            var l = ui.Working; l.Body = 3; l.Primary = 5; l.Secondary = 15; l.HairColor = 23; l.NameTag = 2;
            ui.SetWorking(l);
            ui.SetFarmName("  Sir   Turnip ");
            Assert.That(ui.ResolvedFarmName, Is.EqualTo("Sir Turnip"));
            yield return null;
            Assert.That(ui.Preview.Farmer.Current, Is.EqualTo(ui.Working), "turntable shows the working loadout");
            var chosen = ui.Working;

            ui.Close(true);
            yield return null;
            Assert.That(WardrobeUI.IsOpen, Is.False);
            Assert.That(Services.Get<ICharacterCreator>().NeedsFirstRun, Is.False, "confirmed: no first run next launch");
            Assert.That(LoadoutStorage.TryFromJson(PlayerPrefs.GetString(LoadoutStorage.PrefsKey), out var saved, out int version));
            Assert.That(version, Is.EqualTo(LoadoutStorage.SaveVersion));
            Assert.That(saved, Is.EqualTo(chosen), "loadout v2 persisted");
            Assert.That(PlayerNames.Read(), Is.EqualTo("Sir Turnip"), "Farm Name stored through IPlayerNameService");
            Assert.That(SettingsService.Current.PlayerName, Is.EqualTo("Sir Turnip"), "Settings and creator agree");

            // Replicate: in-process host + client, each spawning a FarmerBody player that sends its saved loadout on spawn.
            yield return LoadScene(WardrobeScene);
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Farmer/Test/FarmerNetTest.prefab");
            Assert.That(prefab, Is.Not.Null, "run FarmerAssetBuilder.BuildAll");
            ushort port = (ushort)UnityEngine.Random.Range(21000, 59000);
            _host = MakeNm("NM Host", port, prefab);
            yield return null;
            Assert.That(_host.StartHost(), "StartHost");
            _client = MakeNm("NM Client", port, prefab);
            yield return null;
            Assert.That(_client.StartClient(), "StartClient");
            yield return WaitFor(() => _client.IsConnectedClient, 10f, "client connect");
            ulong clientId = _client.LocalClientId;

            FarmerLoadoutSync OnHost(ulong owner) => FarmerLoadoutSync.Active.FirstOrDefault(s => s != null && s.IsSpawned && s.NetworkManager == _host && s.OwnerClientId == owner);
            FarmerLoadoutSync OnClient(ulong owner) => FarmerLoadoutSync.Active.FirstOrDefault(s => s != null && s.IsSpawned && s.NetworkManager == _client && s.OwnerClientId == owner);
            yield return WaitFor(() => OnHost(clientId) != null && OnHost(clientId).Loadout.Hat == chosen.Hat && OnClient(0) != null && OnClient(0).Loadout.Hat == chosen.Hat, 10f, "both farmers replicated");

            var remoteOnHost = OnHost(clientId).Loadout;
            var expectClient = chosen; expectClient.NameTag = (byte)((chosen.NameTag + 1) % cat.nameTags.Count);
            Assert.That(remoteOnHost, Is.EqualTo(expectClient), "client's saved loadout (all v2 fields) on the host; duplicate tag -> next free (join order)");
            Assert.That(OnClient(0).Loadout, Is.EqualTo(chosen), "host's farmer on the client");
            var hostView = OnHost(clientId).Visual;
            Assert.That(hostView.Current, Is.EqualTo(expectClient), "host applied it to the body");
            Assert.That(hostView.Accessories.Any(a => a.name == "Cosmetic_outfit.raincoat"), "outfit parts are visible to others");

            // Later wardrobe change on the client goes through the server and reaches the host.
            var changed = OnClient(clientId).Loadout;
            changed.Hat = IdOf(cat, "hat.chef"); changed.Outfit = (byte)IdOf(cat, "outfit.gingham");
            Assert.That(OnClient(clientId).RequestLoadout(changed));
            yield return WaitFor(() => OnHost(clientId).Loadout.Hat == changed.Hat && OnHost(clientId).Loadout.Outfit == changed.Outfit, 5f, "wardrobe change replicated");
#endif
            LogAssert.ignoreFailingMessages = false;
        }

        static NetworkManager MakeNm(string name, ushort port, GameObject playerPrefab)
        {
            var go = new GameObject(name);
            var t = go.AddComponent<UnityTransport>();
            t.SetConnectionData("127.0.0.1", port);
            var nm = go.AddComponent<NetworkManager>();
            nm.NetworkConfig = new NetworkConfig { NetworkTransport = t, EnableSceneManagement = false, PlayerPrefab = playerPrefab };
            nm.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = playerPrefab });
            return nm;
        }

        // ---- Screen behaviour --------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Creator_EveryTabBuilds_LockedItemsRefused_RevertRestores()
        {
            yield return LoadScene(WardrobeScene);
            var cat = Catalog();
            LoadoutStorage.Save(WardrobeTestLineup.SetA[0]);
            var ui = WardrobeUI.OpenStandalone(cat, CharacterCreatorContext.MainMenu);
            Assert.That(ui, Is.Not.Null);
            yield return null;
            foreach (CreatorTab tab in Enum.GetValues(typeof(CreatorTab)))
            {
                ui.ShowTab(tab);
                yield return null;
                Assert.That(ui.CurrentTab, Is.EqualTo(tab));
                Assert.That(ui.VisibleCardCount, Is.GreaterThan(0), tab + " shows choices");
            }
            ui.ShowTab(CreatorTab.Hat);
            Assert.That(ui.VisibleCardCount, Is.EqualTo(cat.Items(CosmeticSlot.Hat).Count + 1), "every hat + None");

            var original = ui.Working;
            bool unlockAll = cat.unlockAllCosmetics;
            try
            {
                cat.unlockAllCosmetics = false;
                Assert.That(ui.IsLocked(CosmeticSlot.Hat, IdOf(cat, "hat.cone")), "Letter hat locked at the ship gate");
                Assert.That(ui.Select(CosmeticSlot.Hat, IdOf(cat, "hat.cone")), Is.False);
                Assert.That(ui.Working.Hat, Is.EqualTo(original.Hat), "locked pick ignored");
                Assert.That(ui.Select(CosmeticSlot.Hat, IdOf(cat, "hat.cowboy")), "starter hat allowed");
            }
            finally { cat.unlockAllCosmetics = unlockAll; }

            Assert.That(ui.Select(CosmeticSlot.Hair, IdOf(cat, "hair.afro")));
            ui.SetFarmName("Changed Name");
            Assert.That(ui.Working, Is.Not.EqualTo(original));
            ui.Revert();
            Assert.That(ui.Working, Is.EqualTo(original));
            ui.ShowTab(CreatorTab.Name);
            ui.SetFarmName("no");
            StringAssert.Contains("Too short", ui.NameStatusText);
            ui.SetFarmName("Big Ass");
            Assert.That(ui.ResolvedFarmName, Does.StartWith("Farmhand #"), "GDD: rejected name -> Farmhand #N");

            ui.Close(false);
            Assert.That(CharacterCreatorService.Instance.IsOpen, Is.True, "IsOpen holds for the closing frame (Esc not handled twice)");
            yield return null;
            Assert.That(CharacterCreatorService.Instance.IsOpen, Is.False);
            Assert.That(InputModal.IsBlocking, Is.False);
            Assert.That(LoadoutStorage.TryFromJson(PlayerPrefs.GetString(LoadoutStorage.PrefsKey), out var saved) && saved.Equals(WardrobeTestLineup.SetA[0]), "cancel saves nothing");
        }

        [UnityTest]
        public IEnumerator Randomize_StaysValidAndChangesTheFarmer()
        {
            yield return LoadScene(WardrobeScene);
            var cat = Catalog();
            var ui = WardrobeUI.OpenStandalone(cat, CharacterCreatorContext.MainMenu);
            yield return null;
            var before = ui.Working;
            for (int i = 0; i < 6 && ui.Working.Equals(before); i++) ui.Randomize();
            Assert.That(ui.Working, Is.Not.EqualTo(before));
            Assert.That(LoadoutValidator.Sanitize(ui.Working, cat, null, out var report), Is.EqualTo(ui.Working), report);
            Assert.That(ui.Working.Emote0, Is.EqualTo(before.Emote0), "emotes kept");
            Assert.That(ui.Working.NameTag, Is.EqualTo(before.NameTag), "tag kept");
            yield return null;
            Assert.That(ui.Preview.Farmer.Current, Is.EqualTo(ui.Working));
        }

        // ---- Visual rules ------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator FullCoverHatHidesHair_HatSquashesBigHair()
        {
            yield return LoadScene(WardrobeScene);
            var f = Object.FindFirstObjectByType<WardrobeTestLineup>().Farmers[0];
            var cat = f.Catalog;
            var l = FarmerLoadout.Default;
            l.Hair = (byte)IdOf(cat, "hair.afro"); l.Hat = 0;
            f.Apply(l);
            yield return null;
            var hair = f.Accessories.FirstOrDefault(a => a.name == "Cosmetic_hair.afro");
            Assert.That(hair, Is.Not.Null);
            int partsBare = hair.transform.childCount;

            l.Hat = IdOf(cat, "hat.straw");
            f.Apply(l);
            yield return null;
            hair = f.Accessories.FirstOrDefault(a => a.name == "Cosmetic_hair.afro");
            Assert.That(hair, Is.Not.Null);
            Assert.That(hair.transform.childCount, Is.LessThan(partsBare), "the puffy top hides under a hat");

            l.Hat = IdOf(cat, "hat.pumpkin");
            f.Apply(l);
            yield return null;
            Assert.That(f.Accessories.Any(a => a != null && a.name.StartsWith("Cosmetic_hair.")), Is.False, "full-cover hats hide hair");
        }

        [UnityTest]
        public IEnumerator Outfit_SleevesReachFirstPersonArms_AndHeadShapeKeepsHatProportions()
        {
            yield return LoadScene(WardrobeScene);
            var cat = Catalog();
            var root = new GameObject("FakePlayer");
            var hooks = root.AddComponent<FakePlayerHooks>();
            var arms = new GameObject("FirstPersonArms").AddComponent<FakeArms>();
            arms.transform.SetParent(root.transform, false);
            hooks.Arms = arms.transform;
            var body = Object.Instantiate(cat.farmerPrefab, root.transform);
            WardrobePreview.StripNetworking(body);
            var v = body.GetComponent<FarmerVisual>();
            yield return null;

            var l = FarmerLoadout.Default; l.Skin = 9; l.Secondary = 14; l.Primary = 1; l.Accent = 13;
            l.Outfit = (byte)IdOf(cat, "outfit.tank");
            v.Apply(l);
            Assert.That(arms.Shirt, Is.EqualTo(cat.SkinColor(9)), "Muscle Tank: bare arms in first person");
            Assert.That(arms.Glove, Is.EqualTo(cat.PaletteColor(13)));
            Assert.That(arms.Skin, Is.EqualTo(cat.SkinColor(9)));
            l.Outfit = (byte)IdOf(cat, "outfit.raincoat");
            v.Apply(l);
            Assert.That(arms.Shirt, Is.EqualTo(cat.PaletteColor(1)), "Rain Slicker sleeves follow the overalls colour");
            l.Outfit = 0;
            v.Apply(l);
            Assert.That(arms.Shirt, Is.EqualTo(v.TintsFor(l).Secondary), "plain overalls: shirt sleeves (the painted shirt on its home swatch)");

            var hatScale = v.HeadAttachPoint.lossyScale;
            l.Head = (byte)IdOf(cat, "head.long");
            v.Apply(l);
            yield return null;
            var s = v.HeadAttachPoint.lossyScale;
            Assert.That(s.y / s.x, Is.EqualTo(hatScale.y / hatScale.x).Within(0.06f), "hats are not stretched by a long head");
            Object.Destroy(root);
        }

        sealed class FakePlayerHooks : MonoBehaviour, IPlayerHooks
        {
            public Transform Arms;
            public ulong ClientId => 0;
            public bool IsLocal => true;
            public Renderer BodyRenderer => null;
            public Animator BodyAnimator => null;
            public Transform FirstPersonArms => Arms;
            public Transform HeadAttachPoint => null;
            public void PlayEmote(string emoteId) { }
            public void Faint(bool fainted) { }
        }

        sealed class FakeArms : MonoBehaviour, IFirstPersonArmsSkin
        {
            public Color Shirt, Glove, Skin;
            public void ApplyArmColors(Color shirt, Color glove, Color skin) { Shirt = shirt; Glove = glove; Skin = skin; }
        }
    }
}
