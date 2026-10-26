using HarvestPanic.Core;
using NUnit.Framework;

namespace HarvestPanic.Tests.EditMode
{
    public class BatchModeSilenceTests
    {
        [Test] public void NonBatch_IsNeverSilenced() => Assert.IsFalse(BatchModeSilence.ShouldSilence(false, new string[0]));
        [Test] public void Batch_NoArg_IsSilenced() => Assert.IsTrue(BatchModeSilence.ShouldSilence(true, new[] { "Unity", "-batchmode" }));
        [Test] public void Batch_WithAudibleArg_IsNotSilenced() => Assert.IsFalse(BatchModeSilence.ShouldSilence(true, new[] { "Unity", "-hpAudible" }));
        [Test] public void Batch_NullArgs_IsSilenced() => Assert.IsTrue(BatchModeSilence.ShouldSilence(true, null));
    }
}
