using HarvestPanic.Core;
using Unity.Netcode;
using UnityEngine;

namespace HarvestPanic.AI.Crops
{
    /// <summary>
    /// Tumble Melon (cursed-crops §3 #5): a 4-hand cursed melon that will not stay put. Server: once pulled from its
    /// field, every time it is set down (released or thrown and come to rest) it waits <see cref="CursedCropTuning.SettleSeconds"/>,
    /// wobbles for <see cref="CursedCropTuning.WobbleSeconds"/> (readable tell, replicated) and rolls: downhill on a slope,
    /// back toward its field when outside the leash, otherwise away from the nearest player (Juke). A player walking
    /// up to a resting melon spooks it into a roll (cooldown). Counter: wedge it, wagon, or just keep holding it.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CursedCropGoods))]
    public sealed class TumbleMelon : NetworkBehaviour
    {
        public enum Phase : byte { Planted, Held, Settling, Wobble, Rolling }

        [SerializeField] CursedCropTuning tuning;
        [SerializeField, Tooltip("Visual root wobbled before a roll (client-local)")] Transform visual;

        readonly NetworkVariable<byte> _netPhase = new NetworkVariable<byte>();
        CursedCropGoods _goods;
        Rigidbody _rb;
        Phase _phase;
        float _phaseAt, _lastSpook = float.NegativeInfinity;
        Quaternion _visualHome = Quaternion.identity;

        /// <summary>Field centre the melon belongs to (set by <see cref="CursedCropField"/>).</summary>
        public Vector3 Home { get; set; }
        /// <summary>Rolls started (tests/debug).</summary>
        public int RollCount { get; private set; }
        /// <summary>Flat direction of the last roll.</summary>
        public Vector3 LastRollDir { get; private set; }
        public Phase Current => IsSpawned && !IsServer ? (Phase)_netPhase.Value : _phase;
        public CursedCropTuning Tuning => tuning != null ? tuning : CursedCropTuning.Default;
        bool Authority => !IsSpawned || IsServer;

        public void Configure(CursedCropTuning t, Transform visualRoot) { tuning = t; visual = visualRoot; }

        void Awake()
        {
            _goods = GetComponent<CursedCropGoods>();
            _rb = GetComponent<Rigidbody>();
            Home = transform.position;
            if (visual != null) _visualHome = visual.localRotation;
            _goods.Grabbed += (_, __) => Enter(Phase.Held);
            _goods.Released += (_, none) => { if (none) Enter(Phase.Settling); };
            _phase = _goods.Planted ? Phase.Planted : Phase.Settling;
        }

        void Enter(Phase p)
        {
            _phase = p; _phaseAt = Time.time;
            if (IsSpawned && IsServer) _netPhase.Value = (byte)p;
        }

        void FixedUpdate()
        {
            if (!Authority || _rb == null) return;
            if (_goods.Planted) { if (_phase != Phase.Planted) Enter(Phase.Planted); return; }
            if (_goods.IsHeld) { if (_phase != Phase.Held) Enter(Phase.Held); return; }
            var t = Tuning;
            float now = Time.time;
            bool resting = _rb.linearVelocity.magnitude < t.RestSpeed;
            switch (_phase)
            {
                case Phase.Planted:
                case Phase.Held:
                    Enter(Phase.Settling);
                    break;
                case Phase.Settling:
                    if (!resting) { _phaseAt = now; break; }
                    if (now - _phaseAt >= t.SettleSeconds) Enter(Phase.Wobble);
                    break;
                case Phase.Wobble:
                    if (now - _phaseAt >= t.WobbleSeconds) Roll();
                    break;
                case Phase.Rolling:
                    if (now - _phaseAt > 0.4f && resting) Enter(Phase.Settling);
                    break;
            }
            if (_phase == Phase.Settling && resting && now - _lastSpook >= t.SpookCooldown && NearestPlayer(t.SpookRadius, out _))
            {
                _lastSpook = now;
                Enter(Phase.Wobble);
            }
        }

        /// <summary>Server: rolls now (public for tests/tools). Returns the flat roll direction.</summary>
        public Vector3 Roll()
        {
            var t = Tuning;
            Vector3 pos = transform.position;
            Vector3 normal = Physics.Raycast(pos + Vector3.up * 0.5f, Vector3.down, out var hit, 3f, ~LayerMask.GetMask(GameLayers.Player, GameLayers.Critter, GameLayers.Carryable), QueryTriggerInteraction.Ignore)
                ? hit.normal : Vector3.up;
            Vector3? player = NearestPlayer(t.PlayerAwareness, out var pp) ? pp : (Vector3?)null;
            Vector3 fallback = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
            Vector3 dir = CursedCropRules.RollDirection(normal, t.DownhillSlopeDeg, pos, Home, t.LeashRadius, player, fallback);
            if (_rb != null && !_rb.isKinematic)
            {
                _rb.linearVelocity = dir * t.RollSpeed + Vector3.up * 0.3f;
                _rb.angularVelocity = Vector3.Cross(Vector3.up, dir) * (t.RollSpeed / 0.35f);
            }
            LastRollDir = dir;
            RollCount++;
            Enter(Phase.Rolling);
            return dir;
        }

        bool NearestPlayer(float radius, out Vector3 at)
        {
            at = default;
            int mask = LayerMask.GetMask(GameLayers.Player);
            if (mask == 0 || radius <= 0f) return false;
            var hits = Physics.OverlapSphere(transform.position, radius, mask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue; bool any = false;
            foreach (var h in hits)
            {
                float d = (h.transform.position - transform.position).sqrMagnitude;
                if (d < best) { best = d; at = h.transform.position; any = true; }
            }
            return any;
        }

        void Update()
        {
            if (visual == null) return;
            if (Current == Phase.Wobble)
                visual.localRotation = _visualHome * Quaternion.Euler(Mathf.Sin(Time.time * 28f) * 9f, 0f, Mathf.Cos(Time.time * 23f) * 9f);
            else visual.localRotation = _visualHome;
        }
    }
}
