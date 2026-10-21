using System;
using System.Collections;
using System.Collections.Generic;
using HarvestPanic.Core;
using HarvestPanic.Voice;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace HarvestPanic.Tests.PlayMode.Voice
{
    /// <summary>
    /// In-process host + client NetworkManagers over Unity Transport on 127.0.0.1 (random port), each with its own
    /// <see cref="VoiceChatService"/>. Capture is a <see cref="SyntheticPcmSource"/> tone, never the microphone.
    /// Heads are plain transforms (MapHeadLocator) so range is controlled exactly.
    /// </summary>
    public class VoicePlayModeTests
    {
        NetworkManager _host, _client;
        VoiceChatService _hostVoice, _clientVoice;
        Transform _hostHead, _clientHead;
        ulong _hostId, _clientId;
        VoiceConfig _cfg;
        readonly List<Action> _restore = new List<Action>();

        sealed class FakeAudio : IAudioService
        {
            public bool LastReport;
            public int Reports;
            public bool PlayAt(string e, Vector3 p, float v = 1f) => false;
            public bool Play2D(string e, float v = 1f) => false;
            public void ReportVoiceActivity(bool any) { LastReport = any; Reports++; }
            public float GetBusVolume(AudioBus bus) => 1f;
            public void SetBusVolume(AudioBus bus, float v) { }
        }

        sealed class FakeDay : IDayModifierService
        {
            public float VoiceMult = 1f;
            public int DayIndex => 0;
            public float Resolve(string p, float b) => p == DayParams.NoiseVoiceRadiusMult ? b * VoiceMult : b;
            public bool HasWorldTag(string t) => false;
#pragma warning disable 67
            public event Action OnModifiersChanged;
#pragma warning restore 67
        }

        static NetworkManager MakeManager(string name, ushort port)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            var utp = go.AddComponent<UnityTransport>();
            var nm = go.AddComponent<NetworkManager>();
            nm.NetworkConfig = new NetworkConfig { NetworkTransport = utp, ConnectionApproval = false, EnableSceneManagement = false };
            go.SetActive(true);
            utp.SetConnectionData("127.0.0.1", port);
            return nm;
        }

        static IEnumerator WaitFor(Func<bool> cond, float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!cond() && Time.realtimeSinceStartup < end) yield return null;
        }

        static IEnumerator Wait(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _cfg = ScriptableObject.CreateInstance<VoiceConfig>();
            _cfg.UseOcclusion = false;
            ushort port = (ushort)Random.Range(41000, 61000);
            _host = MakeManager("VoiceTestHost", port);
            Assert.IsTrue(_host.StartHost(), "host failed to start on " + port);
            _client = MakeManager("VoiceTestClient", port);
            Assert.IsTrue(_client.StartClient(), "client failed to start");
            yield return WaitFor(() => _client.IsConnectedClient && _host.ConnectedClientsIds.Count == 2, 10f);
            Assert.IsTrue(_client.IsConnectedClient, "client did not connect");
            _hostId = _host.LocalClientId;
            _clientId = _client.LocalClientId;

            _hostHead = new GameObject("HostHead").transform;
            _hostHead.position = new Vector3(0f, 1.6f, 0f);
            _clientHead = new GameObject("ClientHead").transform;
            _clientHead.position = new Vector3(6f, 1.6f, 0f);

            _hostVoice = MakeVoice(_host, "HostVoice", _hostHead);
            _clientVoice = MakeVoice(_client, "ClientVoice", _clientHead);
            yield return WaitFor(() => _hostVoice.SessionActive && _clientVoice.SessionActive, 5f);
            Assert.IsTrue(_hostVoice.SessionActive && _clientVoice.SessionActive, "voice did not bind to the sessions");
            Assert.AreEqual("None", _hostVoice.BackendName, "tests must never open the real microphone");
        }

        VoiceChatService MakeVoice(NetworkManager nm, string name, Transform listener)
        {
            var v = VoiceChatService.Create(nm, false, _cfg, name);
            var heads = new MapHeadLocator();
            heads.Heads[_hostId] = _hostHead;
            heads.Heads[_clientId] = _clientHead;
            v.Heads = heads;
            v.ListenerOverride = listener;
            return v;
        }

        static SyntheticPcmSource Talk(VoiceChatService v)
        {
            var src = new SyntheticPcmSource(16000) { Frequency = 440f, Amplitude = 0.3f };
            v.Mode = VoiceTransmitMode.OpenMic;
            v.OpenMicThreshold = 0f;
            v.UsePcmSource(src);
            return src;
        }

        /// <summary>Registers a fake and restores whatever provider was there before (the real AudioService auto-starts in PlayMode).</summary>
        void Register<T>(T svc) where T : class
        {
            Services.TryGet(out T previous);
            Services.Register(svc);
            _restore.Add(() =>
            {
                Services.Unregister(svc);
                if (previous != null && !(previous is Object uo && uo == null)) Services.Register(previous);
            });
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int i = _restore.Count - 1; i >= 0; i--) _restore[i]();
            _restore.Clear();
            if (_hostVoice != null) Object.Destroy(_hostVoice.gameObject);
            if (_clientVoice != null) Object.Destroy(_clientVoice.gameObject);
            if (_client != null && _client.IsListening) _client.Shutdown();
            if (_host != null && _host.IsListening) _host.Shutdown();
            yield return null;
            yield return null;
            if (_client != null) Object.Destroy(_client.gameObject);
            if (_host != null) Object.Destroy(_host.gameObject);
            if (_hostHead != null) Object.Destroy(_hostHead.gameObject);
            if (_clientHead != null) Object.Destroy(_clientHead.gameObject);
            Object.Destroy(_cfg);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ClientVoice_ArrivesAtHost_PlaysOn3DSourceAtSpeakerHead()
        {
            var events = new List<(ulong, bool)>();
            _hostVoice.OnSpeakingChanged += (id, on) => events.Add((id, on));
            var audio = new FakeAudio();
            Register<IAudioService>(audio);
            _hostVoice.ReportsDucking = true;

            Talk(_clientVoice);
            VoiceSpeakerStream stream = null;
            yield return WaitFor(() => _hostVoice.TryGetStream(_clientId, out stream) && stream.FramesPlayed > 15, 5f);
            Assert.IsNotNull(stream, "host never received the client's voice");
            Assert.That(stream.FramesPlayed, Is.GreaterThan(15));
            Assert.IsTrue(_clientVoice.IsTransmitting);
            Assert.That(_clientVoice.Stats.FramesSent, Is.GreaterThan(15));
            Assert.AreEqual(VoicePacket.UpHeaderBytes + 123, _clientVoice.Stats.BytesSent / _clientVoice.Stats.FramesSent, "131 B per 40 ms = 26.2 kbps uplink");

            Assert.IsTrue(_hostVoice.TryGetPlayback(_clientId, out var playback));
            var src = playback.Source;
            Assert.AreEqual(1f, src.spatialBlend, "3D voice");
            Assert.AreEqual(AudioRolloffMode.Custom, src.rolloffMode);
            Assert.AreEqual(25f, src.maxDistance, 0.01f, "range 25 m without the Megaphone");
            Assert.IsTrue(src.loop && src.clip != null, "carrier clip drives the 3D source");
            Assert.That(Vector3.Distance(playback.transform.position, _clientHead.position), Is.LessThan(0.01f), "source sits at the speaker's head");
            float expected = 0.3f / Mathf.Sqrt(2f);
            Assert.That(stream.LastFrameRms, Is.EqualTo(expected).Within(expected * 0.3f), "decoded tone keeps its level");
            Assert.That(stream.SamplesQueued, Is.GreaterThan(15 * 640), "decoded audio queued for the audio thread");
            TestContext.WriteLine($"isPlaying={src.isPlaying} filterReads={playback.FilterReads} outputRate={AudioSettings.outputSampleRate} lost={stream.FramesLost} late={stream.Late}");

            Assert.IsTrue(_hostVoice.IsSpeaking(_clientId));
            CollectionAssert.Contains(events, (_clientId, true));
            Assert.IsTrue(audio.LastReport, "speaker within 15 m ducks music/critters");
            var marker = _clientHead.Find("VoiceSpeaking");
            Assert.IsNotNull(marker, "speaking marker above the speaker");

            _clientVoice.MicMuted = true;
            yield return WaitFor(() => !_hostVoice.IsSpeaking(_clientId) && !audio.LastReport, 3f);
            Assert.IsFalse(_hostVoice.IsSpeaking(_clientId), "speaking times out after the talker stops");
            Assert.IsFalse(audio.LastReport, "ducking released");
            CollectionAssert.Contains(events, (_clientId, false));
        }

        [UnityTest]
        public IEnumerator HostVoice_RelayedToClientInRange()
        {
            Talk(_hostVoice);
            VoiceSpeakerStream stream = null;
            yield return WaitFor(() => _clientVoice.TryGetStream(_hostId, out stream) && stream.FramesPlayed > 15, 5f);
            Assert.IsNotNull(stream, "client never heard the host");
            Assert.That(_hostVoice.Stats.FramesRelayed, Is.GreaterThan(15));
            Assert.That(_clientVoice.Stats.ClientFramesIn, Is.GreaterThan(15));
            Assert.AreEqual(25f, stream.RangeMetres, "host sends the audible range");
            Assert.IsTrue(_clientVoice.TryGetPlayback(_hostId, out var playback));
            Assert.AreEqual(1f, playback.Source.spatialBlend);
            Assert.That(Vector3.Distance(playback.transform.position, _hostHead.position), Is.LessThan(0.01f));
        }

        [UnityTest]
        public IEnumerator MutedSpeaker_NotRelayedNorPlayed_UnmuteRestores()
        {
            _clientVoice.SetMuted(_hostId, true);
            yield return WaitFor(() => _hostVoice.Stats.MuteUpdates > 0, 3f);
            Assert.That(_hostVoice.Stats.MuteUpdates, Is.GreaterThan(0), "mute list reached the host");

            Talk(_hostVoice);
            yield return WaitFor(() => _hostVoice.Stats.DroppedMuted > 25, 5f);
            Assert.That(_hostVoice.Stats.DroppedMuted, Is.GreaterThan(25), "host stops relaying a muted speaker");
            Assert.AreEqual(0, _hostVoice.Stats.FramesRelayed);
            Assert.AreEqual(0, _clientVoice.Stats.ClientFramesIn);
            Assert.IsFalse(_clientVoice.TryGetStream(_hostId, out _), "nothing played for the muted speaker");
            Assert.IsTrue(_clientVoice.IsMuted(_hostId));

            _clientVoice.SetMuted(_hostId, false);
            VoiceSpeakerStream stream = null;
            yield return WaitFor(() => _clientVoice.TryGetStream(_hostId, out stream) && stream.FramesPlayed > 5, 5f);
            Assert.IsNotNull(stream, "unmuting restores the voice");
        }

        [UnityTest]
        public IEnumerator HostMutesClient_LocalPlaybackDropped()
        {
            _hostVoice.SetMuted(_clientId, true);
            Talk(_clientVoice);
            yield return WaitFor(() => _hostVoice.Stats.ServerFramesIn > 25, 5f);
            Assert.That(_hostVoice.Stats.ServerFramesIn, Is.GreaterThan(25), "host still receives (it may relay to others)");
            Assert.That(_hostVoice.Stats.DroppedMuted, Is.GreaterThan(25));
            Assert.IsFalse(_hostVoice.TryGetStream(_clientId, out _), "but does not play it to its own player");
        }

        [UnityTest]
        public IEnumerator OutOfRange_Dropped_MegaphoneDoublesRange()
        {
            var day = new FakeDay();
            Register<IDayModifierService>(day);
            _hostVoice.InvalidateRange();
            _clientHead.position = new Vector3(40f, 1.6f, 0f); // beyond 25 m + 2 m margin
            Talk(_hostVoice);
            yield return WaitFor(() => _hostVoice.Stats.DroppedRange > 25, 5f);
            Assert.That(_hostVoice.Stats.DroppedRange, Is.GreaterThan(25), "listener at 40 m is out of range");
            Assert.AreEqual(0, _hostVoice.Stats.FramesRelayed);
            Assert.AreEqual(0, _clientVoice.Stats.ClientFramesIn);

            day.VoiceMult = 2f; // Megaphone upgrade (Noise.VoiceRadiusMult x2)
            _hostVoice.InvalidateRange();
            VoiceSpeakerStream stream = null;
            yield return WaitFor(() => _clientVoice.TryGetStream(_hostId, out stream) && stream.FramesPlayed > 5, 5f);
            Assert.IsNotNull(stream, "Megaphone range (50 m) reaches the listener at 40 m");
            Assert.AreEqual(50f, stream.RangeMetres);
            Assert.AreEqual(50f, _hostVoice.CurrentRange);
            yield return null;
            Assert.IsTrue(_clientVoice.TryGetPlayback(_hostId, out var playback));
            Assert.AreEqual(50f, playback.Source.maxDistance, 0.01f, "rolloff re-baked for the longer range");
        }

        [UnityTest]
        public IEnumerator PushToTalk_SendsOnlyWhileHeld()
        {
            var src = new SyntheticPcmSource(16000) { Frequency = 300f, Amplitude = 0.3f };
            _clientVoice.Mode = VoiceTransmitMode.PushToTalk;
            _clientVoice.PushToTalkOverride = false;
            _clientVoice.UsePcmSource(src);
            yield return Wait(0.8f);
            Assert.AreEqual(0, _clientVoice.Stats.FramesSent, "not held: nothing sent");
            Assert.IsFalse(_clientVoice.IsTransmitting);
            _clientVoice.PushToTalkOverride = true;
            yield return WaitFor(() => _hostVoice.TryGetStream(_clientId, out var s) && s.FramesPlayed > 5, 5f);
            Assert.That(_clientVoice.Stats.FramesSent, Is.GreaterThan(5));
            Assert.IsTrue(_hostVoice.IsSpeaking(_clientId));
        }
    }
}
