using System;
using System.Collections.Generic;
using HarvestPanic.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace HarvestPanic.Customization
{
    public sealed partial class WardrobeUI
    {
        static readonly string[] TabNames = { "Body", "Skin", "Head", "Hair", "Outfit", "Hat", "Face", "Back", "Colours", "Name", "Emotes" };

        VisualElement _content, _tabBar;
        Label _sectionTitle, _sectionNote, _count, _infoName, _infoFlavour, _infoHint, _captionName, _captionSub, _toast, _nameStatus;
        VisualElement _infoLock;
        CreatorIcon _infoLockIcon;
        Image _photo;
        Button _btnRandom, _btnConfirm, _btnCancel;
        ScrollView _scroll;
        TextField _nameField;
        Label _plaqueName, _photoHint;
        CreatorIcon _plaqueIcon;
        readonly List<Image> _thumbs = new List<Image>();
        readonly List<(VisualElement el, Func<bool> selected)> _selectables = new List<(VisualElement, Func<bool>)>();
        readonly List<Button> _tabButtons = new List<Button>();
        readonly List<VisualElement> _badges = new List<VisualElement>();
        readonly List<VisualElement> _locks = new List<VisualElement>();

        /// <summary>Number of tabs (tests).</summary>
        public int TabCount => _tabButtons.Count;

        /// <summary>Cards on the current tab (tests).</summary>
        public int VisibleCardCount => _selectables.Count;

        // ---- Layout ----------------------------------------------------------------------------------------------------

        void BuildLayout()
        {
            var host = _doc.rootVisualElement;
            host.Clear();
            host.style.flexGrow = 1;
            if (_catalog.creatorStyle != null) host.styleSheets.Add(_catalog.creatorStyle);

            Root = new VisualElement { name = "cc-root" };
            Root.AddToClassList("cc-root");
            Root.EnableInClassList("cc-root--wardrobe", _context == CharacterCreatorContext.Wardrobe);
            if (_context != CharacterCreatorContext.Wardrobe) Root.style.backgroundImage = new StyleBackground(CreatorTheme.Desk);
            host.Add(Root);
            CreatorTheme.ApplyRootClasses(Root);

            // Header
            var header = El(Root, "cc-header");
            Text(header, _context == CharacterCreatorContext.Wardrobe ? "GRANDPA'S WARDROBE" : "CREATE YOUR FARMER", "cc-title").name = "cc-title";
            Text(header, _context == CharacterCreatorContext.FirstRun ? "Grandpa left you overalls. The rest is up to you."
                : _context == CharacterCreatorContext.Wardrobe ? "Friends can still shove you. Choose fast." : "Same farmer, new look.", "cc-subtitle");

            var body = El(Root, "cc-body");

            // Left: polaroid turntable
            var left = El(body, "cc-left");
            var polaroidHost = new VisualElement();
            left.Add(polaroidHost);
            El(polaroidHost, "cc-polaroid-shadow");
            var polaroid = El(polaroidHost, "cc-polaroid");
            _photo = new Image { name = "cc-photo", image = _preview != null ? _preview.Texture : null, scaleMode = ScaleMode.ScaleAndCrop };
            _photo.AddToClassList("cc-photo");
            polaroid.Add(_photo);
            _photo.RegisterCallback<PointerDownEvent>(e => { _dragging = true; _photo.CapturePointer(e.pointerId); });
            _photo.RegisterCallback<PointerUpEvent>(e => { _dragging = false; _photo.ReleasePointer(e.pointerId); _preview?.Release(); });
            _photo.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!_dragging) return;
                _preview?.Orbit(-e.deltaPosition.x * 0.45f, e.deltaPosition.y * 0.15f);
                if (_photoHint != null) _photoHint.AddToClassList("cc-photo-hint--hidden"); // learnt it: get out of the way
            });
            _photo.RegisterCallback<WheelEvent>(e => { _preview?.Zoom(e.delta.y * 0.06f); e.StopPropagation(); });
            var tapeL = El(polaroid, "cc-tape"); tapeL.AddToClassList("cc-tape--l"); tapeL.style.backgroundImage = new StyleBackground(CreatorTheme.Tape);
            var tapeR = El(polaroid, "cc-tape"); tapeR.AddToClassList("cc-tape--r"); tapeR.style.backgroundImage = new StyleBackground(CreatorTheme.Tape);
            _photoHint = Text(polaroid, "Drag to spin  ·  Scroll to zoom", "cc-photo-hint");
            _photoHint.pickingMode = PickingMode.Ignore;
            var caption = El(polaroid, "cc-caption");
            _captionName = Text(caption, "", "cc-caption-name");
            _captionSub = Text(caption, "", "cc-caption-sub");

            var emoteBar = El(left, "cc-emote-bar");
            Text(emoteBar, "Try:", "cc-emote-bar-label");
            for (int k = 0; k < 4; k++)
            {
                int slot = k;
                var chip = Chip(emoteBar, "", CreatorGlyph.Play, () => PreviewEquippedEmote(slot));
                chip.name = "cc-try-" + k;
            }

            // Right: tabs + ledger page
            var right = El(body, "cc-right");
            _tabBar = El(right, "cc-tabs");
            for (int i = 0; i < TabNames.Length; i++)
            {
                var tab = (CreatorTab)i;
                var b = new Button(() => { ShowTab(tab); FocusSelectedLater(); }) { text = TabNames[i], name = "tab-" + tab };
                b.AddToClassList("cc-tab");
                b.style.backgroundColor = CreatorTheme.Sticky[i % CreatorTheme.Sticky.Length];
                _tabBar.Add(b);
                _tabButtons.Add(b);
            }
            var page = El(right, "cc-page");
            page.style.backgroundImage = new StyleBackground(CreatorTheme.LedgerPaper);
            var head = El(page, "cc-page-head");
            _sectionTitle = Text(head, "", "cc-section-title");
            _sectionNote = Text(head, "", "cc-section-note");
            El(head, "cc-spacer");
            _count = Text(head, "", "cc-count");
            _scroll = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            _scroll.AddToClassList("cc-scroll");
            page.Add(_scroll);
            _content = _scroll.contentContainer;
            var info = El(page, "cc-info");
            info.name = "cc-info";
            _infoName = Text(info, "", "cc-info-name");
            _infoFlavour = Text(info, "", "cc-info-flavour");
            _infoLock = El(info, "cc-info-lock");
            _infoLockIcon = new CreatorIcon(CreatorGlyph.Lock, CreatorTheme.Get(CreatorReserved.Landlord));
            _infoLock.Add(_infoLockIcon);
            _infoHint = Text(_infoLock, "", "cc-info-hint");

            // Footer
            var footer = El(Root, "cc-footer");
            _btnRandom = Btn(footer, "Randomize", CreatorGlyph.Dice, Randomize, "cc-btn--random", "R");
            _btnRandom.name = "btn-surprise";
            Btn(footer, "Revert", CreatorGlyph.Revert, Revert, null, null).name = "btn-revert";
            El(footer, "cc-spacer");
            Text(footer, "Q / E  flip tabs     Esc  " + (_context == CharacterCreatorContext.FirstRun ? "later" : "cancel"), "cc-keys");
            _btnCancel = Btn(footer, _context == CharacterCreatorContext.FirstRun ? "Decide later" : "Cancel", null, () => Close(false), null, null);
            _btnCancel.name = "btn-cancel";
            _btnConfirm = Btn(footer, _context == CharacterCreatorContext.FirstRun ? "Start farming!" : "Confirm", CreatorGlyph.Check, () => Close(true), "cc-btn--confirm", null);
            _btnConfirm.name = "btn-confirm";

            _toast = Text(Root, "", "cc-toast");
            _toast.pickingMode = PickingMode.Ignore;

            ApplyReservedColours();
            RefreshCaption();
            RefreshEmoteBar();
        }

        void ApplyReservedColours()
        {
            if (_btnConfirm == null) return;
            var good = CreatorTheme.Get(CreatorReserved.Good);
            _btnConfirm.style.backgroundColor = good;
            bool darkGood = good.r * 0.3f + good.g * 0.59f + good.b * 0.11f < 0.45f;
            _btnConfirm.style.color = darkGood ? CreatorTheme.Paper : CreatorTheme.Ink;
            foreach (var icon in _btnConfirm.Query<CreatorIcon>().ToList()) icon.Tint = darkGood ? CreatorTheme.Paper : CreatorTheme.Ink;
            if (_infoLockIcon != null) _infoLockIcon.Tint = CreatorTheme.Get(CreatorReserved.Landlord);
            if (_infoHint != null) _infoHint.style.color = CreatorTheme.Get(CreatorReserved.Landlord);
        }

        // ---- Tabs --------------------------------------------------------------------------------------------------------

        void ShowTab(CreatorTab tab, bool keepFocus)
        {
            _tab = tab;
            if (_content == null) return;
            float scroll = keepFocus ? _scroll.scrollOffset.y : 0f;
            string focusedName = keepFocus ? (Root.panel?.focusController?.focusedElement as VisualElement)?.name : null;
            _content.Clear();
            _thumbs.Clear();
            _selectables.Clear();
            _badges.Clear();
            _locks.Clear();
            _nameField = null;
            for (int i = 0; i < _tabButtons.Count; i++) _tabButtons[i].EnableInClassList("cc-tab--active", i == (int)tab);
            _sectionNote.text = "";
            _count.style.display = DisplayStyle.None;

            switch (tab)
            {
                case CreatorTab.Body: BodyTab(); break;
                case CreatorTab.Skin: SkinTab(); break;
                case CreatorTab.Head: ItemTab(CosmeticSlot.Head, "Head & face", "the noggin", PreviewFocus.Face); break;
                case CreatorTab.Hair: HairTab(); break;
                case CreatorTab.Outfit: ItemTab(CosmeticSlot.Outfit, "Outfit", "patterns tint with your colours", PreviewFocus.Outfit); break;
                case CreatorTab.Hat: ItemTab(CosmeticSlot.Hat, "Hats", "your silhouette from 40 m", PreviewFocus.Head); break;
                case CreatorTab.Face: ItemTab(CosmeticSlot.Face, "Face items", "", PreviewFocus.Face); break;
                case CreatorTab.Back: ItemTab(CosmeticSlot.Back, "Back items", "", PreviewFocus.Back); break;
                case CreatorTab.Colours: ColoursTab(); break;
                case CreatorTab.Name: NameTab(); break;
                case CreatorTab.Emotes: EmotesTab(); break;
            }
            if (!keepFocus) _preview?.SetFocus(FocusFor(tab));
            if (!keepFocus && !UiAccessibility.ReducedMotion)
            {
                foreach (var g in _content.Children()) g.AddToClassList("cc-grid--enter");
                _content.schedule.Execute(() => { foreach (var g in _content.Children()) g.RemoveFromClassList("cc-grid--enter"); }).ExecuteLater(16);
            }
            if (keepFocus)
            {
                _scroll.schedule.Execute(() => _scroll.scrollOffset = new Vector2(0f, scroll)).ExecuteLater(1);
                if (!string.IsNullOrEmpty(focusedName)) _content.schedule.Execute(() => Root.Q(focusedName)?.Focus()).ExecuteLater(1);
            }
            else _scroll.scrollOffset = Vector2.zero;
            ShowSelectedInfo();
            RefreshEmoteBar();
        }

        static PreviewFocus FocusFor(CreatorTab t)
        {
            switch (t)
            {
                case CreatorTab.Head: case CreatorTab.Face: return PreviewFocus.Face;
                case CreatorTab.Hair: case CreatorTab.Hat: case CreatorTab.Skin: return PreviewFocus.Head;
                case CreatorTab.Back: return PreviewFocus.Back;
                case CreatorTab.Outfit: return PreviewFocus.Outfit;
                default: return PreviewFocus.FullBody;
            }
        }

        void BodyTab()
        {
            _sectionTitle.text = "Body type";
            _sectionNote.text = "same size hitbox, very different energy";
            var grid = Grid();
            for (int i = 0; i < _catalog.bodies.Count; i++)
            {
                int idx = i;
                var b = _catalog.bodies[i];
                var look = _working; look.Body = (byte)i;
                var card = Card(grid, "body-" + i, b.name, b.icon != null ? (Texture)b.icon : Thumb(look, PreviewFocus.FullBody), false, () => _working.Body == idx,
                    () => { var l = _working; l.Body = (byte)idx; _working = l; _preview?.Apply(_working); RefreshSelection(); },
                    () => ShowInfo(b.name, b.flavour, null), wide: true);
            }
        }

        void SkinTab()
        {
            _sectionTitle.text = "Skin tone";
            _sectionNote.text = "every base tone is free, forever";
            Sub("Tones");
            var row = Row();
            for (int i = 0; i < _catalog.skinTones.Count; i++)
            {
                if (_catalog.skinTones[i].fantasy) continue;
                SkinSwatch(row, i);
            }
            Sub("Fantasy tones", "earned from Grandpa's letters");
            var row2 = Row();
            for (int i = 0; i < _catalog.skinTones.Count; i++) if (_catalog.skinTones[i].fantasy) SkinSwatch(row2, i);
        }

        void SkinSwatch(VisualElement row, int i)
        {
            int idx = i;
            var s = _catalog.skinTones[i];
            bool owned = _catalog.IsSkinOwnedLocally(i);
            Swatch(row, "skin-" + i, s.color, s.name, true, owned, () => _working.Skin == idx, () =>
            {
                if (!owned) { Toast("Locked: a letter from Grandpa"); return; }
                var l = _working; l.Skin = (byte)idx; _working = l; _preview?.Apply(_working); RefreshSelection();
            }, () => ShowInfo(s.name, s.fantasy ? "A fantasy tone. Grandpa would have questions." : "A fine farmer's tone.", owned ? null : "A letter from Grandpa (achievement)"));
        }

        void HairTab()
        {
            Sub("Hair colour", "brows and beards follow it too");
            var row = Row();
            row.AddToClassList("cc-swatch-row--compact");
            for (int i = 0; i < _catalog.palette.Count; i++)
            {
                byte idx = (byte)i;
                var sw = _catalog.palette[i];
                Swatch(row, "haircol-" + i, sw.color, sw.name, false, true, () => _working.HairColor == idx,
                    () => { var l = _working; l.HairColor = idx; _working = l; _preview?.Apply(_working); ShowTab(CreatorTab.Hair, keepFocus: true); },
                    () => ShowInfo(sw.name, "Hair colour. Dye costs nothing; dignity costs extra.", null));
            }
            ItemTab(CosmeticSlot.Hair, "Hair", "hats squash big hairdos", PreviewFocus.Head, clear: false);
        }

        void ItemTab(CosmeticSlot slot, string title, string note, PreviewFocus framing, bool clear = true)
        {
            _sectionTitle.text = title;
            _sectionNote.text = note;
            var items = _catalog.Items(slot);
            int owned = 0;
            foreach (var it in items) if (_catalog.IsOwnedLocally(it)) owned++;
            _count.text = owned + " / " + items.Count + " unlocked";
            _count.style.display = DisplayStyle.Flex;
            var grid = Grid();

            string baseName = CosmeticCatalog.BaseLookName(slot), baseFlavour = CosmeticCatalog.BaseLookFlavour(slot);
            Card(grid, "item-" + slot + "-0", baseName, Thumb(LookWith(slot, 0), framing), false, () => _working.GetItem(slot) == 0,
                () => Select(slot, 0), () => ShowInfo(baseName, baseFlavour, null));
            foreach (var item in items)
            {
                var it = item;
                bool locked = !_catalog.IsOwnedLocally(it);
                Texture tex = it.icon != null ? it.icon : Thumb(LookWith(slot, it.id), framing);
                Card(grid, "item-" + slot + "-" + it.id, it.displayName, tex, locked, () => _working.GetItem(slot) == it.id,
                    () => Select(slot, it.id),
                    () => ShowInfo(it.displayName, it.flavour, locked ? CosmeticCatalog.UnlockHint(it.unlock, it.unlockHint) : null));
            }
        }

        /// <summary>The working loadout with <paramref name="id"/> in <paramref name="slot"/>, tidied for a clear thumbnail.</summary>
        FarmerLoadout LookWith(CosmeticSlot slot, ushort id)
        {
            var l = _working;
            l.SetItem(slot, id);
            if (slot == CosmeticSlot.Hair || slot == CosmeticSlot.Head || slot == CosmeticSlot.Face) l.Hat = 0;
            if (slot == CosmeticSlot.Hair || slot == CosmeticSlot.Head) l.Face = 0;
            return l;
        }

        void ColoursTab()
        {
            _sectionTitle.text = "Colours";
            _sectionNote.text = "overalls are your team colour from across the field";
            PaletteRow("Overalls", "col-primary", () => _working.Primary, v => { var l = _working; l.Primary = v; return l; });
            PaletteRow("Shirt", "col-secondary", () => _working.Secondary, v => { var l = _working; l.Secondary = v; return l; });
            PaletteRow("Boots & gloves", "col-accent", () => _working.Accent, v => { var l = _working; l.Accent = v; return l; });
        }

        void PaletteRow(string title, string id, Func<byte> get, Func<byte, FarmerLoadout> set)
        {
            Sub(title + "  -  " + _catalog.PaletteName(get()));
            var row = Row();
            for (int i = 0; i < _catalog.palette.Count; i++)
            {
                byte idx = (byte)i;
                var sw = _catalog.palette[i];
                Swatch(row, id + "-" + i, sw.color, sw.name, false, true, () => get() == idx,
                    () => { _working = set(idx); _preview?.Apply(_working); RefreshSelection(); },
                    () => ShowInfo(sw.name, title + " colour.", null));
            }
        }

        void NameTab()
        {
            _sectionTitle.text = "Name & tag";
            _sectionNote.text = "3-16 letters, digits, space, - or _";
            Sub("Farm Name", "leave it empty to use your Steam name");
            var row = Row();
            row.AddToClassList("cc-name-row");
            _nameField = new TextField { name = "cc-name-field", maxLength = NameFilter.MaxLength + 4, value = _name };
            _nameField.AddToClassList("cc-name-field");
            _nameField.RegisterValueChangedCallback(e => { _name = e.newValue ?? ""; RefreshNameStatus(); RefreshCaption(); });
            row.Add(_nameField);
            Chip(row, "Roll a name", CreatorGlyph.Dice, () => SetFarmName(LoadoutRandomizer.RandomFarmName(_rng))).name = "btn-roll-name";
            _nameStatus = Text(_content, "", "cc-name-status");
            _nameStatus.name = "cc-name-status";

            var plaque = El(_content, "cc-plaque");
            var style = _catalog.NameTag(_working.NameTag);
            _plaqueIcon = new CreatorIcon(CreatorGlyph.Shape, style.color, style.shape);
            _plaqueIcon.SetOutline(CreatorTheme.Ink, 3f);
            plaque.Add(_plaqueIcon);
            _plaqueName = Text(plaque, "", "cc-plaque-name");

            Sub("Name-tag colour", "also your colour on the HUD and in the Blame Ledger");
            var tags = Row();
            for (int i = 0; i < _catalog.nameTags.Count; i++)
            {
                byte idx = (byte)i;
                var t = _catalog.nameTags[i];
                var b = new Button { name = "tag-" + i };
                b.AddToClassList("cc-tagcard");
                var icon = new CreatorIcon(CreatorGlyph.Shape, t.color, t.shape);
                icon.SetOutline(CreatorTheme.Ink, 2.5f);
                b.Add(icon);
                var col = new VisualElement(); col.AddToClassList("cc-tagcard-text"); col.pickingMode = PickingMode.Ignore;
                var lbl = new Label(t.name); lbl.AddToClassList("cc-tagcard-name"); col.Add(lbl);
                var shp = new Label(t.shape.ToString()); shp.AddToClassList("cc-tagcard-shape"); col.Add(shp);
                b.Add(col);
                AddBadge(b);
                b.clicked += () => { var l = _working; l.NameTag = idx; _working = l; _preview?.Apply(_working); RefreshSelection(); RefreshNameStatus(); };
                Hover(b, () => ShowInfo(t.name + " " + t.shape, "Shapes keep you readable for colour-blind friends. Duplicates go to whoever joined first.", null));
                tags.Add(b);
                _selectables.Add((b, () => _working.NameTag == idx));
            }
            RefreshNameStatus();
            RefreshSelection();
        }

        void RefreshNameStatus()
        {
            if (_plaqueName != null)
            {
                var style = _catalog.NameTag(_working.NameTag);
                string shown = ResolvedFarmName;
                _plaqueName.text = string.IsNullOrEmpty(shown) ? "Farmer" : shown;
                _plaqueName.style.color = style.color;
                if (_plaqueIcon != null) { _plaqueIcon.Tint = style.color; _plaqueIcon.TagShape = style.shape; }
            }
            if (_nameStatus == null) return;
            var r = NameFilter.Check(_name, out string cleaned);
            string msg;
            bool ok = r == NameCheck.Ok || r == NameCheck.Empty;
            switch (r)
            {
                case NameCheck.Ok: msg = "Grandpa writes \"" + cleaned + "\" in the ledger."; break;
                case NameCheck.Empty: msg = "No Farm Name: you'll go by your Steam name."; break;
                case NameCheck.TooShort: msg = "Too short: at least " + NameFilter.MinLength + " characters. Otherwise you'll be " + NameFilter.Fallback(_fallbackN) + "."; break;
                case NameCheck.TooLong: msg = "Too long: " + NameFilter.MaxLength + " characters max. Grandpa's ledger is narrow."; break;
                case NameCheck.BadCharacters: msg = "Letters, digits, space, - and _ only. Otherwise you'll be " + NameFilter.Fallback(_fallbackN) + "."; break;
                default: msg = "Grandpa won't write that down. You'll be " + NameFilter.Fallback(_fallbackN) + "."; break;
            }
            _nameStatus.text = msg;
            _nameStatus.style.color = ok ? CreatorTheme.Ink : CreatorTheme.Get(CreatorReserved.Loss);
        }

        /// <summary>Current Name-tab validation message (tests).</summary>
        public string NameStatusText => _nameStatus != null ? _nameStatus.text : "";

        void EmotesTab()
        {
            _sectionTitle.text = "Emotes";
            _sectionNote.text = "keys 5-8; pick a slot, then an emote";
            var slots = El(_content, "cc-slots");
            for (int k = 0; k < 4; k++)
            {
                int slot = k;
                var def = _catalog.FindEmote(_working.GetEmote(k));
                var b = new Button(() => { _emoteSlot = slot; RefreshSelection(); if (def != null) _preview?.PlayEmote(def.key); }) { name = "slot-" + k };
                b.AddToClassList("cc-slot");
                var key = new Label((k + 5).ToString()); key.AddToClassList("cc-keycap"); b.Add(key);
                var nm = new Label(def != null ? def.displayName : "-"); nm.AddToClassList("cc-slot-name"); b.Add(nm);
                slots.Add(b);
                _selectables.Add((b, () => _emoteSlot == slot));
                Hover(b, () => ShowInfo("Key " + (slot + 5), def != null ? def.displayName + ": " + def.flavour : "Empty", null));
            }
            int owned = 0;
            foreach (var e in _catalog.emotes) if (_catalog.IsOwnedLocally(e)) owned++;
            _count.text = owned + " / " + _catalog.emotes.Count + " unlocked";
            _count.style.display = DisplayStyle.Flex;
            var grid = Grid();
            foreach (var emote in _catalog.emotes)
            {
                var e = emote;
                bool locked = !_catalog.IsOwnedLocally(e);
                var b = new Button { name = "emote-" + e.key };
                b.AddToClassList("cc-card"); b.AddToClassList("cc-card--emote");
                b.EnableInClassList("cc-card--locked", locked);
                b.Add(new CreatorIcon(CreatorGlyph.Play, CreatorTheme.Ink));
                var lbl = new Label(e.displayName); lbl.AddToClassList("cc-card-name"); b.Add(lbl);
                AddBadge(b);
                if (locked) AddLock(b);
                b.clicked += () =>
                {
                    if (locked) { Toast("Locked: " + CosmeticCatalog.UnlockHint(e.unlock, e.unlockHint)); return; }
                    var l = _working; l.SetEmote(_emoteSlot, e.id); _working = l;
                    _preview?.PlayEmote(e.key);
                    ShowTab(CreatorTab.Emotes, keepFocus: true);
                };
                Hover(b, () => ShowInfo(e.displayName, e.flavour, locked ? CosmeticCatalog.UnlockHint(e.unlock, e.unlockHint) : null));
                grid.Add(b);
                _selectables.Add((b, () => _working.GetEmote(_emoteSlot) == e.id));
            }
            RefreshSelection();
        }

        void PreviewEquippedEmote(int slot)
        {
            var def = _catalog.FindEmote(_working.GetEmote(slot));
            if (def != null) _preview?.PlayEmote(def.key);
        }

        void RefreshEmoteBar()
        {
            if (Root == null) return;
            for (int k = 0; k < 4; k++)
            {
                var chip = Root.Q<Button>("cc-try-" + k);
                if (chip == null) continue;
                var def = _catalog.FindEmote(_working.GetEmote(k));
                var lbl = chip.Q<Label>();
                if (lbl != null) lbl.text = def != null ? def.displayName : "-";
            }
        }

        // ---- Selection / info ------------------------------------------------------------------------------------------

        void RefreshSelection()
        {
            foreach (var (el, selected) in _selectables)
            {
                bool on = selected();
                if (el.ClassListContains("cc-card")) el.EnableInClassList("cc-card--selected", on);
                else if (el.ClassListContains("cc-swatch")) el.EnableInClassList("cc-swatch--selected", on);
                else if (el.ClassListContains("cc-tagcard")) el.EnableInClassList("cc-tagcard--selected", on);
                else if (el.ClassListContains("cc-slot")) el.EnableInClassList("cc-slot--active", on);
            }
            RefreshCaption();
            if (_tab == CreatorTab.Colours) RelabelPaletteHeads();
            ShowSelectedInfo();
        }

        void RelabelPaletteHeads()
        {
            var heads = _content.Query<Label>(className: "cc-subhead").ToList();
            string[] titles = { "Overalls", "Shirt", "Boots & gloves" };
            byte[] values = { _working.Primary, _working.Secondary, _working.Accent };
            for (int i = 0; i < heads.Count && i < 3; i++) heads[i].text = titles[i] + "  -  " + _catalog.PaletteName(values[i]);
        }

        void ShowSelectedInfo()
        {
            switch (_tab)
            {
                case CreatorTab.Body: { var b = _catalog.Body(_working.Body); ShowInfo(b.name, b.flavour, null); break; }
                case CreatorTab.Skin: { var i = Mathf.Clamp(_working.Skin, 0, _catalog.skinTones.Count - 1); ShowInfo(_catalog.skinTones.Count > 0 ? _catalog.skinTones[i].name : "", "Skin tone.", null); break; }
                case CreatorTab.Head: ItemInfo(CosmeticSlot.Head); break;
                case CreatorTab.Hair: ItemInfo(CosmeticSlot.Hair); break;
                case CreatorTab.Outfit: ItemInfo(CosmeticSlot.Outfit); break;
                case CreatorTab.Hat: ItemInfo(CosmeticSlot.Hat); break;
                case CreatorTab.Face: ItemInfo(CosmeticSlot.Face); break;
                case CreatorTab.Back: ItemInfo(CosmeticSlot.Back); break;
                case CreatorTab.Colours: ShowInfo("Overalls: " + _catalog.PaletteName(_working.Primary), "Shirt: " + _catalog.PaletteName(_working.Secondary) + ".  Boots & gloves: " + _catalog.PaletteName(_working.Accent) + ".", null); break;
                case CreatorTab.Name: { var t = _catalog.NameTag(_working.NameTag); ShowInfo(t.name + " " + t.shape, "Your colour and shape on name tags, pings, the HUD and the Blame Ledger.", null); break; }
                case CreatorTab.Emotes: { var d = _catalog.FindEmote(_working.GetEmote(_emoteSlot)); ShowInfo("Key " + (_emoteSlot + 5) + ": " + (d != null ? d.displayName : "-"), d != null ? d.flavour : "", null); break; }
            }
        }

        void ItemInfo(CosmeticSlot slot)
        {
            var it = _catalog.FindItem(slot, _working.GetItem(slot));
            if (it == null) ShowInfo(CosmeticCatalog.BaseLookName(slot), CosmeticCatalog.BaseLookFlavour(slot), null);
            else ShowInfo(it.displayName, it.flavour, null);
        }

        void ShowInfo(string name, string flavour, string lockHint)
        {
            if (_infoName == null) return;
            _infoName.text = name ?? "";
            _infoFlavour.text = flavour ?? "";
            bool locked = !string.IsNullOrEmpty(lockHint);
            _infoLock.style.display = locked ? DisplayStyle.Flex : DisplayStyle.None;
            _infoHint.text = locked ? "Locked - " + lockHint : "";
        }

        void RefreshCaption()
        {
            if (_captionName == null) return;
            string n = ResolvedFarmName;
            _captionName.text = string.IsNullOrEmpty(n) ? "Farmer" : n;
            var body = _catalog.Body(_working.Body);
            _captionSub.text = body.name + " farmer  ·  " + _catalog.PaletteName(_working.Primary) + " overalls";
        }

        void FocusTab() => _tabButtons[(int)_tab].Focus();

        void FocusSelectedLater()
        {
            _content?.schedule.Execute(() =>
            {
                foreach (var (el, selected) in _selectables) if (selected()) { el.Focus(); return; }
                if (_selectables.Count > 0) _selectables[0].el.Focus(); else FocusTab();
            }).ExecuteLater(1);
        }

        // ---- Widgets -----------------------------------------------------------------------------------------------------

        Texture Thumb(FarmerLoadout look, PreviewFocus framing) => _icons != null ? _icons.Get(look, framing) : null;

        Button Card(VisualElement parent, string id, string label, Texture thumb, bool locked, Func<bool> selected, Action onClick, Action onHover, bool wide = false)
        {
            var b = new Button { name = id };
            b.AddToClassList("cc-card");
            if (wide) b.AddToClassList("cc-card--wide");
            b.EnableInClassList("cc-card--locked", locked);
            var img = new Image { image = thumb, scaleMode = ScaleMode.ScaleAndCrop };
            img.AddToClassList("cc-card-thumb");
            if (thumb == null)
            {
                var ph = new Label(string.IsNullOrEmpty(label) ? "?" : label.Substring(0, 1)); ph.AddToClassList("cc-card-placeholder");
                img.Add(ph);
            }
            b.Add(img);
            _thumbs.Add(img);
            var name = new Label(label); name.AddToClassList("cc-card-name"); b.Add(name);
            AddBadge(b);
            if (locked) AddLock(b);
            b.clicked += onClick;
            Hover(b, onHover);
            parent.Add(b);
            _selectables.Add((b, selected));
            b.EnableInClassList("cc-card--selected", selected());
            return b;
        }

        void Swatch(VisualElement parent, string id, Color c, string label, bool big, bool owned, Func<bool> selected, Action onClick, Action onHover)
        {
            var b = new Button(onClick) { name = id, text = "" };
            b.AddToClassList("cc-swatch");
            if (big) b.AddToClassList("cc-swatch--big");
            b.EnableInClassList("cc-swatch--locked", !owned);
            b.style.backgroundColor = c;
            var lbl = new Label(label); lbl.AddToClassList("cc-swatch-name"); b.Add(lbl);
            AddBadge(b);
            if (!owned) AddLock(b);
            Hover(b, onHover);
            parent.Add(b);
            _selectables.Add((b, selected));
            b.EnableInClassList("cc-swatch--selected", selected());
        }

        void AddBadge(VisualElement host)
        {
            var badge = El(host, "cc-badge");
            badge.pickingMode = PickingMode.Ignore;
            var good = CreatorTheme.Get(CreatorReserved.Good);
            badge.style.backgroundColor = good;
            bool dark = good.r * 0.3f + good.g * 0.59f + good.b * 0.11f < 0.45f;
            badge.Add(new CreatorIcon(CreatorGlyph.Check, dark ? CreatorTheme.Paper : CreatorTheme.Ink));
            _badges.Add(badge);
        }

        void AddLock(VisualElement host)
        {
            var l = El(host, "cc-lock");
            l.pickingMode = PickingMode.Ignore;
            l.style.backgroundColor = CreatorTheme.Get(CreatorReserved.Landlord);
            l.Add(new CreatorIcon(CreatorGlyph.Lock, CreatorTheme.Paper));
            _locks.Add(l);
        }

        void Hover(VisualElement el, Action onHover)
        {
            if (onHover == null) return;
            el.RegisterCallback<PointerEnterEvent>(_ => onHover());
            el.RegisterCallback<FocusInEvent>(_ => onHover());
            el.RegisterCallback<PointerLeaveEvent>(_ => ShowSelectedInfo());
        }

        VisualElement Grid()
        {
            var g = El(_content, "cc-grid");
            return g;
        }

        VisualElement Row()
        {
            var r = El(_content, "cc-swatch-row");
            return r;
        }

        void Sub(string title, string note = null)
        {
            var row = El(_content, "cc-page-head");
            row.style.height = StyleKeyword.Auto;
            var l = Text(row, title, "cc-subhead");
            if (!string.IsNullOrEmpty(note)) { var n = Text(row, note, "cc-subhead-note"); n.style.marginLeft = 14; }
        }

        Button Btn(VisualElement parent, string text, CreatorGlyph? glyph, Action onClick, string extraClass, string key)
        {
            var b = new Button(onClick);
            b.AddToClassList("cc-btn");
            if (extraClass != null) b.AddToClassList(extraClass);
            if (glyph.HasValue) b.Add(new CreatorIcon(glyph.Value, CreatorTheme.Ink));
            var l = new Label(text); l.pickingMode = PickingMode.Ignore; b.Add(l);
            if (key != null) { var k = new Label(key); k.AddToClassList("cc-btn-key"); k.pickingMode = PickingMode.Ignore; b.Add(k); }
            parent.Add(b);
            return b;
        }

        Button Chip(VisualElement parent, string text, CreatorGlyph glyph, Action onClick)
        {
            var b = new Button(onClick);
            b.AddToClassList("cc-chip");
            b.Add(new CreatorIcon(glyph, CreatorTheme.Ink));
            var l = new Label(text); l.pickingMode = PickingMode.Ignore; b.Add(l);
            parent.Add(b);
            return b;
        }

        static VisualElement El(VisualElement parent, string cls)
        {
            var v = new VisualElement();
            v.AddToClassList(cls);
            parent.Add(v);
            return v;
        }

        static Label Text(VisualElement parent, string text, string cls)
        {
            var l = new Label(text);
            l.AddToClassList(cls);
            parent.Add(l);
            return l;
        }
    }
}
