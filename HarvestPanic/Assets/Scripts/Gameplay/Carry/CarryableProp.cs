using HarvestPanic.Core;
using Unity.Netcode;
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

        /// <summary>Cached rigidbody.</summary>
        protected Rigidbody Body { get; private set; }
        /// <summary>Client id of the first current holder, or <see cref="PlayerIds.None"/> (server).</summary>
        public ulong Holder { get; private set; } = PlayerIds.None;

        protected virtual void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Body.mass = massKg;
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
