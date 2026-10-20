using System;
using System.Collections.Generic;
using HarvestPanic.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace HarvestPanic.Customization
{
    /// <summary>
    /// Wardrobe screen (GDD "Wardrobe (barn)"), UI Toolkit, built in code so it has no UXML dependency.
    /// Tabs: Body (variant + skin), Colours (overalls/shirt/boots from the 24-swatch palette), Hat, Face, Back,
    /// Tag (name-tag colour + shape), Emotes (keys 5-8). Live 3D preview via <see cref="WardrobePreview"/>.
    /// Buttons: Surprise me!, Revert, Cancel, Confirm. Esc = Cancel. Changes apply only on Confirm.
    /// The world keeps running; the wardrobe closes if the farmer faints/ragdolls.
    /// </summary>
    public sealed class WardrobeUI : MonoBehaviour
    {
        enum Tab { Body, Colours, Hat, Face, Back, Tag, Emotes }

        static readonly Color Cream = new Color(1f, 0.957f, 0.839f), Wood = new Color(0.36f, 0.24f, 0.15f), WoodDark = new Color(0.22f, 0.14f, 0.09f),
            Ink = new Color(0.2f, 0.13f, 0.08f), Gold = new Color(0.96f, 0.77f, 0.26f), Green = new Color(0.25f, 0.68f, 0.35f);

        /// <summary>Open wardrobe, or null.</summary>
        public static WardrobeUI Current { get; private set; }

        /// <summary>True while a wardrobe screen is open.</summary>
        public static bool IsOpen => Current != null;

        /// <summary>Raised when the wardrobe closes (true = confirmed).</summary>
        public static event Action<bool> Closed;

        FarmerLoadoutSync _target;
        CosmeticCatalog _catalog;
        FarmerLoadout _original, _working;
        Tab _tab = Tab.Hat;
        WardrobePreview _preview;
        UIDocument _doc;
        VisualElement _content, _tabBar;
        Label _flavour;
        Image _previewImage;
        CursorLockMode _prevLock;
        bool _prevVisible;
        System.Random _rng = new System.Random();
        bool _dragging;

        /// <summary>Working (unconfirmed) loadout.</summary>
        public FarmerLoadout Working => _working;

        /// <summary>The preview rig.</summary>
        public WardrobePreview Preview => _preview;

        /// <summary>
        /// Opens the wardrobe for <paramref name="target"/> (normally <see cref="FarmerLoadoutSync.Local"/>).
        /// <paramref name="panelOverride"/> lets offscreen captures render the panel into a RenderTexture.
        /// </summary>
        public static WardrobeUI Open(FarmerLoadoutSync target, PanelSettings panelOverride = null)
        {
            if (target == null || target.Visual.Catalog == null) return null;
            if (Current != null) return Current;
            var cat = target.Visual.Catalog;
            var panel = panelOverride != null ? panelOverride : cat.wardrobePanel;
            if (panel == null) { Debug.LogWarning("[Wardrobe] Catalog has no PanelSettings"); return null; }

            var go = new GameObject("WardrobeUI");
            go.SetActive(false);
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = panel;
            doc.sortingOrder = 100;
            var ui = go.AddComponent<WardrobeUI>();
            ui._doc = doc;
            ui._target = target;
            ui._catalog = cat;
            ui._original = ui._working = target.Loadout;
            Current = ui;
            go.SetActive(true);
            return ui;
        }

        void OnEnable()
        {
            if (_doc == null) return;
            _prevLock = UnityEngine.Cursor.lockState; _prevVisible = UnityEngine.Cursor.visible;
            UnityEngine.Cursor.lockState = CursorLockMode.None; UnityEngine.Cursor.visible = true;
            FarmerEmotes.InputBlocked = true;
            InputModal.Push(this); // pauses player move/look/interact while the wardrobe is open
            _preview = WardrobePreview.Create(_catalog);
            _preview?.Apply(_working);
            BuildLayout();
            ShowTab(_tab);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) { Close(false); return; }
            var anim = _target != null ? _target.Visual.BodyAnimator : null;
            if (_target == null || (anim != null && anim.isActiveAndEnabled && anim.GetBool(FarmerVisual.FaintedParam))) Close(false);
        }

        /// <summary>Closes the wardrobe; <paramref name="confirm"/> sends the working loadout.</summary>
        public void Close(bool confirm)
        {
            if (Current != this) return;
            if (confirm && _target != null) _target.RequestLoadout(_working);
            Current = null;
            FarmerEmotes.InputBlocked = false;
            InputModal.Pop(this);
            UnityEngine.Cursor.lockState = _prevLock; UnityEngine.Cursor.visible = _prevVisible;
            if (_preview != null) Destroy(_preview.gameObject);
            Destroy(gameObject);
            Closed?.Invoke(confirm);
        }

        void OnDestroy()
        {
            InputModal.Pop(this); // safety: never leave input held if destroyed without Close
            if (Current == this) { Current = null; FarmerEmotes.InputBlocked = false; }
        }

        /// <summary>Randomises the working loadout ("Surprise me!").</summary>
        public void SurpriseMe() => SetWorking(LoadoutStorage.Randomize(_catalog, _rng, _working));

        /// <summary>Restores the loadout the wardrobe opened with.</summary>
        public void Revert() => SetWorking(_original);

        /// <summary>Sets the working loadout and refreshes preview + current tab.</summary>
        public void SetWorking(FarmerLoadout l)
        {
            _working = l;
            _preview?.Apply(_working);
            ShowTab(_tab);
        }

        // ---- Layout -------------------------------------------------------------------------------------------------

        void BuildLayout()
        {
            var root = _doc.rootVisualElement;
            root.Clear();
            root.style.flexGrow = 1;
            var screen = Box(root, flexRow: true);
            screen.style.flexGrow = 1;
            screen.style.backgroundColor = new Color(0.08f, 0.05f, 0.03f, 0.55f);
            screen.style.paddingLeft = screen.style.paddingRight = 60;
            screen.style.paddingTop = screen.style.paddingBottom = 50;

            // Left: preview
            var left = Panel(screen, 560);
            left.style.marginRight = 24;
            Title(left, "GRANDPA'S WARDROBE", 34);
            _previewImage = new Image { image = _preview != null ? _preview.Texture : null, scaleMode = ScaleMode.ScaleAndCrop };
            _previewImage.style.flexGrow = 1;
            _previewImage.style.marginTop = 10;
            SetRadius(_previewImage, 14);
            SetBorder(_previewImage, 4, WoodDark);
            _previewImage.RegisterCallback<PointerDownEvent>(e => { _dragging = true; _previewImage.CapturePointer(e.pointerId); });
            _previewImage.RegisterCallback<PointerUpEvent>(e => { _dragging = false; _previewImage.ReleasePointer(e.pointerId); });
            _previewImage.RegisterCallback<PointerMoveEvent>(e => { if (_dragging) _preview?.Orbit(e.deltaPosition.x * 0.4f, e.deltaPosition.y * 0.2f); });
            _previewImage.RegisterCallback<WheelEvent>(e => _preview?.Zoom(e.delta.y * 0.05f));
            left.Add(_previewImage);
            var hint = Text(left, "Drag to spin  ·  Scroll to zoom", 16, Ink);
            hint.style.unityTextAlign = TextAnchor.MiddleCenter;
            hint.style.marginTop = 6;

            // Right: tabs + content
            var right = Panel(screen, 0);
            right.style.flexGrow = 1;
            _tabBar = Box(right, flexRow: true);
            _tabBar.style.flexWrap = Wrap.Wrap;
            foreach (Tab t in Enum.GetValues(typeof(Tab)))
            {
                var tab = t;
                var b = MakeButton(_tabBar, tab.ToString(), () => ShowTab(tab), 20);
                b.name = "tab-" + tab;
            }
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.style.marginTop = 10;
            right.Add(scroll);
            _content = scroll.contentContainer;

            _flavour = Text(right, "", 20, Ink);
            _flavour.style.unityFontStyleAndWeight = FontStyle.Italic;
            _flavour.style.minHeight = 34;
            _flavour.style.whiteSpace = WhiteSpace.Normal;
            _flavour.style.marginTop = 6;

            var actions = Box(right, flexRow: true);
            actions.style.justifyContent = Justify.SpaceBetween;
            actions.style.marginTop = 10;
            var surprise = MakeButton(actions, "Surprise me!", SurpriseMe, 24, Gold);
            surprise.name = "btn-surprise";
            var group = Box(actions, flexRow: true);
            MakeButton(group, "Revert", Revert, 22).name = "btn-revert";
            MakeButton(group, "Cancel (Esc)", () => Close(false), 22).name = "btn-cancel";
            MakeButton(group, "Confirm", () => Close(true), 24, Green).name = "btn-confirm";
        }

        void ShowTab(Tab tab)
        {
            _tab = tab;
            if (_content == null) return;
            _content.Clear();
            foreach (var child in _tabBar.Children())
            {
                bool on = child.name == "tab-" + tab;
                child.style.backgroundColor = on ? Gold : Cream;
            }
            switch (tab)
            {
                case Tab.Body: BodyTab(); break;
                case Tab.Colours: ColoursTab(); break;
                case Tab.Hat: ItemTab(CosmeticSlot.Hat, "Bald & Proud", "The breeze is free."); break;
                case Tab.Face: ItemTab(CosmeticSlot.Face, "Bare Face", "Choose your expression for the inevitable."); break;
                case Tab.Back: ItemTab(CosmeticSlot.Back, "Nothing", "Travelling light. Suspiciously light."); break;
                case Tab.Tag: TagTab(); break;
                case Tab.Emotes: EmoteTab(); break;
            }
        }

        void BodyTab()
        {
            Section("Body type");
            var row = Grid();
            for (int i = 0; i < _catalog.bodies.Count; i++)
            {
                int idx = i;
                var b = _catalog.bodies[i];
                Card(row, b.name, _working.Body == i, true, () => { var l = _working; l.Body = (byte)idx; SetWorking(l); _flavour.text = b.flavour; }, 220);
            }
            Section("Skin tone");
            var sw = Grid();
            for (int i = 0; i < _catalog.skinTones.Count; i++)
            {
                int idx = i;
                var s = _catalog.skinTones[i];
                bool owned = !s.fantasy || _catalog.IsOwned(UnlockSource.Letter, "skin." + i);
                SwatchButton(sw, s.color, s.name + (s.fantasy ? " (fantasy)" : ""), _working.Skin == i, owned, () => { var l = _working; l.Skin = (byte)idx; SetWorking(l); });
            }
            _flavour.text = _catalog.Body(_working.Body).flavour;
        }

        void ColoursTab()
        {
            PaletteSection("Overalls", _working.Primary, v => { var l = _working; l.Primary = v; SetWorking(l); });
            PaletteSection("Shirt", _working.Secondary, v => { var l = _working; l.Secondary = v; SetWorking(l); });
            PaletteSection("Boots & gloves", _working.Accent, v => { var l = _working; l.Accent = v; SetWorking(l); });
            _flavour.text = $"Overalls: {_catalog.palette[Mathf.Min(_working.Primary, _catalog.palette.Count - 1)].name}. Factory default. Smells like the store.";
        }

        void PaletteSection(string title, byte selected, Action<byte> set)
        {
            Section(title + " — " + _catalog.palette[Mathf.Min(selected, _catalog.palette.Count - 1)].name);
            var g = Grid();
            for (int i = 0; i < _catalog.palette.Count; i++)
            {
                byte idx = (byte)i;
                SwatchButton(g, _catalog.palette[i].color, _catalog.palette[i].name, selected == i, true, () => set(idx), 40);
            }
        }

        void ItemTab(CosmeticSlot slot, string noneName, string noneFlavour)
        {
            Section(slot + " slot");
            var g = Grid();
            ushort current = _working.GetItem(slot);
            Card(g, noneName, current == 0, true, () => { var l = _working; l.SetItem(slot, 0); SetWorking(l); _flavour.text = noneFlavour; });
            string flavour = noneFlavour;
            foreach (var item in _catalog.Items(slot))
            {
                var it = item;
                bool owned = _catalog.IsOwned(it.unlock, it.key);
                if (current == it.id) flavour = it.flavour;
                Card(g, owned ? it.displayName : it.displayName + " (locked)", current == it.id, owned, () =>
                {
                    var l = _working; l.SetItem(slot, it.id); SetWorking(l); _flavour.text = it.flavour;
                });
            }
            _flavour.text = flavour;
        }

        void TagTab()
        {
            Section("Name tag colour (also your colour on the HUD and in the Blame Ledger)");
            var g = Grid();
            for (int i = 0; i < _catalog.nameTags.Count; i++)
            {
                int idx = i;
                var t = _catalog.nameTags[i];
                var card = Card(g, t.name + " · " + t.shape, _working.NameTag == i, true, () => { var l = _working; l.NameTag = (byte)idx; SetWorking(l); }, 230);
                card.style.borderLeftColor = t.color; card.style.borderLeftWidth = 14;
            }
            _flavour.text = "Duplicate colours go to whoever joined first. Shapes keep you readable for colour-blind friends.";
        }

        void EmoteTab()
        {
            Section("Emotes on keys 5-8");
            for (int k = 0; k < 4; k++)
            {
                int slot = k;
                var row = Box(_content, flexRow: true);
                row.style.alignItems = Align.Center;
                row.style.marginBottom = 6;
                var key = Text(row, $"Key {k + 5}", 22, Ink);
                key.style.width = 90;
                var def = _catalog.FindEmote(_working.GetEmote(k));
                MakeButton(row, "<", () => CycleEmote(slot, -1), 22);
                var name = Text(row, def != null ? def.displayName : "-", 22, Ink);
                name.style.width = 160; name.style.unityTextAlign = TextAnchor.MiddleCenter;
                MakeButton(row, ">", () => CycleEmote(slot, +1), 22);
            }
            _flavour.text = "Wave doubles as a shout. Faint drops whatever you're carrying. Choose wisely.";
        }

        void CycleEmote(int slot, int dir)
        {
            var owned = new List<EmoteDefinition>();
            foreach (var e in _catalog.emotes) if (_catalog.IsOwned(e.unlock, "emote." + e.key)) owned.Add(e);
            if (owned.Count == 0) return;
            int cur = owned.FindIndex(e => e.id == _working.GetEmote(slot));
            int next = ((cur < 0 ? 0 : cur + dir) % owned.Count + owned.Count) % owned.Count;
            var l = _working; l.SetEmote(slot, owned[next].id); SetWorking(l);
        }


        // ---- Widgets ------------------------------------------------------------------------------------------------

        void Section(string title)
        {
            var l = Text(_content, title, 22, Ink);
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.marginTop = 8; l.style.marginBottom = 4;
        }

        VisualElement Grid()
        {
            var g = Box(_content, flexRow: true);
            g.style.flexWrap = Wrap.Wrap;
            return g;
        }

        VisualElement Card(VisualElement parent, string label, bool selected, bool enabled, Action onClick, float width = 205)
        {
            var b = MakeButton(parent, label, enabled ? onClick : null, 20, selected ? Gold : Cream);
            b.style.width = width; b.style.height = 56;
            b.SetEnabled(enabled);
            if (selected) SetBorder(b, 4, Ink);
            return b;
        }

        void SwatchButton(VisualElement parent, Color c, string tooltip, bool selected, bool enabled, Action onClick, float size = 48)
        {
            var b = new Button(onClick) { tooltip = tooltip, text = "" };
            b.style.width = size; b.style.height = size;
            b.style.backgroundColor = c;
            b.style.marginRight = 6; b.style.marginBottom = 6;
            SetRadius(b, size * 0.5f);
            SetBorder(b, selected ? 5 : 2, selected ? Ink : WoodDark);
            b.SetEnabled(enabled);
            parent.Add(b);
        }

        static Button MakeButton(VisualElement parent, string label, Action onClick, int fontSize, Color? bg = null)
        {
            var b = onClick != null ? new Button(onClick) : new Button();
            b.text = label;
            b.style.fontSize = fontSize;
            b.style.color = Ink;
            b.style.backgroundColor = bg ?? Cream;
            b.style.unityFontStyleAndWeight = FontStyle.Bold;
            b.style.paddingLeft = b.style.paddingRight = 14;
            b.style.paddingTop = b.style.paddingBottom = 6;
            b.style.marginRight = 8; b.style.marginBottom = 8;
            SetRadius(b, 10);
            SetBorder(b, 2, WoodDark);
            parent.Add(b);
            return b;
        }

        static VisualElement Panel(VisualElement parent, float width)
        {
            var p = Box(parent, flexRow: false);
            if (width > 0) p.style.width = width;
            p.style.backgroundColor = new Color(0.93f, 0.85f, 0.68f, 0.97f);
            p.style.paddingLeft = p.style.paddingRight = 20;
            p.style.paddingTop = p.style.paddingBottom = 16;
            SetRadius(p, 18);
            SetBorder(p, 6, Wood);
            return p;
        }

        static VisualElement Box(VisualElement parent, bool flexRow)
        {
            var v = new VisualElement();
            v.style.flexDirection = flexRow ? FlexDirection.Row : FlexDirection.Column;
            parent.Add(v);
            return v;
        }

        static Label Text(VisualElement parent, string text, int size, Color color)
        {
            var l = new Label(text);
            l.style.fontSize = size;
            l.style.color = color;
            parent.Add(l);
            return l;
        }

        static void Title(VisualElement parent, string text, int size)
        {
            var l = Text(parent, text, size, Wood);
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
        }

        static void SetRadius(VisualElement v, float r)
        {
            v.style.borderTopLeftRadius = v.style.borderTopRightRadius = v.style.borderBottomLeftRadius = v.style.borderBottomRightRadius = r;
        }

        static void SetBorder(VisualElement v, float w, Color c)
        {
            v.style.borderTopWidth = v.style.borderBottomWidth = v.style.borderLeftWidth = v.style.borderRightWidth = w;
            v.style.borderTopColor = v.style.borderBottomColor = v.style.borderLeftColor = v.style.borderRightColor = c;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Current = null; Closed = null; }
    }
}
