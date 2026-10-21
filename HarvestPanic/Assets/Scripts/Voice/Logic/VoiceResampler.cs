using System;

namespace HarvestPanic.Voice
{
    /// <summary>2:1 decimator (16 kHz -> 8 kHz) with a 15-tap windowed-sinc low-pass at 3.5 kHz. Keeps history between calls.</summary>
    public sealed class Decimator2
    {
        static readonly float[] Taps = BuildTaps(15, 3500f / 16000f);
        readonly float[] _hist = new float[Taps.Length - 1];
        float[] _work = new float[0];

        static float[] BuildTaps(int n, float fc)
        {
            var h = new float[n];
            int m = (n - 1) / 2;
            double sum = 0;
            for (int i = 0; i < n; i++)
            {
                int k = i - m;
                double sinc = k == 0 ? 2 * fc : Math.Sin(2 * Math.PI * fc * k) / (Math.PI * k);
                double w = 0.54 - 0.46 * Math.Cos(2 * Math.PI * i / (n - 1));
                h[i] = (float)(sinc * w);
                sum += h[i];
            }
            for (int i = 0; i < n; i++) h[i] = (float)(h[i] / sum);
            return h;
        }

        /// <summary>Filters and decimates an even-length block; returns n/2 output samples.</summary>
        public int Process(float[] input, int offset, int n, float[] output, int outOffset)
        {
            int h = _hist.Length;
            if (_work.Length < h + n) _work = new float[h + n];
            Array.Copy(_hist, 0, _work, 0, h);
            Array.Copy(input, offset, _work, h, n);
            int outN = n / 2;
            for (int o = 0; o < outN; o++)
            {
                int end = 2 * o + h; // newest sample index in _work
                float acc = 0f;
                for (int t = 0; t < Taps.Length; t++) acc += Taps[t] * _work[end - t];
                output[outOffset + o] = acc;
            }
            Array.Copy(_work, n, _hist, 0, h);
            return outN;
        }

        public void Reset() => Array.Clear(_hist, 0, _hist.Length);
    }

    /// <summary>1:2 interpolator (8 kHz -> 16 kHz), linear, with one sample of history.</summary>
    public sealed class Upsampler2
    {
        float _prev;

        public int Process(float[] input, int offset, int n, float[] output, int outOffset)
        {
            for (int i = 0; i < n; i++)
            {
                float x = input[offset + i];
                output[outOffset + 2 * i] = 0.5f * (_prev + x);
                output[outOffset + 2 * i + 1] = x;
                _prev = x;
            }
            return n * 2;
        }

        public void Reset() => _prev = 0f;
    }

    /// <summary>Linear resampler for capture devices that ignore the requested rate.</summary>
    public sealed class LinearResampler
    {
        double _pos;
        float _last;

        /// <summary>Converts <paramref name="n"/> samples at <paramref name="fromRate"/> to <paramref name="toRate"/>; returns samples written (bounded by capacity).</summary>
        public int Process(float[] input, int n, int fromRate, int toRate, float[] output, int capacity)
        {
            if (fromRate == toRate) { int c = Math.Min(n, capacity); Array.Copy(input, output, c); return c; }
            double step = (double)fromRate / toRate;
            int w = 0;
            // _pos is relative to input[-1] (= _last)
            while (w < capacity)
            {
                double p = _pos;
                int i0 = (int)Math.Floor(p);
                if (i0 + 1 > n) break;
                float a = i0 == 0 ? _last : input[i0 - 1];
                float b = input[i0];
                output[w++] = a + (b - a) * (float)(p - i0);
                _pos += step;
            }
            _pos -= n;
            if (n > 0) _last = input[n - 1];
            return w;
        }
    }
}
