using System;
using System.Collections.Generic;
using System.IO;
using HarvestPanic.Core;
using Steamworks;
using UnityEngine;

namespace HarvestPanic.Voice
{
    /// <summary>
    /// Steam voice through Facepunch.Steamworks (adr-voice-chat backend 1). Steam captures, applies its own voice
    /// detection and compresses; we only frame, relay and decode. Every call is guarded: if Steam is not running or the
    /// API throws, the caller falls back to the PCM backend. Main thread only. Not exercisable in batchmode tests
    /// (needs a running Steam client and two accounts): see the owner checklist in the ADR.
    /// </summary>
    public static class SteamVoice
    {
        public const int OutputRate = 16000;
        static readonly MemoryStream DecIn = new MemoryStream(4096), DecOut = new MemoryStream(32768);
        static bool _rateSet;
        static float _nextLog;

        public static bool IsAvailable
        {
            get { try { return SteamClient.IsValid; } catch (Exception) { return false; } }
        }

        /// <summary><see cref="SteamVoiceDecodeFn"/>: decodes a block-framed payload to 16 kHz; -1 when Steam is unavailable.</summary>
        public static int Decode(byte[] src, int offset, int length, float[] dst)
        {
            if (!IsAvailable) return -1;
            int pos = offset, end = offset + length, written = 0;
            while (written < dst.Length && SteamVoiceBlocks.TryNext(src, ref pos, end, out int bo, out int bl))
            {
                int n = DecodeRaw(src, bo, bl, dst, written);
                if (n < 0) return written > 0 ? written : -1;
                written += n;
            }
            return written;
        }

        /// <summary>Decodes one raw <c>ReadVoiceData</c> block into <paramref name="dst"/> at <paramref name="dstOffset"/>.</summary>
        public static int DecodeRaw(byte[] src, int offset, int length, float[] dst, int dstOffset)
        {
            try
            {
                if (!_rateSet) { SteamUser.SampleRate = OutputRate; _rateSet = true; }
                DecIn.SetLength(0);
                DecIn.Write(src, offset, length);
                DecIn.Position = 0;
                DecOut.SetLength(0);
                int bytes = SteamUser.DecompressVoice(DecIn, length, DecOut);
                if (bytes <= 0) return 0;
                byte[] buf = DecOut.GetBuffer();
                int samples = Math.Min(bytes / 2, dst.Length - dstOffset);
                for (int i = 0; i < samples; i++) dst[dstOffset + i] = (short)(buf[2 * i] | (buf[2 * i + 1] << 8)) / 32768f;
                return samples;
            }
            catch (Exception e)
            {
                LogRateLimited("decode failed: " + e.Message);
                return -1;
            }
        }

        internal static void LogRateLimited(string msg)
        {
            if (Time.realtimeSinceStartup < _nextLog) return;
            _nextLog = Time.realtimeSinceStartup + 5f;
            Debug.LogWarning("[Voice] Steam " + msg);
        }
    }

    /// <summary>
    /// Steam capture: <c>SteamUser.VoiceRecord</c> follows the gate (push-to-talk held + a short tail, or always on for
    /// open mic), <c>ReadVoiceData</c> runs every tick, and the blocks read within one 40 ms window are packed into a
    /// single <see cref="VoiceCodec.Steam"/> frame. Loudness is measured locally by decoding our own data (Steam exposes
    /// no PCM), so open-mic VAD and voice Noise Events work the same as on the PCM backend.
    /// </summary>
    public sealed class SteamVoiceEncoder : IVoiceEncoder
    {
        readonly VoiceConfig _cfg;
        readonly MemoryStream _read = new MemoryStream(8192);
        readonly VoiceActivityDetector _vad;
        readonly VoiceOutFrame[] _pool = new VoiceOutFrame[8];
        readonly byte[] _acc;
        readonly float[] _pcm = new float[SteamVoice.OutputRate];
        int _poolNext, _accLen;
        float _accTime, _accLoud, _tail;
        bool _started, _recording, _wasOpen, _failed;

        public SteamVoiceEncoder(VoiceConfig cfg)
        {
            _cfg = cfg;
            _vad = new VoiceActivityDetector { Attack = cfg.VadAttack, Release = cfg.VadRelease, Hangover = cfg.VadHangover };
            _acc = new byte[cfg.MaxPayloadBytes];
            for (int i = 0; i < _pool.Length; i++) _pool[i] = new VoiceOutFrame { Data = new byte[cfg.MaxPayloadBytes] };
        }

        public string Name => "Steam";
        public float Loudness => _vad.Loudness;
        public bool Transmitting { get; private set; }
        /// <summary>True after a Steam API exception; the service then switches to the PCM backend.</summary>
        public bool Failed => _failed;

        public bool Start()
        {
            if (!SteamVoice.IsAvailable) return false;
            _started = true;
            _failed = false;
            return true;
        }

        public void Stop()
        {
            SetRecording(false);
            _started = false;
            Transmitting = false;
            _wasOpen = false;
            _accLen = 0;
        }

        public void Tick(float dt, in VoiceGateInput gate, List<VoiceOutFrame> output)
        {
            if (!_started || _failed) return;
            bool want;
            if (gate.MicMuted) want = false;
            else if (gate.Mode == VoiceTransmitMode.PushToTalk)
            {
                if (gate.PttHeld && !gate.InputBlocked) _tail = _cfg.SteamPttTail; else _tail -= dt;
                want = _tail > 0f;
            }
            else want = true;
            SetRecording(want);
            if (!_recording)
            {
                Flush(output);
                _vad.Process(0f, dt, gate.Threshold);
                Transmitting = false;
                _wasOpen = false;
                return;
            }

            int n = 0;
            try
            {
                if (SteamUser.HasVoiceData)
                {
                    _read.SetLength(0);
                    n = SteamUser.ReadVoiceData(_read);
                }
            }
            catch (Exception e)
            {
                _failed = true;
                SteamVoice.LogRateLimited("capture failed, falling back to the microphone backend: " + e.Message);
                Stop();
                return;
            }

            bool open;
            if (n > 0)
            {
                byte[] data = _read.GetBuffer();
                int samples = SteamVoice.DecodeRaw(data, 0, n, _pcm, 0);
                float raw = samples > 0 ? VoiceMath.Loudness(VoiceMath.RmsDbfs(_pcm, 0, samples), 0f, _cfg.LoudnessFloorDb, _cfg.LoudnessSpanDb) : 0f;
                _vad.Process(raw, samples > 0 ? samples / (float)SteamVoice.OutputRate : dt, gate.Threshold);
                open = gate.Mode == VoiceTransmitMode.PushToTalk || _vad.Active;
                if (open)
                {
                    if (!SteamVoiceBlocks.TryAppend(_acc, ref _accLen, _acc.Length, data, 0, n))
                    {
                        Flush(output);
                        SteamVoiceBlocks.TryAppend(_acc, ref _accLen, _acc.Length, data, 0, n); // oversize blocks are dropped
                    }
                    _accLoud = Mathf.Max(_accLoud, Mathf.Max(raw, _vad.Loudness));
                }
            }
            else
            {
                _vad.Process(0f, dt, gate.Threshold);
                open = gate.Mode == VoiceTransmitMode.PushToTalk || _vad.Active;
            }

            _accTime += dt;
            if (_accLen > 0 && _accTime * 1000f >= _cfg.FrameMs) Flush(output);
            Transmitting = open;
            if (!open) _wasOpen = false;
        }

        void Flush(List<VoiceOutFrame> output)
        {
            _accTime = 0f;
            if (_accLen <= 0) return;
            var f = _pool[_poolNext];
            _poolNext = (_poolNext + 1) % _pool.Length;
            Buffer.BlockCopy(_acc, 0, f.Data, 0, _accLen);
            f.Length = _accLen;
            f.Codec = VoiceCodec.Steam;
            f.Flags = _wasOpen ? (byte)0 : VoicePacket.FlagSpurtStart;
            f.Loudness = _accLoud;
            output.Add(f);
            _wasOpen = true;
            _accLen = 0;
            _accLoud = 0f;
        }

        void SetRecording(bool want)
        {
            if (want == _recording) return;
            try { SteamUser.VoiceRecord = want; _recording = want; }
            catch (Exception e) { _failed = true; _recording = false; SteamVoice.LogRateLimited("VoiceRecord failed: " + e.Message); }
        }
    }
}
