using System.Collections.Generic;
using HarvestPanic.Core;
using Unity.Netcode;
using UnityEngine;

namespace HarvestPanic.AI.Night
{
    /// <summary>
    /// Mr. Stitches (night-creatures §3.1/3.3). Server-authoritative: moves ONLY while no viewer sees ≥15% of him
    /// (frustum + line of sight, 40 m / 15 m fog). Stalks the nearest farmer outside safe zones (biased to the loudest
    /// cell), teleports between field posts when far, never enters barn/market/bridge/lantern light. A touch knocks the
    /// farmer out with a yank toward the field; then he drags loose goods back to the field (still only when unseen).
    /// Position replicates via NetworkTransform; state via a NetworkVariable.
    /// </summary>
    public sealed class MrStitches : NetworkBehaviour
    {
        public enum Mode : byte { Idle, Stalking, Watched, Dragging }

        readonly NetworkVariable<byte> _nvMode = new NetworkVariable<byte>();
        NightDirector _dir;
        NightTuning _t;
        Mode _mode;
        float _nextGrab, _unseenSince;
        Transform _dragged; Rigidbody _draggedRb; bool _draggedWasKinematic; ICarryable _draggedCarry;
        Vector3 _dragTo;
        readonly List<Vector3> _pts = new List<Vector3>();

        /// <summary>Test/dev: overrides the stalk target.</summary>
        public Vector3? DebugTarget;
        public Mode CurrentMode => IsSpawned && !IsServer ? (Mode)_nvMode.Value : _mode;
        public bool ObservedNow { get; private set; }
        /// <summary>Distance moved by the server since spawn (tests).</summary>
        public float DistanceMoved { get; private set; }

        void Awake() { if (transform.Find("StitchesVisual") == null) NightVisuals.BuildStitches(transform); }

        public void Init(NightDirector d) { _dir = d; _t = d.Tuning; }

        bool Authority => IsSpawned ? IsServer : !(NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening);

        public Bounds WorldBounds => new Bounds(transform.position + Vector3.up * 1.4f, new Vector3(1.2f, 2.6f, 1.0f));

        void SetMode(Mode m) { _mode = m; if (IsSpawned && IsServer) _nvMode.Value = (byte)m; }

        void Update()
        {
            if (!Authority) return;
            if (_dir == null) { _dir = NightDirector.Instance; if (_dir == null) return; _t = _dir.Tuning; }
            float fog = Services.TryGet(out IDayModifierService mods) ? mods.Resolve(DayParams.WeatherFogDistance, 0f) : 0f;
            NightMath.Samples(WorldBounds, _pts);
            ObservedNow = NightMath.IsObserved(NightObservers.Current(_t), _pts, NightMath.ViewDistance(fog, _t), _t.MinVisibleFraction, HasLos);
            if (ObservedNow) { _unseenSince = -1f; SetMode(Mode.Watched); HoldDragged(); return; }
            if (_unseenSince < 0f) _unseenSince = Time.time;

            if (_dragged != null) { TickDrag(); return; }
            Vector3? target = DebugTarget ?? PickTarget(out _);
            if (target == null) { SetMode(Mode.Idle); return; }
            SetMode(Mode.Stalking);
            Vector3 to = target.Value - transform.position;
            float dist = NightDirector.Flat(to);
            if (DebugTarget == null && dist > _t.TeleportDistance && Time.time - _unseenSince > 1f)
            {
                var post = _dir.NearestPost(target.Value);
                if (NightDirector.Flat(post - transform.position) > 2f && !PointSeen(post)) { Teleport(NightDirector.Ground(post)); return; }
            }
            MoveToward(target.Value, _t.StitchesSpeed);
            TryGrab();
        }

        bool HasLos(Vector3 eye, Vector3 p) => !Physics.Linecast(eye, p, NightDirector.LosMask, QueryTriggerInteraction.Ignore);

        bool PointSeen(Vector3 p)
        {
            var b = new Bounds(NightDirector.Ground(p) + Vector3.up * 1.4f, new Vector3(1.2f, 2.6f, 1f));
            var pts = new List<Vector3>(); NightMath.Samples(b, pts);
            return NightMath.IsObserved(NightObservers.Current(_t), pts, _t.ViewDistance, _t.MinVisibleFraction, HasLos);
        }

        void Teleport(Vector3 p)
        {
            DistanceMoved += (p - transform.position).magnitude;
            var nt = GetComponent<Unity.Netcode.Components.NetworkTransform>();
            if (IsSpawned && nt != null) nt.Teleport(p, transform.rotation, transform.localScale); else transform.position = p;
        }

        void MoveToward(Vector3 target, float speed)
        {
            Vector3 d = target - transform.position; d.y = 0f;
            if (d.sqrMagnitude < 0.01f) return;
            Vector3 next = transform.position + Vector3.ClampMagnitude(d, speed * Time.deltaTime);
            if (_dir.InSafeZone(next)) return; // stops at the edge of the light
            next = NightDirector.Ground(next);
            DistanceMoved += (next - transform.position).magnitude;
            transform.SetPositionAndRotation(next, Quaternion.LookRotation(d.normalized, Vector3.up)); // snaps to face: creepier
        }

        Vector3? PickTarget(out IKnockable victim)
        {
            victim = null;
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return null;
            Vector3 bias = _dir.HotCell ?? transform.position;
            float best = float.MaxValue; Vector3? pos = null;
            foreach (var c in nm.ConnectedClientsList)
            {
                var po = c.PlayerObject;
                if (po == null || !po.TryGetComponent(out IKnockable k) || k.IsDown) continue;
                Vector3 p = po.transform.position;
                if (_dir.InSafeZone(p)) continue;
                float score = NightDirector.Flat(p - transform.position) + 0.5f * NightDirector.Flat(p - bias);
                if (score < best) { best = score; pos = p; victim = k; }
            }
            return pos;
        }

        void TryGrab()
        {
            if (Time.time < _nextGrab) return;
            var hits = Physics.OverlapSphere(transform.position + Vector3.up, _t.GrabRange, ~0, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                if (!Knockables.TryGetFor(h.gameObject, out var k) || k.IsDown) continue;
                Vector3 toField = _dir.NearestPost(transform.position) - h.transform.position; toField.y = 0f;
                Vector3 yank = (toField.sqrMagnitude > 0.01f ? toField.normalized : -transform.forward) * _t.GrabImpulse + Vector3.up * _t.GrabImpulse * 0.3f;
                k.ApplyImpulse(yank, PlayerIds.Nature, BlameVerb.KnockedOut);
                if (k is IDraggable drag) drag.RequestDrag(_dir.NearestPost(transform.position), _t.StitchesFarmerDragSeconds); // polish-2: owner drags the KO'd body
                _nextGrab = Time.time + _t.GrabCooldown;
                Debug.Log("[Night] Mr. Stitches grabbed a farmer");
                break;
            }
            if (_dragged == null) TryPickGoods();
        }

        void TryPickGoods()
        {
            var hits = Physics.OverlapSphere(transform.position + Vector3.up * 0.5f, 3f, ~0, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                var goods = h.GetComponentInParent<IGoods>();
                if (goods == null || h.GetComponentInParent<IKnockable>() != null) continue;
                var rb = h.attachedRigidbody;
                if (rb != null && rb.linearVelocity.sqrMagnitude > 4f) continue; // probably held/thrown
                _dragged = goods.GoodsTransform; _draggedRb = rb;
                _draggedWasKinematic = rb != null && rb.isKinematic;
                _draggedCarry = h.GetComponentInParent<ICarryable>();
                _draggedCarry?.OnGrabbed(PlayerIds.Nature);
                if (rb != null) rb.isKinematic = true;
                _dragTo = _dir.NearestPost(transform.position);
                SetMode(Mode.Dragging);
                return;
            }
        }

        void TickDrag()
        {
            SetMode(Mode.Dragging);
            MoveToward(_dragTo, _t.StitchesDragSpeed);
            HoldDragged();
            if (NightDirector.Flat(_dragTo - transform.position) < 1.5f) DropDragged();
        }

        void HoldDragged()
        {
            if (_dragged == null) return;
            _dragged.position = transform.position + transform.right * 0.9f + Vector3.up * 0.4f;
        }

        public void DropDragged()
        {
            if (_dragged == null) return;
            if (_draggedRb != null) _draggedRb.isKinematic = _draggedWasKinematic;
            _draggedCarry?.OnReleased(PlayerIds.Nature);
            _dragged = null; _draggedRb = null; _draggedCarry = null;
        }

        public override void OnNetworkDespawn() { if (IsServer) DropDragged(); }
    }
}
