using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HarvestPanic.World.Editor
{
    /// <summary>
    /// Flat-shaded mesh combiner for the Muddlebury architecture: boxes, convex prisms and cylinders with metre-based UVs,
    /// one submesh per material. A whole building bakes to ONE renderer (one draw per material), which keeps the town
    /// inside the 1,500 draw-call budget even though every unique box size would otherwise be its own mesh.
    /// Positions are in the building's local frame; <see cref="Push"/>/<see cref="Pop"/> nest local transforms.
    /// </summary>
    public sealed class WorldMeshBuilder
    {
        readonly List<Vector3> _v = new List<Vector3>();
        readonly List<Vector3> _n = new List<Vector3>();
        readonly List<Vector2> _uv = new List<Vector2>();
        readonly List<Material> _mats = new List<Material>();
        readonly List<List<int>> _subs = new List<List<int>>();
        readonly Stack<Matrix4x4> _stack = new Stack<Matrix4x4>();
        Matrix4x4 _m = Matrix4x4.identity;

        /// <summary>Number of vertices so far.</summary>
        public int VertexCount => _v.Count;

        /// <summary>Pushes a local transform (position, yaw/pitch/roll in degrees).</summary>
        public void Push(Vector3 pos, Quaternion rot) { _stack.Push(_m); _m = _m * Matrix4x4.TRS(pos, rot, Vector3.one); }
        /// <summary>Pops the last pushed transform.</summary>
        public void Pop() { _m = _stack.Pop(); }

        int Sub(Material m)
        {
            int i = _mats.IndexOf(m);
            if (i >= 0) return i;
            _mats.Add(m); _subs.Add(new List<int>());
            return _mats.Count - 1;
        }

        /// <summary>One flat quad (local points), wound so its front faces <paramref name="outward"/>.</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, Material m, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            Vector3 wa = _m.MultiplyPoint3x4(a), wb = _m.MultiplyPoint3x4(b), wc = _m.MultiplyPoint3x4(c), wd = _m.MultiplyPoint3x4(d);
            Vector3 n = _m.MultiplyVector(outward).normalized;
            int s = _v.Count;
            _v.Add(wa); _v.Add(wb); _v.Add(wc); _v.Add(wd);
            _n.Add(n); _n.Add(n); _n.Add(n); _n.Add(n);
            _uv.Add(ua); _uv.Add(ub); _uv.Add(uc); _uv.Add(ud);
            var t = _subs[Sub(m)];
            bool front = Vector3.Dot(Vector3.Cross(wb - wa, wc - wa), n) >= 0f;
            if (front) { t.Add(s); t.Add(s + 1); t.Add(s + 2); t.Add(s); t.Add(s + 2); t.Add(s + 3); }
            else { t.Add(s); t.Add(s + 2); t.Add(s + 1); t.Add(s); t.Add(s + 3); t.Add(s + 2); }
        }

        /// <summary>One flat triangle facing <paramref name="outward"/>.</summary>
        public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 outward, Material m, Vector2 ua, Vector2 ub, Vector2 uc)
        {
            Vector3 wa = _m.MultiplyPoint3x4(a), wb = _m.MultiplyPoint3x4(b), wc = _m.MultiplyPoint3x4(c);
            Vector3 n = _m.MultiplyVector(outward).normalized;
            int s = _v.Count;
            _v.Add(wa); _v.Add(wb); _v.Add(wc);
            _n.Add(n); _n.Add(n); _n.Add(n);
            _uv.Add(ua); _uv.Add(ub); _uv.Add(uc);
            var t = _subs[Sub(m)];
            if (Vector3.Dot(Vector3.Cross(wb - wa, wc - wa), n) >= 0f) { t.Add(s); t.Add(s + 1); t.Add(s + 2); }
            else { t.Add(s); t.Add(s + 2); t.Add(s + 1); }
        }

        /// <summary>
        /// Axis-aligned (in the current frame, optionally yawed) box. UVs are metres / <paramref name="tile"/>, with V up on the
        /// sides, so wall textures line up with the box no matter its size. <paramref name="skip"/>: faces to omit
        /// (bit 0 -X, 1 +X, 2 -Y, 3 +Y, 4 -Z, 5 +Z).
        /// </summary>
        public void Box(Vector3 centre, Vector3 size, Material m, float tile = 1f, float yaw = 0f, int skip = 0, float pitch = 0f, float roll = 0f)
        {
            Push(centre, Quaternion.Euler(pitch, yaw, roll));
            Vector3 h = size * 0.5f;
            float it = 1f / Mathf.Max(0.01f, tile);
            // -X / +X (u along z, v along y)
            if ((skip & 1) == 0) Face(new Vector3(-h.x, -h.y, h.z), new Vector3(-h.x, -h.y, -h.z), new Vector3(-h.x, h.y, -h.z), new Vector3(-h.x, h.y, h.z), Vector3.left, m, size.z * it, size.y * it);
            if ((skip & 2) == 0) Face(new Vector3(h.x, -h.y, -h.z), new Vector3(h.x, -h.y, h.z), new Vector3(h.x, h.y, h.z), new Vector3(h.x, h.y, -h.z), Vector3.right, m, size.z * it, size.y * it);
            // -Y / +Y (u along x, v along z)
            if ((skip & 4) == 0) Face(new Vector3(-h.x, -h.y, -h.z), new Vector3(h.x, -h.y, -h.z), new Vector3(h.x, -h.y, h.z), new Vector3(-h.x, -h.y, h.z), Vector3.down, m, size.x * it, size.z * it);
            if ((skip & 8) == 0) Face(new Vector3(-h.x, h.y, -h.z), new Vector3(h.x, h.y, -h.z), new Vector3(h.x, h.y, h.z), new Vector3(-h.x, h.y, h.z), Vector3.up, m, size.x * it, size.z * it);
            // -Z / +Z (u along x, v along y)
            if ((skip & 16) == 0) Face(new Vector3(-h.x, -h.y, -h.z), new Vector3(h.x, -h.y, -h.z), new Vector3(h.x, h.y, -h.z), new Vector3(-h.x, h.y, -h.z), Vector3.back, m, size.x * it, size.y * it);
            if ((skip & 32) == 0) Face(new Vector3(h.x, -h.y, h.z), new Vector3(-h.x, -h.y, h.z), new Vector3(-h.x, h.y, h.z), new Vector3(h.x, h.y, h.z), Vector3.forward, m, size.x * it, size.y * it);
            Pop();
        }

        void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n, Material m, float u, float v) =>
            Quad(a, b, c, d, n, m, new Vector2(0, 0), new Vector2(u, 0), new Vector2(u, v), new Vector2(0, v));

        /// <summary>
        /// Convex 2D profile (x across, y up) extruded along local Z by <paramref name="length"/> (centred).
        /// Caps optional. Side UVs: u = edge length, v = depth (metres / tile).
        /// </summary>
        public void Prism(Vector2[] profile, float length, Material sides, Material caps, float tile = 1f, bool capStart = true, bool capEnd = true)
        {
            float it = 1f / Mathf.Max(0.01f, tile), hz = length * 0.5f;
            Vector2 c = Vector2.zero; foreach (var p in profile) c += p; c /= profile.Length;
            float run = 0f;
            for (int i = 0; i < profile.Length; i++)
            {
                Vector2 a = profile[i], b = profile[(i + 1) % profile.Length];
                Vector2 mid = (a + b) * 0.5f, e = b - a;
                var nrm = new Vector3(e.y, -e.x, 0f).normalized;
                if (Vector2.Dot(new Vector2(nrm.x, nrm.y), mid - c) < 0f) nrm = -nrm;
                float len = e.magnitude;
                Quad(new Vector3(a.x, a.y, -hz), new Vector3(b.x, b.y, -hz), new Vector3(b.x, b.y, hz), new Vector3(a.x, a.y, hz), nrm, sides,
                    new Vector2(run * it, 0), new Vector2((run + len) * it, 0), new Vector2((run + len) * it, length * it), new Vector2(run * it, length * it));
                run += len;
            }
            if (caps == null) return;
            for (int cap = 0; cap < 2; cap++)
            {
                if (cap == 0 && !capStart || cap == 1 && !capEnd) continue;
                float z = cap == 0 ? -hz : hz;
                var nrm = cap == 0 ? Vector3.back : Vector3.forward;
                for (int i = 1; i < profile.Length - 1; i++)
                {
                    Vector2 a = profile[0], b = profile[i], d = profile[i + 1];
                    Tri(new Vector3(a.x, a.y, z), new Vector3(b.x, b.y, z), new Vector3(d.x, d.y, z), nrm, caps, a * it, b * it, d * it);
                }
            }
        }

        /// <summary>Upright n-sided cylinder / frustum (r0 at y0, r1 at y1), optional caps. UV u = around (m), v = up (m).</summary>
        public void Cylinder(Vector3 baseCentre, float r0, float r1, float height, int sides, Material side, Material cap, float tile = 1f, bool capTop = true, bool capBottom = false, float phase = 0f)
        {
            float it = 1f / Mathf.Max(0.01f, tile);
            float circ = 2f * Mathf.PI * Mathf.Max(r0, r1);
            for (int i = 0; i < sides; i++)
            {
                float a0 = phase + i * Mathf.PI * 2f / sides, a1 = phase + (i + 1) * Mathf.PI * 2f / sides, am = (a0 + a1) * 0.5f;
                var d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)); var d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                Vector3 b0 = baseCentre + d0 * r0, b1 = baseCentre + d1 * r0, t0 = baseCentre + d0 * r1 + Vector3.up * height, t1 = baseCentre + d1 * r1 + Vector3.up * height;
                var outward = new Vector3(Mathf.Cos(am), (r0 - r1) / Mathf.Max(0.01f, height), Mathf.Sin(am)).normalized;
                float u0 = circ * i / sides * it, u1 = circ * (i + 1) / sides * it;
                if (r1 > 1e-4f) Quad(b0, b1, t1, t0, outward, side, new Vector2(u0, 0), new Vector2(u1, 0), new Vector2(u1, height * it), new Vector2(u0, height * it));
                else Tri(b0, b1, t0, outward, side, new Vector2(u0, 0), new Vector2(u1, 0), new Vector2((u0 + u1) * 0.5f, height * it));
                if (cap != null && capTop && r1 > 1e-4f)
                {
                    var c = baseCentre + Vector3.up * height;
                    Tri(c, t0, t1, Vector3.up, cap, new Vector2(0.5f, 0.5f), new Vector2(0.5f + d0.x * 0.5f, 0.5f + d0.z * 0.5f), new Vector2(0.5f + d1.x * 0.5f, 0.5f + d1.z * 0.5f));
                }
                if (cap != null && capBottom)
                    Tri(baseCentre, b1, b0, Vector3.down, cap, new Vector2(0.5f, 0.5f), new Vector2(0.5f + d1.x * 0.5f, 0.5f + d1.z * 0.5f), new Vector2(0.5f + d0.x * 0.5f, 0.5f + d0.z * 0.5f));
            }
        }

        /// <summary>Thin double-sided panel (two quads back to back) in the local XY plane centred on <paramref name="centre"/>.</summary>
        public void Panel(Vector3 centre, float width, float height, Material m, float yaw = 0f, float uvW = 1f, float uvH = 1f)
        {
            Push(centre, Quaternion.Euler(0f, yaw, 0f));
            float w = width * 0.5f, h = height * 0.5f;
            Quad(new Vector3(-w, -h, 0), new Vector3(w, -h, 0), new Vector3(w, h, 0), new Vector3(-w, h, 0), Vector3.back, m, new Vector2(0, 0), new Vector2(uvW, 0), new Vector2(uvW, uvH), new Vector2(0, uvH));
            Quad(new Vector3(w, -h, 0), new Vector3(-w, -h, 0), new Vector3(-w, h, 0), new Vector3(w, h, 0), Vector3.forward, m, new Vector2(0, 0), new Vector2(uvW, 0), new Vector2(uvW, uvH), new Vector2(0, uvH));
            Pop();
        }

        /// <summary>Builds (and saves as an asset) the combined mesh; returns the mesh and its materials in submesh order.</summary>
        public Mesh Build(string assetName, out Material[] materials)
        {
            var mesh = new Mesh { name = assetName };
            if (_v.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(_v); mesh.SetNormals(_n); mesh.SetUVs(0, _uv);
            mesh.subMeshCount = _subs.Count;
            for (int i = 0; i < _subs.Count; i++) mesh.SetTriangles(_subs[i], i);
            mesh.RecalculateBounds();
            materials = _mats.ToArray();
            return WorldAssets.SaveMesh(mesh, assetName);
        }

        /// <summary>Bakes into a static GameObject (one MeshRenderer) under <paramref name="parent"/> at a pose.</summary>
        public GameObject Bake(string assetName, string objectName, Transform parent, Vector3 pos, Quaternion rot, ShadowCastingMode shadows = ShadowCastingMode.On)
        {
            var mesh = Build(assetName, out var mats);
            var go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats;
            r.shadowCastingMode = shadows;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
            return go;
        }
    }
}
