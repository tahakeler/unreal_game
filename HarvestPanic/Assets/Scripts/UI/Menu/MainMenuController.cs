using Cursor = UnityEngine.Cursor;
using System;
using System.Collections;
using System.Collections.Generic;
using HarvestPanic.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Direction = UnityEngine.UIElements.NavigationMoveEvent.Direction;

namespace HarvestPanic.UI
{
    /// <summary>Who may find a hosted farm (frontend-lobby "Privacy"). Invite-only needs a private Steam lobby (Networking gap).</summary>
    public enum HostPrivacy { FriendsOnly = 0, Public = 1, Lan = 2 }

    public enum JoinTab { Friends = 0, Nearby = 1, Address = 2 }

    /// <summary>
    /// Title screen and the Play flow (Host / Join / Continue Farm), Character, Settings, Credits and Quit, on UI Toolkit over
    /// the live farm backdrop (<see cref="MenuBackdrop"/>). Talks to the game only through Core contracts:
    /// <see cref="ISessionService"/> / <see cref="ISessionPlatform"/> (host, join, LAN list, Steam), <see cref="IRunSaveService"/>
    /// (continue) and <see cref="ICharacterCreator"/> (Customization's creator). Navigation is a <see cref="ScreenStack{T}"/>;
    /// Back = Esc / B / the Back buttons. Every text comes from the string table (<see cref="UiText"/>).
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        public const float ConnectTimeout = 20f, NoteSeconds = 9f;

        public ScreenStack<MenuScreen> Stack { get; } = new ScreenStack<MenuScreen>();
        public UiPanel Panel { get; private set; }
        public UiDialog Dialog { get; private set; }
        public bool IsConnecting { get; private set; }
        public HostPrivacy Privacy { get; private set; } = HostPrivacy.Lan;
        public int MaxPlayers => _maxPlayers;
        public bool HostContinue { get; private set; }
        public JoinTab ActiveJoinTab { get; private set; } = JoinTab.Nearby;
        public SettingsPanel Settings => _settings;

        ISessionService _session;
        ISessionPlatform _platform;
        VisualElement _root, _screensRoot, _footer, _hostPlayers, _hostPrivacy, _hostContinueBox, _joinTabs, _footerPrompts, _noteCard;
        readonly Dictionary<MenuScreen, VisualElement> _screens = new Dictionary<MenuScreen, VisualElement>();
        readonly Dictionary<JoinTab, VisualElement> _panes = new Dictionary<JoinTab, VisualElement>();
        readonly List<VisualElement> _crewChips = new List<VisualElement>(), _privacyChips = new List<VisualElement>(), _tabChips = new List<VisualElement>();
        readonly List<Button> _lanRows = new List<Button>();
        Label _hostMode, _hostSolo, _lanEmpty, _lanSearching, _footerMode, _noteText, _steamStatus, _profileName, _hostTitle, _hostSub, _continueSub;
        TextField _hostName, _joinAddress;
        ScrollView _lanList;
        Button _btnPlay, _btnGo, _btnContinueCard, _btnRefresh;
        FarmerBadge _profileBadge;
        readonly List<LanHost> _hosts = new List<LanHost>();
        SettingsPanel _settings;
        CreditsPanel _credits;
        int _maxPlayers = 4, _noteIndex = -1;
        float _connectLeft, _loadAt = -1f, _noteAt;
        string _connectTarget, _connectTitle;
        Action _lastAttempt;
        bool _platformConnectSeen;

        void OnEnable()
        {
            _session = UiRuntime.EnsureSession();
            _platform = SessionAccess.Platform;
            if (_platform != null)
            {
                var st = SettingsService.Current;
                if (!string.IsNullOrEmpty(st.PlayerName)) _platform.SetLocalPlayerName(st.PlayerName);
                _platform.OnLanSessionsChanged += OnLanChanged;
            }
            Privacy = _platform != null && _platform.IsSteamAvailable ? HostPrivacy.FriendsOnly : HostPrivacy.Lan;
            SettingsService.Changed += OnSettingsChanged;
            InputGlyphs.Changed += RefreshPrompts;
            Build();
            if (Services.TryGet(out ICharacterCreator cc)) cc.Closed += OnCreatorClosed;
            _session.OnSessionChanged += OnSessionChanged;
            _session.OnError += OnSessionError;
            Stack.Changed += OnScreenChanged;
            Stack.ClearTo(MenuScreen.Title);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            Time.timeScale = 1f;
            if (_platform != null && !string.IsNullOrEmpty(_platform.LastEndReason))
            {
                string reason = _platform.LastEndReason;
                _platform.ClearLastEndReason();
                ShowError(reason, null);
            }
            else StartCoroutine(OpenCreatorOnFirstRun());
        }

        void OnDisable()
        {
            if (_platform != null) { _platform.OnLanSessionsChanged -= OnLanChanged; _platform.StopLanSearch(); }
            SettingsService.Changed -= OnSettingsChanged;
            InputGlyphs.Changed -= RefreshPrompts;
            if (_session != null) { _session.OnSessionChanged -= OnSessionChanged; _session.OnError -= OnSessionError; }
            Stack.Changed -= OnScreenChanged;
            if (Services.TryGet(out ICharacterCreator cc)) cc.Closed -= OnCreatorClosed;
        }

        void OnLanChanged() { if (Stack.Current == MenuScreen.Join) RefreshHosts(false); }

        void OnSettingsChanged(GameSettings g)
        {
            if (_platform != null && !string.IsNullOrEmpty(g.PlayerName)) _platform.SetLocalPlayerName(g.PlayerName);
            RefreshProfile();
        }

        /// <summary>
        /// First launch: open Customization's "Create your farmer" once (Core ICharacterCreator, feature/character-creator).
        /// Skipped in automated player builds (batchmode) so headless runs never wait on it.
        /// </summary>
        IEnumerator OpenCreatorOnFirstRun()
        {
            yield return null; yield return null;
            if (Application.isBatchMode && !Application.isEditor) yield break;
            if (Services.TryGet(out ICharacterCreator creator) && creator.NeedsFirstRun && !creator.IsOpen && Stack.Current == MenuScreen.Title && !Dialog.IsOpen)
                creator.Open(CharacterCreatorContext.FirstRun);
        }

        void OnCreatorClosed(bool confirmed)
        {
            if (this == null || !isActiveAndEnabled) return;
            RefreshProfile();
            if (Stack.Current == MenuScreen.Title) UiFocus.FocusLater(_btnPlay);
        }

        static bool CreatorOpen => Services.TryGet(out ICharacterCreator c) && c.IsOpen;

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (IsConnecting)
            {
                _connectLeft -= dt;
                int dots = 1 + (int)(Time.unscaledTime * 3f) % 3;
                Dialog.SetBody(UiText.Get("connect.knocking") + new string('.', dots) + (string.IsNullOrEmpty(_connectTarget) ? "" : "\n" + _connectTarget));
                if (_connectLeft <= 0f) FailConnect(UiText.Get("error.timeout.reason"));
            }
            else if (_platform != null && _platform.IsConnecting && !_platformConnectSeen && !_session.IsConnected)
            {
                // A Steam invite accepted from the overlay starts a join without the menu: show the progress card.
                _platformConnectSeen = true;
                _lastAttempt = null;
                BeginConnect(UiText.Get("connect.invite.title"), UiText.Get("connect.invite.target"));
            }
            if (_platform != null && !_platform.IsConnecting) _platformConnectSeen = false;
            if (_loadAt >= 0f && Time.unscaledTime >= _loadAt) { _loadAt = -1f; LoadingScreen.Show(SceneLoader.Lobby); SceneLoader.Load(SceneLoader.Lobby); }

            if (Stack.Current == MenuScreen.Title && !Dialog.IsOpen && Time.unscaledTime >= _noteAt) NextNote();

            if (CreatorOpen || SettingsPanel.IsListening) return;
            var kb = Keyboard.current; var pad = Gamepad.current;
            bool back = kb != null && kb.escapeKey.wasPressedThisFrame || pad != null && pad.buttonEast.wasPressedThisFrame;
            if (back) GoBack();
            if (Stack.Current == MenuScreen.Join && !Dialog.IsOpen && !IsConnecting)
            {
                bool typing = _root.panel?.focusController?.focusedElement is VisualElement fe && _joinAddress.Contains(fe);
                int step = 0;
                if (pad != null && pad.leftShoulder.wasPressedThisFrame || kb != null && !typing && kb.qKey.wasPressedThisFrame) step = -1;
                if (pad != null && pad.rightShoulder.wasPressedThisFrame || kb != null && !typing && kb.eKey.wasPressedThisFrame) step = 1;
                if (step != 0) { SelectJoinTab((JoinTab)(((int)ActiveJoinTab + step + 3) % 3)); UiSounds.Hover(); }
                if (ActiveJoinTab == JoinTab.Nearby && (kb != null && kb.f5Key.wasPressedThisFrame || pad != null && pad.buttonNorth.wasPressedThisFrame)) RefreshHosts();
            }
        }

        // ------------------------------------------------------------------ public API (also used by tests)

        /// <summary>Esc / B: closes the dialog, cancels a connect, leaves settings/credits, or pops one screen.</summary>
        public void GoBack()
        {
            if (Dialog.IsOpen) { Dialog.Back(); return; }
            if (IsConnecting) { CancelConnect(); return; }
            if (Stack.Current == MenuScreen.Settings) { _settings?.Close(); if (Stack.Current == MenuScreen.Settings) CloseSettings(); return; }
            if (Stack.Current == MenuScreen.Title) { UiFocus.FocusLater(_root.Q<Button>("btn-quit")); return; }
            if (Stack.Pop()) UiSounds.Back();
        }

        public void OpenPlay() => Stack.Push(MenuScreen.Play);

        /// <summary>Opens the host form; <paramref name="continueRun"/> hosts the saved farm instead of a new one.</summary>
        public void OpenHost(bool continueRun = false)
        {
            HostContinue = continueRun && HasSave(out _);
            Stack.Push(MenuScreen.Host);
        }

        public void OpenJoin() => Stack.Push(MenuScreen.Join);

        /// <summary>Hosts and resumes the saved run (save-persistence; Economy loads it when the farm starts).</summary>
        public void ContinueFarm()
        {
            if (!HasSave(out var save)) { Dialog.Show(UiText.Get("continue.none.title"), UiText.Get("continue.none.body")); return; }
            save.ContinueRequested = true;
            StartHosting();
        }

        public void HostGame()
        {
            if (Services.TryGet(out IRunSaveService save)) save.ContinueRequested = false;
            StartHosting();
        }

        void StartHosting()
        {
            string farm = string.IsNullOrWhiteSpace(_hostName.value) ? UiText.Get("host.farmname.default") : _hostName.value.Trim();
            int max = _maxPlayers;
            var privacy = Privacy;
            _lastAttempt = () => { _maxPlayers = max; Privacy = privacy; StartHosting(); };
            BeginConnect(UiText.Get("connect.host.title"), farm);
            if (_platform != null)
            {
                if (privacy == HostPrivacy.Lan || !_platform.IsSteamAvailable) _platform.HostLan(farm, max);
                else _platform.HostSteam(farm, max, privacy == HostPrivacy.FriendsOnly);
            }
            else _session.StartHost(farm, max);
        }

        public void JoinAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                Dialog.Show(UiText.Get("join.empty.title"), UiText.Get("join.empty.body"), DialogTone.Info, "?");
                return;
            }
            string a = address.Trim();
            _lastAttempt = () => JoinAddress(a);
            BeginConnect(UiText.Get("connect.join.title"), a);
            if (_platform != null)
            {
                if (a.Length >= 10 && ulong.TryParse(a, out ulong steamId)) _platform.JoinSteam(steamId); else _platform.JoinLan(a);
            }
            else _session.Join(a);
        }

        public void SetMaxPlayers(int n)
        {
            _maxPlayers = Mathf.Clamp(n, GameInfo.MinPlayers, GameInfo.MaxPlayers);
            for (int i = 0; i < _crewChips.Count; i++) UiKit.Select(_crewChips[i], i + 1 == _maxPlayers);
            _hostSolo.style.display = _maxPlayers == 1 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void SetPrivacy(HostPrivacy p)
        {
            bool steam = _platform != null && _platform.IsSteamAvailable;
            if (!steam) p = HostPrivacy.Lan;
            Privacy = p;
            for (int i = 0; i < _privacyChips.Count; i++) UiKit.Select(_privacyChips[i], i == (int)p);
            _hostMode.text = UiText.Get(p switch
            {
                HostPrivacy.FriendsOnly => "host.privacy.friends.hint",
                HostPrivacy.Public => "host.privacy.public.hint",
                _ => steam ? "host.privacy.lan.hint" : "host.privacy.lan.nosteam",
            });
        }

        public void SelectJoinTab(JoinTab tab)
        {
            ActiveJoinTab = tab;
            foreach (var kv in _panes) kv.Value.EnableInClassList("is-active", kv.Key == tab);
            for (int i = 0; i < _tabChips.Count; i++) UiKit.Select(_tabChips[i], i == (int)tab);
            if (tab == JoinTab.Nearby) RefreshHosts(); else _platform?.StopLanSearch();
            LinkJoinNav();
            UiFocus.FocusLater(_tabChips[(int)tab]);
        }

        /// <summary>Shows/updates the Continue card and host continue box from the run save. Returns true when a save exists.</summary>
        public bool RefreshContinue()
        {
            bool has = HasSave(out var save);
            _btnContinueCard.SetEnabled(has);
            _continueSub.text = has ? UiText.Format("play.continue.sub", save.Summary) : UiText.Get("play.continue.none");
            _hostContinueBox.style.display = HostContinue && has ? DisplayStyle.Flex : DisplayStyle.None;
            _root.Q<Label>("host-continue-info").text = has ? save.Summary : "";
            _hostTitle.text = UiText.Get(HostContinue && has ? "host.title.continue" : "host.title");
            _hostSub.text = UiText.Get(HostContinue && has ? "host.sub.continue" : "host.sub");
            _btnGo.text = UiText.Get(HostContinue && has ? "host.go.continue" : "host.go");
            return has;
        }

        static bool HasSave(out IRunSaveService save) => Services.TryGet(out save) && save.HasSave;

        /// <summary>Credits: team + third-party licences (THIRD_PARTY_LICENSES.md). Back / Esc / B returns to the title.</summary>
        public CreditsPanel OpenCredits()
        {
            Stack.Push(MenuScreen.Credits);
            _credits = new CreditsPanel(_root.Q("credits-host"));
            _credits.Closed += () => { if (Stack.Current == MenuScreen.Credits) { Stack.Pop(); UiSounds.Back(); } };
            _credits.FocusFirst();
            return _credits;
        }

        public SettingsPanel OpenSettings()
        {
            Stack.Push(MenuScreen.Settings);
            _settings = new SettingsPanel(_root.Q("settings-host"));
            _settings.Closed += CloseSettings;
            _settings.FocusFirst();
            return _settings;
        }

        /// <summary>Character button: Customization's creator when present, otherwise a pointer to the lobby wardrobe.</summary>
        public void OpenCharacter()
        {
            if (Services.TryGet(out ICharacterCreator creator) && creator.Open(CharacterCreatorContext.MainMenu)) return;
            if (SessionAccess.TryOpenWardrobe()) return;
            Dialog.Show(UiText.Get("character.fallback.title"), UiText.Get("character.fallback.body"), DialogTone.Info, "?",
                "@common.ok", null, "@character.fallback.name", () => { OpenSettings(); });
        }

        public void ConfirmQuit()
        {
            Dialog.Show(UiText.Get("quit.title"), UiText.Get("quit.body"), DialogTone.Info, "?", "@quit.yes", Quit, "@common.stay", null);
            UiFocus.FocusLater(Dialog.Secondary);
        }

        // ------------------------------------------------------------------ build
        void Build()
        {
            Panel = UiPanel.Create(transform, "UI_MainMenu", 100);
            _root = Panel.Root;
            _screensRoot = _root.Q("screens");
            _footer = _root.Q("footer");
            Dialog = new UiDialog(_root.Q("dialog-layer"), _screensRoot, _footer);
            _screens[MenuScreen.Title] = _root.Q("title-screen");
            _screens[MenuScreen.Play] = _root.Q("play-screen");
            _screens[MenuScreen.Host] = _root.Q("host-screen");
            _screens[MenuScreen.Join] = _root.Q("join-screen");
            _screens[MenuScreen.Settings] = _root.Q("settings-screen");
            _screens[MenuScreen.Credits] = _root.Q("credits-screen");
            _hostMode = _root.Q<Label>("host-mode"); _hostSolo = _root.Q<Label>("host-solo");
            _hostTitle = _root.Q<Label>("host-title"); _hostSub = _root.Q<Label>("host-sub");
            _lanEmpty = _root.Q<Label>("lan-empty"); _lanSearching = _root.Q<Label>("lan-searching");
            _footerMode = _root.Q<Label>("footer-mode"); _footerPrompts = _root.Q("footer-prompts");
            _noteText = _root.Q<Label>("note-text"); _noteCard = _root.Q("note-card");
            _steamStatus = _root.Q<Label>("steam-status"); _profileName = _root.Q<Label>("profile-name");
            _continueSub = _root.Q<Label>("continue-card-sub");
            _hostName = _root.Q<TextField>("host-name"); _joinAddress = _root.Q<TextField>("join-address");
            _hostPlayers = _root.Q("host-players"); _hostPrivacy = _root.Q("host-privacy"); _hostContinueBox = _root.Q("host-continue");
            _joinTabs = _root.Q("join-tabs");
            _lanList = _root.Q<ScrollView>("lan-list");
            _btnPlay = _root.Q<Button>("btn-play"); _btnGo = _root.Q<Button>("btn-host-go");
            _btnContinueCard = _root.Q<Button>("btn-continue-card"); _btnRefresh = _root.Q<Button>("btn-refresh");
            _panes[JoinTab.Friends] = _root.Q("join-friends");
            _panes[JoinTab.Nearby] = _root.Q("join-lan");
            _panes[JoinTab.Address] = _root.Q("join-address-pane");

            _root.Q<Label>("footer-version").text = UiText.Format("footer.version", Application.version);
            bool steam = _platform != null ? _platform.IsSteamAvailable : SessionAccess.TransportLabel(_session) == "STEAM";
            _footerMode.text = UiText.Get(steam ? "footer.mode.steam" : "footer.mode.lan");
            _root.Q("footer-chip").EnableInClassList("mode-chip--steam", steam);
            _steamStatus.text = UiText.Get(steam ? "join.friends.status.on" : "join.friends.status.off");
            _hostName.value = UiText.Get("host.farmname.default");

            // art
            _root.Q("host-card-art").Add(new MenuIcon(MenuIconKind.Barn, 180f));
            _root.Q("join-card-art").Add(new MenuIcon(MenuIconKind.Gate, 180f));
            _root.Q("continue-card-art").Add(new MenuIcon(MenuIconKind.Ledger, 180f));
            _root.Q("friends-icon").Add(new MenuIcon(MenuIconKind.Friends, 96f));
            _root.Q("lan-icon").Add(new MenuIcon(MenuIconKind.Lan, 58f));
            _root.Q("address-icon").Add(new MenuIcon(MenuIconKind.Signpost, 58f));
            _profileBadge = new FarmerBadge(0, 64f);
            _root.Q("profile-badge").Add(_profileBadge);
            RefreshProfile();

            Click("btn-play", OpenPlay);
            Click("btn-character", OpenCharacter);
            Click("profile-chip", OpenCharacter);
            Click("btn-settings", () => OpenSettings());
            Click("btn-credits", () => OpenCredits());
            Click("btn-quit", ConfirmQuit);
            Click("btn-play-back", () => { if (Stack.Pop()) { } });
            Click("btn-host-card", () => OpenHost(false));
            Click("btn-join-card", OpenJoin);
            Click("btn-continue-card", () => OpenHost(true));
            Click("btn-host-go", () => { if (HostContinue) ContinueFarm(); else HostGame(); });
            Click("btn-host-back", () => Stack.Pop());
            Click("btn-join-back", () => Stack.Pop());
            Click("btn-join-go", () => JoinAddress(_joinAddress.value));
            Click("btn-refresh", () => RefreshHosts());
            Click("btn-steam-friends", OnSteamFriends);
            _joinAddress.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) JoinAddress(_joinAddress.value); });

            for (int n = 1; n <= GameInfo.MaxPlayers; n++)
            {
                int count = n;
                var b = UiKit.Chip(n == 1 ? UiText.Get("host.crew.solo") : UiText.Format("host.crew.n", n), () => SetMaxPlayers(count), "crew-" + n);
                _hostPlayers.Add(b); _crewChips.Add(b);
            }
            string[] privacyKeys = { "@host.privacy.friends", "@host.privacy.public", "@host.privacy.lan" };
            for (int i = 0; i < privacyKeys.Length; i++)
            {
                var p = (HostPrivacy)i;
                var b = UiKit.Chip(privacyKeys[i], () => SetPrivacy(p), "privacy-" + p.ToString().ToLowerInvariant());
                b.SetEnabled(p == HostPrivacy.Lan || steam);
                _hostPrivacy.Add(b); _privacyChips.Add(b);
            }
            string[] tabKeys = { "@join.tab.friends", "@join.tab.lan", "@join.tab.address" };
            for (int i = 0; i < tabKeys.Length; i++)
            {
                var t = (JoinTab)i;
                var b = UiKit.Chip(tabKeys[i], () => SelectJoinTab(t), "tab-join-" + t.ToString().ToLowerInvariant());
                _joinTabs.Add(b); _tabChips.Add(b);
            }
            SetMaxPlayers(4);
            SetPrivacy(Privacy);
            LinkNav();
            RefreshPrompts();
        }

        void Click(string name, Action a)
        {
            var b = _root.Q<Button>(name);
            if (b == null) { Debug.LogError("[UI] Missing button " + name); return; }
            b.clicked += a;
        }

        void RefreshProfile()
        {
            if (_profileName == null) return;
            string n = SettingsService.Current.PlayerName;
            if (string.IsNullOrWhiteSpace(n) && _platform != null) n = _platform.LocalPlayerName;
            _profileName.text = string.IsNullOrWhiteSpace(n) ? UiText.Get("common.farmer") : n;
        }

        // ------------------------------------------------------------------ navigation routes
        void LinkNav()
        {
            var title = new List<VisualElement> { _btnPlay, _root.Q("btn-character"), _root.Q("btn-settings"), _root.Q("btn-credits"), _root.Q("btn-quit") };
            UiNav.Column(title, wrap: true);
            var profile = _root.Q("profile-chip");
            foreach (var b in title) UiNav.Link(b, Direction.Right, profile);
            UiNav.Link(profile, Direction.Left, _btnPlay); UiNav.Link(profile, Direction.Down, _btnPlay);

            var cards = new List<VisualElement> { _root.Q("btn-host-card"), _root.Q("btn-join-card"), _btnContinueCard };
            UiNav.Row(cards);
            var playBack = _root.Q("btn-play-back");
            UiNav.RowVertical(cards, null, playBack);
            UiNav.Link(playBack, Direction.Up, cards[0]);

            var hostBack = _root.Q("btn-host-back");
            UiNav.Row(_crewChips); UiNav.Row(_privacyChips);
            UiNav.Link(_hostName, Direction.Down, _crewChips[3]);
            UiNav.RowVertical(_crewChips, _hostName, _privacyChips[(int)HostPrivacy.Lan]);
            UiNav.RowVertical(_privacyChips, _crewChips[3], _btnGo);
            UiNav.Link(_btnGo, Direction.Up, _privacyChips[(int)HostPrivacy.Lan]);
            UiNav.Link(_btnGo, Direction.Left, hostBack);
            UiNav.Link(hostBack, Direction.Right, _btnGo);
            UiNav.Link(hostBack, Direction.Up, _privacyChips[(int)HostPrivacy.Lan]);
            UiNav.Row(_tabChips);
        }

        /// <summary>Routes inside the join card for the active tab: tabs -> pane content -> Back.</summary>
        void LinkJoinNav()
        {
            var back = _root.Q("btn-join-back");
            var tab = _tabChips[(int)ActiveJoinTab];
            VisualElement first;
            switch (ActiveJoinTab)
            {
                case JoinTab.Friends:
                    first = _root.Q("btn-steam-friends");
                    UiNav.Link(first, Direction.Up, tab); UiNav.Link(first, Direction.Down, back);
                    break;
                case JoinTab.Address:
                    first = _joinAddress;
                    var go = _root.Q("btn-join-go");
                    UiNav.Link(_joinAddress, Direction.Up, tab); UiNav.Link(_joinAddress, Direction.Down, go);
                    UiNav.Link(go, Direction.Up, _joinAddress); UiNav.Link(go, Direction.Left, _joinAddress); UiNav.Link(go, Direction.Down, back);
                    break;
                default:
                    first = _lanRows.Count > 0 ? (VisualElement)_lanRows[0] : _btnRefresh;
                    UiNav.Link(_btnRefresh, Direction.Up, tab); UiNav.Link(_btnRefresh, Direction.Left, tab);
                    UiNav.Link(_btnRefresh, Direction.Down, _lanRows.Count > 0 ? (VisualElement)_lanRows[0] : back);
                    var rows = new List<VisualElement>(_lanRows);
                    UiNav.Column(rows);
                    if (rows.Count > 0)
                    {
                        UiNav.Link(rows[0], Direction.Up, _btnRefresh);
                        UiNav.Link(rows[rows.Count - 1], Direction.Down, back);
                        foreach (var r in rows) UiNav.Link(r, Direction.Right, _btnRefresh);
                    }
                    break;
            }
            UiNav.RowVertical(_tabChips, null, first);
            UiNav.Link(back, Direction.Up, ActiveJoinTab == JoinTab.Nearby && _lanRows.Count > 0 ? (VisualElement)_lanRows[_lanRows.Count - 1] : first);
        }

        // ------------------------------------------------------------------ screens
        void OnScreenChanged(MenuScreen? from, MenuScreen? to)
        {
            if (_platform != null && from == MenuScreen.Join && to != MenuScreen.Join) _platform.StopLanSearch();
            foreach (var kv in _screens)
            {
                if (to.HasValue && kv.Key == to.Value) UiKit.ShowScreen(kv.Value);
                else UiKit.HideScreen(kv.Value);
            }
            bool footerVisible = to != MenuScreen.Settings && to != MenuScreen.Credits;
            _footer.style.display = footerVisible ? DisplayStyle.Flex : DisplayStyle.None;
            _root.Q("fade-left").style.display = to == MenuScreen.Settings || to == MenuScreen.Credits ? DisplayStyle.None : DisplayStyle.Flex;
            MenuBackdrop.Instance?.SetShot(to ?? MenuScreen.Title);
            RefreshPrompts();
            if (!to.HasValue) return;
            switch (to.Value)
            {
                case MenuScreen.Title: UiFocus.FocusLater(_btnPlay); _noteAt = 0f; RefreshProfile(); break;
                case MenuScreen.Play:
                    bool has = RefreshContinue();
                    UiFocus.FocusLater(from == MenuScreen.Join ? _root.Q<Button>("btn-join-card") : from == MenuScreen.Host ? (has && HostContinue ? _btnContinueCard : _root.Q<Button>("btn-host-card")) : has ? _btnContinueCard : _root.Q<Button>("btn-host-card"));
                    break;
                case MenuScreen.Host: RefreshContinue(); UiFocus.FocusLater(_btnGo); break;
                case MenuScreen.Join: SelectJoinTab(ActiveJoinTab); break;
            }
        }

        void NextNote()
        {
            _noteAt = Time.unscaledTime + NoteSeconds;
            var tips = UiText.KeysWithPrefix("tip.");
            if (tips.Count == 0) { _noteCard.style.display = DisplayStyle.None; return; }
            _noteIndex = (_noteIndex + 1 + UnityEngine.Random.Range(0, Mathf.Max(1, tips.Count - 1))) % tips.Count;
            string text = UiText.Get(tips[_noteIndex]);
            if (UiKit.ReducedMotion || string.IsNullOrEmpty(_noteText.text)) { _noteText.text = text; return; }
            _noteCard.AddToClassList("note--swap");
            _noteCard.schedule.Execute(() => { _noteText.text = text; _noteCard.RemoveFromClassList("note--swap"); }).StartingIn(380);
            UiKit.Pulse(_root.Q("logo-2"), "logo--bounce", 360);
        }

        void RefreshPrompts()
        {
            if (_footerPrompts == null) return;
            _footerPrompts.Clear();
            void Add(UiPrompt p, string key) { var chip = InputGlyphs.Prompt(p, key, out _); chip.AddToClassList("prompt--on-dark"); _footerPrompts.Add(chip); }
            Add(UiPrompt.Navigate, "prompt.navigate");
            Add(UiPrompt.Select, "prompt.select");
            if (Stack.HasCurrent && Stack.Current != MenuScreen.Title) Add(UiPrompt.Back, "prompt.back");
            if (Stack.HasCurrent && Stack.Current == MenuScreen.Join) Add(UiPrompt.Tab, "prompt.tabs");
        }

        void RefreshHosts(bool search = true)
        {
            if (search) SessionAccess.RefreshDiscovery(_session);
            _hosts.Clear();
            _hosts.AddRange(SessionAccess.GetHosts(_session));
            var focused = _root.panel?.focusController?.focusedElement as VisualElement;
            int focusedIndex = focused != null ? _lanRows.IndexOf(focused as Button) : -1;
            _lanList.Clear(); _lanRows.Clear();
            foreach (var h in _hosts) { var row = MakeLanRow(h); _lanList.Add(row); _lanRows.Add(row); }
            bool supported = SessionAccess.SupportsDiscovery(_session);
            _lanEmpty.style.display = _hosts.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _lanEmpty.text = UiText.Get(supported ? "join.lan.empty" : "join.lan.unsupported");
            _lanSearching.style.display = supported ? DisplayStyle.Flex : DisplayStyle.None;
            LinkJoinNav();
            if (focusedIndex >= 0 && _lanRows.Count > 0) UiFocus.FocusLater(_lanRows[Mathf.Min(focusedIndex, _lanRows.Count - 1)]);
        }

        Button MakeLanRow(LanHost h)
        {
            bool full = h.MaxPlayers > 0 && h.Players >= h.MaxPlayers;
            bool otherVersion = !string.IsNullOrEmpty(h.Version) && h.Version != Application.version;
            var row = UiKit.Button("", () => OnLanRowChosen(h), "lan-row");
            row.name = "lan-row-" + _lanRows.Count;
            row.EnableInClassList("lan-row--blocked", full || otherVersion);
            var names = new VisualElement { pickingMode = PickingMode.Ignore }; names.AddToClassList("lan-row__names");
            names.Add(UiKit.Label(h.Display, "lan-row__name"));
            string sub = string.IsNullOrEmpty(h.HostName) ? h.Address : UiText.Format("join.lan.hostedby", h.HostName, h.Address);
            names.Add(UiKit.Label(sub, "lan-row__host"));
            row.Add(names);
            if (h.InProgress) row.Add(UiKit.Label("@join.lan.inprogress", "tag"));
            if (otherVersion) row.Add(UiKit.Label("@join.lan.version", "tag", "tag--pumpkin"));
            if (full) row.Add(UiKit.Label("@join.lan.full", "tag"));
            var seats = new VisualElement { pickingMode = PickingMode.Ignore }; seats.AddToClassList("lan-row__seats");
            int max = Mathf.Clamp(h.MaxPlayers > 0 ? h.MaxPlayers : GameInfo.MaxPlayers, 1, GameInfo.MaxPlayers);
            for (int i = 0; i < max; i++) { var s = new VisualElement(); s.AddToClassList("seat"); s.EnableInClassList("seat--taken", i < h.Players); seats.Add(s); }
            row.Add(seats);
            row.Add(UiKit.Label(h.Players + "/" + max, "lan-row__count"));
            foreach (var c in row.Children()) c.pickingMode = PickingMode.Ignore;
            return row;
        }

        void OnLanRowChosen(LanHost h)
        {
            bool full = h.MaxPlayers > 0 && h.Players >= h.MaxPlayers;
            if (full) { ShowError(UiText.Format("error.full.reason", h.MaxPlayers), () => RefreshHosts()); return; }
            if (!string.IsNullOrEmpty(h.Version) && h.Version != Application.version)
            {
                Dialog.Show(UiText.Get("error.version.title"), UiText.Format("error.version.body", Application.version, h.Version), DialogTone.Error, "!");
                return;
            }
            _joinAddress.value = h.Address;
            JoinAddress(h.Address);
        }

        void CloseSettings()
        {
            SettingsService.Persist();
            if (Stack.Current == MenuScreen.Settings) { Stack.Pop(); UiSounds.Back(); }
        }

        void OnSteamFriends()
        {
            if (!SessionAccess.TryOpenSteamOverlay())
                Dialog.Show(UiText.Get("error.steam.title"), UiText.Get("join.friends.overlay.off"), DialogTone.Info, "!");
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------ connecting
        void BeginConnect(string title, string target)
        {
            IsConnecting = true;
            _connectLeft = ConnectTimeout;
            _connectTarget = target;
            _connectTitle = title;
            Dialog.ShowProgress(title, UiText.Get("connect.knocking"), CancelConnect);
        }

        void EndConnect()
        {
            if (!IsConnecting) return;
            IsConnecting = false;
            if (Dialog.IsOpen && Dialog.Tone == DialogTone.Busy) Dialog.Close();
        }

        void CancelConnect()
        {
            EndConnect();
            if (_session.IsConnected || _platform != null && _platform.IsConnecting) _session.Leave();
            LoadingScreen.Cancel();
        }

        void FailConnect(string message)
        {
            bool was = IsConnecting;
            EndConnect();
            if (was && _session.IsConnected) _session.Leave();
            LoadingScreen.Cancel();
            ShowError(message, _lastAttempt);
        }

        void ShowError(string reason, Action retry)
        {
            var e = SessionErrors.Classify(reason);
            Dialog.ShowError(e, retry);
        }

        void OnSessionChanged()
        {
            if (!IsConnecting || !_session.IsConnected) return;
            EndConnect();
            // The real SessionService loads the Lobby itself through NGO (the loading card picks that load up);
            // only the dev fake needs the UI to load it.
            if (_session is FakeSessionService) _loadAt = Time.unscaledTime + 0.35f;
            else LoadingScreen.Show(SceneLoader.Lobby);
        }

        void OnSessionError(string message)
        {
            if (IsConnecting) { FailConnect(message); return; }
            ShowError(message, null);
        }
    }
}
