using HarvestPanic.Audio;
using HarvestPanic.Core;
using NUnit.Framework;
using UnityEngine;

namespace HarvestPanic.Tests.EditMode.Audio
{
    public class AudioMathTests
    {
        [Test] public void Db_RoundTrips()
        {
            Assert.AreEqual(1f, AudioMath.DbToLinear(0f), 1e-5f);
            Assert.AreEqual(0.5012f, AudioMath.DbToLinear(-6f), 1e-3f);
            Assert.AreEqual(0f, AudioMath.DbToLinear(-80f));
            Assert.AreEqual(-6f, AudioMath.LinearToDb(AudioMath.DbToLinear(-6f)), 1e-3f);
            Assert.AreEqual(-80f, AudioMath.LinearToDb(0f));
        }

        [Test] public void ImpactGain_FollowsGddFormula()
        {
            Assert.AreEqual(-24f, AudioMath.ImpactGainDb(0f, 0f), 1e-4f);
            Assert.AreEqual(0f, AudioMath.ImpactGainDb(12f, 400f), 1e-4f);
            Assert.AreEqual(0f, AudioMath.ImpactGainDb(100f, 9999f), 1e-4f);
            Assert.AreEqual(-24f + 9f + 3f, AudioMath.ImpactGainDb(6f, 200f), 1e-4f);
        }

        [TestCase(0, 0)] [TestCase(-5, 0)] [TestCase(9, 0)] [TestCase(10, 1)] [TestCase(39, 3)] [TestCase(50, 5)] [TestCase(500, 5)]
        public void CoinTicks_FloorTenthCappedAtFive(int loss, int expected) => Assert.AreEqual(expected, AudioMath.CoinTicks(loss));

        [Test] public void ChaosAndLayer()
        {
            Assert.AreEqual(0f, AudioMath.ChaosLevel(0, 0, 0));
            Assert.AreEqual(1f, AudioMath.ChaosLevel(8, 2, 2), 1e-5f);
            Assert.AreEqual(1f, AudioMath.ChaosLevel(80, 20, 20), 1e-5f);
            Assert.AreEqual(0.4f, AudioMath.ChaosLevel(8, 0, 0), 1e-5f);
            Assert.AreEqual(1, AudioMath.MusicLayer(0f));
            Assert.AreEqual(1, AudioMath.MusicLayer(0.33f));
            Assert.AreEqual(2, AudioMath.MusicLayer(0.34f));
            Assert.AreEqual(3, AudioMath.MusicLayer(1f));
            Assert.AreEqual(3, AudioMath.MusicLayer(5f));
        }

        [Test] public void PitchAndSmoothing()
        {
            Assert.AreEqual(2f, AudioMath.SemitonesToPitch(12f), 1e-5f);
            Assert.AreEqual(1f, AudioMath.SemitonesToPitch(0f), 1e-6f);
            Assert.AreEqual(1f, AudioMath.SmoothAlpha(0.016f, 0f));
            Assert.AreEqual(1f - Mathf.Exp(-1f), AudioMath.SmoothAlpha(0.08f, 0.08f), 1e-5f);
        }
    }

    public class AudioEventTableTests
    {
        [Test] public void EventNamesAreUniqueAndResolvable()
        {
            for (int i = 0; i < AudioEvents.All.Length; i++) Assert.AreEqual(i, AudioEvents.IndexOf(AudioEvents.All[i].Name), AudioEvents.All[i].Name);
            Assert.AreEqual(-1, AudioEvents.IndexOf("SFX_Nope"));
            Assert.Less(AudioEvents.All.Length, ushort.MaxValue);
        }

        [Test] public void AttenuationPresetsMatchSoundBible()
        {
            void R(AttenuationPreset p, float mn, float mx) { AudioEvents.Range(p, out var a, out var b); Assert.AreEqual(mn, a); Assert.AreEqual(mx, b); }
            R(AttenuationPreset.Player, 2, 25); R(AttenuationPreset.CritterLarge, 4, 60); R(AttenuationPreset.Impact, 3, 50); R(AttenuationPreset.HazardLoud, 5, 90);
            Assert.AreEqual(8, AudioEvents.CapOf(VoiceLimit.Footsteps));
            Assert.AreEqual(12, AudioEvents.CapOf(VoiceLimit.Impacts));
        }

        [Test] public void BusMapping()
        {
            Assert.AreEqual(AudioBus.Music, AudioEvents.BusOf(MixGroup.Music));
            Assert.AreEqual(AudioBus.UI, AudioEvents.BusOf(MixGroup.UIStingers));
            Assert.AreEqual(AudioBus.Sfx, AudioEvents.BusOf(MixGroup.Critters));
            Assert.AreEqual(AudioBus.Voice, AudioEvents.BusOf(MixGroup.Voice));
        }

        [Test] public void EveryEventHasClipsInResources()
        {
            foreach (var d in AudioEvents.All)
                Assert.Greater(Resources.LoadAll<AudioClip>("Audio/" + d.Name).Length, 0, "missing clips for " + d.Name);
        }
    }

    public class NoiseRoutingTests
    {
        static string Route(NoiseSource s, NoiseTag t = NoiseTag.None, float loud = 0.5f, bool chicken = false)
        {
            var e = new NoiseEvent { Source = s, Tags = t, Loudness = loud, Location = new Vector3(1, 2, 3) };
            return NoiseRouting.TryRoute(e, chicken, out var c) ? c.Event : null;
        }

        [Test] public void RoutingTable()
        {
            Assert.AreEqual(AudioEvents.FootstepDirt, Route(NoiseSource.Footstep));
            Assert.AreEqual(AudioEvents.PlayerShout, Route(NoiseSource.Shout));
            Assert.AreEqual(AudioEvents.PigSqueal, Route(NoiseSource.Critter, NoiseTag.Squeal));
            Assert.AreEqual(AudioEvents.ChickenPanic, Route(NoiseSource.Critter, NoiseTag.Squeal, chicken: true));
            Assert.AreEqual(AudioEvents.PlankCreak, Route(NoiseSource.Hazard, NoiseTag.Creak));
            Assert.AreEqual(AudioEvents.PlankBreak, Route(NoiseSource.Hazard, NoiseTag.PlankBreak | NoiseTag.Creak));
            Assert.AreEqual(AudioEvents.SplashBig, Route(NoiseSource.Hazard, NoiseTag.Splash, 0.6f));
            Assert.AreEqual(AudioEvents.SplashSmall, Route(NoiseSource.Hazard, NoiseTag.Splash, 0.59f));
            Assert.AreEqual(AudioEvents.ImpactMetal, Route(NoiseSource.Impact, NoiseTag.Metal));
            Assert.AreEqual(AudioEvents.ToolMetal, Route(NoiseSource.Tool, NoiseTag.Food));
            Assert.AreEqual(AudioEvents.ImpactCrate, Route(NoiseSource.Impact));
            Assert.IsNull(Route(NoiseSource.Voice));
            Assert.IsNull(Route(NoiseSource.Scripted));
        }

        [Test] public void ImpactGainScalesWithLoudness()
        {
            var quiet = new NoiseEvent { Source = NoiseSource.Impact, Loudness = 0.15f };
            var loud = new NoiseEvent { Source = NoiseSource.Impact, Loudness = 0.9f };
            NoiseRouting.TryRoute(quiet, false, out var q); NoiseRouting.TryRoute(loud, false, out var l);
            Assert.Less(q.GainDb, l.GainDb);
            Assert.AreEqual(0f, NoiseRouting.LoudnessDb(0.5f, 0.5f), 1e-4f);
            Assert.AreEqual(-18f, NoiseRouting.LoudnessDb(0f, 0.5f));
            Assert.AreEqual(6f, NoiseRouting.LoudnessDb(100f, 0.5f));
        }

        [Test] public void CuePositionIsNoiseLocation()
        {
            var e = new NoiseEvent { Source = NoiseSource.Footstep, Loudness = 0.3f, Location = new Vector3(4, 5, 6) };
            Assert.IsTrue(NoiseRouting.TryRoute(e, false, out var c));
            Assert.AreEqual(new Vector3(4, 5, 6), c.Position);
        }
    }

    public class MixRulesTests
    {
        [Test] public void DuckTargets()
        {
            Assert.AreEqual(-6f, DuckingState.TargetDb(MixGroup.Music, true, false, false));
            Assert.AreEqual(-3f, DuckingState.TargetDb(MixGroup.Critters, true, false, false));
            Assert.AreEqual(-3f, DuckingState.TargetDb(MixGroup.Ambience, true, false, false));
            Assert.AreEqual(-9f, DuckingState.TargetDb(MixGroup.Music, false, true, false));
            Assert.AreEqual(-15f, DuckingState.TargetDb(MixGroup.Music, true, true, false));
            Assert.AreEqual(DuckingState.CutDb, DuckingState.TargetDb(MixGroup.Music, true, true, true));
            Assert.AreEqual(0f, DuckingState.TargetDb(MixGroup.Voice, true, true, true));
            Assert.AreEqual(0f, DuckingState.TargetDb(MixGroup.Impacts, true, true, false));
        }

        [Test] public void DuckSmoothing_AttackFastReleaseSlow()
        {
            var d = new DuckingState { VoiceActive = true };
            for (int i = 0; i < 5; i++) d.Tick(0.016f); // 80 ms = one attack time constant
            float afterAttack = d.CurrentDb(MixGroup.Music);
            Assert.That(afterAttack, Is.LessThan(-3.5f).And.GreaterThan(-6f));
            for (int i = 0; i < 60; i++) d.Tick(0.016f);
            Assert.AreEqual(-6f, d.CurrentDb(MixGroup.Music), 0.01f);
            Assert.AreEqual(-3f, d.CurrentDb(MixGroup.Critters), 0.01f);
            d.VoiceActive = false;
            for (int i = 0; i < 5; i++) d.Tick(0.016f);
            Assert.Less(d.CurrentDb(MixGroup.Music), -5f, "release (600 ms) is much slower than attack");
            for (int i = 0; i < 300; i++) d.Tick(0.016f);
            Assert.AreEqual(0f, d.CurrentDb(MixGroup.Music), 0.01f);
            d.MusicCut = true;
            Assert.AreEqual(DuckingState.CutDb, d.CurrentDb(MixGroup.Music));
        }

        [Test] public void DisasterRules()
        {
            var r = new DisasterStingerRules();
            r.Decide(BlameVerb.DroppedInRiver, 50, 10.0, out var full, out var s, out var scratch);
            Assert.IsTrue(full); Assert.AreEqual(AudioEvents.StgRiver, s); Assert.IsFalse(scratch);
            r.Decide(BlameVerb.BrokePlank, 90, 12.0, out full, out s, out scratch);
            Assert.IsFalse(full); Assert.AreEqual(AudioEvents.StgSweetener, s); Assert.IsFalse(scratch);
            r.Decide(BlameVerb.BrokePlank, 90, 20.0, out full, out s, out scratch);
            Assert.IsTrue(full); Assert.AreEqual(AudioEvents.StgPlank, s); Assert.IsTrue(scratch);
            Assert.AreEqual(AudioEvents.StgWagon, DisasterStingerRules.StingerFor(BlameVerb.TippedWagon));
            Assert.AreEqual(AudioEvents.StgGeneric, DisasterStingerRules.StingerFor(BlameVerb.KnockedOut));
        }

        [Test] public void WalletCueRules()
        {
            Assert.AreEqual(AudioEvents.KaChing, WalletCues.CueFor(25, out var t)); Assert.AreEqual(0, t);
            Assert.AreEqual(AudioEvents.WalletWah, WalletCues.CueFor(-35, out t)); Assert.AreEqual(3, t);
            Assert.AreEqual(AudioEvents.WalletWah, WalletCues.CueFor(-999, out t)); Assert.AreEqual(5, t);
            Assert.IsNull(WalletCues.CueFor(0, out t));
        }

        [Test] public void MusicCueByPhase()
        {
            Assert.AreEqual(AudioEvents.MusMorning, MusicRules.CueFor("Morning", 20, out var w)); Assert.IsFalse(w);
            Assert.AreEqual(AudioEvents.MusRoundup, MusicRules.CueFor("Workday", 61, out w)); Assert.IsFalse(w);
            Assert.AreEqual(AudioEvents.MusCrunch, MusicRules.CueFor("Workday", 60, out w)); Assert.IsTrue(w);
            Assert.AreEqual(AudioEvents.MusDusk, MusicRules.CueFor("Dusk", 100, out w));
            Assert.AreEqual(AudioEvents.MusPayout, MusicRules.CueFor("Payout", 0, out w));
            Assert.AreEqual(AudioEvents.MusSunday, MusicRules.CueFor("Sunday", 0, out w));
            Assert.IsNull(MusicRules.CueFor("None", 0, out w));
        }

        [Test] public void VoiceStealing_FootstepsFarthest_ImpactsQuietest()
        {
            var dist = new[] { 5f, 20f, 10f }; var gain = new[] { 0.5f, 0.9f, 0.1f };
            Assert.AreEqual(1, VoiceStealing.PickVictim(VoiceLimit.Footsteps, dist, gain, 3, 3f, 1f));
            Assert.AreEqual(-1, VoiceStealing.PickVictim(VoiceLimit.Footsteps, dist, gain, 3, 30f, 1f), "new farther than all -> rejected");
            Assert.AreEqual(2, VoiceStealing.PickVictim(VoiceLimit.Impacts, dist, gain, 3, 1f, 0.6f));
            Assert.AreEqual(-1, VoiceStealing.PickVictim(VoiceLimit.Impacts, dist, gain, 3, 1f, 0.05f), "new quieter than all -> rejected");
            Assert.AreEqual(-1, VoiceStealing.PickVictim(VoiceLimit.Stingers, dist, gain, 3, 0f, 1f));
        }
    }
}
