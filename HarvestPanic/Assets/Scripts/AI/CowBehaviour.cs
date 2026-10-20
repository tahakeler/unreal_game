using HarvestPanic.Core;
using Unity.Netcode;
using UnityEngine;

namespace HarvestPanic.AI
{
    /// <summary>
    /// Cow ("Big Martha") signature task (livestock-roster §3.1): one loud moo per ~20 s (Critter noise 0.5, routed to
    /// <c>SFX_Critter_Cow_Moo</c> by Audio) plus a complaint moo on grab. Stubbornness itself is data
    /// (slow, high FleeThreshold, Freeze when cornered, <c>SecureCarriers</c> 2 and a 4-hand carry). Server only.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CritterBrain))]
    public sealed class CowBehaviour : NetworkBehaviour
    {
        [SerializeField] LivestockTuning tuning;
        CritterBrain _brain;
        float _nextMoo = -1f;

        /// <summary>Moos emitted (tests/debug).</summary>
        public int MooCount { get; private set; }
        public LivestockTuning Tuning => tuning != null ? tuning : LivestockTuning.Default;
        bool Authority => !IsSpawned || IsServer;

        public void Configure(LivestockTuning t) => tuning = t;

        void Awake()
        {
            _brain = GetComponent<CritterBrain>();
            _brain.StateChanged += OnState;
        }

        public override void OnDestroy() { if (_brain != null) _brain.StateChanged -= OnState; base.OnDestroy(); }

        void OnState(CritterState prev, CritterState next)
        {
            if (Authority && next == CritterState.Carried && Tuning.MooOnGrab) Moo();
        }

        void Update()
        {
            if (!Authority) return;
            float now = Time.time;
            if (_nextMoo < 0f) { _nextMoo = LivestockRules.NextMoo(now, Random.value, Tuning) - Tuning.MooInterval * 0.5f; return; }
            if (now < _nextMoo) return;
            _nextMoo = LivestockRules.NextMoo(now, Random.value, Tuning);
            Moo();
        }

        /// <summary>Server: emit one moo noise. Public for tests.</summary>
        public void Moo()
        {
            MooCount++;
            if (Services.TryGet(out INoiseService noise))
                noise.Emit(NoiseSource.Critter, Tuning.MooLoudness, transform.position, IsSpawned ? NetworkObjectId : 0UL, PlayerIds.None, NoiseTag.None);
        }
    }
}
