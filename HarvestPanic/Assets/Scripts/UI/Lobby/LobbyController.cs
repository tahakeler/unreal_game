using Cursor = UnityEngine.Cursor;
using System;
using System.Collections.Generic;
using HarvestPanic.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace HarvestPanic.UI
{
    /// <summary>
    /// Lobby Yard UI and rules: crew overlay (Tab), ready via porch (hold 1 s) or R, dinner bell (B, host only) with the
    /// "start anyway?" confirm and 3 s countdown that ends in <see cref="ISessionService.StartGame"/>.
    /// </summary>
    public sealed class LobbyController : MonoBehaviour
    {
        public const float ReadyHoldTime = 1f;

        [SerializeField] LobbyFallbackWalker fallbackWalker;
        [SerializeField, Tooltip("Static overview camera used until a real player camera exists.")] Camera lobbyCamera;
        float _camPoll;
        [SerializeField, Tooltip("Seconds to wait for a real player before enabling the fallback walker.")] float playerWait = 1.5f;

        public StartSequence Sequence { get; } = new StartSequence();
        public UiPanel Panel { get; private set; }
        public bool CrewOpen { get; private set; }
        public string ToastText => _toastText.text;

        ISessionService _session;
        VisualElement _root, _crewPanel, _crewList, _centerStack, _toast, _readyBox, _promptBell;
        Label _toastText, _countNumber, _confirmLabel, _readyText, _yardSub, _yardCount, _crewHint;
        ScrollView _chatLog;
        TextField _chatInput;
        ShapePip _readyCheck;
        float _porchTime, _toastLeft, _graceLeft;
        bool _readyByPorch, _wasCounting;
        int _lastShown = -1;
        Action _unsubChat;
        CursorLockMode _prevLock; bool _prevVisible;

        void OnEnable()
        {
            _session = UiRuntime.EnsureSession();
            if (!_session.IsConnected)
            {
                if (_session is FakeSessionService fake) fake.StartHost("Dev Farm", GameInfo.MaxPlayers);
            }
            Build();
            _session.OnSessionChanged += Refresh;
            _unsubChat = SessionAccess.SubscribeChat(_session, AddChat);
            _graceLeft = 1f;
            Refresh();
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
            Time.timeScale = 1f;
        }

        void OnDisable()
        {
            if (_session != null) _session.OnSessionChanged -= Refresh;
            _unsubChat?.Invoke();
            if (CrewOpen) UiFocus.ModalCount = Mathf.Max(0, UiFocus.ModalCount - 1);
        }

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

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _camPoll -= dt; if (_camPoll <= 0f) { _camPoll = 0.5f; PollCameras(); }
            if (_toastLeft > 0f) { _toastLeft -= dt; if (_toastLeft <= 0f) _toast.RemoveFromClassList("is-shown"); }
            if (_graceLeft > 0f) _graceLeft -= dt;
            else if (!_session.IsConnected) { SceneLoader.Load(SceneLoader.MainMenu); enabled = false; return; }

            bool typing = _root.panel?.focusController?.focusedElement is TextField;
            var kb = Keyboard.current; var pad = Gamepad.current;
            if (!typing)
            {
                if (kb != null && kb.tabKey.wasPressedThisFrame || pad != null && pad.selectButton.wasPressedThisFrame) ToggleCrew();
                if (kb != null && kb.rKey.wasPressedThisFrame || pad != null && pad.buttonNorth.wasPressedThisFrame) ToggleReady();
                if (kb != null && kb.bKey.wasPressedThisFrame || pad != null && pad.leftShoulder.wasPressedThisFrame) RingBell();
            }
            else if (kb != null && kb.enterKey.wasPressedThisFrame) SendChat();
            if (CrewOpen && kb != null && kb.escapeKey.wasPressedThisFrame && !typing) SetCrewOpen(false);

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
            _session.SetReady(!IsLocalReady());
        }

        // ------------------------------------------------------------------ bell and countdown
        public void RingBell()
        {
            var result = Sequence.RingBell(_session.IsHost, _session.Players);
            switch (result)
            {
                case BellResult.NotHost:
                    Toast("Only the host can ring the bell. Ask them nicely.");
                    break;
                case BellResult.NeedsConfirm:
                    DinnerBell.Instance?.Ring();
                    _confirmLabel.text = "Start anyway? " + Sequence.ReadyCount + "/" + Sequence.TotalCount + " ready.\nRing the bell again!";
                    _centerStack.AddToClassList("is-confirming");
                    break;
                case BellResult.CountdownStarted:
                    DinnerBell.Instance?.Ring();
                    _centerStack.RemoveFromClassList("is-confirming");
                    _centerStack.AddToClassList("is-counting");
                    _lastShown = -1;
                    UpdateCountdownUi();
                    break;
            }
        }

        void UpdateCountdownUi()
        {
            int n = Mathf.Max(1, Mathf.CeilToInt(Sequence.SecondsLeft));
            if (n == _lastShown) return;
            _lastShown = n;
            _countNumber.text = n.ToString();
            _countNumber.AddToClassList("tick");
            _countNumber.schedule.Execute(() => _countNumber.RemoveFromClassList("tick")).StartingIn(120);
        }

        void FinishCountdown()
        {
            _centerStack.RemoveFromClassList("is-counting");
            if (_session.IsHost) _session.StartGame();
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
                UiFocus.ModalCount++;
                _prevLock = Cursor.lockState; _prevVisible = Cursor.visible;
                Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
                UiFocus.FocusLater(_root.Q<Button>("btn-crew-close"));
            }
            else
            {
                UiFocus.ModalCount = Mathf.Max(0, UiFocus.ModalCount - 1);
                Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
                _root.panel?.focusController?.focusedElement?.Blur();
            }
        }

        void SendChat()
        {
            var text = _chatInput.value?.Trim();
            _chatInput.value = "";
            if (string.IsNullOrEmpty(text)) return;
            if (!SessionAccess.SendChat(_session, text)) AddChat("You", text);
            UiFocus.FocusLater(_chatInput);
        }

        void AddChat(string who, string text)
        {
            var l = new Label(who + ": " + text); l.AddToClassList("chat-line"); l.AddToClassList("bold");
            _chatLog.Add(l);
            _chatLog.schedule.Execute(() => _chatLog.scrollOffset = new Vector2(0, 99999)).StartingIn(20);
        }

        void Refresh()
        {
            if (_crewList == null) return;
            var players = _session.Players;
            _yardCount.text = players.Count + " / " + GameInfo.MaxPlayers + " farmers";
            _yardSub.text = SessionAccess.TransportLabel(_session) + (_session.IsHost ? "  -  you are the host" : "");
            _promptBell.EnableInClassList("prompt--off", !_session.IsHost);
            _crewList.Clear();
            bool canKick = _session.IsHost && SessionAccess.SupportsKick(_session);
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                bool me = p.ClientId == _session.LocalClientId;
                var row = new VisualElement(); row.AddToClassList("crew-row"); row.EnableInClassList("crew-row--me", me);
                row.Add(new ShapePip(ShapePip.TeamShape(i), ShapePip.TeamColor(i)));
                var name = new Label(string.IsNullOrEmpty(p.Name) ? "Farmer " + (i + 1) : p.Name); name.AddToClassList("bold"); name.AddToClassList("crew-name");
                row.Add(name);
                if (p.IsHost) { var t = new Label("HOST"); t.AddToClassList("bold"); t.AddToClassList("crew-tag"); row.Add(t); }
                if (me) { var t = new Label("YOU"); t.AddToClassList("bold"); t.AddToClassList("crew-tag"); row.Add(t); }
                var ready = new Label(p.IsReady ? "READY" : "waiting"); ready.AddToClassList("bold"); ready.AddToClassList("crew-ready");
                ready.EnableInClassList("crew-ready--yes", p.IsReady);
                row.Add(ready);
                row.Add(new ShapePip(p.IsReady ? PipShape.Check : PipShape.Circle, p.IsReady ? new Color(0.5f, 0.64f, 0.29f) : new Color(1, 1, 1, 0f), 36f));
                if (canKick && !me && !p.IsHost)
                {
                    ulong id = p.ClientId;
                    var kick = new Button(() => SessionAccess.Kick(_session, id)) { text = "Kick", name = "kick-" + id };
                    kick.AddToClassList("hp-btn"); kick.AddToClassList("crew-kick");
                    row.Add(kick);
                }
                _crewList.Add(row);
            }
            _crewHint.text = _session.IsHost ? "Ring the dinner bell (B) when everyone is ready." : "Waiting for the host to ring the dinner bell.";
            bool localReady = IsLocalReady();
            _readyText.text = localReady ? "Ready! (R to undo)" : "Not ready (stand on the porch or press R)";
            _readyBox.EnableInClassList("is-ready", localReady);
            _readyCheck.style.display = localReady ? DisplayStyle.Flex : DisplayStyle.None;
            if (Sequence.OnPlayersChanged(players))
            {
                _centerStack.RemoveFromClassList("is-counting");
                DinnerBell.Instance?.Sad();
                Toast("Someone left the porch. The bell rings sadly.");
            }
        }

        void Toast(string text)
        {
            _toastText.text = text;
            _toast.AddToClassList("is-shown");
            _toastLeft = 3f;
        }

        void Build()
        {
            Panel = UiPanel.Create(transform, "UI_Lobby", 100);
            _root = Panel.Root;
            _crewPanel = _root.Q("crew-panel"); _crewList = _root.Q("crew-list");
            _centerStack = _root.Q("center-stack"); _toast = _root.Q("toast"); _readyBox = _root.Q("ready-box"); _promptBell = _root.Q("prompt-bell");
            _toastText = _root.Q<Label>("toast-text"); _countNumber = _root.Q<Label>("countdown-number");
            _confirmLabel = _root.Q<Label>("confirm-label"); _readyText = _root.Q<Label>("ready-text");
            _yardSub = _root.Q<Label>("yard-sub"); _yardCount = _root.Q<Label>("yard-count"); _crewHint = _root.Q<Label>("crew-hint");
            _chatLog = _root.Q<ScrollView>("chat-log"); _chatInput = _root.Q<TextField>("chat-input");
            _chatInput.label = SessionAccess.SupportsChat(_session) ? "Say something (Enter)" : "Say something (local only)";
            _readyCheck = new ShapePip(PipShape.Check, Color.white, 30f);
            _readyBox.Add(_readyCheck);
            _readyCheck.style.display = DisplayStyle.None;
            _root.Q<Button>("btn-crew-close").clicked += () => SetCrewOpen(false);
        }
    }
}
