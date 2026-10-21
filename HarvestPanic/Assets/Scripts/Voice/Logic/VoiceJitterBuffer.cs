using System;

namespace HarvestPanic.Voice
{
    /// <summary>One buffered voice frame (slot storage is reused; copy or decode before the next push).</summary>
    public sealed class VoiceFrame
    {
        public ushort Seq;
        public VoiceCodec Codec;
        public byte Flags;
        public float Loudness;
        public byte[] Data;
        public int Length;
        internal bool Used;
    }

    public enum JitterPush { Accepted, Duplicate, Late, Reset, TooLarge }
    public enum JitterPop { Empty, Frame, Lost, Underrun }

    /// <summary>
    /// Per-speaker jitter buffer keyed by a wrapping u16 sequence. Pre-fills <c>prefill</c> frames before playing,
    /// pops strictly in order, reports gaps as <see cref="JitterPop.Lost"/> (caller conceals), drops late/duplicate
    /// packets, and resets when the sender jumps more than the capacity ahead. After <c>maxUnderruns</c> consecutive
    /// empty pops the spurt ends and the next packet re-anchors the sequence.
    /// </summary>
    public sealed class VoiceJitterBuffer
    {
        readonly VoiceFrame[] _slots;
        readonly int _prefill, _maxUnderruns, _maxPayload;
        ushort _next;
        int _underruns;

        public int Count { get; private set; }
        public bool Playing { get; private set; }
        public ushort NextSeq => _next;
        public int Capacity => _slots.Length;

        public VoiceJitterBuffer(int capacity = 16, int prefill = 2, int maxUnderruns = 3, int maxPayload = 600)
        {
            capacity = Math.Max(4, capacity);
            _slots = new VoiceFrame[capacity];
            for (int i = 0; i < capacity; i++) _slots[i] = new VoiceFrame { Data = new byte[maxPayload] };
            _prefill = Math.Max(1, Math.Min(prefill, capacity));
            _maxUnderruns = Math.Max(1, maxUnderruns);
            _maxPayload = maxPayload;
        }

        public void Reset()
        {
            foreach (var s in _slots) s.Used = false;
            Count = 0; Playing = false; _underruns = 0;
        }

        public JitterPush Push(ushort seq, VoiceCodec codec, byte flags, float loudness, byte[] src, int offset, int length)
        {
            if (length <= 0 || length > _maxPayload) return JitterPush.TooLarge;
            var result = JitterPush.Accepted;
            if (Count == 0 && !Playing) _next = seq;
            short diff = (short)(seq - _next);
            if (diff < 0)
            {
                if (Playing) return JitterPush.Late;
                // still pre-filling: an earlier packet arrived after a later one -> move the anchor back if it fits
                if (-diff + MaxOffset() < _slots.Length) { _next = seq; diff = 0; }
                else return JitterPush.Late;
            }
            if (diff >= _slots.Length)
            {
                Reset();
                _next = seq; diff = 0;
                result = JitterPush.Reset;
            }
            var slot = _slots[seq % _slots.Length];
            if (slot.Used) return slot.Seq == seq ? JitterPush.Duplicate : JitterPush.Late;
            slot.Used = true; slot.Seq = seq; slot.Codec = codec; slot.Flags = flags; slot.Loudness = loudness; slot.Length = length;
            Buffer.BlockCopy(src, offset, slot.Data, 0, length);
            Count++;
            return result;
        }

        int MaxOffset()
        {
            int max = 0;
            foreach (var s in _slots) if (s.Used) max = Math.Max(max, (short)(s.Seq - _next));
            return max;
        }

        /// <summary>Called once per frame period by the playout clock.</summary>
        public JitterPop Pop(out VoiceFrame frame)
        {
            frame = null;
            if (!Playing)
            {
                if (Count < _prefill) return JitterPop.Empty;
                Playing = true; _underruns = 0;
            }
            if (Count == 0)
            {
                _next++;
                if (++_underruns >= _maxUnderruns) { Playing = false; _underruns = 0; }
                return JitterPop.Underrun;
            }
            var slot = _slots[_next % _slots.Length];
            _next++;
            _underruns = 0;
            if (slot.Used && slot.Seq == (ushort)(_next - 1))
            {
                slot.Used = false; Count--;
                frame = slot;
                return JitterPop.Frame;
            }
            return JitterPop.Lost;
        }
    }
}
