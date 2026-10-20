using HarvestPanic.Core;
using Unity.Netcode;
using UnityEngine;

namespace HarvestPanic.Gameplay
{
    /// <summary>
    /// Net (design/gdd/farm-tools.md rule 2): Use swings (0.5 s wind-up, 1 s cooldown); the server checks a
    /// 1.5 m, 90 degree arc and scoops the nearest free carryable needing at most 1 hand into the bag. The catch
    /// rides in the net (also when the net is dropped or thrown). Use again empties the bag in front.
    /// Living catches (Critter layer or IGoods.IsAlive) escape 20% per 5 s; catch chance uses the GDD
    /// formula with the default agility because agility is not exposed through Core contracts.
    /// </summary>
    public sealed class NetTool : CarryableProp, IUsableTool
    {
        [SerializeField] ToolTuning tuning;
        [SerializeField] Transform bagPoint;

        readonly NetworkVariable<bool> _full = new NetworkVariable<bool>(false);
        static readonly Collider[] Overlaps = new Collider[32];
        ICarryable _caught;
        Rigidbody _caughtBody;
        bool _caughtWasKinematic, _caughtAlive;
        float _swingAt = -1f, _readyAt, _nextEscapeRoll;
        PlayerCarry _user;

        /// <summary>True while something is in the bag.</summary>
        public bool IsFull => _full.Value;
        /// <summary>Server: the caught object.</summary>
        public ICarryable Caught => _caught;

        /// <inheritdoc />
        public string UsePrompt => _full.Value ? "RMB: Empty net" : "RMB: Swing net";

        /// <inheritdoc />
        public void ServerUse(bool down, PlayerCarry user)
        {
            if (!down || Time.time < _readyAt) return;
            if (_caught != null) { Release(user.HeadPivotForward()); return; }
            _user = user;
            _swingAt = Time.time + tuning.NetWindUp;
            _readyAt = Time.time + tuning.NetWindUp + tuning.NetCooldown;
            SwingVisualRpc();
        }

        [Rpc(SendTo.ClientsAndHost)]
        void SwingVisualRpc() => _swingVisualUntil = Time.time + 0.6f;
        float _swingVisualUntil;

        void Update()
        {
            if (transform.childCount > 0)
            {
                float k = Mathf.Clamp01((_swingVisualUntil - Time.time) / 0.6f);
                transform.GetChild(0).localRotation = Quaternion.Euler(Mathf.Sin(k * Mathf.PI) * 70f, 0f, 0f);
            }
            if (!IsServer) return;
            if (_swingAt > 0f && Time.time >= _swingAt) { _swingAt = -1f; DoScoop(); }
            if (_caught != null && _caughtAlive && Time.time >= _nextEscapeRoll)
            {
                _nextEscapeRoll = Time.time + tuning.NetEscapeInterval;
                if (Random.value < tuning.NetEscapeChance) Release(transform.forward);
            }
        }

        void FixedUpdate()
        {
            if (!IsServer || _caught == null) return;
            if (_caughtBody == null || _caught.CarryTransform == null) { _caught = null; _full.Value = false; return; }
            Vector3 p = bagPoint != null ? bagPoint.position : transform.position;
            _caughtBody.MovePosition(p);
        }

        void DoScoop()
        {
            if (_user == null || _caught != null) return;
            var head = _user.Character.HeadPivot;
            Vector3 origin = head.position + Vector3.down * 0.6f;
            Vector3 fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            int n = Physics.OverlapSphereNonAlloc(origin + fwd * tuning.NetReach * 0.5f, tuning.NetReach, Overlaps, ~0, QueryTriggerInteraction.Ignore);
            ICarryable best = null; float bestD = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var c = Overlaps[i].GetComponentInParent<ICarryable>();
                if (c == null || ReferenceEquals(c, this) || c is PlayerCharacter || c.HandsRequired > tuning.NetMaxHands) continue;
                if (c.CarryTransform.GetComponent<CarryBody>() is CarryBody cb && cb.GripCount > 0) continue;
                Vector3 to = c.CarryTransform.position - origin; to.y = 0f;
                if (Vector3.Angle(fwd, to) > tuning.NetArc * 0.5f || to.magnitude > tuning.NetReach + 0.5f) continue;
                if (to.sqrMagnitude < bestD) { bestD = to.sqrMagnitude; best = c; }
            }
            if (best == null) return;

            bool alive = best.CarryTransform.gameObject.layer == LayerMask.NameToLayer(GameLayers.Critter)
                || (best.CarryTransform.GetComponentInParent<IGoods>() is IGoods g && g.Definition != null && g.Definition.IsAlive);
            if (alive && Random.value > CarryMath.NetCatchChance(tuning.NetDefaultAgility)) return;

            _caught = best;
            _caughtAlive = alive;
            _caughtBody = best.CarryTransform.GetComponent<Rigidbody>();
            if (_caughtBody == null) { _caught = null; return; }
            _caughtWasKinematic = _caughtBody.isKinematic;
            _caughtBody.isKinematic = true;
            foreach (var col in best.CarryTransform.GetComponentsInChildren<Collider>()) foreach (var mine in GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(col, mine, true);
            best.OnGrabbed(_user.OwnerClientId);
            best.CarryTransform.GetComponentInParent<IGoods>()?.RecordTouch(_user.OwnerClientId);
            _nextEscapeRoll = Time.time + tuning.NetEscapeInterval;
            _full.Value = true;
        }

        void Release(Vector3 forward)
        {
            if (_caught == null) return;
            var c = _caught;
            _caught = null;
            _full.Value = false;
            if (_caughtBody != null)
            {
                _caughtBody.isKinematic = _caughtWasKinematic;
                _caughtBody.position = transform.position + forward.normalized * 0.8f + Vector3.up * 0.2f;
                foreach (var col in c.CarryTransform.GetComponentsInChildren<Collider>()) foreach (var mine in GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(col, mine, false);
            }
            c.OnReleased(_user != null ? _user.OwnerClientId : PlayerIds.None);
        }

        public override void OnNetworkDespawn() { if (IsServer) Release(transform.forward); }
    }
}
