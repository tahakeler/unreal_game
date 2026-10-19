using System.Collections.Generic;
using HarvestPanic.Core;
using Unity.Netcode;
using UnityEngine;

namespace HarvestPanic.Gameplay
{
    /// <summary>
    /// Server-only carry simulation added at runtime to any grabbed <see cref="ICarryable"/>
    /// (design/gdd/physics-carry.md rules 2-4, 8). Each grip is a spring-damper "hand" (stiffness 1500,
    /// damping 120, max 2500 N per hand) applied at the grab point, so several carriers average out and
    /// opposing carriers create torque. Below <c>HandsRequired</c> the body is dragged (no lift).
    /// Also records the touch history and reports impacts above BruiseImpactMin as bruises
    /// (<see cref="IGoods.ApplyBruise"/>), impact noise, and player knock-backs (<see cref="IKnockable"/>)
    /// while held and for <c>ImpactReportWindow</c> seconds after release.
    /// </summary>
    public sealed class CarryBody : MonoBehaviour
    {
        sealed class Grip
        {
            public PlayerCarry Carrier;
            public Vector3 LocalPoint;
            public int Hands;
            public SlipTracker Slip;
            public float NextTouch;
        }

        readonly List<Grip> _grips = new List<Grip>();
        CarryTuning _t;
        Rigidbody _rb;
        IGoods _goods;
        TouchLog _log;
        float _savedAngularDamping;
        bool _savedKinematic, _stored, _selfSimulated;
        Vector2 _wobbleSeed;
        float _struggle;
        ICarryableEvents _events;

        /// <summary>The carried object.</summary>
        public ICarryable Carryable { get; private set; }
        /// <summary>The object's NetworkObject.</summary>
        public NetworkObject NetObject { get; private set; }
        /// <summary>Number of players holding.</summary>
        public int GripCount => _grips.Count;
        /// <summary>Hands attached across all players.</summary>
        public int HandsAttached { get; private set; }
        /// <summary>True when enough hands are attached to lift.</summary>
        public bool IsLifted => HandsAttached >= Mathf.Max(1, Carryable.HandsRequired);
        /// <summary>Touch/attribution log.</summary>
        public TouchLog Log => _log;

        /// <summary>Returns the body for <paramref name="no"/>, adding it on first grab (server).</summary>
        public static CarryBody GetOrAdd(NetworkObject no, ICarryable carryable)
        {
            var host = carryable.CarryTransform != null ? carryable.CarryTransform.gameObject : no.gameObject;
            if (!host.TryGetComponent<CarryBody>(out var body))
            {
                body = host.AddComponent<CarryBody>();
                body.Init(no, carryable);
            }
            return body;
        }

        void Init(NetworkObject no, ICarryable carryable)
        {
            NetObject = no;
            Carryable = carryable;
            _rb = GetComponent<Rigidbody>() ?? GetComponentInParent<Rigidbody>();
            _goods = GetComponentInParent<IGoods>() ?? GetComponentInChildren<IGoods>();
            _selfSimulated = carryable is ISelfSimulatedCarryable;
            var any = PlayerCharacter.All.Count > 0 ? PlayerCharacter.All[0].CarryTuning : null;
            _t = any != null ? any : ScriptableObject.CreateInstance<CarryTuning>();
            _log = new TouchLog(_t.TouchWindow, _t.TouchCapacity, _t.ImpactReportWindow);
            _wobbleSeed = new Vector2(Random.value * 100f, Random.value * 100f);
            _events = carryable as ICarryableEvents;
            if (_events != null) { _events.OnForcedRelease += HandleForcedRelease; _events.OnStruggle += HandleStruggle; }
        }

        /// <summary>The carryable broke free from these holders (critter escape).</summary>
        void HandleForcedRelease(ulong[] holders)
        {
            if (holders == null) return;
            for (int i = _grips.Count - 1; i >= 0; i--)
            {
                var c = _grips[i].Carrier;
                if (c != null && System.Array.IndexOf(holders, c.OwnerClientId) >= 0) { c.ServerOnGripLost(); RemoveGrip(c, HandlingMode.Slipped); }
            }
        }

        /// <summary>Critter wriggle (design units): shoves each holder and raises the wobble.</summary>
        void HandleStruggle(Vector3 designImpulse)
        {
            float mag = designImpulse.magnitude;
            _struggle = Mathf.Max(_struggle, Mathf.Clamp(mag / 120f, 0f, 3f));
            foreach (var g in _grips)
                if (g.Carrier != null) g.Carrier.Character.ApplyImpulse(designImpulse, PlayerIds.Nature, BlameVerb.LetEscape);
            if (_rb != null && !_rb.isKinematic) _rb.AddForce(ImpactUnits.ToUnity(mag) * Random.onUnitSphere, ForceMode.Impulse);
        }

        /// <summary>Adds a grip; rejects a second player on a 1-hand item and joins beyond what is needed.</summary>
        public bool TryAddGrip(PlayerCarry carrier, Vector3 localPoint, out int hands)
        {
            hands = 0;
            foreach (var g in _grips) if (g.Carrier == carrier) return false;
            int required = Mathf.Max(1, Carryable.HandsRequired);
            if (HandsAttached >= required) return false; // "no item held by two 1-hand grips"; crews are capped
            hands = CarryMath.HandsContributed(required, HandsAttached);
            _t = carrier.CarryTuning;
            _grips.Add(new Grip { Carrier = carrier, LocalPoint = localPoint, Hands = hands, Slip = new SlipTracker(_t.SlipDistance, _t.SlipTime) });
            HandsAttached += hands;

            if (!_stored && _rb != null && !_selfSimulated)
            {
                _stored = true;
                _savedAngularDamping = _rb.angularDamping;
                _savedKinematic = _rb.isKinematic;
                if (Carryable.IsKinematicCarry) _rb.isKinematic = true;
                else { _rb.isKinematic = false; _rb.angularDamping = _t.CarriedAngularDamping; }
            }

            ulong id = carrier.OwnerClientId;
            _log.Handle(id, HandlingMode.Held, Time.time);
            _goods?.RecordTouch(id);
            Carryable.OnGrabbed(id);
            PushLoad();
            return true;
        }

        /// <summary>Removes a carrier's grip (drop, slip, ragdoll, despawn).</summary>
        public void RemoveGrip(PlayerCarry carrier, HandlingMode mode)
        {
            int i = _grips.FindIndex(g => g.Carrier == carrier);
            if (i < 0) return;
            HandsAttached -= _grips[i].Hands;
            _grips.RemoveAt(i);
            ulong id = carrier.OwnerClientId;
            _log.Handle(id, _grips.Count > 0 ? HandlingMode.Held : mode, Time.time);
            _goods?.RecordTouch(id);
            Carryable.OnReleased(id);
            if (_grips.Count == 0) Restore();
            PushLoad();
        }

        /// <summary>Releases the sole carrier and launches the body (carrier blamed for impacts).</summary>
        public void ThrowFrom(PlayerCarry carrier, Vector3 velocity)
        {
            RemoveGrip(carrier, HandlingMode.Thrown);
            Carryable.ApplyThrow(velocity, carrier.OwnerClientId);
            // Generic fallback for implementers that only flag the throw.
            if (_rb != null && !_selfSimulated && !_rb.isKinematic && _rb.linearVelocity.sqrMagnitude < 0.01f) _rb.linearVelocity = velocity;
            NoiseService.TryEmit(NoiseSource.Impact, 0.15f, transform.position, NetObject.NetworkObjectId, carrier.OwnerClientId);
        }

        void Restore()
        {
            if (!_stored || _rb == null) return;
            _stored = false;
            _rb.angularDamping = _savedAngularDamping;
            _rb.isKinematic = !Carryable.IsKinematicCarry && _savedKinematic;
        }

        void PushLoad()
        {
            float mass = Carryable.MassKg;
            int required = Mathf.Max(1, Carryable.HandsRequired);
            float speed = CarryMath.SpeedMultiplier(mass, HandsAttached, required, _t.KgPerHand, _t.CarrySpeedFloor);
            foreach (var g in _grips)
                g.Carrier.ServerSetLoad(Carryable.IsKinematicCarry ? 1f : speed,
                    Carryable.IsKinematicCarry ? 0f : CarryMath.LoadRatio(mass, HandsAttached, _t.KgPerHand), g.Hands);
        }

        void FixedUpdate()
        {
            if (_grips.Count == 0) return;
            for (int i = _grips.Count - 1; i >= 0; i--)
            {
                var g = _grips[i];
                if (g.Carrier == null || !g.Carrier.IsSpawned) { _grips.RemoveAt(i); HandsAttached -= g.Hands; continue; }
            }
            if (_grips.Count == 0) { Restore(); return; }
            if (NetObject == null || !NetObject.IsSpawned) { DropAll(); return; }

            float now = Time.time, dt = Time.fixedDeltaTime;
            _struggle = Mathf.MoveTowards(_struggle, 0f, dt);
            foreach (var g in _grips)
                if (now >= g.NextTouch) { g.NextTouch = now + _t.HoldTouchRefresh; _log.Touch(g.Carrier.OwnerClientId, now); _goods?.RecordTouch(g.Carrier.OwnerClientId); }

            PushLoad();
            if (_selfSimulated || _rb == null) return;

            if (Carryable.IsKinematicCarry)
            {
                var c = _grips[0].Carrier;
                _rb.MovePosition(c.HoldPoint(true));
                _rb.MoveRotation(c.ToolRotation);
                return;
            }

            bool lifted = IsLifted;
            Grip slipped = null;
            foreach (var g in _grips)
            {
                var carrier = g.Carrier.Character;
                Vector3 p = transform.TransformPoint(g.LocalPoint);
                float speedRatio = carrier.Velocity.magnitude / Mathf.Max(0.1f, carrier.Tuning.SprintSpeed);
                float amp = CarryMath.WobbleAmplitude(_t.WobbleAmplitude, speedRatio, _struggle);
                float tn = now * _t.WobbleFrequency;
                Vector3 wobble = new Vector3(Mathf.PerlinNoise(tn, _wobbleSeed.x) - 0.5f, Mathf.PerlinNoise(_wobbleSeed.y, tn) - 0.5f, Mathf.PerlinNoise(tn, tn + _wobbleSeed.x) - 0.5f) * (2f * amp);
                Vector3 target = g.Carrier.HoldPoint(false) + wobble;
                Vector3 to = target - p;
                Vector3 f = CarryMath.HandForce(to, _rb.GetPointVelocity(p) - carrier.Velocity,
                    _t.HandStiffness * g.Hands, _t.HandDamping * g.Hands, _t.HandMaxForce * g.Hands);
                if (!lifted) f.y = Mathf.Min(f.y, 0f); // dragged: friction on the ground, no lift
                _rb.AddForceAtPosition(f, p);
                if (g.Slip.Tick(to.magnitude, dt)) slipped = g;
            }
            if (slipped != null)
            {
                var c = slipped.Carrier;
                c.ServerOnGripLost();
                RemoveGrip(c, HandlingMode.Slipped);
            }
        }

        void DropAll()
        {
            for (int i = _grips.Count - 1; i >= 0; i--)
            {
                var c = _grips[i].Carrier;
                if (c != null) { c.ServerOnGripLost(); RemoveGrip(c, HandlingMode.Dropped); }
            }
        }

        void OnDestroy()
        {
            if (_events != null) { _events.OnForcedRelease -= HandleForcedRelease; _events.OnStruggle -= HandleStruggle; }
            DropAll();
        }

        void OnCollisionEnter(Collision c)
        {
            if (NetObject == null || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
            float now = Time.time;
            if (!_log.IsReporting(now)) return;
            float design = ImpactUnits.ToDesign(c.impulse.magnitude);
            ulong instigator = _log.Instigator(now);

            // Thrown/dropped items knock players over (not the carriers holding it).
            if (Knockables.TryGetFor(c.collider.gameObject, out var knock))
            {
                bool isCarrier = false;
                foreach (var g in _grips) if (c.collider.transform.IsChildOf(g.Carrier.transform)) isCarrier = true;
                if (!isCarrier && design > 0f)
                {
                    Vector3 dir = _rb != null && _rb.linearVelocity.sqrMagnitude > 0.01f ? _rb.linearVelocity.normalized : -c.impulse.normalized;
                    knock.ApplyImpulse(dir * design, instigator, BlameVerb.Threw);
                }
            }

            if (design < _t.BruiseImpactMin) return;
            if (_goods != null && !Carryable.IsKinematicCarry) _goods.ApplyBruise(design, instigator, _log.VerbForImpact());
            NoiseService.TryEmit(NoiseSource.Impact, design > _t.HeavyImpact ? _t.HeavyImpactLoudness : _t.LightImpactLoudness,
                c.GetContact(0).point, NetObject.NetworkObjectId, instigator);
        }
    }
}
