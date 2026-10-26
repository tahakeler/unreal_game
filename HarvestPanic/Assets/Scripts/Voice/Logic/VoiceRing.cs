using System;

namespace HarvestPanic.Voice
{
    /// <summary>
    /// Thread-safe mono float ring buffer between the main-thread playout clock (writer) and the audio thread
    /// (reader). Overflow drops the oldest samples (bounds latency); underrun reads silence.
    /// </summary>
    public sealed class VoiceRing
    {
        readonly float[] _buf;
        readonly object _lock = new object();
        int _read, _count;
        double _frac;

        public VoiceRing(int capacity) { _buf = new float[Math.Max(16, capacity)]; }

        public int Capacity => _buf.Length;
        public int Count { get { lock (_lock) return _count; } }

        public void Clear() { lock (_lock) { _read = 0; _count = 0; _frac = 0; } }

        /// <summary>Appends samples (scaled by <paramref name="gain"/>); returns how many old samples were dropped.</summary>
        public int Write(float[] src, int offset, int n, float gain = 1f)
        {
            lock (_lock)
            {
                int dropped = 0;
                for (int i = 0; i < n; i++)
                {
                    if (_count == _buf.Length) { _read = (_read + 1) % _buf.Length; _count--; dropped++; }
                    _buf[(_read + _count) % _buf.Length] = src[offset + i] * gain;
                    _count++;
                }
                return dropped;
            }
        }

        /// <summary>Reads up to <paramref name="n"/> samples at the native rate; zero-fills the rest. Returns real samples read.</summary>
        public int Read(float[] dst, int offset, int n)
        {
            lock (_lock)
            {
                int r = Math.Min(n, _count);
                for (int i = 0; i < r; i++) { dst[offset + i] = _buf[_read]; _read = (_read + 1) % _buf.Length; }
                _count -= r;
                for (int i = r; i < n; i++) dst[offset + i] = 0f;
                return r;
            }
        }

        /// <summary>
        /// Audio-thread read: multiplies interleaved <paramref name="data"/> (already carrying the AudioSource's 3D
        /// gain/pan from a constant-1 clip) by the voice signal resampled with <paramref name="step"/> = ringRate/outputRate.
        /// </summary>
        public void ReadMultiply(float[] data, int channels, double step)
        {
            lock (_lock)
            {
                int frames = data.Length / Math.Max(1, channels);
                for (int f = 0; f < frames; f++)
                {
                    float s = 0f;
                    if (_count > 0)
                    {
                        float a = _buf[_read];
                        float b = _count > 1 ? _buf[(_read + 1) % _buf.Length] : a;
                        s = a + (b - a) * (float)_frac;
                        _frac += step;
                        while (_frac >= 1.0 && _count > 0) { _frac -= 1.0; _read = (_read + 1) % _buf.Length; _count--; }
                        if (_count == 0) _frac = 0;
                    }
                    for (int c = 0; c < channels; c++) data[f * channels + c] *= s;
                }
            }
        }
    }
}
