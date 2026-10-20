using System.IO;
using HarvestPanic.Audio;
using HarvestPanic.Core;
using NUnit.Framework;

namespace HarvestPanic.Tests.EditMode.Audio
{
    /// <summary>Polish-2: bruise cues, night layer, appended events, client cue feed.</summary>
    public class Polish2AudioTests
    {
        [Test]
        public void BruiseCues_ThudCrunchAndRegister()
        {
            Assert.IsNull(BruiseCues.CueFor(0f, 0, out bool reg)); Assert.IsFalse(reg);
            Assert.AreEqual(AudioEvents.BruiseThud, BruiseCues.CueFor(2f, 0, out reg)); Assert.IsFalse(reg);
            Assert.AreEqual(AudioEvents.BruiseCrunch, BruiseCues.CueFor(20f, 3, out reg)); Assert.IsTrue(reg);
        }

        [Test]
        public void NightLayer_OnlyAtNight()
        {
            Assert.AreEqual(1f, NightLayerRules.Target("Dusk"));
            foreach (var p in new[] { "Morning", "Workday", "Payout", "Sunday", "None" }) Assert.AreEqual(0f, NightLayerRules.Target(p));
        }

        [Test]
        public void NewEvents_AreAppendedAndHaveClips()
        {
            int n = AudioEvents.All.Length;
            Assert.AreEqual(AudioEvents.MusNightThreat, AudioEvents.All[n - 1].Name);
            Assert.AreEqual(AudioEvents.StgLandlordTuba, AudioEvents.All[n - 2].Name);
            Assert.AreEqual(AudioEvents.GoatBleat, AudioEvents.All[n - 3].Name, "older ids unchanged");
            foreach (var e in new[] { AudioEvents.MusNightThreat, AudioEvents.StgLandlordTuba, AudioEvents.BruiseCrunch, AudioEvents.BruiseRegister })
                Assert.IsTrue(System.Array.Exists(Directory.GetFiles("Assets/Audio/Resources/Audio/" + e), f => f.EndsWith(".wav") || f.EndsWith(".ogg")), e);
        }

        [Test]
        public void NoiseCueFeed_RaisesToSubscribers()
        {
            NoiseEvent got = default; int calls = 0;
            void H(NoiseEvent e) { got = e; calls++; }
            NoiseCueFeed.OnNoiseCue += H;
            try { NoiseCueFeed.Raise(new NoiseEvent { Source = NoiseSource.Shout, Tags = NoiseTag.Whistle }); }
            finally { NoiseCueFeed.OnNoiseCue -= H; }
            Assert.AreEqual(1, calls); Assert.AreEqual(NoiseTag.Whistle, got.Tags);
        }
    }
}
