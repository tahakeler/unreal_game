using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace HarvestPanic.UI
{
    /// <summary>
    /// Shared widget factories and panel decoration for the scrapbook theme (USS_Default_Theme). Every screen builds its
    /// buttons, chips and labels here so focus/hover/press states, sounds and string-table text stay identical everywhere.
    /// </summary>
    public static class UiKit
    {
        /// <summary>Focusable settings/list row (gamepad focus lands on the whole row).</summary>
        public const string FocusRowClass = "hp-row--focus";
        /// <summary>Row whose value changes with Left/Right (choice rows).</summary>
        public const string ChoiceClass = "hp-choice";
        public const string BackClass = "hp-btn--back";
        public const string SelectedClass = "is-selected";
        const string EnteringClass = "is-entering";

        /// <summary>Text from the string table when <paramref name="keyOrText"/> starts with '@', else as given (names, numbers).</summary>
        public static string T(string keyOrText) =>
            !string.IsNullOrEmpty(keyOrText) && keyOrText[0] == UiText.KeyPrefix ? UiText.Get(keyOrText) : keyOrText ?? "";

        public static Button Button(string keyOrText, Action onClick, params string[] classes)
        {
            var b = new Button(onClick) { text = T(keyOrText) };
            b.AddToClassList("hp-btn");
            foreach (var c in classes) if (!string.IsNullOrEmpty(c)) b.AddToClassList(c);
            return b;
        }

        public static Label Label(string keyOrText, params string[] classes)
        {
            var l = new Label(T(keyOrText));
            foreach (var c in classes) if (!string.IsNullOrEmpty(c)) l.AddToClassList(c);
            return l;
        }

        /// <summary>Selectable chip (crew size, privacy, tabs): a small button that shows a stamped check when selected.</summary>
        public static Button Chip(string keyOrText, Action onClick, string name = null)
        {
            var b = Button(keyOrText, onClick, "hp-chip");
            if (name != null) b.name = name;
            return b;
        }

        public static void Select(VisualElement e, bool selected) => e?.EnableInClassList(SelectedClass, selected);

        /// <summary>True for elements that use Left/Right themselves (sliders, choice rows, text fields).</summary>
        public static bool ConsumesHorizontal(VisualElement e) =>
            e is Slider || e is SliderInt || e is TextField || e.ClassListContains(ChoiceClass);

        /// <summary>
        /// One-time decoration of a panel root: menu sounds for focus, hover, click and submit on any button
        /// (<see cref="BackClass"/> buttons play the back sound), and highlight of the row that holds focus.
        /// </summary>
        public static void Decorate(VisualElement root)
        {
            UiSounds.AttachFocusSounds(root);
            VisualElement lastHover = null;
            root.RegisterCallback<PointerOverEvent>(e =>
            {
                var b = ButtonOf(e.target as VisualElement);
                if (b == null || b == lastHover || !b.enabledInHierarchy) return;
                lastHover = b;
                UiSounds.Hover();
            }, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerOutEvent>(e => { if (ButtonOf(e.target as VisualElement) == lastHover) lastHover = null; }, TrickleDown.TrickleDown);
            root.RegisterCallback<ClickEvent>(e => PressSound(e.target as VisualElement), TrickleDown.TrickleDown);
            root.RegisterCallback<NavigationSubmitEvent>(e => PressSound(e.target as VisualElement), TrickleDown.TrickleDown);
            root.RegisterCallback<FocusInEvent>(e => MarkRow(e.target as VisualElement, true), TrickleDown.TrickleDown);
            root.RegisterCallback<FocusOutEvent>(e => MarkRow(e.target as VisualElement, false), TrickleDown.TrickleDown);
        }

        static void PressSound(VisualElement target)
        {
            var b = ButtonOf(target);
            if (b == null || !b.enabledInHierarchy) return;
            if (b.ClassListContains(BackClass)) UiSounds.Back(); else UiSounds.Confirm();
        }

        static Button ButtonOf(VisualElement e)
        {
            for (var p = e; p != null; p = p.parent)
            {
                if (p is Button b) return b;
                if (p.ClassListContains("hp-root")) return null;
            }
            return null;
        }

        static void MarkRow(VisualElement e, bool on)
        {
            for (var p = e; p != null; p = p.parent)
            {
                if (p.ClassListContains("setting-row")) { p.EnableInClassList("is-focused", on); return; }
                if (p.ClassListContains("hp-root")) return;
            }
        }

        // ------------------------------------------------------------------ screen animation

        /// <summary>Shows a screen with the enter transition (fade + small rise); instant with reduced motion.</summary>
        public static void ShowScreen(VisualElement screen, bool animate = true)
        {
            if (screen == null) return;
            bool already = screen.ClassListContains("is-active");
            screen.AddToClassList("is-active");
            if (already || !animate || ReducedMotion) { screen.RemoveFromClassList(EnteringClass); return; }
            screen.AddToClassList(EnteringClass);
            screen.schedule.Execute(() => screen.RemoveFromClassList(EnteringClass)).StartingIn(16);
        }

        public static void HideScreen(VisualElement screen)
        {
            if (screen == null) return;
            screen.RemoveFromClassList("is-active");
            screen.RemoveFromClassList(EnteringClass);
        }

        /// <summary>Adds <paramref name="cls"/> for <paramref name="ms"/> milliseconds (bounces, flashes). No-op with reduced motion.</summary>
        public static void Pulse(VisualElement e, string cls, long ms = 180)
        {
            if (e == null || ReducedMotion) return;
            e.AddToClassList(cls);
            e.schedule.Execute(() => e.RemoveFromClassList(cls)).StartingIn(ms);
        }

        public static bool ReducedMotion => SettingsService.Current.ReducedMotion;

        /// <summary>Ink colour of the theme (also used by vector widgets).</summary>
        public static readonly Color Ink = new Color32(0x2B, 0x21, 0x18, 255);
        public static readonly Color Paper = new Color32(0xF6, 0xEB, 0xD3, 255);
        public static readonly Color Cream = new Color32(0xFF, 0xF4, 0xD6, 255);
    }
}
