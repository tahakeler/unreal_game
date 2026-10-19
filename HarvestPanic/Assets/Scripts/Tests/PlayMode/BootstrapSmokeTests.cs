using System.Collections;
using HarvestPanic.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HarvestPanic.Tests.PlayMode
{
    public class BootstrapSmokeTests
    {
        [UnityTest]
        public IEnumerator BootstrapScene_LoadsAndHasCamera()
        {
            yield return SceneManager.LoadSceneAsync(GameInfo.BootstrapScene, LoadSceneMode.Single);
            yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(GameInfo.BootstrapScene));
            Assert.That(Object.FindFirstObjectByType<Camera>(), Is.Not.Null);
        }
    }
}
