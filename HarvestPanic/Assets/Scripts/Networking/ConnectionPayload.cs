using System;
using System.IO;
using System.Text;

namespace HarvestPanic.Networking
{
    /// <summary>
    /// Versioned connection-approval payload sent by a joining client (NetworkConfig.ConnectionData).
    /// Wire: 'H' 'P' [byte format=1] [int32 protocol] [string build] [string name] (BinaryWriter UTF-8). Max 512 bytes.
    /// </summary>
    public struct ConnectionPayload
    {
        /// <summary>Format version of this payload layout (bump when fields are added).</summary>
        public const byte FormatVersion = 1;
        /// <summary>Hard cap on accepted payload size.</summary>
        public const int MaxBytes = 512;

        /// <summary>Gameplay protocol version (NetworkingConfig.protocolVersion).</summary>
        public int Protocol;
        /// <summary>Application.version of the client build.</summary>
        public string BuildVersion;
        /// <summary>Requested display name (unsanitised).</summary>
        public string PlayerName;

        /// <summary>Serialises the payload.</summary>
        public byte[] ToBytes()
        {
            using var ms = new MemoryStream();
            using (var w = new BinaryWriter(ms, Encoding.UTF8, true))
            {
                w.Write((byte)'H'); w.Write((byte)'P'); w.Write(FormatVersion);
                w.Write(Protocol);
                w.Write(Clip(BuildVersion, 32));
                w.Write(Clip(PlayerName, 64));
            }
            return ms.ToArray();
        }

        static string Clip(string s, int n) => s == null ? string.Empty : (s.Length > n ? s.Substring(0, n) : s);

        /// <summary>Parses bytes; false for null, oversize, wrong magic or truncated data. Newer formats are read by prefix.</summary>
        public static bool TryParse(byte[] data, out ConnectionPayload payload)
        {
            payload = default;
            if (data == null || data.Length < 3 || data.Length > MaxBytes) return false;
            if (data[0] != 'H' || data[1] != 'P' || data[2] < 1) return false;
            try
            {
                using var r = new BinaryReader(new MemoryStream(data, 3, data.Length - 3), Encoding.UTF8);
                payload.Protocol = r.ReadInt32();
                payload.BuildVersion = r.ReadString();
                payload.PlayerName = r.ReadString();
                return true;
            }
            catch (Exception) { payload = default; return false; }
        }
    }
}
