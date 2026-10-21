using System;
using System.Collections.Generic;
using System.Text;
using HarvestPanic.Core;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace HarvestPanic.Economy
{
    /// <summary>
    /// Host-authoritative economy + day loop (day-loop, shared-wallet-economy, week-quota, blame-ledger,
    /// landlord-events light). Registers <see cref="IEconomyService"/> and <see cref="IDayModifierService"/>.
    /// Clients mirror state through NetworkVariables and RPCs; mutators are ignored on clients.
    /// Offline (not spawned and no listening NetworkManager) it runs locally for dev scenes and tests.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class EconomyDirector : NetworkBehaviour, IEconomyService, IDayModifierService, IDayModifierWriter, IDayClockService
    {
        /// <summary>The live director (one per session).</summary>
        public static EconomyDirector Instance { get; private set; }

        [SerializeField] EconomyTuning tuning;
        [SerializeField, Tooltip("Local HUD instantiated on every peer.")] GameObject hudPrefab;
        [SerializeField, Tooltip("Spawn goods at Goods markers each morning.")] bool spawnGoodsAtMarkers = true;
        [SerializeField, Tooltip("Run offline when no network session starts within this many seconds.")] float offlineStartDelay = 0.25f;

        // ---- replicated ----
        readonly NetworkVariable<int> _nvBalance = new NetworkVariable<int>();
        readonly NetworkVariable<byte> _nvPhase = new NetworkVariable<byte>();
        readonly NetworkVariable<float> _nvRemaining = new NetworkVariable<float>();
        readonly NetworkVariable<float> _nvDuration = new NetworkVariable<float>();
        readonly NetworkVariable<int> _nvCalendar = new NetworkVariable<int>(); // dayIndex | week<<16 | dayOfWeek<<26
        readonly NetworkVariable<int> _nvQuota = new NetworkVariable<int>();
        readonly NetworkVariable<int> _nvIou = new NetworkVariable<int>();
        readonly NetworkVariable<int> _nvWeekIncome = new NetworkVariable<int>();
        readonly NetworkVariable<FixedString512Bytes> _nvOrder = new NetworkVariable<FixedString512Bytes>();
        readonly NetworkVariable<FixedString4096Bytes> _nvMods = new NetworkVariable<FixedString4096Bytes>();

        // ---- server state ----
        Wallet _wallet;
        BlameLedger _ledger;
        Market _market;
        readonly DayClock _clock = new DayClock();
        readonly DayModifierStack _mods = new DayModifierStack();
        readonly List<GameObject> _spawned = new List<GameObject>();
        readonly List<MarketPen> _pens = new List<MarketPen>();
        readonly List<DayModifier> _landlordRules = new List<DayModifier>();
        int _players = 1, _quota, _weekIncome, _dayCosts, _orderBonusToday;
        float _dayTime, _graceUntil = -1f, _remainingPushTimer, _clientRemainingAt;
        string _clientModsKey = "";
        bool _started;
        System.Random _rng = new System.Random(Environment.TickCount);

        /// <summary>Last receipt (payout or Sunday) on this peer.</summary>
        public ReceiptData LastReceipt { get; private set; }

        public event Action<WalletTxn> OnWalletTxn;
        public event Action<BlameEvent> OnDisaster;
        public event Action OnModifiersChanged;
        /// <summary>Raised on every peer when the phase changes (old, new).</summary>
        public event Action<DayPhase, DayPhase> OnPhaseChanged;
        /// <summary>Raised on every peer when a receipt arrives.</summary>
        public event Action<ReceiptData> OnReceipt;

        public EconomyTuning Tuning => tuning != null ? tuning : (tuning = EconomyTuning.Load());
        public bool IsEconomyAuthority => IsSpawned ? IsServer : !NetUtil.IsListening;
        bool ReadRemote => IsSpawned && !IsServer;

        // ---------------- read API (HUD) ----------------
        public int Balance => ReadRemote ? _nvBalance.Value : (_wallet?.Balance ?? 0);
        public DayPhase Phase => ReadRemote ? (DayPhase)_nvPhase.Value : _clock.Phase;
        public float PhaseRemaining => ReadRemote ? Mathf.Max(0f, _nvRemaining.Value - (Time.time - _clientRemainingAt)) : _clock.Remaining;
        public float PhaseDuration => ReadRemote ? _nvDuration.Value : _clock.Duration;
        public int DayIndex => ReadRemote ? (_nvCalendar.Value & 0xFFFF) : _clock.DayIndex;
        public int Week => ReadRemote ? ((_nvCalendar.Value >> 16) & 0x3FF) : _clock.Week;
        public int DayOfWeek => ReadRemote ? ((_nvCalendar.Value >> 26) & 0x1F) : _clock.DayOfWeek;
        public int Quota => ReadRemote ? _nvQuota.Value : _quota;
        public int Iou => ReadRemote ? _nvIou.Value : (_wallet?.IouThisWeek ?? 0);
        /// <summary>Net income banked toward this week's rent (balance − week start balance).</summary>
        public int WeekIncome => ReadRemote ? _nvWeekIncome.Value : _weekIncome;
        public bool IsMarketOpen => Phase == DayPhase.Workday || Phase == DayPhase.Dusk;
        public bool InSundownGrace => _graceUntil > 0f && Time.time <= _graceUntil;
        public bool FreshnessRunning => Phase == DayPhase.Workday;
        /// <summary>True when the local peer may press host buttons.</summary>
        public bool LocalIsHost => !IsSpawned || IsServer;
        int IDayModifierService.DayIndex => DayIndex;

        /// <summary>Order lines (mirrored on clients).</summary>
        public IReadOnlyList<OrderLine> Order => ReadRemote ? ParseOrder(_nvOrder.Value.ToString()) : (_market != null ? _market.Order : (IReadOnlyList<OrderLine>)Array.Empty<OrderLine>());

        /// <summary>Active modifier assets (mirrored on clients).</summary>
        public IEnumerable<DayModifier> ActiveModifiers { get { foreach (var a in _mods.ActiveList) yield return a.Mod; } }

        // ---------------- lifecycle ----------------

        void Awake()
        {
            _wallet = new Wallet(Tuning);
            _ledger = new BlameLedger(Tuning);
            _market = new Market(Tuning);
            _clock.DurationOf = BaseDuration;
            _clock.DaysPerWeek = Tuning.DaysPerWeek;
            _clock.PhaseTimedOut += OnPhaseTimedOut;
            _wallet.Applied += t => { if (t.Type != WalletTxnType.Quota) _weekIncome += t.Amount; };
            _ledger.Disaster += e => RaiseDisaster(e);
            _mods.Changed += () => { PushMods(); OnModifiersChanged?.Invoke(); };
            OnPhaseChanged += LandlordPhaseChanged;
            ShopAwake();
        }

        void OnEnable()
        {
            Instance = this;
            Services.Register<IEconomyService>(this);
            Services.Register<IDayModifierService>(this);
            Services.Register<IDayModifierWriter>(this);
            Services.Register<IDayClockService>(this);
        }

        void OnDisable()
        {
            Services.Unregister<IEconomyService>(this);
            Services.Unregister<IDayModifierService>(this);
            Services.Unregister<IDayModifierWriter>(this);
            Services.Unregister<IDayClockService>(this);
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            EnsureHud();
            if (!IsSpawned) Invoke(nameof(OfflineStart), offlineStartDelay);
        }

        void OfflineStart()
        {
            if (!IsSpawned && !NetUtil.IsListening && !_started) StartRun();
        }

        public override void OnNetworkSpawn()
        {
            Instance = this;
            if (IsServer) { if (!_started) StartRun(); }
            else
            {
                _nvPhase.OnValueChanged += (a, b) => { RebuildClientMods(); OnPhaseChanged?.Invoke((DayPhase)a, (DayPhase)b); };
                _nvRemaining.OnValueChanged += (a, b) => _clientRemainingAt = Time.time;
                _nvMods.OnValueChanged += (a, b) => RebuildClientMods();
                RebuildClientMods();
                HookClientShop();
            }
        }

        void EnsureHud()
        {
            if (hudPrefab == null || Application.isBatchMode && !Application.isEditor && SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            if (FindFirstObjectByType<EconomyHudMarker>() != null) return;
            var hud = Instantiate(hudPrefab);
            hud.name = hudPrefab.name;
        }

        float BaseDuration(DayPhase p)
        {
            switch (p)
            {
                case DayPhase.Morning: return Tuning.MorningSeconds;
                case DayPhase.Workday: return _mods.Resolve(DayModifierStack.Params.WorkdaySeconds, Tuning.WorkdaySeconds, DayPhase.Workday);
                case DayPhase.Dusk: return _mods.Resolve(DayModifierStack.Params.DuskSeconds, Tuning.DuskSeconds, DayPhase.Dusk);
                case DayPhase.Payout: return Tuning.PayoutAutoAdvanceSeconds;
                case DayPhase.Shop: return Tuning.ShopAutoAdvanceSeconds;
                case DayPhase.Collateral: return Tuning.CollateralSeconds;
                default: return 0f;
            }
        }

        void Update()
        {
            if (!IsEconomyAuthority || !_started) return;
            float dt = Time.deltaTime;
            _clock.DurationScale = EconomyDevCvars.DayScale;
            _clock.Tick(dt);
            _mods.Tick(dt);
            if (_clock.Phase == DayPhase.Workday || _clock.Phase == DayPhase.Dusk) _dayTime += dt;
            ShopTick(dt);
            LandlordTick();
            if (IsSpawned && (_remainingPushTimer -= dt) <= 0f) { _remainingPushTimer = 0.5f; _nvRemaining.Value = _clock.Remaining; }
        }

        // ---------------- run / phases (server) ----------------

        /// <summary>Host: new run from Week 1 Day 1.</summary>
        public void StartRun()
        {
            if (!IsEconomyAuthority) return;
            _started = true;
            if (TryContinueRun()) return;
            _wallet.Reset();
            _ledger.StartWeek();
            _mods.Clear();
            _landlordRules.Clear();
            _clock.ResetRun();
            _weekIncome = 0;
            ShopResetRun();
            SampleWeek();
            LandlordPlanWeek();
            PushWallet();
            Go(DayPhase.Morning);
            LandlordBark(LandlordText.WeekOneWelcome(_quota)); // bot-week-report #4: week 1 had no Landlord line at all
        }

        void SampleWeek()
        {
            _players = CountPlayers();
            _quota = EconomyMath.Quota(_clock.Week, _players, Tuning);
            _ledger.WeeklyQuota = _quota;
            if (IsSpawned) _nvQuota.Value = _quota;
        }

        int CountPlayers()
        {
            if (Services.TryGet(out ISessionService s) && s.Players != null && s.Players.Count > 0) return Mathf.Clamp(s.Players.Count, 1, GameInfo.MaxPlayers);
            if (NetUtil.IsListening && NetworkManager.Singleton.IsServer) return Mathf.Clamp(NetworkManager.Singleton.ConnectedClientsIds.Count, 1, GameInfo.MaxPlayers);
            return 1;
        }

        void Go(DayPhase to)
        {
            var from = _clock.Phase;
            _clock.DurationScale = EconomyDevCvars.DayScale;
            _clock.GoTo(to);
            switch (to)
            {
                case DayPhase.Morning: EnterMorning(); break;
                case DayPhase.Payout: EnterPayout(); break;
                case DayPhase.Shop: EnterShop(); break;
            }
            if (IsSpawned)
            {
                _nvPhase.Value = (byte)to;
                _nvDuration.Value = _clock.Duration;
                _nvRemaining.Value = _clock.Remaining;
                _nvCalendar.Value = (_clock.DayIndex & 0xFFFF) | ((_clock.Week & 0x3FF) << 16) | ((_clock.DayOfWeek & 0x1F) << 26);
            }
            OnPhaseChanged?.Invoke(from, to);
        }

        void OnPhaseTimedOut(DayPhase p)
        {
            switch (p)
            {
                case DayPhase.Morning: Go(DayPhase.Workday); break;
                case DayPhase.Workday:
                    _graceUntil = Time.time + Tuning.SundownGraceSeconds;
                    foreach (var pen in _pens) if (pen != null) pen.MarkClose();
                    Go(Tuning.DuskEnabled ? DayPhase.Dusk : DayPhase.Payout);
                    break;
                case DayPhase.Dusk:
                    _graceUntil = Time.time + Tuning.SundownGraceSeconds;
                    foreach (var pen in _pens) if (pen != null) pen.MarkClose();
                    Go(DayPhase.Payout);
                    break;
                case DayPhase.Payout: NextDay(); break;
                case DayPhase.Shop: NextDay(); break;
                case DayPhase.Collateral: Evict(); break;
            }
        }

        void EnterMorning()
        {
            _dayTime = 0f; _dayCosts = 0; _orderBonusToday = 0;
            _ledger.StartDay();
            foreach (var q in _mods.TakeQueued())
                if (_mods.Apply(q, _clock.Week) && LandlordParams.IsVisit(q))
                    LandlordBark(q.ModifierId.EndsWith("Inspection") ? LandlordText.InspectionArrive : LandlordText.HelpfulArrive); // deferred visit arrives
            LandlordMorning();
            DrawWeather();
            var available = new List<string>();
            foreach (var m in LevelMarker.FindAll(MarkerRole.Goods))
                if (GoodsResolver.TryResolve(m.Key, out var def) && !available.Contains(def.GoodsId)) available.Add(def.GoodsId);
            foreach (var g in Goods.All) if (g.Definition != null && !available.Contains(g.GoodsId)) available.Add(g.GoodsId);
            int lines = Mathf.RoundToInt(_mods.Resolve(DayModifierStack.Params.OrderLineCount, Tuning.OrderLines, DayPhase.Morning));
            _market.StartDay(_rng.Next(), _clock.Week, _players, lines, available);
            PushOrder();
            SetupPens();
            if (spawnGoodsAtMarkers) RespawnGoods();
        }

        void DrawWeather()
        {
            // weather-twists: the World/Weather IWeatherService owns the daily card and applies it via IDayModifierWriter.
            if (Services.TryGet(out IWeatherService _)) return;
            if (_rng.NextDouble() >= Tuning.WeatherChancePerDay) return;
            var pool = new List<DayModifier>();
            foreach (var m in Content.DayModifiers.All) if (m.Category == DayModifierCategory.Weather) pool.Add(m);
            if (pool.Count > 0) _mods.Apply(pool[_rng.Next(pool.Count)], _clock.Week);
        }

        void DrawLandlordRule()
        {
            var pool = new List<DayModifier>();
            foreach (var m in Content.DayModifiers.All)
                if (LandlordParams.IsRule(m) && !_landlordRules.Contains(m)) pool.Add(m);
            if (pool.Count == 0 || _clock.Week < 2) return;
            var pick = pool[_rng.Next(pool.Count)];
            if (_landlordRules.Count >= Tuning.MaxLandlordRules) { _mods.Remove(_landlordRules[0].ModifierId); _landlordRules.RemoveAt(0); }
            _landlordRules.Add(pick);
            _mods.Apply(pick, _clock.Week, bypassCap: true);
            LandlordBark(LandlordText.NewRule(pick.CardText, pick.ModifierId)); // bot-week-report #4: rules were silent
        }

        void SetupPens()
        {
            _pens.Clear();
            foreach (var m in LevelMarker.FindAll(MarkerRole.MarketPen))
            {
                var pen = m.GetComponent<MarketPen>();
                if (pen == null) pen = m.gameObject.AddComponent<MarketPen>();
                _pens.Add(pen);
            }
            foreach (var pen in FindObjectsByType<MarketPen>(FindObjectsSortMode.None)) if (!_pens.Contains(pen)) _pens.Add(pen);
        }

        void RespawnGoods()
        {
            foreach (var go in _spawned)
            {
                if (go == null) continue;
                var no = go.GetComponent<NetworkObject>();
                if (no != null && no.IsSpawned) no.Despawn(true); else Destroy(go);
            }
            _spawned.Clear();
            // Every marker's own units first (the level's spots), then the crew-size extras (GDD 9.3, tuned 2026-10-21)
            // dealt round-robin over that goods' markers and laid out in the free space around them.
            var markers = LevelMarker.FindAll(MarkerRole.Goods);
            var jobs = new List<(LevelMarker marker, GoodsDefinition def)>();
            foreach (var m in markers)
                if (GoodsResolver.TryResolve(m.Key, out var def) && def.WorldPrefab != null)
                    for (int i = 0; i < m.Count; i++) jobs.Add((m, def));
            foreach (var kv in ExtraGoodsPerMarker(markers, _players, Tuning))
                if (GoodsResolver.TryResolve(kv.Key.Key, out var def) && def.WorldPrefab != null)
                    for (int i = 0; i < kv.Value; i++) jobs.Add((kv.Key, def));
            var placed = new List<Bounds>();
            foreach (var (m, def) in jobs)
            {
                var go = Instantiate(def.WorldPrefab, m.transform.position, m.transform.rotation);
                PlaceResting(go, m.transform.position, placed, Tuning.SpawnSpacing);
                var no = go.GetComponent<NetworkObject>();
                if (no != null && IsSpawned) no.Spawn(true);
                _spawned.Add(go);
            }
            Physics.SyncTransforms();
        }

        /// <summary>Crew-size extras per marker: each goods' <see cref="EconomyMath.ExtraDailyGoods"/> dealt round-robin over its markers.</summary>
        public static Dictionary<LevelMarker, int> ExtraGoodsPerMarker(List<LevelMarker> markers, int players, EconomyTuning t)
        {
            var extra = new Dictionary<LevelMarker, int>();
            var dealt = new HashSet<string>();
            foreach (var m in markers)
            {
                if (!GoodsResolver.TryResolve(m.Key, out var def) || !dealt.Add(def.GoodsId)) continue;
                int n = EconomyMath.ExtraDailyGoods(def.GoodsId, players, t);
                var same = markers.FindAll(x => GoodsResolver.TryResolve(x.Key, out var d2) && d2.GoodsId == def.GoodsId);
                for (int k = 0; k < n; k++) { var at = same[k % same.Count]; extra.TryGetValue(at, out int c); extra[at] = c + 1; }
            }
            return extra;
        }

        const int SpawnCandidates = 32;
        const float GroundProbeUp = 0.5f, GroundProbeDown = 4f, SpawnLift = 0.005f;
        static readonly Collider[] OverlapScratch = new Collider[16];

        /// <summary>
        /// Candidate offset <paramref name="k"/> around a Goods marker (golden-angle spiral, k = 0 is the marker itself).
        /// <paramref name="step"/> is one footprint plus the spacing, so neighbouring candidates don't overlap.
        /// </summary>
        public static Vector3 SpawnCandidateOffset(int k, float step)
        {
            if (k <= 0) return Vector3.zero;
            float a = k * 137.508f * Mathf.Deg2Rad, r = step * (0.8f + 0.5f * Mathf.Sqrt(k));
            return new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
        }

        /// <summary>
        /// Moves a freshly instantiated goods item so it rests on the ground at (or next to) <paramref name="anchor"/>
        /// (fix/beta-bugs-1, bot-week-report #3). The ground is the nearest surface below a probe 0.5 m above the anchor,
        /// so props that overhang the marker (cart, eaves) are never used as a shelf; the item's collider bottom sits
        /// 5 mm above it, so a 180 kg pumpkin no longer drops and bruises itself at Morning. A spot is free when the
        /// footprint clears every item placed this Morning (+<paramref name="spacing"/>) and all static geometry;
        /// the first free spot on <see cref="SpawnCandidateOffset"/>'s spiral wins (the marker itself when free).
        /// Returns false when no candidate was free (the item is then grounded at the anchor, as before).
        /// </summary>
        public static bool PlaceResting(GameObject go, Vector3 anchor, List<Bounds> placed, float spacing)
        {
            Physics.SyncTransforms();
            bool any = false; var b = new Bounds();
            foreach (var c in go.GetComponentsInChildren<Collider>())
            {
                if (c.isTrigger) continue;
                if (!any) { b = c.bounds; any = true; } else b.Encapsulate(c.bounds);
            }
            if (!any) return false;
            Vector3 centreOffset = b.center - go.transform.position;
            float bottomOffset = b.min.y - go.transform.position.y;
            float step = Mathf.Max(b.size.x, b.size.z) + spacing;
            int mask = EnvironmentMask();
            Vector3 fallback = go.transform.position; bool haveFallback = false;
            for (int k = 0; k < SpawnCandidates; k++)
            {
                Vector3 c = anchor + SpawnCandidateOffset(k, step);
                if (!GroundBelow(c, go.transform, mask, out float groundY)) continue;
                var pos = new Vector3(c.x, groundY - bottomOffset + SpawnLift, c.z);
                if (!haveFallback) { fallback = pos; haveFallback = true; }
                var foot = new Bounds(pos + centreOffset, b.size);
                if (Overlaps(foot, placed, spacing) || BlockedByWorld(foot, go.transform, mask)) continue;
                placed.Add(foot);
                MoveTo(go, pos);
                return true;
            }
            if (haveFallback) { placed.Add(new Bounds(fallback + centreOffset, b.size)); MoveTo(go, fallback); }
            return false;
        }

        static bool GroundBelow(Vector3 at, Transform self, int mask, out float y)
        {
            y = 0f;
            var hits = Physics.RaycastAll(at + Vector3.up * GroundProbeUp, Vector3.down, GroundProbeUp + GroundProbeDown, mask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            foreach (var h in hits)
                if (!h.collider.transform.IsChildOf(self) && h.distance < best) { best = h.distance; y = h.point.y; }
            return best < float.MaxValue;
        }

        static bool Overlaps(Bounds foot, List<Bounds> placed, float spacing)
        {
            foreach (var p in placed)
            {
                var grown = p; grown.Expand(new Vector3(spacing, 0f, spacing));
                if (grown.Intersects(foot)) return true;
            }
            return false;
        }

        /// <summary>True when the footprint (lifted 5 cm off the ground it rests on) intersects static/world geometry.</summary>
        static bool BlockedByWorld(Bounds foot, Transform self, int mask)
        {
            var ext = foot.extents; ext.y = Mathf.Max(0.01f, ext.y - 0.05f);
            int n = Physics.OverlapBoxNonAlloc(foot.center + Vector3.up * 0.05f, ext, OverlapScratch, Quaternion.identity, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) if (!OverlapScratch[i].transform.IsChildOf(self)) return true;
            return false;
        }

        static void MoveTo(GameObject go, Vector3 pos)
        {
            go.transform.position = pos;
            if (go.TryGetComponent(out Rigidbody rb)) { rb.position = pos; rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        }

        /// <summary>World geometry for spawn checks: everything except carryables, critters, players and ragdolls.</summary>
        static int EnvironmentMask() =>
            ~(LayerBit(GameLayers.Carryable) | LayerBit(GameLayers.Critter) | LayerBit(GameLayers.Player) | LayerBit(GameLayers.Ragdoll));

        static int LayerBit(string name) { int l = LayerMask.NameToLayer(name); return l < 0 ? 0 : 1 << l; }

        void EnterPayout()
        {
            LandlordPayoutFees();
            _mods.ExpireAt(DayModifierDuration.Day);
            if (_market.OrderComplete)
            {
                _orderBonusToday = EconomyMath.OrderCompleteBonus(_clock.Week, Tuning);
                AddWalletTxn(new WalletTxn { Type = WalletTxnType.OrderBonus, Amount = _orderBonusToday, PlayerId = PlayerIds.None });
            }
            var r = new ReceiptData
            {
                DayIndex = _clock.DayIndex, Week = _clock.Week, DayOfWeek = _clock.DayOfWeek,
                Sales = _market.SalesToday, UnitsSold = _market.UnitsToday, OrderBonus = _orderBonusToday, OrderComplete = _market.OrderComplete,
                Costs = _dayCosts, Balance = _wallet.Balance, Quota = _quota, Iou = _wallet.IouThisWeek,
            };
            r.Net = r.Sales + r.OrderBonus + r.Costs;
            foreach (var l in _market.Order)
                r.Money.Add(new ReceiptLine { Text = LedgerText.OrderLabel(l), Count = l.Filled, Dollars = l.Quantity });
            var head = _ledger.Headline();
            r.Headline = head != null ? NameOf(head.Event.PrimaryInstigator, head.Event.CauseTag) + ": " + LedgerText.Line(head.Event.Verb, LedgerText.ItemName(head.Event.ItemId, head.Event.ItemTag), head.Count) : LedgerText.NoHeadline;
            ReceiptBuilder.AddBlame(r, _ledger.Day, id => NameOf(id, null), Tuning);
            r.Awards.AddRange(AwardPicker.Daily(_ledger.Day, id => NameOf(id, null), _players, Tuning.DailyAwardsMax));
            PublishReceipt(r);
        }

        /// <summary>Host: leave Payout (next day or Sunday), or leave a paid Sunday.</summary>
        public void NextDay()
        {
            if (!IsEconomyAuthority) { if (IsSpawned) RequestNextDayRpc(); return; }
            switch (_clock.Phase)
            {
                case DayPhase.Payout: Go(DayPhase.Shop); break;
                case DayPhase.Shop:
                    _shop.ClearQueue();
                    if (_clock.IsLastDayOfWeek) { Go(DayPhase.Sunday); ResolveSunday(); }
                    else Go(DayPhase.Morning);
                    break;
                case DayPhase.Sunday:
                    _mods.ExpireAt(DayModifierDuration.Week);
                    _wallet.ClearIou();
                    _weekIncome = 0;
                    _ledger.StartWeek();
                    _visitPlan.Clear(); // last week's plan must not fire on the new Monday
                    _players = CountPlayers(); // Monday's goods/order use this week's crew (SampleWeek runs after the week advances)
                    Go(DayPhase.Morning);  // advances week
                    SampleWeek();
                    DrawLandlordRule();
                    LandlordPlanWeek();
                    LandlordMorning(); // today's visit from the NEW plan (Morning ran before the plan existed)
                    PushWallet();
                    SaveRun(RunResumePoint.WeekStart);
                    break;
                case DayPhase.Evicted: RunSaveStore.Delete(); StartRun(); break;
            }
        }

        /// <summary>Host: restart the run from Week 1.</summary>
        public void Restart()
        {
            if (!IsEconomyAuthority) { if (IsSpawned) RequestRestartRpc(); return; }
            RunSaveStore.Delete(); // abandon wipes the run save (save-persistence)
            StartRun();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        void RequestNextDayRpc(RpcParams p = default) { if (p.Receive.SenderClientId == NetworkManager.ServerClientId) NextDay(); }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        void RequestRestartRpc(RpcParams p = default) { if (p.Receive.SenderClientId == NetworkManager.ServerClientId) Restart(); }

        void ResolveSunday()
        {
            LandlordSundayFees();
            int due = _quota + _wallet.IouThisWeek;
            bool paid = _wallet.Balance >= due;
            var r = new ReceiptData
            {
                IsSunday = true, Week = _clock.Week, DayIndex = _clock.DayIndex, Quota = _quota, Iou = _wallet.IouThisWeek, Due = due, Paid = paid,
            };
            ulong mvd = _ledger.MostValuableDisaster();
            r.Mvd = mvd == PlayerIds.None ? "The Weather" : NameOf(mvd, null);
            r.Awards.AddRange(AwardPicker.Weekly(_ledger.Week, id => NameOf(id, null), _players));
            if (paid)
            {
                AddWalletTxn(new WalletTxn { Type = WalletTxnType.Quota, Amount = -due, PlayerId = PlayerIds.None });
                r.Carryover = _wallet.Balance;
                CountPaidWeek();
            }
            r.Balance = _wallet.Balance;
            r.SundayLine = LedgerText.Sunday(paid, r.Carryover);
            if (!paid && _shop.Owned.Count > 0)
            {
                r.Collateral = true;
                r.SundayLine = LedgerText.CollateralLine(due - _wallet.Balance);
                PublishReceipt(r);
                Go(DayPhase.Collateral);
                return;
            }
            if (!paid) { FinishEviction(r); return; }
            PublishReceipt(r);
        }

        // ---------------- IEconomyService ----------------

        public void AddWalletTxn(in WalletTxn txn)
        {
            if (!IsEconomyAuthority) return;
            var t = txn;
            t.DayIndex = _clock.DayIndex;
            t.Timestamp = _dayTime;
            if (!_wallet.Apply(t, out var applied)) return;
            if (applied.Amount < 0 && applied.Type != WalletTxnType.Quota) _dayCosts += applied.Amount;
            PushWallet();
            OnWalletTxn?.Invoke(applied);
            if (IsSpawned) WalletTxnRpc((byte)applied.Type, applied.Amount, applied.PlayerId, applied.GoodsId ?? "");
        }

        [Rpc(SendTo.NotServer)]
        void WalletTxnRpc(byte type, int amount, ulong player, string goodsId) =>
            OnWalletTxn?.Invoke(new WalletTxn { Type = (WalletTxnType)type, Amount = amount, PlayerId = player, GoodsId = goodsId, DayIndex = DayIndex });

        public int ChargeRespawnFee(ulong playerId, BlameVerb cause)
        {
            if (!IsEconomyAuthority) return 0;
            int fee = EconomyMath.RespawnFee(_clock.Week, _mods.Resolve(DayParams.EconomyRespawnFeeMult, 1f, _clock.Phase), Tuning, _players);
            int before = _wallet.Balance, iouBefore = _wallet.IouThisWeek;
            AddWalletTxn(new WalletTxn { Type = WalletTxnType.RespawnFee, Amount = -fee, PlayerId = playerId });
            int charged = before - _wallet.Balance + (_wallet.IouThisWeek - iouBefore);
            EmitBlame(new BlameEvent { Instigators = new[] { playerId }, Verb = cause, DollarDelta = -charged });
            return charged;
        }

        public void EmitBlame(in BlameEvent evt)
        {
            if (!IsEconomyAuthority) return;
            var e = evt;
            e.DayIndex = (short)_clock.DayIndex;
            e.Timestamp = _dayTime;
            _ledger.Submit(e);
        }

        void RaiseDisaster(BlameEvent e)
        {
            OnDisaster?.Invoke(e);
            if (IsSpawned) DisasterRpc(JsonUtility.ToJson(e), NameOf(e.PrimaryInstigator, e.CauseTag));
            else LastDisasterName = NameOf(e.PrimaryInstigator, e.CauseTag);
        }

        /// <summary>Display name of the last disaster's culprit (HUD toast).</summary>
        public string LastDisasterName { get; private set; }

        [Rpc(SendTo.ClientsAndHost)]
        void DisasterRpc(string json, string name)
        {
            LastDisasterName = name;
            if (IsServer) return; // server already raised
            OnDisaster?.Invoke(JsonUtility.FromJson<BlameEvent>(json));
        }

        public bool TrySell(IGoods item, ulong seller, out int finalValue)
        {
            finalValue = 0;
            if (!IsEconomyAuthority || item == null || item.Definition == null) return false;
            if (!IsMarketOpen && !InSundownGrace) return false;
            if ((item.Tags & GoodsTag.Pocketed) != 0) return false;
            finalValue = QuoteValue(item, true);
            bool order = _market.WouldFillOrder(item.GoodsId);
            _market.RecordSale(item.GoodsId, finalValue);
            if (order) PushOrder();
            AddWalletTxn(new WalletTxn { Type = WalletTxnType.Sale, Amount = finalValue, PlayerId = seller, GoodsId = item.GoodsId });
            if (finalValue == 0) OnWalletTxn?.Invoke(new WalletTxn { Type = WalletTxnType.Sale, GoodsId = item.GoodsId, PlayerId = seller });
            if (item is Goods g) g.Remove();
            else if (item.GoodsTransform != null)
            {
                var no = item.GoodsTransform.GetComponentInParent<NetworkObject>();
                if (no != null && no.IsSpawned) no.Despawn(true); else Destroy(item.GoodsTransform.gameObject);
            }
            return true;
        }

        /// <summary>Called by a pen when an item finished resting.</summary>
        internal void SellFromPen(IGoods item, MarketPen pen) => TrySell(item, item.LastHandlerId, out _);

        /// <summary>
        /// Current value of <paramref name="item"/>: base × B × F × D(next unit) × O × Goods.ValueMult × dusk, minus income tax
        /// when <paramref name="forSale"/>.
        /// </summary>
        public int QuoteValue(IGoods item, bool forSale)
        {
            var d = item.Definition;
            if (d == null) return 0;
            var phase = Phase;
            float demand = _market.NextDemand(d.GoodsId, _mods.Resolve(DayModifierStack.Params.DemandMult, 1f, phase));
            float order = _market.WouldFillOrder(d.GoodsId) ? Tuning.OrderMultiplier : 1f;
            float mult = _mods.Resolve(DayModifierStack.Params.GoodsValueMult, 1f, phase);
            if (d.Category == GoodsCategory.Livestock) mult *= _mods.Resolve(LandlordParams.HatlessLivestockValueMult, 1f, phase); // Sunday Best (no hats yet)
            if (phase == DayPhase.Dusk) mult *= _mods.Resolve(DayModifierStack.Params.DuskBonus, Tuning.DuskBonus, phase);
            int v = EconomyMath.SaleValue(d.BaseValue, d.Category, item.BruiseState, item.Freshness, demand, order, mult, Tuning);
            if (!forSale) return v;
            float tax = _mods.Resolve(DayModifierStack.Params.IncomeTaxRate, 0f, phase);
            return tax > 0f ? EconomyMath.RoundHalfUp(v * (1.0 - tax)) : v;
        }

        /// <summary>Today's D_day for loss reports.</summary>
        public float ExpectedDemand(string goodsId) => _market.DayDemand(goodsId);

        // ---------------- IDayModifierService ----------------

        public float Resolve(string paramName, float baseValue) => _mods.Resolve(paramName, baseValue, Phase);
        /// <summary>Alias used by goods/pens.</summary>
        public float ResolveParam(string paramName, float baseValue) => _mods.Resolve(paramName, baseValue, Phase);
        public bool HasWorldTag(string worldTag) => _mods.HasWorldTag(worldTag, Phase);

        /// <summary>Server: applies a modifier (weather, landlord visit, curse...). Rescales the running workday.</summary>
        public bool ApplyModifier(DayModifier mod, bool bypassCap = false)
        {
            if (!IsEconomyAuthority) return false;
            bool ok = _mods.Apply(mod, _clock.Week, bypassCap);
            if (ok && (_clock.Phase == DayPhase.Workday || _clock.Phase == DayPhase.Dusk)) _clock.Rescale(BaseDuration(_clock.Phase));
            return ok;
        }

        DayClockPhase IDayClockService.Phase => (DayClockPhase)(byte)Phase;
        float IDayClockService.PhaseProgress01 => PhaseDuration > 0f ? Mathf.Clamp01(1f - PhaseRemaining / PhaseDuration) : 0f;
        bool IDayModifierWriter.ApplyModifier(DayModifier mod, bool bypassCap) => ApplyModifier(mod, bypassCap);

        // ---------------- replication helpers ----------------

        void PushWallet()
        {
            if (!IsSpawned) return;
            _nvBalance.Value = _wallet.Balance;
            _nvIou.Value = _wallet.IouThisWeek;
            _nvWeekIncome.Value = _weekIncome;
        }

        void PushOrder()
        {
            if (!IsSpawned) return;
            var sb = new StringBuilder();
            foreach (var l in _market.Order) sb.Append(l.GoodsId).Append(':').Append(l.Quantity).Append(':').Append(l.Filled).Append(';');
            _nvOrder.Value = new FixedString512Bytes(sb.ToString());
        }

        void PushMods()
        {
            if (!IsSpawned || !IsServer) return;
            var sb = new StringBuilder();
            foreach (var a in _mods.ActiveList) sb.Append(a.Mod.ModifierId).Append(';');
            _nvMods.Value = new FixedString4096Bytes(sb.ToString());
        }

        void RebuildClientMods()
        {
            if (!ReadRemote) return;
            string key = _nvMods.Value.ToString();
            if (key == _clientModsKey) return;
            _clientModsKey = key;
            _mods.Clear();
            foreach (var id in key.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                if (Content.DayModifiers.TryGet(id, out var m)) _mods.Apply(m, 99, bypassCap: true);
        }

        static List<OrderLine> ParseOrder(string s)
        {
            var list = new List<OrderLine>();
            foreach (var part in s.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var f = part.Split(':');
                if (f.Length == 3 && int.TryParse(f[1], out int q) && int.TryParse(f[2], out int filled))
                    list.Add(new OrderLine { GoodsId = f[0], Quantity = q, Filled = filled });
            }
            return list;
        }

        void PublishReceipt(ReceiptData r)
        {
            LastReceipt = r;
            OnReceipt?.Invoke(r);
            if (IsSpawned) ReceiptRpc(JsonUtility.ToJson(r));
        }

        [Rpc(SendTo.NotServer)]
        void ReceiptRpc(string json)
        {
            LastReceipt = JsonUtility.FromJson<ReceiptData>(json);
            OnReceipt?.Invoke(LastReceipt);
        }

        string NameOf(ulong id, string causeTag)
        {
            if (!PlayerIds.IsPlayer(id)) return LedgerText.NatureName(causeTag);
            if (Services.TryGet(out ISessionService s) && s.Players != null)
                foreach (var p in s.Players) if (p.ClientId == id && !string.IsNullOrEmpty(p.Name)) return p.Name;
            return "Farmer " + (id + 1);
        }

        /// <summary>Test/debug: jump the current timed phase to its end.</summary>
        public void DebugSkipPhase()
        {
            if (!IsEconomyAuthority) return;
            if (_clock.Duration > 0f) _clock.Tick(_clock.Remaining + 0.001f);
            else NextDay();
        }

        /// <summary>Test/debug access to the ledger.</summary>
        public BlameLedger Ledger => _ledger;
        /// <summary>Test/debug access to the wallet.</summary>
        public Wallet WalletState => _wallet;
    }
}
