using System;
using System.Collections.Generic;
using HarvestPanic.Core;
using Unity.Netcode;
using UnityEngine;

namespace HarvestPanic.Weather
{
    /// <summary>Designer knobs (weather-twists §7). Lives on the WeatherDirector prefab.</summary>
    [Serializable]
    public sealed class WeatherTuning
    {
        [Tooltip("Deck weights by WeatherKind: Clear, Rain, Wind, Fog, Heatwave, Storm")]
        public float[] Weights = (float[])WeatherDeck.DefaultWeights.Clone();
        [Tooltip("Wind direction re-rolls every N s (GDD 60)")] public float WindRotateSeconds = 60f;
        [Tooltip("Gust cycle length, s")] public Vector2 GustPeriod = new Vector2(6f, 12f);
        [Tooltip("Only carryables at or below this mass are blown, kg")] public float WindMaxMassKg = 8f;
        [Tooltip("Exposed area of a light carryable, m^2")] public float WindAreaM2 = 0.4f;
        [Tooltip("Cap on wind force as a fraction of the item's weight (never yanks from a full team, §5)")] public float WindMaxForceOfWeight = 0.6f;
        [Tooltip("Design-unit nudge on players at gust peaks (below ragdoll threshold)")] public float PlayerGustImpulse = 120f;
        [Tooltip("Gust slow-down on player speed at peak")] public float PlayerGustSpeedMult = 0.85f;
        [Tooltip("Roof check ray length, m")] public float RoofCheckHeight = 25f;
        [Tooltip("Rescan interval for goods/carryables, s")] public float RescanSeconds = 2f;
    }

    /// <summary>
    /// Host-authoritative weather (weather-twists). Rolls the day's card at Morning from a replicated run seed,
    /// applies the card's Day Modifier (all gameplay numbers live in <c>Mod.Weather.Day.*</c> assets) and runs
    /// the two physical effects that need a driver: rain soak (via <see cref="IGoodsSoakable"/>) and wind gusts on
    /// light carryables. Registers <see cref="IWeatherService"/> for HUD/Audio and a global
    /// <see cref="IMovementModifierSource"/> for slick ground and gusts. Offline (no session) it runs locally.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeatherDirector : NetworkBehaviour, IWeatherService, IMovementModifierSource
    {
        /// <summary>Live director on this peer.</summary>
        public static WeatherDirector Instance { get; private set; }

        [SerializeField] WeatherTuning tuning = new WeatherTuning();
        [SerializeField, Tooltip("Rain streak material (URP particles unlit)")] Material rainMaterial;
        [SerializeField, Tooltip("Leaf material (URP particles unlit)")] Material leafMaterial;
        [SerializeField, Tooltip("Create local visuals (sun/sky/fog/particles)")] bool presentation = true;

        readonly NetworkVariable<byte> _nvKind = new NetworkVariable<byte>();
        readonly NetworkVariable<int> _nvSeed = new NetworkVariable<int>();
        readonly NetworkVariable<Vector3> _nvWind = new NetworkVariable<Vector3>();

        WeatherKind _kind, _localKind;
        int _seed;
        Vector3 _wind;
        DayClockPhase _lastPhase = DayClockPhase.None;
        int _lastDay = -1;
        bool _forced;
        float _windDirTimer, _gustTimer, _gustLength = 8f, _rescanTimer;
        float _windYaw;
        System.Random _windRng;
        readonly List<Rigidbody> _bodies = new List<Rigidbody>();
        readonly List<IGoods> _goods = new List<IGoods>();
        readonly Dictionary<IGoods, float> _exposure = new Dictionary<IGoods, float>();
        readonly HashSet<IGoods> _soaked = new HashSet<IGoods>();
        WeatherPresenter _presenter;

        public event Action<WeatherKind, WeatherKind> OnWeatherChanged;

        /// <summary>Tuning in use.</summary>
        public WeatherTuning Tuning => tuning;
        /// <summary>Run seed (replicated).</summary>
        public int Seed => ReadRemote ? _nvSeed.Value : _seed;
        /// <summary>True when this peer simulates weather.</summary>
        public bool IsAuthority => IsSpawned ? IsServer : !(NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening);
        bool ReadRemote => IsSpawned && !IsServer;

        public WeatherKind Current => ReadRemote ? (WeatherKind)_nvKind.Value : _kind;
        public string ForecastText => WeatherDeck.Forecast(Current);
        public Vector3 Wind => ReadRemote ? _nvWind.Value : _wind;
        /// <summary>0..1 gust strength now (1 at gust peak).</summary>
        public float Gust01 => Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Clamp01(_gustTimer / Mathf.Max(0.1f, _gustLength)))), 2f);

        void OnEnable()
        {
            Instance = this;
            Services.Register<IWeatherService>(this);
            MovementModifiers.Register(this);
        }

        void OnDisable()
        {
            Services.Unregister<IWeatherService>(this);
            MovementModifiers.Unregister(this);
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            if (!IsSpawned && IsAuthority) InitSeed();
            EnsurePresenter();
        }

        public override void OnNetworkSpawn()
        {
            Instance = this;
            if (IsServer) { InitSeed(); _nvKind.Value = (byte)_kind; }
            else
            {
                _localKind = (WeatherKind)_nvKind.Value;
                _nvKind.OnValueChanged += (a, b) => { _localKind = (WeatherKind)b; OnWeatherChanged?.Invoke((WeatherKind)a, (WeatherKind)b); };
                if (_localKind != WeatherKind.Clear) OnWeatherChanged?.Invoke(WeatherKind.Clear, _localKind); // join-in-progress (§5)
            }
            EnsurePresenter();
        }

        void EnsurePresenter()
        {
            if (!presentation || _presenter != null || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            _presenter = gameObject.AddComponent<WeatherPresenter>();
            _presenter.Init(this, rainMaterial, leafMaterial);
        }

        void InitSeed()
        {
            if (_seed != 0) return;
            _seed = int.TryParse(Arg("-hpWeatherSeed"), out int s) ? s : Environment.TickCount;
            if (_seed == 0) _seed = 1;
            _windRng = new System.Random(_seed);
            if (IsSpawned) _nvSeed.Value = _seed;
            if (WeatherDeck.TryParse(Arg("-hpWeather"), out var forced)) ForceWeather(forced);
        }

        static string Arg(string name)
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            return null;
        }

        // ---------------- server ----------------

        /// <summary>Server/dev: sets today's weather and keeps it for the rest of the run (tests, -hpWeather).</summary>
        public void ForceWeather(WeatherKind k)
        {
            if (!IsAuthority) return;
            _forced = true;
            SetWeather(k);
        }

        /// <summary>Server: resumes seeded draws from the next morning.</summary>
        public void ClearForce() => _forced = false;

        /// <summary>Server: draws the card for <paramref name="dayIndex"/> and applies it.</summary>
        public void RollForDay(int dayIndex)
        {
            if (!IsAuthority) return;
            SetWeather(_forced ? _kind : WeatherDeck.Roll(_seed, dayIndex, tuning.Weights));
        }

        void SetWeather(WeatherKind k)
        {
            var old = _kind;
            _kind = k;
            _exposure.Clear(); _soaked.Clear();
            ApplyModifier(k);
            if (IsSpawned) _nvKind.Value = (byte)k;
            if (old != k) OnWeatherChanged?.Invoke(old, k);
        }

        static void ApplyModifier(WeatherKind k)
        {
            if (!Content.DayModifiers.TryGet(WeatherDeck.ModifierId(k), out var mod)) { Debug.LogWarning("[Weather] Missing modifier " + WeatherDeck.ModifierId(k)); return; }
            // The daily card always lands (ReplaceGroup Group.Weather replaces any other weather); bypassCap so the
            // visible weather and its modifier can never disagree.
            if (Services.TryGet(out IDayModifierWriter w)) w.ApplyModifier(mod, true);
        }

        void Update()
        {
            if (!IsAuthority) return;
            if (_seed == 0) InitSeed();
            DetectMorning();
            float dt = Time.deltaTime;
            TickWind(dt);
            if ((_rescanTimer -= dt) <= 0f) { _rescanTimer = tuning.RescanSeconds; Rescan(); }
            TickSoak(dt);
        }

        void DetectMorning()
        {
            DayClockPhase phase = Services.TryGet(out IDayClockService clock) ? clock.Phase : DayClockPhase.None;
            int day = Services.TryGet(out IDayModifierService mods) ? mods.DayIndex : -1;
            bool enteredMorning = phase == DayClockPhase.Morning && _lastPhase != DayClockPhase.Morning;
            bool noClockNewDay = clock == null && mods != null && day != _lastDay;
            bool newDay = mods != null && day != _lastDay;
            if (enteredMorning || noClockNewDay || newDay) RollForDay(Mathf.Max(0, day));
            else if (mods != null && (phase == DayClockPhase.Morning || phase == DayClockPhase.Workday || phase == DayClockPhase.Dusk)
                     && !mods.HasWorldTag(WeatherDeck.WorldTag(_kind)) && Content.DayModifiers.TryGet(WeatherDeck.ModifierId(_kind), out _))
                ApplyModifier(_kind); // stack was cleared (run restart) or the day service arrived late
            _lastPhase = phase; _lastDay = day;
        }

        float Resolve(string p, float b) => Services.TryGet(out IDayModifierService d) ? d.Resolve(p, b) : b;

        void TickWind(float dt)
        {
            float speed = Resolve(DayParams.WeatherWindSpeed, 0f);
            if (speed <= 0f) { SetWind(Vector3.zero); return; }
            if (_windRng == null) _windRng = new System.Random(_seed);
            if ((_windDirTimer -= dt) <= 0f) { _windDirTimer = tuning.WindRotateSeconds; _windYaw = (float)_windRng.NextDouble() * 360f; }
            _gustTimer += dt;
            if (_gustTimer >= _gustLength)
            {
                _gustTimer = 0f;
                _gustLength = Mathf.Lerp(tuning.GustPeriod.x, tuning.GustPeriod.y, (float)_windRng.NextDouble());
            }
            float v = speed * (0.5f + 0.5f * Gust01); // GDD 4-12 m/s around the card's 8
            SetWind(Quaternion.Euler(0f, _windYaw, 0f) * Vector3.forward * v);
        }

        void SetWind(Vector3 w)
        {
            _wind = w;
            if (IsSpawned && (_nvWind.Value - w).sqrMagnitude > 0.04f) _nvWind.Value = w;
        }

        void Rescan()
        {
            _bodies.Clear(); _goods.Clear();
            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (mb is IGoods g && !_goods.Contains(g)) _goods.Add(g);
                if (mb is ICarryable c && c.MassKg <= tuning.WindMaxMassKg && c.CarryTransform != null)
                {
                    var rb = c.CarryTransform.GetComponent<Rigidbody>();
                    if (rb != null && !_bodies.Contains(rb)) _bodies.Add(rb);
                }
            }
        }

        void FixedUpdate()
        {
            if (!IsAuthority || _wind.sqrMagnitude < 0.01f) return;
            float v2 = _wind.sqrMagnitude;
            Vector3 dir = _wind.normalized;
            float force = WindForce(v2, tuning.WindAreaM2);
            for (int i = _bodies.Count - 1; i >= 0; i--)
            {
                var rb = _bodies[i];
                if (rb == null) { _bodies.RemoveAt(i); continue; }
                if (rb.isKinematic) continue;
                float cap = rb.mass * Physics.gravity.magnitude * tuning.WindMaxForceOfWeight;
                rb.AddForce(dir * Mathf.Min(force, cap), ForceMode.Force);
            }
            if (Gust01 > 0.98f && !_gustPushed) { _gustPushed = true; PushPlayers(dir); }
            if (Gust01 < 0.5f) _gustPushed = false;
        }

        bool _gustPushed;

        void PushPlayers(Vector3 dir)
        {
            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (mb is IKnockable k && !k.IsDown) k.ApplyImpulse(dir * tuning.PlayerGustImpulse, PlayerIds.Nature, BlameVerb.KnockedOut);
        }

        /// <summary>Wind force on a body (weather-twists §4): 0.5 * rho(1.2) * v^2 * A * Cd(1.0).</summary>
        public static float WindForce(float speedSquared, float areaM2) => 0.5f * 1.2f * speedSquared * areaM2;

        void TickSoak(float dt)
        {
            float soakAfter = Resolve(DayParams.WeatherSoakSeconds, 0f);
            if (soakAfter <= 0f) return;
            int mask = ~((1 << LayerMask.NameToLayer(GameLayers.Carryable)) | (1 << LayerMask.NameToLayer(GameLayers.Player)) | (1 << LayerMask.NameToLayer(GameLayers.Critter)) | (1 << LayerMask.NameToLayer(GameLayers.Water)) | (1 << LayerMask.NameToLayer(GameLayers.Interactable)));
            for (int i = _goods.Count - 1; i >= 0; i--)
            {
                var g = _goods[i];
                if (g == null || (g is UnityEngine.Object o && o == null) || g.GoodsTransform == null) { _goods.RemoveAt(i); continue; }
                if (_soaked.Contains(g) || !(g is IGoodsSoakable soak)) continue;
                if (Physics.Raycast(g.GoodsTransform.position + Vector3.up * 0.6f, Vector3.up, tuning.RoofCheckHeight, mask, QueryTriggerInteraction.Ignore)) continue; // under a roof
                _exposure.TryGetValue(g, out float t);
                t += dt;
                _exposure[g] = t;
                if (t >= soakAfter) { _soaked.Add(g); soak.ApplySoak(PlayerIds.Nature); }
            }
        }

        /// <summary>Number of goods soaked by rain today (tests/QA).</summary>
        public int SoakedToday => _soaked.Count;

        // ---------------- movement (every peer) ----------------

        /// <inheritdoc/>
        public float GetSpeedMultiplier(Vector3 position)
        {
            float m = Resolve(DayParams.WeatherGroundSpeedMult, 1f);
            var w = Wind;
            float speed = Resolve(DayParams.WeatherWindSpeed, 0f);
            if (speed > 0f) m *= Mathf.Lerp(1f, tuning.PlayerGustSpeedMult, Mathf.Clamp01(w.magnitude / speed - 0.5f) * 2f);
            return m;
        }
    }
}
