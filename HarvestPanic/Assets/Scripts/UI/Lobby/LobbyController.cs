using Cursor = UnityEngine.Cursor;
using System;
using System.Collections.Generic;
using HarvestPanic.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Direction = UnityEngine.UIElements.NavigationMoveEvent.Direction;

namespace HarvestPanic.UI
{
    /// <summary>
    /// Lobby Yard UI and rules: an always-on crew strip (badges in team colours, ready checks, talking rings), the crew
    /// ledger (Tab / View) with name tags, host crown, voice + mute, kick, the host's farm rules (countdown length, Start
    /// the day, Invite) and chat; ready via porch (hold 1 s) or R / Y; dinner bell (B / LB, host only) with the
    /// "start anyway?" confirm and the countdown that ends in <see cref="ISessionService.StartGame"/>.
    /// </summary>
    public sealed class LobbyController : MonoBehaviour
    {
        public const float ReadyHoldTime = 1f;
        public static readonly float[] CountdownChoices = { 3f, 5f, 10f };

        [SerializeField] LobbyFallbackWalker fallbackWalker;
        [SerializeField, Tooltip("Static overview camera used until a real player camera exists.")] Camera lobbyCamera;
        float _camPoll;
        [SerializeField, Tooltip("Seconds to wait for a real player before enabling the fallback walker.")] float playerWait = 1.5f;

        public StartSequence Sequence { get; } = new StartSequence();
        public UiPanel Panel { get; private set; }
        public bool CrewOpen { get; private set; }
        public string ToastText => _toastText.text;

        ISessionService _session;
        VisualElement _root, _crewPanel, _crewList, _crewStrip, _centerStack, _toast, _readyBox, _promptBar, _hostRules, _countdownChips;
        Label _toastText, _countNumber, _confirmLabel, _readyText, _yardSub, _yardCount, _crewHint, _crewCount;
        ScrollView _chatLog;
        TextField _chatInput;
        ShapePip _readyCheck;
        Button _btnStart, _btnInvite, _btnReady, _btnClose;
        readonly List<Button> _countChips = new List<Button>();
        readonly List<(UiPrompt p, Label cap)> _keycaps = new List<(UiPrompt, Label)>();
        VisualElement _bellPrompt;
        float _porchTime, _toastLeft, _graceLeft;
        bool _readyByPorch, _wasCounting;
        int _lastShown = -1;
        Action _unsubChat;
        IVoiceChat _voice;

        void OnEnable()
        {
            _session = UiRuntime.EnsureSession();
            if (!_session.IsConnected)
            {
                if (_session is FakeSessionService fake) fake.StartHost(UiText.Get("host.farmname.default"), GameInfo.MaxPlayers);
            }
            Build();
            _session.OnSessionChanged += Refresh;
            _session.OnError += OnSessionError;
            _unsubChat = SessionAccess.SubscribeChat(_session, AddChat);
            if (Services.TryGet(out _voice)) _voice.OnSpeakingChanged += OnSpeakingChanged;
            InputGlyphs.Changed += RefreshPrompts;
            _graceLeft = 1f;
            Refresh();
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
            Time.timeScale = 1f;
        }

        void OnDisable()
        {
            if (_session != null) { _session.OnSessionChanged -= Refresh; _session.OnError -= OnSessionError; }
            _unsubChat?.Invoke();
            if (_voice != null) _voice.OnSpeakingChanged -= OnSpeakingChanged;
            InputGlyphs.Changed -= RefreshPrompts;
            UiFocus.Pop(this);
        }

        void OnSpeakingChanged(ulong clientId, bool speaking) => Refresh();

        void OnSessionError(string message) => Toast(SessionErrors.Classify(message).Body);

        void Start()
        {
            if (fallbackWalker != null) Invoke(nameof(MaybeEnableWalker), playerWait);
        }

        void MaybeEnableWalker()
        {
            int playerLayer = LayerMask.NameToLayer(GameLayers.Player);
            foreach (var cc in FindObjectsByType<Collider>(FindObjectsSortMode.None))
                if (cc.gameObject.layer == playerLayer && !cc.transform.IsChildOf(fallbackWalker.transform)) return;
            fallbackWalker.gameObject.SetActive(true);
            fallbackWalker.enabled = true;
            if (lobbyCamera != null) lobbyCamera.gameObject.SetActive(false);
        }

        void PollCameras()
        {
            if (lobbyCamera == null || !lobbyCamera.gameObject.activeSelf) return;
            foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (c != lobbyCamera && c.enabled && c.gameObject.activeInHierarchy && c.targetTexture == null && c.cameraType == CameraType.Game)
                { lobbyCamera.gameObject.SetActive(false); return; }
        }

        static bool CreatorOpen => Services.TryGet(out ICharacterCreator creator) && creator.IsOpen;

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _camPoll -= dt; if (_camPoll <= 0f) { _camPoll = 0.5f; PollCameras(); }
            if (_toastLeft > 0f) { _toastLeft -= dt; if (_toastLeft <= 0f) _toast.RemoveFromClassList("is-shown"); }
            if (_graceLeft > 0f) _graceLeft -= dt;
            else if (!_session.IsConnected) { LoadingScreen.Show(SceneLoader.MainMenu); SceneLoader.Load(SceneLoader.MainMenu); enabled = false; return; }

            bool typing = _root.panel?.focusController?.focusedElement is VisualElement fe && _chatInput.Contains(fe) || CreatorOpen;
            bool paused = UiRuntime.Instance != null && UiRuntime.Instance.GetComponent<PauseOverlay>() is PauseOverlay po && po.IsOpen;
            var kb = Keyboard.current; var pad = Gamepad.current;
            if (!typing && !paused)
            {
                if (kb != null && kb.tabKey.wasPressedThisFrame || pad != null && pad.selectButton.wasPressedThisFrame) ToggleCrew();
                if (kb != null && kb.rKey.wasPressedThisFrame || pad != null && pad.buttonNorth.wasPressedThisFrame && !CrewOpen) ToggleReady();
                if (kb != null && kb.cKey.wasPressedThisFrame && !CrewOpen && !OpenWardrobe()) Toast(UiText.Get("lobby.toast.wardrobe"));
                if (kb != null && kb.bKey.wasPressedThisFrame || pad != null && pad.leftShoulder.wasPressedThisFrame) RingBell();
            }
            else if (!CreatorOpen && kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) SendChat();
            if (CrewOpen && !paused && (kb != null && kb.escapeKey.wasPressedThisFrame && !typing || pad != null && pad.buttonEast.wasPressedThisFrame)) SetCrewOpen(false);

            TickPorch(dt);

            if (Sequence.IsCounting)
            {
                if (Sequence.Tick(dt)) FinishCountdown();
                else UpdateCountdownUi();
            }
            else
            {
                Sequence.Tick(dt);
                if (_wasCounting) { _centerStack.RemoveFromClassList("is-counting"); }
                _centerStack.EnableInClassList("is-confirming", Sequence.IsAwaitingConfirm);
            }
            _wasCounting = Sequence.IsCounting;
        }

        /// <summary>C: Customization's wardrobe (Core ICharacterCreator), else the reflection bridge.</summary>
        static bool OpenWardrobe()
        {
            if (Services.TryGet(out ICharacterCreator creator) && creator.Open(CharacterCreatorContext.Wardrobe)) return true;
            return SessionAccess.TryOpenWardrobe();
        }

        // ------------------------------------------------------------------ ready
        void TickPorch(float dt)
        {
            var porch = PorchZone.Instance;
            if (porch == null) return;
            bool inside = porch.LocalInside;
            if (inside)
            {
                _porchTime += dt;
                if (_porchTime >= ReadyHoldTime && !IsLocalReady()) { _readyByPorch = true; _session.SetReady(true); }
            }
            else
            {
                _porchTime = 0f;
                if (_readyByPorch) { _readyByPorch = false; if (IsLocalReady()) _session.SetReady(false); }
            }
            porch.SetLight(IsLocalReady());
        }

        public bool IsLocalReady()
        {
            foreach (var p in _session.Players) if (p.ClientId == _session.LocalClientId) return p.IsReady;
            return false;
        }

        public void ToggleReady()
        {
            _readyByPorch = false;
            bool next = !IsLocalReady();
            _session.SetReady(next);
            if (next) UiSounds.Confirm(); else UiSounds.Back();
        }

        // ------------------------------------------------------------------ bell and countdown

        /// <summary>Host rule: countdown length (3 / 5 / 10 s). Ignored while a countdown runs.</summary>
        public void SetCountdown(float seconds)
        {
            if (Sequence.IsCounting) return;
            Sequence.CountdownSeconds = Mathf.Clamp(seconds, 0f, 10f);
            for (int i = 0; i < _countChips.Count; i++) UiKit.Select(_countChips[i], Mathf.Approximately(CountdownChoices[i], Sequence.CountdownSeconds));
        }

        public void RingBell()
        {
            var result = Sequence.RingBell(_session.IsHost, _session.Players);
            switch (result)
            {
                case BellResult.NotHost:
                    Toast(UiText.Get("lobby.toast.nothost"));
                    UiSounds.Error();
                    break;
                case BellResult.NeedsConfirm:
                    DinnerBell.Instance?.Ring();
                    _confirmLabel.text = UiText.Format("lobby.confirm", Sequence.ReadyCount, Sequence.TotalCount);
                    _centerStack.AddToClassList("is-confirming");
                    UiSounds.Confirm();
                    break;
                case BellResult.CountdownStarted:
                    DinnerBell.Instance?.Ring();
                    _centerStack.RemoveFromClassList("is-confirming");
                    _centerStack.AddToClassList("is-counting");
                    _lastShown = -1;
                    UpdateCountdownUi();
                    UiSounds.Confirm();
                    break;
            }
        }

        void UpdateCountdownUi()
        {
            int n = Mathf.Max(1, Mathf.CeilToInt(Sequence.SecondsLeft));
            if (n == _lastShown) return;
            _lastShown = n;
            _countNumber.text = n.ToString();
            UiKit.Pulse(_countNumber, "tick", 140);
            UiSounds.Hover();
        }

        void FinishCountdown()
        {
            _centerStack.RemoveFromClassList("is-counting");
            if (CrewOpen) SetCrewOpen(false);
            if (!_session.IsHost) return;
            LoadingScreen.Show("Muddlebury");
            _session.StartGame();
        }

        // ------------------------------------------------------------------ crew
        void ToggleCrew() => SetCrewOpen(!CrewOpen);

        public void SetCrewOpen(bool open)
        {
            if (open == CrewOpen) return;
            CrewOpen = open;
            _crewPanel.EnableInClassList("is-open", open);
            if (open)
            {
                UiFocus.Push(this);
                Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
                UiKit.ShowScreen(_crewPanel);
                UiFocus.FocusLater(_session.IsHost ? _btnStart : _btnReady);
                UiSounds.Confirm();
            }
            else
            {
                UiFocus.Pop(this);
                UiKit.HideScreen(_crewPanel);
                Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
                _root.panel?.focusController?.focusedElement?.Blur();
                UiSounds.Back();
            }
        }

        void SendChat()
        {
            var text = _chatInput.value?.Trim();
            _chatInput.value = "";
            if (string.IsNullOrEmpty(text)) return;
            if (!SessionAccess.SendChat(_session, text)) AddChat(UiText.Get("lobby.chat.you"), text);
            UiFocus.FocusLater(_chatInput);
        }

        void AddChat(string who, string text)
        {
            var line = new VisualElement(); line.AddToClassList("row"); line.AddToClassList("chat-line");
            int slot = SlotOfName(who);
            var name = UiKit.Label(who + ":", "bold", "chat-name");
            if (slot >= 0) name.style.color = Color.Lerp(ShapePip.TeamColor(slot), UiKit.Ink, 0.5f);
            line.Add(name);
            line.Add(UiKit.Label(text, "chat-text"));
            _chatLog.Add(line);
            while (_chatLog.contentContainer.childCount > 60) _chatLog.contentContainer.RemoveAt(0);
            _chatLog.schedule.Execute(() => _chatLog.scrollOffset = new Vector2(0, 99999)).StartingIn(20);
            if (!CrewOpen) Toast(who + ": " + text);
        }

        int SlotOfName(string who)
        {
            var players = _session.Players;
            for (int i = 0; i < players.Count; i++) if (players[i].Name == who) return i;
            return -1;
        }

        void Refresh()
        {
            if (_crewList == null) return;
            var players = _session.Players;
            bool host = _session.IsHost;
            _yardCount.text = UiText.Format("lobby.count", players.Count, GameInfo.MaxPlayers);
            _crewCount.text = _yardCount.text;
            string transport = SessionAccess.TransportLabel(_session);
            _yardSub.text = UiText.Get(transport == "STEAM" ? "lobby.sub.steam" : "lobby.sub.lan") + (host ? "  ·  " + UiText.Get("lobby.sub.host") : "");
            _bellPrompt?.EnableInClassList("prompt--off", !host);
            _hostRules.EnableInClassList("host-rules--guest", !host);
            _btnStart.SetEnabled(host);
            foreach (var c in _countChips) c.SetEnabled(host);

            var focusedName = (_root.panel?.focusController?.focusedElement as VisualElement)?.name;
            _crewList.Clear(); _crewStrip.Clear();
            bool canKick = host && SessionAccess.SupportsKick(_session);
            var rowButtons = new List<VisualElement>();
            for (int i = 0; i < GameInfo.MaxPlayers; i++)
            {
                bool filled = i < players.Count;
                var p = filled ? players[i] : default;
                bool me = filled && p.ClientId == _session.LocalClientId;
                bool talking = filled && _voice != null && (me ? _voice.IsTransmitting : _voice.IsSpeaking(p.ClientId));
                string name = !filled ? UiText.Get("lobby.seat.open") : string.IsNullOrEmpty(p.Name) ? UiText.Format("common.farmer.n", i + 1) : p.Name;
                Color nameColor = filled ? Color.Lerp(ShapePip.TeamColor(i), UiKit.Ink, 0.55f) : new Color(0.17f, 0.13f, 0.09f, 0.45f);

                // strip card
                var card = new VisualElement(); card.AddToClassList("strip-card"); card.EnableInClassList("strip-card--empty", !filled); card.EnableInClassList("strip-card--me", me);
                var sb = new FarmerBadge(i, 58f, !filled); sb.EnableInClassList("farmer-badge--talking", talking);
                card.Add(sb);
                var sName = UiKit.Label(name, "bold", "strip-name"); sName.style.color = nameColor;
                card.Add(sName);
                if (filled)
                {
                    var tick = new VisualElement(); tick.AddToClassList("strip-ready"); tick.EnableInClassList("strip-ready--on", p.IsReady);
                    if (p.IsReady) tick.Add(new ShapePip(PipShape.Check, Color.white, 26f));
                    card.Add(tick);
                }
                _crewStrip.Add(card);

                // ledger row
                var row = new VisualElement(); row.AddToClassList("crew-row"); row.EnableInClassList("crew-row--me", me); row.EnableInClassList("crew-row--empty", !filled);
                var badge = new FarmerBadge(i, 66f, !filled); badge.EnableInClassList("farmer-badge--talking", talking);
                row.Add(badge);
                var names = new VisualElement(); names.AddToClassList("crew-names");
                var nameLabel = UiKit.Label(name, "display", "crew-name"); nameLabel.style.color = nameColor;
                names.Add(nameLabel);
                var tags = new VisualElement(); tags.AddToClassList("row");
                if (filled && p.IsHost) tags.Add(UiKit.Label("@lobby.tag.host", "tag", "tag--pumpkin"));
                if (me) tags.Add(UiKit.Label("@lobby.tag.you", "tag"));
                if (talking) tags.Add(UiKit.Label("@lobby.talking", "tag", "tag--talking"));
                if (filled && _voice != null && !me && _voice.IsMuted(p.ClientId)) tags.Add(UiKit.Label("@lobby.muted", "tag"));
                names.Add(tags);
                row.Add(names);
                row.Add(new VisualElement { style = { flexGrow = 1 } });
                if (filled)
                {
                    var stamp = UiKit.Label(p.IsReady ? "@lobby.stamp.ready" : "@lobby.stamp.waiting", "stamp");
                    stamp.EnableInClassList("stamp--muted", !p.IsReady);
                    row.Add(stamp);
                }
                if (filled && _voice != null && !me)
                {
                    ulong vid = p.ClientId;
                    var mute = UiKit.Button(_voice.IsMuted(vid) ? "@lobby.unmute" : "@lobby.mute", () => { _voice.SetMuted(vid, !_voice.IsMuted(vid)); Refresh(); }, "hp-btn--small", "hp-btn--center", "crew-btn");
                    mute.name = "mute-" + vid;
                    mute.EnableInClassList(UiKit.SelectedClass, _voice.IsMuted(vid));
                    row.Add(mute); rowButtons.Add(mute);
                }
                if (filled && canKick && !me && !p.IsHost)
                {
                    ulong id = p.ClientId;
                    var kick = UiKit.Button("@lobby.kick", () => SessionAccess.Kick(_session, id), "hp-btn--small", "hp-btn--center", "crew-btn", UiKit.BackClass);
                    kick.name = "kick-" + id;
                    row.Add(kick); rowButtons.Add(kick);
                }
                _crewList.Add(row);
            }
            _crewHint.text = UiText.Get(host ? "lobby.hint.host" : "lobby.hint.guest");
            bool localReady = IsLocalReady();
            _readyText.text = UiText.Get(localReady ? "lobby.ready.on" : "lobby.ready.off");
            _readyBox.EnableInClassList("is-ready", localReady);
            _readyCheck.style.display = localReady ? DisplayStyle.Flex : DisplayStyle.None;
            _btnReady.text = UiText.Get(localReady ? "lobby.ready.undo" : "lobby.ready.button");
            _btnReady.EnableInClassList(UiKit.SelectedClass, localReady);
            LinkCrewNav(rowButtons);
            if (!string.IsNullOrEmpty(focusedName) && CrewOpen)
            {
                var again = _crewPanel.Q(focusedName);
                if (again != null) UiFocus.FocusLater(again);
            }
            if (Sequence.OnPlayersChanged(players))
            {
                _centerStack.RemoveFromClassList("is-counting");
                DinnerBell.Instance?.Sad();
                Toast(UiText.Get("lobby.toast.sadbell"));
            }
        }

        void LinkCrewNav(List<VisualElement> rowButtons)
        {
            var bottom = new List<VisualElement>();
            UiNav.Column(rowButtons);
            var rules = new List<VisualElement>(_countChips);
            UiNav.Row(rules);
            var actions = new List<VisualElement> { _btnStart, _btnInvite };
            UiNav.Row(actions);
            var foot = new List<VisualElement> { _btnReady, _btnClose };
            UiNav.Row(foot);
            VisualElement top = rowButtons.Count > 0 ? rowButtons[0] : null;
            VisualElement afterRows = _countChips[0];
            if (rowButtons.Count > 0) UiNav.Link(rowButtons[rowButtons.Count - 1], Direction.Down, afterRows);
            UiNav.RowVertical(rules, rowButtons.Count > 0 ? rowButtons[rowButtons.Count - 1] : null, _btnStart);
            UiNav.RowVertical(actions, _countChips[0], _chatInput);
            UiNav.Link(_chatInput, Direction.Up, _btnStart);
            UiNav.Link(_chatInput, Direction.Down, _btnReady);
            UiNav.RowVertical(foot, _chatInput, null);
        }

        void Toast(string text)
        {
            _toastText.text = text;
            _toast.AddToClassList("is-shown");
            _toastLeft = 3.2f;
        }

        void RefreshPrompts()
        {
            if (_promptBar == null) return;
            _promptBar.Clear(); _keycaps.Clear();
            void Add(UiPrompt p, string key, bool bell = false)
            {
                var chip = InputGlyphs.Prompt(p, key, out var cap);
                chip.AddToClassList("prompt--pill");
                _keycaps.Add((p, cap));
                _promptBar.Add(chip);
                if (bell) _bellPrompt = chip;
            }
            Add(UiPrompt.Ready, "lobby.prompt.ready");
            Add(UiPrompt.Crew, "lobby.prompt.crew");
            if (!InputGlyphs.UsingGamepad) Add(UiPrompt.Wardrobe, "lobby.prompt.wardrobe");
            Add(UiPrompt.Bell, "lobby.prompt.bell", true);
            Add(UiPrompt.Menu, "lobby.prompt.menu");
            _bellPrompt?.EnableInClassList("prompt--off", _session != null && !_session.IsHost);
        }

        void Build()
        {
            Panel = UiPanel.Create(transform, "UI_Lobby", 100);
            _root = Panel.Root;
            _crewPanel = _root.Q("crew-panel"); _crewList = _root.Q("crew-list"); _crewStrip = _root.Q("crew-strip");
            _centerStack = _root.Q("center-stack"); _toast = _root.Q("toast"); _readyBox = _root.Q("ready-box"); _promptBar = _root.Q("prompt-bar");
            _toastText = _root.Q<Label>("toast-text"); _countNumber = _root.Q<Label>("countdown-number");
            _confirmLabel = _root.Q<Label>("confirm-label"); _readyText = _root.Q<Label>("ready-text");
            _yardSub = _root.Q<Label>("yard-sub"); _yardCount = _root.Q<Label>("yard-count"); _crewHint = _root.Q<Label>("crew-hint"); _crewCount = _root.Q<Label>("crew-count");
            _chatLog = _root.Q<ScrollView>("chat-log"); _chatInput = _root.Q<TextField>("chat-input");
            _hostRules = _root.Q("host-rules"); _countdownChips = _root.Q("countdown-chips");
            _btnStart = _root.Q<Button>("btn-start-day"); _btnInvite = _root.Q<Button>("btn-invite");
            _btnReady = _root.Q<Button>("btn-crew-ready"); _btnClose = _root.Q<Button>("btn-crew-close");
            _chatInput.label = UiText.Get(SessionAccess.SupportsChat(_session) ? "lobby.chat.label" : "lobby.chat.local");
            _readyCheck = new ShapePip(PipShape.Check, Color.white, 30f);
            _readyBox.Add(_readyCheck);
            _readyCheck.style.display = DisplayStyle.None;
            foreach (float s in CountdownChoices)
            {
                float secs = s;
                var b = UiKit.Chip(UiText.Format("lobby.rules.seconds", Mathf.RoundToInt(s)), () => SetCountdown(secs), "countdown-" + Mathf.RoundToInt(s));
                _countdownChips.Add(b); _countChips.Add(b);
            }
            SetCountdown(Sequence.CountdownSeconds);
            _btnClose.clicked += () => SetCrewOpen(false);
            _btnStart.clicked += RingBell;
            _btnReady.clicked += ToggleReady;
            _btnInvite.clicked += () => Toast(UiText.Get(SessionAccess.TryOpenSteamOverlay() ? "pause.invite.steam" : "pause.invite.lan"));
            UiKit.HideScreen(_crewPanel);
            RefreshPrompts();
        }
    }
}
