using System.Collections;
using HarvestPanic.Core;
using HarvestPanic.Networking;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HarvestPanic.Tests.PlayMode
{
    public class BootstrapSmokeTests
    {
        /// <summary>
        /// Bootstrap hands off to MainMenu on its first frame, and its NetworkManager is DontDestroyOnLoad.
        /// Leaving it alive would make every later gameplay-scene test see a stale, non-listening NetworkManager
        /// (no editor auto-host, so critter/player spawners wait forever), hence the cleanup below.
        /// </summary>
        [UnityTearDown]
        public IEnumerator DestroyPersistentNetworkManager()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null)
            {
                if (nm.IsListening) nm.Shutdown();
                yield return null;
                if (nm != null) Object.Destroy(nm.gameObject);
            }
            else if (NetworkBootstrap.Instance != null) Object.Destroy(NetworkBootstrap.Instance.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BootstrapScene_LoadsAndHasCamera()
        {
            yield return SceneManager.LoadSceneAsync(GameInfo.BootstrapScene, LoadSceneMode.Single);
            yield return null;
            string active = SceneManager.GetActiveScene().name;
            Assert.That(active, Is.EqualTo(GameInfo.BootstrapScene).Or.EqualTo(NetworkingConfig.Load().menuScene),
                "Bootstrap stays loaded, or has already handed off to the menu");
            Assert.That(NetworkBootstrap.Instance, Is.Not.Null, "Bootstrap scene must bring up the persistent NetworkManager");
            yield return null;
            Assert.That(Object.FindFirstObjectByType<Camera>(), Is.Not.Null);
        }
    }
}
