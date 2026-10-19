using System.Collections.Generic;
using UnityEngine;

namespace HarvestPanic.Customization
{
    /// <summary>Colours a recipe part can follow (resolved from the loadout at build time).</summary>
    public struct TintColors
    {
        /// <summary>Overalls colour.</summary>
        public Color Primary;
        /// <summary>Shirt colour.</summary>
        public Color Secondary;
        /// <summary>Boots/gloves colour.</summary>
        public Color Accent;
        /// <summary>Name-tag colour.</summary>
        public Color NameTag;

        /// <summary>Colour for <paramref name="part"/>.</summary>
        public Color Resolve(in CosmeticPart part)
        {
            switch (part.tint)
            {
                case PartTint.Primary: return Primary;
                case PartTint.Secondary: return Secondary;
                case PartTint.Accent: return Accent;
                case PartTint.NameTag: return NameTag;
                default: return part.color;
            }
        }
    }

    /// <summary>
    /// Builds accessory GameObjects from <see cref="CosmeticItem"/> primitive recipes (or instantiates the authored prefab).
    /// Meshes and per-colour materials are cached and shared by every farmer so 4 players cost a handful of materials.
    /// Accessories never get colliders (GDD: "the hat has the Cosmetic tag and is never sellable or grabbable as goods").
    /// </summary>
    public static class AccessoryBuilder
    {
        static readonly Dictionary<PartShape, Mesh> Builtins = new Dictionary<PartShape, Mesh>();
        static readonly Dictionary<int, Mesh> Cones = new Dictionary<int, Mesh>();
        static readonly Dictionary<(Material, Color), Material> Materials = new Dictionary<(Material, Color), Material>();
        static Material _fallbackBase;

        /// <summary>Creates the accessory under <paramref name="parent"/> (local identity) or returns null for None.</summary>
        public static GameObject Build(CosmeticItem item, Transform parent, Material baseMaterial, in TintColors tints)
        {
            if (item == null || parent == null) return null;
            GameObject root;
            if (item.prefab != null)
            {
                root = Object.Instantiate(item.prefab, parent, false);
            }
            else
            {
                root = new GameObject(item.key);
                root.transform.SetParent(parent, false);
                foreach (var part in item.parts) BuildPart(part, root.transform, baseMaterial, tints);
            }
            root.name = "Cosmetic_" + item.key;
            SetLayerRecursive(root, parent.gameObject.layer);
            return root;
        }

        /// <summary>Creates one primitive part (also used for wardrobe furniture and pings).</summary>
        public static GameObject BuildPart(in CosmeticPart part, Transform parent, Material baseMaterial, in TintColors tints)
        {
            var go = new GameObject(part.shape.ToString());
            var t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = part.position;
            t.localRotation = Quaternion.Euler(part.euler);
            t.localScale = ScaleFor(part);
            go.AddComponent<MeshFilter>().sharedMesh = MeshFor(part);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = MaterialFor(baseMaterial, tints.Resolve(part));
            return go;
        }

        /// <summary>Local scale that turns the unit primitive into <see cref="CosmeticPart.size"/>.</summary>
        public static Vector3 ScaleFor(in CosmeticPart p)
        {
            switch (p.shape)
            {
                case PartShape.Cylinder:
                case PartShape.Capsule: return new Vector3(p.size.x, p.size.y * 0.5f, p.size.z);
                default: return p.size;
            }
        }

        /// <summary>Shared mesh for a part (built-in primitive or cached procedural cone).</summary>
        public static Mesh MeshFor(in CosmeticPart p)
        {
            if (p.shape == PartShape.Cone) return Cone(p.taper);
            if (Builtins.TryGetValue(p.shape, out var m) && m != null) return m;
            PrimitiveType type = p.shape == PartShape.Cube ? PrimitiveType.Cube : p.shape == PartShape.Sphere ? PrimitiveType.Sphere
                : p.shape == PartShape.Capsule ? PrimitiveType.Capsule : PrimitiveType.Cylinder;
            var tmp = GameObject.CreatePrimitive(type);
            m = tmp.GetComponent<MeshFilter>().sharedMesh;
            if (Application.isPlaying) Object.Destroy(tmp); else Object.DestroyImmediate(tmp);
            Builtins[p.shape] = m;
            return m;
        }

        /// <summary>Shared lit material of <paramref name="color"/> cloned from <paramref name="baseMaterial"/>.</summary>
        public static Material MaterialFor(Material baseMaterial, Color color)
        {
            if (baseMaterial == null)
            {
                if (_fallbackBase == null) _fallbackBase = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "HP_CosmeticFallback" };
                baseMaterial = _fallbackBase;
            }
            if (Materials.TryGetValue((baseMaterial, color), out var mat) && mat != null) return mat;
            mat = new Material(baseMaterial) { name = baseMaterial.name + "_" + ColorUtility.ToHtmlStringRGB(color) };
            SetColor(mat, color);
            Materials[(baseMaterial, color)] = mat;
            return mat;
        }

        /// <summary>Sets the URP base colour (and legacy _Color) on <paramref name="mat"/>.</summary>
        public static void SetColor(Material mat, Color c)
        {
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
        }

        /// <summary>Unit cone: base radius 0.5 at y=0, top radius 0.5*taper at y=1, capped. Cached by taper.</summary>
        public static Mesh Cone(float taper)
        {
            int key = Mathf.RoundToInt(taper * 1000f);
            if (Cones.TryGetValue(key, out var cached) && cached != null) return cached;
            const int seg = 24;
            float rb = 0.5f, rt = 0.5f * Mathf.Max(0f, taper);
            var v = new List<Vector3>(); var n = new List<Vector3>(); var tri = new List<int>();
            float slope = (rb - rt); // over height 1
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                var normal = new Vector3(dir.x, slope, dir.z).normalized;
                v.Add(dir * rb); n.Add(normal);
                v.Add(dir * rt + Vector3.up); n.Add(normal);
            }
            for (int i = 0; i < seg; i++)
            {
                int b = i * 2;
                tri.Add(b); tri.Add(b + 2); tri.Add(b + 1);
                tri.Add(b + 1); tri.Add(b + 2); tri.Add(b + 3);
            }
            AddCap(v, n, tri, 0f, rb, Vector3.down, seg);
            if (rt > 0.0001f) AddCap(v, n, tri, 1f, rt, Vector3.up, seg);
            var mesh = new Mesh { name = "HP_Cone_" + key };
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetTriangles(tri, 0);
            mesh.RecalculateBounds();
            Cones[key] = mesh;
            return mesh;
        }

        static void AddCap(List<Vector3> v, List<Vector3> n, List<int> tri, float y, float r, Vector3 normal, int seg)
        {
            int center = v.Count;
            v.Add(new Vector3(0f, y, 0f)); n.Add(normal);
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                v.Add(new Vector3(Mathf.Sin(a) * r, y, Mathf.Cos(a) * r)); n.Add(normal);
            }
            for (int i = 0; i < seg; i++)
            {
                int a = center + 1 + i, b = center + 2 + i;
                if (normal.y < 0f) { tri.Add(center); tri.Add(b); tri.Add(a); }
                else { tri.Add(center); tri.Add(a); tri.Add(b); }
            }
        }

        /// <summary>Sets <paramref name="layer"/> on <paramref name="go"/> and all children.</summary>
        public static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform c in go.transform) SetLayerRecursive(c.gameObject, layer);
        }
    }
}
