using System.Collections.Generic;

namespace HarvestPanic.Voice
{
    /// <summary>Header fields of one voice frame on the wire.</summary>
    public struct VoiceHeader
    {
        public ulong Speaker;
        public VoiceCodec Codec;
        public ushort Seq;
        public byte Flags;
        public byte Loudness;
        public byte RangeQ;
        public int Length;
    }

    /// <summary>
    /// Versioned byte layouts for HP_VoiceUp / HP_VoiceDown / HP_VoiceMute (adr-voice-chat section 4). Little endian.
    /// Readers reject unknown versions, unknown codecs and length mismatches.
    /// </summary>
    public static class VoicePacket
    {
        public const byte Version = 1;
        public const int UpHeaderBytes = 8, DownHeaderBytes = 17;
        public const byte FlagSpurtStart = 1;
        public const string UpMessage = "HP_VoiceUp", DownMessage = "HP_VoiceDown", MuteMessage = "HP_VoiceMute";

        public static bool IsKnownCodec(VoiceCodec c) => c == VoiceCodec.Adpcm4 || c == VoiceCodec.Adpcm3 || c == VoiceCodec.Steam;

        public static int WriteUp(byte[] dst, in VoiceHeader h, byte[] payload, int offset, int length)
        {
            dst[0] = Version; dst[1] = (byte)h.Codec;
            WriteU16(dst, 2, h.Seq); dst[4] = h.Flags; dst[5] = h.Loudness; WriteU16(dst, 6, (ushort)length);
            System.Buffer.BlockCopy(payload, offset, dst, UpHeaderBytes, length);
            return UpHeaderBytes + length;
        }

        public static bool TryReadUp(byte[] src, int len, int maxPayload, out VoiceHeader h, out int payloadOffset)
        {
            h = default; payloadOffset = UpHeaderBytes;
            if (src == null || len < UpHeaderBytes || src[0] != Version) return false;
            h.Codec = (VoiceCodec)src[1]; h.Seq = ReadU16(src, 2); h.Flags = src[4]; h.Loudness = src[5]; h.Length = ReadU16(src, 6);
            return IsKnownCodec(h.Codec) && h.Length > 0 && h.Length <= maxPayload && UpHeaderBytes + h.Length == len;
        }

        public static int WriteDown(byte[] dst, in VoiceHeader h, byte[] payload, int offset, int length)
        {
            dst[0] = Version;
            ulong s = h.Speaker;
            for (int i = 0; i < 8; i++) dst[1 + i] = (byte)(s >> (8 * i));
            dst[9] = (byte)h.Codec; WriteU16(dst, 10, h.Seq); dst[12] = h.Flags; dst[13] = h.Loudness; dst[14] = h.RangeQ;
            WriteU16(dst, 15, (ushort)length);
            System.Buffer.BlockCopy(payload, offset, dst, DownHeaderBytes, length);
            return DownHeaderBytes + length;
        }

        public static bool TryReadDown(byte[] src, int len, int maxPayload, out VoiceHeader h, out int payloadOffset)
        {
            h = default; payloadOffset = DownHeaderBytes;
            if (src == null || len < DownHeaderBytes || src[0] != Version) return false;
            ulong s = 0;
            for (int i = 0; i < 8; i++) s |= (ulong)src[1 + i] << (8 * i);
            h.Speaker = s; h.Codec = (VoiceCodec)src[9]; h.Seq = ReadU16(src, 10); h.Flags = src[12]; h.Loudness = src[13];
            h.RangeQ = src[14]; h.Length = ReadU16(src, 15);
            return IsKnownCodec(h.Codec) && h.Length > 0 && h.Length <= maxPayload && DownHeaderBytes + h.Length == len;
        }

        public static int MuteBytes(int count) => 2 + 8 * count;

        public static int WriteMute(byte[] dst, IReadOnlyList<ulong> ids)
        {
            int n = System.Math.Min(ids.Count, 255);
            dst[0] = Version; dst[1] = (byte)n;
            for (int k = 0; k < n; k++)
                for (int i = 0; i < 8; i++) dst[2 + 8 * k + i] = (byte)(ids[k] >> (8 * i));
            return MuteBytes(n);
        }

        public static bool TryReadMute(byte[] src, int len, List<ulong> into)
        {
            into.Clear();
            if (src == null || len < 2 || src[0] != Version || MuteBytes(src[1]) != len) return false;
            for (int k = 0; k < src[1]; k++)
            {
                ulong v = 0;
                for (int i = 0; i < 8; i++) v |= (ulong)src[2 + 8 * k + i] << (8 * i);
                into.Add(v);
            }
            return true;
        }

        static void WriteU16(byte[] d, int o, ushort v) { d[o] = (byte)v; d[o + 1] = (byte)(v >> 8); }
        static ushort ReadU16(byte[] s, int o) => (ushort)(s[o] | (s[o + 1] << 8));
    }
}
