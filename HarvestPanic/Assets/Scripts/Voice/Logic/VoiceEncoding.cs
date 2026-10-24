using System;
using System.Collections.Generic;
using HarvestPanic.Core;

namespace HarvestPanic.Voice
{
    /// <summary>Mono float PCM capture (microphone, or a synthetic source in tests).</summary>
    public interface IVoicePcmSource
    {
        string Name { get; }
        /// <summary>Actual capture rate in Hz.</summary>
        int SampleRate { get; }
        bool Start();
        void Stop();
        /// <summary>Copies newly captured samples (max <paramref name="max"/>); returns the count.</summary>
        int Read(float[] dst, int offset, int max);
    }

    /// <summary>Per-tick transmit inputs.</summary>
    public struct VoiceGateInput
    {
        public VoiceTransmitMode Mode;
        public float Threshold;
        public bool PttHeld;
        public bool MicMuted;
        public bool InputBlocked;
    }

    /// <summary>One encoded outgoing frame (pooled; valid until the next Tick).</summary>
    public sealed class VoiceOutFrame
    {
        public VoiceCodec Codec;
        public byte Flags;
        public float Loudness;
        public byte[] Data;
        public int Length;
    }

    /// <summary>Capture + gate + encode. Implementations: <see cref="PcmVoiceEncoder"/> (ADPCM) and the Steam encoder (Runtime).</summary>
    public interface IVoiceEncoder
    {
        string Name { get; }
        bool Start();
        void Stop();
        /// <summary>Pulls captured audio and appends frames to send (only while the gate is open).</summary>
        void Tick(float dt, in VoiceGateInput gate, List<VoiceOutFrame> output);
        float Loudness { get; }
        bool Transmitting { get; }
    }

    /// <summary>
    /// PCM pipeline: source (any rate) -> 16 kHz -> 40 ms frames -> RMS loudness -> VAD/PTT gate ->
    /// 2:1 decimation -> IMA ADPCM (3 or 4 bit). Decimation runs continuously so filter state never glitches.
    /// </summary>
    public sealed class PcmVoiceEncoder : IVoiceEncoder
    {
        readonly IVoicePcmSource _source;
        readonly VoiceConfig _cfg;
        readonly VoiceCodec _codec;
        readonly int _bits, _frameSamples;
        readonly VoiceActivityDetector _vad;
        readonly Decimator2 _dec = new Decimator2();
        readonly LinearResampler _resampler = new LinearResampler();
        readonly float[] _raw = new float[8192], _rs = new float[16384], _frame, _down;
        int _frameFill;
        AdpcmCodec.State _adpcm;
        readonly VoiceOutFrame[] _pool = new VoiceOutFrame[16];
        int _poolNext;
        bool _wasOpen;

        public string Name => _source.Name;
        public float Loudness => _vad.Loudness;
        public bool Transmitting { get; private set; }
        public VoiceCodec Codec => _codec;

        public PcmVoiceEncoder(IVoicePcmSource source, VoiceConfig cfg, VoiceCodec codec)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _cfg = cfg;
            _codec = codec;
            _bits = AdpcmCodec.BitsFor(codec);
            if (_bits == 0) throw new ArgumentException("PCM encoder needs an ADPCM codec", nameof(codec));
            _frameSamples = cfg.FrameSamples;
            _frame = new float[_frameSamples];
            _down = new float[_frameSamples / 2];
            _vad = new VoiceActivityDetector { Attack = cfg.VadAttack, Release = cfg.VadRelease, Hangover = cfg.VadHangover };
            int bytes = AdpcmCodec.EncodedBytes(_frameSamples / 2, _bits);
            for (int i = 0; i < _pool.Length; i++) _pool[i] = new VoiceOutFrame { Data = new byte[bytes] };
        }

        public bool Start() => _source.Start();
        public void Stop() { _source.Stop(); Transmitting = false; _wasOpen = false; }

        public void Tick(float dt, in VoiceGateInput gate, List<VoiceOutFrame> output)
        {
            int guard = 0;
            int n;
            while ((n = _source.Read(_raw, 0, _raw.Length)) > 0 && guard++ < 16)
            {
                int m = _resampler.Process(_raw, n, _source.SampleRate, _cfg.SampleRate, _rs, _rs.Length);
                for (int i = 0; i < m; i++)
                {
                    _frame[_frameFill++] = _rs[i];
                    if (_frameFill == _frameSamples) { _frameFill = 0; ProcessFrame(gate, output); }
                }
            }
        }

        void ProcessFrame(in VoiceGateInput gate, List<VoiceOutFrame> output)
        {
            float frameSec = _cfg.FrameMs / 1000f;
            float raw = VoiceMath.Loudness(VoiceMath.RmsDbfs(_frame, 0, _frameSamples), 0f, _cfg.LoudnessFloorDb, _cfg.LoudnessSpanDb);
            bool vad = _vad.Process(raw, frameSec, gate.Threshold);
            int half = _dec.Process(_frame, 0, _frameSamples, _down, 0);
            bool open = VoiceMath.ShouldTransmit(gate.Mode, gate.PttHeld, gate.MicMuted, vad, gate.InputBlocked);
            Transmitting = open;
            if (!open) { _wasOpen = false; return; }
            var f = _pool[_poolNext]; _poolNext = (_poolNext + 1) % _pool.Length;
            if (!_wasOpen) _adpcm = default; // fresh decoder state per talk spurt
            f.Codec = _codec;
            f.Flags = _wasOpen ? (byte)0 : VoicePacket.FlagSpurtStart;
            f.Loudness = Math.Max(raw, _vad.Loudness);
            f.Length = AdpcmCodec.Encode(_down, 0, half, _bits, ref _adpcm, f.Data, 0);
            _wasOpen = true;
            output.Add(f);
        }
    }

    /// <summary>Decodes a Steam voice payload (u16-length blocks) into 16 kHz float PCM; returns samples, or -1 when unavailable.</summary>
    public delegate int SteamVoiceDecodeFn(byte[] src, int offset, int length, float[] dst16k);

    /// <summary>
    /// Decodes any wire codec to 16 kHz float PCM. One instance per remote speaker (the 8 -> 16 kHz upsampler keeps one
    /// sample of history). Steam decoding is injected by the runtime because it needs a live SteamClient.
    /// </summary>
    public sealed class VoiceDecoder
    {
        readonly SteamVoiceDecodeFn _steam;
        readonly Upsampler2 _up = new Upsampler2();
        readonly float[] _tmp = new float[4096];

        public VoiceDecoder(SteamVoiceDecodeFn steamDecode = null) { _steam = steamDecode; }

        public void Reset() => _up.Reset();

        /// <summary>Returns 16 kHz samples written to <paramref name="dst"/>, or -1 if the codec can't be decoded here.</summary>
        public int Decode(VoiceCodec codec, byte[] src, int offset, int length, float[] dst)
        {
            int bits = AdpcmCodec.BitsFor(codec);
            if (bits > 0)
            {
                int n = AdpcmCodec.Decode(src, offset, length, bits, _tmp, 0, Math.Min(_tmp.Length, dst.Length / 2));
                return _up.Process(_tmp, 0, n, dst, 0);
            }
            if (codec == VoiceCodec.Steam && _steam != null) return _steam(src, offset, length, dst);
            return -1;
        }
    }

    /// <summary>
    /// Steam voice payload framing: each <c>SteamUser.ReadVoiceData</c> result inside one 40 ms frame is stored as
    /// <c>u16 length (LE) + bytes</c>, so receivers can call <c>DecompressVoice</c> per block.
    /// </summary>
    public static class SteamVoiceBlocks
    {
        /// <summary>Appends one block at <paramref name="pos"/>; returns false (nothing written) if it would exceed <paramref name="capacity"/>.</summary>
        public static bool TryAppend(byte[] dst, ref int pos, int capacity, byte[] block, int offset, int length)
        {
            if (length <= 0 || length > ushort.MaxValue || pos + 2 + length > Math.Min(capacity, dst.Length)) return false;
            dst[pos] = (byte)length; dst[pos + 1] = (byte)(length >> 8);
            Buffer.BlockCopy(block, offset, dst, pos + 2, length);
            pos += 2 + length;
            return true;
        }

        /// <summary>Iterates blocks: returns false at the end or on a malformed length.</summary>
        public static bool TryNext(byte[] src, ref int pos, int end, out int blockOffset, out int blockLength)
        {
            blockOffset = 0; blockLength = 0;
            if (pos + 2 > end) return false;
            int len = src[pos] | (src[pos + 1] << 8);
            if (len <= 0 || pos + 2 + len > end) return false;
            blockOffset = pos + 2; blockLength = len;
            pos += 2 + len;
            return true;
        }
    }
}
