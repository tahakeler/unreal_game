using System.Collections;
using System.Linq;
using System.Reflection;
using HarvestPanic.Customization;
using HarvestPanic.Networking;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HarvestPanic.Tests.PlayMode.Customization
{
    /// <summary>
    /// The spawned player wears the hero Tripo farmer (valid humanoid avatar, hero controller, recolour shader) and its
    /// colours replicate: the host picks a loadout and an in-process client's copy of the host's player shows the same
    /// overalls/shirt/skin in its property block. Host = real SessionService in Muddlebury on a random LAN port.
    /// </summary>
    public class HeroFarmerNetworkTests
    {
        ushort _savedPort;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true; // client-side scene/spawn warnings are irrelevant here
            var cfg = NetworkingConfig.Load();
            _savedPort = cfg.lanPort;
            cfg.lanPort = (ushort)Random.Range(20000, 40000);
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Muddlebury.unity", new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return null;
#endif
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var nm in Object.FindObjectsByType<NetworkManager>(FindObjectsSortMode.None))
                if (nm != null && nm.IsListening) nm.Shutdown();
            yield return null; yield return null;
            foreach (var nm in Object.FindObjectsByType<NetworkManager>(FindObjectsSortMode.None))
                if (nm != null) Object.Destroy(nm.gameObject);
            yield return null;
            NetworkingConfig.Load().lanPort = _savedPort;
            LogAssert.ignoreFailingMessages = false;
        }

        static IEnumerator WaitFor(System.Func<bool> cond, float seconds = 20f)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!cond() && Time.realtimeSinceStartup < end) yield return null;
        }

        static NetworkManager StartClient(NetworkManager host, ushort port)
        {
            var go = new GameObject("TestClient Cora");
            go.SetActive(false);
            var utp = go.AddComponent<UnityTransport>();
            var nm = go.AddComponent<NetworkManager>();
            var cfg = new NetworkConfig();
            foreach (var f in typeof(NetworkConfig).GetFields(BindingFlags.Public | BindingFlags.Instance)) f.SetValue(cfg, f.GetValue(host.NetworkConfig));
            cfg.NetworkTransport = utp;
            cfg.ConnectionData = new ConnectionPayload { Protocol = NetworkingConfig.Load().protocolVersion, BuildVersion = Application.version, PlayerName = "Cora" }.ToBytes();
            nm.NetworkConfig = cfg;
            go.SetActive(true);
            Object.DontDestroyOnLoad(go);
            utp.SetConnectionData("127.0.0.1", port);
            Assert.IsTrue(nm.StartClient(), "client failed to start");
            nm.SceneManager.VerifySceneBeforeLoading = (_, __, ___) => false; // the host's scene is already loaded here
            return nm;
        }

        static void AssertHeroBody(FarmerVisual v, string who)
        {
            Assert.That(v.RecolourRenderers.Count, Is.GreaterThan(0), who + ": recoloured hero body");
            var smr = v.RecolourRenderers[0] as SkinnedMeshRenderer;
            Assert.That(smr != null && smr.sharedMesh != null && smr.sharedMesh.name.Contains("SK_Farmer_Hero"), who + ": hero mesh");
            Assert.That(smr.sharedMaterial.shader.name, Is.EqualTo("HarvestPanic/Characters/FarmerRecolour"), who);
            var anim = v.BodyAnimator;
            Assert.That(anim.avatar != null && anim.avatar.isValid && anim.avatar.isHuman, who + ": valid humanoid avatar");
            Assert.That(anim.runtimeAnimatorController != null && anim.runtimeAnimatorController.name == "FarmerHero", who + ": hero controller");
        }

        static bool Near(Color a, Color b) => Mathf.Abs(a.r - b.r) < 0.004f && Mathf.Abs(a.g - b.g) < 0.004f && Mathf.Abs(a.b - b.b) < 0.004f;

        static bool Shows(FarmerVisual v, string prop, Color want)
        {
            var mpb = new MaterialPropertyBlock();
            v.RecolourRenderers[0].GetPropertyBlock(mpb);
            var c = mpb.GetColor(prop);
            return Near(c, want) || Near(c, want.linear);
        }

        [UnityTest]
        public IEnumerator SpawnedPlayer_IsTheHero_AndItsColoursReplicateToAClient()
        {
            // The in-process client refuses the host's scene load (it shares this process), so in-scene placed objects
            // fail to soft-sync on it; only the dynamically spawned players matter here.
            LogAssert.ignoreFailingMessages = true;
            var cfg = NetworkingConfig.Load();
            var session = Object.Instantiate(cfg.networkManagerPrefab).GetComponent<SessionService>();
#if UNITY_EDITOR
            session.HostInPlaceForEditor();
#endif
            yield return WaitFor(() => session.IsHost && FarmerLoadoutSync.Local != null && FarmerLoadoutSync.Local.IsSpawned);
            var hostSync = FarmerLoadoutSync.Local;
            Assert.That(hostSync, Is.Not.Null, "host player spawned");
            AssertHeroBody(hostSync.Visual, "host");

            var look = new FarmerLoadout { Body = 0, Primary = 2, Secondary = 20, Accent = 12, Skin = 0, NameTag = 3, HairColor = 11, Hat = 1, Emote0 = 1, Emote1 = 2, Emote2 = 3, Emote3 = 4 };
            Assert.That(hostSync.RequestLoadout(look), "loadout accepted");
            yield return WaitFor(() => hostSync.Loadout.Equals(look), 5f);
            Assert.That(hostSync.Loadout, Is.EqualTo(look));

            var client = StartClient(session.Manager, cfg.lanPort);
            ulong id = hostSync.NetworkObjectId;
            yield return WaitFor(() => client.IsConnectedClient && client.SpawnManager != null && client.SpawnManager.SpawnedObjects.ContainsKey(id));
            Assert.That(client.SpawnManager.SpawnedObjects.ContainsKey(id), "client sees the host's player");
            var remote = client.SpawnManager.SpawnedObjects[id].GetComponentInChildren<FarmerLoadoutSync>();
            Assert.That(remote, Is.Not.Null);
            Assert.That(remote, Is.Not.SameAs(hostSync), "the client's own copy");
            yield return WaitFor(() => remote.Visual.Current.Equals(look), 5f);
            Assert.That(remote.Visual.Current, Is.EqualTo(look), "loadout replicated");
            AssertHeroBody(remote.Visual, "client copy");
            var cat = remote.Visual.Catalog;
            Assert.That(Shows(remote.Visual, "_PrimaryColor", cat.PaletteColor(look.Primary)), "overalls colour on the client");
            Assert.That(Shows(remote.Visual, "_SecondaryColor", cat.PaletteColor(look.Secondary)), "shirt colour on the client");
            Assert.That(Shows(remote.Visual, "_SkinColor", cat.SkinColor(look.Skin)), "skin tone on the client");
            Assert.That(Shows(remote.Visual, "_BandColor", cat.NameTag(look.NameTag).color), "hat band = name-tag colour on the client");

            // a change after joining replicates too
            var next = look; next.Primary = 3;
            Assert.That(hostSync.RequestLoadout(next));
            yield return WaitFor(() => remote.Visual.Current.Equals(next), 5f);
            Assert.That(Shows(remote.Visual, "_PrimaryColor", cat.PaletteColor(3)), "re-dress replicates");
        }
    }
}
