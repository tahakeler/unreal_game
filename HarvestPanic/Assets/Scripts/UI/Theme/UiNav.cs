using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine.UIElements;
using Direction = UnityEngine.UIElements.NavigationMoveEvent.Direction;

namespace HarvestPanic.UI
{
    /// <summary>
    /// Explicit gamepad / arrow-key focus routes (unity-ui-specialist: "define explicit navigation routes, don't rely on
    /// automatic"). Each element keeps up/down/left/right targets; a link to a hidden or disabled element is skipped by
    /// following that element's own link in the same direction. Unlinked directions are swallowed when the element is
    /// inside a <see cref="Trap"/> (modal dialogs), otherwise UI Toolkit's default navigation runs.
    /// </summary>
    public static class UiNav
    {
        sealed class Links { public VisualElement Up, Down, Left, Right; public bool Hooked; }

        static readonly ConditionalWeakTable<VisualElement, Links> Table = new ConditionalWeakTable<VisualElement, Links>();
        static readonly ConditionalWeakTable<VisualElement, object> Traps = new ConditionalWeakTable<VisualElement, object>();

        /// <summary>Sets one directed link a -> b (b may be null to clear).</summary>
        public static void Link(VisualElement a, Direction dir, VisualElement b)
        {
            if (a == null) return;
            var l = Get(a);
            switch (dir)
            {
                case Direction.Up: l.Up = b; break;
                case Direction.Down: l.Down = b; break;
                case Direction.Left: l.Left = b; break;
                case Direction.Right: l.Right = b; break;
            }
        }

        /// <summary>Vertical list: Down goes to the next item, Up to the previous (wrapping when asked).</summary>
        public static void Column(IList<VisualElement> items, bool wrap = false)
        {
            for (int i = 0; i < items.Count; i++)
            {
                var prev = i > 0 ? items[i - 1] : wrap ? items[items.Count - 1] : null;
                var next = i < items.Count - 1 ? items[i + 1] : wrap ? items[0] : null;
                if (prev != null) Link(items[i], Direction.Up, prev);
                if (next != null) Link(items[i], Direction.Down, next);
            }
        }

        /// <summary>Horizontal row: Right goes to the next item, Left to the previous.</summary>
        public static void Row(IList<VisualElement> items, bool wrap = false)
        {
            for (int i = 0; i < items.Count; i++)
            {
                var prev = i > 0 ? items[i - 1] : wrap ? items[items.Count - 1] : null;
                var next = i < items.Count - 1 ? items[i + 1] : wrap ? items[0] : null;
                if (prev != null) Link(items[i], Direction.Left, prev);
                if (next != null) Link(items[i], Direction.Right, next);
            }
        }

        /// <summary>Every item of <paramref name="row"/> goes Up to <paramref name="above"/> and Down to <paramref name="below"/>.</summary>
        public static void RowVertical(IList<VisualElement> row, VisualElement above, VisualElement below)
        {
            foreach (var e in row)
            {
                if (above != null) Link(e, Direction.Up, above);
                if (below != null) Link(e, Direction.Down, below);
            }
        }

        /// <summary>Clears all links of <paramref name="e"/>.</summary>
        public static void Clear(VisualElement e)
        {
            if (e != null && Table.TryGetValue(e, out var l)) l.Up = l.Down = l.Left = l.Right = null;
        }

        /// <summary>Keeps keyboard/gamepad focus inside <paramref name="container"/> (modal dialogs).</summary>
        public static void Trap(VisualElement container)
        {
            if (container == null) return;
            Traps.Remove(container); Traps.Add(container, null);
        }

        /// <summary>Target of <paramref name="from"/> in <paramref name="dir"/>, skipping hidden/disabled links. Null when none.</summary>
        public static VisualElement Resolve(VisualElement from, Direction dir)
        {
            var cur = from;
            for (int hops = 0; hops < 32 && cur != null; hops++)
            {
                if (!Table.TryGetValue(cur, out var l)) return null;
                var next = dir == Direction.Up ? l.Up : dir == Direction.Down ? l.Down : dir == Direction.Left ? l.Left : l.Right;
                if (next == null) return null;
                if (CanFocus(next)) return next;
                cur = next;
            }
            return null;
        }

        /// <summary>True when the element is attached, displayed, visible, enabled and focusable.</summary>
        public static bool CanFocus(VisualElement e)
        {
            if (e == null || e.panel == null || !e.focusable || !e.enabledInHierarchy || !e.canGrabFocus) return false;
            for (var p = e; p != null; p = p.parent)
                if (p.resolvedStyle.display == DisplayStyle.None || !p.visible) return false;
            return true;
        }

        static Links Get(VisualElement e)
        {
            if (!Table.TryGetValue(e, out var l)) { l = new Links(); Table.Add(e, l); }
            if (!l.Hooked)
            {
                l.Hooked = true;
                e.RegisterCallback<NavigationMoveEvent>(OnMove);
            }
            return l;
        }

        static void OnMove(NavigationMoveEvent e)
        {
            if (!(e.currentTarget is VisualElement self) || e.target != self) return;
            if (e.direction == Direction.None || e.direction == Direction.Next || e.direction == Direction.Previous) return;
            // Sliders and choice rows use Left/Right for their value.
            if ((e.direction == Direction.Left || e.direction == Direction.Right) && UiKit.ConsumesHorizontal(self)) return;
            var target = Resolve(self, e.direction);
            if (target == null)
            {
                if (InTrap(self)) { e.StopPropagation(); self.focusController?.IgnoreEvent(e); }
                return;
            }
            target.Focus();
            e.StopPropagation();
            self.focusController?.IgnoreEvent(e);
        }

        static bool InTrap(VisualElement e)
        {
            for (var p = e; p != null; p = p.parent) if (Traps.TryGetValue(p, out _)) return true;
            return false;
        }
    }
}
