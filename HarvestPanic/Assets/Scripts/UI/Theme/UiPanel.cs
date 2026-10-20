using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace HarvestPanic.UI
{
    /// <summary>
    /// Runtime UI Toolkit panel built from Resources/Frontend assets (UXML + USS), kept in sync with <see cref="SettingsService"/>:
    /// UI scale (80 to 150 percent), text size and reduced motion. One panel per screen group; never mixed with UGUI.
    /// </summary>
    public sealed class UiPanel : MonoBehaviour
    {
        const string ResPath = "Frontend/";
        static readonly string[] SharedSheets = { "USS_Default_Theme", "USS_Default_Menu", "USS_Default_Lobby", "USS_Default_Pause", "USS_Default_Onboarding" };

        PanelSettings _panel;
        UIDocument _doc;

        /// <summary>The <c>.hp-root</c> element (child of the document root).</summary>
        public VisualElement Root { get; private set; }
        public UIDocument Document => _doc;
        public PanelSettings Panel => _panel;

        /// <summary>Creates a child GameObject with a configured UIDocument.</summary>
        public static UiPanel Create(Transform parent, string uxmlName, int sortOrder)
        {
            var go = new GameObject("UI_" + uxmlName) { hideFlags = HideFlags.None };
            go.SetActive(false);
            if (parent != null) go.transform.SetParent(parent, false);
            var p = go.AddComponent<UiPanel>();
            var uxml = Resources.Load<VisualTreeAsset>(ResPath + uxmlName);
            if (uxml == null) throw new InvalidOperationException("Missing UXML Resources/" + ResPath + uxmlName);

            p._panel = ScriptableObject.CreateInstance<PanelSettings>();
            p._panel.name = "HP_Panel_" + uxmlName;
            p._panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>(ResPath + "HP_Theme");
            p._panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            p._panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            p._panel.match = 0.5f;
            p._panel.referenceResolution = new Vector2Int(1920, 1080);
            p._panel.sortingOrder = sortOrder;
            p._panel.clearColor = false;

            p._doc = go.AddComponent<UIDocument>();
            p._doc.panelSettings = p._panel;
            p._doc.visualTreeAsset = uxml;
            go.SetActive(true);

            var rootHost = p._doc.rootVisualElement;
            foreach (var n in SharedSheets)
            {
                var sheet = Resources.Load<StyleSheet>(ResPath + n);
                if (sheet != null) rootHost.styleSheets.Add(sheet);
            }
            p.Root = rootHost.Q("root") ?? rootHost;
            p.Root.style.flexGrow = 1;
            SettingsService.Changed += p.OnSettings;
            p.OnSettings(SettingsService.Current);
            return p;
        }

        void OnSettings(GameSettings s)
        {
            if (_panel == null || Root == null) return;
            float k = Mathf.Clamp(s.UiScale, GameSettings.MinUiScale, GameSettings.MaxUiScale);
            _panel.referenceResolution = new Vector2Int(Mathf.RoundToInt(1920f / k), Mathf.RoundToInt(1080f / k));
            Root.EnableInClassList("hp-root--text-small", s.TextSize == 0);
            Root.EnableInClassList("hp-root--text-large", s.TextSize == 2);
            Root.EnableInClassList("hp-root--reduced-motion", s.ReducedMotion);
            foreach (ColorblindMode m in System.Enum.GetValues(typeof(ColorblindMode)))
            {
                string cls = UiPalette.RootClass(m);
                if (cls.Length > 0) Root.EnableInClassList(cls, m == s.Colorblind);
            }
        }

        void OnDestroy()
        {
            SettingsService.Changed -= OnSettings;
            if (_panel != null) Destroy(_panel);
        }
    }
}
