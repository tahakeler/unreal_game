using System;
using System.Collections.Generic;
using HarvestPanic.Core;
using UnityEngine;

namespace HarvestPanic.World
{
    /// <summary>Replicated plank state.</summary>
    public enum PlankState : byte { Intact = 0, Cracked = 1, Broken = 2 }

    /// <summary>
    /// One breakable bridge plank (bridge-hazards section 3). The server samples the mass resting on the plank every 0.2 s,
    /// applies <see cref="PlankMath.DamagePerSecond"/>, cracks at 50, breaks at 0 (collider off, visual falls), emits noise
    /// and a BrokePlank blame naming the heaviest player on it. <see cref="BridgeNetSync"/> mirrors state to clients.
    /// Works without a network session (tests/editor) as if it were the server.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BridgePlank : MonoBehaviour
    {
        [SerializeField, Min(0)] float safeLoadKg = PlankMath.DefaultSafeLoadKg;
        [SerializeField, Min(0.05f)] float sampleInterval = 0.2f;
        [SerializeField, Tooltip("Height of the load-sampling box above the plank top (m)")] float sampleHeight = 2.6f;
        [SerializeField, Tooltip("Visible plank mesh (hidden when broken; a falling copy is spawned)")] Transform visual;
        [SerializeField] Material crackedMaterial;
        [SerializeField, Min(0)] int index;

        readonly Collider[] _hits = new Collider[64];
        readonly HashSet<int> _seen = new HashSet<int>();
        BoxCollider _box;
        Renderer[] _renderers;
        Material[] _intactMaterials;
        float _integrity = PlankMath.MaxIntegrity;
        float _nextSample;
        PlankState _state;
        ulong _heaviestPlayer = PlayerIds.None;
        float _lastLoad;

        /// <summary>Raised on every peer when the state changes.</summary>
        public event Action<BridgePlank, PlankState> StateChanged;

        /// <summary>Order along the bridge, 0 = west end.</summary>
        public int Index => index;
        /// <summary>0..100.</summary>
        public float Integrity => _integrity;
        /// <summary>Current state.</summary>
        public PlankState State => _state;
        /// <summary>Last sampled load (kg).</summary>
        public float LastLoadKg => _lastLoad;
        /// <summary>Safe load (kg) before day multipliers.</summary>
        public float SafeLoadKg { get => safeLoadKg; set => safeLoadKg = Mathf.Max(0f, value); }

        /// <summary>Editor/build-time setup.</summary>
        public void Configure(int plankIndex, Transform visualRoot, Material cracked)
        {
            index = plankIndex; visual = visualRoot; crackedMaterial = cracked;
        }

        void Awake()
        {
            _box = GetComponent<BoxCollider>();
            _renderers = visual != null ? visual.GetComponentsInChildren<Renderer>(true) : GetComponentsInChildren<Renderer>(true);
            _intactMaterials = new Material[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++) _intactMaterials[i] = _renderers[i].sharedMaterial;
        }

        void FixedUpdate()
        {
            if (_state == PlankState.Broken || !WorldAuthority.IsServerOrOffline) return;
            if (Time.time < _nextSample) return;
            _nextSample = Time.time + sampleInterval;
            _lastLoad = SampleLoad(out _heaviestPlayer);
            float safeMult = 1f, breakMult = 1f;
            if (Services.TryGet(out IDayModifierService day))
            {
                safeMult = day.Resolve(WorldDayParams.SafeLoadMult, 1f);
                breakMult = day.Resolve(WorldDayParams.PlankBreakMult, 1f);
            }
            float dmg = PlankMath.DamagePerSecond(_lastLoad, _state == PlankState.Cracked, safeLoadKg, safeMult, breakMult) * sampleInterval;
            if (dmg > 0f) ApplyDamage(dmg);
        }

        void OnCollisionEnter(Collision c)
        {
            if (_state == PlankState.Broken || !WorldAuthority.IsServerOrOffline) return;
            float d = PlankMath.ImpactDamage(c.impulse.magnitude);
            if (d > 0f) ApplyDamage(d);
        }

        /// <summary>Server: subtracts integrity and advances the state machine.</summary>
        public void ApplyDamage(float amount)
        {
            if (_state == PlankState.Broken || amount <= 0f) return;
            _integrity = Mathf.Max(0f, _integrity - amount);
            if (_integrity <= 0f) { SetState(PlankState.Broken, true); Announce(0.75f, NoiseTag.PlankBreak, true); }
            else if (_state == PlankState.Intact && PlankMath.IsCracked(_integrity)) { SetState(PlankState.Cracked, true); Announce(0.3f, NoiseTag.Creak, false); }
        }

        /// <summary>Server: full repair (each morning).</summary>
        public void Repair()
        {
            _integrity = PlankMath.MaxIntegrity;
            SetState(PlankState.Intact, true);
        }

        /// <summary>Applies a state on any peer (server-authoritative changes come in through <see cref="BridgeNetSync"/>).</summary>
        public void ApplyReplicatedState(PlankState s, float integrity)
        {
            _integrity = integrity;
            if (s != _state) SetState(s, false);
        }

        void SetState(PlankState s, bool authoritative)
        {
            if (s == _state) return;
            var prev = _state;
            _state = s;
            if (_renderers == null) Awake();
            bool broken = s == PlankState.Broken;
            if (_box != null) _box.enabled = !broken;
            for (int i = 0; i < _renderers.Length; i++)
            {
                _renderers[i].enabled = !broken;
                _renderers[i].sharedMaterial = s == PlankState.Cracked && crackedMaterial != null ? crackedMaterial : _intactMaterials[i];
            }
            if (broken && prev != PlankState.Broken) SpawnFallingCopy();
            StateChanged?.Invoke(this, s);
        }

        void SpawnFallingCopy()
        {
            if (visual == null || !Application.isPlaying) return;
            var copy = Instantiate(visual.gameObject, visual.position, visual.rotation);
            copy.name = name + "_Debris";
            foreach (var r in copy.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
            var rb = copy.AddComponent<Rigidbody>();
            rb.mass = 15f;
            rb.AddTorque(UnityEngine.Random.insideUnitSphere * 4f, ForceMode.VelocityChange);
            Destroy(copy, 6f);
        }

        float SampleLoad(out ulong heaviestPlayer)
        {
            heaviestPlayer = PlayerIds.None;
            if (_box == null) return 0f;
            var t = transform;
            Vector3 half = Vector3.Scale(_box.size, t.lossyScale) * 0.5f;
            Vector3 top = t.TransformPoint(_box.center) + t.up * (half.y + sampleHeight * 0.5f);
            int n = Physics.OverlapBoxNonAlloc(top, new Vector3(half.x, sampleHeight * 0.5f, half.z), _hits, t.rotation, ~0, QueryTriggerInteraction.Ignore);
            _seen.Clear();
            float total = 0f, heaviest = 0f;
            for (int i = 0; i < n; i++)
            {
                var col = _hits[i];
                if (col == _box) continue;
                var rb = col.attachedRigidbody;
                Component key = rb != null ? rb : col.transform.root;
                if (!_seen.Add(key.GetInstanceID())) continue;
                if (!OwnsCentre(key.transform.position)) continue;
                var hooks = col.GetComponentInParent<IPlayerHooks>();
                float mass;
                var carry = col.GetComponentInParent<ICarryable>();
                if (carry != null) mass = carry.MassKg;
                else if (rb != null && !rb.isKinematic) mass = rb.mass;
                else if (hooks != null || col.gameObject.layer == LayerMask.NameToLayer(GameLayers.Player)) mass = rb != null ? Mathf.Max(rb.mass, PlankMath.PlayerMassKg) : PlankMath.PlayerMassKg;
                else continue;
                total += mass;
                if (hooks != null && mass > heaviest) { heaviest = mass; heaviestPlayer = hooks.ClientId; }
            }
            return total;
        }

        /// <summary>A body counts on the plank whose footprint contains its centre, so a load is never double counted.</summary>
        bool OwnsCentre(Vector3 worldPos)
        {
            Vector3 local = transform.InverseTransformPoint(worldPos) - _box.center;
            return Mathf.Abs(local.x) <= _box.size.x * 0.5f && Mathf.Abs(local.z) <= _box.size.z * 0.5f;
        }

        void Announce(float loudness, NoiseTag tag, bool broke)
        {
            Vector3 p = transform.position;
            if (Services.TryGet(out INoiseService noise))
                noise.Emit(NoiseSource.Hazard, loudness, p, 0, _heaviestPlayer, tag);
            if (!broke || !Services.TryGet(out IEconomyService eco)) return;
            bool hasPlayer = PlayerIds.IsPlayer(_heaviestPlayer);
            var evt = new BlameEvent
            {
                Timestamp = Time.timeSinceLevelLoad,
                DayIndex = (short)(Services.TryGet(out IDayModifierService day) ? day.DayIndex : 0),
                Instigators = hasPlayer ? new[] { _heaviestPlayer } : Array.Empty<ulong>(),
                BlameShares = hasPlayer ? new[] { 1f } : Array.Empty<float>(),
                PrimaryInstigator = hasPlayer ? _heaviestPlayer : PlayerIds.None,
                Verb = BlameVerb.BrokePlank,
                ItemId = string.Empty,
                ItemTag = "Bridge.Plank",
                DollarDelta = -30,
                Location = p,
                CauseTag = "Hazard.Bridge",
            };
            eco.EmitBlame(evt);
        }
    }
}
