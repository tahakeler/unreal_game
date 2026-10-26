using System;

namespace HarvestPanic.Voice
{
    /// <summary>
    /// Receive pipeline for one remote speaker (adr-voice-chat section 6): jitter buffer -> decoder -> loss concealment
    /// -> 16 kHz playout ring read by the audio thread. <see cref="Tick"/> is the main-thread playout clock: it pops one
    /// frame per frame period once the buffer has pre-filled, and pre-buffers one extra frame at the start of each talk
    /// spurt so the audio thread does not starve between ticks. Pure C# (no Unity objects), EditMode tested.
    /// </summary>
    public sealed class VoiceSpeakerStream
    {
        public readonly ulong Speaker;
        public readonly VoiceRing Ring;

        readonly VoiceConfig _cfg;
        readonly VoiceJitterBuffer _jitter;
        readonly VoiceDecoder _decoder;
        readonly double _frameSec;
        readonly int _frameSamples;
        readonly float[] _pcm, _last, _silence;
        int _lastLen;
        bool _concealedOnce;
        double _clock;

        /// <summary>Time of the last accepted packet (caller's clock, seconds).</summary>
        public double LastPacketTime { get; private set; } = double.NegativeInfinity;
        /// <summary>Loudness 0..1 of the last packet.</summary>
        public float Loudness { get; private set; }
        /// <summary>Audible range sent by the host in the last packet (m); 0 until the first packet.</summary>
        public float RangeMetres { get; private set; }
        /// <summary>RMS of the last decoded frame (diagnostics/tests).</summary>
        public float LastFrameRms { get; private set; }

        public int FramesReceived, FramesPlayed, FramesLost, FramesConcealed, Underruns, Duplicates, Late, Resets, Invalid, Undecodable;
        public long SamplesQueued;

        public bool Playing => _jitter.Playing;
        public int Buffered => _jitter.Count;

        public VoiceSpeakerStream(ulong speaker, VoiceConfig cfg, SteamVoiceDecodeFn steamDecode = null)
        {
            Speaker = speaker;
            _cfg = cfg;
            _jitter = new VoiceJitterBuffer(cfg.JitterSlots, cfg.JitterPrefillFrames, cfg.MaxUnderruns, cfg.MaxPayloadBytes);
            _decoder = new VoiceDecoder(steamDecode);
            _frameSec = cfg.FrameMs / 1000.0;
            _frameSamples = cfg.FrameSamples;
            _pcm = new float[Math.Max(8192, _frameSamples * 4)];
            _last = new float[_pcm.Length];
            _silence = new float[_frameSamples];
            Ring = new VoiceRing(Math.Max(_frameSamples * 3, cfg.SampleRate * cfg.PlayoutRingMs / 1000));
        }

        /// <summary>True while packets keep arriving (within <paramref name="timeout"/> seconds of <paramref name="now"/>).</summary>
        public bool IsSpeaking(double now, float timeout) => FramesReceived > 0 && now - LastPacketTime <= timeout;

        /// <summary>Queues one received frame. <paramref name="now"/> is the caller's clock in seconds.</summary>
        public JitterPush Push(in VoiceHeader h, byte[] src, int offset, double now)
        {
            LastPacketTime = now;
            FramesReceived++;
            Loudness = VoiceMath.DequantizeLoudness(h.Loudness);
            if (h.RangeQ > 0) RangeMetres = VoiceMath.DequantizeRange(h.RangeQ);
            // A new talk spurt while the previous one is still draining its underrun budget: re-anchor instead of
            // treating the (contiguous) sequence number as late.
            if ((h.Flags & VoicePacket.FlagSpurtStart) != 0 && _jitter.Playing && _jitter.Count == 0) _jitter.Reset();
            var r = _jitter.Push(h.Seq, h.Codec, h.Flags, Loudness, src, offset, h.Length);
            switch (r)
            {
                case JitterPush.Duplicate: Duplicates++; break;
                case JitterPush.Late: Late++; break;
                case JitterPush.Reset: Resets++; break;
                case JitterPush.TooLarge: Invalid++; break;
            }
            return r;
        }

        /// <summary>Main-thread playout clock; <paramref name="dt"/> in seconds (unscaled).</summary>
        public void Tick(double dt)
        {
            if (!_jitter.Playing)
            {
                var first = _jitter.Pop(out var f);
                _clock = 0;
                if (first == JitterPop.Empty) return;
                Handle(first, f);
                if (_jitter.Playing) Handle(_jitter.Pop(out f), f);
                return;
            }
            _clock += dt;
            if (_clock > _frameSec * 8) _clock = _frameSec * 8; // after a long hitch catch up at most 8 frames
            while (_clock >= _frameSec && _jitter.Playing)
            {
                _clock -= _frameSec;
                Handle(_jitter.Pop(out var f), f);
            }
            if (!_jitter.Playing) _clock = 0;
        }

        void Handle(JitterPop r, VoiceFrame f)
        {
            switch (r)
            {
                case JitterPop.Frame:
                    int n = _decoder.Decode(f.Codec, f.Data, 0, f.Length, _pcm);
                    if (n <= 0) { Undecodable++; Conceal(); return; }
                    Ring.Write(_pcm, 0, n);
                    Array.Copy(_pcm, _last, n);
                    _lastLen = n;
                    _concealedOnce = false;
                    FramesPlayed++;
                    SamplesQueued += n;
                    LastFrameRms = Rms(_pcm, n);
                    break;
                case JitterPop.Lost:
                    FramesLost++;
                    Conceal();
                    break;
                case JitterPop.Underrun:
                    Underruns++;
                    break;
            }
        }

        /// <summary>One lost frame: repeat the last good frame at reduced gain once, then silence (keeps playout timing).</summary>
        void Conceal()
        {
            if (!_concealedOnce && _lastLen > 0)
            {
                Ring.Write(_last, 0, _lastLen, _cfg.ConcealGain);
                SamplesQueued += _lastLen;
                FramesConcealed++;
                _concealedOnce = true;
                return;
            }
            Ring.Write(_silence, 0, _frameSamples);
            SamplesQueued += _frameSamples;
        }

        public void Reset()
        {
            _jitter.Reset();
            _decoder.Reset();
            Ring.Clear();
            _clock = 0;
            _lastLen = 0;
            _concealedOnce = false;
        }

        static float Rms(float[] a, int n)
        {
            double s = 0;
            for (int i = 0; i < n; i++) s += a[i] * a[i];
            return n > 0 ? (float)Math.Sqrt(s / n) : 0f;
        }
    }
}
