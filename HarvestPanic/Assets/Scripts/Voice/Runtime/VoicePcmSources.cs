using System;
using UnityEngine;

namespace HarvestPanic.Voice
{
    /// <summary>
    /// Unity <see cref="Microphone"/> capture (default device) into a 1 s looping clip at 16 kHz (or the closest rate the
    /// device supports; the encoder resamples). Never constructed in batchmode unless <c>-hpVoiceMic</c> is passed.
    /// </summary>
    public sealed class MicrophonePcmSource : IVoicePcmSource
    {
        readonly int _wantedRate;
        readonly string _device;
        AudioClip _clip;
        int _readPos;
        float[] _tmp = new float[0];

        public MicrophonePcmSource(int wantedRate = 16000, string device = null)
        {
            _wantedRate = wantedRate;
            _device = string.IsNullOrEmpty(device) ? null : device;
        }

        public string Name => "Microphone";
        public int SampleRate => _clip != null ? _clip.frequency : _wantedRate;
        public bool IsRunning => _clip != null;

        public bool Start()
        {
            if (_clip != null) return true;
            try
            {
                if (Microphone.devices == null || Microphone.devices.Length == 0) return false;
                Microphone.GetDeviceCaps(_device, out int min, out int max);
                int rate = min == 0 && max == 0 ? _wantedRate : Mathf.Clamp(_wantedRate, min, max);
                _clip = Microphone.Start(_device, true, 1, rate);
                _readPos = 0;
                return _clip != null;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Voice] Microphone start failed: " + e.Message);
                _clip = null;
                return false;
            }
        }

        public void Stop()
        {
            if (_clip == null) return;
            try { Microphone.End(_device); } catch (Exception) { /* device vanished */ }
            _clip = null;
        }

        public int Read(float[] dst, int offset, int max)
        {
            if (_clip == null || max <= 0) return 0;
            int pos = Microphone.GetPosition(_device);
            int len = _clip.samples;
            if (pos < 0 || len <= 0) return 0;
            int avail = (pos - _readPos + len) % len;
            if (avail == 0) return 0;
            int ch = Mathf.Max(1, _clip.channels);
            int frames = Mathf.Min(avail, max);
            if (_tmp.Length < frames * ch) _tmp = new float[Mathf.Max(frames * ch, 4096)];
            // Explicit two-part read at the loop point (no reliance on GetData wrap semantics, no allocation).
            int first = Mathf.Min(frames, len - _readPos);
            if (!_clip.GetData(_tmp.AsSpan(0, first * ch), _readPos)) return 0;
            if (frames > first && !_clip.GetData(_tmp.AsSpan(first * ch, (frames - first) * ch), 0)) frames = first;
            for (int i = 0; i < frames; i++) dst[offset + i] = _tmp[i * ch];
            _readPos = (_readPos + frames) % len;
            return frames;
        }
    }

    /// <summary>
    /// Deterministic tone generator standing in for a microphone (tests, <c>-hpVoiceSynth</c> dev runs). Produces samples
    /// in real time from <see cref="Clock"/> like a capture device, so the encoder sees realistic frame timing.
    /// </summary>
    public sealed class SyntheticPcmSource : IVoicePcmSource
    {
        public float Frequency = 440f;
        public float Amplitude = 0.3f;
        /// <summary>When true the source still runs but emits silence (VAD tests).</summary>
        public bool Silent;
        public Func<double> Clock = () => Time.realtimeSinceStartupAsDouble;

        readonly int _rate;
        double _start;
        long _produced;
        bool _running;

        public SyntheticPcmSource(int rate = 16000) { _rate = rate; }

        public string Name => "Synthetic";
        public int SampleRate => _rate;
        public bool IsRunning => _running;
        public long Produced => _produced;

        public bool Start()
        {
            if (_running) return true;
            _running = true;
            _start = Clock();
            _produced = 0;
            return true;
        }

        public void Stop() => _running = false;

        public int Read(float[] dst, int offset, int max)
        {
            if (!_running) return 0;
            long due = (long)((Clock() - _start) * _rate);
            if (due - _produced > _rate) _produced = due - _rate; // after a long stall keep at most 1 s of backlog
            int n = (int)Math.Min(max, due - _produced);
            if (n <= 0) return 0;
            double w = 2.0 * Math.PI * Frequency / _rate;
            for (int i = 0; i < n; i++)
                dst[offset + i] = Silent ? 0f : (float)(Amplitude * Math.Sin(w * (_produced + i)));
            _produced += n;
            return n;
        }
    }
}
