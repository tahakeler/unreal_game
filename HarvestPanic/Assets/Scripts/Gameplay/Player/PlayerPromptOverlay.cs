using System.Collections.Generic;
using HarvestPanic.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace HarvestPanic.Gameplay
{
    /// <summary>
    /// Owner-only centre HUD (player-character 5f; hud-menus "Contextual prompts"; art-bible §7 ledger skin), UI Toolkit:
    /// a small cream reticle dot that opens into a ring on a grabbable, a paper prompt chip with key caps
    /// ("[E] Grab (0/2 hands)"), and a stamina arc beside the reticle that fades out when full. Never colour-only:
    /// the arc length carries the value and exhaustion adds a "WINDED" tag. Hidden while an input modal
    /// (pause, wardrobe) is open. Presentation only: reads <see cref="PlayerCarry"/> and the motor's stamina.
    /// </summary>
    public sealed class PlayerPromptOverlay : MonoBehaviour
    {
        [SerializeField] bool show = true;
        [SerializeField, Tooltip("Shared HUD panel (Prefabs/HUD/EconomyHudPanel) so scaling matches the day HUD")] PanelSettings panelSettings;
        [SerializeField] StyleSheet styleSheet;
        [SerializeField] Font headlineFont;
        [SerializeField] Font bodyFont;
        [SerializeField, Tooltip("Seconds the stamina arc stays after refilling")] float staminaHideDelay = 0.6f;

        PlayerCharacter _pc;
        PlayerCarry _carry;
        UIDocument _doc;
        VisualElement _root, _reticle, _chip;
        StaminaArc _arc;
        Label _winded;
        string _shownPrompt;
        float _fullSince = -1f;
        readonly List<PromptAction> _actions = new List<PromptAction>();

        /// <summary>The overlay's UIDocument once built (QA captures).</summary>
        public UIDocument Document => _doc;
        /// <summary>True while the reticle shows the "grabbable" ring (tests).</summary>
        public bool ReticleOpen => _reticle != null && _reticle.ClassListContains("hp-reticle--grab");
        /// <summary>Visible prompt chip text (tests): key caps and labels joined.</summary>
        public string ChipText { get; private set; } = string.Empty;
        /// <summary>Stamina arc visibility 0..1 (tests).</summary>
        public float ArcOpacity => _arc != null ? _arc.resolvedStyle.opacity : 0f;

        void Awake() { _pc = GetComponent<PlayerCharacter>(); _carry = GetComponent<PlayerCarry>(); }

        void OnDisable() { if (_doc != null) _doc.gameObject.SetActive(false); }

        void OnEnable() { if (_doc != null) _doc.gameObject.SetActive(true); }

        void OnDestroy() { if (_doc != null) Destroy(_doc.gameObject); }

        void Build()
        {
            var go = new GameObject("PlayerCentreHUD");
            go.transform.SetParent(transform, false);
            _doc = go.AddComponent<UIDocument>();
            if (panelSettings == null)
            {
                panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                panelSettings.referenceResolution = new Vector2Int(1920, 1080);
            }
            _doc.sortingOrder = -1; // under the day HUD and receipts
            _doc.panelSettings = panelSettings;

            var root = _doc.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            if (styleSheet != null) root.styleSheets.Add(styleSheet);
            if (bodyFont != null) root.style.unityFontDefinition = FontDefinition.FromFont(bodyFont);
            _root = new VisualElement { name = "hp-centre", pickingMode = PickingMode.Ignore };
            _root.AddToClassList("hp-centre");
            root.Add(_root);

            _reticle = new VisualElement { name = "hp-reticle", pickingMode = PickingMode.Ignore };
            _reticle.AddToClassList("hp-reticle");
            var inner = new VisualElement { pickingMode = PickingMode.Ignore };
            inner.AddToClassList("hp-reticle-dot");
            _reticle.Add(inner);
            _root.Add(_reticle);

            _arc = new StaminaArc { name = "hp-stamina", pickingMode = PickingMode.Ignore };
            _arc.AddToClassList("hp-stamina");
            _root.Add(_arc);
            _winded = new Label("WINDED") { pickingMode = PickingMode.Ignore };
            _winded.AddToClassList("hp-winded");
            Headline(_winded);
            _root.Add(_winded);

            _chip = new VisualElement { name = "hp-prompt", pickingMode = PickingMode.Ignore };
            _chip.AddToClassList("hp-prompt");
            _root.Add(_chip);
        }

        void Headline(VisualElement v) { if (headlineFont != null) v.style.unityFontDefinition = FontDefinition.FromFont(headlineFont); }

        void Update()
        {
            bool active = show && _pc != null && _pc.IsOwnerSafe && _pc.IsSpawned;
            if (!active) { if (_root != null) _root.style.display = DisplayStyle.None; return; }
            if (_doc == null) Build();
            bool visible = !InputModal.IsBlocking;
            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible) return;

            bool down = _pc.State != PlayerState.Normal;
            _reticle.style.display = down ? DisplayStyle.None : DisplayStyle.Flex;
            _reticle.EnableInClassList("hp-reticle--grab", _carry.AimingAtGrabbable);
            _reticle.EnableInClassList("hp-reticle--holding", _carry.IsHoldingReplicated);

            // Stamina arc: shown while below max; fades after staminaHideDelay at full.
            var st = _pc.Motor != null ? _pc.Motor.Stamina : null;
            float max = Mathf.Max(1f, st != null ? st.Max : _pc.Tuning.StaminaMax);
            float frac = st != null ? Mathf.Clamp01(st.Value / max) : 1f;
            bool exhausted = st != null && st.Exhausted;
            if (frac >= 0.999f) { if (_fullSince < 0f) _fullSince = Time.unscaledTime; }
            else _fullSince = -1f;
            bool showArc = !down && (_fullSince < 0f || Time.unscaledTime - _fullSince < staminaHideDelay);
            _arc.EnableInClassList("hp-stamina--hidden", !showArc);
            _arc.EnableInClassList("hp-stamina--exhausted", exhausted);
            _arc.SetValue(frac, exhausted);
            _winded.style.display = exhausted && showArc ? DisplayStyle.Flex : DisplayStyle.None;

            SetPrompt(_carry.Prompt);
        }

        void SetPrompt(string prompt)
        {
            if (prompt == _shownPrompt) return;
            _shownPrompt = prompt;
            _chip.Clear();
            PromptFormat.Parse(prompt, _actions);
            _chip.style.display = _actions.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            var sb = new System.Text.StringBuilder();
            foreach (var a in _actions)
            {
                var row = new VisualElement { pickingMode = PickingMode.Ignore };
                row.AddToClassList("hp-prompt-action");
                if (!string.IsNullOrEmpty(a.Key))
                {
                    var key = new Label(a.Key) { pickingMode = PickingMode.Ignore };
                    key.AddToClassList("hp-key");
                    Headline(key);
                    row.Add(key);
                    sb.Append('[').Append(a.Key).Append("] ");
                }
                var label = new Label(a.Label) { pickingMode = PickingMode.Ignore };
                label.AddToClassList("hp-prompt-text");
                row.Add(label);
                sb.Append(a.Label).Append("  ");
                _chip.Add(row);
            }
            ChipText = sb.ToString().TrimEnd();
        }

        /// <summary>Stamina ring segment drawn with Painter2D (right of the reticle, bottom to top).</summary>
        sealed class StaminaArc : VisualElement
        {
            float _value = 1f;
            bool _exhausted;
            static readonly Color Track = new Color(0.17f, 0.13f, 0.09f, 0.45f);
            static readonly Color Fill = new Color(1f, 0.957f, 0.839f, 1f);      // cream #FFF4D6
            static readonly Color Tired = new Color(0.898f, 0.282f, 0.231f, 1f); // tomato #E5483B
            static readonly Color Ink = new Color(0.169f, 0.129f, 0.094f, 1f);   // ink #2B2118
            const float Start = 50f, Sweep = -100f; // degrees from +X, y down: bottom-right (50) up to top-right (-50)

            public StaminaArc() { generateVisualContent += Draw; }

            public void SetValue(float v, bool exhausted)
            {
                if (Mathf.Abs(v - _value) < 0.002f && exhausted == _exhausted) return;
                _value = v; _exhausted = exhausted;
                MarkDirtyRepaint();
            }

            void Draw(MeshGenerationContext ctx)
            {
                var r = contentRect;
                var c = r.center;
                float radius = Mathf.Min(r.width, r.height) * 0.5f - 4f;
                var p = ctx.painter2D;
                p.lineCap = LineCap.Round;
                // Ink outline under the track keeps it readable on bright wheat (art-bible §7.3).
                Stroke(p, c, radius, Start, Start + Sweep, 8f, Ink * new Color(1f, 1f, 1f, 0.55f));
                Stroke(p, c, radius, Start, Start + Sweep, 5f, Track);
                if (_value > 0.005f) Stroke(p, c, radius, Start, Start + Sweep * _value, 5f, _exhausted ? Tired : Fill);
            }

            static void Stroke(Painter2D p, Vector2 c, float radius, float from, float to, float width, Color col)
            {
                p.lineWidth = width;
                p.strokeColor = col;
                p.BeginPath();
                p.Arc(c, radius, Angle.Degrees(from), Angle.Degrees(to), to < from ? ArcDirection.CounterClockwise : ArcDirection.Clockwise);
                p.Stroke();
            }
        }
    }
}
