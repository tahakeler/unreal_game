using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace HarvestPanic.UI
{
    /// <summary>
    /// Scene-transition card ("Off to Muddlebury!") with one of Grandpa's tips, shown above everything while a scene loads.
    /// It covers loads the UI starts (<see cref="Show"/>), and NGO loads started by the host or the network on any peer:
    /// an async load in flight is detected from <see cref="SceneManager.sceneCount"/> &gt; <see cref="SceneManager.loadedSceneCount"/>.
    /// It hides once nothing is loading and the new scene has had a moment to settle (the first spawn hitch stays covered),
    /// with a minimum on-screen time so it never flickers. Display only: never takes focus or input. Lives on UiRuntime.
    /// </summary>
    public sealed class LoadingScreen : MonoBehaviour
    {
        public const float MinVisible = 0.9f, SettleSeconds = 0.45f, FailSafeSeconds = 25f;

        public static LoadingScreen Instance { get; private set; }

        UiPanel _panel;
        VisualElement _card, _runner;
        Label _title, _sub, _tip, _tipHead;
        float _shownAt = -1f, _settleUntil = -1f;
        string _activeAtShow;
        bool _explicit;
        int _tipIndex = -1;
        readonly System.Random _rng = new System.Random();

        public bool IsVisible { get; private set; }
        /// <summary>Destination of the current or last transition ("" when unknown).</summary>
        public string Destination { get; private set; } = "";
        /// <summary>Every destination shown this session, in order (tests).</summary>
        public readonly List<string> History = new List<string>();
        public string TipText => _tip != null ? _tip.text : "";
        public string TitleText => _title != null ? _title.text : "";
        public VisualElement Root => _panel != null ? _panel.Root : null;

        void Awake()
        {
            Instance = this;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            if (Instance == this) Instance = null;
        }

        /// <summary>Shows the card now, for a load the caller is about to start. <paramref name="sceneName"/> picks the title.</summary>
        public static void Show(string sceneName)
        {
            var ls = Instance != null ? Instance : UiRuntime.Ensure().GetComponent<LoadingScreen>();
            ls?.Begin(sceneName, true);
        }

        /// <summary>Hides immediately (e.g. a connect failed before any load started).</summary>
        public static void Cancel() { if (Instance != null) Instance.End(true); }

        void OnActiveSceneChanged(Scene from, Scene to)
        {
            if (!IsVisible) return;
            _settleUntil = Time.unscaledTime + SettleSeconds;
        }

        void Update()
        {
            bool loading = SceneManager.sceneCount > SceneManager.loadedSceneCount;
            if (loading && !IsVisible) Begin(LoadingSceneName(), false);
            if (!IsVisible) return;
            float now = Time.unscaledTime;
            if (loading) { _settleUntil = now + SettleSeconds; return; }
            bool sceneChanged = SceneManager.GetActiveScene().name != _activeAtShow;
            if (_explicit && !sceneChanged && now - _shownAt < FailSafeSeconds) return; // the load the caller promised hasn't started yet
            if (_settleUntil < 0f) _settleUntil = now + SettleSeconds;
            if (now >= _settleUntil && now - _shownAt >= MinVisible) End(false);
        }

        static string LoadingSceneName()
        {
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var s = SceneManager.GetSceneAt(i);
                if (!s.isLoaded) return s.name;
            }
            return "";
        }

        void Begin(string scene, bool isExplicit)
        {
            Build();
            Destination = scene ?? "";
            History.Add(Destination);
            if (History.Count > 32) History.RemoveAt(0);
            _explicit = isExplicit;
            _activeAtShow = SceneManager.GetActiveScene().name;
            _settleUntil = -1f;
            _title.text = UiText.Get(TitleKey(Destination));
            _sub.text = UiText.Get(SubKey(Destination));
            NextTip();
            if (!IsVisible) _shownAt = Time.unscaledTime;
            IsVisible = true;
            _panel.Root.style.display = DisplayStyle.Flex;
            _panel.Root.RemoveFromClassList("loading--out");
            UiKit.Pulse(_card, "loading-card--in", 30);
        }

        void End(bool immediate)
        {
            if (!IsVisible) return;
            IsVisible = false;
            _explicit = false;
            if (immediate || UiKit.ReducedMotion) { _panel.Root.style.display = DisplayStyle.None; return; }
            _panel.Root.AddToClassList("loading--out");
            _panel.Root.schedule.Execute(() => { if (!IsVisible) _panel.Root.style.display = DisplayStyle.None; }).StartingIn(320);
        }

        public static string TitleKey(string scene) => scene switch
        {
            SceneLoader.Lobby => "loading.lobby.title",
            SceneLoader.MainMenu => "loading.menu.title",
            "Muddlebury" => "loading.farm.title",
            _ => "loading.generic.title",
        };

        static string SubKey(string scene) => scene switch
        {
            SceneLoader.Lobby => "loading.lobby.sub",
            SceneLoader.MainMenu => "loading.menu.sub",
            "Muddlebury" => "loading.farm.sub",
            _ => "loading.generic.sub",
        };

        void NextTip()
        {
            var tips = UiText.KeysWithPrefix("tip.");
            if (tips.Count == 0) { _tip.text = ""; return; }
            int i = _rng.Next(tips.Count);
            if (tips.Count > 1 && i == _tipIndex) i = (i + 1) % tips.Count;
            _tipIndex = i;
            _tip.text = UiText.Get(tips[i]);
        }

        void Build()
        {
            if (_panel != null) return;
            _panel = UiPanel.Create(transform, "UI_Loading", 900);
            var r = _panel.Root;
            r.pickingMode = PickingMode.Ignore;
            r.Query<VisualElement>().ForEach(e => e.pickingMode = PickingMode.Ignore);
            _card = r.Q("loading-card");
            _title = r.Q<Label>("loading-title"); _sub = r.Q<Label>("loading-sub");
            _tip = r.Q<Label>("loading-tip"); _tipHead = r.Q<Label>("loading-tip-head");
            _runner = r.Q("loading-runner");
            var wheelHost = r.Q("loading-wheel");
            wheelHost?.Add(new WagonWheel(84f));
            r.style.display = DisplayStyle.None;
        }
    }
}
