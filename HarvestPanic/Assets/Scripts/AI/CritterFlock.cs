using System.Collections.Generic;
using UnityEngine;

namespace HarvestPanic.AI
{
    /// <summary>
    /// Flock group for <c>HerdMode.Flock</c> critters (chickens): one alarm scatters everyone outward, then the
    /// group regroups at its centre after <see cref="CritterTuning.FlockRegroupSeconds"/> (critter-ai §3.4, livestock-roster).
    /// </summary>
    public sealed class CritterFlock
    {
        static readonly List<CritterFlock> Flocks = new List<CritterFlock>();
        readonly List<CritterBrain> _members = new List<CritterBrain>();

        /// <summary>Species id the flock is made of.</summary>
        public string CritterId { get; }
        /// <summary>Members.</summary>
        public IReadOnlyList<CritterBrain> Members => _members;
        /// <summary>Time of the last scatter (-inf when never).</summary>
        public float LastScatterTime { get; private set; } = float.NegativeInfinity;
        /// <summary>Threat centroid of the last scatter.</summary>
        public Vector3 LastScatterOrigin { get; private set; }

        CritterFlock(string id) { CritterId = id; }

        /// <summary>Joins the nearest flock of the same species within <paramref name="joinRadius"/>, or starts one.</summary>
        public static CritterFlock Join(CritterBrain b, float joinRadius)
        {
            string id = b.Personality != null ? b.Personality.CritterId : "";
            CritterFlock best = null; float bestD = joinRadius * joinRadius;
            foreach (var f in Flocks)
            {
                if (f.CritterId != id || f._members.Count == 0) continue;
                float d = (f.Centre() - b.transform.position).sqrMagnitude;
                if (d <= bestD) { bestD = d; best = f; }
            }
            if (best == null) { best = new CritterFlock(id); Flocks.Add(best); }
            best._members.Add(b);
            return best;
        }

        /// <summary>Leaves the flock (destroyed/despawned/sold).</summary>
        public void Leave(CritterBrain b)
        {
            _members.Remove(b);
            if (_members.Count == 0) Flocks.Remove(this);
        }

        /// <summary>Average position of members not being carried.</summary>
        public Vector3 Centre()
        {
            Vector3 sum = Vector3.zero; int n = 0;
            foreach (var m in _members)
                if (m != null && m.State != CritterState.Carried) { sum += m.transform.position; n++; }
            return n > 0 ? sum / n : Vector3.zero;
        }

        /// <summary>One member panicked: alarm every other member (they flee outward from the centre).</summary>
        public void Scatter(CritterBrain source, Vector3 threatCentroid, float now, float alarmThreat)
        {
            if (now - LastScatterTime < 0.5f) return;
            LastScatterTime = now;
            LastScatterOrigin = threatCentroid;
            foreach (var m in _members)
                if (m != null && m != source) m.ReceiveFlockAlarm(threatCentroid, alarmThreat);
        }

        /// <summary>True once the regroup delay after the last scatter has passed.</summary>
        public bool ShouldRegroup(float now, float regroupSeconds) => now - LastScatterTime >= regroupSeconds;

        internal static void ResetAll() => Flocks.Clear();
    }
}
