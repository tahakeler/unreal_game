using HarvestPanic.Core;
using Unity.Netcode;
using UnityEngine;

namespace HarvestPanic.AI.Night
{
    /// <summary>
    /// Night fox (GDD 8.9 roster, cheap pool creature, cost 4). Server: trots to the nearest light carryable
    /// (chickens/eggs ≤ FoxMaxMassKg), grabs it and runs back to its den where it drops the loot (recoverable).
    /// Shouts (loudness ≥ 0.6 within 12 m), a farmer within 4 m, or lantern light scare it: it drops the loot and bolts.
    /// </summary>
    public sealed class NightFox : NetworkBehaviour
    {
        public enum Mode : byte { Prowl, Carry, Flee }

        readonly NetworkVariable<byte> _nvMode = new NetworkVariable<byte>();
        NightDirector _dir; NightTuning _t;
        Vector3 _den; Mode _mode; float _nextScan, _fleeUntil;
        Transform _target, _carried; Rigidbody _rb; bool _wasKin; ICarryable _carry;

        public Mode CurrentMode => IsSpawned && !IsServer ? (Mode)_nvMode.Value : _mode;
        /// <summary>Done (escaped or delivered); the director frees its slot.</summary>
        public bool Finished { get; private set; }
        public Transform Carried => _carried;

        void Awake() { if (transform.Find("FoxVisual") == null) NightVisuals.BuildFox(transform); }
        public void Init(NightDirector d, Vector3 den) { _dir = d; _t = d.Tuning; _den = den; }
        bool Authority => IsSpawned ? IsServer : !(NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening);
        void SetMode(Mode m) { _mode = m; if (IsSpawned && IsServer) _nvMode.Value = (byte)m; }

        public void HearNoise(NoiseEvent e)
        {
            if (_t == null || Finished) return;
            if (e.Loudness >= _t.FoxScareLoudness && NightDirector.Flat(e.Location - transform.position) <= _t.FoxScareRadius) Scare();
        }

        public void Scare()
        {
            if (_mode == Mode.Flee) return;
            DropCarried();
            SetMode(Mode.Flee); _fleeUntil = Time.time + 6f;
        }

        void Update()
        {
            if (!Authority || _dir == null || Finished) return;
            if (_mode != Mode.Flee && (_dir.InSafeZone(transform.position) || PlayerNear())) Scare();
            switch (_mode)
            {
                case Mode.Prowl:
                    if (_target == null || Time.time >= _nextScan) { _nextScan = Time.time + 2f; _target = FindLoot(); }
                    if (_target == null) { Move(_den, _t.FoxSpeed * 0.5f); return; }
                    Move(_target.position, _t.FoxSpeed);
                    if (NightDirector.Flat(_target.position - transform.position) < 0.9f) Grab(_target);
                    break;
                case Mode.Carry:
                    if (_carried == null) { SetMode(Mode.Prowl); return; }
                    Move(_den, _t.FoxCarrySpeed);
                    _carried.position = transform.position + transform.forward * 0.6f + Vector3.up * 0.5f;
                    if (NightDirector.Flat(_den - transform.position) < 1.5f) { DropCarried(); Finished = true; }
                    break;
                case Mode.Flee:
                    Move(_den, _t.FoxFleeSpeed);
                    if (NightDirector.Flat(_den - transform.position) < 1.5f || Time.time > _fleeUntil + 10f) Finished = true;
                    break;
            }
        }

        bool PlayerNear()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return false;
            foreach (var c in nm.ConnectedClientsList)
                if (c.PlayerObject != null && NightDirector.Flat(c.PlayerObject.transform.position - transform.position) < _t.FoxScarePlayerRadius) return true;
            return false;
        }

        Transform FindLoot()
        {
            var hits = Physics.OverlapSphere(transform.position, 80f, (1 << 6) | (1 << 7), QueryTriggerInteraction.Ignore);
            Transform best = null; float bd = float.MaxValue;
            foreach (var h in hits)
            {
                var c = h.GetComponentInParent<ICarryable>();
                if (c == null || c.MassKg > _t.FoxMaxMassKg || h.GetComponentInParent<IKnockable>() != null || h.GetComponentInParent<NightFox>() != null) continue;
                if (_dir.InSafeZone(c.CarryTransform.position)) continue;
                float d = (c.CarryTransform.position - transform.position).sqrMagnitude;
                if (d < bd) { bd = d; best = c.CarryTransform; }
            }
            return best;
        }

        void Grab(Transform t)
        {
            _carry = t.GetComponentInParent<ICarryable>();
            _carried = t; _rb = t.GetComponent<Rigidbody>();
            _carry?.OnGrabbed(PlayerIds.Nature);
            _wasKin = _rb != null && _rb.isKinematic;
            if (_rb != null) _rb.isKinematic = true;
            SetMode(Mode.Carry);
        }

        public void DropCarried()
        {
            if (_carried == null) return;
            if (_rb != null) _rb.isKinematic = _wasKin;
            _carry?.OnReleased(PlayerIds.Nature);
            _carried = null; _rb = null; _carry = null;
        }

        void Move(Vector3 target, float speed)
        {
            Vector3 d = target - transform.position; d.y = 0f;
            if (d.sqrMagnitude < 0.01f) return;
            var next = NightDirector.Ground(transform.position + Vector3.ClampMagnitude(d, speed * Time.deltaTime));
            transform.SetPositionAndRotation(next, Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d.normalized), 10f * Time.deltaTime));
        }

        public override void OnNetworkDespawn() { if (IsServer) DropCarried(); }
    }
}
