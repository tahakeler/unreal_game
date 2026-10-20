using System.Collections.Generic;
using HarvestPanic.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HarvestPanic.Gameplay
{
    /// <summary>
    /// Owner-only first-person arms (design/gdd/player-character.md 5b): stylized forearms with rolled shirt
    /// sleeves and chunky work gloves, built procedurally at runtime under the player's FirstPersonArms transform.
    /// Poses: idle (breathing, look-lag and walk sway), reach toward the aim point, side grip on two-hand items,
    /// one-hand carry from below, tool grip, throw wind-up and follow-through. Colours come from the farmer loadout
    /// through <see cref="IFirstPersonArmsSkin"/> (Customization calls it; this assembly never references it).
    /// Presentation only: reads <see cref="PlayerCarry"/> / <see cref="PlayerMotor"/>, owns no gameplay state.
    /// </summary>
    [DefaultExecutionOrder(60)]
    public sealed class FirstPersonArmsRig : MonoBehaviour, IFirstPersonArmsSkin
    {
        [SerializeField] ArmsTuning tuning;
        [SerializeField, Tooltip("URP Lit material cloned per colour zone")] Material baseMaterial;

        sealed class Finger { public Transform Proximal, Distal; }

        sealed class Arm
        {
            public float Side; // -1 left, +1 right
            public Transform Upper, Fore, Hand;
            public readonly List<Finger> Fingers = new List<Finger>();
            public Finger Thumb;
            public Vector3 Palm, PalmVel, Fwd = Vector3.forward, Up = Vector3.up;
            public float Curl;
        }

        ArmsTuning _t;
        Arm _left, _right;
        Material _shirt, _cuff, _skin, _glove;
        PlayerCharacter _pc;
        PlayerCarry _carry;
        PlayerMotor _motor;
        Transform _space;
        bool _built, _init;
        float _breath, _walkPhase, _swingT = -1f, _lastCharge;
        bool _wasHolding;
        Vector3 _lookSway;
        Quaternion _lastLook = Quaternion.identity;

        Transform _heldCached;
        readonly List<Renderer> _heldRenderers = new List<Renderer>();
        Transform _heldGrip;

        /// <summary>Current pose (tests, QA).</summary>
        public ArmPose Pose { get; private set; }
        /// <summary>Left/right palm centres in camera-pivot space (tests, QA).</summary>
        public Vector3 LeftPalm => _left != null ? _left.Palm : Vector3.zero;
        public Vector3 RightPalm => _right != null ? _right.Palm : Vector3.zero;

        void Awake()
        {
            _pc = GetComponentInParent<PlayerCharacter>();
            if (_pc != null) { _carry = _pc.GetComponent<PlayerCarry>(); _motor = _pc.GetComponent<PlayerMotor>(); }
            _t = tuning != null ? tuning : ScriptableObject.CreateInstance<ArmsTuning>();
            _space = transform.parent != null ? transform.parent : transform;
            Build();
        }

        void OnEnable() { _init = false; }

        void OnDestroy()
        {
            foreach (var m in new[] { _shirt, _cuff, _skin, _glove }) if (m != null) Destroy(m);
        }

        // ---- IFirstPersonArmsSkin ------------------------------------------------------------------------

        /// <inheritdoc />
        public void ApplyArmColors(Color shirt, Color glove, Color skin)
        {
            if (!_built) Build();
            SetColor(_shirt, shirt);
            SetColor(_cuff, shirt * _t.CuffShade);
            SetColor(_skin, skin);
            SetColor(_glove, glove);
        }

        static void SetColor(Material m, Color c)
        {
            if (m == null) return;
            c.a = 1f;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        // ---- Build -----------------------------------------------------------------------------------------

        Material NewMat(string n, Color c)
        {
            Material m;
            if (baseMaterial != null) m = new Material(baseMaterial);
            else
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit");
                m = new Material(sh != null ? sh : Shader.Find("Standard"));
            }
            m.name = "FPArms_" + n;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", _t.Smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            SetColor(m, c);
            return m;
        }

        void Build()
        {
            if (_built) return;
            _built = true;
            if (_t == null) _t = tuning != null ? tuning : ScriptableObject.CreateInstance<ArmsTuning>();
            // Legacy primitive arms from older prefabs.
            for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
            _shirt = NewMat("Shirt", _t.Shirt);
            _cuff = NewMat("Cuff", _t.Shirt * _t.CuffShade);
            _skin = NewMat("Skin", _t.Skin);
            _glove = NewMat("Glove", _t.Glove);
            _left = BuildArm(-1f);
            _right = BuildArm(1f);
        }

        GameObject Part(string n, Transform parent, Mesh mesh, Material mat, Vector3 pos, Quaternion rot)
        {
            var g = new GameObject(n);
            g.layer = gameObject.layer;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = rot;
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = true;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return g;
        }

        Transform Node(string n, Transform parent, Vector3 pos)
        {
            var t = new GameObject(n).transform;
            t.gameObject.layer = gameObject.layer;
            t.SetParent(parent, false);
            t.localPosition = pos;
            return t;
        }

        Arm BuildArm(float side)
        {
            var t = _t;
            string s = side < 0 ? "L" : "R";
            var arm = new Arm { Side = side };
            var root = Node("Arm" + s, transform, Vector3.zero);

            // Upper arm and forearm are unit-length nodes along +Y; length scale is applied each frame on Y only for
            // the bone node, and the meshes are children built at the rest length (stretch is small and reads fine).
            arm.Upper = Node("Upper" + s, root, Vector3.zero);
            Part("UpperSleeve" + s, arm.Upper, ArmMeshes.TaperedCapsule(t.UpperRadius, t.ElbowRadius * 1.05f, t.UpperLength), _shirt, Vector3.zero, Quaternion.identity);

            arm.Fore = Node("Fore" + s, root, Vector3.zero);
            float L = t.ForeLength;
            float sleeveEnd = L * t.SleeveFraction;
            float rMid = Mathf.Lerp(t.ElbowRadius, t.WristRadius, t.SleeveFraction);
            Part("Sleeve" + s, arm.Fore, ArmMeshes.TaperedCapsule(t.ElbowRadius * 1.08f, rMid * 1.1f, sleeveEnd), _shirt, Vector3.zero, Quaternion.identity);
            // Rolled cuff: a fat short band at the sleeve end.
            Part("Cuff" + s, arm.Fore, ArmMeshes.TaperedCapsule(rMid * 1.32f, rMid * 1.28f, 0.02f), _cuff, new Vector3(0f, sleeveEnd - 0.012f, 0f), Quaternion.identity);
            float gloveY = L * t.GloveStart;
            Part("Forearm" + s, arm.Fore, ArmMeshes.TaperedCapsule(rMid * 0.98f, t.WristRadius, gloveY - sleeveEnd + 0.01f), _skin, new Vector3(0f, sleeveEnd, 0f), Quaternion.identity);
            // Flared glove gauntlet up to the wrist.
            Part("Gauntlet" + s, arm.Fore, ArmMeshes.TaperedCapsule(t.WristRadius * 1.02f, t.GauntletRadius, L - gloveY - 0.01f), _glove, new Vector3(0f, gloveY, 0f), Quaternion.identity);

            // Hand: +Z fingers, +Y back of hand, thumb on the inner side (-side * X).
            arm.Hand = Node("Hand" + s, root, Vector3.zero);
            var hand = Node("HandScale" + s, arm.Hand, Vector3.zero);
            hand.localScale = Vector3.one * t.HandScale;
            Part("Palm" + s, hand, ArmMeshes.Ellipsoid(new Vector3(0.088f, 0.042f, 0.092f)), _glove, new Vector3(0f, 0f, 0.046f), Quaternion.identity);
            Part("Knuckles" + s, hand, ArmMeshes.Ellipsoid(new Vector3(0.084f, 0.036f, 0.03f)), _glove, new Vector3(0f, 0.004f, 0.084f), Quaternion.identity);
            Part("Heel" + s, hand, ArmMeshes.Ellipsoid(new Vector3(0.07f, 0.04f, 0.04f)), _glove, new Vector3(0f, -0.002f, 0.012f), Quaternion.identity);
            float[] xs = { -0.031f, -0.0105f, 0.0105f, 0.03f };
            float[] lens = { 0.034f, 0.037f, 0.034f, 0.027f };
            Quaternion along = Quaternion.Euler(90f, 0f, 0f); // capsule +Y -> finger +Z
            for (int i = 0; i < 4; i++)
            {
                float x = side > 0 ? xs[i] : -xs[i]; // xs[0] = index finger, next to the thumb (inner side)
                var f = new Finger { Proximal = Node("Finger" + i + s, hand, new Vector3(x, 0.002f, 0.088f)) };
                Part("Prox" + i + s, f.Proximal, ArmMeshes.TaperedCapsule(0.0125f, 0.0118f, lens[i]), _glove, Vector3.zero, along);
                f.Distal = Node("Dist" + i + s, f.Proximal, new Vector3(0f, 0f, lens[i]));
                Part("Tip" + i + s, f.Distal, ArmMeshes.TaperedCapsule(0.0118f, 0.011f, lens[i] * 0.8f), _glove, Vector3.zero, along);
                arm.Fingers.Add(f);
            }
            var thumb = new Finger { Proximal = Node("Thumb" + s, hand, new Vector3(-side * 0.04f, -0.006f, 0.03f)) };
            Part("ThumbProx" + s, thumb.Proximal, ArmMeshes.TaperedCapsule(0.0145f, 0.013f, 0.032f), _glove, Vector3.zero, along);
            thumb.Distal = Node("ThumbDist" + s, thumb.Proximal, new Vector3(0f, 0f, 0.032f));
            Part("ThumbTip" + s, thumb.Distal, ArmMeshes.TaperedCapsule(0.013f, 0.012f, 0.024f), _glove, Vector3.zero, along);
            arm.Thumb = thumb;
            return arm;
        }

        // ---- Pose ------------------------------------------------------------------------------------------

        void LateUpdate()
        {
            if (!_built || _pc == null) return;
            Tick(Time.deltaTime);
        }

        /// <summary>Snaps the arms to their current target pose (previews, QA captures).</summary>
        public void PoseNow() { _init = false; Tick(1f / 30f); }

        void Tick(float dt)
        {
            var t = _t;
            bool holding = _carry != null && _carry.IsSpawned && _carry.IsHoldingReplicated;
            Transform held = holding ? _carry.HeldObject : null;
            bool tool = holding && _carry.HoldingTool;
            int hands = held != null && held.TryGetComponent<ICarryable>(out var c) ? c.HandsRequired : 1;
            bool aiming = !holding && _carry != null && _carry.AimingAtGrabbable;

            // Throw follow-through: released while charging.
            float charge = _carry != null ? _carry.ThrowCharge : 0f;
            if (_wasHolding && !holding && _lastCharge > 0.05f) _swingT = 0f;
            _wasHolding = holding; _lastCharge = charge;
            float swing = 0f;
            if (_swingT >= 0f)
            {
                _swingT += dt;
                swing = ArmMath.SwingCurve(_swingT, t.SwingDuration);
                if (_swingT >= t.SwingDuration) _swingT = -1f;
            }

            Pose = ArmMath.Select(holding && held != null, tool, hands, aiming, swing > 0f && !holding);
            float curl = ArmMath.Curl(Pose);

            Vector3 idleL = t.IdleHand, idleR = Mirror(t.IdleHand);
            Vector3 pl = idleL, pr = idleR;
            Vector3 fwdL = new Vector3(0.22f, 0.25f, 1f), fwdR = Mirror(fwdL);
            Vector3 upL = new Vector3(-0.9f, 0.7f, 0f), upR = Mirror(upL);
            float curlL = curl, curlR = curl;

            switch (Pose)
            {
                case ArmPose.Reach:
                {
                    Vector3 aim = _carry.AimPoint;
                    Vector3 local = _space.InverseTransformPoint(aim);
                    float d = Mathf.Min(local.magnitude, t.ReachDistance);
                    Vector3 dir = local.sqrMagnitude > 1e-6f ? local.normalized : Vector3.forward;
                    Vector3 mid = dir * d + Vector3.down * t.ReachDrop;
                    Vector3 lateral = Vector3.Cross(Vector3.up, dir).normalized;
                    pl = Vector3.Lerp(idleL, mid - lateral * t.ReachHalfSpread, t.ReachBlend);
                    pr = Vector3.Lerp(idleR, mid + lateral * t.ReachHalfSpread, t.ReachBlend);
                    fwdL = dir + new Vector3(0.15f, 0f, 0f); fwdR = dir + new Vector3(-0.15f, 0f, 0f);
                    upL = new Vector3(-0.35f, 1f, 0f); upR = Mirror(upL);
                    curlL = curlR = curl;
                    break;
                }
                case ArmPose.GripTwoHands:
                {
                    if (HeldBox(held, out var center, out var ext))
                    {
                        ArmMath.SideGrip(center, ext, t.GripLow, t.GripInset, t.GripMaxHalfSpread, out pl, out pr);
                        fwdL = new Vector3(0.05f, 0.05f, 1f); fwdR = Mirror(fwdL);
                        upL = new Vector3(-1f, 0.15f, 0f); upR = Mirror(upL);
                    }
                    break;
                }
                case ArmPose.GripOneHand:
                {
                    if (HeldBox(held, out var center, out var ext))
                    {
                        // Right hand cradles it from below; left hand stays relaxed.
                        pr = new Vector3(center.x + ext.x * 0.15f, center.y - ext.y - 0.025f, center.z - ext.z * 0.2f);
                        fwdR = new Vector3(-0.2f, 0.15f, 1f);
                        upR = new Vector3(0.25f, -1f, 0f); // palm up
                        curlR = curl;
                        curlL = ArmMath.Curl(ArmPose.Idle);
                    }
                    break;
                }
                case ArmPose.Tool:
                {
                    if (ToolGrip(held, out var grip))
                    {
                        pr = grip + new Vector3(0f, -0.01f, -0.035f);
                        fwdR = new Vector3(-0.35f, 0.2f, 1f);
                        upR = new Vector3(0.9f, 0.5f, 0f);
                        curlR = curl;
                        curlL = ArmMath.Curl(ArmPose.Idle);
                    }
                    break;
                }
                case ArmPose.ThrowSwing:
                {
                    pl = Vector3.Lerp(idleL, t.SwingHand, swing);
                    pr = Vector3.Lerp(idleR, Mirror(t.SwingHand), swing);
                    fwdL = new Vector3(0.1f, 0.3f, 1f); fwdR = Mirror(fwdL);
                    upL = new Vector3(-0.3f, 1f, 0f); upR = Mirror(upL);
                    break;
                }
            }

            // Throw wind-up while charging: pull back and up.
            if (holding && charge > 0f)
            {
                Vector3 pull = new Vector3(0f, t.ThrowLift * charge, -t.ThrowPull * charge);
                pl += pull; pr += pull;
            }

            // Sway: breathing, look lag, walk bob.
            _breath += dt * t.BreathHz * Mathf.PI * 2f;
            Vector3 breath = new Vector3(0f, Mathf.Sin(_breath) * t.BreathAmplitude, Mathf.Sin(_breath * 0.5f) * t.BreathAmplitude * 0.5f);
            Quaternion look = _space.rotation;
            if (!_init) _lastLook = look;
            Quaternion delta = Quaternion.Inverse(_lastLook) * look;
            _lastLook = look;
            delta.ToAngleAxis(out float ang, out Vector3 axis);
            if (ang > 180f) ang -= 360f;
            Vector3 angVel = dt > 1e-5f && !float.IsNaN(axis.x) ? axis * (ang / dt) : Vector3.zero; // deg/s in camera space
            Vector3 targetSway = new Vector3(-angVel.y, angVel.x, 0f) * t.LookSway;
            targetSway = Vector3.ClampMagnitude(targetSway, t.LookSwayMax);
            _lookSway = Vector3.Lerp(_lookSway, targetSway, 1f - Mathf.Exp(-t.LookSwaySharpness * dt));
            float speed = _motor != null ? Mathf.Clamp(_motor.PlanarSpeed / Mathf.Max(0.01f, _pc.Tuning.WalkSpeed), 0f, 1.8f) : 0f;
            bool grounded = _motor == null || _motor.Grounded;
            if (grounded && speed > 0.05f) _walkPhase += dt * _pc.Tuning.HeadBobFrequency * Mathf.PI * 2f * Mathf.Max(0.6f, speed);
            float bobAmp = grounded ? t.WalkBob * speed : 0f;
            Vector3 bobL = new Vector3(Mathf.Cos(_walkPhase) * bobAmp * 0.6f, -Mathf.Abs(Mathf.Sin(_walkPhase)) * bobAmp, 0f);
            Vector3 bobR = new Vector3(Mathf.Cos(_walkPhase) * bobAmp * 0.6f, -Mathf.Abs(Mathf.Cos(_walkPhase)) * bobAmp, 0f);
            bool gripping = Pose == ArmPose.GripTwoHands;
            pl += breath + _lookSway + (gripping ? bobR : bobL);
            pr += breath + _lookSway + bobR;

            float k = _init ? 1f - Mathf.Exp(-t.Sharpness * dt) : 1f;
            Drive(_left, pl, fwdL, upL, curlL, k);
            Drive(_right, pr, fwdR, upR, curlR, k);
            _init = true;
        }

        static Vector3 Mirror(Vector3 v) => new Vector3(-v.x, v.y, v.z);

        void Drive(Arm a, Vector3 palm, Vector3 fwd, Vector3 up, float curl, float k)
        {
            var t = _t;
            a.Palm = Vector3.Lerp(a.Palm, palm, k);
            a.Fwd = Vector3.Slerp(a.Fwd, fwd.normalized, k);
            a.Up = Vector3.Slerp(a.Up, up.normalized, k);
            a.Curl = Mathf.Lerp(a.Curl, curl, k);

            Quaternion handRot = Quaternion.LookRotation(a.Fwd, a.Up);
            Vector3 wrist = a.Palm - handRot * new Vector3(0f, 0f, 0.046f * t.HandScale);
            Vector3 shoulder = a.Side < 0 ? t.Shoulder : Mirror(t.Shoulder);
            Vector3 pole = a.Side < 0 ? t.ElbowPole : Mirror(t.ElbowPole);
            Vector3 elbow = ArmMath.SolveTwoBone(shoulder, wrist, t.UpperLength, t.ForeLength, pole, t.MaxStretch, out Vector3 reached, out float stretch);

            Bone(a.Upper, shoulder, elbow, t.UpperLength);
            Bone(a.Fore, elbow, reached, t.ForeLength);
            a.Hand.localPosition = reached;
            a.Hand.localRotation = handRot;

            float c = a.Curl;
            foreach (var f in a.Fingers)
            {
                f.Proximal.localRotation = Quaternion.Euler(10f + c * 70f, 0f, 0f);
                f.Distal.localRotation = Quaternion.Euler(8f + c * 75f, 0f, 0f);
            }
            a.Thumb.Proximal.localRotation = Quaternion.Euler(12f + c * 30f, -a.Side * (48f - c * 22f), a.Side * (20f + c * 25f));
            a.Thumb.Distal.localRotation = Quaternion.Euler(c * 40f, 0f, 0f);
        }

        static void Bone(Transform bone, Vector3 from, Vector3 to, float restLength)
        {
            Vector3 d = to - from;
            float len = d.magnitude;
            bone.localPosition = from;
            bone.localRotation = len > 1e-5f ? Quaternion.FromToRotation(Vector3.up, d / len) : Quaternion.identity;
            bone.localScale = new Vector3(1f, restLength > 1e-4f ? len / restLength : 1f, 1f);
        }

        // ---- Held object geometry ----------------------------------------------------------------------------

        void CacheHeld(Transform held)
        {
            if (held == _heldCached) return;
            _heldCached = held;
            _heldRenderers.Clear();
            _heldGrip = null;
            if (held == null) return;
            held.GetComponentsInChildren(false, _heldRenderers);
            for (int i = _heldRenderers.Count - 1; i >= 0; i--)
                if (_heldRenderers[i] is ParticleSystemRenderer || _heldRenderers[i] is TrailRenderer || _heldRenderers[i] is LineRenderer) _heldRenderers.RemoveAt(i);
            foreach (var tr in held.GetComponentsInChildren<Transform>())
            {
                string n = tr.name;
                if (n == "Grip" || n == "GripR" || n == "HandleTop") { _heldGrip = tr; if (n != "HandleTop") break; }
            }
        }

        /// <summary>Held object's box in camera-pivot space (centre, half extents) from its renderers' local bounds.</summary>
        bool HeldBox(Transform held, out Vector3 center, out Vector3 ext)
        {
            center = ext = Vector3.zero;
            CacheHeld(held);
            bool any = false;
            Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
            foreach (var r in _heldRenderers)
            {
                if (r == null || !r.enabled) continue;
                Bounds lb = r.localBounds;
                Matrix4x4 m = _space.worldToLocalMatrix * r.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = lb.center + Vector3.Scale(lb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = m.MultiplyPoint3x4(corner);
                    min = Vector3.Min(min, p); max = Vector3.Max(max, p);
                }
                any = true;
            }
            if (!any) return false;
            center = (min + max) * 0.5f;
            ext = (max - min) * 0.5f;
            return true;
        }

        /// <summary>Tool handle in camera-pivot space: a "Grip"/"HandleTop" child, else a bounds heuristic.</summary>
        bool ToolGrip(Transform held, out Vector3 grip)
        {
            grip = Vector3.zero;
            CacheHeld(held);
            if (_heldGrip != null) { grip = _space.InverseTransformPoint(_heldGrip.position); return true; }
            if (!HeldBox(held, out var c, out var e)) return false;
            // Long tools (net, pole): grip the near end. Squat tools (bucket): the top of the handle.
            grip = e.z > e.y * 1.6f ? new Vector3(c.x, c.y, c.z - e.z * 0.7f) : new Vector3(c.x, c.y + e.y * 0.92f, c.z);
            return true;
        }
    }
}
