using System;
using System.Collections.Generic;
using HarvestPanic.Core;
using Unity.Netcode;
using UnityEngine;

namespace HarvestPanic.Gameplay
{
    /// <summary>
    /// Player side of physics-carry (design/gdd/physics-carry.md; player-character 5f):
    /// owner aims a centre-screen ray (GrabRange from the camera), shows "E: Grab (n/m hands)", requests grabs,
    /// drops, charged throws (hold LMB up to 1 s), tool use (RMB) and teammate revives (hold E 1.5 s on a KO body).
    /// The server validates every request and runs the physics through <see cref="CarryBody"/>.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class PlayerCarry : NetworkBehaviour
    {
        /// <summary>Sentinel for "holding nothing".</summary>
        public const ulong NoObject = ulong.MaxValue;

        static readonly List<PlayerCarry> AllCarry = new List<PlayerCarry>();
        static readonly RaycastHit[] Hits = new RaycastHit[16];

        readonly NetworkVariable<ulong> _heldId = new NetworkVariable<ulong>(NoObject);
        readonly NetworkVariable<byte> _hands = new NetworkVariable<byte>(0);
        readonly NetworkVariable<float> _speedMult = new NetworkVariable<float>(1f);
        readonly NetworkVariable<float> _loadRatio = new NetworkVariable<float>(0f);
        readonly NetworkVariable<bool> _heldIsTool = new NetworkVariable<bool>(false);
        readonly NetworkVariable<float> _extraDrain = new NetworkVariable<float>(0f);

        PlayerCharacter _pc;
        PlayerToolbelt _belt;
        CarryBody _serverBody;
        float _throwHeldFor;
        bool _charging;
        float _interactDownAt = -1f;
        ulong _interactTarget = NoObject;
        float _reviveProgress;
        string _prompt = string.Empty;
        readonly List<Collider> _ignored = new List<Collider>();
        int _mask;

        /// <summary>Raised on the owner when the prompt text changes (HUD hook).</summary>
        public event Action<string> OnPromptChanged;

        /// <summary>Carry tuning.</summary>
        public CarryTuning CarryTuning => _pc.CarryTuning;
        /// <summary>Server-computed movement multiplier for this carrier.</summary>
        public float SpeedMultiplier => _speedMult.Value;
        /// <summary>Server-computed load ratio 0..1.</summary>
        public float LoadRatio => _loadRatio.Value;
        /// <summary>Extra stamina drain per second set by the server (lasso tug-of-war, farm-tools rule 3: 10/s).</summary>
        public float ExtraStaminaDrain => _extraDrain.Value;
        /// <summary>NetworkObjectId of the held object or <see cref="NoObject"/>.</summary>
        public ulong HeldId => _heldId.Value;
        /// <summary>Hands this player has on the held object.</summary>
        public int HandsUsed => _hands.Value;
        /// <summary>True on every machine while this player holds something.</summary>
        public bool IsHoldingReplicated => _heldId.Value != NoObject;
        /// <summary>True while the held object is a rigidly held tool.</summary>
        public bool HoldingTool => _heldIsTool.Value && IsHoldingReplicated;
        /// <summary>Held object transform on this machine (null when none / not spawned here).</summary>
        public Transform HeldObject => TryGetSpawned(_heldId.Value, out var no) ? no.transform : null;
        /// <summary>Owner: throw charge 0..1.</summary>
        public float ThrowCharge => _charging ? CarryMath.ThrowCharge(_throwHeldFor, CarryTuning.ThrowChargeTime) : 0f;
        /// <summary>Owner: the centre ray is on something grabbable.</summary>
        public bool AimingAtGrabbable { get; private set; }
        /// <summary>Owner: world point the centre ray hit on the aimed grabbable (valid while <see cref="AimingAtGrabbable"/>).</summary>
        public Vector3 AimPoint { get; private set; }
        /// <summary>Owner: hands required by the aimed grabbable (0 when none).</summary>
        public int AimHandsRequired { get; private set; }
        /// <summary>Owner: hands already on the aimed grabbable (other players).</summary>
        public int AimHandsAttached { get; private set; }
        /// <summary>Owner: revive progress 0..1 while holding E on a KO teammate.</summary>
        public float ReviveProgress => _reviveProgress;
        /// <summary>Owner: current prompt ("E: Grab (0/2 hands)", "E: Drop  LMB: Throw").</summary>
        public string Prompt => _prompt;
        /// <summary>Server: the body currently held.</summary>
        public CarryBody ServerBody => _serverBody;
        /// <summary>The player.</summary>
        public PlayerCharacter Character => _pc;

        void Awake()
        {
            _pc = GetComponent<PlayerCharacter>();
            _belt = GetComponent<PlayerToolbelt>();
            _mask = ~(LayerMask.GetMask(GameLayers.Water, "Ignore Raycast", "UI"));
        }

        public override void OnNetworkSpawn()
        {
            AllCarry.Add(this);
            _heldId.OnValueChanged += OnHeldChanged;
            OnHeldChanged(NoObject, _heldId.Value);
        }

        public override void OnNetworkDespawn()
        {
            AllCarry.Remove(this);
            _heldId.OnValueChanged -= OnHeldChanged;
            if (IsServer) ServerRelease(HandlingMode.Dropped);
        }

        bool TryGetSpawned(ulong id, out NetworkObject no)
        {
            no = null;
            return id != NoObject && NetworkManager != null && NetworkManager.SpawnManager != null &&
                   NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(id, out no) && no != null;
        }

        /// <summary>Total hands attached to <paramref name="objectId"/> across all players (any machine).</summary>
        public static int HandsOn(ulong objectId)
        {
            int n = 0;
            foreach (var c in AllCarry) if (c._heldId.Value == objectId) n += c._hands.Value;
            return n;
        }

        // ---- Hold point (all machines; server uses it for the spring target) -----------------------------

        /// <summary>World hold target in front of the eyes (tools: rigid right-hand offset).</summary>
        public Vector3 HoldPoint(bool tool)
        {
            var head = _pc.HeadPivot;
            var c = CarryTuning;
            if (tool) return head.TransformPoint(c.ToolHoldOffset);
            return head.position + head.forward * c.HoldDistance + Vector3.down * c.HoldDrop;
        }

        /// <summary>Current look direction.</summary>
        public Vector3 HeadPivotForward() => _pc.HeadPivot.forward;

        /// <summary>World rotation for a rigidly held tool.</summary>
        public Quaternion ToolRotation => _pc.HeadPivot.rotation;

        // ---- Owner input ---------------------------------------------------------------------------------

        void Update()
        {
            if (!IsSpawned || !IsOwner) return;
            var input = _pc.Input;
            string prompt = string.Empty;
            AimingAtGrabbable = false;
            AimHandsRequired = AimHandsAttached = 0;

            if (!_pc.HasControl)
            {
                _charging = false;
                _interactDownAt = -1f;
                _reviveProgress = 0f;
                if (_pc.CanGetUp) prompt = "SPACE: Get up";
                else if (_pc.State == PlayerState.KnockedOut) prompt = $"Knocked out ({_pc.KoRemaining:0}s)";
                SetPrompt(prompt);
                return;
            }

            if (IsHoldingReplicated)
            {
                bool pocketTool = _belt != null && _belt.HeldIsPocketTool;
                var heldT = HeldObject;
                var toolInteract = heldT != null ? heldT.GetComponent<IToolInteract>() : null;
                string toolE = toolInteract != null ? toolInteract.InteractPrompt : null;
                // A pocket tool in hand and something grabbable under the reticle: E puts the tool away and grabs it
                // (farm-tools rule 1: carries auto-holster the tool).
                ICarryable swapTarget = null; NetworkObject swapNo = null; Vector3 swapLocal = Vector3.zero;
                bool swapAim = pocketTool && string.IsNullOrEmpty(toolE) && TryAim(out swapTarget, out swapNo, out swapLocal);
                if (input.InteractPressed)
                {
                    if (!string.IsNullOrEmpty(toolE)) ToolInteractRpc();
                    else if (swapAim) SwapGrabRpc(swapNo.NetworkObjectId, swapLocal);
                    else if (pocketTool) _belt.RequestSelect(0);
                    else RequestRelease();
                }
                if (input.ThrowHeld) { _charging = true; _throwHeldFor += Time.deltaTime; }
                if (input.ThrowReleased && _charging) { RequestThrow(ThrowCharge); _charging = false; _throwHeldFor = 0f; }
                if (input.UsePressed) UseToolRpc(true);
                if (input.UseReleased) UseToolRpc(false);

                string eText = !string.IsNullOrEmpty(toolE) ? toolE
                    : swapAim ? (swapTarget is ICustomGrabPrompt sg ? sg.GrabPrompt : $"E: Grab ({HandsOn(swapNo.NetworkObjectId)}/{swapTarget.HandsRequired} hands)")
                    : pocketTool ? "E: Put away" : "E: Drop";
                prompt = eText + "  LMB: Throw";
                if (HoldingTool && heldT != null && heldT.TryGetComponent<IUsableTool>(out var tool)) prompt += "  " + tool.UsePrompt;
                var heldC = heldT != null ? heldT.GetComponent<ICarryable>() : null;
                if (heldC is ICustomGrabPrompt custom) prompt = custom.HoldPrompt;
                else
                {
                    int need = heldC != null ? heldC.HandsRequired : 0;
                    int have = HandsOn(_heldId.Value);
                    if (need > have) prompt += $"  (dragging {have}/{need} hands)";
                }
                if (_charging) prompt += $"  [{Mathf.RoundToInt(ThrowCharge * 100f)}%]";
                SetPrompt(prompt);
                return;
            }
            _charging = false;
            _throwHeldFor = 0f;

            bool found = TryAim(out var target, out var no, out var localPoint);
            var ko = found ? target as PlayerCharacter : null;
            AimingAtGrabbable = found;
            AimHandsRequired = found ? target.HandsRequired : 0;
            AimHandsAttached = found ? HandsOn(no.NetworkObjectId) : 0;
            if (found) AimPoint = target.CarryTransform.TransformPoint(localPoint);

            if (found)
            {
                int have = HandsOn(no.NetworkObjectId);
                prompt = target is ICustomGrabPrompt cg ? cg.GrabPrompt : $"E: Grab ({have}/{target.HandsRequired} hands)";
                if (ko != null) prompt = $"Tap E: Carry  Hold E: Revive";
            }

            // E: tap grabs; on a KO teammate holding E revives after ReviveHold.
            if (input.InteractPressed && found) { _interactDownAt = Time.time; _interactTarget = no.NetworkObjectId; }
            if (_interactDownAt >= 0f)
            {
                float held = Time.time - _interactDownAt;
                bool sameTarget = found && no.NetworkObjectId == _interactTarget;
                if (ko != null && sameTarget && input.InteractHeld && held > CarryTuning.TapVsHoldSeconds)
                {
                    _reviveProgress = Mathf.Clamp01(held / _pc.Tuning.ReviveHold);
                    prompt = $"Reviving... {Mathf.RoundToInt(_reviveProgress * 100f)}%";
                    if (_reviveProgress >= 1f) { ReviveRpc(_interactTarget); _interactDownAt = -1f; _reviveProgress = 0f; }
                }
                else if (ko == null && sameTarget)
                {
                    RequestGrabRpc(_interactTarget, localPoint);
                    _interactDownAt = -1f;
                }
                else if (!input.InteractHeld || !sameTarget)
                {
                    if (sameTarget && held <= CarryTuning.TapVsHoldSeconds) RequestGrabRpc(_interactTarget, localPoint);
                    _interactDownAt = -1f;
                    _reviveProgress = 0f;
                }
            }
            SetPrompt(prompt);
        }

        void SetPrompt(string p)
        {
            if (p == _prompt) return;
            _prompt = p;
            OnPromptChanged?.Invoke(p);
        }

        /// <summary>Owner: centre-screen ray from the camera to the nearest grabbable within GrabRange.</summary>
        public bool TryAim(out ICarryable target, out NetworkObject netObj, out Vector3 localPoint)
        {
            target = null; netObj = null; localPoint = Vector3.zero;
            var head = _pc.CameraPivot;
            int n = Physics.RaycastNonAlloc(head.position, head.forward, Hits, CarryTuning.GrabRange, _mask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            var heldT = IsHoldingReplicated ? HeldObject : null;
            for (int i = 0; i < n; i++)
            {
                var h = Hits[i];
                if (h.collider.transform.IsChildOf(transform) || h.distance >= best) continue;
                if (heldT != null && h.collider.transform.IsChildOf(heldT)) continue; // our own held tool
                best = h.distance;
                var c = h.collider.GetComponentInParent<ICarryable>();
                var no = c is Component comp ? comp.GetComponentInParent<NetworkObject>() : null;
                if (c == null || no == null || !no.IsSpawned || (c is IGrabGate g && !g.CanBeGrabbedBy(OwnerClientId))
                    || (c is CarryableProp hp && hp.IsHolstered))
                { target = null; netObj = null; continue; }
                target = c; netObj = no;
                localPoint = c.CarryTransform.InverseTransformPoint(h.point);
            }
            return target != null;
        }

        /// <summary>Owner: grabs whatever the centre ray is on (tests, bots). Returns false if nothing aimed.</summary>
        public bool TryGrabAimed()
        {
            if (!IsOwner || !TryAim(out _, out var no, out var lp)) return false;
            RequestGrabRpc(no.NetworkObjectId, lp);
            return true;
        }

        /// <summary>Owner: drop.</summary>
        public void RequestRelease() { if (IsOwner) ReleaseRpc(); }

        /// <summary>Owner: throw with charge 0..1 along the look direction.</summary>
        public void RequestThrow(float charge) { if (IsOwner) ThrowRpc(Mathf.Clamp01(charge)); }

        // ---- Server --------------------------------------------------------------------------------------

        [Rpc(SendTo.Server)]
        void RequestGrabRpc(ulong objectId, Vector3 localPoint)
        {
            if (!TryGetSpawned(objectId, out var no)) return;
            ServerGrab(no, localPoint, true);
        }

        /// <summary>
        /// Server: grabs <paramref name="no"/> at <paramref name="localPoint"/> (object space). Range-checked for client
        /// requests; tools taken out of the toolbelt and lasso auto-grabs skip the range check.
        /// </summary>
        public bool ServerGrab(NetworkObject no, Vector3 localPoint, bool checkRange)
        {
            if (!IsServer || _serverBody != null || !_pc.HasControlServer || no == null || !no.IsSpawned) return false;
            var carryable = no.GetComponentInChildren<ICarryable>();
            if (carryable == null || ReferenceEquals(carryable, _pc)) return false;
            if (carryable is IGrabGate gate && !gate.CanBeGrabbedBy(OwnerClientId)) return false;
            if (carryable is CarryableProp prop && prop.IsHolstered && prop.HolsteredBy != OwnerClientId) return false;
            Vector3 grabWorld = carryable.CarryTransform.TransformPoint(localPoint);
            float reach = CarryTuning.GrabRange + CarryTuning.GrabRangeSlack;
            if (checkRange && (grabWorld - _pc.HeadPivot.position).sqrMagnitude > reach * reach)
            {
                // Bounds-based fallback for big bodies whose grab point drifted.
                var col = no.GetComponentInChildren<Collider>();
                if (col == null || (col.ClosestPoint(_pc.HeadPivot.position) - _pc.HeadPivot.position).sqrMagnitude > reach * reach) return false;
            }
            var body = CarryBody.GetOrAdd(no, carryable);
            if (!body.TryAddGrip(this, localPoint, out int hands)) return false;
            _serverBody = body;
            _heldIsTool.Value = carryable.IsKinematicCarry;
            _hands.Value = (byte)hands;
            _heldId.Value = no.NetworkObjectId;
            return true;
        }

        [Rpc(SendTo.Server)]
        void ReleaseRpc() => ServerRelease(HandlingMode.Dropped);

        [Rpc(SendTo.Server)]
        void ThrowRpc(float charge)
        {
            var body = _serverBody;
            if (body == null) return;
            if (!CarryMath.CanThrow(body.GripCount, body.HandsAttached, body.Carryable.HandsRequired, body.Carryable.MassKg, CarryTuning.HeaveMaxMass))
            {
                ServerRelease(HandlingMode.Dropped);
                return;
            }
            var c = CarryTuning;
            float speed = CarryMath.ThrowSpeed(charge, body.Carryable.MassKg, c.ThrowSpeedMin, c.ThrowSpeedMax, c.ThrowMassRef, c.ThrowMassFloor);
            Vector3 dir = (_pc.HeadPivot.forward + Vector3.up * 0.08f).normalized;
            ClearServerHold();
            body.ThrowFrom(this, dir * speed + _pc.Velocity);
            if (_belt != null) _belt.ServerOnThrown(body);
        }

        [Rpc(SendTo.Server)]
        void ToolInteractRpc()
        {
            if (_serverBody == null) return;
            if (_serverBody.TryGetComponent<IToolInteract>(out var t) && t.ServerInteract(this)) return;
            // Fallback: the default E action.
            if (_belt != null && _serverBody.NetObject != null && _belt.SlotOf(_serverBody.NetObject.NetworkObjectId) > 0) _belt.ServerSelect(0);
            else ServerRelease(HandlingMode.Dropped);
        }

        [Rpc(SendTo.Server)]
        void SwapGrabRpc(ulong objectId, Vector3 localPoint)
        {
            if (!TryGetSpawned(objectId, out var no) || _serverBody == null || _belt == null) return;
            if (_serverBody.NetObject == null || _belt.SlotOf(_serverBody.NetObject.NetworkObjectId) <= 0) return; // only pocket tools auto-holster
            ServerRelease(HandlingMode.Dropped); // holsters it (ServerOnReleased)
            _belt.ServerSelect(0);
            ServerGrab(no, localPoint, true);
        }

        [Rpc(SendTo.Server)]
        void UseToolRpc(bool down)
        {
            if (_serverBody != null && _serverBody.TryGetComponent<IUsableTool>(out var tool)) tool.ServerUse(down, this);
        }

        [Rpc(SendTo.Server)]
        void ReviveRpc(ulong targetObjectId)
        {
            if (!TryGetSpawned(targetObjectId, out var no) || !no.TryGetComponent<PlayerCharacter>(out var target)) return;
            float reach = CarryTuning.GrabRange + CarryTuning.GrabRangeSlack + 0.5f;
            if ((target.transform.position - transform.position).sqrMagnitude > reach * reach) return;
            target.ServerRevive(OwnerClientId);
        }

        /// <summary>Server: lets go of whatever is held (drop, ragdoll, despawn, slip).</summary>
        public void ServerRelease(HandlingMode mode)
        {
            if (!IsServer) return;
            var body = _serverBody;
            ClearServerHold();
            if (body == null) return;
            body.RemoveGrip(this, mode);
            if (_belt != null) _belt.ServerOnReleased(body, mode); // pocket tools go back on the belt
        }

        /// <summary>Server: extra stamina drain per second for this player (0 clears it). Lasso tug-of-war uses 10/s.</summary>
        public void ServerSetExtraDrain(float perSecond)
        {
            if (!IsServer || !IsSpawned) return;
            perSecond = Mathf.Max(0f, perSecond);
            if (Mathf.Abs(_extraDrain.Value - perSecond) > 0.01f) _extraDrain.Value = perSecond;
        }

        void ClearServerHold()
        {
            _serverBody = null;
            if (!IsSpawned) return;
            _heldId.Value = NoObject;
            _hands.Value = 0;
            _speedMult.Value = 1f;
            _loadRatio.Value = 0f;
            _heldIsTool.Value = false;
        }

        /// <summary>Server: called by <see cref="CarryBody"/> every physics step with the shared load numbers.</summary>
        internal void ServerSetLoad(float speedMult, float loadRatio, int hands)
        {
            if (Mathf.Abs(_speedMult.Value - speedMult) > 0.005f) _speedMult.Value = speedMult;
            if (Mathf.Abs(_loadRatio.Value - loadRatio) > 0.005f) _loadRatio.Value = loadRatio;
            if (_hands.Value != hands) _hands.Value = (byte)hands;
        }

        /// <summary>Server: <see cref="CarryBody"/> broke this grip (slip / object despawned).</summary>
        internal void ServerOnGripLost() => ClearServerHold();

        // ---- All machines: no self-collision with the held object -------------------------------------------

        void OnHeldChanged(ulong prev, ulong next)
        {
            var mine = GetComponents<Collider>();
            foreach (var other in _ignored)
                if (other != null) foreach (var m in mine) Physics.IgnoreCollision(m, other, false);
            _ignored.Clear();
            if (!TryGetSpawned(next, out var no)) return;
            no.GetComponentsInChildren(_ignored);
            foreach (var other in _ignored)
                foreach (var m in mine) Physics.IgnoreCollision(m, other, true);
        }
    }
}
