using HarvestPanic.Core;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace HarvestPanic.Gameplay
{
    /// <summary>
    /// Generic networked carryable for props and tools owned by the Player team (crates, buckets, nets).
    /// Goods and critters provide their own <see cref="ICarryable"/>. Needs a Rigidbody, a NetworkTransform
    /// (server authority) and NetworkRigidbody on the same object.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CarryableProp : NetworkBehaviour, ICarryable
    {
        [SerializeField, Range(1, 8)] int handsRequired = 1;
        [SerializeField, Min(0.1f)] float massKg = 5f;
        [SerializeField, Tooltip("Rigid hold (tools): no wobble, no bruising")] bool kinematicCarry;

        readonly NetworkVariable<ulong> _holsteredBy = new NetworkVariable<ulong>(PlayerIds.None);
        bool _holsterApplied;

        /// <summary>Cached rigidbody.</summary>
        protected Rigidbody Body { get; private set; }
        /// <summary>Client id of the first current holder, or <see cref="PlayerIds.None"/> (server).</summary>
        public ulong Holder { get; private set; } = PlayerIds.None;

        protected virtual void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Body.mass = massKg;
        }

        /// <summary>Client id whose toolbelt holds this put away (<see cref="PlayerIds.None"/> when out in the world/hand).</summary>
        public ulong HolsteredBy => IsSpawned ? _holsteredBy.Value : _offlineHolster;
        ulong _offlineHolster = PlayerIds.None;
        /// <summary>True while put away in a toolbelt (hidden, no collision).</summary>
        public bool IsHolstered => HolsteredBy != PlayerIds.None;

        public override void OnNetworkSpawn()
        {
            _holsteredBy.OnValueChanged += OnHolsterChanged;
            ApplyHolsterVisual(IsHolstered);
        }

        public override void OnNetworkDespawn() => _holsteredBy.OnValueChanged -= OnHolsterChanged;

        void OnHolsterChanged(ulong prev, ulong next) => ApplyHolsterVisual(next != PlayerIds.None);

        /// <summary>
        /// Server: puts the tool away into <paramref name="clientId"/>'s toolbelt (hidden, kinematic, parked at
        /// <paramref name="parkAt"/>) or takes it out again with <see cref="PlayerIds.None"/>. Parked tools do not follow
        /// their owner (no per-frame sync); taking one out teleports it to the hand (<see cref="ServerTeleport"/>).
        /// </summary>
        public void ServerSetHolstered(ulong clientId, Transform parkAt)
        {
            if (IsSpawned) { if (IsServer) _holsteredBy.Value = clientId; }
            else _offlineHolster = clientId;
            if (Body != null)
            {
                if (clientId != PlayerIds.None) { Body.linearVelocity = Vector3.zero; Body.angularVelocity = Vector3.zero; }
                Body.isKinematic = clientId != PlayerIds.None;
            }
            if (clientId != PlayerIds.None && parkAt != null) ServerTeleport(parkAt.position + Vector3.up * 0.5f, parkAt.rotation);
            ApplyHolsterVisual(clientId != PlayerIds.None);
        }

        /// <summary>Server: moves the prop without client interpolation (NetworkTransform teleport when spawned).</summary>
        public void ServerTeleport(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            if (Body != null) { Body.position = position; Body.rotation = rotation; }
            if (IsSpawned && IsServer && TryGetComponent<NetworkTransform>(out var nt) && nt.CanCommitToTransform) nt.Teleport(position, rotation, transform.localScale);
        }

        /// <summary>Hides renderers and colliders while holstered (every peer).</summary>
        void ApplyHolsterVisual(bool holstered)
        {
            if (!holstered && !_holsterApplied) return; // never re-enable renderers we did not hide
            _holsterApplied = holstered;
            foreach (var r in GetComponentsInChildren<Renderer>(true)) if (!(r is LineRenderer)) r.enabled = !holstered;
            foreach (var c in GetComponentsInChildren<Collider>(true)) c.enabled = !holstered;
        }

        /// <summary>Configures from code (editor builders, tests).</summary>
        public void Configure(int hands, float mass, bool kinematic)
        {
            handsRequired = hands; massKg = mass; kinematicCarry = kinematic;
            if (Body != null) Body.mass = mass; else GetComponent<Rigidbody>().mass = mass;
        }

        /// <inheritdoc />
        public Transform CarryTransform => transform;
        /// <inheritdoc />
        public int HandsRequired => handsRequired;
        /// <inheritdoc />
        public float MassKg => massKg;
        /// <inheritdoc />
        public bool IsKinematicCarry => kinematicCarry;

        /// <inheritdoc />
        public virtual void OnGrabbed(ulong clientId) { if (Holder == PlayerIds.None) Holder = clientId; }

        /// <inheritdoc />
        public virtual void OnReleased(ulong clientId) { if (Holder == clientId) Holder = PlayerIds.None; }

        /// <inheritdoc />
        public virtual void ApplyThrow(Vector3 velocity, ulong thrower)
        {
            if (!IsServer) return;
            Body.isKinematic = false;
            Body.linearVelocity = velocity;
            Body.angularVelocity = Random.insideUnitSphere * 2f;
        }
    }
}
