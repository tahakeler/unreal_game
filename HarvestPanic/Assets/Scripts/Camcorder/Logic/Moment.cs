using System;
using System.Collections.Generic;
using HarvestPanic.Core;
using UnityEngine;

namespace HarvestPanic.Camcorder
{
    /// <summary>
    /// A saved tape: compact copy of the ring frames between Start and End plus disaster metadata.
    /// Times are relative to the first frame (0 .. <see cref="Duration"/>).
    /// </summary>
    public sealed class Moment
    {
        public long Id;
        public string Title = "";
        public bool Manual;
        public int DayIndex;
        public byte Severity;
        public int DollarDelta;
        public int InstigatorCount;
        public BlameVerb Verb;
        public ulong PrimaryInstigator = PlayerIds.None;
        public Vector3 Focus;
        /// <summary>Recording-clock time of frame 0 (used to re-cut an extended tape).</summary>
        public float SourceStart;
        /// <summary>Trigger time relative to frame 0 (the "impact").</summary>
        public float TriggerTime;

        public float[] FrameTimes = Array.Empty<float>();
        public int[] FrameStarts = Array.Empty<int>();
        public ReplaySample[] Samples = Array.Empty<ReplaySample>();
        public ReplayEvent[] Events = Array.Empty<ReplayEvent>();
        public List<TrackInfo> Tracks = new List<TrackInfo>();

        /// <summary>Live "best shot" picture taken at the impact (runtime only, not serialized).</summary>
        [NonSerialized] public Texture2D Thumbnail;
        /// <summary>Folder of the last export, if any.</summary>
        [NonSerialized] public string ExportFolder;

        public int FrameCount => FrameTimes.Length;
        public float Duration => FrameTimes.Length == 0 ? 0f : FrameTimes[FrameTimes.Length - 1];

        /// <summary>GDD keep score: Severity + 30 * manual + 10 * instigators.</summary>
        public int KeepScore => Severity + (Manual ? 30 : 0) + 10 * InstigatorCount;

        public int SampleCountAt(int frame) =>
            (frame + 1 < FrameStarts.Length ? FrameStarts[frame + 1] : Samples.Length) - FrameStarts[frame];

        /// <summary>Approximate bytes held (budget accounting).</summary>
        public long MemoryBytes =>
            (long)Samples.Length * ReplaySample.SizeBytes + FrameTimes.Length * 8L + Events.Length * 40L + Tracks.Count * 48L +
            (Thumbnail != null ? (long)Thumbnail.width * Thumbnail.height * 3 : 0);

        /// <summary>Last frame index with time &lt;= t (clamped).</summary>
        public int FrameAt(float t)
        {
            int lo = 0, hi = FrameTimes.Length - 1;
            if (hi < 0) return -1;
            if (t <= FrameTimes[0]) return 0;
            if (t >= FrameTimes[hi]) return hi;
            while (lo < hi) { int mid = (lo + hi + 1) >> 1; if (FrameTimes[mid] <= t) lo = mid; else hi = mid - 1; }
            return lo;
        }

        /// <summary>Finds actor <paramref name="id"/> in <paramref name="frame"/>.</summary>
        public bool TryGetSample(int frame, int id, out ReplaySample s)
        {
            int a = FrameStarts[frame], b = a + SampleCountAt(frame);
            for (int i = a; i < b; i++) if (Samples[i].Id == id) { s = Samples[i]; return true; }
            s = default; return false;
        }

        /// <summary>Interpolated pose of actor <paramref name="id"/> at time <paramref name="t"/>; false when absent.</summary>
        public bool TryEvaluate(int id, float t, out Vector3 pos, out Quaternion rot, out SampleFlags flags)
        {
            pos = default; rot = Quaternion.identity; flags = SampleFlags.None;
            int f = FrameAt(t);
            if (f < 0 || !TryGetSample(f, id, out var a)) return false;
            pos = a.Position; rot = a.Rotation; flags = a.Flags;
            if (f + 1 < FrameTimes.Length && TryGetSample(f + 1, id, out var b))
            {
                float span = FrameTimes[f + 1] - FrameTimes[f];
                float u = span > 1e-5f ? Mathf.Clamp01((t - FrameTimes[f]) / span) : 0f;
                pos = Vector3.LerpUnclamped(a.Position, b.Position, u);
                rot = Quaternion.Slerp(a.Rotation, b.Rotation, u);
            }
            return true;
        }

        public bool TryGetTrack(int id, out TrackInfo info)
        {
            for (int i = 0; i < Tracks.Count; i++) if (Tracks[i].Id == id) { info = Tracks[i]; return true; }
            info = default; return false;
        }

        /// <summary>Track id of the player owned by <paramref name="clientId"/>, or -1.</summary>
        public int PlayerTrackFor(ulong clientId)
        {
            for (int i = 0; i < Tracks.Count; i++)
                if (Tracks[i].Kind == TrackKind.Player && Tracks[i].OwnerClientId == clientId) return Tracks[i].Id;
            return -1;
        }

        public void ReleaseRuntime()
        {
            if (Thumbnail != null) { UnityEngine.Object.Destroy(Thumbnail); Thumbnail = null; }
        }
    }
}
