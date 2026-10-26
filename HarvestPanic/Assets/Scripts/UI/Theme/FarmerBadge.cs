using UnityEngine;
using UnityEngine.UIElements;

namespace HarvestPanic.UI
{
    /// <summary>
    /// Cut-out farmer portrait sticker for crew lists, the title profile chip and the lobby strip: overalls in the
    /// player's team colour (art bible 6.4), a hat silhouette per slot and the team shape pip in the corner, so a
    /// farmer is identifiable without colour (7.6). Pure vector (Painter2D), no textures, scales with UI scale.
    /// </summary>
    public sealed class FarmerBadge : VisualElement
    {
        public const string UssClass = "farmer-badge";
        static readonly Color Skin = new Color32(0xF0, 0xC8, 0xA0, 255), Blush = new Color32(0xE8, 0x9A, 0x86, 140),
            Straw = new Color32(0xD9, 0xB4, 0x6A, 255), Band = new Color32(0x5E, 0x3F, 0x27, 255), Sage = new Color32(0x86, 0x9B, 0x6B, 255),
            Denim = new Color32(0x5C, 0x6F, 0x8A, 255), Shirt = new Color32(0xF3, 0xE7, 0xCF, 255);

        int _slot;
        Color _team;
        bool _empty;
        readonly ShapePip _pip;

        public int Slot => _slot;

        public FarmerBadge(int slot, float size = 72f, bool empty = false)
        {
            AddToClassList(UssClass);
            style.width = size; style.height = size; style.flexShrink = 0;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            _pip = new ShapePip(ShapePip.TeamShape(slot), ShapePip.TeamColor(slot), Mathf.Round(size * 0.36f));
            _pip.AddToClassList("farmer-badge__pip");
            _pip.style.position = Position.Absolute; _pip.style.right = -size * 0.04f; _pip.style.bottom = -size * 0.04f;
            Add(_pip);
            Set(slot, empty);
        }

        /// <summary>Changes the slot (team colour, shape, hat) and whether this is an empty seat.</summary>
        public void Set(int slot, bool empty = false)
        {
            _slot = ((slot % 4) + 4) % 4; _empty = empty;
            _team = ShapePip.TeamColor(_slot);
            _pip.Set(ShapePip.TeamShape(_slot), empty ? new Color(1f, 1f, 1f, 0.6f) : _team);
            _pip.style.display = empty ? DisplayStyle.None : DisplayStyle.Flex;
            EnableInClassList("farmer-badge--empty", empty);
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext ctx)
        {
            var rect = contentRect;
            if (rect.width < 4f) return;
            var p = ctx.painter2D;
            float r = Mathf.Min(rect.width, rect.height) * 0.5f - 2f;
            var c = new Vector2(rect.width * 0.5f, rect.height * 0.5f);
            float lw = Mathf.Max(2f, r * 0.07f);
            var ink = UiKit.Ink;
            p.lineJoin = LineJoin.Round; p.lineCap = LineCap.Round;

            // sticker disc
            p.fillColor = _empty ? new Color(1f, 0.96f, 0.84f, 0.55f) : UiKit.Cream;
            p.strokeColor = _empty ? new Color(ink.r, ink.g, ink.b, 0.45f) : ink;
            p.lineWidth = lw;
            p.BeginPath(); p.Arc(c, r, 0f, 360f); p.ClosePath(); p.Fill(); p.Stroke();
            if (_empty)
            {
                // dashed question mark-ish seat: a faint silhouette
                p.fillColor = new Color(ink.r, ink.g, ink.b, 0.14f);
                p.BeginPath(); p.Arc(c + new Vector2(0f, -0.12f * r), 0.34f * r, 0f, 360f); p.Fill();
                Body(p, c, r, new Color(ink.r, ink.g, ink.b, 0.14f), null, 0f);
                return;
            }

            // overalls (team colour) clipped to the disc bottom, shirt collar, bib + straps
            Body(p, c, r, _team, ink, lw);
            p.fillColor = Shirt; p.strokeColor = ink; p.lineWidth = lw * 0.7f;
            p.BeginPath();
            p.MoveTo(c + new Vector2(-0.22f * r, 0.17f * r)); p.LineTo(c + new Vector2(0f, 0.36f * r)); p.LineTo(c + new Vector2(0.22f * r, 0.17f * r));
            p.ClosePath(); p.Fill(); p.Stroke();
            var bib = Color.Lerp(_team, ink, 0.18f);
            p.fillColor = bib;
            p.BeginPath();
            p.MoveTo(c + new Vector2(-0.3f * r, 0.46f * r)); p.LineTo(c + new Vector2(0.3f * r, 0.46f * r));
            p.LineTo(c + new Vector2(0.3f * r, 0.98f * r)); p.LineTo(c + new Vector2(-0.3f * r, 0.98f * r)); p.ClosePath(); p.Fill();
            p.strokeColor = bib; p.lineWidth = lw * 1.1f;
            p.BeginPath(); p.MoveTo(c + new Vector2(-0.26f * r, 0.5f * r)); p.LineTo(c + new Vector2(-0.42f * r, 0.22f * r)); p.Stroke();
            p.BeginPath(); p.MoveTo(c + new Vector2(0.26f * r, 0.5f * r)); p.LineTo(c + new Vector2(0.42f * r, 0.22f * r)); p.Stroke();
            p.fillColor = ink;
            foreach (float bx in new[] { -0.2f, 0.2f }) { p.BeginPath(); p.Arc(c + new Vector2(bx * r, 0.55f * r), 0.045f * r, 0f, 360f); p.Fill(); }

            // head
            var head = c + new Vector2(0f, -0.12f * r);
            float hr = 0.34f * r;
            p.fillColor = Skin; p.strokeColor = ink; p.lineWidth = lw;
            p.BeginPath(); p.Arc(head, hr, 0f, 360f); p.ClosePath(); p.Fill(); p.Stroke();
            p.fillColor = Blush;
            foreach (float bx in new[] { -0.19f, 0.19f }) { p.BeginPath(); p.Arc(head + new Vector2(bx * r, 0.07f * r), 0.06f * r, 0f, 360f); p.Fill(); }
            p.fillColor = ink;
            foreach (float ex in new[] { -0.12f, 0.12f }) { p.BeginPath(); p.Arc(head + new Vector2(ex * r, -0.02f * r), 0.042f * r, 0f, 360f); p.Fill(); }
            p.strokeColor = ink; p.lineWidth = lw * 0.75f;
            p.BeginPath(); p.Arc(head + new Vector2(0f, 0.05f * r), 0.12f * r, 25f, 155f); p.Stroke();

            Hat(p, head, hr, r, ink, lw);
        }

        static void Body(Painter2D p, Vector2 c, float r, Color fill, Color? stroke, float lw)
        {
            // Shoulder line meets the disc at y = 0.45r; the bottom follows the disc so nothing spills out.
            float y = 0.45f, x = Mathf.Sqrt(1f - y * y);
            float a0 = Mathf.Atan2(y, x) * Mathf.Rad2Deg, a1 = 180f - a0;
            p.fillColor = fill;
            p.BeginPath();
            p.MoveTo(c + new Vector2(-x * r, y * r));
            p.BezierCurveTo(c + new Vector2(-0.8f * r, 0.18f * r), c + new Vector2(-0.6f * r, 0.15f * r), c + new Vector2(-0.24f * r, 0.15f * r));
            p.LineTo(c + new Vector2(0.24f * r, 0.15f * r));
            p.BezierCurveTo(c + new Vector2(0.6f * r, 0.15f * r), c + new Vector2(0.8f * r, 0.18f * r), c + new Vector2(x * r, y * r));
            p.Arc(c, r, a0, a1);
            p.ClosePath();
            p.Fill();
            if (stroke.HasValue && lw > 0f) { p.strokeColor = stroke.Value; p.lineWidth = lw; p.Stroke(); }
        }

        void Hat(Painter2D p, Vector2 head, float hr, float r, Color ink, float lw)
        {
            p.strokeColor = ink; p.lineWidth = lw;
            switch (_slot)
            {
                case 0: // straw hat
                    p.fillColor = Straw;
                    Ellipse(p, head + new Vector2(0f, -0.62f * hr), 1.55f * hr, 0.32f * hr); p.Fill(); p.Stroke();
                    p.BeginPath();
                    p.MoveTo(head + new Vector2(-0.72f * hr, -0.66f * hr));
                    p.BezierCurveTo(head + new Vector2(-0.7f * hr, -1.45f * hr), head + new Vector2(0.7f * hr, -1.45f * hr), head + new Vector2(0.72f * hr, -0.66f * hr));
                    p.ClosePath(); p.Fill(); p.Stroke();
                    p.strokeColor = Band; p.lineWidth = lw * 1.3f;
                    p.BeginPath(); p.MoveTo(head + new Vector2(-0.7f * hr, -0.8f * hr)); p.LineTo(head + new Vector2(0.7f * hr, -0.8f * hr)); p.Stroke();
                    break;
                case 1: // ball cap with bill
                    p.fillColor = Denim;
                    p.BeginPath();
                    p.MoveTo(head + new Vector2(-0.98f * hr, -0.35f * hr));
                    p.BezierCurveTo(head + new Vector2(-0.95f * hr, -1.35f * hr), head + new Vector2(0.95f * hr, -1.35f * hr), head + new Vector2(0.98f * hr, -0.35f * hr));
                    p.ClosePath(); p.Fill(); p.Stroke();
                    p.BeginPath();
                    p.MoveTo(head + new Vector2(0.2f * hr, -0.38f * hr));
                    p.BezierCurveTo(head + new Vector2(0.9f * hr, -0.5f * hr), head + new Vector2(1.55f * hr, -0.42f * hr), head + new Vector2(1.6f * hr, -0.22f * hr));
                    p.LineTo(head + new Vector2(0.2f * hr, -0.22f * hr)); p.ClosePath(); p.Fill(); p.Stroke();
                    p.fillColor = ink; p.BeginPath(); p.Arc(head + new Vector2(0f, -1.08f * hr), 0.1f * hr, 0f, 360f); p.Fill();
                    break;
                case 2: // bucket hat
                    p.fillColor = Sage;
                    p.BeginPath();
                    p.MoveTo(head + new Vector2(-1.25f * hr, -0.45f * hr)); p.LineTo(head + new Vector2(1.25f * hr, -0.45f * hr));
                    p.LineTo(head + new Vector2(0.8f * hr, -0.7f * hr)); p.LineTo(head + new Vector2(-0.8f * hr, -0.7f * hr)); p.ClosePath(); p.Fill(); p.Stroke();
                    p.BeginPath();
                    p.MoveTo(head + new Vector2(-0.78f * hr, -0.7f * hr));
                    p.LineTo(head + new Vector2(-0.55f * hr, -1.3f * hr)); p.LineTo(head + new Vector2(0.55f * hr, -1.3f * hr)); p.LineTo(head + new Vector2(0.78f * hr, -0.7f * hr));
                    p.ClosePath(); p.Fill(); p.Stroke();
                    break;
                default: // knit beanie with pom
                    var wool = new Color32(0xC9, 0x8B, 0x5A, 255);
                    p.fillColor = wool;
                    p.BeginPath();
                    p.MoveTo(head + new Vector2(-0.96f * hr, -0.42f * hr));
                    p.BezierCurveTo(head + new Vector2(-0.9f * hr, -1.45f * hr), head + new Vector2(0.9f * hr, -1.45f * hr), head + new Vector2(0.96f * hr, -0.42f * hr));
                    p.ClosePath(); p.Fill(); p.Stroke();
                    p.fillColor = Color.Lerp(wool, ink, 0.2f);
                    p.BeginPath();
                    p.MoveTo(head + new Vector2(-1.0f * hr, -0.32f * hr)); p.LineTo(head + new Vector2(1.0f * hr, -0.32f * hr));
                    p.LineTo(head + new Vector2(0.98f * hr, -0.58f * hr)); p.LineTo(head + new Vector2(-0.98f * hr, -0.58f * hr)); p.ClosePath(); p.Fill(); p.Stroke();
                    p.fillColor = UiKit.Cream; p.BeginPath(); p.Arc(head + new Vector2(0f, -1.32f * hr), 0.2f * hr, 0f, 360f); p.Fill(); p.Stroke();
                    break;
            }
        }

        static void Ellipse(Painter2D p, Vector2 c, float rx, float ry)
        {
            const float k = 0.5523f;
            p.BeginPath();
            p.MoveTo(c + new Vector2(-rx, 0f));
            p.BezierCurveTo(c + new Vector2(-rx, -ry * k), c + new Vector2(-rx * k, -ry), c + new Vector2(0f, -ry));
            p.BezierCurveTo(c + new Vector2(rx * k, -ry), c + new Vector2(rx, -ry * k), c + new Vector2(rx, 0f));
            p.BezierCurveTo(c + new Vector2(rx, ry * k), c + new Vector2(rx * k, ry), c + new Vector2(0f, ry));
            p.BezierCurveTo(c + new Vector2(-rx * k, ry), c + new Vector2(-rx, ry * k), c + new Vector2(-rx, 0f));
            p.ClosePath();
        }
    }
}
