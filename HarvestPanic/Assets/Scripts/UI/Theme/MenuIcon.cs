using UnityEngine;
using UnityEngine.UIElements;

namespace HarvestPanic.UI
{
    public enum MenuIconKind { Barn, Gate, Ledger, Friends, Lan, Signpost }

    /// <summary>Hand-inked pictograms for menu cards (host barn, join gate, continue ledger, friends, LAN, address sign).</summary>
    public sealed class MenuIcon : VisualElement
    {
        static readonly Color Brick = new Color32(0xB0, 0x5A, 0x45, 255), Roof = new Color32(0x5A, 0x4A, 0x44, 255), Wood = new Color32(0xA9, 0x7A, 0x4E, 255),
            Book = new Color32(0x6E, 0x8F, 0x5A, 255), Page = new Color32(0xFF, 0xF4, 0xD6, 255), Sky = new Color32(0x9E, 0xD0, 0xE6, 255);
        MenuIconKind _kind;

        public MenuIcon(MenuIconKind kind, float size = 120f)
        {
            _kind = kind;
            AddToClassList("menu-icon");
            style.width = size; style.height = size; style.flexShrink = 0;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public void Set(MenuIconKind kind) { _kind = kind; MarkDirtyRepaint(); }

        void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect;
            if (r.width < 8f) return;
            var p = ctx.painter2D;
            float s = Mathf.Min(r.width, r.height);
            Vector2 o = new Vector2((r.width - s) * 0.5f, (r.height - s) * 0.5f);
            Vector2 P(float x, float y) => o + new Vector2(x * s, y * s);
            float lw = Mathf.Max(2.5f, s * 0.035f);
            p.lineJoin = LineJoin.Round; p.lineCap = LineCap.Round; p.strokeColor = UiKit.Ink; p.lineWidth = lw;

            void Poly(Color fill, params Vector2[] pts)
            {
                p.fillColor = fill; p.BeginPath(); p.MoveTo(pts[0]);
                for (int i = 1; i < pts.Length; i++) p.LineTo(pts[i]);
                p.ClosePath(); p.Fill(); p.Stroke();
            }
            void Line(Vector2 a, Vector2 b) { p.BeginPath(); p.MoveTo(a); p.LineTo(b); p.Stroke(); }
            void Circle(Vector2 c, float rad, Color fill) { p.fillColor = fill; p.BeginPath(); p.Arc(c, rad * s, 0f, 360f); p.Fill(); p.Stroke(); }

            switch (_kind)
            {
                case MenuIconKind.Barn:
                    Poly(Brick, P(0.16f, 0.88f), P(0.16f, 0.45f), P(0.5f, 0.2f), P(0.84f, 0.45f), P(0.84f, 0.88f));
                    Poly(Roof, P(0.08f, 0.5f), P(0.5f, 0.12f), P(0.92f, 0.5f), P(0.84f, 0.55f), P(0.5f, 0.24f), P(0.16f, 0.55f));
                    Poly(UiKit.Cream, P(0.36f, 0.88f), P(0.36f, 0.6f), P(0.64f, 0.6f), P(0.64f, 0.88f));
                    Line(P(0.36f, 0.6f), P(0.64f, 0.88f)); Line(P(0.64f, 0.6f), P(0.36f, 0.88f));
                    Poly(UiKit.Cream, P(0.44f, 0.46f), P(0.44f, 0.36f), P(0.56f, 0.36f), P(0.56f, 0.46f));
                    break;
                case MenuIconKind.Gate:
                    Poly(Wood, P(0.12f, 0.88f), P(0.12f, 0.22f), P(0.22f, 0.22f), P(0.22f, 0.88f));
                    Poly(Wood, P(0.78f, 0.88f), P(0.78f, 0.22f), P(0.88f, 0.22f), P(0.88f, 0.88f));
                    foreach (float y in new[] { 0.34f, 0.56f, 0.78f }) Poly(UiKit.Cream, P(0.22f, y - 0.05f), P(0.78f, y - 0.05f), P(0.78f, y + 0.04f), P(0.22f, y + 0.04f));
                    Line(P(0.24f, 0.8f), P(0.76f, 0.32f));
                    Circle(P(0.5f, 0.12f), 0.07f, Sky);
                    break;
                case MenuIconKind.Ledger:
                    Poly(Book, P(0.18f, 0.86f), P(0.18f, 0.16f), P(0.8f, 0.16f), P(0.8f, 0.86f));
                    Poly(Page, P(0.26f, 0.8f), P(0.26f, 0.22f), P(0.74f, 0.22f), P(0.74f, 0.8f));
                    foreach (float y in new[] { 0.34f, 0.46f, 0.58f, 0.7f }) Line(P(0.33f, y), P(0.67f, y));
                    Poly(UiKit.Ink, P(0.6f, 0.16f), P(0.6f, 0.4f), P(0.66f, 0.34f), P(0.72f, 0.4f), P(0.72f, 0.16f));
                    break;
                case MenuIconKind.Friends:
                    Circle(P(0.36f, 0.36f), 0.13f, new Color32(0xF0, 0xC8, 0xA0, 255));
                    Circle(P(0.66f, 0.4f), 0.12f, new Color32(0xE2, 0xB0, 0x86, 255));
                    Poly(new Color32(0xE6, 0x9F, 0x00, 255), P(0.12f, 0.86f), P(0.16f, 0.6f), P(0.36f, 0.52f), P(0.56f, 0.6f), P(0.6f, 0.86f));
                    Poly(new Color32(0x56, 0xB4, 0xE9, 255), P(0.46f, 0.86f), P(0.5f, 0.64f), P(0.66f, 0.56f), P(0.84f, 0.64f), P(0.88f, 0.86f));
                    break;
                case MenuIconKind.Lan:
                    p.lineWidth = lw * 1.6f;
                    for (int i = 1; i <= 3; i++) { p.BeginPath(); p.Arc(P(0.5f, 0.78f), i * 0.17f * s, 220f, 320f); p.Stroke(); }
                    p.lineWidth = lw;
                    Circle(P(0.5f, 0.78f), 0.07f, UiKit.Cream);
                    break;
                default: // signpost
                    Poly(Wood, P(0.46f, 0.92f), P(0.46f, 0.14f), P(0.54f, 0.14f), P(0.54f, 0.92f));
                    Poly(UiKit.Cream, P(0.18f, 0.24f), P(0.74f, 0.24f), P(0.86f, 0.33f), P(0.74f, 0.42f), P(0.18f, 0.42f));
                    Poly(Sky, P(0.82f, 0.5f), P(0.26f, 0.5f), P(0.14f, 0.59f), P(0.26f, 0.68f), P(0.82f, 0.68f));
                    break;
            }
        }
    }
}
