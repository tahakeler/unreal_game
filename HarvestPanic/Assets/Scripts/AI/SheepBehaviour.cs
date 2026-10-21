using HarvestPanic.Core;
using Unity.Netcode;
using UnityEngine;

namespace HarvestPanic.AI
{
    /// <summary>
    /// Sheep signature task (livestock-roster §3.1 #4). Herding itself is data + <see cref="CritterFlock"/> Follow mode;
    /// this adds the presentation: a baa every ~14 s (Critter noise 0.35, routed to <c>SFX_Critter_Sheep_Baa</c>), a
    /// louder panic baa when the flock stampedes, a complaint baa on grab, and the bell-wether's bell (replicated leader
    /// flag shows the <see cref="bell"/> child only on the leader so players know whom to grab). Server decides; clients render.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CritterBrain))]
    public sealed class SheepBehaviour : NetworkBehaviour
    {
        [SerializeField] LivestockTuning tuning;
        [SerializeField, Tooltip("Bell-wether bell (shown only on the flock leader)")] GameObject bell;

        readonly NetworkVariable<bool> _netLeader = new NetworkVariable<bool>();
        CritterBrain _brain;
        float _nextBaa = -1f, _lastBaa = -99f, _nextLeaderCheck;
        bool _leader;

        /// <summary>Baas emitted (tests/debug).</summary>
        public int BaaCount { get; private set; }
        /// <summary>True when this sheep is the bell-wether (replicated).</summary>
        public bool IsLeader => IsSpawned && !IsServer ? _netLeader.Value : _leader;
        public LivestockTuning Tuning => tuning != null ? tuning : LivestockTuning.Default;
        bool Authority => !IsSpawned || IsServer;

        public void Configure(LivestockTuning t, GameObject bellObject) { tuning = t; bell = bellObject; }

        void Awake()
        {
            _brain = GetComponent<CritterBrain>();
            _brain.StateChanged += OnState;
            if (bell != null) bell.SetActive(false);
        }

        public override void OnNetworkSpawn()
        {
            _netLeader.OnValueChanged += (_, v) => ShowBell(v);
            if (IsServer) _netLeader.Value = _leader; else ShowBell(_netLeader.Value);
        }

        public override void OnDestroy() { if (_brain != null) _brain.StateChanged -= OnState; base.OnDestroy(); }

        void OnState(CritterState prev, CritterState next)
        {
            if (!Authority) return;
            if (next == CritterState.Carried) Baa(Tuning.PanicBaaLoudness);
            else if (next == CritterState.Flee && (_leader || Random.value < 0.35f)) Baa(Tuning.PanicBaaLoudness);
        }

        void Update()
        {
            if (!Authority) return;
            float now = Time.time;
            if (now >= _nextLeaderCheck)
            {
                _nextLeaderCheck = now + 0.25f;
                bool l = _brain.IsFlockLeader;
                if (l != _leader) { _leader = l; ShowBell(l); if (IsSpawned) _netLeader.Value = l; }
            }
            if (_nextBaa < 0f) { _nextBaa = now + Random.Range(1f, Tuning.BaaInterval); return; }
            if (now < _nextBaa) return;
            _nextBaa = now + Mathf.Max(1f, Tuning.BaaInterval + Random.Range(-Tuning.BaaJitter, Tuning.BaaJitter));
            if (_brain.State == CritterState.Idle || _brain.State == CritterState.Wander) Baa(Tuning.BaaLoudness);
        }

        void ShowBell(bool on) { if (bell != null) bell.SetActive(on); }

        /// <summary>Server: emit one baa noise (rate limited). Public for tests.</summary>
        public bool Baa(float loudness)
        {
            if (Time.time - _lastBaa < Tuning.BaaCooldown) return false;
            _lastBaa = Time.time;
            BaaCount++;
            if (Services.TryGet(out INoiseService noise))
                noise.Emit(NoiseSource.Critter, loudness, transform.position, IsSpawned ? NetworkObjectId : 0UL, PlayerIds.None, NoiseTag.None);
            return true;
        }
    }
}
