using System;
using System.Collections.Generic;
using HarvestPanic.Core;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace HarvestPanic.Gameplay
{
    /// <summary>
    /// Player root (design/gdd/player-character.md). Owns the server-authoritative <see cref="PlayerState"/>,
    /// ragdoll / knockout / revive / barn respawn, holler and footstep noise, and exposes the Customization
    /// hooks (<see cref="IPlayerHooks"/>). A knocked-out player is itself an <see cref="ICarryable"/>
    /// (2 hands, 70 kg) whose body is simulated by its owner.
    ///
    /// Network model: movement is owner-authoritative (CharacterController + NetworkTransform in Owner mode),
    /// which gives instant response without a reconciliation layer NGO does not provide. Every shared outcome
    /// (state, carry, impulses, noise, fees) is decided on the server.
    /// </summary>
    [DefaultExecutionOrder(-60)]
    public sealed class PlayerCharacter : NetworkBehaviour, IPlayerHooks, IKnockable, IDraggable, ICarryable, IGrabGate, ISelfSimulatedCarryable
    {
        [Header("Config")]
        [SerializeField] PlayerTuning tuning;
        [SerializeField] CarryTuning carryTuning;

        [Header("Rig")]
        [SerializeField] Transform headPivot;
        [SerializeField] Transform cameraPivot;
        [SerializeField] GameObject viewCamera;
        [SerializeField] GameObject firstPersonCamera;
        [SerializeField] Transform firstPersonArms;
        [SerializeField] Transform bodyRoot;
        [SerializeField] Renderer bodyRenderer;
        [SerializeField] Animator bodyAnimator;
        [SerializeField] Transform headAttachPoint;
        [SerializeField] CapsuleCollider ragdollCollider;

        static readonly List<PlayerCharacter> AllPlayers = new List<PlayerCharacter>();

        /// <summary>Every spawned player on this machine.</summary>
        public static IReadOnlyList<PlayerCharacter> All => AllPlayers;
        /// <summary>The locally controlled player (null on a dedicated server).</summary>
        public static PlayerCharacter Local { get; private set; }
        /// <summary>Raised when a player spawns locally (any owner).</summary>
        public static event Action<PlayerCharacter> OnPlayerSpawned;
        /// <summary>Raised on every machine when this player's state changes.</summary>
        public event Action<PlayerState, PlayerState> OnStateChanged;

        readonly NetworkVariable<PlayerState> _state = new NetworkVariable<PlayerState>(PlayerState.Normal);
        readonly NetworkVariable<ulong> _carriedBy = new NetworkVariable<ulong>(PlayerIds.None);
        readonly NetworkVariable<float> _pitch = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<byte> _moveMode = new NetworkVariable<byte>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<byte> _stamina = new NetworkVariable<byte>(100, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        CharacterController _cc;
        Rigidbody _rb;
        NetworkTransform _nt;
        PlayerMotor _motor;
        PlayerInputReader _input;
        KnockoutTracker _koTracker;
        RestTracker _rest;
        Vector3 _lastPos;
        float _footDistance;
        float _stateUntil;            // server: KO end / respawn time
        float _hollerReadyAt;         // server
        float _ownerHollerReadyAt;    // owner (cosmetic gate)
        float _controlLockUntil;      // owner: get-up animation
        bool _physicsMode;
        ulong _lastThrower = PlayerIds.None;
        float _lastThrownAt = -100f;
        BlameVerb _respawnCause;
        int _playerLayer, _ragdollLayer;
        bool _offlineLocal;
        IPlayerHooks _body;
        int _spawnPoseFrames;

        // ---- Accessors -------------------------------------------------------------------------------------

        /// <summary>Movement tuning.</summary>
        public PlayerTuning Tuning => tuning;
        /// <summary>Carry tuning.</summary>
        public CarryTuning CarryTuning => carryTuning;
        /// <summary>Carry component.</summary>
        public PlayerCarry Carry { get; private set; }
        /// <summary>Owner motor.</summary>
        public PlayerMotor Motor => _motor;
        /// <summary>Input reader (owner).</summary>
        public PlayerInputReader Input => _input;
        /// <summary>Pitch pivot at the eyes; forward = look direction on every machine.</summary>
        public Transform HeadPivot => headPivot;
        /// <summary>Bob/shake pivot under the head (camera and arms live here).</summary>
        public Transform CameraPivot => cameraPivot;
        /// <summary>Body visual root (third-person placeholder; Customization may replace its children).</summary>
        public Transform BodyRoot => bodyRoot;
        /// <summary>Owner camera object (Camera + CinemachineBrain).</summary>
        public GameObject ViewCamera => viewCamera;
        /// <summary>Current state.</summary>
        public PlayerState State => _state.Value;
        /// <summary>True when the player has input control.</summary>
        public bool HasControl => PlayerStateMachine.HasControl(State) && Time.time >= _controlLockUntil;
        /// <summary>Server-side control check (no client get-up lock).</summary>
        public bool HasControlServer => State == PlayerState.Normal;
        /// <inheritdoc />
        public bool IsDown => State != PlayerState.Normal;
        /// <summary>Who is carrying this (KO) body, or <see cref="PlayerIds.None"/>.</summary>
        public ulong CarriedBy => _carriedBy.Value;
        /// <summary>Replicated stamina (0..100).</summary>
        public float StaminaReplicated => _stamina.Value;
        /// <summary>Replicated move mode: 0 walk, 1 sprint, 2 crouch.</summary>
        public byte MoveMode => _moveMode.Value;
        /// <summary>Estimated world velocity (all machines; from transform deltas).</summary>
        public Vector3 Velocity { get; private set; }
        /// <summary>Owner-check that also works before spawn (offline previews).</summary>
        public bool IsOwnerSafe => IsSpawned ? IsOwner : _offlineLocal;
        /// <summary>Seconds of KO left (all machines approximate).</summary>
        public float KoRemaining => State == PlayerState.KnockedOut ? Mathf.Max(0f, _stateUntil - Time.time) : 0f;
        /// <summary>Owner: ragdoll has come to rest and the player may press Jump to get up.</summary>
        public bool CanGetUp => State == PlayerState.Ragdoll && _rest != null && _rest.AtRest;

        // ---- IPlayerHooks ----------------------------------------------------------------------------------

        /// <inheritdoc />
        public ulong ClientId => IsSpawned ? OwnerClientId : 0UL;
        /// <inheritdoc />
        public bool IsLocal => IsOwnerSafe;
        /// <summary>Customization's FarmerVisual (nested FarmerBody prefab) when present; visual members forward to it.</summary>
        public IPlayerHooks BodyVisual => _body;
        /// <inheritdoc />
        public Renderer BodyRenderer => _body != null ? _body.BodyRenderer : bodyRenderer;
        /// <inheritdoc />
        public Animator BodyAnimator => _body != null ? _body.BodyAnimator : bodyAnimator;
        /// <inheritdoc />
        public Transform FirstPersonArms => firstPersonArms;
        /// <inheritdoc />
        public Transform HeadAttachPoint => _body != null && _body.HeadAttachPoint != null ? _body.HeadAttachPoint : headAttachPoint;

        /// <inheritdoc />
        public void PlayEmote(string emoteId)
        {
            // Visual only: Customization emits emote noise server-side; the Holler key (H) is separate.
            _body?.PlayEmote(emoteId);
        }

        /// <inheritdoc />
        public void Faint(bool fainted)
        {
            _body?.Faint(fainted);
            if (!IsSpawned) return;
            if (fainted && State == PlayerState.Normal) { if (IsServer) ServerEnterRagdoll(Vector3.zero, PlayerIds.None, false); else if (IsOwner) OwnerTrip(); }
            else if (!fainted && State == PlayerState.Ragdoll && IsOwner) GetUpRpc();
        }

        // ---- ICarryable (knocked-out body) -----------------------------------------------------------------

        /// <inheritdoc />
        public Transform CarryTransform => transform;
        /// <inheritdoc />
        public int HandsRequired => 2;
        /// <inheritdoc />
        public float MassKg => tuning != null ? tuning.MassKg : 70f;
        /// <inheritdoc />
        public bool IsKinematicCarry => false;
        /// <inheritdoc />
        public bool CanBeGrabbedBy(ulong clientId) => State == PlayerState.KnockedOut && clientId != OwnerClientId && CarriedBy == PlayerIds.None;
        /// <inheritdoc />
        public void OnGrabbed(ulong clientId) { if (IsServer) _carriedBy.Value = clientId; }
        /// <inheritdoc />
        public void OnReleased(ulong clientId) { if (IsServer && _carriedBy.Value == clientId) _carriedBy.Value = PlayerIds.None; }
        /// <inheritdoc />
        public void ApplyThrow(Vector3 velocity, ulong thrower)
        {
            if (!IsServer) return;
            _lastThrower = thrower;
            _lastThrownAt = Time.time;
            LaunchOwnerRpc(velocity, true);
        }

        // ---- Lifecycle -------------------------------------------------------------------------------------

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _rb = GetComponent<Rigidbody>();
            _nt = GetComponent<NetworkTransform>();
            _motor = GetComponent<PlayerMotor>();
            _input = GetComponent<PlayerInputReader>();
            Carry = GetComponent<PlayerCarry>();
            foreach (var h in GetComponentsInChildren<IPlayerHooks>(true))
                if (!ReferenceEquals(h, this)) { _body = h; break; }
            _playerLayer = LayerMask.NameToLayer(GameLayers.Player);
            _ragdollLayer = LayerMask.NameToLayer(GameLayers.Ragdoll);
            _koTracker = new KnockoutTracker(tuning.KoRagdollCount, tuning.KoWindow);
            _rest = new RestTracker(tuning.RestSpeed, tuning.RestTime);
            if (_rb != null) { _rb.isKinematic = true; _rb.mass = tuning.MassKg; }
            if (ragdollCollider != null) ragdollCollider.enabled = false;
            SetEyeHeight(tuning.StandHeight * 0.5f + tuning.EyeAboveCentre);
        }

        /// <summary>Configures an unspawned instance for previews/screenshots (no networking).</summary>
        public void ConfigureOffline(bool local)
        {
            _offlineLocal = local;
            GetComponent<PlayerView>()?.ApplyViewMode();
        }

        public override void OnNetworkSpawn()
        {
            AllPlayers.Add(this);
            _lastPos = transform.position;
            _state.OnValueChanged += HandleStateChanged;
            if (IsOwner)
            {
                Local = this;
                _input.Activate();
                _motor.InitOwner();
                _spawnPoseFrames = 2; // re-commit the server spawn pose through the owner NetworkTransform
                DisableForeignCameras();
            }
            ApplyPhysicsMode(PlayerStateMachine.IsPhysicsDriven(State) || State == PlayerState.Respawning);
            GetComponent<PlayerView>()?.ApplyViewMode();
            OnPlayerSpawned?.Invoke(this);
        }

        public override void OnNetworkDespawn()
        {
            AllPlayers.Remove(this);
            _state.OnValueChanged -= HandleStateChanged;
            if (Local == this) Local = null;
            if (IsServer)
            {
                Carry?.ServerRelease(HandlingMode.Dropped);
                ReleaseFromCarrier();
            }
        }

        /// <summary>Finds a spawned player by client id.</summary>
        public static PlayerCharacter Find(ulong clientId)
        {
            foreach (var p in AllPlayers) if (p.OwnerClientId == clientId) return p;
            return null;
        }

        void DisableForeignCameras()
        {
            foreach (var cam in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (!cam.transform.IsChildOf(transform) && cam.CompareTag("MainCamera")) cam.gameObject.SetActive(false);
        }

        // ---- Owner helpers called by the motor ---------------------------------------------------------------

        /// <summary>Applies camera pitch locally and replicates it (owner).</summary>
        public void ApplyPitch(float pitch)
        {
            headPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            if (IsSpawned && IsOwner && Mathf.Abs(_pitch.Value - pitch) > 0.2f) _pitch.Value = pitch;
        }

        /// <summary>Moves the eye pivot (crouch).</summary>
        public void SetEyeHeight(float y)
        {
            var p = headPivot.localPosition;
            headPivot.localPosition = new Vector3(p.x, y, p.z);
        }

        /// <summary>Owner publishes cheap state the server needs (move mode for footsteps, stamina for the ragdoll formula).</summary>
        public void PublishOwnerState(bool sprinting, bool crouched, float stamina)
        {
            if (!IsSpawned || !IsOwner) return;
            OwnerTickDrag();
            byte mode = (byte)(crouched ? 2 : sprinting ? 1 : 0);
            if (_moveMode.Value != mode) _moveMode.Value = mode;
            byte s = (byte)Mathf.Clamp(Mathf.RoundToInt(stamina), 0, 255);
            if (_stamina.Value != s) _stamina.Value = s;
        }

        /// <summary>Owner: loaded-sprint slope trip. Predicts locally, server confirms.</summary>
        public void OwnerTrip()
        {
            if (!IsOwner || State != PlayerState.Normal || _physicsMode) return;
            ApplyPhysicsMode(true);
            _rb.linearVelocity = _cc.velocity + transform.forward * 1.5f + Vector3.up * 1f;
            TripRpc();
        }

        /// <summary>Holler / shout emote (Noise 0.8, 2 s cooldown). Callable on the owner.</summary>
        public void Holler()
        {
            if (!IsSpawned || !IsOwner || Time.time < _ownerHollerReadyAt) return;
            _ownerHollerReadyAt = Time.time + tuning.HollerCooldown;
            HollerRpc();
        }

        // ---- RPCs -----------------------------------------------------------------------------------------

        [Rpc(SendTo.Server)]
        void TripRpc()
        {
            if (State == PlayerState.Normal) ServerEnterRagdoll(Vector3.zero, PlayerIds.None, false);
        }

        [Rpc(SendTo.Server)]
        void HollerRpc()
        {
            if (Time.time < _hollerReadyAt || State == PlayerState.Respawning) return;
            _hollerReadyAt = Time.time + tuning.HollerCooldown - 0.1f;
            NoiseService.TryEmit(NoiseSource.Shout, tuning.HollerLoudness, headPivot.position, NetworkObjectId, OwnerClientId);
        }

        [Rpc(SendTo.Server)]
        void GetUpRpc()
        {
            if (State == PlayerState.Ragdoll) ServerSetState(PlayerState.Normal);
        }

        /// <summary>Owner: launch the body (ragdoll start or thrown while KO).</summary>
        [Rpc(SendTo.Owner)]
        void LaunchOwnerRpc(Vector3 velocity, bool replace)
        {
            ApplyPhysicsMode(true);
            if (replace) _rb.linearVelocity = velocity; else _rb.linearVelocity += velocity;
            _rest.Reset();
        }

        // ---- Drag (IDraggable, polish-2) ----------------------------------------------------------------------
        Vector3 _dragTarget;
        float _dragUntil = -1f;
        /// <summary>True while the owner is simulating a drag (tests).</summary>
        public bool IsBeingDragged => _dragUntil > Time.time;

        /// <inheritdoc />
        public void RequestDrag(Vector3 toward, float seconds)
        {
            if (!IsServer || !IsDown || seconds <= 0f) return;
            DragOwnerRpc(toward, seconds);
        }

        [Rpc(SendTo.Owner)]
        void DragOwnerRpc(Vector3 toward, float seconds)
        {
            _dragTarget = toward;
            _dragUntil = Time.time + seconds;
        }

        void OwnerTickDrag()
        {
            if (_dragUntil <= Time.time) return;
            if (!_physicsMode || _rb.isKinematic || _carriedBy.Value != PlayerIds.None) { _dragUntil = -1f; return; }
            Vector3 to = _dragTarget - _rb.position; to.y = 0f;
            if (to.sqrMagnitude < 1f) { _dragUntil = -1f; return; }
            Vector3 v = _rb.linearVelocity; Vector3 flat = new Vector3(v.x, 0f, v.z);
            Vector3 want = to.normalized * tuning.DragSpeed;
            Vector3 dv = Vector3.ClampMagnitude(want - flat, tuning.DragAccel * Time.fixedDeltaTime);
            _rb.AddForce(dv, ForceMode.VelocityChange);
            _rest.Reset(); // being dragged is not "at rest"
        }

        // ---- Owner-predicted footsteps (polish-2): the server does not relay our own footsteps back to us --------
        Vector3 _ownerFootPos;
        float _ownerFootDistance;

        void OwnerTickFootsteps()
        {
            Vector3 p = transform.position;
            Vector3 d = p - _ownerFootPos; d.y = 0f;
            _ownerFootPos = p;
            if (IsServer) return; // host hears its own server-emitted footsteps already
            if (State != PlayerState.Normal || MoveMode == 2 || d.sqrMagnitude > 4f) { _ownerFootDistance = 0f; return; }
            _ownerFootDistance += d.magnitude;
            bool sprint = MoveMode == 1;
            if (_ownerFootDistance < (sprint ? tuning.StrideSprint : tuning.StrideWalk)) return;
            _ownerFootDistance = 0f;
            float loud = sprint ? tuning.SprintFootstepLoudness : tuning.WalkFootstepLoudness;
            if (Services.TryGet(out IAudioService audio))
                audio.PlayAt(OwnFootstepEvent, p, Mathf.Clamp(loud / 0.5f, 0.125f, 2f)); // same gain curve as Audio's NoiseRouting
            OwnFootstepsPlayed++;
        }

        /// <summary>Audio event the owner plays for its own footsteps (Audio's FootstepDirt).</summary>
        public const string OwnFootstepEvent = "SFX_Player_Footstep_Dirt";
        /// <summary>Locally predicted footsteps (tests).</summary>
        public int OwnFootstepsPlayed { get; private set; }

        [Rpc(SendTo.Owner)]
        void ImpactFeedbackOwnerRpc(float designImpulse)
        {
            GetComponent<PlayerView>()?.AddTrauma(designImpulse / Mathf.Max(1f, tuning.ShakeFullImpulse));
        }

        [Rpc(SendTo.Owner)]
        void TeleportOwnerRpc(Vector3 position, Quaternion rotation)
        {
            OwnerTeleport(position, rotation);
        }

        /// <summary>Owner-side teleport through the authoritative NetworkTransform.</summary>
        public void OwnerTeleport(Vector3 position, Quaternion rotation)
        {
            bool wasPhysics = _physicsMode;
            _rb.isKinematic = true;
            _cc.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            _rb.position = position;
            Physics.SyncTransforms();
            if (IsSpawned && _nt != null && _nt.CanCommitToTransform) _nt.Teleport(position, rotation, transform.localScale);
            _motor.ResetMotion();
            _motor.SetLook(rotation.eulerAngles.y, 0f);
            if (!wasPhysics || State == PlayerState.Normal) { _physicsMode = true; ApplyPhysicsMode(false); }
            _lastPos = position;
        }

        // ---- Server API: IKnockable -------------------------------------------------------------------------

        /// <summary>
        /// Server: applies an impulse in design units (<see cref="ImpactUnits"/>).
        /// Above the ragdoll threshold the player ragdolls; at 2x RagdollImpulse, or the 3rd ragdoll in 20 s, KO.
        /// </summary>
        public void ApplyImpulse(Vector3 designImpulse, ulong instigator, BlameVerb verb)
        {
            if (!IsServer || State == PlayerState.Respawning) return;
            float mag = designImpulse.magnitude;
            if (mag <= 0f) return;
            ImpactFeedbackOwnerRpc(mag);
            float threshold = CarryMath.RagdollThreshold(tuning.RagdollImpulse, Carry != null ? Carry.LoadRatio : 0f, _stamina.Value,
                1f, tuning.LoadThresholdFactor, tuning.LowStaminaThreshold, tuning.LowStaminaMult);
            if (mag < threshold && !CarryMath.IsKnockoutImpulse(mag, tuning.RagdollImpulse)) return;

            float launch = Mathf.Min(tuning.RagdollLaunchMax, mag * tuning.RagdollLaunchPerImpulse);
            Vector3 dir = (designImpulse.normalized + Vector3.up * 0.35f).normalized;
            ServerEnterRagdoll(dir * launch, instigator, CarryMath.IsKnockoutImpulse(mag, tuning.RagdollImpulse));
            NoiseService.TryEmit(NoiseSource.Impact, mag > carryTuning.HeavyImpact ? carryTuning.HeavyImpactLoudness : carryTuning.LightImpactLoudness, transform.position, NetworkObjectId, instigator);
        }

        /// <summary>Server: sends the player to the barn (RespawnDelay s). Verb Drowned / KnockedOut / Stranded.</summary>
        public void SendToBarn(BlameVerb cause, bool chargeFee)
        {
            if (!IsServer || State == PlayerState.Respawning) return;
            Carry?.ServerRelease(HandlingMode.Dropped);
            ReleaseFromCarrier();

            if (Services.TryGet(out IEconomyService economy))
            {
                if (chargeFee) economy.ChargeRespawnFee(OwnerClientId, cause);
                if (cause == BlameVerb.Drowned && PlayerIds.IsPlayer(_lastThrower) && Time.time - _lastThrownAt <= tuning.YeetBlameWindow)
                {
                    economy.EmitBlame(new BlameEvent
                    {
                        Instigators = new[] { _lastThrower }, BlameShares = new[] { 1f }, PrimaryInstigator = _lastThrower,
                        Verb = BlameVerb.YeetedTeammate, ItemTag = "Player", Location = transform.position, CauseTag = "Hazard.River",
                        Timestamp = Time.time,
                    });
                }
            }
            _respawnCause = cause;
            ServerSetState(PlayerState.Respawning);
            _stateUntil = Time.time + tuning.RespawnDelay;
        }

        /// <summary>Server: teleports the player (owner applies it through the owner-authoritative transform).</summary>
        public void ServerTeleport(Vector3 position, Quaternion rotation)
        {
            if (IsServer) TeleportOwnerRpc(position, rotation);
        }

        /// <summary>Server: revive a knocked-out player at once (teammate held Interact for ReviveHold).</summary>
        public bool ServerRevive(ulong reviver)
        {
            if (!IsServer || State != PlayerState.KnockedOut || reviver == OwnerClientId) return false;
            ServerSetState(PlayerState.Normal);
            if (Services.TryGet(out IEconomyService economy))
                economy.EmitBlame(new BlameEvent
                {
                    Instigators = new[] { reviver }, BlameShares = new[] { 1f }, PrimaryInstigator = reviver,
                    Verb = BlameVerb.Rescue, ItemTag = "Player", Location = transform.position, Timestamp = Time.time,
                });
            return true;
        }

        void ServerEnterRagdoll(Vector3 launchVelocity, ulong instigator, bool knockout)
        {
            bool thirdInWindow = _koTracker.RegisterRagdoll(Time.time);
            var target = knockout || thirdInWindow ? PlayerState.KnockedOut : PlayerState.Ragdoll;
            if (State == target || !PlayerStateMachine.CanTransition(State, target)) { if (launchVelocity != Vector3.zero) LaunchOwnerRpc(launchVelocity, false); return; }
            // Ragdoll while holding: hands release; the item keeps the hand's velocity (edge case).
            Carry?.ServerRelease(HandlingMode.Dropped);
            ServerSetState(target);
            if (target == PlayerState.KnockedOut) _stateUntil = Time.time + tuning.KoDuration;
            LaunchOwnerRpc(launchVelocity, false);
        }

        void ServerSetState(PlayerState next)
        {
            if (!PlayerStateMachine.CanTransition(State, next)) return;
            if (State == PlayerState.KnockedOut && next != PlayerState.KnockedOut) ReleaseFromCarrier();
            _state.Value = next;
        }

        void ReleaseFromCarrier()
        {
            if (_carriedBy.Value == PlayerIds.None) return;
            var carrier = Find(_carriedBy.Value);
            if (carrier != null && carrier.Carry != null) carrier.Carry.ServerRelease(HandlingMode.Dropped);
            _carriedBy.Value = PlayerIds.None;
        }

        // ---- State reaction (all machines) ---------------------------------------------------------------------

        void HandleStateChanged(PlayerState prev, PlayerState next)
        {
            bool physics = PlayerStateMachine.IsPhysicsDriven(next) || next == PlayerState.Respawning;
            if (!physics && IsOwner && PlayerStateMachine.IsPhysicsDriven(prev))
            {
                // Get-up: 1.2 s during which control is locked while the body rights itself.
                _controlLockUntil = Time.time + tuning.GetUpDuration;
            }
            ApplyPhysicsMode(physics);
            if (next == PlayerState.KnockedOut) _stateUntil = Time.time + tuning.KoDuration;
            _rest.Reset();
            GetComponent<PlayerView>()?.ApplyViewMode();
            OnStateChanged?.Invoke(prev, next);
        }

        /// <summary>Switches between controller-driven and physics-driven body on this machine.</summary>
        void ApplyPhysicsMode(bool physics)
        {
            gameObject.layer = physics ? _ragdollLayer : _playerLayer;
            if (physics == _physicsMode) return;
            _physicsMode = physics;
            bool simulate = physics && IsOwnerSafe;
            if (ragdollCollider != null) ragdollCollider.enabled = physics;
            if (physics)
            {
                Vector3 v = _cc.enabled ? _cc.velocity : Vector3.zero;
                _cc.enabled = false;
                _rb.isKinematic = !simulate;
                if (simulate)
                {
                    _rb.interpolation = RigidbodyInterpolation.Interpolate;
                    _rb.constraints = RigidbodyConstraints.None;
                    _rb.linearVelocity = v;
                }
            }
            else
            {
                if (!_rb.isKinematic) { _rb.linearVelocity = Vector3.zero; _rb.angularVelocity = Vector3.zero; }
                _rb.isKinematic = true;
                _rb.interpolation = RigidbodyInterpolation.None;
                if (IsOwnerSafe)
                {
                    // Stand up where the body lies, keep the heading.
                    float yaw = Quaternion.LookRotation(Vector3.ProjectOnPlane(transform.forward, Vector3.up).sqrMagnitude > 1e-3f
                        ? Vector3.ProjectOnPlane(transform.forward, Vector3.up) : Vector3.forward).eulerAngles.y;
                    Vector3 pos = transform.position;
                    if (Physics.Raycast(pos + Vector3.up * 1.5f, Vector3.down, out var hit, 4f, ~(1 << _ragdollLayer | 1 << _playerLayer), QueryTriggerInteraction.Ignore))
                        pos = hit.point + Vector3.up * 0.05f;
                    transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
                    _rb.position = pos;
                    Physics.SyncTransforms();
                    _motor.ResetMotion();
                    _motor.SetLook(yaw, 0f);
                }
                _cc.enabled = true;
            }
        }

        // ---- Per-frame --------------------------------------------------------------------------------------

        void Update()
        {
            if (!IsSpawned) return;
            if (!IsOwner) headPivot.localRotation = Quaternion.Slerp(headPivot.localRotation, Quaternion.Euler(_pitch.Value, 0f, 0f), 1f - Mathf.Exp(-20f * Time.deltaTime));

            if (IsOwner)
            {
                if (_spawnPoseFrames > 0 && --_spawnPoseFrames == 0) OwnerTeleport(transform.position, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
                if (_physicsMode && !_rb.isKinematic) _rest.Tick(_rb.linearVelocity.magnitude, Time.deltaTime);
                if (CanGetUp && _input.JumpPressed) GetUpRpc();
                if (HasControl && _input.HollerPressed) Holler();
                OwnerTickFootsteps();
            }

            if (IsServer) ServerTick();
        }

        void ServerTick()
        {
            float now = Time.time;
            switch (State)
            {
                case PlayerState.KnockedOut:
                    if (now >= _stateUntil) ServerSetState(PlayerState.Normal);
                    break;
                case PlayerState.Respawning:
                    if (now >= _stateUntil) ServerFinishRespawn();
                    break;
            }
            if (State != PlayerState.Respawning && transform.position.y < tuning.KillZ) SendToBarn(BlameVerb.Drowned, true);

            // Footsteps (proximity-voice table): walk 0.10, sprint 0.25, crouch none.
            Vector3 p = transform.position;
            Vector3 d = p - _lastPos; d.y = 0f;
            _lastPos = p;
            if (State != PlayerState.Normal || MoveMode == 2 || d.sqrMagnitude > 4f) { _footDistance = 0f; return; }
            _footDistance += d.magnitude;
            bool sprint = MoveMode == 1;
            if (_footDistance >= (sprint ? tuning.StrideSprint : tuning.StrideWalk))
            {
                _footDistance = 0f;
                NoiseService.TryEmit(NoiseSource.Footstep, sprint ? tuning.SprintFootstepLoudness : tuning.WalkFootstepLoudness, p, NetworkObjectId, OwnerClientId);
            }
        }

        void ServerFinishRespawn()
        {
            Vector3 pos = new Vector3(0f, 1f, 0f);
            Quaternion rot = Quaternion.identity;
            var barns = LevelMarker.FindAll(MarkerRole.BarnRespawn);
            if (barns.Count > 0)
            {
                var m = barns[(int)(OwnerClientId % (ulong)barns.Count)];
                pos = m.transform.position;
                rot = Quaternion.Euler(0f, m.transform.eulerAngles.y, 0f);
            }
            _koTracker.Reset();
            ServerSetState(PlayerState.Normal);
            TeleportOwnerRpc(pos, rot);
        }

        void FixedUpdate()
        {
            Vector3 p = transform.position;
            Velocity = Vector3.Lerp(Velocity, (p - _prevFixedPos) / Time.fixedDeltaTime, 0.5f);
            _prevFixedPos = p;

            if (!IsSpawned || !IsOwner) return;

            // A carried KO body pulls itself toward the carrier's hold point (owner-simulated).
            if (_physicsMode && !_rb.isKinematic && _carriedBy.Value != PlayerIds.None)
            {
                var carrier = Find(_carriedBy.Value);
                if (carrier != null && carrier.Carry != null)
                {
                    Vector3 target = carrier.Carry.HoldPoint(false);
                    Vector3 com = _rb.worldCenterOfMass;
                    Vector3 f = CarryMath.HandForce(target - com, _rb.linearVelocity - carrier.Velocity,
                        carryTuning.HandStiffness * 2f, carryTuning.HandDamping * 2f, carryTuning.HandMaxForce * 2f);
                    _rb.AddForce(f + _rb.mass * -Physics.gravity * 0.6f);
                }
            }
        }

        Vector3 _prevFixedPos;

        void OnTriggerEnter(Collider other)
        {
            if (!IsSpawned || !IsServer || State == PlayerState.Respawning) return;
            if (other.gameObject.layer == LayerMask.NameToLayer(GameLayers.Water) || other.CompareTag(MarkerTag.River))
                SendToBarn(BlameVerb.Drowned, true);
        }

        void OnCollisionEnter(Collision c)
        {
            // Server: a dynamic body (thrown goods, charging critter with a rigidbody) hit this player.
            if (!IsSpawned || !IsServer || c.rigidbody == null || c.rigidbody.isKinematic) return;
            if (c.rigidbody.GetComponent<CarryBody>() != null) return; // reported by the carry system with attribution
            float design = ImpactUnits.ToDesign(c.impulse.magnitude);
            ApplyImpulse(-c.impulse.normalized * design, PlayerIds.Nature, BlameVerb.Crashed);
        }
    }
}
