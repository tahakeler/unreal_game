using HarvestPanic.Core;
using NUnit.Framework;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HarvestPanic.Tests.EditMode
{
    public class CoreSmokeTests
    {
        [Test]
        public void PlayerCount_SupportsFourPlayerCoop()
        {
            Assert.That(GameInfo.MaxPlayers, Is.EqualTo(4));
            Assert.That(GameInfo.MinPlayers, Is.LessThanOrEqualTo(GameInfo.MaxPlayers));
        }

        [Test]
        public void Graphics_UsesUniversalRenderPipeline()
        {
            Assert.That(GraphicsSettings.defaultRenderPipeline, Is.InstanceOf<UniversalRenderPipelineAsset>());
        }
    }
}
