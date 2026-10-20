using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HarvestPanic.Camcorder
{
    /// <summary>
    /// Visual-only recipe of a recorded actor (meshes + materials + local matrices), captured once when the actor is first
    /// tracked so a ghost can be rebuilt even after the original was sold, destroyed or despawned. Ghosts have no colliders,
    /// rigidbodies, scripts or NetworkObjects. Skinned meshes are drawn in bind pose (beta).
    /// </summary>
    public sealed class GhostVisual
    {
        struct Part { public Mesh Mesh; public Material[] Materials; public Vector3 Pos; public Quaternion Rot; public Vector3 Scale; }
        readonly List<Part> _parts = new List<Part>();
        public TrackKind Kind { get; private set; }
        public int PartCount => _parts.Count;

        static readonly List<Renderer> Scratch = new List<Renderer>(32);

        public static GhostVisual Capture(Transform root, TrackKind kind)
        {
            var g = new GhostVisual { Kind = kind };
            if (root == null) return g;
            Scratch.Clear();
            root.GetComponentsInChildren(true, Scratch);
            var inv = root.worldToLocalMatrix;
            foreach (var r in Scratch)
            {
                Mesh mesh = null;
                if (r is MeshRenderer mr) { var mf = mr.GetComponent<MeshFilter>(); mesh = mf != null ? mf.sharedMesh : null; }
                else if (r is SkinnedMeshRenderer smr) mesh = smr.sharedMesh;
                if (mesh == null) continue;
                // First-person arms/props live under the view camera; third-person body is what replays should show.
                if (r.GetComponentInParent<Camera>(true) != null) continue;
                var m = inv * r.transform.localToWorldMatrix;
                g._parts.Add(new Part
                {
                    Mesh = mesh, Materials = r.sharedMaterials,
                    Pos = m.GetColumn(3), Rot = m.rotation, Scale = m.lossyScale
                });
            }
            Scratch.Clear();
            return g;
        }

        /// <summary>Builds a ghost under <paramref name="parent"/>; falls back to a coloured primitive when no meshes were captured.</summary>
        public GameObject Build(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            if (_parts.Count == 0)
            {
                var prim = GameObject.CreatePrimitive(Kind == TrackKind.Player ? PrimitiveType.Capsule : PrimitiveType.Cube);
                Object.Destroy(prim.GetComponent<Collider>());
                prim.transform.SetParent(go.transform, false);
                if (Kind == TrackKind.Player) prim.transform.localPosition = Vector3.up;
                else prim.transform.localScale = Kind == TrackKind.Plank ? new Vector3(2f, 0.1f, 0.4f) : Vector3.one * 0.6f;
                var r = prim.GetComponent<Renderer>();
                var block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", FallbackColor(Kind));
                block.SetColor("_Color", FallbackColor(Kind));
                r.SetPropertyBlock(block);
                return go;
            }
            for (int i = 0; i < _parts.Count; i++)
            {
                var p = _parts[i];
                var child = new GameObject("part" + i);
                child.transform.SetParent(go.transform, false);
                child.transform.localPosition = p.Pos;
                child.transform.localRotation = p.Rot;
                child.transform.localScale = p.Scale;
                child.AddComponent<MeshFilter>().sharedMesh = p.Mesh;
                var mr = child.AddComponent<MeshRenderer>();
                mr.sharedMaterials = p.Materials;
                mr.shadowCastingMode = ShadowCastingMode.On;
            }
            return go;
        }

        public static Color FallbackColor(TrackKind k)
        {
            switch (k)
            {
                case TrackKind.Player: return new Color(0.95f, 0.55f, 0.2f);
                case TrackKind.Critter: return new Color(0.95f, 0.7f, 0.75f);
                case TrackKind.Carryable: return new Color(0.9f, 0.85f, 0.3f);
                case TrackKind.Plank: return new Color(0.55f, 0.38f, 0.22f);
                default: return Color.grey;
            }
        }
    }
}
