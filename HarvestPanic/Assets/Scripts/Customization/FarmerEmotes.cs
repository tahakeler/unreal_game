using System.Collections;
using System.Collections.Generic;
using HarvestPanic.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HarvestPanic.Customization
{
    /// <summary>
    /// Emotes on keys 5-8 (GDD "Emotes": Wave / Point / Dance / Faint by default; rebindable).
    /// Owner presses a key -> client cooldown check -> <see cref="RequestEmoteRpc"/> -> server validates (equipped,
    /// per-emote cooldown, <see cref="CosmeticCatalog.emoteRateLimit"/> per second) -> Wave emits an
    /// <see cref="INoiseService"/> shout, Faint goes out reliable and calls <see cref="IPlayerHooks.Faint"/> on every peer
    /// (the Player drops carried items on the server), other emotes go out unreliable and play on the body.
    /// </summary>
    [RequireComponent(typeof(FarmerLoadoutSync))]
    public sealed class FarmerEmotes : NetworkBehaviour
    {
        /// <summary>Default keyboard bindings for key slots 0..3.</summary>
        public static readonly string[] DefaultBindings = { "<Keyboard>/5", "<Keyboard>/6", "<Keyboard>/7", "<Keyboard>/8" };

        readonly InputAction[] _actions = new InputAction[4];
        readonly Dictionary<ushort, float> _clientReadyAt = new Dictionary<ushort, float>();
        readonly Dictionary<ushort, double> _serverReadyAt = new Dictionary<ushort, double>();
        double _bucketTime;
        float _bucket;
        FarmerLoadoutSync _sync;
        Coroutine _faintRoutine;

        FarmerLoadoutSync Sync => _sync != null ? _sync : _sync = GetComponent<FarmerLoadoutSync>();
        FarmerVisual Visual => Sync.Visual;
        CosmeticCatalog Catalog => Visual.Catalog;

        /// <summary>Key-slot action (0..3 = keys 5..8) for rebinding UIs.</summary>
        public InputAction ActionForSlot(int slot) => _actions[slot];

        /// <summary>True while the local farmer may not emote (wardrobe open).</summary>
        public static bool InputBlocked { get; set; }

        void Awake()
        {
            for (int i = 0; i < 4; i++)
            {
                int slot = i;
                _actions[i] = new InputAction("Emote" + (i + 5), InputActionType.Button, DefaultBindings[i]);
                _actions[i].performed += _ => OnKey(slot);
            }
        }

        void Update()
        {
            bool want = Sync.IsLocalFarmer && !Visual.IsPreview;
            for (int i = 0; i < 4; i++)
            {
                if (want && !_actions[i].enabled) _actions[i].Enable();
                else if (!want && _actions[i].enabled) _actions[i].Disable();
            }
        }

        void OnDisable() { foreach (var a in _actions) a?.Disable(); }

        /// <inheritdoc/>
        public override void OnDestroy()
        {
            foreach (var a in _actions) a?.Dispose();
            base.OnDestroy();
        }

        void OnKey(int slot)
        {
            if (InputBlocked) return;
            TryEmote(Sync.Loadout.GetEmote(slot));
        }

        /// <summary>Local player: requests emote <paramref name="emoteId"/>. Returns false when on cooldown/unknown.</summary>
        public bool TryEmote(ushort emoteId)
        {
            var def = Catalog != null ? Catalog.FindEmote(emoteId) : null;
            if (def == null || !Sync.IsLocalFarmer) return false;
            if (_clientReadyAt.TryGetValue(emoteId, out float ready) && Time.time < ready) return false;
            _clientReadyAt[emoteId] = Time.time + def.cooldown;

            Vector3 aim = def.placesPing ? AimPoint() : Vector3.zero;
            if (IsSpawned) RequestEmoteRpc(emoteId, aim);
            else Execute(def, aim, emitNoise: false);
            return true;
        }

        Vector3 AimPoint()
        {
            var cam = Camera.main;
            Vector3 origin = cam != null ? cam.transform.position : transform.position + Vector3.up * 1.6f;
            Vector3 dir = cam != null ? cam.transform.forward : transform.forward;
            float max = Catalog.pingMaxDistance;
            int mask = ~0;
            int player = LayerMask.NameToLayer(GameLayers.Player);
            if (player >= 0) mask &= ~(1 << player);
            return Physics.Raycast(origin, dir, out var hit, max, mask, QueryTriggerInteraction.Ignore) ? hit.point : origin + dir * Mathf.Min(max, 15f);
        }

        /// <summary>Owner -> server: play an emote.</summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void RequestEmoteRpc(ushort emoteId, Vector3 aim)
        {
            var def = Catalog.FindEmote(emoteId);
            if (def == null || !IsEquipped(emoteId)) return;
            double now = NetworkManager.ServerTime.Time;
            if (_serverReadyAt.TryGetValue(emoteId, out double ready) && now < ready - 0.1) return;
            if (!TakeToken(now)) return;
            _serverReadyAt[emoteId] = now + def.cooldown;
            if (Vector3.Distance(aim, transform.position) > Catalog.pingMaxDistance + 2f) aim = transform.position;

            if (def.noiseLoudness > 0f && Services.TryGet(out INoiseService noise))
                noise.Emit(NoiseSource.Shout, def.noiseLoudness, transform.position + Vector3.up * 1.6f, NetworkObjectId, OwnerClientId, NoiseTag.None);

            if (def.kind == EmoteKind.Faint)
            {
                FaintRpc(true);
                if (_faintRoutine != null) StopCoroutine(_faintRoutine);
                _faintRoutine = StartCoroutine(EndFaintAfter(def.duration));
            }
            else PlayEmoteRpc(emoteId, aim);
        }

        IEnumerator EndFaintAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            _faintRoutine = null;
            if (IsSpawned) FaintRpc(false);
        }

        bool IsEquipped(ushort id)
        {
            var l = Sync.Loadout;
            return l.Emote0 == id || l.Emote1 == id || l.Emote2 == id || l.Emote3 == id;
        }

        bool TakeToken(double now)
        {
            float rate = Mathf.Max(0.1f, Catalog.emoteRateLimit);
            _bucket = Mathf.Min(rate, _bucket + (float)(now - _bucketTime) * rate);
            _bucketTime = now;
            if (_bucket < 1f) return false;
            _bucket -= 1f;
            return true;
        }

        /// <summary>Server -> everyone (unreliable): play an animation emote.</summary>
        [Rpc(SendTo.Everyone, Delivery = RpcDelivery.Unreliable, InvokePermission = RpcInvokePermission.Server)]
        void PlayEmoteRpc(ushort emoteId, Vector3 aim)
        {
            var def = Catalog.FindEmote(emoteId);
            if (def != null) Execute(def, aim, emitNoise: false);
        }

        /// <summary>Server -> everyone (reliable, it changes physics): faint start/end.</summary>
        [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
        void FaintRpc(bool fainted) => SetFainted(fainted);

        void Execute(EmoteDefinition def, Vector3 aim, bool emitNoise)
        {
            if (def.kind == EmoteKind.Faint)
            {
                if (IsSpawned) return; // networked faint goes through FaintRpc
                SetFainted(true);
                if (_faintRoutine != null) StopCoroutine(_faintRoutine);
                _faintRoutine = StartCoroutine(OfflineFaint(def.duration));
                return;
            }
            Visual.PlayEmote(def.key);
            if (def.placesPing) PointPing.Spawn(aim, Catalog.NameTag(Sync.Loadout.NameTag).color, Catalog.pingLifetime, Catalog.unlitMaterial);
        }

        IEnumerator OfflineFaint(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            _faintRoutine = null;
            SetFainted(false);
        }

        void SetFainted(bool fainted)
        {
            var owner = Visual.Owner;
            if (owner != null) owner.Faint(fainted); // Player: ragdoll + drop carried items (server), may forward to the body pose
            else Visual.Faint(fainted);
        }
    }
}
