using HarvestPanic.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace HarvestPanic.AI
{
    /// <summary>
    /// Goat ("Gary") signature tasks (livestock-roster §3.1, §5), server-authoritative, layered on <see cref="CritterBrain"/>:
    /// <list type="bullet">
    /// <item><b>Headbutt</b>: a player close in front gets a readable telegraph (head lowered + bleat, <c>HeadbuttTelegraph</c> s),
    /// then a knockdown via <see cref="IKnockable.ApplyImpulse"/> if still in reach. Sidestep/back off to dodge.</item>
    /// <item><b>Climb</b>: from Idle it hops onto nearby crates/bales/wagons/roofs in the height band and perches; jumps down
    /// when bored (45 s) or threatened.</item>
    /// <item><b>Graze</b>: walks to loose Crop/Product goods (reusing the brain's lure) and bites them (bruise = value loss, Nature blame).</item>
    /// </list>
    /// Knobs live in <see cref="LivestockTuning"/>. The telegraph pose is replicated so clients see the wind-up.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CritterBrain))]
    public sealed class GoatBehaviour : NetworkBehaviour
    {
        enum Mode : byte { None, Telegraph, Hop, Perch }

        [SerializeField] LivestockTuning tuning;
        [SerializeField, Tooltip("Visual pivot pitched down during the telegraph")] Transform model;

        readonly NetworkVariable<bool> _netTelegraph = new NetworkVariable<bool>();
        readonly Collider[] _hits = new Collider[24];
        CritterBrain _brain;
        NavMeshAgent _agent;
        Rigidbody _rb;
        LivestockTuning _t;
        Mode _mode;
        float _nextCheck, _modeUntil, _cooldownUntil, _perchSince, _nextBite;
        IKnockable _target;
        Transform _targetTf;
        Vector3 _hopFrom, _hopTo;
        bool _hopDown;
        IGoods _food;
        int _playerMask;
        float _pitch;
        bool _resumePerch;
        Quaternion _baseRot = Quaternion.identity;

        /// <summary>Headbutts that connected (tests/debug).</summary>
        public int HeadbuttHits { get; private set; }
        /// <summary>True while winding up a headbutt.</summary>
        public bool IsTelegraphing => IsSpawned && !IsServer ? _netTelegraph.Value : _mode == Mode.Telegraph;
        /// <summary>True while standing on something it climbed.</summary>
        public bool IsPerched => _mode == Mode.Perch;
        public LivestockTuning Tuning => _t != null ? _t : (_t = tuning != null ? tuning : LivestockTuning.Default);

        bool Authority => !IsSpawned || IsServer;

        /// <summary>Editor builder hook.</summary>
        public void Configure(LivestockTuning t, Transform pivot) { tuning = t; model = pivot; }

        void Awake()
        {
            _brain = GetComponent<CritterBrain>();
            _agent = GetComponent<NavMeshAgent>();
            _rb = GetComponent<Rigidbody>();
            _playerMask = LayerMask.GetMask(GameLayers.Player);
            if (model != null) _baseRot = model.localRotation;
        }

        void Update()
        {
            float target = IsTelegraphing ? 22f : 0f;
            _pitch = Mathf.MoveTowards(_pitch, target, 120f * Time.deltaTime);
            if (model != null) model.localRotation = Quaternion.Euler(_pitch, 0f, 0f) * _baseRot;
            if (!Authority || _brain.Personality == null) return;
            var t = Tuning;
            float now = Time.time;

            if (_brain.State == CritterState.Carried || _brain.State == CritterState.Stunned)
            {
                if (_mode != Mode.None) SetMode(Mode.None); // carry/physics own the body now
                return;
            }

            switch (_mode)
            {
                case Mode.Telegraph: TickTelegraph(now, t); return;
                case Mode.Hop: TickHop(now, t); return;
                case Mode.Perch:
                    _brain.ExternalHoldUntil = now + 1f;
                    if (LivestockRules.ShouldLeavePerch(now - _perchSince, _brain.Threat, _brain.Personality.FleeThreshold, t)) StartHopDown(now, t);
                    else if (now >= _nextCheck) { _nextCheck = now + 1f / t.CheckHz; TryStartHeadbutt(now, t); }
                    return;
            }

            if (_food != null) TickGraze(now, t);
            if (now < _nextCheck) return;
            _nextCheck = now + 1f / t.CheckHz;
            if (TryStartHeadbutt(now, t)) return;
            var s = _brain.State;
            if ((s == CritterState.Idle || s == CritterState.Wander) && _food == null)
            {
                if (Random.value < t.ClimbChance * (1f / t.CheckHz) && TryStartClimb(now, t)) return;
                TryFindFood(t);
            }
        }

        // ------------------------------------------------------------ headbutt

        /// <summary>Server: looks for a player in the front cone and starts the telegraph. Public for tests (force skips the chance roll).</summary>
        public bool TryStartHeadbutt(float now, LivestockTuning t, bool force = false)
        {
            if (_mode == Mode.Hop || _mode == Mode.Telegraph) return false;
            int n = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up * 0.5f, t.HeadbuttRange + 0.5f, _hits, _playerMask, QueryTriggerInteraction.Ignore);
            var state = _mode == Mode.Perch ? CritterState.Idle : _brain.State;
            for (int i = 0; i < n; i++)
            {
                if (!Knockables.TryGetFor(_hits[i].gameObject, out var k)) continue;
                Vector3 to = _hits[i].transform.position - transform.position;
                if (!LivestockRules.CanStartHeadbutt(state, transform.forward, to, k.IsDown, _cooldownUntil - now, t)) continue;
                _cooldownUntil = now + t.HeadbuttCooldown;
                if (!force && Random.value > t.HeadbuttChance) return false;
                _target = k; _targetTf = _hits[i].transform;
                _resumePerch = _mode == Mode.Perch;
                SetMode(Mode.Telegraph);
                _modeUntil = now + t.HeadbuttTelegraph;
                _brain.ExternalHoldUntil = _modeUntil + 0.3f;
                if (_agent != null && _agent.enabled && _agent.isOnNavMesh) _agent.ResetPath();
                if (Services.TryGet(out INoiseService noise))
                    noise.Emit(NoiseSource.Critter, 0.4f, transform.position, IsSpawned ? NetworkObjectId : 0UL, PlayerIds.None, NoiseTag.None);
                return true;
            }
            return false;
        }

        void TickTelegraph(float now, LivestockTuning t)
        {
            if (_targetTf != null)
            {
                Vector3 to = _targetTf.position - transform.position; to.y = 0f;
                if (to.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), 360f * Time.deltaTime);
            }
            if (now < _modeUntil) return;
            SetMode(_resumePerch ? Mode.Perch : Mode.None);
            if (_target == null || _targetTf == null) return;
            Vector3 d = _targetTf.position - transform.position;
            if (!LivestockRules.HeadbuttConnects(d, _target.IsDown, t)) return;
            _target.ApplyImpulse(LivestockRules.HeadbuttImpulse(d, transform.forward, t), PlayerIds.Nature, BlameVerb.KnockedOut);
            HeadbuttHits++;
            _target = null; _targetTf = null;
        }

        // ------------------------------------------------------------ climb

        bool TryStartClimb(float now, LivestockTuning t)
        {
            int n = Physics.OverlapSphereNonAlloc(transform.position, t.ClimbSearchRadius, _hits, ~0, QueryTriggerInteraction.Ignore);
            float feet = transform.position.y;
            for (int i = 0; i < n; i++)
            {
                var c = _hits[i];
                if (c == null || c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic) continue;
                int layer = c.gameObject.layer;
                if (layer == gameObject.layer || ((1 << layer) & _playerMask) != 0) continue;
                var b = c.bounds;
                if (b.size.x < 0.7f || b.size.z < 0.7f || b.size.x > 12f || b.size.z > 12f) continue; // skip ground/huge
                if (!LivestockRules.IsClimbable(b.max.y, feet, t)) continue;
                Vector3 top = new Vector3(b.center.x, b.max.y + 2f, b.center.z);
                if (!Physics.Raycast(top, Vector3.down, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore) || hit.collider != c) continue;
                StartHop(hit.point, false, now, t);
                return true;
            }
            return false;
        }

        /// <summary>Server/tests: hop onto <paramref name="point"/> and perch.</summary>
        public void StartHop(Vector3 point, bool down, float now, LivestockTuning t)
        {
            _hopFrom = transform.position; _hopTo = point; _hopDown = down;
            if (_agent != null && _agent.enabled) { if (_agent.isOnNavMesh) _agent.ResetPath(); _agent.enabled = false; }
            if (_rb != null) _rb.isKinematic = true;
            SetMode(Mode.Hop);
            _modeUntil = now + t.HopSeconds;
            _brain.ExternalHoldUntil = _modeUntil + 1f;
            Vector3 flat = point - _hopFrom; flat.y = 0f;
            if (flat.sqrMagnitude > 1e-3f) transform.rotation = Quaternion.LookRotation(flat);
        }

        void TickHop(float now, LivestockTuning t)
        {
            float u = 1f - Mathf.Clamp01((_modeUntil - now) / t.HopSeconds);
            Vector3 p = Vector3.Lerp(_hopFrom, _hopTo, u);
            p.y += Mathf.Sin(u * Mathf.PI) * 0.8f;
            transform.position = p;
            if (u < 1f) return;
            if (_hopDown)
            {
                SetMode(Mode.None);
                _perchSince = 0f; _hopTo = Vector3.zero;
                if (_agent != null) { _agent.enabled = true; if (NavMesh.SamplePosition(transform.position, out var h, 2f, NavMesh.AllAreas)) _agent.Warp(h.position); }
                _brain.Home = transform.position;
                _brain.ExternalHoldUntil = 0f;
            }
            else { SetMode(Mode.Perch); _perchSince = now; }
        }

        void StartHopDown(float now, LivestockTuning t)
        {
            Vector3 dir = Random.insideUnitSphere; dir.y = 0f;
            Vector3 guess = transform.position + (dir.sqrMagnitude > 1e-3f ? dir.normalized : transform.forward) * 1.8f;
            Vector3 land = NavMesh.SamplePosition(guess, out var h, 3f, NavMesh.AllAreas) && h.position.y < transform.position.y - 0.3f ? h.position : guess - Vector3.up * (transform.position.y - _hopFrom.y);
            StartHop(land, true, now, t);
        }

        // ------------------------------------------------------------ graze

        void TryFindFood(LivestockTuning t)
        {
            int n = Physics.OverlapSphereNonAlloc(transform.position, t.GrazeSearchRadius, _hits, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var g = _hits[i].GetComponentInParent<IGoods>();
                if (g == null || g.Definition == null || g.Definition.IsAlive) continue;
                var cat = g.Definition.Category;
                if (cat != GoodsCategory.Crop && cat != GoodsCategory.Product) continue;
                if (g.GoodsTransform.parent != null && g.GoodsTransform.root.GetComponentInChildren<IKnockable>() != null) continue; // held
                if (g.BruiseState == BruiseState.Ruined) continue;
                float d = (g.GoodsTransform.position - transform.position).sqrMagnitude;
                if (d < best) { best = d; _food = g; }
            }
            if (_food != null) _brain.SetLure(_food.GoodsTransform.position, t.GrazeAffinity);
        }

        void TickGraze(float now, LivestockTuning t)
        {
            var go = _food as Component;
            if (go == null || _food.BruiseState == BruiseState.Ruined || _brain.State == CritterState.Flee) { _food = null; return; }
            if ((_food.GoodsTransform.position - transform.position).sqrMagnitude > t.BiteRange * t.BiteRange)
            {
                if (_brain.State != CritterState.Lured && _brain.State != CritterState.Alert) _brain.SetLure(_food.GoodsTransform.position, t.GrazeAffinity);
                return;
            }
            if (now < _nextBite) return;
            _nextBite = now + t.BiteInterval;
            Bite(_food, t);
        }

        /// <summary>Server: one bite (bruise through the goods contract, blamed on Nature). Public for tests.</summary>
        public void Bite(IGoods goods, LivestockTuning t) => goods?.ApplyBruise(t.BiteImpulse, PlayerIds.Nature, BlameVerb.Bruised);

        void SetMode(Mode m)
        {
            _mode = m;
            if (IsSpawned && IsServer) _netTelegraph.Value = m == Mode.Telegraph;
            if (m == Mode.None && _agent != null && !_agent.enabled && _brain.State != CritterState.Carried && _brain.State != CritterState.Stunned)
            {
                _agent.enabled = true;
                if (NavMesh.SamplePosition(transform.position, out var h, 2f, NavMesh.AllAreas)) _agent.Warp(h.position);
            }
        }
    }
}
