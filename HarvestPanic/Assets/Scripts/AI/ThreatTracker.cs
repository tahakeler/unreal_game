using System.Collections.Generic;
using HarvestPanic.Core;
using UnityEngine;

namespace HarvestPanic.AI
{
    /// <summary>One tracked threat (player, noise location, attacker).</summary>
    public sealed class ThreatSource
    {
        /// <summary>Stable key (player key or a unique noise key).</summary>
        public int Key;
        /// <summary>NGO client id of the player behind the threat, or <see cref="PlayerIds.None"/>.</summary>
        public ulong ClientId = PlayerIds.None;
        /// <summary>Last known world position.</summary>
        public Vector3 Position;
        /// <summary>Proximity term P (0..1), refreshed each perception tick.</summary>
        public float Proximity;
        /// <summary>Sprint term S (0/1).</summary>
        public float Sprint;
        /// <summary>Sight term V (0/1).</summary>
        public float Sight;
        /// <summary>Noise term N (decays).</summary>
        public float Noise;
        /// <summary>Damage term D (decays).</summary>
        public float Damage;
        /// <summary>Last threat value computed by <see cref="ThreatTracker.Recompute"/>.</summary>
        public float Value;
        /// <summary>Time the source was last physically observed.</summary>
        public float LastSeen;
        /// <summary>True while the source was seen during the current perception tick.</summary>
        public bool SeenThisTick;
        internal Vector3 PrevObservedPos;
        internal float PrevObservedTime = -1f;
    }

    /// <summary>
    /// Per-critter threat list (critter-ai §3.2/§4). Plain C# so it can be unit-tested; the brain feeds
    /// observations and noises and reads <see cref="Total"/>, the centroid and the strongest source.
    /// </summary>
    public sealed class ThreatTracker
    {
        readonly List<ThreatSource> _sources = new List<ThreatSource>();
        readonly List<float> _values = new List<float>();
        readonly CritterTuning _t;
        int _nextNoiseKey = int.MinValue;

        /// <summary>Creates a tracker using <paramref name="tuning"/> weights and decay rates.</summary>
        public ThreatTracker(CritterTuning tuning) { _t = tuning; }

        /// <summary>Tracked sources (read-only use).</summary>
        public IReadOnlyList<ThreatSource> Sources => _sources;

        /// <summary>Total threat after the last <see cref="Recompute"/>.</summary>
        public float Total { get; private set; }

        /// <summary>Player key for a client id, so noise and sight of the same player merge.</summary>
        public static int PlayerKey(ulong clientId) => unchecked((int)clientId) ^ 0x2B5E0000;

        /// <summary>Starts a perception tick: clears per-tick terms.</summary>
        public void BeginTick()
        {
            foreach (var s in _sources) { s.SeenThisTick = false; s.Proximity = 0f; s.Sprint = 0f; s.Sight = 0f; }
        }

        /// <summary>
        /// Records that a threat at <paramref name="position"/> was observed now. Returns its horizontal speed estimate (m/s).
        /// </summary>
        public float Observe(int key, ulong clientId, Vector3 position, float now, float proximity, bool inSight)
        {
            var s = GetOrAdd(key);
            if (PlayerIds.IsPlayer(clientId)) s.ClientId = clientId;
            float speed = 0f;
            if (s.PrevObservedTime >= 0f && now > s.PrevObservedTime)
            {
                Vector3 d = position - s.PrevObservedPos; d.y = 0f;
                speed = d.magnitude / (now - s.PrevObservedTime);
            }
            s.PrevObservedPos = position; s.PrevObservedTime = now;
            s.Position = position; s.LastSeen = now; s.SeenThisTick = true;
            s.Proximity = Mathf.Clamp01(proximity);
            s.Sight = inSight ? 1f : 0f;
            s.Sprint = proximity > 0f && speed >= _t.SprintSpeedThreshold ? 1f : 0f;
            return speed;
        }

        /// <summary>Adds a perceived noise. Player noises merge into that player's source; others get their own.</summary>
        public void AddNoise(ulong blamedPlayer, Vector3 location, float perceived)
        {
            if (perceived <= 0f) return;
            ThreatSource s;
            if (PlayerIds.IsPlayer(blamedPlayer))
            {
                s = GetOrAdd(PlayerKey(blamedPlayer));
                s.ClientId = blamedPlayer;
                if (!s.SeenThisTick) s.Position = location;
            }
            else
            {
                s = GetOrAdd(_nextNoiseKey++);
                s.Position = location;
            }
            s.Noise = Mathf.Max(s.Noise, perceived);
        }

        /// <summary>Adds damage threat (hit/throw) attributed to <paramref name="clientId"/> at <paramref name="position"/>.</summary>
        public void AddDamage(ulong clientId, Vector3 position)
        {
            var s = PlayerIds.IsPlayer(clientId) ? GetOrAdd(PlayerKey(clientId)) : GetOrAdd(_nextNoiseKey++);
            if (PlayerIds.IsPlayer(clientId)) s.ClientId = clientId;
            if (!s.SeenThisTick) s.Position = position;
            s.Damage = Mathf.Max(s.Damage, _t.DamageThreat);
        }

        /// <summary>Decays noise/damage by <paramref name="dt"/>, recomputes each source and the total, drops dead sources.</summary>
        public float Recompute(float dt)
        {
            _values.Clear();
            for (int i = _sources.Count - 1; i >= 0; i--)
            {
                var s = _sources[i];
                s.Noise = Mathf.Max(0f, s.Noise - _t.NoiseDecayPerSecond * dt);
                s.Damage = Mathf.Max(0f, s.Damage - _t.DamageDecayPerSecond * dt);
                s.Value = CritterMath.SourceThreat(s.Proximity, s.Sprint, s.Sight, s.Noise, s.Damage, _t);
                if (s.Value <= 0f && !s.SeenThisTick) { _sources.RemoveAt(i); continue; }
            }
            foreach (var s in _sources) _values.Add(s.Value);
            Total = CritterMath.TotalThreat(_values, _t.OtherSourcesWeight);
            return Total;
        }

        /// <summary>Copies positive sources into parallel lists for flee queries.</summary>
        public void Collect(List<Vector3> positions, List<float> weights)
        {
            positions.Clear(); weights.Clear();
            foreach (var s in _sources) if (s.Value > 0f) { positions.Add(s.Position); weights.Add(s.Value); }
        }

        /// <summary>The highest-threat source, or null.</summary>
        public ThreatSource Strongest()
        {
            ThreatSource best = null;
            foreach (var s in _sources) if (best == null || s.Value > best.Value) best = s;
            return best;
        }

        /// <summary>Number of player sources physically within <paramref name="radius"/> of <paramref name="from"/>.</summary>
        public int PlayersWithin(Vector3 from, float radius)
        {
            int n = 0; float r2 = radius * radius;
            foreach (var s in _sources)
                if (s.SeenThisTick && (s.Position - from).sqrMagnitude <= r2) n++;
            return n;
        }

        /// <summary>Forgets everything (respawn, delivered).</summary>
        public void Clear() { _sources.Clear(); Total = 0f; }

        ThreatSource GetOrAdd(int key)
        {
            foreach (var s in _sources) if (s.Key == key) return s;
            if (_sources.Count >= Mathf.Max(1, _t.MaxThreatSources))
            {
                int weakest = 0;
                for (int i = 1; i < _sources.Count; i++) if (_sources[i].Value < _sources[weakest].Value) weakest = i;
                _sources.RemoveAt(weakest);
            }
            var n = new ThreatSource { Key = key };
            _sources.Add(n);
            return n;
        }
    }
}
