using System;
using System.Collections.Generic;
using HarvestPanic.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace HarvestPanic.AI
{
    /// <summary>
    /// Server-authoritative critter brain (critter-ai §3.1): Idle / Wander / Alert (0.4 s tell) / Flee / Lured /
    /// Cornered / Carried / Stunned / Calm. Ticks at <see cref="CritterTuning.TickHz"/> on the server (or offline when
    /// not spawned); clients only read the replicated <see cref="State"/> for presentation.
    /// All species behaviour comes from the <see cref="CritterPersonality"/> asset.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class CritterBrain : NetworkBehaviour
    {
        [SerializeField] CritterPersonality personality;
        [SerializeField, Tooltip("Optional override; defaults to Resources/Data/Critters/CritterTuning")] CritterTuning tuning;
        [SerializeField, Tooltip("Sight origin; defaults to transform + 0.5 m")] Transform eye;
        [SerializeField, Tooltip("Debug: log state changes")] bool logStateChanges;

        readonly NetworkVariable<CritterState> _netState = new NetworkVariable<CritterState>(CritterState.Idle);
        readonly List<Vector3> _threatPos = new List<Vector3>(16);
        readonly List<float> _threatW = new List<float>(16);
        readonly Collider[] _overlap = new Collider[16];
        readonly HashSet<int> _seenRoots = new HashSet<int>();
        NavMeshPath _path;

        NavMeshAgent _agent;
        Rigidbody _rb;
        CritterTuning _t;
        ThreatTracker _threats;
        CritterState _state = CritterState.Idle;
        float _stateTime, _tickAccum, _now;
        float _idleUntil, _belowSince, _cornerUntil, _lastFleeQuery = -99f, _stunUntil, _airborneSince;
        float _lureAffinity, _lureUntil, _eatUntil;
        bool _hasLure, _stunTimerStarted;
        Vector3 _home, _lurePos, _fleeTarget, _fleeDir, _scatterDir;
        float _scatterUntil;
        Vector3 _lastProgressPos; float _lastProgressTime;
        int _playerMask, _losMask;
        float _alarmBoost;

        /// <summary>Species data.</summary>
        public CritterPersonality Personality => personality;
        /// <summary>Shared tuning in use.</summary>
        public CritterTuning Tuning => _t != null ? _t : (tuning != null ? tuning : CritterTuning.Default);
        /// <summary>Current state (replicated value on clients).</summary>
        public CritterState State => IsSpawned && !IsServer ? _netState.Value : _state;
        /// <summary>Seconds in the current state (authority only).</summary>
        public float StateTime => _stateTime;
        /// <summary>Total threat 0..1 from the last tick (authority only).</summary>
        public float Threat => _threats?.Total ?? 0f;
        /// <summary>Threat list (authority only; debug/tests).</summary>
        public ThreatTracker Threats => _threats;
        /// <summary>True on the server, or offline when the object is not network-spawned.</summary>
        public bool HasAuthority => !IsSpawned || IsServer;
        /// <summary>Flock this critter belongs to (Flock herd mode only).</summary>
        public CritterFlock Flock { get; private set; }
        /// <summary>Current flee/lure/juke destination (debug).</summary>
        public Vector3 CurrentTarget => _fleeTarget;
        /// <summary>Home point for idle wandering.</summary>
        public Vector3 Home { get => _home; set => _home = value; }
        /// <summary>Extra flee-speed multiplier (GrumpyMods, set by <see cref="CritterGoods"/>).</summary>
        public float SpeedMultiplier { get; set; } = 1f;
        /// <summary>Server: while Time.time is below this, Idle/Wander do not pick new wander targets (signature tasks such as a goat headbutt telegraph or perch).</summary>
        public float ExternalHoldUntil { get; set; }

        /// <summary>Follow herd: the current bell-wether of this critter's flock (null when none / re-electing).</summary>
        public CritterBrain FlockLeader => Flock != null && Flock.Mode == HerdMode.Follow ? Flock.Leader(Time.time, Tuning.LeaderReelectSeconds) : null;
        /// <summary>Follow herd: true when this critter is the bell-wether.</summary>
        public bool IsFlockLeader => ReferenceEquals(FlockLeader, this);
        /// <summary>Follow herd: true when this critter follows a leader that is being carried (it trails the carrier).</summary>
        public bool FollowingCarriedLeader
        {
            get { var l = FlockLeader; return l != null && l != this && l.State == CritterState.Carried; }
        }

        /// <summary>Raised on every peer when the state changes (old, new).</summary>
        public event Action<CritterState, CritterState> StateChanged;

        /// <summary>Assigns the personality from code (spawners, tests) before the first tick.</summary>
        public void SetPersonality(CritterPersonality p) { personality = p; ApplyAgentSettings(); }

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _rb = GetComponent<Rigidbody>();
            _t = tuning != null ? tuning : CritterTuning.Default;
            _threats = new ThreatTracker(_t);
            _path = new NavMeshPath();
            _home = transform.position;
            _playerMask = LayerMask.GetMask(GameLayers.Player);
            _losMask = ~LayerMask.GetMask(GameLayers.Player, GameLayers.Critter, GameLayers.Carryable, GameLayers.Interactable,
                GameLayers.Water, GameLayers.Ragdoll, "Ignore Raycast");
            _tickAccum = Random.value / Mathf.Max(1f, _t.TickHz); // stagger ticks across critters
            ApplyAgentSettings();
        }

        void OnEnable()
        {
            CritterRegistry.Add(this);
            if (personality != null && (personality.HerdMode == HerdMode.Flock || personality.HerdMode == HerdMode.Follow) && Flock == null)
                Flock = CritterFlock.Join(this, Tuning.FlockJoinRadius);
        }

        void OnDisable()
        {
            CritterRegistry.Remove(this);
            Flock?.Leave(this);
            Flock = null;
        }

        /// <inheritdoc/>
        public override void OnNetworkSpawn()
        {
            _netState.OnValueChanged += OnNetStateChanged;
            if (IsServer) _netState.Value = _state;
            else if (_agent != null) _agent.enabled = false; // clients follow NetworkTransform
        }

        /// <inheritdoc/>
        public override void OnNetworkDespawn() => _netState.OnValueChanged -= OnNetStateChanged;

        void OnNetStateChanged(CritterState prev, CritterState cur) { if (!IsServer) StateChanged?.Invoke(prev, cur); }

        void ApplyAgentSettings()
        {
            if (_agent == null || personality == null) return;
            _agent.speed = personality.MoveSpeed;
            _agent.acceleration = personality.Acceleration;
            _agent.angularSpeed = 720f;
            _agent.autoBraking = true;
            _agent.stoppingDistance = 0.2f;
        }

        void Update()
        {
            if (!HasAuthority || personality == null) return;
            float dt = Time.deltaTime;
            _now = Time.time;
            _stateTime += dt;
            _tickAccum += dt;
            float tick = 1f / Mathf.Max(1f, _t.TickHz);
            if (_tickAccum >= tick)
            {
                float tickDt = _tickAccum;
                _tickAccum = 0f;
                CritterRegistry.EnsureNoiseHook();
                if (_state != CritterState.Carried) Perceive(tickDt);
                Think();
            }
            if (_state == CritterState.Alert) FaceThreat(dt);
        }

        // ---------------------------------------------------------------- perception

        void Perceive(float dt)
        {
            _threats.BeginTick();
            Vector3 pos = transform.position;
            Vector3 eyePos = eye != null ? eye.position : pos + Vector3.up * 0.5f;
            int n = Physics.OverlapSphereNonAlloc(pos, personality.AlertRadius, _overlap, _playerMask, QueryTriggerInteraction.Ignore);
            _seenRoots.Clear();
            for (int i = 0; i < n; i++)
            {
                var c = _overlap[i];
                Transform root = c.attachedRigidbody != null ? c.attachedRigidbody.transform : c.transform.root;
                if (!_seenRoots.Add(root.GetInstanceID())) continue;
                var no = root.GetComponentInParent<NetworkObject>();
                ulong clientId = no != null && no.IsSpawned ? no.OwnerClientId : PlayerIds.None;
                int key = clientId != PlayerIds.None ? ThreatTracker.PlayerKey(clientId) : root.GetInstanceID();
                Vector3 target = c.bounds.center;
                Vector3 to = target - pos;
                float dist = new Vector2(to.x, to.z).magnitude;
                float p = CritterMath.Proximity(dist, personality.AlertRadius);
                bool sight = CritterMath.InSightCone(transform.forward, to, personality.SightCone)
                             && !Physics.Linecast(eyePos, target, _losMask, QueryTriggerInteraction.Ignore);
                _threats.Observe(key, clientId, root.position, _now, p, sight);
            }
            if (_alarmBoost > 0f) _alarmBoost = Mathf.Max(0f, _alarmBoost - _t.NoiseDecayPerSecond * dt);
            _threats.Recompute(dt);
        }

        /// <summary>Server: reacts to a noise (threat, lure or ignore per the personality's tag responses).</summary>
        public void HearNoise(in NoiseEvent e)
        {
            if (personality == null || _state == CritterState.Carried) return;
            if (IsSpawned && e.InstigatorId != 0 && e.InstigatorId == NetworkObjectId) return; // own squeal
            float d = Vector3.Distance(transform.position, e.Location);
            float perceived = CritterMath.PerceivedNoise(e, d, personality, _t.GlobalNoiseMult);
            float falloff = e.Radius > 0f ? Mathf.Clamp01(1f - d / e.Radius) : 0f;
            if (falloff <= 0f) return;
            if ((e.Tags & NoiseTag.Mandrake) != 0 && d <= _t.MandrakeSpookRadius)
                perceived = Mathf.Max(perceived, CritterMath.MandrakeSpook(personality.FleeThreshold)); // cursed-crops AC 2: livestock within 20 m flee
            else if (personality.TryGetNoiseResponse(e.Tags, out var resp))
            {
                switch (resp.Reaction)
                {
                    case NoiseReaction.Lure:
                        float aff = CritterMath.LureAffinityFor(personality, CritterMath.LureTagFor(e.Tags), resp.Strength);
                        SetLure(e.Location, aff);
                        return;
                    case NoiseReaction.Rally:
                        if (resp.Strength > 0f) SetLure(e.Location, resp.Strength * falloff); // sheep long whistle: come to the whistler
                        return;
                    case NoiseReaction.Ignore:
                        return;
                    case NoiseReaction.Flee:
                        perceived = Mathf.Max(perceived, resp.Strength * falloff * personality.NoiseSensitivity);
                        break;
                }
            }
            _threats.AddNoise(e.BlamedPlayerId, e.Location, perceived);
            _threats.Recompute(0f);
        }

        /// <summary>Server: registers a food lure at <paramref name="position"/> (also called by feed-bucket style tools).</summary>
        public void SetLure(Vector3 position, float affinity)
        {
            _hasLure = true;
            _lurePos = position;
            _lureAffinity = Mathf.Max(_hasLure && _now < _lureUntil ? _lureAffinity : 0f, affinity);
            _lureUntil = Time.time + _t.LureMemorySeconds;
        }

        /// <summary>Server: removes the active lure (bucket picked up).</summary>
        public void ClearLure() { _hasLure = false; }

        /// <summary>Server: damage threat (hit, throw) from <paramref name="clientId"/>.</summary>
        public void AddDamage(ulong clientId, Vector3 from) { _threats.AddDamage(clientId, from); _threats.Recompute(0f); }

        /// <summary>Server: a flock mate panicked (chain reaction).</summary>
        public void ReceiveFlockAlarm(Vector3 threatCentroid, float threat)
        {
            if (_state == CritterState.Carried || _state == CritterState.Stunned) return;
            _threats.AddNoise(PlayerIds.None, threatCentroid, threat);
            _threats.Recompute(0f);
            if (_state != CritterState.Flee && _state != CritterState.Alert) Enter(CritterState.Alert);
        }

        // ---------------------------------------------------------------- FSM

        void Think()
        {
            float T = _threats.Total;
            if (FollowingCarriedLeader) T *= _t.FollowCarriedThreatScale; // sheep trail whoever carries the bell-wether
            if (_hasLure && _now > _lureUntil) _hasLure = false;
            switch (_state)
            {
                case CritterState.Idle:
                case CritterState.Wander:
                    if (TryLure(T)) break;
                    if (T >= personality.AlertThreshold) { Enter(CritterState.Alert); break; }
                    if (_now >= ExternalHoldUntil) TickIdleWander();
                    break;

                case CritterState.Alert:
                    if (_stateTime < _t.AlertTellSeconds) break; // readable tell
                    if (T >= personality.FleeThreshold) { Enter(CritterState.Flee); break; }
                    if (TryLure(T)) break;
                    if (T < personality.AlertThreshold)
                    {
                        if (_belowSince < 0f) _belowSince = _now;
                        if (_now - _belowSince >= _t.AlertDecaySeconds) Enter(CritterState.Idle);
                    }
                    else _belowSince = -1f;
                    break;

                case CritterState.Flee:
                    if (T < personality.CalmThreshold) { Enter(CritterState.Calm); break; }
                    TickFlee(T);
                    break;

                case CritterState.Calm:
                    if (T >= personality.FleeThreshold || T >= personality.CalmThreshold * 2f) { Enter(CritterState.Flee); break; }
                    if (T >= personality.CalmThreshold) _belowSince = _now;
                    if (_now - _belowSince >= personality.CalmTime) { _home = transform.position; Enter(CritterState.Idle); }
                    break;

                case CritterState.Lured:
                    TickLured(T);
                    break;

                case CritterState.Cornered:
                    if (personality.CorneredResponse == CorneredResponse.Charge) TryChargeHit();
                    if (_now >= _cornerUntil) Enter(T >= personality.CalmThreshold ? CritterState.Flee : CritterState.Idle);
                    else if (personality.CorneredResponse == CorneredResponse.Juke && AgentReady && !_agent.pathPending && _agent.remainingDistance < 0.5f)
                        Enter(CritterState.Flee);
                    break;

                case CritterState.Carried:
                    break; // driven by CritterCarryable

                case CritterState.Stunned:
                    TickStunned(T);
                    break;
            }
            TickStuck();
        }

        void Enter(CritterState next)
        {
            var prev = _state;
            if (prev == next) return;
            _state = next;
            _stateTime = 0f;
            _belowSince = next == CritterState.Calm ? _now : -1f;
            if (IsSpawned && IsServer) _netState.Value = next;
            if (logStateChanges) Debug.Log($"[Critter] {name}: {prev} -> {next} (T={Threat:0.00})", this);

            switch (next)
            {
                case CritterState.Idle:
                    _idleUntil = _now + Random.Range(_t.IdleMinSeconds, _t.IdleMaxSeconds);
                    Stop();
                    break;
                case CritterState.Alert:
                    Stop();
                    break;
                case CritterState.Flee:
                    _lastFleeQuery = -99f;
                    if (Flock != null) StartScatter();
                    TickFlee(_threats.Total);
                    break;
                case CritterState.Calm:
                    SetSpeed(personality.MoveSpeed * _t.WanderSpeedFraction);
                    break;
                case CritterState.Lured:
                    _eatUntil = -1f;
                    MoveTo(_lurePos, personality.MoveSpeed);
                    break;
                case CritterState.Cornered:
                    EnterCornered();
                    break;
            }
            StateChanged?.Invoke(prev, next);
        }

        bool TryLure(float threat)
        {
            if (!_hasLure) return false;
            float d = Vector3.Distance(transform.position, _lurePos);
            if (!CritterMath.LureWins(_lureAffinity, d, threat, _t.LureRange, _t.LureThreatFactor)) return false;
            Enter(CritterState.Lured);
            return true;
        }

        void TickIdleWander()
        {
            if (!AgentReady) return;
            if (TickFollowLeader()) return;
            if (_state == CritterState.Idle)
            {
                if (_now < _idleUntil) return;
                Vector3 centre = _home;
                if (Flock != null && Flock.Mode == HerdMode.Flock && Flock.ShouldRegroup(_now, _t.FlockRegroupSeconds)) centre = Flock.Centre();
                Vector3 rnd = Random.insideUnitSphere * _t.WanderRadius; rnd.y = 0f;
                if (Flock != null) rnd *= 0.4f;
                if (NavMesh.SamplePosition(centre + rnd, out var hit, 2f, _agent.areaMask))
                {
                    MoveTo(hit.position, personality.MoveSpeed * _t.WanderSpeedFraction);
                    Enter(CritterState.Wander);
                }
                else _idleUntil = _now + 1f;
            }
            else if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.1f)
                Enter(CritterState.Idle);
        }

        float _followRepathAt;

        /// <summary>Follow herd: walk to 2-4 m from the bell-wether when it is further than FollowMaxDistance. True = handled.</summary>
        bool TickFollowLeader()
        {
            var leader = FlockLeader;
            if (leader == null || leader == this) return false;
            Vector3 lp = leader.transform.position;
            bool carried = leader.State == CritterState.Carried;
            if (!FlockRules.NeedsToFollow(lp, transform.position, _t.FollowMaxDistance))
            {
                if (_state == CritterState.Wander && !_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.1f) Enter(CritterState.Idle);
                return true; // close enough: graze beside the leader instead of wandering off home
            }
            if (_now < _followRepathAt && _state == CritterState.Wander) return true;
            _followRepathAt = _now + 0.5f;
            float spread = ((GetInstanceID() & 0xff) / 255f - 0.5f) * 120f;
            Vector3 p = FlockRules.FollowPoint(lp, transform.position, _t.FollowMinDistance, _t.FollowMaxDistance, Random.value, spread);
            if (!NavMesh.SamplePosition(p, out var hit, 2.5f, _agent.areaMask)) return false;
            MoveTo(hit.position, FlockRules.FollowSpeed(personality.MoveSpeed, personality.SprintSpeed * _t.GlobalSpeedMult, carried, _t.FollowCarriedSpeedMult));
            if (_state != CritterState.Wander) Enter(CritterState.Wander);
            return true;
        }

        void TickLured(float threat)
        {
            if (threat > _t.LuredBreakAbove) { _hasLure = false; Enter(CritterState.Flee); return; }
            if (!_hasLure) { Enter(CritterState.Idle); return; }
            if (threat >= _t.LuredIgnoreBelow && !CritterMath.LureWins(_lureAffinity, Vector3.Distance(transform.position, _lurePos), threat, _t.LureRange, _t.LureThreatFactor))
            { Enter(CritterState.Alert); return; }
            float d = Vector3.Distance(transform.position, _lurePos);
            if (_eatUntil < 0f)
            {
                if (d <= _t.EatDistance) { _eatUntil = _now + personality.EatTime; Stop(); }
                else if (AgentReady && (_agent.destination - _lurePos).sqrMagnitude > 0.25f) MoveTo(_lurePos, personality.MoveSpeed);
            }
            else if (_now >= _eatUntil)
            {
                _hasLure = false;
                _home = transform.position;
                Enter(CritterState.Idle);
            }
        }

        void TickFlee(float threat)
        {
            if (!AgentReady) return;
            float speed = personality.SprintSpeed * SpeedMultiplier * _t.GlobalSpeedMult;
            SetSpeed(speed);
            if (_now - _lastFleeQuery < _t.FleeQueryInterval) return;
            bool needNew = _lastFleeQuery < 0f || _agent.pathPending == false && _agent.remainingDistance < 4f;
            _threats.Collect(_threatPos, _threatW);
            Vector3 pos = transform.position;
            Vector3 dir = CritterMath.FleeDirection(pos, _threatPos, _threatW, transform.forward);
            if (Flock != null && _now < _scatterUntil) dir = _scatterDir;
            else if (Flock != null && Flock.Mode == HerdMode.Follow) dir = Flock.ShareStampede(dir, _now, _t.StampedeShareSeconds); // stampede together
            if (!needNew && Vector3.Dot(dir, _fleeDir) < 0.5f) needNew = true;
            if (!needNew && _now - _lastFleeQuery < 1f) return;
            _lastFleeQuery = _now;
            _fleeDir = dir;

            Vector3 centroid = CritterMath.ThreatCentroid(_threatPos, _threatW, pos - dir);
            if (TryPickFleePoint(pos, dir, centroid, out var target, out float costRatio)
                && !(costRatio > _t.CorneredCostRatio && _threats.PlayersWithin(pos, _t.CorneredPlayerRadius) >= _t.CorneredPlayersNeeded))
            {
                _fleeTarget = target;
                _agent.SetDestination(target);
            }
            else if (_threats.PlayersWithin(pos, _t.CorneredPlayerRadius) >= _t.CorneredPlayersNeeded)
            {
                Enter(CritterState.Cornered);
            }
        }

        /// <summary>
        /// Samples up to 9 NavMesh points in a fan around <paramref name="dir"/>, scores them by alignment and distance
        /// from the threat centroid, and validates the best few with a real path. Returns the path/straight cost ratio.
        /// </summary>
        bool TryPickFleePoint(Vector3 pos, Vector3 dir, Vector3 centroid, out Vector3 best, out float costRatio)
        {
            best = pos; costRatio = float.PositiveInfinity;
            Span<float> offsets = stackalloc float[] { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 125f, -125f };
            Span<Vector3> pts = stackalloc Vector3[offsets.Length];
            Span<float> scores = stackalloc float[offsets.Length];
            float dist = _t.FleeDistance;
            int count = 0;
            for (int i = 0; i < offsets.Length; i++)
            {
                Vector3 d = Quaternion.Euler(0f, offsets[i], 0f) * dir;
                if (!NavMesh.SamplePosition(pos + d * dist, out var hit, _t.FleeSampleRadius, _agent.areaMask)) continue;
                Vector3 h = hit.position;
                Vector3 fromC = h - centroid; fromC.y = 0f;
                float score = Vector3.Dot(d, dir) + fromC.magnitude / dist;
                pts[count] = h; scores[count] = score; count++;
            }
            for (int tries = 0; tries < 4 && count > 0; tries++)
            {
                int bi = 0;
                for (int i = 1; i < count; i++) if (scores[i] > scores[bi]) bi = i;
                Vector3 cand = pts[bi];
                scores[bi] = float.NegativeInfinity;
                if (!NavMesh.CalculatePath(pos, cand, _agent.areaMask, _path) || _path.status != NavMeshPathStatus.PathComplete) continue;
                float len = 0f;
                var corners = _path.corners;
                for (int c = 1; c < corners.Length; c++) len += Vector3.Distance(corners[c - 1], corners[c]);
                float straight = Mathf.Max(0.5f, Vector3.Distance(pos, cand));
                if ((cand - pos).sqrMagnitude < 4f) continue; // nowhere to go
                best = cand; costRatio = len / straight;
                return true;
            }
            return false;
        }

        void StartScatter()
        {
            Vector3 pos = transform.position;
            _threats.Collect(_threatPos, _threatW);
            Vector3 away = CritterMath.FleeDirection(pos, _threatPos, _threatW, transform.forward);
            if (Flock.Mode == HerdMode.Follow)
            {
                // Sheep: one shared heading for the whole flock (they panic together, not apart).
                Vector3 h = Flock.ShareStampede(away, _now, _t.StampedeShareSeconds);
                _scatterDir = Quaternion.Euler(0f, Random.Range(-_t.StampedeJitterDegrees, _t.StampedeJitterDegrees), 0f) * h;
                _scatterUntil = _now + _t.StampedeShareSeconds;
                Flock.Scatter(this, CritterMath.ThreatCentroid(_threatPos, _threatW, pos), _now, personality.FleeThreshold + 0.1f);
                return;
            }
            Vector3 outward = pos - Flock.Centre(); outward.y = 0f;
            outward = outward.sqrMagnitude > 0.01f ? outward.normalized : Random.insideUnitSphere;
            outward.y = 0f;
            Vector3 dir = (away + outward.normalized * _t.FlockOutwardWeight).normalized;
            if (dir.sqrMagnitude < 0.01f) dir = away;
            _scatterDir = Quaternion.Euler(0f, Random.Range(-_t.FlockScatterJitterDegrees, _t.FlockScatterJitterDegrees), 0f) * dir;
            _scatterUntil = _now + 2f;
            Flock.Scatter(this, CritterMath.ThreatCentroid(_threatPos, _threatW, pos), _now, personality.FleeThreshold + 0.1f);
        }

        void EnterCornered()
        {
            _cornerUntil = _now + Random.Range(_t.CorneredMinSeconds, _t.CorneredMaxSeconds);
            _chargeHit = false;
            _threats.Collect(_threatPos, _threatW);
            Vector3 pos = transform.position;
            switch (personality.CorneredResponse)
            {
                case CorneredResponse.Charge:
                    var s = _threats.Strongest();
                    if (s != null) MoveTo(s.Position, personality.SprintSpeed * _t.GlobalSpeedMult);
                    break;
                case CorneredResponse.Juke:
                case CorneredResponse.Burrow:
                case CorneredResponse.Teleport:
                    Vector3 gap = CritterMath.WidestGapDirection(pos, _threatPos, transform.forward);
                    if (AgentReady && NavMesh.SamplePosition(pos + gap * 8f, out var hit, 3f, _agent.areaMask))
                    {
                        _fleeTarget = hit.position;
                        MoveTo(hit.position, personality.SprintSpeed * _t.JukeSpeedMult * SpeedMultiplier * _t.GlobalSpeedMult);
                    }
                    break;
                default: // Freeze, PlayDead
                    Stop();
                    break;
            }
        }

        bool _chargeHit;

        /// <summary>Charge: knock the first player in reach via <see cref="IKnockable"/> (ChargeImpulse is in design units).</summary>
        void TryChargeHit()
        {
            if (_chargeHit || personality.ChargeImpulse <= 0f) return;
            float reach = (_agent != null ? _agent.radius : 0.5f) + _t.ChargeHitReach;
            int n = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up * 0.5f, reach, _overlap, _playerMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                if (!Knockables.TryGetFor(_overlap[i].gameObject, out var k) || k.IsDown) continue;
                Vector3 dir = _overlap[i].transform.position - transform.position; dir.y = 0f;
                dir = (dir.sqrMagnitude > 1e-4f ? dir.normalized : transform.forward) + Vector3.up * 0.3f;
                k.ApplyImpulse(dir.normalized * personality.ChargeImpulse, PlayerIds.Nature, BlameVerb.KnockedOut);
                _chargeHit = true;
                Stop();
                return;
            }
        }

        void TickStunned(float threat)
        {
            if (!_stunTimerStarted)
            {
                bool grounded = _rb == null || _rb.isKinematic || _rb.linearVelocity.sqrMagnitude < 0.25f;
                if (grounded || _now - _airborneSince > _t.MaxAirSeconds) { _stunTimerStarted = true; _stunUntil = _now + _stunDuration; }
                return;
            }
            if (_now < _stunUntil) return;
            RecoverToNavMesh();
            Enter(threat >= personality.CalmThreshold ? CritterState.Flee : CritterState.Alert);
        }

        float _stunDuration;

        void TickStuck()
        {
            bool moving = _state == CritterState.Flee || _state == CritterState.Wander || _state == CritterState.Lured && _eatUntil < 0f;
            if (!moving || !AgentReady) { _lastProgressTime = _now; _lastProgressPos = transform.position; return; }
            if ((transform.position - _lastProgressPos).sqrMagnitude > 0.25f) { _lastProgressTime = _now; _lastProgressPos = transform.position; return; }
            float stuck = _now - _lastProgressTime;
            if (stuck >= _t.StuckWarpSeconds)
            {
                Vector3 r = Random.insideUnitSphere * 3f; r.y = 0f;
                if (NavMesh.SamplePosition(transform.position + r, out var hit, 4f, _agent.areaMask)) _agent.Warp(hit.position);
                _lastProgressTime = _now;
            }
            else if (stuck >= _t.StuckSeconds && _state == CritterState.Flee) _lastFleeQuery = -99f;
            else if (stuck >= _t.StuckSeconds && _state == CritterState.Wander) Enter(CritterState.Idle);
        }

        // ---------------------------------------------------------------- carry hooks (CritterCarryable)

        /// <summary>Server: first holder grabbed the critter. Disables navigation; the carry system moves the transform.</summary>
        public void BeginCarried()
        {
            if (_agent != null && _agent.enabled) { if (_agent.isOnNavMesh) _agent.ResetPath(); _agent.enabled = false; }
            if (_rb != null) { _rb.isKinematic = true; }
            Enter(CritterState.Carried);
        }

        /// <summary>
        /// Server: released (dropped, thrown or broke free). Physics takes over and the critter is Stunned for
        /// <paramref name="stunSeconds"/> after landing. <paramref name="velocity"/> is applied when non-zero.
        /// </summary>
        public void EndCarried(Vector3 velocity, float stunSeconds)
        {
            if (_rb != null)
            {
                _rb.isKinematic = false;
                _rb.interpolation = RigidbodyInterpolation.Interpolate;
                _rb.useGravity = true;
                _rb.linearVelocity = velocity;
                _rb.angularVelocity = Vector3.zero;
            }
            _stunDuration = stunSeconds;
            _stunTimerStarted = false;
            _airborneSince = Time.time;
            Enter(CritterState.Stunned);
        }

        void RecoverToNavMesh()
        {
            Vector3 p = transform.position;
            if (_rb != null) { _rb.linearVelocity = Vector3.zero; _rb.angularVelocity = Vector3.zero; _rb.isKinematic = true; _rb.interpolation = RigidbodyInterpolation.None; }
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            if (_agent == null) return;
            if (NavMesh.SamplePosition(p, out var hit, 4f, NavMesh.AllAreas))
            {
                _agent.enabled = true;
                _agent.Warp(hit.position);
            }
            else _agent.enabled = true;
        }

        // ---------------------------------------------------------------- movement helpers

        bool AgentReady => _agent != null && _agent.enabled && _agent.isOnNavMesh;

        void MoveTo(Vector3 p, float speed)
        {
            if (!AgentReady) return;
            SetSpeed(speed);
            _agent.isStopped = false;
            _agent.SetDestination(p);
        }

        void SetSpeed(float s) { if (_agent != null) _agent.speed = s; }

        void Stop()
        {
            if (!AgentReady) return;
            _agent.ResetPath();
            _agent.velocity = Vector3.zero;
        }

        void FaceThreat(float dt)
        {
            var s = _threats.Strongest();
            if (s == null) return;
            Vector3 to = s.Position - transform.position; to.y = 0f;
            if (to.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), 540f * dt);
        }

        // ---------------------------------------------------------------- debug

#if UNITY_EDITOR
        static readonly Color[] StateColors =
        {
            new Color(0.4f, 0.9f, 0.4f), new Color(0.5f, 0.8f, 1f), new Color(1f, 0.85f, 0.2f), new Color(1f, 0.3f, 0.2f),
            new Color(1f, 0.6f, 0.9f), new Color(0.8f, 0.2f, 1f), new Color(1f, 1f, 1f), new Color(0.5f, 0.5f, 0.5f), new Color(0.3f, 0.7f, 0.6f)
        };

        void OnDrawGizmosSelected()
        {
            if (personality == null) return;
            Vector3 pos = transform.position;
            var col = StateColors[Mathf.Clamp((int)State, 0, StateColors.Length - 1)];
            UnityEditor.Handles.color = col;
            UnityEditor.Handles.DrawWireDisc(pos, Vector3.up, personality.AlertRadius);
            float half = personality.SightCone * 0.5f;
            UnityEditor.Handles.color = new Color(col.r, col.g, col.b, 0.08f);
            UnityEditor.Handles.DrawSolidArc(pos + Vector3.up * 0.05f, Vector3.up, Quaternion.Euler(0f, -half, 0f) * transform.forward, personality.SightCone, personality.AlertRadius);
            if (_threats != null)
            {
                foreach (var s in _threats.Sources)
                {
                    Gizmos.color = Color.Lerp(Color.yellow, Color.red, s.Value);
                    Gizmos.DrawLine(pos + Vector3.up * 0.5f, s.Position + Vector3.up * 0.5f);
                    Gizmos.DrawWireSphere(s.Position, 0.2f + s.Value * 0.5f);
                }
            }
            if (State == CritterState.Flee || State == CritterState.Cornered) { Gizmos.color = Color.red; Gizmos.DrawLine(pos, _fleeTarget); Gizmos.DrawWireCube(_fleeTarget, Vector3.one * 0.4f); }
            if (_hasLure) { Gizmos.color = Color.magenta; Gizmos.DrawWireSphere(_lurePos, 0.5f); }
            Gizmos.color = Color.green; Gizmos.DrawWireSphere(_home, 0.3f);
            UnityEditor.Handles.Label(pos + Vector3.up * 1.5f, $"{State}  T={Threat:0.00}");
        }
#endif
    }
}
