using System;
using System.IO;
using System.Text;
using HarvestPanic.Core;
using UnityEngine;

namespace HarvestPanic.Camcorder
{
    /// <summary>
    /// Binary tape format "HPM1" (little-endian). Written next to exported frames as <c>moment.hpm</c> so a kept tape can be
    /// re-opened in the viewer later. Positions/rotations are stored as full floats (beta; quantization is a later optimisation).
    /// </summary>
    public static class MomentSerializer
    {
        const uint Magic = 0x314D5048; // "HPM1"
        public const int Version = 1;

        public static byte[] ToBytes(Moment m)
        {
            using var ms = new MemoryStream(64 + m.Samples.Length * ReplaySample.SizeBytes);
            Write(m, ms);
            return ms.ToArray();
        }

        public static Moment FromBytes(byte[] data)
        {
            using var ms = new MemoryStream(data, false);
            return Read(ms);
        }

        public static void Write(Moment m, Stream s)
        {
            using var w = new BinaryWriter(s, Encoding.UTF8, true);
            w.Write(Magic); w.Write(Version);
            w.Write(m.Id); w.Write(m.Title ?? ""); w.Write(m.Manual); w.Write(m.DayIndex); w.Write(m.Severity);
            w.Write(m.DollarDelta); w.Write(m.InstigatorCount); w.Write((int)m.Verb); w.Write(m.PrimaryInstigator);
            WriteV3(w, m.Focus); w.Write(m.SourceStart); w.Write(m.TriggerTime);
            w.Write(m.Tracks.Count);
            foreach (var t in m.Tracks) { w.Write(t.Id); w.Write((byte)t.Kind); w.Write(t.OwnerClientId); w.Write(t.Name ?? ""); }
            w.Write(m.FrameTimes.Length);
            for (int i = 0; i < m.FrameTimes.Length; i++) { w.Write(m.FrameTimes[i]); w.Write(m.FrameStarts[i]); }
            w.Write(m.Samples.Length);
            foreach (var x in m.Samples)
            {
                w.Write(x.Id); w.Write((byte)x.Kind); w.Write((byte)x.Flags); WriteV3(w, x.Position);
                w.Write(x.Rotation.x); w.Write(x.Rotation.y); w.Write(x.Rotation.z); w.Write(x.Rotation.w);
            }
            w.Write(m.Events.Length);
            foreach (var e in m.Events)
            {
                w.Write(e.Time); w.Write((byte)e.Type); WriteV3(w, e.Position); w.Write(e.Value); w.Write(e.Code); w.Write(e.PlayerId);
            }
        }

        public static Moment Read(Stream s)
        {
            using var r = new BinaryReader(s, Encoding.UTF8, true);
            if (r.ReadUInt32() != Magic) throw new InvalidDataException("Not a HARVEST PANIC tape");
            int ver = r.ReadInt32();
            if (ver != Version) throw new InvalidDataException("Unsupported tape version " + ver);
            var m = new Moment
            {
                Id = r.ReadInt64(), Title = r.ReadString(), Manual = r.ReadBoolean(), DayIndex = r.ReadInt32(), Severity = r.ReadByte(),
                DollarDelta = r.ReadInt32(), InstigatorCount = r.ReadInt32(), Verb = (BlameVerb)r.ReadInt32(), PrimaryInstigator = r.ReadUInt64(),
                Focus = ReadV3(r), SourceStart = r.ReadSingle(), TriggerTime = r.ReadSingle()
            };
            int tc = Count(r, 100000);
            for (int i = 0; i < tc; i++)
                m.Tracks.Add(new TrackInfo { Id = r.ReadInt32(), Kind = (TrackKind)r.ReadByte(), OwnerClientId = r.ReadUInt64(), Name = r.ReadString() });
            int fc = Count(r, 1000000);
            m.FrameTimes = new float[fc]; m.FrameStarts = new int[fc];
            for (int i = 0; i < fc; i++) { m.FrameTimes[i] = r.ReadSingle(); m.FrameStarts[i] = r.ReadInt32(); }
            int sc = Count(r, 50000000);
            m.Samples = new ReplaySample[sc];
            for (int i = 0; i < sc; i++)
            {
                var x = new ReplaySample { Id = r.ReadInt32(), Kind = (TrackKind)r.ReadByte(), Flags = (SampleFlags)r.ReadByte(), Position = ReadV3(r) };
                x.Rotation = new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                m.Samples[i] = x;
            }
            int ec = Count(r, 1000000);
            m.Events = new ReplayEvent[ec];
            for (int i = 0; i < ec; i++)
                m.Events[i] = new ReplayEvent { Time = r.ReadSingle(), Type = (ReplayEventType)r.ReadByte(), Position = ReadV3(r), Value = r.ReadSingle(), Code = r.ReadInt32(), PlayerId = r.ReadUInt64() };
            for (int i = 0; i < fc; i++)
                if (m.FrameStarts[i] < 0 || m.FrameStarts[i] > sc) throw new InvalidDataException("Corrupt frame table");
            return m;
        }

        static int Count(BinaryReader r, int max)
        {
            int n = r.ReadInt32();
            if (n < 0 || n > max) throw new InvalidDataException("Corrupt tape count " + n);
            return n;
        }

        static void WriteV3(BinaryWriter w, Vector3 v) { w.Write(v.x); w.Write(v.y); w.Write(v.z); }
        static Vector3 ReadV3(BinaryReader r) => new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
    }
}
