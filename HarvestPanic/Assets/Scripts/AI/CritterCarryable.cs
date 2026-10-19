using System;
using System.Collections.Generic;
using HarvestPanic.Core;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

namespace HarvestPanic.AI
{
    /// <summary>
    /// <see cref="ICarryable"/> for critters (physics-carry rule 6, critter-ai Carried state). Server-side:
    /// grabbing disables navigation and makes the body kinematic (the carry system moves <see cref="CarryTransform"/>);
    /// while held the critter struggles every StruggleInterval and may break free when fewer than
    /// <c>SecureCarriers</c> players hold it (see <see cref="CritterMath.BreakFreeChance(int,int,CritterTuning)"/>).
    /// Screams critters (pig) emit a Squeal noise when grabbed.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CritterBrain))]
    public sealed class CritterCarryable : NetworkBehaviour, ICarryable
    {
        [SerializeField, Tooltip("Rigid attach (no spring wobble). Critters are carried kinematically.")] bool kinematicCarry = true;

        readonly List<ulong> _holders = new List<ulong>(4);
        CritterBrain _brain;
        CritterGoods _goods;
        Rigidbody _rb;
        float _nextStruggle;
        ulong _lastThrower = PlayerIds.None;

        /// <summary>Raised on the server at every struggle with the wriggle impulse (N*s) the holders should feel.</summary>
        public event Action<Vector3> Struggled;
        /// <summary>Raised on the server when the critter wriggles free; the listed holders must drop it.</summary>
        public event Action<IReadOnlyList<ulong>> BrokeFree;

        /// <summary>Players currently holding the critter.</summary>
        public IReadOnlyList<ulong> Holders => _holders;
        /// <summary>True while at least one player holds it.</summary>
        public bool IsHeld => _holders.Count > 0;
        /// <summary>Number of struggles since the last grab (tests/debug).</summary>
        public int StruggleCount { get; private set; }

        /// <inheritdoc/>
        public Transform CarryTransform => transform;
        /// <inheritdoc/>
        public int HandsRequired => _goods != null && _goods.Definition != null ? _goods.Definition.CarryHands : 1;
        /// <inheritdoc/>
        public float MassKg => _goods != null && _goods.Definition != null ? _goods.Definition.MassKg : (_rb != null ? _rb.mass : 5f);
        /// <inheritdoc/>
        public bool IsKinematicCarry => kinematicCarry;

        bool Authority => !IsSpawned || IsServer;

        void Awake()
        {
            _brain = GetComponent<CritterBrain>();
            _goods = GetComponent<CritterGoods>();
            _rb = GetComponent<Rigidbody>();
        }

        /// <inheritdoc/>
        public void OnGrabbed(ulong clientId)
        {
            if (!Authority || _holders.Contains(clientId)) return;
            bool first = _holders.Count == 0;
            _holders.Add(clientId);
            _goods?.RecordTouch(clientId);
            if (!first) return;
            StruggleCount = 0;
            _lastThrower = PlayerIds.None;
            _brain.BeginCarried();
            ScheduleStruggle();
            var p = _brain.Personality;
            if (p != null && (p.ComedyFlags & ComedyFlag.Screams) != 0 && Services.TryGet(out INoiseService noise))
                noise.Emit(NoiseSource.Critter, _brain.Tuning.GrabSquealLoudness, transform.position, IsSpawned ? NetworkObjectId : 0UL, clientId, NoiseTag.Squeal);
        }

        /// <inheritdoc/>
        public void OnReleased(ulong clientId)
        {
            if (!Authority || !_holders.Remove(clientId)) return;
            _goods?.RecordTouch(clientId);
            if (_holders.Count == 0 && _brain.State == CritterState.Carried)
                _brain.EndCarried(_rb != null ? _rb.linearVelocity : Vector3.zero, _brain.Tuning.DropStunSeconds);
        }

        /// <inheritdoc/>
        public void ApplyThrow(Vector3 velocity, ulong thrower)
        {
            if (!Authority) return;
            _lastThrower = thrower;
            _goods?.RecordTouch(thrower);
            _holders.Clear();
            Detach();
            _brain.AddDamage(thrower, transform.position);
            _brain.EndCarried(velocity, _brain.Tuning.StunSeconds);
        }

        /// <summary>Last player who threw the critter (bruise attribution).</summary>
        public ulong LastThrower => _lastThrower;

        void Update()
        {
            if (!Authority || _holders.Count == 0 || _brain.State != CritterState.Carried) return;
            if (Time.time < _nextStruggle) return;
            Struggle();
            ScheduleStruggle();
        }

        /// <summary>Server: one struggle step (wriggle impulse + break-free roll). Public for tests.</summary>
        public bool Struggle(float? roll = null)
        {
            var p = _brain.Personality;
            if (p == null || _holders.Count == 0) return false;
            StruggleCount++;
            Vector3 dir = Random.onUnitSphere; dir.y = Mathf.Abs(dir.y);
            Vector3 impulse = dir * p.StruggleImpulse;
            Struggled?.Invoke(impulse);
            if (_rb != null && !_rb.isKinematic) _rb.AddForce(impulse, ForceMode.Impulse);

            float chance = CritterMath.BreakFreeChance(p.SecureCarriers, _holders.Count, _brain.Tuning);
            if ((roll ?? Random.value) >= chance) return false;
            BreakFree();
            return true;
        }

        void BreakFree()
        {
            var released = new List<ulong>(_holders);
            _holders.Clear();
            Detach();
            foreach (var id in released) _brain.AddDamage(id, transform.position);
            BrokeFree?.Invoke(released);
            Vector3 hop = Random.onUnitSphere; hop.y = 0f;
            hop = hop.normalized * _brain.Tuning.BreakFreeHopSpeed + Vector3.up * 2f;
            _brain.EndCarried(hop, _brain.Tuning.BreakFreeStunSeconds);
        }

        void Detach()
        {
            if (transform.parent == null) return;
            if (IsSpawned) NetworkObject.TryRemoveParent();
            else transform.SetParent(null, true);
        }

        void ScheduleStruggle()
        {
            var p = _brain.Personality;
            float min = p != null ? p.StruggleIntervalMin : 0.5f, max = p != null ? p.StruggleIntervalMax : 1.5f;
            _nextStruggle = Time.time + Random.Range(min, Mathf.Max(min, max));
        }

        void OnCollisionEnter(Collision c)
        {
            if (!Authority || _goods == null || _brain.State != CritterState.Stunned) return;
            float impulse = ImpactUnits.ToDesign(c.impulse.magnitude) * _brain.Tuning.ImpactImpulseScale;
            ulong who = PlayerIds.IsPlayer(_lastThrower) ? _lastThrower : _goods.LastHandlerId;
            _goods.ApplyBruise(impulse, who, PlayerIds.IsPlayer(_lastThrower) ? BlameVerb.Threw : BlameVerb.Dropped);
        }
    }
}
