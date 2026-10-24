using System;
using System.Collections.Generic;
using HarvestPanic.Core;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace HarvestPanic.Voice
{
    /// <summary>Counters for tests, the debug log and the two-process test.</summary>
    public sealed class VoiceStats
    {
        public int FramesCaptured, FramesSent, ServerFramesIn, FramesRelayed, DroppedRange, DroppedMuted, Invalid, RateLimited;
        public int ClientFramesIn, FramesHeard, ListenerDroppedMuted, NoiseEvents, MuteUpdates;
        public long BytesSent, BytesRelayed;
        public void Reset()
        {
            FramesCaptured = FramesSent = ServerFramesIn = FramesRelayed = DroppedRange = DroppedMuted = Invalid = RateLimited = 0;
            ClientFramesIn = FramesHeard = ListenerDroppedMuted = NoiseEvents = MuteUpdates = 0;
            BytesSent = BytesRelayed = 0;
        }
    }

    /// <summary>
    /// Proximity voice chat (docs/architecture/adr-voice-chat.md). One instance per NetworkManager: the primary one is
    /// created after the first scene loads, follows <see cref="NetworkManager.Singleton"/> and is registered as
    /// <see cref="IVoiceChat"/>; tests create extra instances bound to their own host/client managers.
    /// <para>Owner: capture (Steam voice, Unity Microphone, or an injected source) -> push-to-talk / open-mic gate ->
    /// <c>HP_VoiceUp</c> (unreliable) to the host. Host: validates and rate-limits, emits the voice Noise Event, relays
    /// <c>HP_VoiceDown</c> (unreliable) to every listener in range who has not muted the speaker, and plays it for its
    /// own player. Listener: jitter buffer -> decode -> 3D <see cref="AudioSource"/> at the speaker's head, speaking
    /// marker over the name tag, and music/critter ducking through <see cref="IAudioService.ReportVoiceActivity"/>.</para>
    /// The real microphone is never opened in batchmode unless <c>-hpVoiceMic</c> is passed (<c>-hpVoiceSynth</c> sends
    /// a test tone, <c>-hpVoicePcm</c> skips Steam voice, <c>-hpVoiceOff</c> disables the service).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-300)]
    public sealed class VoiceChatService : MonoBehaviour, IVoiceChat
    {
        public const int MaxSpeakers = 8;

        /// <summary>The auto-created instance registered as <see cref="IVoiceChat"/> (null in tests / when disabled).</summary>
        public static VoiceChatService Primary { get; private set; }

        public VoiceConfig Config { get; private set; }
        /// <summary>Where players' voices come from. Default: player object "HeadPivot".</summary>
        public IVoiceHeadLocator Heads = new PlayerHeadLocator();
        public readonly VoiceStats Stats = new VoiceStats();
        /// <summary>Tests/bots: forces the push-to-talk key state (null = read the keyboard).</summary>
        public bool? PushToTalkOverride;
        /// <summary>Tests/bots: listener position for ducking/occlusion (null = local head, else the AudioListener).</summary>
        public Transform ListenerOverride;
        /// <summary>Calls <see cref="IAudioService.ReportVoiceActivity"/> (music/critter ducking). True for the primary instance.</summary>
        public bool ReportsDucking;

        sealed class Speaker
        {
            public VoiceSpeakerStream Stream;
            public VoiceSpeakerPlayback Playback;
            public VoiceSpeakingIndicator Indicator;
            public Transform Head;
            public bool Speaking, Announced;
            public int Walls;
            public float NextOcclusion;
        }

        NetworkManager _bound;
        bool _followSingleton, _registerService;
        NetworkManager _nm;
        CustomMessagingManager _cmm;
        bool _sessionActive;

        // capture
        IVoiceEncoder _encoder, _injectedEncoder;
        IVoicePcmSource _injectedSource;
        bool _encoderStarted, _devOpenMic;
        float _nextStartTry;
        string _backend = "None";
        readonly List<VoiceOutFrame> _outFrames = new List<VoiceOutFrame>(8);
        ushort _seq;
        InputAction _ptt;

        // host
        readonly VoiceMuteTable _muteTable = new VoiceMuteTable();
        readonly VoiceNoiseGate _noiseGate = new VoiceNoiseGate();
        readonly Dictionary<ulong, VoiceRateLimiter> _limiters = new Dictionary<ulong, VoiceRateLimiter>();
        readonly Dictionary<ulong, int> _uplinkFrames = new Dictionary<ulong, int>();
        readonly List<ulong> _targets = new List<ulong>(4);
        float _range = -1f, _nextRangeResolve;

        // listener
        readonly HashSet<ulong> _muted = new HashSet<ulong>();
        readonly List<ulong> _muteIds = new List<ulong>(VoiceMuteTable.MaxEntries);
        bool _muteDirty;
        readonly Dictionary<ulong, Speaker> _speakers = new Dictionary<ulong, Speaker>();
        readonly List<ulong> _remove = new List<ulong>();
        readonly List<(ulong id, bool on)> _events = new List<(ulong, bool)>();
        readonly RaycastHit[] _hits = new RaycastHit[8];
        AudioListener _listener;
        float _nextListenerLookup;
        bool _duckReported;
        float _nextDuckReport;

        byte[] _rx, _tx;
        float _nextLog;

        VoiceTransmitMode _mode = VoiceTransmitMode.PushToTalk;
        float _threshold;
        bool _micMuted;

        // ------------------------------------------------------------------ creation

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Primary != null || HasArg("-hpVoiceOff")) return;
            if (SceneManager.GetActiveScene().name.StartsWith("InitTestScene")) return; // test runner: tests create their own
            Create(null, true);
        }

        /// <summary>
        /// Creates a voice service. <paramref name="nm"/> null = follow <see cref="NetworkManager.Singleton"/>.
        /// <paramref name="registerAsService"/> = the primary instance (registers <see cref="IVoiceChat"/>, reads the
        /// push-to-talk key, reports ducking to Audio, survives scene loads).
        /// </summary>
        public static VoiceChatService Create(NetworkManager nm, bool registerAsService, VoiceConfig cfg = null, string name = null)
        {
            var go = new GameObject(name ?? "[VoiceChat]");
            go.SetActive(false);
            var svc = go.AddComponent<VoiceChatService>();
            svc.Config = cfg != null ? cfg : VoiceConfig.Load();
            svc._bound = nm;
            svc._followSingleton = nm == null;
            svc._registerService = registerAsService;
            svc.ReportsDucking = registerAsService;
            go.SetActive(true);
            if (registerAsService) DontDestroyOnLoad(go);
            return svc;
        }

        /// <summary>Tests/dev: capture from <paramref name="source"/> with the PCM codec (takes effect at the next session start).</summary>
        public void UsePcmSource(IVoicePcmSource source) { _injectedSource = source; _injectedEncoder = null; RestartCapture(); }

        /// <summary>Tests/dev: capture through a custom encoder (takes effect at the next session start).</summary>
        public void UseEncoder(IVoiceEncoder encoder) { _injectedEncoder = encoder; _injectedSource = null; RestartCapture(); }

        void Awake()
        {
            if (Config == null) Config = VoiceConfig.Load();
            _threshold = Config.DefaultOpenMicThreshold;
            _rx = new byte[VoicePacket.DownHeaderBytes + Config.MaxPayloadBytes + 64];
            _tx = new byte[VoicePacket.DownHeaderBytes + Config.MaxPayloadBytes + 64];
        }

        void OnEnable()
        {
            if (_registerService)
            {
                Primary = this;
                Services.Register<IVoiceChat>(this);
                _ptt = new InputAction("VoicePushToTalk", InputActionType.Button, Config.PushToTalkBinding);
                _ptt.Enable();
            }
        }

        void OnDisable()
        {
            if (_registerService)
            {
                Services.Unregister<IVoiceChat>(this);
                if (Primary == this) Primary = null;
            }
            if (ReportsDucking) ReportDuck(false, true);
            if (_ptt != null) { _ptt.Disable(); _ptt.Dispose(); _ptt = null; }
            if (_sessionActive) EndSession();
        }

        // ------------------------------------------------------------------ IVoiceChat

        public event Action<ulong, bool> OnSpeakingChanged;

        public VoiceTransmitMode Mode { get => _mode; set => _mode = value; }

        public float OpenMicThreshold
        {
            get => _threshold;
            set => _threshold = float.IsNaN(value) ? Config.DefaultOpenMicThreshold : Mathf.Clamp01(value);
        }

        public bool MicMuted { get => _micMuted; set => _micMuted = value; }

        public bool IsTransmitting => _sessionActive && !_micMuted && _encoderStarted && _encoder != null && _encoder.Transmitting;

        public float LocalLoudness => _encoderStarted && _encoder != null ? _encoder.Loudness : 0f;

        public string BackendName => _backend;

        public bool IsSpeaking(ulong clientId) => _speakers.TryGetValue(clientId, out var s) && s.Speaking;

        public bool IsMuted(ulong clientId) => _muted.Contains(clientId);

        public void SetMuted(ulong clientId, bool muted)
        {
            bool changed = muted ? _muted.Count < VoiceMuteTable.MaxEntries && _muted.Add(clientId) : _muted.Remove(clientId);
            if (!changed) return;
            if (muted && _speakers.TryGetValue(clientId, out var sp)) sp.Stream.Reset();
            if (_nm != null && _nm.IsServer) _muteTable.Set(_nm.LocalClientId, _muted);
            else _muteDirty = true;
        }

        // ------------------------------------------------------------------ diagnostics

        /// <summary>The bound NetworkManager while a session is live.</summary>
        public NetworkManager Manager => _nm;
        public bool SessionActive => _sessionActive;
        /// <summary>Audible range the host currently applies (m).</summary>
        public float CurrentRange => _range > 0f ? _range : Config.Range;

        public bool TryGetPlayback(ulong speaker, out VoiceSpeakerPlayback playback)
        {
            playback = _speakers.TryGetValue(speaker, out var s) ? s.Playback : null;
            return playback != null;
        }

        public bool TryGetStream(ulong speaker, out VoiceSpeakerStream stream)
        {
            stream = _speakers.TryGetValue(speaker, out var s) ? s.Stream : null;
            return stream != null;
        }

        /// <summary>Forces the host to re-read <see cref="DayParams.NoiseVoiceRadiusMult"/> on the next frame.</summary>
        public void InvalidateRange() => _nextRangeResolve = 0f;

        // ------------------------------------------------------------------ frame

        void Update()
        {
            var nm = _followSingleton ? NetworkManager.Singleton : _bound;
            bool live = nm != null && nm.IsListening && (nm.IsServer || nm.IsConnectedClient) && nm.CustomMessagingManager != null;
            if (!live)
            {
                if (_sessionActive) EndSession();
                return;
            }
            if (!_sessionActive || nm != _nm || nm.CustomMessagingManager != _cmm)
            {
                if (_sessionActive) EndSession();
                BeginSession(nm);
            }
            float dt = Time.unscaledDeltaTime;
            double now = Time.realtimeSinceStartupAsDouble;
            TickCapture(dt);
            if (_muteDirty) SendMuteList();
            TickPlayback(dt, now);
        }

        void BeginSession(NetworkManager nm)
        {
            _nm = nm;
            _cmm = nm.CustomMessagingManager;
            _cmm.RegisterNamedMessageHandler(VoicePacket.UpMessage, OnUp);
            _cmm.RegisterNamedMessageHandler(VoicePacket.DownMessage, OnDown);
            _cmm.RegisterNamedMessageHandler(VoicePacket.MuteMessage, OnMute);
            _nm.OnClientDisconnectCallback += OnClientDisconnect;
            _sessionActive = true;
            _seq = (ushort)UnityEngine.Random.Range(0, 65536);
            _muteTable.Clear();
            _noiseGate.Clear();
            _limiters.Clear();
            _uplinkFrames.Clear();
            _muted.Clear(); // client ids are per session
            _muteDirty = false;
            _range = -1f;
            _nextRangeResolve = 0f;
            Heads.Clear();
            SelectBackend();
            Log($"session start role={(nm.IsServer ? "host" : "client")} id={nm.LocalClientId} backend={_backend}", true);
        }

        void EndSession()
        {
            if (_cmm != null)
            {
                try
                {
                    _cmm.UnregisterNamedMessageHandler(VoicePacket.UpMessage);
                    _cmm.UnregisterNamedMessageHandler(VoicePacket.DownMessage);
                    _cmm.UnregisterNamedMessageHandler(VoicePacket.MuteMessage);
                }
                catch (Exception) { /* manager already torn down */ }
            }
            if (_nm != null) _nm.OnClientDisconnectCallback -= OnClientDisconnect;
            Log($"session end sent={Stats.FramesSent} relayed={Stats.FramesRelayed} heard={Stats.FramesHeard} droppedRange={Stats.DroppedRange} droppedMuted={Stats.DroppedMuted}", true);
            StopCapture();
            _encoder = null;
            _backend = "None";
            _events.Clear();
            foreach (var kv in _speakers)
            {
                if (kv.Value.Speaking) _events.Add((kv.Key, false));
                DestroySpeaker(kv.Value);
            }
            _speakers.Clear();
            Heads.Clear();
            if (ReportsDucking) ReportDuck(false, true);
            _nm = null;
            _cmm = null;
            _sessionActive = false;
            RaiseEvents();
        }

        // ------------------------------------------------------------------ capture

        void SelectBackend()
        {
            _encoder = null;
            _backend = "None";
            _devOpenMic = false;
            if (_injectedEncoder != null) { _encoder = _injectedEncoder; _backend = _encoder.Name; return; }
            if (_injectedSource != null) { Pcm(_injectedSource); return; }
            if (_registerService && HasArg("-hpVoiceSynth"))
            {
                // Dev/two-process test: a headless "talker" that always transmits a 330 Hz tone (no key, no VAD threshold).
                Pcm(new SyntheticPcmSource(Config.SampleRate) { Frequency = 330f, Amplitude = 0.25f });
                _devOpenMic = true;
                return;
            }
            bool steamSession = Services.TryGet(out ISessionPlatform platform) && platform.ActiveTransport == SessionTransport.Steam;
            if (steamSession && !HasArg("-hpVoicePcm") && SteamVoice.IsAvailable)
            {
                _encoder = new SteamVoiceEncoder(Config);
                _backend = "Steam";
                return;
            }
            UseMicrophone();
        }

        void UseMicrophone()
        {
            _encoder = null;
            _backend = "None";
            if (!_registerService) return; // only the primary instance may own the real microphone
            if (Application.isBatchMode && !HasArg("-hpVoiceMic")) return; // never open the real mic in tests/headless
            string[] devices;
            try { devices = Microphone.devices; } catch (Exception) { devices = null; }
            if (devices == null || devices.Length == 0) { Log("no microphone found: receive only", true); return; }
            Pcm(new MicrophonePcmSource(Config.SampleRate));
        }

        void Pcm(IVoicePcmSource source)
        {
            _encoder = new PcmVoiceEncoder(source, Config, Config.PcmCodec);
            _backend = source.Name;
        }

        void RestartCapture()
        {
            if (!_sessionActive) return;
            StopCapture();
            SelectBackend();
        }

        void StopCapture()
        {
            if (_encoder != null && _encoderStarted) _encoder.Stop();
            _encoderStarted = false;
        }

        bool PttHeld() => PushToTalkOverride ?? (_ptt != null && _ptt.IsPressed());

        void TickCapture(float dt)
        {
            if (_encoder == null) return;
            if (_micMuted)
            {
                if (_encoderStarted) { _encoder.Stop(); _encoderStarted = false; }
                return;
            }
            if (!_encoderStarted)
            {
                if (Time.unscaledTime < _nextStartTry) return;
                _encoderStarted = _encoder.Start();
                if (!_encoderStarted) { _nextStartTry = Time.unscaledTime + 5f; Log("capture start failed (" + _backend + "), retrying in 5 s", true); return; }
            }
            var gate = new VoiceGateInput
            {
                Mode = _devOpenMic ? VoiceTransmitMode.OpenMic : _mode,
                Threshold = _devOpenMic ? 0f : _threshold,
                PttHeld = PttHeld(),
                MicMuted = _micMuted,
                InputBlocked = InputModal.IsBlocking,
            };
            _outFrames.Clear();
            _encoder.Tick(dt, gate, _outFrames);
            if (_encoder is SteamVoiceEncoder steam && steam.Failed)
            {
                _encoderStarted = false;
                UseMicrophone();
                return;
            }
            for (int i = 0; i < _outFrames.Count; i++) SendFrame(_outFrames[i]);
        }

        void SendFrame(VoiceOutFrame f)
        {
            if (f.Length <= 0 || f.Length > Config.MaxPayloadBytes) return;
            var h = new VoiceHeader
            {
                Speaker = _nm.LocalClientId,
                Codec = f.Codec,
                Seq = _seq++,
                Flags = f.Flags,
                Loudness = VoiceMath.QuantizeLoudness(f.Loudness),
                Length = f.Length,
            };
            Stats.FramesCaptured++;
            if (_nm.IsServer) { ServerHandleFrame(h, f.Data, 0); return; }
            int len = VoicePacket.WriteUp(_tx, h, f.Data, 0, f.Length);
            using var w = new FastBufferWriter(len, Allocator.Temp);
            w.WriteBytesSafe(_tx, len);
            _cmm.SendNamedMessage(VoicePacket.UpMessage, NetworkManager.ServerClientId, w, NetworkDelivery.Unreliable);
            Stats.FramesSent++;
            Stats.BytesSent += len;
        }

        // ------------------------------------------------------------------ host relay

        void OnUp(ulong sender, FastBufferReader reader)
        {
            if (_nm == null || !_nm.IsServer || sender == _nm.LocalClientId) return;
            if (!ReadAll(reader, out int len) || !VoicePacket.TryReadUp(_rx, len, Config.MaxPayloadBytes, out var h, out int off))
            {
                Stats.Invalid++;
                Log($"dropped malformed voice packet from {sender} ({len} B)");
                return;
            }
            if (!_limiters.TryGetValue(sender, out var lim)) _limiters[sender] = lim = new VoiceRateLimiter();
            if (!lim.TryConsume(len, Time.realtimeSinceStartupAsDouble, Config.MaxPacketsPerSecond, Config.PacketBurst, Config.MaxBytesPerSecond))
            {
                Stats.RateLimited++;
                Log($"rate-limited voice from {sender}");
                return;
            }
            if (!_nm.ConnectedClients.ContainsKey(sender)) return;
            h.Speaker = sender; // identity comes from the transport, never the packet
            Stats.ServerFramesIn++;
            _uplinkFrames.TryGetValue(sender, out int got);
            _uplinkFrames[sender] = ++got;
            if (got == 25) Log($"uplink speaker={sender} frames={got} bytes={len} codec={h.Codec}", true);
            ServerHandleFrame(h, _rx, off);
        }

        void ServerHandleFrame(VoiceHeader h, byte[] buf, int off)
        {
            ulong speaker = h.Speaker;
            float range = ResolveRange();
            h.RangeQ = VoiceMath.QuantizeRange(range);
            Transform sHead = Heads.FindHead(_nm, speaker);
            EmitVoiceNoise(speaker, sHead, VoiceMath.DequantizeLoudness(h.Loudness));

            _targets.Clear();
            var ids = _nm.ConnectedClientsIds;
            for (int i = 0; i < ids.Count; i++) // indexed: no boxed enumerator per relayed frame
            {
                ulong id = ids[i];
                if (id == _nm.LocalClientId) continue; // the host's own ears are handled below without a send
                Count(Route(speaker, id, sHead, range), id);
            }
            if (_targets.Count > 0)
            {
                int len = VoicePacket.WriteDown(_tx, h, buf, off, h.Length);
                using var w = new FastBufferWriter(len, Allocator.Temp);
                w.WriteBytesSafe(_tx, len);
                _cmm.SendNamedMessage(VoicePacket.DownMessage, _targets, w, NetworkDelivery.Unreliable);
                Stats.FramesRelayed += _targets.Count;
                Stats.BytesRelayed += (long)len * _targets.Count;
            }
            if (_nm.IsClient && speaker != _nm.LocalClientId)
            {
                var route = Route(speaker, _nm.LocalClientId, sHead, range);
                if (route == VoiceRoute.Deliver) Receive(h, buf, off);
                else if (route == VoiceRoute.Muted) Stats.DroppedMuted++;
                else if (route == VoiceRoute.OutOfRange) Stats.DroppedRange++;
            }
        }

        void Count(VoiceRoute route, ulong listener)
        {
            switch (route)
            {
                case VoiceRoute.Deliver: _targets.Add(listener); break;
                case VoiceRoute.Muted: Stats.DroppedMuted++; break;
                case VoiceRoute.OutOfRange: Stats.DroppedRange++; break;
            }
        }

        VoiceRoute Route(ulong speaker, ulong listener, Transform sHead, float range)
        {
            Transform lHead = speaker == listener ? null : Heads.FindHead(_nm, listener);
            bool have = sHead != null && lHead != null;
            return VoiceRelay.Decide(speaker, listener, _muteTable.IsMuted(listener, speaker), have,
                have ? sHead.position : Vector3.zero, have ? lHead.position : Vector3.zero, range, Config.RelayMargin);
        }

        float ResolveRange()
        {
            if (_range > 0f && Time.unscaledTime < _nextRangeResolve) return _range;
            _nextRangeResolve = Time.unscaledTime + 0.5f;
            float mult = Services.TryGet(out IDayModifierService day) ? day.Resolve(DayParams.NoiseVoiceRadiusMult, 1f) : 1f;
            _range = VoiceMath.EffectiveRange(Config.Range, mult, Config.MinRangeMult, Config.MaxRangeMult);
            return _range;
        }

        void EmitVoiceNoise(ulong speaker, Transform head, float loudness)
        {
            if (!Config.VoiceDrawsCritters || head == null) return;
            if (!_noiseGate.ShouldEmit(speaker, loudness, Time.realtimeSinceStartupAsDouble, Config.VoiceNoiseThreshold, Config.VoiceNoiseInterval)) return;
            if (!Services.TryGet(out INoiseService noise)) return;
            var players = _nm.SpawnManager?.GetPlayerNetworkObjects(speaker);
            ulong instigator = players != null && players.Count > 0 && players[0] != null ? players[0].NetworkObjectId : 0;
            if (noise.Emit(NoiseSource.Voice, Mathf.Clamp01(loudness), head.position, instigator, speaker, NoiseTag.None)) Stats.NoiseEvents++;
        }

        void OnMute(ulong sender, FastBufferReader reader)
        {
            if (_nm == null || !_nm.IsServer) return;
            if (!ReadAll(reader, out int len) || !VoicePacket.TryReadMute(_rx, len, _muteIds)) { Stats.Invalid++; return; }
            _muteTable.Set(sender, _muteIds);
            Stats.MuteUpdates++;
        }

        void OnClientDisconnect(ulong id)
        {
            if (_nm != null && _nm.IsServer)
            {
                _muteTable.Remove(id);
                _noiseGate.Forget(id);
                _limiters.Remove(id);
            }
            if (_speakers.TryGetValue(id, out var sp))
            {
                if (sp.Speaking) _events.Add((id, false));
                DestroySpeaker(sp);
                _speakers.Remove(id);
                RaiseEvents();
            }
        }

        // ------------------------------------------------------------------ listener

        void OnDown(ulong sender, FastBufferReader reader)
        {
            if (_nm == null || _nm.IsServer || sender != NetworkManager.ServerClientId) return;
            if (!ReadAll(reader, out int len) || !VoicePacket.TryReadDown(_rx, len, Config.MaxPayloadBytes, out var h, out int off))
            {
                Stats.Invalid++;
                return;
            }
            if (h.Speaker == _nm.LocalClientId) return;
            Stats.ClientFramesIn++;
            Receive(h, _rx, off);
        }

        void Receive(in VoiceHeader h, byte[] buf, int off)
        {
            if (_muted.Contains(h.Speaker)) { Stats.ListenerDroppedMuted++; return; }
            var sp = GetOrCreateSpeaker(h.Speaker);
            if (sp == null) return;
            sp.Stream.Push(h, buf, off, Time.realtimeSinceStartupAsDouble);
            Stats.FramesHeard++;
            if (!sp.Announced && sp.Stream.FramesReceived >= 25)
            {
                sp.Announced = true;
                Log($"heard speaker={h.Speaker} frames={sp.Stream.FramesReceived} codec={h.Codec} range={VoiceMath.DequantizeRange(h.RangeQ):0.#}", true);
            }
        }

        Speaker GetOrCreateSpeaker(ulong id)
        {
            if (_speakers.TryGetValue(id, out var sp)) return sp;
            if (_speakers.Count >= MaxSpeakers) return null;
            var go = new GameObject("[Voice] Speaker " + id);
            go.transform.SetParent(transform, false);
            go.AddComponent<AudioSource>();
            var playback = go.AddComponent<VoiceSpeakerPlayback>();
            var stream = new VoiceSpeakerStream(id, Config, SteamVoice.Decode);
            playback.Init(stream, Config, CurrentRange);
            sp = new Speaker { Stream = stream, Playback = playback };
            _speakers[id] = sp;
            return sp;
        }

        void DestroySpeaker(Speaker sp)
        {
            if (sp.Playback != null) Destroy(sp.Playback.gameObject);
            if (sp.Indicator != null) Destroy(sp.Indicator.gameObject);
        }

        void TickPlayback(float dt, double now)
        {
            Transform listener = ListenerTransform();
            bool duck = IsTransmitting;
            float bus = VoiceBusGain();
            _remove.Clear();
            foreach (var kv in _speakers)
            {
                var sp = kv.Value;
                sp.Stream.Tick(dt);
                if (sp.Head == null) sp.Head = Heads.FindHead(_nm, kv.Key);
                bool hasHead = sp.Head != null;
                if (hasHead) sp.Playback.transform.position = sp.Head.position;
                sp.Playback.SetSpatial(hasHead);
                if (sp.Stream.RangeMetres > 0f) sp.Playback.ApplyRange(sp.Stream.RangeMetres);

                float occlusion = 1f;
                if (Config.UseOcclusion && hasHead && listener != null)
                {
                    if (Time.unscaledTime >= sp.NextOcclusion)
                    {
                        sp.NextOcclusion = Time.unscaledTime + 1f / Mathf.Max(1f, Config.OcclusionHz);
                        sp.Walls = CountWalls(listener, sp.Head);
                    }
                    occlusion = VoiceMath.OcclusionGain(sp.Walls, Config.OcclusionDbPerWall, Config.MaxOccludingWalls);
                }
                // 2D fallback (speaker's body not spawned yet) plays at the edge-of-range level so it never sounds "in your ear".
                float fallback = hasHead ? 1f : 0.35f;
                sp.Playback.SetVolume(bus * occlusion * fallback);

                bool speaking = sp.Stream.IsSpeaking(now, Config.SpeakerTimeout);
                if (speaking != sp.Speaking) { sp.Speaking = speaking; _events.Add((kv.Key, speaking)); }
                if (speaking && sp.Indicator == null && hasHead) sp.Indicator = VoiceSpeakingIndicator.Attach(sp.Head, Config);
                if (sp.Indicator != null) sp.Indicator.Show(speaking, sp.Stream.Loudness);
                if (speaking && (listener == null || !hasHead || (listener.position - sp.Head.position).sqrMagnitude <= Config.DuckRadius * Config.DuckRadius)) duck = true;
                if (!speaking && now - sp.Stream.LastPacketTime > Config.SpeakerIdleDestroy) _remove.Add(kv.Key);
            }
            foreach (var id in _remove) { DestroySpeaker(_speakers[id]); _speakers.Remove(id); }
            if (ReportsDucking) ReportDuck(duck, false);
            RaiseEvents();
        }

        int CountWalls(Transform listener, Transform head)
        {
            Vector3 a = listener.position, b = head.position, d = b - a;
            float dist = d.magnitude;
            if (dist < 0.5f) return 0;
            int n = Physics.RaycastNonAlloc(a, d / dist, _hits, dist, Config.OcclusionMask, QueryTriggerInteraction.Ignore);
            Transform ra = listener.root, rb = head.root;
            int walls = 0;
            for (int i = 0; i < n && walls < Config.MaxOccludingWalls; i++)
            {
                var t = _hits[i].collider != null ? _hits[i].collider.transform : null;
                if (t == null || t.root == ra || t.root == rb) continue;
                walls++;
            }
            return walls;
        }

        Transform ListenerTransform()
        {
            if (ListenerOverride != null) return ListenerOverride;
            var head = _nm != null ? Heads.FindHead(_nm, _nm.LocalClientId) : null;
            if (head != null) return head;
            if (_listener == null && Time.unscaledTime >= _nextListenerLookup)
            {
                _nextListenerLookup = Time.unscaledTime + 1f;
                _listener = FindAnyObjectByType<AudioListener>();
            }
            return _listener != null ? _listener.transform : null;
        }

        static float VoiceBusGain()
        {
            if (!Services.TryGet(out IAudioSettingsSink sink)) return 1f;
            return Mathf.Clamp01(sink.GetBusVolume(AudioBus.Master)) * Mathf.Clamp01(sink.GetBusVolume(AudioBus.Voice));
        }

        void ReportDuck(bool duck, bool force)
        {
            if (!force && duck == _duckReported && Time.unscaledTime < _nextDuckReport) return;
            _duckReported = duck;
            _nextDuckReport = Time.unscaledTime + 0.5f;
            if (Services.TryGet(out IAudioService audio)) audio.ReportVoiceActivity(duck);
        }

        void SendMuteList()
        {
            if (_nm == null || _nm.IsServer || _cmm == null) { _muteDirty = false; return; }
            _muteIds.Clear();
            foreach (var id in _muted) _muteIds.Add(id);
            int len = VoicePacket.WriteMute(_tx, _muteIds);
            using var w = new FastBufferWriter(len, Allocator.Temp);
            w.WriteBytesSafe(_tx, len);
            _cmm.SendNamedMessage(VoicePacket.MuteMessage, NetworkManager.ServerClientId, w, NetworkDelivery.ReliableSequenced);
            _muteDirty = false;
        }

        void RaiseEvents()
        {
            if (_events.Count == 0) return;
            var handler = OnSpeakingChanged;
            for (int i = 0; i < _events.Count; i++)
            {
                if (handler == null) break;
                try { handler(_events[i].id, _events[i].on); }
                catch (Exception e) { Debug.LogException(e); }
            }
            _events.Clear();
        }

        // ------------------------------------------------------------------ helpers

        bool ReadAll(FastBufferReader reader, out int len)
        {
            len = reader.Length - reader.Position;
            if (len <= 0 || len > _rx.Length) return false;
            reader.ReadBytesSafe(ref _rx, len);
            return true;
        }

        void Log(string msg, bool always = false)
        {
            if (!always)
            {
                if (Time.realtimeSinceStartup < _nextLog) return;
                _nextLog = Time.realtimeSinceStartup + 5f;
            }
            Debug.Log("[Voice] " + (_registerService ? "" : "(" + name + ") ") + msg);
        }

        static bool HasArg(string arg)
        {
            foreach (var a in Environment.GetCommandLineArgs())
                if (string.Equals(a, arg, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
