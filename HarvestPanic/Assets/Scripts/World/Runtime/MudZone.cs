using System.Collections.Generic;
using HarvestPanic.Core;
using UnityEngine;

namespace HarvestPanic.World
{
    /// <summary>
    /// Mud volume (bridge-hazards section 3.8). Rigidbodies inside (critters, goods, rigidbody players) are slowed to
    /// <see cref="SpeedMultiplier"/> of their horizontal speed; CharacterController players can query
    /// <see cref="SpeedMultiplierAt"/> once a Core movement-modifier contract exists (requested from the lead).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class MudZone : MonoBehaviour
    {
        static readonly List<MudZone> Active = new List<MudZone>();

        [SerializeField, Range(0.1f, 1f)] float speedMultiplier = 0.6f;
        readonly HashSet<Rigidbody> _inside = new HashSet<Rigidbody>();
        Collider _col;

        /// <summary>Player speed multiplier inside the mud (0.6 by default, day parameter Mud.SpeedMult may override).</summary>
        public float SpeedMultiplier => Services.TryGet(out IDayModifierService d) ? d.Resolve(WorldDayParams.MudSpeedMult, speedMultiplier) : speedMultiplier;

        /// <summary>Smallest multiplier of any mud zone containing <paramref name="p"/> (1 outside mud).</summary>
        public static float SpeedMultiplierAt(Vector3 p)
        {
            float m = 1f;
            foreach (var z in Active) if (z._col != null && z._col.bounds.Contains(p)) m = Mathf.Min(m, z.SpeedMultiplier);
            return m;
        }

        void Awake() { _col = GetComponent<Collider>(); _col.isTrigger = true; }
        void OnEnable() => Active.Add(this);
        void OnDisable() { Active.Remove(this); _inside.Clear(); }

        void OnTriggerEnter(Collider o) { if (o.attachedRigidbody != null) _inside.Add(o.attachedRigidbody); }
        void OnTriggerExit(Collider o) { if (o.attachedRigidbody != null) _inside.Remove(o.attachedRigidbody); }

        void FixedUpdate()
        {
            if (!WorldAuthority.IsServerOrOffline || _inside.Count == 0) return;
            // Damping that converges to speedMultiplier of the driven speed: remove (1-m) of the horizontal velocity gained each step.
            float keep = Mathf.Lerp(1f, SpeedMultiplier, 0.25f);
            _inside.RemoveWhere(rb => rb == null);
            foreach (var rb in _inside)
            {
                if (rb.isKinematic) continue;
                var v = rb.linearVelocity;
                rb.linearVelocity = new Vector3(v.x * keep, v.y, v.z * keep);
            }
        }
    }
}
