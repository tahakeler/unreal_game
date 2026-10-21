using System;

namespace HarvestPanic.Voice
{
    /// <summary>
    /// IMA ADPCM, 4-bit (standard) or 3-bit (index table {-1,-1,2,4}). Every encoded block starts with the 3-byte
    /// decoder state (int16 predictor LE, u8 step index) so packets decode independently. Pure, allocation free.
    /// </summary>
    public static class AdpcmCodec
    {
        public const int StateBytes = 3;

        static readonly int[] StepTable =
        {
            7, 8, 9, 10, 11, 12, 13, 14, 16, 17, 19, 21, 23, 25, 28, 31, 34, 37, 41, 45, 50, 55, 60, 66, 73, 80, 88, 97,
            107, 118, 130, 143, 157, 173, 190, 209, 230, 253, 279, 307, 337, 371, 408, 449, 494, 544, 598, 658, 724, 796,
            876, 963, 1060, 1166, 1282, 1411, 1552, 1707, 1878, 2066, 2272, 2499, 2749, 3024, 3327, 3660, 4026, 4428, 4871,
            5358, 5894, 6484, 7132, 7845, 8630, 9493, 10442, 11487, 12635, 13899, 15289, 16818, 18500, 20350, 22385, 24623,
            27086, 29794, 32767,
        };
        static readonly int[] Index4 = { -1, -1, -1, -1, 2, 4, 6, 8 };
        static readonly int[] Index3 = { -1, -1, 2, 4 };

        /// <summary>Encoder/decoder state carried between samples.</summary>
        public struct State { public int Predictor; public int Index; }

        /// <summary>Bits per sample for an ADPCM codec id (0 if not ADPCM).</summary>
        public static int BitsFor(VoiceCodec codec) => codec == VoiceCodec.Adpcm4 ? 4 : codec == VoiceCodec.Adpcm3 ? 3 : 0;

        /// <summary>Bytes needed for <paramref name="samples"/> samples including the state header.</summary>
        public static int EncodedBytes(int samples, int bits) => StateBytes + (samples * bits + 7) / 8;

        /// <summary>Whole samples contained in a block of <paramref name="blockBytes"/>.</summary>
        public static int SamplesIn(int blockBytes, int bits) => blockBytes <= StateBytes || bits <= 0 ? 0 : (blockBytes - StateBytes) * 8 / bits;

        /// <summary>Encodes float PCM (-1..1) into <paramref name="dst"/>; returns bytes written. <paramref name="st"/> continues across calls.</summary>
        public static int Encode(float[] pcm, int offset, int count, int bits, ref State st, byte[] dst, int dstOffset)
        {
            if (bits != 3 && bits != 4) throw new ArgumentOutOfRangeException(nameof(bits));
            short p = (short)st.Predictor;
            dst[dstOffset] = (byte)(p & 0xFF);
            dst[dstOffset + 1] = (byte)((p >> 8) & 0xFF);
            dst[dstOffset + 2] = (byte)st.Index;
            int bitPos = 0, start = dstOffset + StateBytes;
            int total = (count * bits + 7) / 8;
            Array.Clear(dst, start, total);
            for (int i = 0; i < count; i++)
            {
                float f = pcm[offset + i];
                int s = f >= 1f ? 32767 : f <= -1f ? -32768 : (int)Math.Round(f * 32767f);
                int code = EncodeSample(s, bits, ref st);
                for (int b = 0; b < bits; b++, bitPos++)
                    if ((code & (1 << b)) != 0) dst[start + (bitPos >> 3)] |= (byte)(1 << (bitPos & 7));
            }
            return StateBytes + total;
        }

        /// <summary>Decodes a block into float PCM; returns samples written (bounded by <paramref name="maxSamples"/>).</summary>
        public static int Decode(byte[] src, int offset, int length, int bits, float[] dst, int dstOffset, int maxSamples)
        {
            if (bits != 3 && bits != 4) return 0;
            int n = Math.Min(SamplesIn(length, bits), maxSamples);
            if (n <= 0) return 0;
            var st = new State
            {
                Predictor = (short)(src[offset] | (src[offset + 1] << 8)),
                Index = Math.Min((int)src[offset + 2], StepTable.Length - 1),
            };
            int bitPos = 0, start = offset + StateBytes;
            for (int i = 0; i < n; i++)
            {
                int code = 0;
                for (int b = 0; b < bits; b++, bitPos++)
                    if ((src[start + (bitPos >> 3)] & (1 << (bitPos & 7))) != 0) code |= 1 << b;
                Update(code, bits, ref st);
                dst[dstOffset + i] = st.Predictor / 32768f;
            }
            return n;
        }

        static int EncodeSample(int sample, int bits, ref State st)
        {
            int step = StepTable[st.Index];
            int diff = sample - st.Predictor;
            int sign = 0;
            if (diff < 0) { sign = 1; diff = -diff; }
            int mag = 0;
            if (bits == 4)
            {
                if (diff >= step) { mag |= 4; diff -= step; }
                if (diff >= step >> 1) { mag |= 2; diff -= step >> 1; }
                if (diff >= step >> 2) mag |= 1;
            }
            else
            {
                if (diff >= step) { mag |= 2; diff -= step; }
                if (diff >= step >> 1) mag |= 1;
            }
            int code = (sign << (bits - 1)) | mag;
            Update(code, bits, ref st); // encoder tracks exactly what the decoder will reconstruct
            return code;
        }

        static void Update(int code, int bits, ref State st)
        {
            int step = StepTable[st.Index];
            int signBit = 1 << (bits - 1);
            int mag = code & (signBit - 1);
            int vpdiff;
            if (bits == 4)
            {
                vpdiff = step >> 3;
                if ((mag & 4) != 0) vpdiff += step;
                if ((mag & 2) != 0) vpdiff += step >> 1;
                if ((mag & 1) != 0) vpdiff += step >> 2;
                st.Index += Index4[mag];
            }
            else
            {
                vpdiff = step >> 2;
                if ((mag & 2) != 0) vpdiff += step;
                if ((mag & 1) != 0) vpdiff += step >> 1;
                st.Index += Index3[mag];
            }
            st.Predictor += (code & signBit) != 0 ? -vpdiff : vpdiff;
            if (st.Predictor > 32767) st.Predictor = 32767; else if (st.Predictor < -32768) st.Predictor = -32768;
            if (st.Index < 0) st.Index = 0; else if (st.Index > StepTable.Length - 1) st.Index = StepTable.Length - 1;
        }
    }
}
