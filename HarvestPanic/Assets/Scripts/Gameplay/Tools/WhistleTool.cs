using System.Collections.Generic;
using HarvestPanic.Core;
using Unity.Netcode;
using UnityEngine;

namespace HarvestPanic.Gameplay
{
    /// <summary>
    /// Whistle (design/gdd/farm-tools.md rule 5, section 5 "Whistle scare during a carry"; GDD 5.8). Press Use for a
    /// short blow: a Tool noise (loudness 0.7) tagged <see cref="NoiseTag.Whistle"/>; every critter answers through its
    /// own Critter Personality (pigs and chickens flee, cows and goats ignore it). Hold Use for 1 s for a long blow
    /// tagged only <see cref="NoiseTag.WhistleLong"/> (rally: Lure/Rally responders come, nobody else is scared).
    /// 3 s cooldown. A short blow also makes critters that players are carrying within earshot struggle harder
    /// (+50% for 2 s); if one wriggles free in that window the whistler is blamed for SpookedHerd.
    /// </summary>
    public sealed class WhistleTool : CarryableProp, IUsableTool, IToolArmPose
    {
        [SerializeField] ToolTuning tuning;
        [SerializeField, Tooltip("Visual root moved to the lips while blowing")] Transform visual;
        [SerializeField, Tooltip("Right-hand target ('Grip')")] Transform grip;
        [SerializeField, Tooltip("Lips position in head space (m)")] Vector3 mouth = new Vector3(0.015f, -0.11f, 0.17f);

        readonly NetworkVariable<bool> _blowing = new NetworkVariable<bool>(false);
        readonly NetworkVariable<double> _readyAt = new NetworkVariable<double>(0);
        readonly NetworkVariable<byte> _blowCount = new NetworkVariable<byte>(0);

        static readonly List<CarryBody> Spooked = new List<CarryBody>();
        readonly List<ICarryableEvents> _watched = new List<ICarryableEvents>();
        float _downAt = -1f, _watchUntil, _blend, _toot;
        bool _longSent;
        PlayerCarry _user;
        Vector3 _visualRest;
        byte _seenBlows;

        /// <summary>Blows so far (every peer; presentation and tests).</summary>
        public int BlowCount => _blowCount.Value;
        /// <summary>True while Use is held (every peer).</summary>
        public bool IsBlowing => _blowing.Value;
        /// <summary>Server: the last blow (tests).</summary>
        public WhistleBlow LastBlow { get; private set; }
        /// <summary>Server: carried critters scared by the last short blow (tests).</summary>
        public int LastSpooked { get; private set; }

        double Now => IsSpawned && NetworkManager != null ? NetworkManager.ServerTime.Time : Time.timeAsDouble;

        /// <summary>True when the cooldown has passed.</summary>
        public bool Ready => WhistleMath.Ready((float)(Now - _readyAt.Value), 0f);

        /// <inheritdoc />
        public string UsePrompt => Ready ? "RMB: Whistle (hold: rally)" : "Catching breath...";

        /// <inheritdoc />
        public ArmPose ArmPose => _blowing.Value || _blend > 0.2f ? ArmPose.WhistleBlow : ArmPose.Tool;

        protected override void Awake()
        {
            base.Awake();
            if (tuning == null) tuning = ScriptableObject.CreateInstance<ToolTuning>();
            if (visual != null) _visualRest = visual.localPosition;
        }

        /// <summary>Editor builder: wires children.</summary>
        public void Configure(ToolTuning tt, Transform visualRoot, Transform gripPoint)
        {
            tuning = tt; visual = visualRoot; grip = gripPoint;
            if (visual != null) _visualRest = visual.localPosition;
        }

        /// <inheritdoc />
        public void ServerUse(bool down, PlayerCarry user)
        {
            if (!IsServer || user == null) return;
            if (down)
            {
                if (_downAt >= 0f || Now < _readyAt.Value) return;
                _downAt = Time.time;
                _longSent = false;
                _user = user;
                _blowing.Value = true;
                return;
            }
            if (_downAt < 0f) return;
            if (!_longSent) Blow(WhistleBlow.Short);
            _downAt = -1f;
            _blowing.Value = false;
        }

        public override void OnReleased(ulong clientId)
        {
            base.OnReleased(clientId);
            if (!IsServer) return;
            _downAt = -1f;
            _blowing.Value = false;
        }

        /// <summary>Server: blows now (tests and bots; normally through Use).</summary>
        public void ServerBlow(WhistleBlow kind, PlayerCarry user)
        {
            if (!IsServer) return;
            _user = user;
            Blow(kind);
        }

        void Blow(WhistleBlow kind)
        {
            LastBlow = kind;
            _readyAt.Value = Now + tuning.WhistleCooldown;
            _blowCount.Value = (byte)(_blowCount.Value + 1);
            ulong who = _user != null ? _user.OwnerClientId : PlayerIds.None;
            Vector3 at = _user != null ? _user.Character.HeadPivot.position : transform.position;
            NoiseService.TryEmit(NoiseSource.Tool, tuning.WhistleLoudness, at, NetworkObjectId, who, WhistleMath.TagsFor(kind));
            LastSpooked = kind == WhistleBlow.Short ? ScareCarried(at, who) : 0;
        }

        /// <summary>
        /// Short blow: carried critters within earshot struggle harder for 2 s; watch them so an escape in that window
        /// is the whistler's SpookedHerd.
        /// </summary>
        int ScareCarried(Vector3 at, ulong who)
        {
            float radius = NoiseRadius(tuning.WhistleLoudness);
            int critterLayer = LayerMask.NameToLayer(GameLayers.Critter);
            UnwatchAll();
            Spooked.Clear();
            foreach (var pc in PlayerCharacter.All)
            {
                var body = pc != null && pc.Carry != null ? pc.Carry.ServerBody : null;
                if (body == null || Spooked.Contains(body)) continue;
                if (body.gameObject.layer != critterLayer) continue;
                if (!WhistleMath.InEarshot(Vector3.Distance(at, body.transform.position), radius)) continue;
                Spooked.Add(body);
                body.BoostStruggle(tuning.WhistleStruggleBoost, tuning.WhistleStruggleSeconds);
                if (body.Carryable is ICarryableEvents ev) { ev.OnForcedRelease += OnSpookedEscape; _watched.Add(ev); }
            }
            _watchUntil = Time.time + tuning.WhistleStruggleSeconds;
            _spookedBy = who;
            return Spooked.Count;
        }

        ulong _spookedBy = PlayerIds.None;

        void OnSpookedEscape(ulong[] holders)
        {
            if (Time.time > _watchUntil) return;
            ToolBlame.Emit(BlameVerb.SpookedHerd, _spookedBy, string.Empty, "Tool.Whistle", transform.position, "Tool.Whistle");
        }

        void UnwatchAll()
        {
            foreach (var ev in _watched) if (ev != null) ev.OnForcedRelease -= OnSpookedEscape;
            _watched.Clear();
        }

        /// <summary>Audible radius the noise service gives a loudness (NoiseTuning curve x Noise.EnvMult).</summary>
        static float NoiseRadius(float loudness)
        {
            var nt = Resources.Load<NoiseTuning>(NoiseTuning.ResourcePath);
            float r = nt != null ? NoiseBroadcaster.RadiusFor(loudness, nt.RadiusMin, nt.RadiusMax, nt.Exponent) : NoiseBroadcaster.RadiusFor(loudness);
            if (Services.TryGet(out IDayModifierService day)) r *= day.Resolve(DayParams.NoiseEnvMult, 1f);
            return r;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer) UnwatchAll();
            base.OnNetworkDespawn();
        }

        void Update()
        {
            if (IsSpawned && IsServer)
            {
                if (_downAt >= 0f && !_longSent && WhistleMath.Classify(Time.time - _downAt, tuning.WhistleLongSeconds) == WhistleBlow.Long)
                {
                    _longSent = true;
                    Blow(WhistleBlow.Long);
                }
                if (_watched.Count > 0 && Time.time > _watchUntil) UnwatchAll();
            }

            // Presentation: lift the whistle to the lips while blowing; a puff on every blow.
            bool blowing = IsSpawned && _blowing.Value;
            if (IsSpawned && _blowCount.Value != _seenBlows) { _seenBlows = _blowCount.Value; _toot = 0.25f; }
            _toot = Mathf.Max(0f, _toot - Time.deltaTime);
            _blend = Mathf.MoveTowards(_blend, blowing || _toot > 0f ? 1f : 0f, Time.deltaTime * 9f);
            if (visual == null) return;
            Vector3 lips = Vector3.zero;
            var ct = PlayerCharacter.All.Count > 0 ? PlayerCharacter.All[0].CarryTuning : null;
            if (ct != null) lips = mouth - ct.ToolHoldOffset; // tool space == head space offset by the hold point
            visual.localPosition = Vector3.Lerp(_visualRest, _visualRest + lips, _blend);
            visual.localScale = Vector3.one * (1f + Mathf.Sin(_toot * 40f) * 0.06f * (_toot > 0f ? 1f : 0f));
        }
    }
}
