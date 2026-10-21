using System.Collections.Generic;
using HarvestPanic.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace HarvestPanic.Gameplay
{
    /// <summary>
    /// Lasso (design/gdd/farm-tools.md rule 3, section 4 tug-of-war, section 5 edge cases; physics-carry rule 7 tethers;
    /// GDD 5.8-5.9). Hold Use to aim (twirl + arc preview), release to throw. The server resolves the throw (12 m range,
    /// 0.9 m catch radius around the aim point), flies a loop (server-simulated, replicated) and on a hit ties a rope
    /// (6 m, tightening from the throw distance):
    /// <list type="bullet">
    /// <item>Critters (Medium and up) stay AI-driven: the rope constrains their NavMesh agent at its length, so they
    /// run circles at the rope's end. If <c>CritterPullN</c> beats the summed player anchors
    /// (<see cref="LassoMath.PlayerAnchor"/>), the holder is ragdolled and dragged (the "water-ski") for a few seconds.
    /// When the rope pulls a critter where it cannot walk (off the bank), it is yanked off its feet and dragged
    /// kinematically; over water it falls in (LassoedIntoRiver).</item>
    /// <item>Goods/props are towed by a <see cref="ConfigurableJoint"/> (hard linear limit, breaks at 4000 N).</item>
    /// <item>Knocked-out teammates are dragged through <see cref="IDraggable"/>; awake teammates make the loop bounce.</item>
    /// </list>
    /// Hold Use while tethered to reel in (2 m/s, 10 stamina/s while the rope pulls); at 1.5 m the catch is grabbed
    /// automatically and the lasso goes back on the belt. E near a post or the wagon hitch ties the rope there (3 m);
    /// the coil stays at the knot until someone picks it up again. Anything a tethered body ends up in the river by
    /// blames the lasser with LassoedIntoRiver.
    /// </summary>
    public sealed class LassoTool : CarryableProp, IUsableTool, IToolArmPose, IToolInteract, IStaysOutWhenReleased
    {
        enum TargetMode : byte { None, Critter, Body, Player }
        enum AnchorMode : byte { Holder, Static, Body }

        [SerializeField] ToolTuning tuning;
        [SerializeField, Tooltip("Rope from the coil to the loop / catch")] LineRenderer rope;
        [SerializeField, Tooltip("The loop (twirling, flying, around the catch)")] LineRenderer loop;
        [SerializeField, Tooltip("Owner-only aim arc + landing ring")] LineRenderer aimArc;
        [SerializeField] LineRenderer aimRing;
        [SerializeField, Tooltip("Coil mesh root")] Transform coil;
        [SerializeField, Tooltip("Right-hand target (first-person rig reads 'Grip')")] Transform grip;
        [SerializeField, Tooltip("Left-hand target ('GripL')")] Transform gripL;

        const int RopeSegments = 18, LoopSegments = 20;
        static readonly List<LassoTool> Active = new List<LassoTool>();
        static readonly RaycastHit[] Hits = new RaycastHit[24];
        static readonly Collider[] Overlaps = new Collider[40];
        static readonly Dictionary<string, CritterPersonality> PersonalityByGoods = new Dictionary<string, CritterPersonality>();
        static readonly Vector3[] RopePts = new Vector3[RopeSegments + 1];
        static readonly Vector3[] LoopPts = new Vector3[LoopSegments];
        static readonly Vector3[] ArcPts = new Vector3[RopeSegments + 1];

        readonly NetworkVariable<byte> _phase = new NetworkVariable<byte>((byte)LassoPhase.Idle);
        readonly NetworkVariable<ulong> _targetId = new NetworkVariable<ulong>(PlayerCarry.NoObject);
        readonly NetworkVariable<float> _length = new NetworkVariable<float>(6f);
        readonly NetworkVariable<Vector3> _loopPos = new NetworkVariable<Vector3>(Vector3.zero);
        readonly NetworkVariable<byte> _anchorMode = new NetworkVariable<byte>((byte)AnchorMode.Holder);
        readonly NetworkVariable<ulong> _anchorId = new NetworkVariable<ulong>(PlayerCarry.NoObject);
        readonly NetworkVariable<Vector3> _anchorPoint = new NetworkVariable<Vector3>(Vector3.zero);
        readonly NetworkVariable<bool> _reeling = new NetworkVariable<bool>(false);
        readonly NetworkVariable<ulong> _lasser = new NetworkVariable<ulong>(PlayerIds.None);

        // ---- server state ----
        PlayerCarry _user;
        PlayerCharacter _holderPc;
        NetworkObject _target;
        TargetMode _mode;
        NavMeshAgent _agent;
        Rigidbody _targetRb;
        ConfigurableJoint _joint;
        PlayerCharacter _targetPlayer;
        IGoods _goods;
        ICarryable _targetCarry;
        ICarryableEvents _targetEvents;
        CritterPersonality _personality;
        NetworkObject _anchorObj;
        Rigidbody _anchorRb;
        Vector3 _flyFrom, _flyTo, _retractFrom;
        float _flyStart, _flyDur, _retractStart;
        NetworkObject _flyTarget;
        LassoVerdict _flyVerdict;
        bool _yanked, _waterSki, _tying;
        float _waterSkiUntil, _nextDrag, _nextTouch, _overSince = -1f;

        // ---- presentation state ----
        float _twirl, _reelPhase, _poseBlend;
        Vector3 _coilRest, _gripRest, _gripLRest;
        bool _restCached;

        /// <summary>Current phase (every peer).</summary>
        public LassoPhase Phase => (LassoPhase)_phase.Value;
        /// <summary>True while a rope is on something.</summary>
        public bool IsTethered => Phase == LassoPhase.Tethered;
        /// <summary>True while tethered and the rope is tied to a post or the wagon instead of held.</summary>
        public bool IsTied => IsTethered && (AnchorMode)_anchorMode.Value != AnchorMode.Holder;
        /// <summary>NetworkObjectId of the catch (<see cref="PlayerCarry.NoObject"/> when none).</summary>
        public ulong TargetId => _targetId.Value;
        /// <summary>Current rope length (m).</summary>
        public float RopeLength => _length.Value;
        /// <summary>True while the holder reels in.</summary>
        public bool IsReeling => _reeling.Value;
        /// <summary>Client id blamed for what the rope does.</summary>
        public ulong Lasser => _lasser.Value;
        /// <summary>Server: the holder is being dragged by the catch (tests).</summary>
        public bool IsWaterSkiing => _waterSki;
        /// <summary>Server: the critter is yanked off its feet (tests).</summary>
        public bool IsYanked => _yanked;
        /// <summary>Server: verdict of the last throw (tests).</summary>
        public LassoVerdict LastVerdict => _flyVerdict;
        /// <summary>Server: what the last throw's aim ray hit and picked (QA logs).</summary>
        public string LastThrowInfo { get; private set; } = string.Empty;
        /// <summary>Tuning in use.</summary>
        public ToolTuning Tuning => tuning;

        /// <inheritdoc />
        public bool StayOut => _tying || IsTied;

        /// <summary>Server: true when some lasso is tethered to <paramref name="no"/> (the wagon will not load it).</summary>
        public static bool IsTetherTarget(NetworkObject no)
        {
            if (no == null) return false;
            foreach (var l in Active) if (l._target == no) return true;
            return false;
        }

        /// <inheritdoc />
        public string UsePrompt
        {
            get
            {
                switch (Phase)
                {
                    case LassoPhase.Aiming: return "RMB: Release to throw";
                    case LassoPhase.Tethered: return IsTied ? string.Empty : "RMB: Hold to reel in";
                    case LassoPhase.Flying: return string.Empty;
                    default: return "RMB: Aim lasso";
                }
            }
        }

        /// <inheritdoc />
        public ArmPose ArmPose
        {
            get
            {
                switch (Phase)
                {
                    case LassoPhase.Aiming: return ArmPose.LassoTwirl;
                    case LassoPhase.Tethered: return IsTied ? ArmPose.Tool : _reeling.Value ? ArmPose.LassoReel : ArmPose.LassoHold;
                    default: return ArmPose.Tool;
                }
            }
        }

        /// <inheritdoc />
        public string InteractPrompt
        {
            get
            {
                if (!IsTethered || IsTied) return null;
                var me = PlayerCharacter.Local;
                if (me != null && t != null && LassoTiePoint.TryFindNearest(me.transform.position, t.TieRadius, out var tie)) return "E: Tie to " + tie.Label;
                return "E: Let go";
            }
        }

        ToolTuning t => tuning;

        protected override void Awake()
        {
            base.Awake();
            if (tuning == null) tuning = ScriptableObject.CreateInstance<ToolTuning>();
            CacheRest();
        }

        void CacheRest()
        {
            if (_restCached) return;
            _restCached = true;
            if (coil != null) _coilRest = coil.localPosition;
            if (grip != null) _gripRest = grip.localPosition;
            if (gripL != null) _gripLRest = gripL.localPosition;
        }

        /// <summary>Editor builder: wires children.</summary>
        public void Configure(ToolTuning tt, LineRenderer ropeLine, LineRenderer loopLine, LineRenderer arc, LineRenderer ring, Transform coilRoot, Transform gripR, Transform gripLeft)
        {
            tuning = tt; rope = ropeLine; loop = loopLine; aimArc = arc; aimRing = ring; coil = coilRoot; grip = gripR; gripL = gripLeft;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsServer) CarryHooks.Claiming += OnClaiming;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer) { EndTether(false); CarryHooks.Claiming -= OnClaiming; }
            base.OnNetworkDespawn();
        }

        // ================================================================================================ server: input

        /// <inheritdoc />
        public void ServerUse(bool down, PlayerCarry user)
        {
            if (!IsServer || user == null) return;
            if (down)
            {
                if (Phase == LassoPhase.Idle) { _user = user; _phase.Value = (byte)LassoPhase.Aiming; }
                else if (Phase == LassoPhase.Tethered && !IsTied && user == _user && !_waterSki) _reeling.Value = true;
                return;
            }
            if (Phase == LassoPhase.Aiming && user == _user) Throw(user);
            _reeling.Value = false;
        }

        /// <inheritdoc />
        public bool ServerInteract(PlayerCarry user)
        {
            if (!IsServer || !IsTethered || IsTied || _waterSki || user == null || user != _user) return false;
            if (!LassoTiePoint.TryFindNearest(user.transform.position, t.TieRadius, out var tie)) return false;
            if (tie.Net != null && _target != null && tie.Net == _target) return false; // no tying a rope to its own catch
            _tying = true;
            SetAnchor(tie.Body != null ? AnchorMode.Body : AnchorMode.Static, tie.Net, tie.Body, tie.Point);
            float dist = LassoMath.FlatDistance(tie.Point, TargetPoint(_target));
            SetLength(Mathf.Max(t.HitchLength, dist));
            _reeling.Value = false;
            SetDrain(0f);
            user.ServerRelease(HandlingMode.Dropped); // StayOut: the belt leaves the coil out
            _tying = false;
            _user = null;
            if (Body != null) Body.isKinematic = true;
            ServerTeleport(tie.Point, Quaternion.LookRotation(Vector3.forward));
            return true;
        }

        public override void OnGrabbed(ulong clientId)
        {
            base.OnGrabbed(clientId);
            if (!IsServer) return;
            var pc = PlayerCharacter.Find(clientId);
            _user = pc != null ? pc.Carry : null;
            if (IsTied && pc != null)
            {
                // Untie: whoever picks the coil up off the post holds the rope (and takes the blame from now on).
                SetAnchor(AnchorMode.Holder, pc.NetworkObject, null, Vector3.zero);
                _holderPc = pc;
                _lasser.Value = clientId;
                SetLength(Mathf.Max(t.LassoLength, LassoMath.FlatDistance(pc.transform.position, TargetPoint(_target))));
            }
        }

        public override void OnReleased(ulong clientId)
        {
            base.OnReleased(clientId);
            if (!IsServer || _tying) return;
            _reeling.Value = false;
            if (Phase == LassoPhase.Aiming) _phase.Value = (byte)LassoPhase.Idle;
            else if (IsTethered && !IsTied && !_waterSki) EndTether(false); // let go of the rope
        }

        public override void ApplyThrow(Vector3 velocity, ulong thrower)
        {
            if (IsServer && Phase != LassoPhase.Idle) EndTether(false);
            if (IsServer) _phase.Value = (byte)LassoPhase.Idle;
            base.ApplyThrow(velocity, thrower);
        }

        void OnClaiming(NetworkObject body, object claimant)
        {
            if (claimant is LassoTool || !IsTethered || body == null || body != _target) return;
            EndTether(false); // a player grabbed the catch, or it went into the wagon
        }

        // ================================================================================================ server: throw

        static int AimMask => ~LayerMask.GetMask(GameLayers.Water, "Ignore Raycast", "UI", GameLayers.Interactable);

        void Throw(PlayerCarry user)
        {
            var pc = user.Character;
            var head = pc.CameraPivot != null ? pc.CameraPivot : pc.HeadPivot;
            Vector3 origin = head.position, dir = head.forward;
            float range = t.LassoRange;

            // Aim point: first thing under the reticle within range (not us, not the coil).
            Vector3 aim = origin + dir * range;
            Collider direct = null;
            int n = Physics.RaycastNonAlloc(origin, dir, Hits, range + 1f, AimMask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var h = Hits[i];
                if (h.distance >= best || h.collider.transform.IsChildOf(pc.transform) || h.collider.transform.IsChildOf(transform)) continue;
                best = h.distance; aim = h.point; direct = h.collider;
            }
            bool groundAim = direct == null;
            if (groundAim && Physics.Raycast(aim + Vector3.up * 0.5f, Vector3.down, out var gh, 30f, AimMask, QueryTriggerInteraction.Ignore)) aim = gh.point;

            // Candidate: the thing under the reticle, else the nearest lassoable thing within the catch radius.
            NetworkObject cand = null; LassoVerdict verdict = LassoVerdict.Nothing; Vector3 candPoint = aim;
            if (direct != null && Classify(direct, pc, out var dNo, out var dv)) { cand = dNo; verdict = dv; }
            if (cand == null || verdict != LassoVerdict.Hit)
            {
                int m = Physics.OverlapSphereNonAlloc(aim, t.LassoCatchRadius, Overlaps, ~0, QueryTriggerInteraction.Ignore);
                float bd = float.MaxValue;
                for (int i = 0; i < m; i++)
                {
                    if (!Classify(Overlaps[i], pc, out var no, out var v)) continue;
                    if (no == cand) continue;
                    float d = (Overlaps[i].bounds.center - aim).sqrMagnitude;
                    bool better = v == LassoVerdict.Hit && (verdict != LassoVerdict.Hit || d < bd);
                    if (cand == null || better) { if (better || cand == null) { cand = no; verdict = v; bd = d; } }
                }
            }
            if (cand != null) candPoint = TargetPoint(cand);
            LastThrowInfo = $"aim {aim} direct {(direct != null ? direct.name + "/" + LayerMask.LayerToName(direct.gameObject.layer) : "none")} cand {(cand != null ? cand.name : "none")} {verdict}";

            _flyFrom = transform.position;
            _flyVerdict = verdict;
            _flyTarget = verdict == LassoVerdict.Hit ? cand : null;
            _flyTo = cand != null ? candPoint : aim;
            Vector3 flat = _flyTo - pc.transform.position; flat.y = 0f;
            if (flat.magnitude > range) _flyTo = pc.transform.position + flat.normalized * range + Vector3.up * (_flyTo.y - pc.transform.position.y);
            _flyStart = Time.time;
            _flyDur = Mathf.Max(0.18f, LassoMath.FlightTime(Vector3.Distance(_flyFrom, _flyTo), t.LassoLoopSpeed));
            _lasser.Value = user.OwnerClientId;
            _holderPc = pc;
            _loopPos.Value = _flyFrom;
            _phase.Value = (byte)LassoPhase.Flying;
        }

        /// <summary>
        /// Server: whether <paramref name="c"/> belongs to something a lasso can rope, and the rule verdict for it.
        /// Returns false for scenery, tools and the wagon (the loop just lands there).
        /// </summary>
        bool Classify(Collider c, PlayerCharacter thrower, out NetworkObject no, out LassoVerdict verdict)
        {
            no = null; verdict = LassoVerdict.Nothing;
            if (c == null) return false;
            float range = t.LassoRange;
            var victim = c.GetComponentInParent<PlayerCharacter>();
            if (victim != null)
            {
                no = victim.NetworkObject;
                verdict = LassoMath.Evaluate(LassoTargetKind.Player, SizeClass.Medium, victim.State == PlayerState.KnockedOut && victim.CarriedBy == PlayerIds.None,
                    victim == thrower, LassoMath.FlatDistance(thrower.transform.position, victim.transform.position), range);
                return true;
            }
            var carry = c.GetComponentInParent<ICarryable>();
            if (carry == null || carry is ISelfSimulatedCarryable || IsTool(carry)) return false;
            no = (carry as Component) != null ? ((Component)carry).GetComponentInParent<NetworkObject>() : null;
            if (no == null || !no.IsSpawned) return false;
            var go = carry.CarryTransform.gameObject;
            bool critter = IsCritter(go);
            var cb = carry.CarryTransform.GetComponent<CarryBody>();
            bool held = cb != null && cb.GripCount > 0;
            var p = critter ? FindPersonality(go) : null;
            verdict = LassoMath.Evaluate(critter ? LassoTargetKind.Critter : LassoTargetKind.Goods, p != null ? p.SizeClass : SizeClass.Medium, false, false,
                LassoMath.FlatDistance(thrower.transform.position, carry.CarryTransform.position), range, t.LassoMinSize, held);
            return true;
        }

        /// <summary>Tools and other rigid props (critters are kinematic carries too, so the flag alone is not enough).</summary>
        static bool IsTool(ICarryable c) => c is IUsableTool || (c is CarryableProp p && (p.IsKinematicCarry || p.IsHolstered));

        /// <summary>Critter body: Critter layer or a NavMesh agent.</summary>
        static bool IsCritter(GameObject go) => go.layer == LayerMask.NameToLayer(GameLayers.Critter) || go.GetComponent<NavMeshAgent>() != null;

        /// <summary>Critter personality for a critter object (through its goods id, Core data only).</summary>
        public static CritterPersonality FindPersonality(GameObject go)
        {
            var goods = go != null ? go.GetComponentInParent<IGoods>() : null;
            string gid = goods != null ? goods.GoodsId : null;
            if (string.IsNullOrEmpty(gid)) return null;
            if (PersonalityByGoods.TryGetValue(gid, out var p) && p != null) return p;
            foreach (var c in Content.Critters.All) if (c != null && c.GoodsId == gid) { p = c; break; }
            PersonalityByGoods[gid] = p;
            return p;
        }

        /// <summary>Where the loop sits on a body: near the top of its colliders (a critter's neck, a crate's top).</summary>
        static Vector3 TargetPoint(NetworkObject no)
        {
            if (no == null) return Vector3.zero;
            var pc = no.GetComponent<PlayerCharacter>();
            if (pc != null) return pc.transform.position + Vector3.up * 0.5f;
            var col = no.GetComponentInChildren<Collider>();
            if (col == null || !col.enabled) return no.transform.position + Vector3.up * 0.3f;
            var b = col.bounds;
            return new Vector3(b.center.x, b.center.y + b.extents.y * 0.5f, b.center.z);
        }

        // ================================================================================================ server: tick

        void Update()
        {
            if (IsSpawned && IsServer) ServerTick();
            Present(Time.deltaTime);
        }

        void ServerTick()
        {
            float now = Time.time;
            switch (Phase)
            {
                case LassoPhase.Aiming:
                    if (_user == null || _user.ServerBody == null || _user.ServerBody.NetObject != NetworkObject) _phase.Value = (byte)LassoPhase.Idle;
                    break;
                case LassoPhase.Flying:
                {
                    float u = (now - _flyStart) / _flyDur;
                    bool alive = _flyTarget != null && _flyTarget.IsSpawned;
                    Vector3 to = alive ? TargetPoint(_flyTarget) : _flyTo;
                    float h = 0.4f + 0.06f * Vector3.Distance(_flyFrom, to);
                    _loopPos.Value = LassoMath.LoopArc(_flyFrom, to, u, h);
                    if (u < 1f) break;
                    if (alive && _flyVerdict == LassoVerdict.Hit && StillValid(_flyTarget)) Attach(_flyTarget);
                    else StartRetract(_loopPos.Value);
                    break;
                }
                case LassoPhase.Retracting:
                {
                    float u = (now - _retractStart) / 0.35f;
                    _loopPos.Value = Vector3.Lerp(_retractFrom, transform.position, Mathf.Clamp01(u));
                    if (u >= 1f) _phase.Value = (byte)LassoPhase.Idle;
                    break;
                }
            }
        }

        bool StillValid(NetworkObject no)
        {
            var pc = no.GetComponent<PlayerCharacter>();
            if (pc != null) return pc.State == PlayerState.KnockedOut && pc.CarriedBy == PlayerIds.None;
            var cb = no.GetComponentInChildren<CarryBody>();
            return cb == null || cb.GripCount == 0;
        }

        void StartRetract(Vector3 from)
        {
            _retractFrom = from;
            _retractStart = Time.time;
            _loopPos.Value = from;
            _phase.Value = (byte)LassoPhase.Retracting;
        }

        void Attach(NetworkObject no)
        {
            CarryHooks.RaiseClaiming(no, this); // a wagon lets go of its cargo
            _target = no;
            _targetPlayer = no.GetComponent<PlayerCharacter>();
            _targetCarry = no.GetComponentInChildren<ICarryable>();
            _targetEvents = _targetCarry as ICarryableEvents;
            _goods = no.GetComponentInChildren<IGoods>();
            _agent = no.GetComponent<NavMeshAgent>();
            _targetRb = no.GetComponent<Rigidbody>();
            _personality = _agent != null ? FindPersonality(no.gameObject) : null;
            _mode = _targetPlayer != null ? TargetMode.Player : _agent != null ? TargetMode.Critter : TargetMode.Body;
            if (_targetEvents != null) _targetEvents.OnForcedRelease += OnTargetBrokeFree;

            _holderPc = _user != null ? _user.Character : _holderPc;
            SetAnchor(AnchorMode.Holder, _holderPc != null ? _holderPc.NetworkObject : null, null, Vector3.zero);
            Vector3 anchor = AnchorPosition();
            SetLength(Mathf.Max(t.LassoLength, LassoMath.FlatDistance(anchor, TargetPoint(no))));
            _targetId.Value = no.NetworkObjectId;
            _phase.Value = (byte)LassoPhase.Tethered;
            _overSince = -1f; _yanked = false; _waterSki = false;
            Active.Add(this);
            if (_mode == TargetMode.Body) CreateJoint();
            _goods?.RecordTouch(_lasser.Value);
            // The crack of the rope spooks the catch away from the thrower, so the rope goes tight.
            if (_mode == TargetMode.Critter && _holderPc != null)
                NoiseService.TryEmit(NoiseSource.Tool, t.LassoCatchLoudness, _holderPc.transform.position, NetworkObjectId, _lasser.Value);
        }

        void OnTargetBrokeFree(ulong[] holders)
        {
            // Only relevant while the rope holds the critter off its feet (carried by the lasser): it wriggled out.
            if (_yanked) { _yanked = false; EndTether(false); }
        }

        void FixedUpdate()
        {
            if (!IsSpawned || !IsServer || Phase != LassoPhase.Tethered) return;
            float now = Time.time, dt = Time.fixedDeltaTime;
            if (_target == null || !_target.IsSpawned) { EndTether(false); return; }

            var am = (AnchorMode)_anchorMode.Value;
            if (am == AnchorMode.Holder)
            {
                if (_waterSki)
                {
                    if (now >= _waterSkiUntil || _holderPc == null || _holderPc.State == PlayerState.Respawning) { EndTether(false); return; }
                }
                else if (_user == null || _user.ServerBody == null || _user.ServerBody.NetObject != NetworkObject) { EndTether(false); return; }
            }
            else if (am == AnchorMode.Body && (_anchorObj == null || !_anchorObj.IsSpawned)) { EndTether(false); return; }

            Vector3 anchor = AnchorPosition();
            Vector3 tp = TargetPoint(_target);
            float dist = LassoMath.FlatDistance(anchor, tp);

            // Rope length: tightens to its rest length after a throw or a tie, shortens while reeling.
            float L = _length.Value;
            float rest = am == AnchorMode.Holder ? t.LassoLength : t.HitchLength;
            if (L > rest) L = Mathf.Max(rest, L - t.LassoReelSpeed * dt);
            bool reeling = _reeling.Value && am == AnchorMode.Holder && !_waterSki;
            if (reeling) L = LassoMath.Reel(L, t.LassoReelSpeed, dt, t.LassoAutoGrab * 0.9f);
            SetLength(L);

            if (reeling && LassoMath.ShouldAutoGrab(dist, t.LassoAutoGrab)) { AutoGrab(); return; }

            if (!_yanked && InWater(tp)) { BlameRiver(tp); EndTether(false); return; }
            if (!_waterSki && dist > L + t.RopeSnapSlack) { Snap(tp); return; }

            switch (_mode)
            {
                case TargetMode.Critter: TickCritter(anchor, tp, dist, L, dt, now); break;
                case TargetMode.Body: TickBody(L, reeling); break;
                case TargetMode.Player: TickPlayer(anchor, dist, L, now); break;
            }
            if (am == AnchorMode.Body && Body != null) Body.MovePosition(anchor); // coil rides the wagon hitch
            if (now >= _nextTouch) { _nextTouch = now + 0.5f; _goods?.RecordTouch(_lasser.Value); }
        }

        void TickCritter(Vector3 anchor, Vector3 tp, float dist, float L, float dt, float now)
        {
            if (_waterSki)
            {
                SetDrain(0f);
                if (now >= _nextDrag && _holderPc != null) { _nextDrag = now + 0.25f; _holderPc.RequestDrag(tp, 0.4f); }
                return;
            }
            if (_yanked) { TickYank(anchor, tp, dist, L, dt); return; }
            bool agentOk = _agent != null && _agent.enabled && _agent.isOnNavMesh;
            bool holder = (AnchorMode)_anchorMode.Value == AnchorMode.Holder;
            if (!agentOk || dist <= L)
            {
                SetDrain(holder && _reeling.Value ? t.LassoStaminaPerSecond : 0f);
                _overSince = -1f;
                return;
            }

            Vector3 away = tp - anchor; away.y = 0f; away = away.sqrMagnitude > 1e-6f ? away.normalized : Vector3.forward;
            bool pullingAway = Vector3.Dot(_agent.velocity, away) > 0.25f || Vector3.Dot(_agent.desiredVelocity, away) > 0.25f;
            float pull = _personality != null ? _personality.CritterPullN : t.DefaultCritterPull;
            float anchorN = AnchorForce();
            if (holder) SetDrain(t.LassoStaminaPerSecond); // pulling against a tight rope
            if (holder && pullingAway && LassoMath.IsDragged(pull, anchorN)) { StartWaterSki(away); return; }
            if ((AnchorMode)_anchorMode.Value == AnchorMode.Body && _anchorRb != null && pullingAway)
                _anchorRb.AddForceAtPosition(away * pull, anchor); // a hitched animal pulls the wagon

            // Hold it at the rope's end; reeling pulls it in. The agent keeps the critter on walkable ground.
            Vector3 desired = LassoMath.ClampToRopeFlat(anchor, _agent.transform.position, L);
            Vector3 delta = desired - _agent.transform.position; delta.y = 0f;
            if (delta.sqrMagnitude > 1e-6f) _agent.Move(delta);
            float after = LassoMath.FlatDistance(anchor, _agent.transform.position);
            if (after > L + t.LassoYankSlack)
            {
                if (_overSince < 0f) _overSince = now;
                else if (now - _overSince > 0.35f) BeginYank();
            }
            else _overSince = -1f;
        }

        void BeginYank()
        {
            if (_targetCarry == null || _targetRb == null) return;
            _overSince = -1f;
            _yanked = true;
            _targetCarry.OnGrabbed(_lasser.Value); // off its feet: AI hands control to us (Carried)
        }

        void EndYank()
        {
            if (!_yanked) return;
            _yanked = false;
            _targetCarry?.OnReleased(_lasser.Value); // AI takes over again (stunned for a moment, then back on the NavMesh)
        }

        void TickYank(Vector3 anchor, Vector3 tp, float dist, float L, float dt)
        {
            if (_targetRb == null) { EndTether(false); return; }
            if ((AnchorMode)_anchorMode.Value == AnchorMode.Holder) SetDrain(t.LassoStaminaPerSecond);
            if (dist <= L + 0.05f) { EndYank(); return; }
            Vector3 pos = _targetRb.position;
            Vector3 desired = LassoMath.ClampToRopeFlat(anchor, pos, L);
            Vector3 next = Vector3.MoveTowards(new Vector3(pos.x, 0f, pos.z), new Vector3(desired.x, 0f, desired.z), t.LassoPullSpeed * dt);
            if (GroundBelow(new Vector3(next.x, pos.y, next.z), out float gy, out bool water) && !water) next.y = gy;
            else
            {
                // Over the river (or a drop): let go so it falls. The rope dragged it there.
                Vector3 at = _targetRb.position;
                EndYank();
                if (water) BlameRiver(at);
                EndTether(false);
                return;
            }
            Vector3 face = desired - pos; face.y = 0f;
            _targetRb.MovePosition(next);
            if (face.sqrMagnitude > 1e-4f) _targetRb.MoveRotation(Quaternion.LookRotation(-face.normalized)); // dragged backwards
        }

        static int GroundMask => ~LayerMask.GetMask(GameLayers.Player, GameLayers.Critter, GameLayers.Carryable, GameLayers.Ragdoll, GameLayers.Interactable, "Ignore Raycast", "UI");

        /// <summary>Ground height under <paramref name="p"/>; <paramref name="water"/> when the first thing below is the river.</summary>
        static bool GroundBelow(Vector3 p, out float y, out bool water)
        {
            y = p.y; water = false;
            if (!Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out var hit, 8f, GroundMask, QueryTriggerInteraction.Collide)) return false;
            y = hit.point.y;
            water = hit.collider.gameObject.layer == LayerMask.NameToLayer(GameLayers.Water);
            return true;
        }

        static bool InWater(Vector3 p) =>
            Physics.CheckSphere(p, 0.25f, LayerMask.GetMask(GameLayers.Water), QueryTriggerInteraction.Collide);

        void StartWaterSki(Vector3 away)
        {
            var pc = _holderPc;
            if (pc == null) { EndTether(false); return; }
            _waterSki = true;
            _waterSkiUntil = Time.time + t.LassoWaterSkiSeconds;
            _reeling.Value = false;
            SetDrain(0f);
            pc.ApplyImpulse(away * t.LassoDragImpulse, PlayerIds.Nature, BlameVerb.Crashed); // ragdoll: the hands let go of the coil, not the rope
            if (!pc.IsDown) { _waterSki = false; EndTether(false); } // braced: the rope rips out of the hands instead
        }

        /// <summary>Sum of player anchors on the same catch (farm-tools section 5: "the tether system sums anchors").</summary>
        float AnchorForce()
        {
            float sum = 0f;
            foreach (var l in Active)
            {
                if (l == null || l._target != _target || l.Phase != LassoPhase.Tethered) continue;
                var am = (AnchorMode)l._anchorMode.Value;
                if (am != AnchorMode.Holder) return float.PositiveInfinity; // tied to a post or the wagon
                if (l._waterSki || l._holderPc == null) continue;
                sum += LassoMath.PlayerAnchor(t.PlayerAnchorBase, l._holderPc.StaminaReplicated, 0, GroundUnder(l._holderPc), t.LassoExhaustedMult);
            }
            return sum;
        }

        static RopeGround GroundUnder(PlayerCharacter pc)
        {
            Vector3 p = pc.transform.position;
            bool bridge = Physics.Raycast(p + Vector3.up * 0.3f, Vector3.down, 1.2f, LayerMask.GetMask(GameLayers.Bridge), QueryTriggerInteraction.Ignore);
            return LassoMath.GroundFor(bridge, MovementModifiers.Evaluate(p));
        }

        void TickBody(float L, bool reeling)
        {
            if (_joint == null) { Snap(TargetPoint(_target)); return; } // broke at RopeBreakForce
            if (_targetRb == null || _targetRb.isKinematic) { EndTether(false); return; }
            var lim = _joint.linearLimit;
            if (Mathf.Abs(lim.limit - L) > 0.01f) { lim.limit = L; _joint.linearLimit = lim; }
            if (_joint.connectedBody == null && (AnchorMode)_anchorMode.Value == AnchorMode.Holder) _joint.connectedAnchor = AnchorPosition();
            SetDrain(reeling ? t.LassoStaminaPerSecond : 0f);
        }

        void TickPlayer(Vector3 anchor, float dist, float L, float now)
        {
            if (_targetPlayer == null || _targetPlayer.State != PlayerState.KnockedOut || _targetPlayer.CarriedBy != PlayerIds.None) { EndTether(false); return; }
            SetDrain(_reeling.Value ? t.LassoStaminaPerSecond : 0f);
            if (dist > L && now >= _nextDrag) { _nextDrag = now + 0.25f; _targetPlayer.RequestDrag(anchor, 0.35f); }
        }

        void CreateJoint()
        {
            if (_targetRb == null || _targetRb.isKinematic) return;
            _joint = _targetRb.gameObject.AddComponent<ConfigurableJoint>();
            _joint.autoConfigureConnectedAnchor = false;
            _joint.anchor = Vector3.zero;
            _joint.xMotion = _joint.yMotion = _joint.zMotion = ConfigurableJointMotion.Limited;
            _joint.angularXMotion = _joint.angularYMotion = _joint.angularZMotion = ConfigurableJointMotion.Free;
            _joint.linearLimit = new SoftJointLimit { limit = _length.Value, bounciness = 0f, contactDistance = 0.05f };
            _joint.breakForce = t.RopeBreakForce;
            _joint.breakTorque = Mathf.Infinity;
            _joint.enableCollision = true;
            ApplyJointAnchor();
        }

        void ApplyJointAnchor()
        {
            if (_joint == null) return;
            var am = (AnchorMode)_anchorMode.Value;
            if (am == AnchorMode.Body && _anchorRb != null)
            {
                _joint.connectedBody = _anchorRb;
                _joint.connectedAnchor = _anchorRb.transform.InverseTransformPoint(AnchorPosition());
            }
            else if (am == AnchorMode.Holder && _holderPc != null && _holderPc.TryGetComponent<Rigidbody>(out var prb))
            {
                _joint.connectedBody = prb; // kinematic player body: the rope tows the item, the item never pulls the player
                _joint.connectedAnchor = new Vector3(0f, 1.1f, 0.3f);
            }
            else
            {
                _joint.connectedBody = null;
                _joint.connectedAnchor = AnchorPosition();
            }
            _targetRb.WakeUp();
        }

        void DestroyJoint()
        {
            if (_joint != null) Destroy(_joint);
            _joint = null;
        }

        void AutoGrab()
        {
            var user = _user;
            var target = _target;
            EndTether(false);
            if (user == null || target == null || !target.IsSpawned) return;
            user.ServerRelease(HandlingMode.Dropped); // the lasso goes back on the belt
            user.ServerGrab(target, Vector3.zero, false);
        }

        void Snap(Vector3 at)
        {
            NoiseService.TryEmit(NoiseSource.Tool, 0.35f, at, NetworkObjectId, _lasser.Value);
            EndTether(false);
        }

        void BlameRiver(Vector3 at)
        {
            string itemId = _goods != null ? _goods.GoodsId : string.Empty;
            string tag = _targetPlayer != null ? "Player" : _goods != null ? _goods.GoodsId : "Prop";
            ToolBlame.Emit(BlameVerb.LassoedIntoRiver, _lasser.Value, itemId, tag, at, "Hazard.River");
        }

        void EndTether(bool retract)
        {
            if (Phase != LassoPhase.Tethered && Phase != LassoPhase.Flying)
            {
                if (Phase == LassoPhase.Aiming) _phase.Value = (byte)LassoPhase.Idle;
                return;
            }
            Vector3 from = _target != null ? TargetPoint(_target) : _loopPos.Value;
            bool wasTied = IsTied;
            DestroyJoint();
            if (_yanked) EndYank();
            if (_targetEvents != null) _targetEvents.OnForcedRelease -= OnTargetBrokeFree;
            SetDrain(0f);
            Active.Remove(this);
            _target = null; _targetPlayer = null; _targetCarry = null; _targetEvents = null; _goods = null; _agent = null; _targetRb = null; _personality = null;
            _mode = TargetMode.None;
            _waterSki = false; _overSince = -1f;
            _targetId.Value = PlayerCarry.NoObject;
            _reeling.Value = false;
            SetAnchor(AnchorMode.Holder, _holderPc != null ? _holderPc.NetworkObject : null, null, Vector3.zero);
            if (wasTied && Body != null && !IsHolstered) Body.isKinematic = false; // the coil drops off the post
            bool held = _user != null && _user.ServerBody != null && _user.ServerBody.NetObject == NetworkObject;
            if (retract && held) StartRetract(from);
            else _phase.Value = (byte)LassoPhase.Idle;
        }

        void SetAnchor(AnchorMode mode, NetworkObject obj, Rigidbody rb, Vector3 point)
        {
            _anchorMode.Value = (byte)mode;
            _anchorObj = obj;
            _anchorRb = rb;
            _anchorId.Value = obj != null ? obj.NetworkObjectId : PlayerCarry.NoObject;
            _anchorPoint.Value = mode == AnchorMode.Body && obj != null ? obj.transform.InverseTransformPoint(point) : point;
            if (mode == AnchorMode.Holder) _holderPc = obj != null ? obj.GetComponent<PlayerCharacter>() : _holderPc;
            ApplyJointAnchor();
        }

        void SetLength(float L) { if (Mathf.Abs(_length.Value - L) > 0.005f) _length.Value = L; }

        void SetDrain(float perSecond)
        {
            var carry = _holderPc != null ? _holderPc.Carry : null;
            if (carry != null) carry.ServerSetExtraDrain(perSecond);
        }

        /// <summary>Rope start on any peer: the coil in hand, the holder's body (water-ski), the post or the wagon hitch.</summary>
        Vector3 AnchorPosition()
        {
            switch ((AnchorMode)_anchorMode.Value)
            {
                case AnchorMode.Static: return _anchorPoint.Value;
                case AnchorMode.Body:
                    var obj = SpawnedObject(_anchorId.Value);
                    return obj != null ? obj.transform.TransformPoint(_anchorPoint.Value) : _anchorPoint.Value;
                default:
                    if (!IsHolstered) return transform.position;
                    var holder = SpawnedObject(_anchorId.Value);
                    return holder != null ? holder.transform.position + Vector3.up * 0.9f : transform.position;
            }
        }

        NetworkObject SpawnedObject(ulong id)
        {
            if (id == PlayerCarry.NoObject || NetworkManager == null || NetworkManager.SpawnManager == null) return null;
            return NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(id, out var no) ? no : null;
        }

        // ================================================================================================ presentation

        void Present(float dt)
        {
            CacheRest();
            var ph = IsSpawned ? Phase : LassoPhase.Idle;
            bool hidden = IsHolstered && !(ph == LassoPhase.Tethered); // water-ski: rope from the body
            var target = ph == LassoPhase.Tethered ? SpawnedObject(_targetId.Value) : null;
            Vector3 anchor = IsSpawned ? AnchorPosition() : transform.position;

            // Hand targets for the first-person rig (local to the coil's parent, which is the rigid hold point).
            bool inHand = !IsHolstered && !IsTied && ph != LassoPhase.Idle;
            _poseBlend = Mathf.MoveTowards(_poseBlend, inHand ? 1f : 0f, dt * 6f);
            _twirl += dt * Mathf.PI * 2f * 2.2f;
            if (_reeling.Value) _reelPhase += dt * 2.2f;
            Vector3 coilPos = _coilRest, gripPos = _gripRest, gripLPos = _gripLRest;
            if (ph == LassoPhase.Aiming)
            {
                coilPos = _coilRest + new Vector3(-0.24f, 0.04f, -0.06f);
                gripLPos = coilPos + new Vector3(0f, 0.1f, 0f);
                gripPos = new Vector3(-0.2f, 0.58f, -0.2f) + new Vector3(Mathf.Cos(_twirl), 0f, Mathf.Sin(_twirl)) * 0.07f;
            }
            else if (ph == LassoPhase.Tethered && !IsTied)
            {
                Vector3 toRope = transform.InverseTransformDirection((target != null ? TargetPoint(target) : anchor) - transform.position);
                toRope = toRope.sqrMagnitude > 1e-4f ? toRope.normalized : Vector3.forward;
                ArmMath.ReelHands(_reelPhase, _reeling.Value ? 0.18f : 0f, out float l, out float r);
                coilPos = _coilRest + new Vector3(-0.08f, 0.04f, 0.02f);
                gripPos = coilPos + new Vector3(0.02f, 0.1f, 0f) - toRope * r;
                gripLPos = coilPos + toRope * (0.3f - l) + new Vector3(0f, 0.05f, 0f);
            }
            if (coil != null) coil.localPosition = Vector3.Lerp(_coilRest, coilPos, _poseBlend);
            if (grip != null) grip.localPosition = Vector3.Lerp(_gripRest, gripPos, _poseBlend);
            if (gripL != null) gripL.localPosition = Vector3.Lerp(_gripLRest, gripLPos, _poseBlend);
            if (coil != null && ph == LassoPhase.Aiming) coil.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(_twirl) * 8f);

            // Loop.
            if (loop != null)
            {
                bool show = !hidden && ph != LassoPhase.Idle || ph == LassoPhase.Tethered;
                loop.enabled = show;
                if (show)
                {
                    Vector3 c; float radius; Quaternion plane;
                    if (ph == LassoPhase.Aiming && grip != null)
                    {
                        c = grip.position + Vector3.up * 0.08f; radius = 0.42f;
                        plane = Quaternion.Euler(10f, _twirl * Mathf.Rad2Deg, 0f);
                    }
                    else if (ph == LassoPhase.Tethered && target != null)
                    {
                        var col = target.GetComponentInChildren<Collider>();
                        float ext = col != null ? Mathf.Clamp(Mathf.Max(col.bounds.extents.x, col.bounds.extents.z) * 0.55f, 0.18f, 0.6f) : 0.3f;
                        c = TargetPoint(target); radius = ext; plane = Quaternion.identity;
                    }
                    else { c = _loopPos.Value; radius = 0.45f; plane = Quaternion.Euler(15f, _twirl * Mathf.Rad2Deg * 0.5f, 0f); }
                    for (int i = 0; i < LoopSegments; i++)
                    {
                        float a = i / (float)LoopSegments * Mathf.PI * 2f;
                        LoopPts[i] = c + plane * new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                    }
                    loop.positionCount = LoopSegments;
                    loop.loop = true;
                    loop.SetPositions(LoopPts);
                }
            }

            // Rope: coil (or post / body) to the loop.
            if (rope != null)
            {
                bool show = (ph == LassoPhase.Flying || ph == LassoPhase.Retracting || ph == LassoPhase.Tethered || ph == LassoPhase.Aiming) && (!hidden || ph == LassoPhase.Tethered);
                rope.enabled = show;
                if (show)
                {
                    Vector3 a = ph == LassoPhase.Tethered && (IsHolstered || IsTied) ? anchor : (gripL != null && ph != LassoPhase.Aiming ? gripL.position : coil != null ? coil.position : transform.position);
                    if (ph == LassoPhase.Aiming && coil != null) a = coil.position;
                    Vector3 b = ph == LassoPhase.Tethered && target != null ? TargetPoint(target)
                        : ph == LassoPhase.Aiming && grip != null ? grip.position : _loopPos.Value;
                    float d = Vector3.Distance(a, b);
                    float sag = ph == LassoPhase.Tethered ? LassoMath.Sag(_length.Value, LassoMath.FlatDistance(a, b)) : ph == LassoPhase.Aiming ? 0.12f : 0.05f;
                    for (int i = 0; i <= RopeSegments; i++)
                    {
                        float u = i / (float)RopeSegments;
                        RopePts[i] = Vector3.Lerp(a, b, u) + Vector3.down * (4f * sag * u * (1f - u));
                    }
                    rope.positionCount = RopeSegments + 1;
                    rope.SetPositions(RopePts);
                }
            }

            PresentAim(ph);
        }

        /// <summary>Owner-only arc preview while aiming: where the loop will land, green on a valid catch.</summary>
        void PresentAim(LassoPhase ph)
        {
            var me = PlayerCharacter.Local;
            bool mine = me != null && me.Carry != null && IsSpawned && me.Carry.HeldId == NetworkObjectId;
            bool show = mine && ph == LassoPhase.Aiming && aimArc != null;
            if (aimArc != null) aimArc.enabled = show;
            if (aimRing != null) aimRing.enabled = show;
            if (!show) return;
            var head = me.CameraPivot;
            Vector3 origin = head.position, dir = head.forward;
            float range = t.LassoRange;
            Vector3 land = origin + dir * range;
            Collider hitCol = null;
            if (Physics.Raycast(origin, dir, out var h, range, AimMask, QueryTriggerInteraction.Ignore) && !h.collider.transform.IsChildOf(me.transform)) { land = h.point; hitCol = h.collider; }
            else if (Physics.Raycast(land + Vector3.up * 0.5f, Vector3.down, out var g, 30f, AimMask, QueryTriggerInteraction.Ignore)) land = g.point;
            Vector3 flat = land - me.transform.position; flat.y = 0f;
            bool inRange = flat.magnitude <= range + 0.01f;
            bool valid = false;
            int n = Physics.OverlapSphereNonAlloc(land, t.LassoCatchRadius, Overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n && !valid; i++) valid = LocalValid(Overlaps[i], me);
            if (!valid && hitCol != null) valid = LocalValid(hitCol, me);
            Color c = valid && inRange ? new Color(0.45f, 1f, 0.45f, 0.95f) : new Color(1f, 0.96f, 0.84f, 0.75f);
            aimArc.startColor = aimArc.endColor = c;
            aimRing.startColor = aimRing.endColor = c;
            Vector3 from = grip != null ? grip.position : transform.position;
            float height = 0.4f + 0.06f * Vector3.Distance(from, land);
            for (int i = 0; i <= RopeSegments; i++) ArcPts[i] = LassoMath.LoopArc(from, land, i / (float)RopeSegments, height);
            aimArc.positionCount = RopeSegments + 1;
            aimArc.SetPositions(ArcPts);
            for (int i = 0; i < LoopSegments; i++)
            {
                float a = i / (float)LoopSegments * Mathf.PI * 2f;
                LoopPts[i] = land + Vector3.up * 0.06f + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * t.LassoCatchRadius;
            }
            aimRing.positionCount = LoopSegments;
            aimRing.loop = true;
            aimRing.SetPositions(LoopPts);
        }

        /// <summary>Client-side guess of the server rule for the aim colour (same data, no authority).</summary>
        bool LocalValid(Collider c, PlayerCharacter me)
        {
            var victim = c.GetComponentInParent<PlayerCharacter>();
            if (victim != null) return victim != me && victim.State == PlayerState.KnockedOut;
            var carry = c.GetComponentInParent<ICarryable>();
            if (carry == null || carry is ISelfSimulatedCarryable || IsTool(carry)) return false;
            var go = carry.CarryTransform.gameObject;
            bool critter = IsCritter(go);
            if (!critter) return true;
            var pers = FindPersonality(go);
            return (pers != null ? pers.SizeClass : SizeClass.Medium) >= t.LassoMinSize;
        }
    }
}
