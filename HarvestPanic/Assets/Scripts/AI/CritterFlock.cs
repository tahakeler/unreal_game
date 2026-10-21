using System.Collections.Generic;
using HarvestPanic.Core;
using UnityEngine;

namespace HarvestPanic.AI
{
    /// <summary>
    /// Herd group for <c>HerdMode.Flock</c> (chickens: one alarm scatters everyone outward, regroup after
    /// <see cref="CritterTuning.FlockRegroupSeconds"/>) and <c>HerdMode.Follow</c> (sheep: members follow the oldest
    /// member, the bell-wether, and stampede together on one shared heading). critter-ai §3.4, livestock-roster §4.
    /// </summary>
    public sealed class CritterFlock
    {
        static readonly List<CritterFlock> Flocks = new List<CritterFlock>();
        readonly List<CritterBrain> _members = new List<CritterBrain>();

        /// <summary>Species id the flock is made of.</summary>
        public string CritterId { get; }
        /// <summary>Herd mode of the species.</summary>
        public HerdMode Mode { get; }
        /// <summary>Members in join order (index 0 = oldest).</summary>
        public IReadOnlyList<CritterBrain> Members => _members;
        /// <summary>Time of the last scatter (-inf when never).</summary>
        public float LastScatterTime { get; private set; } = float.NegativeInfinity;
        /// <summary>Threat centroid of the last scatter.</summary>
        public Vector3 LastScatterOrigin { get; private set; }
        /// <summary>Follow mode: heading shared by the current stampede.</summary>
        public Vector3 StampedeHeading { get; private set; }
        /// <summary>Follow mode: when <see cref="StampedeHeading"/> was set.</summary>
        public float StampedeTime { get; private set; } = float.NegativeInfinity;
        /// <summary>Follow mode: when the last leader left (-inf when never).</summary>
        public float LeaderLostAt { get; private set; } = float.NegativeInfinity;

        CritterFlock(string id, HerdMode mode) { CritterId = id; Mode = mode; }

        /// <summary>Joins the nearest flock of the same species within <paramref name="joinRadius"/>, or starts one.</summary>
        public static CritterFlock Join(CritterBrain b, float joinRadius)
        {
            string id = b.Personality != null ? b.Personality.CritterId : "";
            var mode = b.Personality != null ? b.Personality.HerdMode : HerdMode.Flock;
            CritterFlock best = null; float bestD = joinRadius * joinRadius;
            foreach (var f in Flocks)
            {
                if (f.CritterId != id || f._members.Count == 0) continue;
                float d = (f.Centre() - b.transform.position).sqrMagnitude;
                if (d <= bestD) { bestD = d; best = f; }
            }
            if (best == null) { best = new CritterFlock(id, mode); Flocks.Add(best); }
            best._members.Add(b);
            return best;
        }

        /// <summary>Leaves the flock (destroyed/despawned/sold). Losing the leader starts the re-elect timer.</summary>
        public void Leave(CritterBrain b)
        {
            int i = _members.IndexOf(b);
            if (i < 0) return;
            bool wasLeader = Mode == HerdMode.Follow && ReferenceEquals(LeaderAt(Time.time, float.MaxValue), b);
            _members.RemoveAt(i);
            if (wasLeader) LeaderLostAt = Time.time;
            if (_members.Count == 0) Flocks.Remove(this);
        }

        /// <summary>Follow mode: the bell-wether (oldest member), or null for <paramref name="reelectSeconds"/> after the last one left.</summary>
        public CritterBrain Leader(float now, float reelectSeconds) => LeaderAt(now, reelectSeconds);

        CritterBrain LeaderAt(float now, float reelectSeconds)
        {
            if (Mode != HerdMode.Follow) return null;
            for (int i = _members.Count - 1; i >= 0; i--) if (_members[i] == null) _members.RemoveAt(i);
            int idx = reelectSeconds >= float.MaxValue ? (_members.Count > 0 ? 0 : -1) : FlockRules.LeaderIndex(_members.Count, now, LeaderLostAt, reelectSeconds);
            return idx >= 0 ? _members[idx] : null;
        }

        /// <summary>Average position of members not being carried.</summary>
        public Vector3 Centre()
        {
            Vector3 sum = Vector3.zero; int n = 0;
            foreach (var m in _members)
                if (m != null && m.State != CritterState.Carried) { sum += m.transform.position; n++; }
            return n > 0 ? sum / n : Vector3.zero;
        }

        /// <summary>One member panicked: alarm every other member (Flock: scatter outward; Follow: stampede together).</summary>
        public void Scatter(CritterBrain source, Vector3 threatCentroid, float now, float alarmThreat)
        {
            if (now - LastScatterTime < 0.5f) return;
            LastScatterTime = now;
            LastScatterOrigin = threatCentroid;
            foreach (var m in _members)
                if (m != null && m != source) m.ReceiveFlockAlarm(threatCentroid, alarmThreat);
        }

        /// <summary>Follow mode: returns the shared stampede heading (setting it from <paramref name="ownAway"/> when stale).</summary>
        public Vector3 ShareStampede(Vector3 ownAway, float now, float shareSeconds)
        {
            bool fresh = now - StampedeTime < shareSeconds;
            var h = FlockRules.StampedeHeading(ownAway, StampedeHeading, fresh);
            if (!fresh) { StampedeHeading = h; StampedeTime = now; }
            return h;
        }

        /// <summary>True once the regroup delay after the last scatter has passed.</summary>
        public bool ShouldRegroup(float now, float regroupSeconds) => now - LastScatterTime >= regroupSeconds;

        internal static void ResetAll() => Flocks.Clear();
    }
}
