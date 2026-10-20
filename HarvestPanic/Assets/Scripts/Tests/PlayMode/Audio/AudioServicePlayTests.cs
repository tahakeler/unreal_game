using System;
using System.Collections;
using HarvestPanic.Audio;
using HarvestPanic.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HarvestPanic.Tests.PlayMode.Audio
{
    public class AudioServicePlayTests
    {
        sealed class FakeNoise : INoiseService
        {
            public event Action<NoiseEvent> OnNoise;
            public bool Emit(NoiseSource s, float l, Vector3 p, ulong i, ulong b, NoiseTag t)
            { OnNoise?.Invoke(new NoiseEvent { Source = s, Loudness = l, Location = p, InstigatorId = i, BlamedPlayerId = b, Tags = t }); return true; }
        }

        sealed class FakeEconomy : IEconomyService
        {
            public int Balance => 0;
            public bool IsMarketOpen => true;
            public event Action<WalletTxn> OnWalletTxn;
            public event Action<BlameEvent> OnDisaster;
            public void AddWalletTxn(in WalletTxn txn) => OnWalletTxn?.Invoke(txn);
            public int ChargeRespawnFee(ulong p, BlameVerb c) => 0;
            public void EmitBlame(in BlameEvent e) => OnDisaster?.Invoke(e);
            public bool TrySell(IGoods item, ulong seller, out int v) { v = 0; return false; }
        }

        sealed class FakePhase : IDayPhaseInfo { public string PhaseName { get; set; } = "None"; public float PhaseRemaining { get; set; } }

        GameObject _go, _listener;
        AudioService _svc;

        [SetUp] public void SetUp()
        {
            var existing = GameObject.Find("[AudioService]");
            if (existing != null) UnityEngine.Object.Destroy(existing);
            _listener = new GameObject("TestListener");
            _go = new GameObject("TestAudio");
            _svc = _go.AddComponent<AudioService>();
            _svc.ListenerOverride = _listener.transform;
        }

        [TearDown] public void TearDown()
        {
            UnityEngine.Object.Destroy(_go);
            UnityEngine.Object.Destroy(_listener);
        }

        static AudioService.PlayRecord Last(AudioService s) => s.History[s.History.Count - 1];

        [UnityTest] public IEnumerator OnNoise_PlaysRoutedClipOnSfxBus()
        {
            var noise = new FakeNoise();
            Services.Register<INoiseService>(noise);
            yield return null; // service binds in Update
            noise.Emit(NoiseSource.Hazard, 0.9f, new Vector3(3, 0, 0), 0, PlayerIds.None, NoiseTag.PlankBreak);
            Assert.AreEqual(1, _svc.History.Count);
            var r = Last(_svc);
            Assert.AreEqual(AudioEvents.PlankBreak, r.Event);
            Assert.AreEqual(AudioBus.Sfx, r.Bus);
            Assert.IsTrue(r.Spatial);
            Assert.AreEqual(1, _svc.ActiveVoiceCount);
            noise.Emit(NoiseSource.Footstep, 0.3f, new Vector3(500, 0, 0), 0, PlayerIds.None, NoiseTag.None);
            Assert.AreEqual(1, _svc.History.Count, "beyond 25 m footstep is culled");
            Services.Unregister<INoiseService>(noise);
        }

        [UnityTest] public IEnumerator WalletTxn_PlaysKaChingOnUiBus_AndLossTicks()
        {
            var eco = new FakeEconomy();
            Services.Register<IEconomyService>(eco);
            yield return null;
            eco.AddWalletTxn(new WalletTxn { Type = WalletTxnType.Sale, Amount = 40 });
            var r = Last(_svc);
            Assert.AreEqual(AudioEvents.KaChing, r.Event);
            Assert.AreEqual(AudioBus.UI, r.Bus);
            Assert.IsFalse(r.Spatial);
            eco.AddWalletTxn(new WalletTxn { Type = WalletTxnType.Fine, Amount = -30 });
            Assert.AreEqual(AudioEvents.WalletWah, Last(_svc).Event);
            yield return new WaitForSecondsRealtime(0.4f);
            int ticks = 0;
            foreach (var h in _svc.History) if (h.Event == AudioEvents.CoinTick) ticks++;
            Assert.AreEqual(3, ticks);
            Services.Unregister<IEconomyService>(eco);
        }

        [UnityTest] public IEnumerator FootstepCap_IsEight()
        {
            for (int i = 0; i < 12; i++) _svc.PlayAt(AudioEvents.FootstepDirt, new Vector3(i, 0, 0));
            yield return null;
            int n = 0;
            foreach (Transform c in _go.transform) { var s = c.GetComponent<AudioSource>(); if (s != null && s.isPlaying && s.clip != null && s.clip.name.StartsWith("sfx_player_footstep")) n++; }
            Assert.LessOrEqual(n, 8);
        }

        [UnityTest] public IEnumerator BusVolume_ScalesVoiceAndShoutDucksMusic()
        {
            Assert.IsTrue(Services.TryGet(out IAudioSettingsSink sink));
            Assert.AreSame(_svc, sink);
            float before = _svc.GetBusVolume(AudioBus.Sfx);
            try
            {
                sink.SetBusVolume(AudioBus.Sfx, 0.25f);
                Assert.AreEqual(0.25f * _svc.GetBusVolume(AudioBus.Master), _svc.BusGain(AudioBus.Sfx), 1e-5f);
                var noise = new FakeNoise();
                Services.Register<INoiseService>(noise);
                yield return null;
                noise.Emit(NoiseSource.Shout, 0.8f, new Vector3(2, 0, 0), 0, PlayerIds.None, NoiseTag.None);
                yield return new WaitForSecondsRealtime(0.3f);
                Assert.Less(_svc.Ducking.CurrentDb(MixGroup.Music), -3f, "nearby shout ducks music");
                Services.Unregister<INoiseService>(noise);
            }
            finally { sink.SetBusVolume(AudioBus.Sfx, before); }
        }

        [UnityTest] public IEnumerator Disaster_CutsMusicThenStinger_SecondIsSweetener()
        {
            var eco = new FakeEconomy();
            var phase = new FakePhase { PhaseName = "Workday", PhaseRemaining = 300f };
            _svc.Music.PhaseSource = phase;
            Services.Register<IEconomyService>(eco);
            yield return null;
            Assert.AreEqual(AudioEvents.MusRoundup, _svc.Music.CurrentCue);
            eco.EmitBlame(new BlameEvent { Verb = BlameVerb.DroppedInRiver, Severity = 90 });
            Assert.IsTrue(_svc.Music.IsCut);
            Assert.AreEqual(AudioEvents.StgRiver, Last(_svc).Event);
            eco.EmitBlame(new BlameEvent { Verb = BlameVerb.BrokePlank, Severity = 90 });
            Assert.AreEqual(AudioEvents.StgSweetener, Last(_svc).Event);
            yield return null;
            Assert.AreEqual(DuckingState.CutDb, _svc.Ducking.CurrentDb(MixGroup.Music));
            phase.PhaseRemaining = 30f;
            yield return null;
            Assert.AreEqual(AudioEvents.MusCrunch, _svc.Music.CurrentCue);
            Services.Unregister<IEconomyService>(eco);
        }
    }
}
