using System.Collections.Generic;
using UnityEngine;

namespace HarvestPanic.Customization
{
    /// <summary>
    /// Overhead name tag: player name in the name-tag colour plus a redundant shape icon (GDD Accessibility:
    /// "Name tag colours have redundant shape icons"). Billboards to the main camera; hidden for the local player.
    /// </summary>
    public sealed class NameTag : MonoBehaviour
    {
        [SerializeField] TextMesh text;
        [SerializeField] TextMesh outline;
        [SerializeField] MeshFilter icon;
        [SerializeField] MeshRenderer iconRenderer;
        [SerializeField, Tooltip("Hide beyond this distance (m).")] float maxDistance = 45f;

        static readonly Dictionary<TagShape, Mesh> ShapeMeshes = new Dictionary<TagShape, Mesh>();

        /// <summary>Displayed name.</summary>
        public string DisplayName => text != null ? text.text : "";

        /// <summary>Current shape.</summary>
        public TagShape Shape { get; private set; }

        /// <summary>Sets the displayed name.</summary>
        public void SetName(string displayName)
        {
            if (text != null) text.text = displayName;
            if (outline != null) outline.text = displayName;
        }

        /// <summary>Applies colour and shape.</summary>
        public void SetStyle(NameTagStyle style, Material unlit)
        {
            Shape = style.shape;
            if (text != null) text.color = style.color;
            if (icon != null) icon.sharedMesh = ShapeMesh(style.shape);
            if (iconRenderer != null) iconRenderer.sharedMaterial = AccessoryBuilder.MaterialFor(unlit, style.color);
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 toCam = transform.position - cam.transform.position;
            bool visible = toCam.sqrMagnitude < maxDistance * maxDistance;
            if (text != null && text.gameObject.activeSelf != visible)
            {
                text.gameObject.SetActive(visible);
                if (outline != null) outline.gameObject.SetActive(visible);
                if (icon != null) icon.gameObject.SetActive(visible);
            }
            if (visible) transform.rotation = Quaternion.LookRotation(toCam, cam.transform.up);
        }

        /// <summary>Flat icon mesh (XY plane, double-sided, ~1 unit wide) for <paramref name="shape"/>.</summary>
        public static Mesh ShapeMesh(TagShape shape)
        {
            if (ShapeMeshes.TryGetValue(shape, out var m) && m != null) return m;
            var outlinePts = ShapeOutline(shape);
            var v = new List<Vector3> { Vector3.zero };
            foreach (var p in outlinePts) v.Add(p);
            var tri = new List<int>();
            int n = outlinePts.Count;
            for (int i = 0; i < n; i++)
            {
                int a = 1 + i, b = 1 + (i + 1) % n;
                tri.Add(0); tri.Add(a); tri.Add(b);
                tri.Add(0); tri.Add(b); tri.Add(a);
            }
            m = new Mesh { name = "HP_TagShape_" + shape };
            m.SetVertices(v); m.SetTriangles(tri, 0); m.RecalculateNormals(); m.RecalculateBounds();
            ShapeMeshes[shape] = m;
            return m;
        }

        /// <summary>Outline of <paramref name="shape"/> (XY, centred, about 1 unit wide, +Y up); also drawn by the creator UI.</summary>
        public static List<Vector2> ShapeOutline(TagShape shape)
        {
            var outlinePts = new List<Vector2>();
            switch (shape)
            {
                case TagShape.Circle: Poly(outlinePts, 28, 0.5f, 0f); break;
                case TagShape.Square: Poly(outlinePts, 4, 0.6f, 45f); break;
                case TagShape.Triangle: Poly(outlinePts, 3, 0.58f, 0f); break;
                case TagShape.Diamond: Poly(outlinePts, 4, 0.58f, 0f); break;
                case TagShape.Hexagon: Poly(outlinePts, 6, 0.52f, 30f); break;
                case TagShape.Star:
                    for (int i = 0; i < 10; i++)
                    {
                        float a = Mathf.Deg2Rad * (90f + i * 36f), r = i % 2 == 0 ? 0.56f : 0.24f;
                        outlinePts.Add(new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r));
                    }
                    break;
                case TagShape.Heart:
                    for (int i = 0; i < 32; i++)
                    {
                        float t = i * Mathf.PI * 2f / 32f;
                        float x = 16f * Mathf.Pow(Mathf.Sin(t), 3f);
                        float y = 13f * Mathf.Cos(t) - 5f * Mathf.Cos(2f * t) - 2f * Mathf.Cos(3f * t) - Mathf.Cos(4f * t);
                        outlinePts.Add(new Vector2(x, y + 2f) / 34f);
                    }
                    break;
                default: // Cross (plus)
                    float w = 0.17f, l = 0.5f;
                    outlinePts.AddRange(new[] { new Vector2(w, l), new Vector2(w, w), new Vector2(l, w), new Vector2(l, -w), new Vector2(w, -w), new Vector2(w, -l),
                        new Vector2(-w, -l), new Vector2(-w, -w), new Vector2(-l, -w), new Vector2(-l, w), new Vector2(-w, w), new Vector2(-w, l) });
                    break;
            }
            return outlinePts;
        }

        static void Poly(List<Vector2> pts, int sides, float r, float startDeg)
        {
            for (int i = 0; i < sides; i++)
            {
                float a = Mathf.Deg2Rad * (90f + startDeg + i * 360f / sides);
                pts.Add(new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r));
            }
        }

#if UNITY_EDITOR
        /// <summary>Editor builder hook.</summary>
        public void EditorSetup(TextMesh label, TextMesh shadow, MeshFilter iconFilter, MeshRenderer iconMr)
        {
            text = label; outline = shadow; icon = iconFilter; iconRenderer = iconMr;
        }
#endif
    }
}
