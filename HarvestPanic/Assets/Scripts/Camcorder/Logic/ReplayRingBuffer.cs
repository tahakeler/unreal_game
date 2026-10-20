using System;
using UnityEngine;

namespace HarvestPanic.Camcorder
{
    /// <summary>
    /// Fixed-size ring of snapshot frames (design/gdd/camcorder-replay.md rule 1). Every array is allocated once in the
    /// constructor; writing a frame overwrites the oldest one when full, so recording never allocates.
    /// Frame i (0 = oldest) holds up to <see cref="MaxActorsPerFrame"/> samples.
    /// </summary>
    public sealed class ReplayRingBuffer
    {
        readonly float[] _times;
        readonly int[] _counts;
        readonly ReplaySample[] _samples;
        int _head;   // slot of the next frame to write
        int _count;  // frames stored
        int _open = -1;

        public int FrameCapacity { get; }
        public int MaxActorsPerFrame { get; }
        public int Count => _count;
        /// <summary>Samples dropped because a frame was full (diagnostics).</summary>
        public int DroppedSamples { get; private set; }
        /// <summary>Bytes held by the preallocated arrays.</summary>
        public long MemoryBytes => (long)_samples.Length * ReplaySample.SizeBytes + _times.Length * 8L;

        public ReplayRingBuffer(int frameCapacity, int maxActorsPerFrame)
        {
            if (frameCapacity < 2) throw new ArgumentOutOfRangeException(nameof(frameCapacity));
            if (maxActorsPerFrame < 1) throw new ArgumentOutOfRangeException(nameof(maxActorsPerFrame));
            FrameCapacity = frameCapacity;
            MaxActorsPerFrame = maxActorsPerFrame;
            _times = new float[frameCapacity];
            _counts = new int[frameCapacity];
            _samples = new ReplaySample[frameCapacity * maxActorsPerFrame];
        }

        /// <summary>Starts a new frame at <paramref name="time"/> (overwrites the oldest when full).</summary>
        public void BeginFrame(float time)
        {
            if (_open >= 0) EndFrame();
            _open = _head;
            _times[_open] = time;
            _counts[_open] = 0;
        }

        /// <summary>Adds a sample to the open frame; false (and counted as dropped) when the frame is full.</summary>
        public bool Add(in ReplaySample s)
        {
            if (_open < 0) throw new InvalidOperationException("BeginFrame first");
            int n = _counts[_open];
            if (n >= MaxActorsPerFrame) { DroppedSamples++; return false; }
            _samples[_open * MaxActorsPerFrame + n] = s;
            _counts[_open] = n + 1;
            return true;
        }

        /// <summary>Commits the open frame.</summary>
        public void EndFrame()
        {
            if (_open < 0) return;
            _open = -1;
            _head = (_head + 1) % FrameCapacity;
            if (_count < FrameCapacity) _count++;
        }

        int Slot(int i)
        {
            if ((uint)i >= (uint)_count) throw new ArgumentOutOfRangeException(nameof(i));
            return (_head - _count + i + FrameCapacity) % FrameCapacity;
        }

        public float TimeAt(int i) => _times[Slot(i)];
        public int SampleCount(int i) => _counts[Slot(i)];
        public ReplaySample SampleAt(int i, int j)
        {
            int s = Slot(i);
            if ((uint)j >= (uint)_counts[s]) throw new ArgumentOutOfRangeException(nameof(j));
            return _samples[s * MaxActorsPerFrame + j];
        }

        public float OldestTime => _count == 0 ? 0f : TimeAt(0);
        public float NewestTime => _count == 0 ? 0f : TimeAt(_count - 1);

        /// <summary>Index of the first frame with time &gt;= <paramref name="t"/> (Count when none).</summary>
        public int FirstFrameAtOrAfter(float t)
        {
            int lo = 0, hi = _count;
            while (lo < hi) { int mid = (lo + hi) >> 1; if (TimeAt(mid) < t) lo = mid + 1; else hi = mid; }
            return lo;
        }

        /// <summary>Counts the frames and samples in [from, to] (used to size a tape exactly once).</summary>
        public void Measure(float from, float to, out int firstFrame, out int frames, out int samples)
        {
            firstFrame = FirstFrameAtOrAfter(from);
            frames = 0; samples = 0;
            for (int i = firstFrame; i < _count && TimeAt(i) <= to; i++) { frames++; samples += SampleCount(i); }
        }

        /// <summary>Copies frames [firstFrame, firstFrame+frames) into compact arrays (times are rebased to <paramref name="t0"/>).</summary>
        public void CopyFrames(int firstFrame, int frames, float t0, float[] times, int[] starts, ReplaySample[] dst)
        {
            int w = 0;
            for (int f = 0; f < frames; f++)
            {
                int slot = Slot(firstFrame + f);
                times[f] = _times[slot] - t0;
                starts[f] = w;
                int n = _counts[slot];
                Array.Copy(_samples, slot * MaxActorsPerFrame, dst, w, n);
                w += n;
            }
        }

        public void Clear() { _head = 0; _count = 0; _open = -1; DroppedSamples = 0; }
    }

    /// <summary>Fixed ring of <see cref="ReplayEvent"/>s (allocation-free).</summary>
    public sealed class ReplayEventRing
    {
        readonly ReplayEvent[] _items;
        int _head, _count;
        public ReplayEventRing(int capacity) { _items = new ReplayEvent[Mathf.Max(1, capacity)]; }
        public int Count => _count;
        public void Add(in ReplayEvent e) { _items[_head] = e; _head = (_head + 1) % _items.Length; if (_count < _items.Length) _count++; }
        public ReplayEvent this[int i] => _items[(_head - _count + i + _items.Length) % _items.Length];
        public void Clear() { _head = _count = 0; }
    }
}
