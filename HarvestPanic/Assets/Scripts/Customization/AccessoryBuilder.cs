using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

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
        /// <summary>Skin tone.</summary>
        public Color Skin;
        /// <summary>Hair colour.</summary>
        public Color Hair;

        /// <summary>Colour for <paramref name="tint"/> (<paramref name="fixedColor"/> when Fixed).</summary>
        public Color Resolve(PartTint tint, Color fixedColor)
        {
            switch (tint)
            {
                case PartTint.Primary: return Primary;
                case PartTint.Secondary: return Secondary;
                case PartTint.Accent: return Accent;
                case PartTint.NameTag: return NameTag;
                case PartTint.Skin: return Skin;
                case PartTint.Hair: return Hair;
                case PartTint.SkinShade: return SkinShade(Skin);
                default: return fixedColor;
            }
        }

        /// <summary>Colour for <paramref name="part"/>.</summary>
        public Color Resolve(in CosmeticPart part) => Resolve(part.tint, part.color);

        /// <summary>Slightly darker, warmer skin (noses, ears in shadow).</summary>
        public static Color SkinShade(Color skin) => new Color(skin.r * 0.9f, skin.g * 0.78f, skin.b * 0.74f, 1f);
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
        static readonly Dictionary<(Material, Color, OutfitPattern), Material> Materials = new Dictionary<(Material, Color, OutfitPattern), Material>();
        static Material _fallbackBase;
        static MaterialPropertyBlock _mpb;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int PrimaryId = Shader.PropertyToID("_PrimaryColor");
        static readonly int SecondaryId = Shader.PropertyToID("_SecondaryColor");
        static readonly int AccentId = Shader.PropertyToID("_AccentColor");
        static readonly int SkinId = Shader.PropertyToID("_SkinColor");

        /// <summary>Creates the accessory under <paramref name="parent"/> (single attach point) or returns null for None.</summary>
        public static GameObject Build(CosmeticItem item, Transform parent, Material baseMaterial, in TintColors tints)
        {
            if (item == null || parent == null) return null;
            var roots = new List<GameObject>(1);
            var p = parent;
            Build(item, _ => p, AttachPoint.Head, baseMaterial, tints, false, roots);
            return roots.Count > 0 ? roots[0] : null;
        }

        /// <summary>
        /// Builds <paramref name="item"/> at its attach point(s) and adds one root per attach point used to <paramref name="roots"/>.
        /// <paramref name="attachFor"/> maps an attach point to the farmer transform (null = skip); <paramref name="hatWorn"/>
        /// drops hair parts flagged <see cref="CosmeticPart.hideUnderHat"/>.
        /// </summary>
        public static void Build(CosmeticItem item, Func<AttachPoint, Transform> attachFor, AttachPoint slotDefault, Material baseMaterial,
            in TintColors tints, bool hatWorn, List<GameObject> roots)
        {
            if (item == null || attachFor == null) return;
            AttachPoint main = item.attach == AttachPoint.Auto ? slotDefault : item.attach;
            if (item.prefab != null)
            {
                var parent = attachFor(main);
                if (parent == null) return;
                var root = Object.Instantiate(item.prefab, parent, false);
                Place(root.transform, item);
                ApplyPrefabTint(root, item, tints);
                Finish(root, item, parent, roots);
                return;
            }

            var byAttach = new Dictionary<AttachPoint, GameObject>();
            foreach (var part in item.parts)
            {
                if (hatWorn && part.hideUnderHat) continue;
                AttachPoint ap = part.attach == AttachPoint.Auto ? main : part.attach;
                if (!byAttach.TryGetValue(ap, out var root))
                {
                    var parent = attachFor(ap);
                    if (parent == null) continue;
                    root = new GameObject(item.key);
                    root.transform.SetParent(parent, false);
                    Place(root.transform, item);
                    byAttach[ap] = root;
                    Finish(root, item, parent, roots);
                }
                BuildPart(part, root.transform, baseMaterial, tints);
            }
        }

        static void Place(Transform t, CosmeticItem item)
        {
            t.localPosition = item.offset;
            t.localRotation = Quaternion.Euler(item.rotation);
            float s = item.scale > 0f ? item.scale : 1f;
            t.localScale = Vector3.one * s;
        }

        static void Finish(GameObject root, CosmeticItem item, Transform parent, List<GameObject> roots)
        {
            root.name = "Cosmetic_" + item.key;
            SetLayerRecursive(root, parent.gameObject.layer);
            foreach (var c in root.GetComponentsInChildren<Collider>(true)) { if (Application.isPlaying) Object.Destroy(c); else Object.DestroyImmediate(c); }
            roots.Add(root);
        }

        /// <summary>Colours an authored prefab instance with property blocks (materials stay shared).</summary>
        public static void ApplyPrefabTint(GameObject root, CosmeticItem item, in TintColors tints)
        {
            if (item.tintMode == TintMode.None) return;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int m = 0; m < mats.Length; m++)
                {
                    _mpb.Clear();
                    if (item.tintMode == TintMode.MaskTexture)
                    {
                        _mpb.SetColor(PrimaryId, tints.Resolve(item.maskRed == PartTint.Fixed ? PartTint.Primary : item.maskRed, tints.Primary));
                        _mpb.SetColor(SecondaryId, tints.Secondary);
                        _mpb.SetColor(AccentId, tints.Accent);
                        _mpb.SetColor(SkinId, tints.Skin);
                    }
                    else
                    {
                        var tint = m < item.materialTints.Count ? item.materialTints[m] : PartTint.Fixed;
                        if (tint == PartTint.Fixed) continue;
                        var c = tints.Resolve(tint, Color.white);
                        _mpb.SetColor(BaseColorId, c);
                        _mpb.SetColor(ColorId, c);
                    }
                    r.SetPropertyBlock(_mpb, m);
                }
            }
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
        public static Material MaterialFor(Material baseMaterial, Color color) => MaterialFor(baseMaterial, color, OutfitPattern.Plain);

        /// <summary>Shared lit material of <paramref name="color"/> with a procedural cloth <paramref name="pattern"/> (needs mesh UVs).</summary>
        public static Material MaterialFor(Material baseMaterial, Color color, OutfitPattern pattern)
        {
            if (baseMaterial == null)
            {
                if (_fallbackBase == null) _fallbackBase = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "HP_CosmeticFallback" };
                baseMaterial = _fallbackBase;
            }
            var key = (baseMaterial, color, pattern);
            if (Materials.TryGetValue(key, out var mat) && mat != null) return mat;
            mat = new Material(baseMaterial) { name = baseMaterial.name + "_" + ColorUtility.ToHtmlStringRGB(color) + (pattern == OutfitPattern.Plain ? "" : "_" + pattern) };
            SetColor(mat, color);
            if (pattern != OutfitPattern.Plain && mat.HasProperty("_BaseMap"))
            {
                mat.SetTexture("_BaseMap", OutfitPatterns.Texture(pattern));
                mat.SetTextureScale("_BaseMap", OutfitPatterns.Tiling(pattern));
            }
            Materials[key] = mat;
            return mat;
        }

        /// <summary>Sets the URP base colour (and legacy _Color) on <paramref name="mat"/>.</summary>
        public static void SetColor(Material mat, Color c)
        {
            if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, c);
            if (mat.HasProperty(ColorId)) mat.SetColor(ColorId, c);
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
