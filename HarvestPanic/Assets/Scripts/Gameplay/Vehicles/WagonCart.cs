using System.Collections.Generic;
using HarvestPanic.Core;
using Unity.Netcode;
using UnityEngine;

namespace HarvestPanic.Gameplay
{
    /// <summary>
    /// The farm wagon (design/gdd/wagon-vehicles.md rules 1-6, 10-11; GDD 5.10). A server-simulated physics hand-cart:
    /// <list type="bullet">
    /// <item>Players grab a handle with E (2 front pull grips, 2 rear push grips; up to 4 players). The cart pushes itself
    /// (<see cref="ISelfSimulatedCarryable"/>): each grip is a tow-bar spring capped at 900 N per player; rolling
    /// resistance <c>total_mass * rolling_k * v</c> makes <see cref="WagonMath.VMax"/> the top speed (dirt/mud/plank);
    /// the rear axle has full sideways grip and the front axle steers whenever someone holds the front handle.</item>
    /// <item>Cargo: free goods/props (and dropped critters) that land in the bed take slots (one per 2 carry hands, soft
    /// cap 400 kg) and ride kinematically. Grabbing one out (or lassoing it) unloads it. Hard bumps above the GDD threshold
    /// may bounce one out (SpilledCargo on the driver); a hard hit bruises the cargo through IGoods (so Padded Crates'
    /// Goods.BruiseMult applies to wagon cargo).</item>
    /// <item>Tipping past 50 degrees spills everything (TippedWagon on the driver, else the last pusher); 2 players
    /// (or everyone, when fewer are connected) on the handles for 2 s right it.</item>
    /// <item>Bridge planks count the cart through <see cref="ICarryable.MassKg"/> (frame only) and every cargo body
    /// through its own ICarryable, so the load is the full wagon with nothing counted twice.</item>
    /// </list>
    /// Deviations (docs/qa/tools-progress.md): slot cargo instead of loose bodies; no tarp, brake lever or wheel damage yet.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class WagonCart : NetworkBehaviour, ICarryable, ISelfSimulatedCarryable, ICarryLoadOverride, ICustomGrabPrompt, IToolArmPose
    {
        sealed class Cargo
        {
            public NetworkObject No;
            public ICarryable Carry;
            public IGoods Goods;
            public Rigidbody Rb;
            public bool WasKinematic, Critter;
            public int First, Cost;
            public float Mass;
            public Vector3 LocalPos;
            public Quaternion LocalRot;
            public readonly List<Collider> Colliders = new List<Collider>();
        }

        [SerializeField] WagonTuning tuning;
        [SerializeField, Tooltip("Bed floor centre (top surface); +Z = front")] Transform bed;
        [SerializeField, Tooltip("0-1 front pull grips, 2-3 rear push grips")] Transform[] handles = new Transform[0];
        [SerializeField, Tooltip("Wheel visuals: 0-1 front, 2-3 rear")] Transform[] wheels = new Transform[0];
        [SerializeField] float wheelRadius = 0.32f;

        /// <summary>All spawned wagons (server queries, tests).</summary>
        public static readonly List<WagonCart> All = new List<WagonCart>();
        static readonly Collider[] Scan = new Collider[32];
        static readonly List<CarryBody.GripInfo> Grips = new List<CarryBody.GripInfo>();

        readonly NetworkVariable<bool> _tipped = new NetworkVariable<bool>(false);
        readonly NetworkVariable<byte> _cargoCount = new NetworkVariable<byte>(0);
        readonly NetworkVariable<float> _cargoKg = new NetworkVariable<float>(0f);
        readonly NetworkVariable<float> _righting = new NetworkVariable<float>(0f);
        readonly NetworkVariable<ulong> _driver = new NetworkVariable<ulong>(PlayerIds.None);

        readonly List<Cargo> _cargo = new List<Cargo>();
        readonly Dictionary<PlayerCarry, float> _rest = new Dictionary<PlayerCarry, float>();
        readonly List<Collider> _own = new List<Collider>();
        WagonSlots _slots;
        Rigidbody _rb;
        CarryBody _body;
        float _nextScan, _tiltSince = -1f, _rightingFor, _lastVy, _nextBump, _wheelSpin;
        ulong _lastPusher = PlayerIds.None;
        bool _inWater;

        /// <summary>True while tipped over (every peer).</summary>
        public bool IsTipped => _tipped.Value;
        /// <summary>Items in the bed (every peer).</summary>
        public int CargoCount => _cargoCount.Value;
        /// <summary>Cargo mass (every peer).</summary>
        public float CargoKg => _cargoKg.Value;
        /// <summary>Player blamed for spills: the front-handle holder, else the last pusher (every peer).</summary>
        public ulong Driver => _driver.Value;
        /// <summary>Tuning in use.</summary>
        public WagonTuning Tuning => tuning;
        /// <summary>Bed transform (floor centre, top surface).</summary>
        public Transform Bed => bed != null ? bed : transform;
        /// <summary>Handle grips (0-1 front, 2-3 rear).</summary>
        public IReadOnlyList<Transform> Handles => handles;
        /// <summary>Server: the cargo objects (tests).</summary>
        public void GetCargo(List<NetworkObject> into) { into.Clear(); foreach (var c in _cargo) if (c.No != null) into.Add(c.No); }
        /// <summary>Server: true when <paramref name="no"/> rides in this bed.</summary>
        public bool HasCargo(NetworkObject no) { foreach (var c in _cargo) if (c.No == no) return true; return false; }

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            if (tuning == null) tuning = ScriptableObject.CreateInstance<WagonTuning>();
            _slots = new WagonSlots(tuning.CargoSlots);
            _rb.mass = tuning.WagonMassKg;
            GetComponentsInChildren(true, _own);
            _own.RemoveAll(c => c == null || c.isTrigger);
        }

        /// <summary>Editor builder: wires the rig.</summary>
        public void Configure(WagonTuning t, Transform bedPoint, Transform[] grips, Transform[] wheelVisuals, float radius)
        {
            tuning = t; bed = bedPoint; handles = grips; wheels = wheelVisuals; wheelRadius = radius;
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public override void OnNetworkSpawn()
        {
            if (IsServer) CarryHooks.Claiming += OnClaiming;
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer) return;
            CarryHooks.Claiming -= OnClaiming;
            for (int i = _cargo.Count - 1; i >= 0; i--) Unload(_cargo[i], Vector3.zero);
        }

        // ================================================================================================ ICarryable

        /// <inheritdoc />
        public Transform CarryTransform => transform;
        /// <inheritdoc />
        public int HandsRequired => 8; // four players at the handles
        /// <inheritdoc />
        /// <remarks>Frame only: cargo bodies are counted by the bridge planks themselves (no double count).</remarks>
        public float MassKg => tuning != null ? tuning.WagonMassKg : 120f;
        /// <inheritdoc />
        public bool IsKinematicCarry => false;

        /// <inheritdoc />
        public void OnGrabbed(ulong clientId) { if (IsServer && PlayerIds.IsPlayer(clientId)) _lastPusher = clientId; }

        /// <inheritdoc />
        public void OnReleased(ulong clientId)
        {
            if (!IsServer) return;
            var pc = PlayerCharacter.Find(clientId);
            if (pc != null && pc.Carry != null) _rest.Remove(pc.Carry);
        }

        /// <inheritdoc />
        public void ApplyThrow(Vector3 velocity, ulong thrower) { } // nobody throws a wagon

        // ================================================================================================ prompts / load

        /// <inheritdoc />
        public string GrabPrompt
        {
            get
            {
                if (_tipped.Value) return "E: Right the wagon";
                var me = PlayerCharacter.Local;
                bool front = me != null && transform.InverseTransformPoint(me.transform.position).z > 0f;
                return front ? "E: Pull wagon" : "E: Push wagon";
            }
        }

        /// <inheritdoc />
        public string HoldPrompt => _tipped.Value
            ? $"Righting {Mathf.RoundToInt(_righting.Value * 100f)}%  E: Let go"
            : $"E: Let go  Cargo {_cargoCount.Value} ({Mathf.RoundToInt(_cargoKg.Value)} kg)";

        /// <inheritdoc />
        public ArmPose ArmPose => ArmPose.WagonPush;

        /// <inheritdoc />
        public float CarrierSpeedMultiplier(int holders)
        {
            if (_tipped.Value) return 0.6f;
            float vmax = WagonMath.VMax(holders * tuning.PushForcePerPlayer, _rb.mass, CurrentK(), tuning.VMax);
            float walk = PlayerCharacter.All.Count > 0 && PlayerCharacter.All[0].Tuning != null ? PlayerCharacter.All[0].Tuning.WalkSpeed : 4f;
            return WagonMath.CarrierSpeedMult(vmax, walk);
        }

        float CurrentK()
        {
            Vector3 p = transform.position;
            bool bridge = Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, 2f, LayerMask.GetMask(GameLayers.Bridge), QueryTriggerInteraction.Ignore);
            var g = WagonMath.GroundFor(bridge, MovementModifiers.Evaluate(p));
            return WagonMath.RollingK(g, tuning.RollingKDirt, tuning.RollingKMud, tuning.RollingKPlank);
        }

        // ================================================================================================ server sim

        void FixedUpdate()
        {
            if (!IsSpawned || !IsServer) return;
            float dt = Time.fixedDeltaTime, now = Time.time;
            if (_body == null) _body = GetComponent<CarryBody>();
            if (_body != null) _body.GetGrips(Grips); else Grips.Clear();

            PruneCargo();
            CheckWater();

            // Tipping (rule 6).
            if (!_tipped.Value)
            {
                if (WagonMath.IsTipped(transform.up, tuning.TipAngle))
                {
                    if (_tiltSince < 0f) _tiltSince = now;
                    else if (now - _tiltSince >= tuning.TipConfirmSeconds) Tip();
                }
                else _tiltSince = -1f;
            }

            if (_tipped.Value) TickRighting(dt);
            else if (!_inWater) Drive(dt);

            FollowCargo();
            Bumps(dt, now);
            if (tuning.AutoLoad && !_tipped.Value && !_inWater && now >= _nextScan) { _nextScan = now + tuning.LoadScanInterval; ScanBed(); }
            SyncState();
        }

        void Drive(float dt)
        {
            int front = -1;
            // Handle springs.
            foreach (var g in Grips)
            {
                var carrier = g.Carrier;
                if (carrier == null || carrier.Character == null) continue;
                int h = NearestHandle(g.LocalPoint);
                if (h < 0) continue;
                if (h <= 1) front = h;
                Vector3 hp = handles[h].position;
                var pc = carrier.Character;
                Vector3 chest = pc.transform.position + Vector3.up * 1.0f;
                if (!_rest.TryGetValue(carrier, out float rest))
                {
                    rest = Mathf.Clamp(LassoMath.FlatDistance(hp, chest), tuning.HandleRest.x, tuning.HandleRest.y);
                    _rest[carrier] = rest;
                }
                float flat = LassoMath.FlatDistance(hp, chest);
                if (flat > tuning.HandleSlipDistance) { carrier.ServerOnGripLost(); _body.RemoveGrip(carrier, HandlingMode.Slipped); continue; }
                Vector3 rel = _rb.GetPointVelocity(hp) - pc.Velocity;
                Vector3 f = WagonMath.HandleForce(hp, chest, rel, rest, tuning.HandleStiffness, tuning.HandleDamping, tuning.PushForcePerPlayer);
                _rb.AddForceAtPosition(f, new Vector3(hp.x, _rb.worldCenterOfMass.y, hp.z));
                _lastPusher = carrier.OwnerClientId;
            }
            ulong driver = front >= 0 ? FrontHolder() : _lastPusher;
            if (_driver.Value != driver) _driver.Value = driver;

            if (!Grounded()) return;
            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (fwd.sqrMagnitude < 1e-4f) return;
            fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            float m = _rb.mass;

            // Sideways grip per axle: the rear axle is fixed; the front axle steers while someone holds the front handle.
            Vector3 rearAxle = transform.TransformPoint(new Vector3(0f, wheelRadius, -tuning.BedLength * 0.35f));
            Vector3 frontAxle = transform.TransformPoint(new Vector3(0f, wheelRadius, tuning.BedLength * 0.35f));
            float rearLat = Vector3.Dot(_rb.GetPointVelocity(rearAxle), right);
            _rb.AddForceAtPosition(-right * rearLat * m * 0.5f * tuning.LateralGrip, rearAxle);
            if (front < 0)
            {
                float frontLat = Vector3.Dot(_rb.GetPointVelocity(frontAxle), right);
                _rb.AddForceAtPosition(-right * frontLat * m * 0.5f * tuning.LateralGrip, frontAxle);
            }

            // Rolling resistance: v_max = F / (m k) under a constant push (section 4).
            Vector3 v = _rb.linearVelocity;
            float vf = Vector3.Dot(v, fwd);
            float k = CurrentK();
            Vector3 drag = -fwd * WagonMath.RollingDrag(m, k, vf);
            if (Grips.Count == 0)
            {
                drag += -fwd * vf * m * tuning.IdleRollDrag;
                Vector3 flatV = new Vector3(v.x, 0f, v.z);
                if (flatV.magnitude < tuning.ParkSpeed)
                {
                    // Parking brake: hold still on the bridge's sag instead of creeping downhill.
                    _rb.linearVelocity = new Vector3(0f, Mathf.Min(0f, v.y), 0f);
                    _rb.angularVelocity = new Vector3(_rb.angularVelocity.x, 0f, _rb.angularVelocity.z);
                    return;
                }
            }
            _rb.AddForce(drag);
            if (Mathf.Abs(vf) > tuning.VMax) _rb.AddForce(-fwd * (Mathf.Abs(vf) - tuning.VMax) * Mathf.Sign(vf) * m * 10f);
        }

        ulong FrontHolder()
        {
            foreach (var g in Grips) if (g.Carrier != null && NearestHandle(g.LocalPoint) <= 1) return g.Carrier.OwnerClientId;
            return _lastPusher;
        }

        int NearestHandle(Vector3 localPoint)
        {
            int best = -1; float bd = float.MaxValue;
            for (int i = 0; i < handles.Length; i++)
            {
                if (handles[i] == null) continue;
                float d = (transform.InverseTransformPoint(handles[i].position) - localPoint).sqrMagnitude;
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        bool Grounded()
        {
            int mask = ~LayerMask.GetMask(GameLayers.Player, GameLayers.Carryable, GameLayers.Critter, GameLayers.Ragdoll, GameLayers.Interactable, GameLayers.Water, "Ignore Raycast");
            int n = 0;
            float x = tuning.BedWidth * 0.6f, z = tuning.BedLength * 0.35f;
            foreach (var c in new[] { new Vector3(-x, 0f, z), new Vector3(x, 0f, z), new Vector3(-x, 0f, -z), new Vector3(x, 0f, -z) })
                if (Physics.Raycast(transform.TransformPoint(c + Vector3.up * wheelRadius), -transform.up, wheelRadius + 0.15f, mask, QueryTriggerInteraction.Ignore)) n++;
            return n >= 2;
        }

        void TickRighting(float dt)
        {
            int holders = Grips.Count;
            int connected = NetworkManager != null ? Mathf.Max(1, NetworkManager.ConnectedClientsIds.Count) : 1;
            int need = WagonMath.RightingPlayers(connected, tuning.RightingPlayers);
            _rightingFor = holders >= need ? _rightingFor + dt : 0f;
            float p = Mathf.Clamp01(_rightingFor / Mathf.Max(0.01f, tuning.RightingSeconds));
            if (Mathf.Abs(_righting.Value - p) > 0.02f || (p == 0f && _righting.Value != 0f)) _righting.Value = p;
            if (_rightingFor >= tuning.RightingSeconds) ServerRight();
        }

        /// <summary>Server: sets the wagon back on its wheels (righting, morning reset).</summary>
        public void ServerRight()
        {
            if (!IsServer) return;
            float yaw = transform.eulerAngles.y;
            Vector3 p = transform.position + Vector3.up * 0.45f;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.position = p;
            _rb.rotation = Quaternion.Euler(0f, yaw, 0f);
            transform.SetPositionAndRotation(p, Quaternion.Euler(0f, yaw, 0f));
            _tipped.Value = false;
            _righting.Value = 0f;
            _rightingFor = 0f;
            _tiltSince = -1f;
        }

        /// <summary>Server: teleports the wagon (morning reset, tests) and rights it.</summary>
        public void ServerPlace(Vector3 position, Quaternion rotation)
        {
            if (!IsServer) return;
            for (int i = _cargo.Count - 1; i >= 0; i--) Unload(_cargo[i], Vector3.zero);
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.position = position; _rb.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            if (TryGetComponent<Unity.Netcode.Components.NetworkTransform>(out var nt) && nt.CanCommitToTransform) nt.Teleport(position, rotation, transform.localScale);
            _tipped.Value = false;
            _inWater = false;
            _tiltSince = -1f;
        }

        /// <summary>Server: tips the wagon now: everything spills, TippedWagon on the driver (rule 6, 11).</summary>
        public void Tip()
        {
            if (!IsServer || _tipped.Value) return;
            _tipped.Value = true;
            _rightingFor = 0f;
            ulong who = _driver.Value != PlayerIds.None ? _driver.Value : _lastPusher;
            Vector3 down = Vector3.ProjectOnPlane(-transform.up, Vector3.up); // toward the side it fell on
            down = down.sqrMagnitude > 1e-3f ? down.normalized : transform.right;
            int spilled = _cargo.Count;
            for (int i = _cargo.Count - 1; i >= 0; i--)
            {
                var c = _cargo[i];
                Vector3 jitter = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)) * 0.8f;
                Unload(c, (down + jitter).normalized * tuning.SpillSpeed + Vector3.up * 2f);
                // The money: each spilled item's bruise is booked as TippedWagon on the driver (blame-ledger "tipped the wagon").
                if (c.Goods != null) { c.Goods.RecordTouch(who); c.Goods.ApplyBruise(tuning.TipSpillImpulse, who, BlameVerb.TippedWagon); }
            }
            ToolBlame.Emit(BlameVerb.TippedWagon, who, string.Empty, "Vehicle.Wagon", transform.position, spilled > 0 ? "Cargo." + spilled : "Vehicle.Wagon");
            NoiseService.TryEmit(NoiseSource.Vehicle, 0.7f, transform.position, NetworkObjectId, who);
        }

        void CheckWater()
        {
            bool wet = Physics.CheckSphere(transform.position + Vector3.up * 0.4f, 0.4f, LayerMask.GetMask(GameLayers.Water), QueryTriggerInteraction.Collide);
            if (wet && !_inWater)
            {
                // Into the river: the cargo floats off (the river soaks/sweeps it); the cart is swept to the pile.
                for (int i = _cargo.Count - 1; i >= 0; i--) Unload(_cargo[i], Vector3.up * 1.5f);
            }
            _inWater = wet;
        }

        void Bumps(float dt, float now)
        {
            float vy = _rb.linearVelocity.y;
            float accel = (vy - _lastVy) / Mathf.Max(1e-4f, dt);
            _lastVy = vy;
            if (_cargo.Count == 0 || _tipped.Value || tuning.BumpSpillChance <= 0f || now < _nextBump) return;
            Vector3 flat = _rb.linearVelocity; flat.y = 0f;
            if (accel < WagonMath.BounceThreshold(tuning.WallHeight) || flat.magnitude < tuning.BumpMinSpeed) return;
            _nextBump = now + tuning.BumpCooldown;
            if (Random.value >= tuning.BumpSpillChance) return;
            var c = _cargo[Random.Range(0, _cargo.Count)];
            ulong who = _driver.Value != PlayerIds.None ? _driver.Value : _lastPusher;
            string id = c.Goods != null ? c.Goods.GoodsId : string.Empty;
            Vector3 at = c.No != null ? c.No.transform.position : transform.position;
            Vector3 side = Random.value < 0.5f ? transform.right : -transform.right;
            Unload(c, side * tuning.SpillSpeed + Vector3.up * 3f + flat);
            if (c.Goods != null) { c.Goods.RecordTouch(who); c.Goods.ApplyBruise(tuning.BumpSpillImpulse, who, BlameVerb.SpilledCargo); }
            ToolBlame.Emit(BlameVerb.SpilledCargo, who, id, string.IsNullOrEmpty(id) ? "Prop" : id, at, "Vehicle.Wagon");
        }

        void OnCollisionEnter(Collision c)
        {
            if (!IsSpawned || !IsServer) return;
            float design = ImpactUnits.ToDesign(c.impulse.magnitude);
            ulong who = _driver.Value != PlayerIds.None ? _driver.Value : _lastPusher;
            Vector3 flat = _rb.linearVelocity; flat.y = 0f;
            // Runaway wagon vs a farmer who is not on the handles.
            if (Knockables.TryGetFor(c.collider.gameObject, out var knock) && flat.magnitude > tuning.KnockSpeed)
            {
                bool holder = false;
                foreach (var g in Grips) if (g.Carrier != null && c.collider.transform.IsChildOf(g.Carrier.transform)) holder = true;
                if (!holder) knock.ApplyImpulse(flat.normalized * Mathf.Max(design, flat.magnitude * _rb.mass * 0.5f), who, BlameVerb.Crashed);
            }
            if (design < tuning.CargoHitMin) return;
            float bruise = WagonMath.CargoBruise(design, tuning.CargoCushion);
            foreach (var cg in _cargo) if (cg.Goods != null) cg.Goods.ApplyBruise(bruise, who, BlameVerb.Bruised);
            NoiseService.TryEmit(NoiseSource.Vehicle, 0.45f, c.GetContact(0).point, NetworkObjectId, who);
        }

        // ================================================================================================ cargo

        void OnClaiming(NetworkObject body, object claimant)
        {
            if (ReferenceEquals(claimant, this)) return;
            for (int i = _cargo.Count - 1; i >= 0; i--)
                if (_cargo[i].No == body) { Unload(_cargo[i], Vector3.zero); return; }
        }

        int LoadMask => LayerMask.GetMask(GameLayers.Carryable, GameLayers.Critter);

        void ScanBed()
        {
            var b = Bed;
            Vector3 half = new Vector3(tuning.BedWidth * 0.5f, 0.55f, tuning.BedLength * 0.5f);
            Vector3 centre = b.TransformPoint(new Vector3(0f, half.y, 0f));
            int n = Physics.OverlapBoxNonAlloc(centre, half, Scan, b.rotation, LoadMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var carry = Scan[i].GetComponentInParent<ICarryable>();
                if (carry == null || ReferenceEquals(carry, this)) continue;
                var no = (carry as Component) != null ? ((Component)carry).GetComponentInParent<NetworkObject>() : null;
                if (no == null || no == NetworkObject || HasCargo(no)) continue;
                ServerTryLoad(no);
            }
        }

        /// <summary>
        /// Server: puts <paramref name="no"/> in the bed if it may ride: a free (not held, not tethered, not a tool or a
        /// player) goods/prop item, or a critter that was dropped/thrown in; it must sit inside the bed, be nearly at rest
        /// relative to the wagon and fit the slots and the soft cap.
        /// </summary>
        public bool ServerTryLoad(NetworkObject no, bool requireInside = true)
        {
            if (!IsServer || no == null || !no.IsSpawned || _tipped.Value) return false;
            var carry = no.GetComponentInChildren<ICarryable>();
            if (carry == null || carry is PlayerCharacter || carry is ISelfSimulatedCarryable || carry is IUsableTool) return false;
            if (carry is CarryableProp prop && (prop.IsHolstered || prop.IsKinematicCarry)) return false; // tools ride on belts, not in the bed
            var cb = carry.CarryTransform.GetComponent<CarryBody>();
            if (cb != null && cb.GripCount > 0) return false;
            if (LassoTool.IsTetherTarget(no)) return false;
            foreach (var other in All) if (other != this && other.HasCargo(no)) return false;
            var rb = carry.CarryTransform.GetComponent<Rigidbody>();
            if (rb == null) return false;
            bool critter = carry.CarryTransform.gameObject.layer == LayerMask.NameToLayer(GameLayers.Critter);
            if (critter && rb.isKinematic) return false; // only critters that fell/were thrown in, not ones walking about
            var b = Bed;
            Vector3 local = b.InverseTransformPoint(carry.CarryTransform.position);
            if (requireInside && (Mathf.Abs(local.x) > tuning.BedWidth * 0.5f || Mathf.Abs(local.z) > tuning.BedLength * 0.5f || local.y < -0.3f || local.y > 1.4f)) return false;
            if (requireInside && !rb.isKinematic && (rb.linearVelocity - _rb.GetPointVelocity(rb.position)).magnitude > tuning.MaxLoadSpeed) return false;
            var goods = carry.CarryTransform.GetComponentInParent<IGoods>();
            int hands = goods != null && goods.Definition != null ? goods.Definition.CarryHands : carry.HandsRequired;
            float mass = carry.MassKg;
            int cost = WagonMath.SlotCost(hands);
            if (!WagonMath.CanLoad(_slots.Used, _slots.Count, _cargoKg.Value, hands, tuning.CargoSoftCapKg)) return false;
            if (!_slots.TryTake(cost, out int first)) return false;

            CarryHooks.RaiseClaiming(no, this);
            var c = new Cargo { No = no, Carry = carry, Goods = goods, Rb = rb, Critter = critter, First = first, Cost = cost, Mass = mass, WasKinematic = rb.isKinematic };
            carry.CarryTransform.GetComponentsInChildren(c.Colliders);
            c.Colliders.RemoveAll(x => x == null || x.isTrigger);
            foreach (var mine in _own) foreach (var theirs in c.Colliders) Physics.IgnoreCollision(mine, theirs, true);

            // Seat it: yaw snapped to the bed, resting on the floor at the slot centre.
            Quaternion rel = Quaternion.Inverse(b.rotation) * carry.CarryTransform.rotation;
            float yaw = critter ? 0f : Mathf.Round(rel.eulerAngles.y / 90f) * 90f;
            c.LocalRot = Quaternion.Euler(0f, yaw, 0f);
            Quaternion worldRot = b.rotation * c.LocalRot;
            if (critter) carry.OnGrabbed(PlayerIds.Nature); // AI hands the body over (Carried, kinematic); an escape shows up as a dynamic body (PruneCargo)
            rb.isKinematic = true;
            carry.CarryTransform.rotation = worldRot;
            Physics.SyncTransforms();
            float bottom = carry.CarryTransform.position.y - ColliderBounds(c.Colliders, carry.CarryTransform.position).min.y;
            Vector2 slot = WagonSlots.Centre(first, cost, _slots.Count, tuning.CargoColumns, tuning.BedWidth, tuning.BedLength);
            c.LocalPos = new Vector3(slot.x, Mathf.Max(0.02f, bottom) + 0.02f, slot.y);
            Vector3 world = b.TransformPoint(c.LocalPos);
            rb.position = world; rb.rotation = worldRot;
            carry.CarryTransform.SetPositionAndRotation(world, worldRot);
            goods?.RecordTouch(_lastPusher);
            _cargo.Add(c);
            RecomputeMass();
            return true;
        }

        static Bounds ColliderBounds(List<Collider> cols, Vector3 fallback)
        {
            bool any = false; var bb = new Bounds(fallback, Vector3.zero);
            foreach (var col in cols) { if (col == null || !col.enabled) continue; if (!any) { bb = col.bounds; any = true; } else bb.Encapsulate(col.bounds); }
            return bb;
        }

        void Forget(Cargo c)
        {
            foreach (var mine in _own) foreach (var theirs in c.Colliders) if (mine != null && theirs != null) Physics.IgnoreCollision(mine, theirs, false);
            _slots.Free(c.First, c.Cost);
            _cargo.Remove(c);
            RecomputeMass();
        }

        void Unload(Cargo c, Vector3 velocity)
        {
            Forget(c);
            if (c.No == null || !c.No.IsSpawned) return;
            if (c.Critter) c.Carry.OnReleased(PlayerIds.Nature); // AI takes it back (stunned)
            else if (c.Rb != null) c.Rb.isKinematic = c.WasKinematic;
            if (c.Rb != null && !c.Rb.isKinematic)
            {
                c.Rb.linearVelocity = _rb.linearVelocity + velocity;
                c.Rb.WakeUp();
            }
        }

        /// <summary>Server: takes <paramref name="no"/> out of the bed without throwing it.</summary>
        public void ServerUnload(NetworkObject no)
        {
            for (int i = _cargo.Count - 1; i >= 0; i--) if (_cargo[i].No == no) { Unload(_cargo[i], Vector3.zero); return; }
        }

        void PruneCargo()
        {
            for (int i = _cargo.Count - 1; i >= 0; i--)
            {
                var c = _cargo[i];
                if (c.No == null || !c.No.IsSpawned || c.Rb == null) { Forget(c); continue; }
                if (c.Critter && !c.Rb.isKinematic) Forget(c); // escaped
            }
        }

        void FollowCargo()
        {
            if (_cargo.Count == 0) return;
            var b = Bed;
            foreach (var c in _cargo)
            {
                if (c.Rb == null) continue;
                c.Rb.MovePosition(b.TransformPoint(c.LocalPos));
                c.Rb.MoveRotation(b.rotation * c.LocalRot);
            }
        }

        void RecomputeMass()
        {
            float kg = 0f;
            foreach (var c in _cargo) kg += c.Mass;
            _rb.mass = tuning.WagonMassKg + kg;
            if (IsSpawned && IsServer) { _cargoKg.Value = kg; _cargoCount.Value = (byte)Mathf.Min(255, _cargo.Count); }
        }

        void SyncState()
        {
            if (_cargoCount.Value != _cargo.Count) RecomputeMass();
        }

        // ================================================================================================ presentation

        void Update()
        {
            if (wheels == null || wheels.Length == 0) return;
            Vector3 v = _rb != null && !_rb.isKinematic ? _rb.linearVelocity : Vector3.zero;
            if (_rb != null && _rb.isKinematic && IsSpawned && !IsServer) v = (transform.position - _lastPos) / Mathf.Max(1e-4f, Time.deltaTime);
            _lastPos = transform.position;
            float vf = Vector3.Dot(v, transform.forward);
            _wheelSpin += vf / Mathf.Max(0.05f, wheelRadius) * Mathf.Rad2Deg * Time.deltaTime;
            foreach (var w in wheels) if (w != null) w.localRotation = Quaternion.Euler(_wheelSpin, 0f, 90f);
        }

        Vector3 _lastPos;
    }
}
