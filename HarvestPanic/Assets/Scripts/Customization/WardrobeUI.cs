using System;
using System.Collections.Generic;
using HarvestPanic.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace HarvestPanic.Customization
{
    /// <summary>Tabs of the character creator, in display order.</summary>
    public enum CreatorTab { Body, Skin, Head, Hair, Outfit, Hat, Face, Back, Colours, Name, Emotes }

    /// <summary>
    /// "Create your farmer" / Grandpa's Wardrobe (design/gdd/character-customization.md "Wardrobe", master GDD 13.7), UI Toolkit.
    /// One screen for three contexts (<see cref="CharacterCreatorContext"/>): first run from the main menu, the main menu
    /// Customize button, and the lobby/barn wardrobe for the spawned farmer. Layout: a taped-in polaroid 3D turntable
    /// (<see cref="WardrobePreview"/>: drag to spin, wheel to zoom, idle + emote preview) and a ledger page with sticky-note
    /// tabs (Body, Skin, Head, Hair, Outfit, Hat, Face, Back, Colours, Name, Emotes). Item cards show thumbnails of the item on
    /// your farmer (<see cref="CosmeticIconRenderer"/>); locked items show a padlock and their unlock hint.
    /// Randomize (weighted + themed), Revert, Cancel/Decide later, Confirm. Changes apply only on Confirm.
    /// Keys: Q/E or LB/RB tabs, R or Y randomize, Esc or B cancel, Start confirm, right stick spins the turntable.
    /// The world keeps running; in the wardrobe the screen closes if the farmer faints/ragdolls.
    /// </summary>
    public sealed partial class WardrobeUI : MonoBehaviour
    {
        /// <summary>Open screen, or null.</summary>
        public static WardrobeUI Current { get; private set; }

        /// <summary>True while a creator screen is open.</summary>
        public static bool IsOpen => Current != null;

        /// <summary>Frame the last screen closed on (so Esc is not handled twice that frame).</summary>
        public static int ClosedFrame { get; private set; } = -1;

        /// <summary>Raised when the screen closes (true = confirmed).</summary>
        public static event Action<bool> Closed;

        FarmerLoadoutSync _target;
        CosmeticCatalog _catalog;
        CharacterCreatorContext _context;
        FarmerLoadout _original, _working;
        string _originalName = "", _name = "";
        int _fallbackN;
        CreatorTab _tab;
        WardrobePreview _preview;
        CosmeticIconRenderer _icons;
        UIDocument _doc;
        PanelSettings _ownedPanel;
        CursorLockMode _prevLock;
        bool _prevVisible;
        readonly System.Random _rng = new System.Random();
        bool _dragging, _built;
        int _emoteSlot;
        float _toastLeft, _wobbleLeft;

        /// <summary>Working (unconfirmed) loadout.</summary>
        public FarmerLoadout Working => _working;

        /// <summary>The loadout the screen opened with.</summary>
        public FarmerLoadout Original => _original;

        /// <summary>The preview rig.</summary>
        public WardrobePreview Preview => _preview;

        /// <summary>Thumbnail renderer.</summary>
        public CosmeticIconRenderer Icons => _icons;

        /// <summary>Where the screen was opened from.</summary>
        public CharacterCreatorContext Context => _context;

        /// <summary>Tab on show.</summary>
        public CreatorTab CurrentTab => _tab;

        /// <summary>Root visual element of the screen (tests, captures).</summary>
        public VisualElement Root { get; private set; }

        /// <summary>Farm Name being edited (unfiltered).</summary>
        public string FarmName => _name;

        /// <summary>Catalogue in use.</summary>
        public CosmeticCatalog Catalog => _catalog;

        // ---- Open / close ---------------------------------------------------------------------------------------------

        /// <summary>Opens the wardrobe for <paramref name="target"/> (normally <see cref="FarmerLoadoutSync.Local"/>).</summary>
        public static WardrobeUI Open(FarmerLoadoutSync target, PanelSettings panelOverride = null) =>
            Open(target, CharacterCreatorContext.Wardrobe, panelOverride);

        /// <summary>Opens the screen for a spawned (or offline-local) farmer; Confirm sends through <see cref="FarmerLoadoutSync.RequestLoadout"/>.</summary>
        public static WardrobeUI Open(FarmerLoadoutSync target, CharacterCreatorContext context, PanelSettings panelOverride = null)
        {
            if (target == null || target.Visual.Catalog == null) return null;
            return OpenInternal(target, target.Visual.Catalog, context, target.Loadout, panelOverride);
        }

        /// <summary>Opens the screen without a farmer (main menu): edits and saves the stored loadout.</summary>
        public static WardrobeUI OpenStandalone(CosmeticCatalog catalog, CharacterCreatorContext context, PanelSettings panelOverride = null)
        {
            if (catalog == null) return null;
            return OpenInternal(null, catalog, context, LoadoutStorage.LoadOrCreate(catalog), panelOverride);
        }

        static WardrobeUI OpenInternal(FarmerLoadoutSync target, CosmeticCatalog cat, CharacterCreatorContext context, FarmerLoadout start, PanelSettings panelOverride)
        {
            if (Current != null) return Current;
            PanelSettings panel = panelOverride;
            PanelSettings owned = null;
            if (panel == null)
            {
                if (cat.wardrobePanel == null) { Debug.LogWarning("[Wardrobe] Catalog has no PanelSettings"); return null; }
                owned = panel = Instantiate(cat.wardrobePanel);
                panel.name = "HP_CreatorPanel";
                float k = Mathf.Clamp(UiAccessibility.UiScale, 0.8f, 1.5f);
                panel.referenceResolution = new Vector2Int(Mathf.RoundToInt(1920f / k), Mathf.RoundToInt(1080f / k));
                panel.sortingOrder = 200;
            }

            var go = new GameObject("WardrobeUI");
            go.SetActive(false);
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = panel;
            doc.sortingOrder = 200;
            var ui = go.AddComponent<WardrobeUI>();
            ui._doc = doc;
            ui._ownedPanel = owned;
            ui._target = target;
            ui._catalog = cat;
            ui._context = context;
            ui._original = ui._working = start;
            ui._originalName = ui._name = PlayerNames.Read();
            ui._fallbackN = 1 + ui._rng.Next(99);
            if (string.IsNullOrEmpty(ui._name) && context == CharacterCreatorContext.FirstRun) ui._name = LoadoutRandomizer.RandomFarmName(ui._rng);
            ui._tab = context == CharacterCreatorContext.Wardrobe ? CreatorTab.Hat : CreatorTab.Body;
            Current = ui;
            go.SetActive(true);
            return ui;
        }

        void OnEnable()
        {
            if (_doc == null || _built) return;
            _built = true;
            _prevLock = UnityEngine.Cursor.lockState; _prevVisible = UnityEngine.Cursor.visible;
            UnityEngine.Cursor.lockState = CursorLockMode.None; UnityEngine.Cursor.visible = true;
            FarmerEmotes.InputBlocked = true;
            InputModal.Push(this); // pauses player move/look/interact while the screen is open
            _preview = WardrobePreview.Create(_catalog);
            _preview?.Apply(_working);
            _icons = CosmeticIconRenderer.Create(_catalog);
            _icons.Rendered += OnIconsRendered;
            UiAccessibility.Changed += OnAccessibilityChanged;
            BuildLayout();
            ShowTab(_tab);
            FocusSelectedLater(); // keyboard / gamepad users start on the equipped item
        }

        void OnAccessibilityChanged()
        {
            if (Root == null) return;
            CreatorTheme.ApplyRootClasses(Root);
            ApplyReservedColours();
            ShowTab(_tab);
        }

        void Update()
        {
            if (Current != this) return;
            float dt = Time.unscaledDeltaTime;
            if (_toastLeft > 0f && (_toastLeft -= dt) <= 0f) _toast?.RemoveFromClassList("cc-toast--shown");
            if (_wobbleLeft > 0f && (_wobbleLeft -= dt) <= 0f) _btnRandom?.RemoveFromClassList("cc-btn--wobble");
            _icons?.Pump(4);
            HandleInput();
            if (Current != this) return;
            if (_context == CharacterCreatorContext.Wardrobe)
            {
                var anim = _target != null ? _target.Visual.BodyAnimator : null;
                if (_target == null || (anim != null && anim.isActiveAndEnabled && anim.GetBool(FarmerVisual.FaintedParam))) Close(false);
            }
        }

        bool Typing => Root?.panel?.focusController?.focusedElement is TextField || _nameField != null && _nameField.panel != null && IsChildFocused(_nameField);

        bool IsChildFocused(VisualElement e)
        {
            var f = Root?.panel?.focusController?.focusedElement as VisualElement;
            while (f != null) { if (f == e) return true; f = f.parent; }
            return false;
        }

        void HandleInput()
        {
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            bool typing = Typing;
            bool esc = kb != null && kb.escapeKey.wasPressedThisFrame || pad != null && pad.buttonEast.wasPressedThisFrame;
            if (esc)
            {
                if (typing) { Root.panel.focusController.focusedElement?.Blur(); FocusTab(); return; }
                Close(false);
                return;
            }
            if (pad != null && pad.startButton.wasPressedThisFrame) { Close(true); return; }
            if (typing) return;
            if (kb != null && kb.qKey.wasPressedThisFrame || pad != null && pad.leftShoulder.wasPressedThisFrame) StepTab(-1);
            if (kb != null && kb.eKey.wasPressedThisFrame || pad != null && pad.rightShoulder.wasPressedThisFrame) StepTab(+1);
            if (kb != null && kb.rKey.wasPressedThisFrame || pad != null && pad.buttonNorth.wasPressedThisFrame) Randomize();
            if (pad != null && pad.buttonWest.wasPressedThisFrame) Revert();
            if (pad != null && _preview != null)
            {
                var rs = pad.rightStick.ReadValue();
                if (rs.sqrMagnitude > 0.04f) { _preview.Orbit(-rs.x * 160f * Time.unscaledDeltaTime, 0f); _preview.Zoom(-rs.y * 1.5f * Time.unscaledDeltaTime); }
            }
        }

        /// <summary>Closes the screen; <paramref name="confirm"/> saves/sends the working loadout and the Farm Name.</summary>
        public void Close(bool confirm)
        {
            if (Current != this) return;
            if (confirm) Commit();
            Current = null;
            ClosedFrame = Time.frameCount;
            FarmerEmotes.InputBlocked = false;
            InputModal.Pop(this);
            UnityEngine.Cursor.lockState = _prevLock; UnityEngine.Cursor.visible = _prevVisible;
            Teardown();
            Destroy(gameObject);
            Closed?.Invoke(confirm);
        }

        void Commit()
        {
            string resolved = NameFilter.Resolve(_name, _fallbackN);
            if (!string.Equals(resolved, _originalName, StringComparison.Ordinal)) PlayerNames.Write(resolved);
            if (_target != null) _target.RequestLoadout(_working);
            else
            {
                LoadoutStorage.Save(_working);
                var local = FarmerLoadoutSync.Local;
                if (local != null) local.RequestLoadout(_working);
            }
            LoadoutStorage.FarmerCreated = true;
            if (_target != null) _target.RefreshName();
        }

        void Teardown()
        {
            UiAccessibility.Changed -= OnAccessibilityChanged;
            if (_icons != null) { _icons.Rendered -= OnIconsRendered; Destroy(_icons.gameObject); _icons = null; }
            if (_preview != null) { Destroy(_preview.gameObject); _preview = null; }
        }

        void OnDestroy()
        {
            InputModal.Pop(this); // safety: never leave input held if destroyed without Close
            if (Current == this) { Current = null; ClosedFrame = Time.frameCount; FarmerEmotes.InputBlocked = false; }
            Teardown();
            if (_ownedPanel != null) Destroy(_ownedPanel);
        }

        // ---- Editing API (also used by tests) ---------------------------------------------------------------------------

        /// <summary>Randomizes the working loadout (weighted, sometimes themed). Keeps name tag and emotes.</summary>
        public void SurpriseMe() => Randomize();

        /// <summary>Randomizes the working loadout with feedback (hop, hay puff, theme toast).</summary>
        public void Randomize()
        {
            SetWorking(LoadoutStorage.Randomize(_catalog, _rng, _working));
            _preview?.Hop();
            if (_btnRandom != null && !UiAccessibility.ReducedMotion) { _btnRandom.AddToClassList("cc-btn--wobble"); _wobbleLeft = 0.18f; }
            if (LoadoutRandomizer.LastTheme != null) Toast("Themed farmer: " + LoadoutRandomizer.LastTheme + "!");
        }

        /// <summary>Restores the loadout and name the screen opened with.</summary>
        public void Revert()
        {
            _name = _originalName;
            if (_nameField != null) _nameField.SetValueWithoutNotify(_name);
            SetWorking(_original);
            Toast("Back to how you walked in.");
        }

        /// <summary>Sets the working loadout and refreshes preview + current tab.</summary>
        public void SetWorking(FarmerLoadout l)
        {
            _working = l;
            _preview?.Apply(_working);
            RefreshCaption();
            if (Root != null) ShowTab(_tab, keepFocus: true);
        }

        /// <summary>True when the local player may not pick <paramref name="id"/> in <paramref name="slot"/>.</summary>
        public bool IsLocked(CosmeticSlot slot, ushort id) => id != 0 && !_catalog.IsOwnedLocally(_catalog.FindItem(slot, id));

        /// <summary>Equips <paramref name="id"/> (0 = None/base look). Returns false for locked or unknown items.</summary>
        public bool Select(CosmeticSlot slot, ushort id)
        {
            if (id != 0 && _catalog.FindItem(slot, id) == null) return false;
            if (IsLocked(slot, id)) { Toast("Locked: " + CosmeticCatalog.UnlockHint(_catalog.FindItem(slot, id).unlock, _catalog.FindItem(slot, id).unlockHint)); return false; }
            var l = _working; l.SetItem(slot, id);
            _working = l;
            _preview?.Apply(_working);
            RefreshSelection();
            return true;
        }

        /// <summary>Sets the Farm Name being edited (filtered on Confirm).</summary>
        public void SetFarmName(string name)
        {
            _name = name ?? "";
            if (_nameField != null && _nameField.value != _name) _nameField.SetValueWithoutNotify(_name);
            RefreshNameStatus();
            RefreshCaption();
        }

        /// <summary>Name that would be stored on Confirm (after the GDD filter).</summary>
        public string ResolvedFarmName => NameFilter.Resolve(_name, _fallbackN);

        /// <summary>Shows <paramref name="tab"/>.</summary>
        public void ShowTab(CreatorTab tab) => ShowTab(tab, false);

        void StepTab(int dir)
        {
            int n = Enum.GetValues(typeof(CreatorTab)).Length;
            ShowTab((CreatorTab)(((int)_tab + dir + n) % n));
            FocusSelectedLater();
        }

        void Toast(string text)
        {
            if (_toast == null) return;
            _toast.text = text;
            _toast.AddToClassList("cc-toast--shown");
            _toastLeft = 2.2f;
        }

        void OnIconsRendered()
        {
            foreach (var img in _thumbs) img.MarkDirtyRepaint();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Current = null; Closed = null; ClosedFrame = -1; }
    }

    /// <summary>Reads/writes the Farm Name through Frontend's <see cref="IPlayerNameService"/>, else the session platform, else PlayerPrefs.</summary>
    public static class PlayerNames
    {
        /// <summary>The saved Farm Name ("" when none).</summary>
        public static string Read()
        {
            if (Services.TryGet(out IPlayerNameService svc)) return svc.PlayerName ?? "";
            string local = PlayerPrefs.GetString(LoadoutStorage.NameKey, "");
            if (!string.IsNullOrEmpty(local)) return local;
            if (Services.TryGet(out ISessionPlatform plat))
            {
                string n = plat.LocalPlayerName;
                return string.IsNullOrEmpty(n) || n == "Farmer" ? "" : n;
            }
            return "";
        }

        /// <summary>Saves a (filtered) Farm Name everywhere it is used.</summary>
        public static void Write(string name)
        {
            name = name ?? "";
            if (Services.TryGet(out IPlayerNameService svc)) svc.SetPlayerName(name);
            else if (Services.TryGet(out ISessionPlatform plat) && name.Length > 0) plat.SetLocalPlayerName(name);
            PlayerPrefs.SetString(LoadoutStorage.NameKey, name);
            PlayerPrefs.Save();
        }
    }
}
