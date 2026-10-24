using HarvestPanic.Core;
using Unity.Netcode;
using UnityEngine;

namespace HarvestPanic.AI.Crops
{
    /// <summary>
    /// Screaming Mandrake (cursed-crops §3 #3). Server: screams when pulled out of the soil, when its carrier jostles it
    /// (smoothed acceleration above <see cref="CursedCropTuning.JostleAccel"/>) or on an impact above
    /// <see cref="CursedCropTuning.JostleImpact"/>. A scream is one Noise Event (Source=Crop, loudness 0.9 x sack x wagon,
    /// tag <see cref="NoiseTag.Mandrake"/>): livestock within 20 m flee (CritterBrain), the night director doubles Crop
    /// noise (attraction), Audio routes it to <c>SFX_Crop_Mandrake_Scream</c>. Players within 3 m without earmuffs get a
    /// 450-unit stagger (below the 900 ragdoll line). 3 s scream, 8 s cooldown. Clients see the replicated scream (shake).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CursedCropGoods))]
    public sealed class MandrakeScream : NetworkBehaviour
    {
        /// <summary>World tag (any active day modifier) that protects players from the stun (Earmuff Hat upgrade).</summary>
        public const string EarmuffWorldTag = "Gear.Earmuffs";

        [SerializeField] CursedCropTuning tuning;
        [SerializeField, Tooltip("Visual root shaken while screaming (client-local)")] Transform visual;
        [SerializeField, Tooltip("Mouth scaled open while screaming")] Transform mouth;

        readonly NetworkVariable<bool> _netScreaming = new NetworkVariable<bool>();
        CursedCropGoods _goods;
        float _lastStart = float.NegativeInfinity, _screamUntil;
        Vector3 _lastPos, _lastVel; float _accel; bool _hasLast;
        Vector3 _visualHome, _mouthHome;
        bool _screaming;

        /// <summary>Screams started (tests/debug).</summary>
        public int ScreamCount { get; private set; }
        /// <summary>Loudness of the last scream.</summary>
        public float LastLoudness { get; private set; }
        /// <summary>Players staggered by the last scream.</summary>
        public int LastStunned { get; private set; }
        /// <summary>True while screaming (replicated).</summary>
        public bool IsScreaming => IsSpawned && !IsServer ? _netScreaming.Value : _screaming;
        /// <summary>Burlap Sack consumable applied (-70% loudness). Set by whoever bags it.</summary>
        public bool Sacked { get; set; }
        /// <summary>Riding in a wagon (-20%). Set by the wagon.</summary>
        public bool InWagon { get; set; }
        /// <summary>Smoothed carrier acceleration (m/s^2, server).</summary>
        public float Acceleration => _accel;
        public CursedCropTuning Tuning => tuning != null ? tuning : CursedCropTuning.Default;
        bool Authority => !IsSpawned || IsServer;

        public void Configure(CursedCropTuning t, Transform visualRoot, Transform mouthT) { tuning = t; visual = visualRoot; mouth = mouthT; }

        void Awake()
        {
            _goods = GetComponent<CursedCropGoods>();
            _goods.Pulled += OnPulled;
            _goods.Impact += OnImpact;
            _goods.Grabbed += (_, __) => _hasLast = false;
            if (visual != null) _visualHome = visual.localPosition;
            if (mouth != null) _mouthHome = mouth.localScale;
        }

        public override void OnDestroy()
        {
            if (_goods != null) { _goods.Pulled -= OnPulled; _goods.Impact -= OnImpact; }
            base.OnDestroy();
        }

        void OnPulled(ulong puller) => TryScream(puller);

        void OnImpact(float impulseDesign)
        {
            if (CursedCropRules.IsJostle(0f, impulseDesign, float.MaxValue, Tuning.JostleImpact)) TryScream(_goods.LastHandlerId);
        }

        void FixedUpdate()
        {
            if (!Authority || _goods == null) return;
            if (!_goods.IsHeld || _goods.Planted) { _hasLast = false; _accel = 0f; return; }
            float dt = Time.fixedDeltaTime;
            Vector3 p = transform.position;
            if (!_hasLast) { _lastPos = p; _lastVel = Vector3.zero; _hasLast = true; return; }
            Vector3 v = (p - _lastPos) / dt;
            float a = (v - _lastVel).magnitude / dt;
            float k = 1f - Mathf.Exp(-dt / Tuning.AccelSmoothing);
            _accel = Mathf.Lerp(_accel, a, k);
            _lastPos = p; _lastVel = v;
            if (CursedCropRules.IsJostle(_accel, 0f, Tuning.JostleAccel, float.MaxValue)) TryScream(_goods.LastHandlerId);
        }

        /// <summary>Server: starts a scream unless cooling down. Returns true when it screamed. Public for tests/tools.</summary>
        public bool TryScream(ulong blamed)
        {
            if (!Authority) return false;
            float now = Time.time;
            if (!CursedCropRules.CanScream(now, _lastStart, Tuning.ScreamCooldown)) return false;
            _lastStart = now;
            _screamUntil = now + Tuning.ScreamSeconds;
            SetScreaming(true);
            ScreamCount++;
            LastLoudness = CursedCropRules.ScreamLoudness(Tuning.ScreamLoudness, Sacked, InWagon, Tuning.SackMult, Tuning.WagonMult);
            if (Services.TryGet(out INoiseService noise))
                noise.Emit(NoiseSource.Crop, LastLoudness, transform.position, IsSpawned ? NetworkObjectId : 0UL,
                           PlayerIds.IsPlayer(blamed) ? blamed : PlayerIds.None, NoiseTag.Mandrake);
            LastStunned = StunNearby(blamed);
            return true;
        }

        int StunNearby(ulong blamed)
        {
            bool earmuffs = Services.TryGet(out IDayModifierService mods) && mods.HasWorldTag(EarmuffWorldTag);
            int mask = LayerMask.GetMask(GameLayers.Player);
            if (mask == 0) return 0;
            var hits = Physics.OverlapSphere(transform.position, Tuning.StunRadius, mask, QueryTriggerInteraction.Ignore);
            int n = 0;
            var seen = new System.Collections.Generic.HashSet<IKnockable>();
            foreach (var h in hits)
            {
                if (!Knockables.TryGetFor(h.gameObject, out var k) || !seen.Add(k) || k.IsDown) continue;
                Vector3 away = h.transform.position - transform.position; away.y = 0f;
                float d = away.magnitude;
                if (!CursedCropRules.Stuns(d, Tuning.StunRadius, earmuffs)) continue;
                away = d > 1e-3f ? away / d : Vector3.forward;
                k.ApplyImpulse((away + Vector3.up * 0.2f).normalized * Tuning.StunImpulse, PlayerIds.IsPlayer(blamed) ? blamed : PlayerIds.Nature, BlameVerb.SpookedHerd);
                n++;
            }
            return n;
        }

        void SetScreaming(bool on)
        {
            _screaming = on;
            if (IsSpawned && IsServer) _netScreaming.Value = on;
        }

        void Update()
        {
            if (Authority && _screaming && Time.time >= _screamUntil) SetScreaming(false);
            bool s = IsScreaming;
            if (visual != null)
                visual.localPosition = s ? _visualHome + new Vector3(Mathf.Sin(Time.time * 71f), 0f, Mathf.Cos(Time.time * 63f)) * 0.012f : _visualHome;
            if (mouth != null)
                mouth.localScale = s ? Vector3.Scale(_mouthHome, new Vector3(1.2f, 1.9f + 0.4f * Mathf.Sin(Time.time * 40f), 1f)) : _mouthHome;
        }
    }
}
