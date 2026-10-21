using Cursor = UnityEngine.Cursor;
using System.Collections.Generic;
using HarvestPanic.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace HarvestPanic.UI
{
    /// <summary>
    /// Title screen and Play flow (Host / Join / Settings) on UI Toolkit. Talks to the game only through
    /// <see cref="ISessionService"/>; navigation is a <see cref="ScreenStack{T}"/>. Back = Escape / B / the Back buttons.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        public const float ConnectTimeout = 20f;

        public ScreenStack<MenuScreen> Stack { get; } = new ScreenStack<MenuScreen>();
        public UiPanel Panel { get; private set; }
        public bool IsConnecting { get; private set; }

        ISessionService _session;
        ISessionPlatform _platform;
        bool _hostSteam;
        Button _transportBtn;
        VisualElement _root;
        readonly Dictionary<MenuScreen, VisualElement> _screens = new Dictionary<MenuScreen, VisualElement>();
        VisualElement _popupConnecting, _popupMessage, _hostPlayers;
        Label _connBody, _msgTitle, _msgBody, _hostMode, _hostSolo, _lanEmpty, _footerMode, _footerHint;
        TextField _hostName, _joinAddress;
        ListView _lanList;
        readonly List<LanHost> _hosts = new List<LanHost>();
        SettingsPanel _settings;
        int _maxPlayers = 4;
        float _connectLeft;
        float _loadAt = -1f;
        string _connectTarget;

        void OnEnable()
        {
            _session = UiRuntime.EnsureSession();
            _platform = SessionAccess.Platform;
            if (_platform != null)
            {
                var st = SettingsService.Current;
                if (!string.IsNullOrEmpty(st.PlayerName)) _platform.SetLocalPlayerName(st.PlayerName);
                _hostSteam = _platform.IsSteamAvailable;
                _platform.OnLanSessionsChanged += OnLanChanged;
                SettingsService.Changed += OnSettingsChanged;
            }
            Build();
            _session.OnSessionChanged += OnSessionChanged;
            _session.OnError += OnSessionError;
            Stack.Changed += OnScreenChanged;
            Stack.ClearTo(MenuScreen.Title);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            Time.timeScale = 1f;
            if (_platform != null && !string.IsNullOrEmpty(_platform.LastEndReason))
            {
                ShowMessage("Back at the farmhouse", _platform.LastEndReason);
                _platform.ClearLastEndReason();
            }
            else StartCoroutine(OpenCreatorOnFirstRun());
        }

        /// <summary>
        /// First launch: open Customization's "Create your farmer" once (Core ICharacterCreator). Skipped in automated
        /// player builds (batchmode) so headless test runs never wait on it.
        /// </summary>
        System.Collections.IEnumerator OpenCreatorOnFirstRun()
        {
            yield return null; yield return null; // after the title focus settles
            if (Application.isBatchMode && !Application.isEditor) yield break;
            if (Services.TryGet(out ICharacterCreator creator) && creator.NeedsFirstRun && !creator.IsOpen && Stack.Current == MenuScreen.Title)
                creator.Open(CharacterCreatorContext.FirstRun);
        }

        /// <summary>True while the character creator owns input on top of the menu.</summary>
        static bool CreatorOpen => Services.TryGet(out ICharacterCreator c) && c.IsOpen;

        void OnLanChanged() { if (Stack.Current == MenuScreen.Join) RefreshHosts(false); }
        void OnSettingsChanged(GameSettings g) { if (_platform != null && !string.IsNullOrEmpty(g.PlayerName)) _platform.SetLocalPlayerName(g.PlayerName); }

        void OnDisable()
        {
            if (_platform != null) { _platform.OnLanSessionsChanged -= OnLanChanged; _platform.StopLanSearch(); SettingsService.Changed -= OnSettingsChanged; }
            if (_session != null) { _session.OnSessionChanged -= OnSessionChanged; _session.OnError -= OnSessionError; }
            Stack.Changed -= OnScreenChanged;
        }

        void Update()
        {
            if (IsConnecting)
            {
                _connectLeft -= Time.unscaledDeltaTime;
                int dots = 1 + (int)(Time.unscaledTime * 3f) % 3;
                _connBody.text = "Knocking on the gate" + new string('.', dots) + (string.IsNullOrEmpty(_connectTarget) ? "" : "\n" + _connectTarget);
                if (_connectLeft <= 0f) FailConnect("The farm never answered. Check the address and your network.");
            }
            if (_loadAt >= 0f && Time.unscaledTime >= _loadAt) { _loadAt = -1f; SceneLoader.Load(SceneLoader.Lobby); }

            bool back = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                        || (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);
            if (back && !CreatorOpen) GoBack();
        }

        // ------------------------------------------------------------------ public API (also used by tests)
        public void GoBack()
        {
            if (_popupMessage.ClassListContains("is-active")) { HideMessage(); return; }
            if (IsConnecting) { CancelConnect(); return; }
            if (Stack.Current == MenuScreen.Settings) { CloseSettings(); return; }
            Stack.Pop();
        }

        public void OpenPlay() => Stack.Push(MenuScreen.Play);

        /// <summary>Hosts and resumes the saved run (save-persistence; Economy loads it when the farm starts).</summary>
        public void ContinueFarm()
        {
            if (!Services.TryGet(out IRunSaveService save) || !save.HasSave) { ShowMessage("No farm to continue", "Grandpa's ledger is empty. Start a new farm instead."); return; }
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
            string name = string.IsNullOrWhiteSpace(_hostName.value) ? "Muddlebury Farm" : _hostName.value.Trim();
            BeginConnect("Opening the gate");
            if (_platform != null)
            {
                if (_hostSteam) _platform.HostSteam(name, _maxPlayers, true); else _platform.HostLan(name, _maxPlayers);
            }
            else _session.StartHost(name, _maxPlayers);
        }

        public void JoinAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address)) { ShowMessage("Which farm?", "Type an address or pick a farm from the list."); return; }
            string a = address.Trim();
            BeginConnect("Joining " + a);
            if (_platform != null)
            {
                if (a.Length >= 10 && ulong.TryParse(a, out ulong steamId)) _platform.JoinSteam(steamId); else _platform.JoinLan(a);
            }
            else _session.Join(a);
        }

        public void SetMaxPlayers(int n)
        {
            _maxPlayers = Mathf.Clamp(n, GameInfo.MinPlayers, GameInfo.MaxPlayers);
            int i = 0;
            foreach (var child in _hostPlayers.Children())
            {
                i++;
                child.EnableInClassList("hp-btn--selected", i == _maxPlayers);
            }
            _hostSolo.style.display = _maxPlayers == 1 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // ------------------------------------------------------------------ build
        void Build()
        {
            Panel = UiPanel.Create(transform, "UI_MainMenu", 100);
            _root = Panel.Root;
            _screens[MenuScreen.Title] = _root.Q("title-screen");
            _screens[MenuScreen.Play] = _root.Q("play-screen");
            _screens[MenuScreen.Host] = _root.Q("host-screen");
            _screens[MenuScreen.Join] = _root.Q("join-screen");
            _screens[MenuScreen.Settings] = _root.Q("settings-screen");
            _popupConnecting = _root.Q("popup-connecting");
            _popupMessage = _root.Q("popup-message");
            _connBody = _root.Q<Label>("connecting-body");
            _msgTitle = _root.Q<Label>("message-title");
            _msgBody = _root.Q<Label>("message-body");
            _hostMode = _root.Q<Label>("host-mode");
            _hostSolo = _root.Q<Label>("host-solo");
            _lanEmpty = _root.Q<Label>("lan-empty");
            _footerMode = _root.Q<Label>("footer-mode");
            _footerHint = _root.Q<Label>("footer-hint");
            _hostName = _root.Q<TextField>("host-name");
            _joinAddress = _root.Q<TextField>("join-address");
            _hostPlayers = _root.Q("host-players");
            _lanList = _root.Q<ListView>("lan-list");

            _root.Q<Label>("footer-version").text = "v" + Application.version + "  -  Muddlebury";
            string transport = SessionAccess.TransportLabel(_session);
            if (_platform != null) transport = _hostSteam ? "STEAM" : "LAN";
            _footerMode.text = transport == "STEAM" ? "Steam online" : transport == "LAN" ? "LAN mode" : transport;
            _footerHint.text = "Arrows / D-pad: move     Enter / A: choose     Esc / B: back";
            _hostMode.text = transport == "STEAM" ? "Hosting over Steam. Friends join from the Steam overlay." : "Hosting over LAN. Friends on your network can find you.";
            _root.Q<Label>("host-card-sub").text = transport == "STEAM" ? "Start a Steam lobby for your friends." : "Start a LAN lobby. Friends on your network join.";
            _hostName.value = "Muddlebury Farm";
            _transportBtn = _root.Q<Button>("btn-host-transport");
            _transportBtn.style.display = _platform != null && _platform.IsSteamAvailable ? DisplayStyle.Flex : DisplayStyle.None;
            _transportBtn.clicked += () =>
            {
                _hostSteam = !_hostSteam;
                _transportBtn.text = _hostSteam ? "Over: Steam (tap to switch)" : "Over: LAN (tap to switch)";
                _hostMode.text = _hostSteam ? "Hosting over Steam. Friends join from the Steam overlay." : "Hosting over LAN. Friends on your network can find you.";
            };
            _transportBtn.text = _hostSteam ? "Over: Steam (tap to switch)" : "Over: LAN (tap to switch)";

            Click("btn-play", OpenPlay);
            Click("btn-customize", OnCustomize);
            Click("btn-settings", OpenSettings);
            Click("btn-quit", Quit);
            Click("btn-play-back", () => Stack.Pop());
            Click("btn-host-card", () => Stack.Push(MenuScreen.Host));
            Click("btn-join-card", () => Stack.Push(MenuScreen.Join));
            Click("btn-host-go", HostGame);
            Click("btn-host-continue", ContinueFarm);
            Click("btn-host-back", () => Stack.Pop());
            Click("btn-join-back", () => Stack.Pop());
            Click("btn-join-go", () => JoinAddress(_joinAddress.value));
            Click("btn-refresh", () => RefreshHosts());
            Click("btn-steam-friends", OnSteamFriends);
            Click("btn-connecting-cancel", CancelConnect);
            Click("btn-message-ok", HideMessage);

            for (int n = 1; n <= GameInfo.MaxPlayers; n++)
            {
                int count = n;
                var b = new Button(() => SetMaxPlayers(count)) { text = n == 1 ? "Solo" : n + " farmers" };
                b.AddToClassList("hp-btn"); b.AddToClassList("hp-btn--small"); b.AddToClassList("hp-btn--center");
                _hostPlayers.Add(b);
            }
            SetMaxPlayers(4);

            _lanList.itemsSource = _hosts;
            _lanList.fixedItemHeight = 68;
            _lanList.makeItem = () =>
            {
                var row = new VisualElement(); row.AddToClassList("lan-row");
                var n = new Label { name = "n" }; n.AddToClassList("bold"); n.AddToClassList("lan-name");
                var c = new Label { name = "c" }; c.AddToClassList("bold"); c.AddToClassList("lan-count");
                var a = new Label { name = "a" }; a.AddToClassList("lan-addr");
                row.Add(n); row.Add(c); row.Add(a);
                return row;
            };
            _lanList.bindItem = (el, i) =>
            {
                var h = _hosts[i];
                el.Q<Label>("n").text = h.Display;
                bool full = h.MaxPlayers > 0 && h.Players >= h.MaxPlayers;
                el.Q<Label>("c").text = full ? "FULL " + h.Players + "/" + h.MaxPlayers : h.Players + "/" + h.MaxPlayers + " farmers";
                el.Q<Label>("a").text = h.Address;
                el.EnableInClassList("lan-row--full", full);
            };
            _lanList.selectionChanged += objs =>
            {
                foreach (var o in objs) if (o is LanHost h && !string.IsNullOrEmpty(h.Address)) _joinAddress.value = h.Address;
            };
            _lanList.itemsChosen += objs =>
            {
                foreach (var o in objs) if (o is LanHost h) JoinAddress(h.Address);
            };
            _root.Q<Button>("btn-steam-friends").style.display = transport == "STEAM" || SessionAccess.TryHasSteam() ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void Click(string name, System.Action a)
        {
            var b = _root.Q<Button>(name);
            if (b == null) { Debug.LogError("[UI] Missing button " + name); return; }
            b.clicked += a;
        }

        // ------------------------------------------------------------------ screens
        void OnScreenChanged(MenuScreen? from, MenuScreen? to)
        {
            if (_platform != null && from == MenuScreen.Join && to != MenuScreen.Join) _platform.StopLanSearch();
            foreach (var kv in _screens) kv.Value.EnableInClassList("is-active", to.HasValue && kv.Key == to.Value);
            if (!to.HasValue) return;
            switch (to.Value)
            {
                case MenuScreen.Title: UiFocus.FocusLater(_root.Q<Button>("btn-play")); break;
                case MenuScreen.Play: UiFocus.FocusLater(_root.Q<Button>("btn-host-card")); break;
                case MenuScreen.Host: UiFocus.FocusLater(RefreshContinue() ? _root.Q<Button>("btn-host-continue") : _root.Q<Button>("btn-host-go")); break;
                case MenuScreen.Join: RefreshHosts(); UiFocus.FocusLater(_root.Q<Button>("btn-join-back")); break;
            }
        }

        /// <summary>Shows "Continue Farm" when a host run save exists. Returns true when shown.</summary>
        public bool RefreshContinue()
        {
            bool has = Services.TryGet(out IRunSaveService save) && save.HasSave;
            var b = _root.Q<Button>("btn-host-continue");
            var info = _root.Q<Label>("host-continue-info");
            if (b != null) b.style.display = has ? DisplayStyle.Flex : DisplayStyle.None;
            if (info != null) { info.text = has ? "Saved farm: " + save.Summary : ""; info.style.display = has ? DisplayStyle.Flex : DisplayStyle.None; }
            var go = _root.Q<Button>("btn-host-go");
            if (go != null) go.text = has ? "New Farm" : "Open the Gate";
            return has;
        }

        void RefreshHosts(bool search = true)
        {
            if (search) SessionAccess.RefreshDiscovery(_session);
            _hosts.Clear();
            _hosts.AddRange(SessionAccess.GetHosts(_session));
            _lanList.Rebuild();
            bool supported = SessionAccess.SupportsDiscovery(_session);
            _lanEmpty.style.display = _hosts.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _lanEmpty.text = supported ? "No farms found yet. Is your friend hosting on this network?" : "Farm browsing is not available yet. Type the host's address below.";
        }

        void OpenSettings()
        {
            Stack.Push(MenuScreen.Settings);
            _settings = new SettingsPanel(_root.Q("settings-host"));
            _settings.Closed += CloseSettings;
            _settings.FocusFirst();
        }

        void CloseSettings()
        {
            SettingsService.Persist();
            if (Stack.Current == MenuScreen.Settings) Stack.Pop();
        }

        void OnCustomize()
        {
            if (Services.TryGet(out ICharacterCreator creator) && creator.Open(CharacterCreatorContext.MainMenu)) return;
            if (!SessionAccess.TryOpenWardrobe())
                ShowMessage("Customize", "Pick your outfit at the wardrobe in the Lobby yard (press C there). Your farmer is wearing the default overalls until then.");
        }

        void OnSteamFriends()
        {
            if (!SessionAccess.TryOpenSteamOverlay())
                ShowMessage("Steam Friends", "Steam is not running. Accept an invite from the Steam overlay once it is, or use a LAN address.");
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
        void BeginConnect(string target)
        {
            IsConnecting = true;
            _connectLeft = ConnectTimeout;
            _connectTarget = target;
            _popupConnecting.AddToClassList("is-active");
            UiFocus.FocusLater(_root.Q<Button>("btn-connecting-cancel"));
        }

        void EndConnect()
        {
            IsConnecting = false;
            _popupConnecting.RemoveFromClassList("is-active");
        }

        void CancelConnect()
        {
            EndConnect();
            if (_session.IsConnected) _session.Leave();
        }

        void FailConnect(string message)
        {
            bool was = IsConnecting;
            EndConnect();
            if (was && _session.IsConnected) _session.Leave();
            ShowMessage("Could not connect", message);
        }

        void OnSessionChanged()
        {
            if (!IsConnecting || !_session.IsConnected) return;
            EndConnect();
            // Networking may load the Lobby itself; only load it here if we are still in the menu shortly after.
            // The real SessionService loads the Lobby itself through NGO; only the dev fake needs the UI to do it.
            if (_session is FakeSessionService) _loadAt = Time.unscaledTime + 0.35f;
        }

        void OnSessionError(string message) => FailConnect(message);

        void ShowMessage(string title, string body)
        {
            _msgTitle.text = title; _msgBody.text = body;
            _popupMessage.AddToClassList("is-active");
            UiFocus.FocusLater(_root.Q<Button>("btn-message-ok"));
        }

        void HideMessage()
        {
            _popupMessage.RemoveFromClassList("is-active");
            switch (Stack.Current)
            {
                case MenuScreen.Title: UiFocus.FocusLater(_root.Q<Button>("btn-play")); break;
                case MenuScreen.Host: UiFocus.FocusLater(_root.Q<Button>("btn-host-go")); break;
                case MenuScreen.Join: UiFocus.FocusLater(_root.Q<Button>("btn-join-go")); break;
            }
        }
    }
}
