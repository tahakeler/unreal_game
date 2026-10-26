using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace HarvestPanic.Gameplay
{
    /// <summary>A place a lasso rope can be tied (physics-carry rule 7: "a rope can be tied to a wagon, post or another player").</summary>
    public struct TiePointInfo
    {
        /// <summary>World position of the knot.</summary>
        public Vector3 Point;
        /// <summary>Moving anchor body (the wagon), or null for a fixed post.</summary>
        public Rigidbody Body;
        /// <summary>NetworkObject that carries the point (wagon, hitching post), or null for scene geometry.</summary>
        public NetworkObject Net;
        /// <summary>Prompt noun: "post", "wagon".</summary>
        public string Label;
    }

    /// <summary>
    /// Marks a lasso tie point (hitching post, wagon hitch). <see cref="TryFindNearest"/> also accepts static scene
    /// colliders whose name contains "Post" (bridge rail posts, fence posts) so the World scene works unchanged.
    /// </summary>
    public sealed class LassoTiePoint : MonoBehaviour
    {
        static readonly List<LassoTiePoint> Registered = new List<LassoTiePoint>();
        static readonly Collider[] Overlaps = new Collider[48];

        [SerializeField, Tooltip("Prompt noun, e.g. post or wagon")] string label = "post";

        /// <summary>Registered tie points (tests).</summary>
        public static IReadOnlyList<LassoTiePoint> All => Registered;
        /// <summary>Prompt noun.</summary>
        public string Label { get => label; set => label = value; }

        void OnEnable() => Registered.Add(this);
        void OnDisable() => Registered.Remove(this);

        /// <summary>Describes this tie point.</summary>
        public TiePointInfo Info
        {
            get
            {
                var rb = GetComponentInParent<Rigidbody>();
                return new TiePointInfo
                {
                    Point = transform.position,
                    Body = rb != null && !rb.isKinematic ? rb : null,
                    Net = GetComponentInParent<NetworkObject>(),
                    Label = label,
                };
            }
        }

        /// <summary>
        /// Nearest tie point within <paramref name="radius"/> of <paramref name="from"/> (horizontal): registered points
        /// first, then static colliders named "*Post*" (no rigidbody).
        /// </summary>
        public static bool TryFindNearest(Vector3 from, float radius, out TiePointInfo info)
        {
            info = default;
            float best = radius * radius;
            bool found = false;
            foreach (var t in Registered)
            {
                if (t == null || !t.isActiveAndEnabled) continue;
                float d = Flat(t.transform.position - from);
                if (d > best) continue;
                best = d; info = t.Info; found = true;
            }
            if (found) return true;
            int n = Physics.OverlapSphereNonAlloc(from, radius, Overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = Overlaps[i];
                if (c.attachedRigidbody != null || c.name.IndexOf("Post", StringComparison.OrdinalIgnoreCase) < 0) continue;
                Vector3 b = c.bounds.center + Vector3.up * c.bounds.extents.y * 0.55f;
                float d = Flat(b - from);
                if (d > best) continue;
                best = d;
                info = new TiePointInfo { Point = b, Body = null, Net = null, Label = "post" };
                found = true;
            }
            return found;
        }

        static float Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude; }
    }
}
