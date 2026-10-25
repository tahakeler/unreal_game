using System;
using System.Collections;
using System.Collections.Generic;
using HarvestPanic.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

namespace HarvestPanic.Customization
{
    /// <summary>Body material zone driven by the loadout.</summary>
    public enum BodyZone : byte { Fixed, Primary, Secondary, Accent, Skin, Hair }

    /// <summary>One sub-mesh of a body renderer and the loadout colour it follows.</summary>
    [Serializable]
    public struct ZoneBinding
    {
        /// <summary>Renderer.</summary>
        public Renderer renderer;
        /// <summary>Material/sub-mesh index on <see cref="renderer"/>.</summary>
        public int materialIndex;
        /// <summary>Colour source.</summary>
        public BodyZone zone;
        /// <summary>Colour when <see cref="zone"/> is Fixed (eyes, hair, belt).</summary>
        public Color fixedColor;
    }

    /// <summary>
    /// Third-person farmer body (Prefabs/Farmer/FarmerBody.prefab). Applies a <see cref="FarmerLoadout"/> (colours,
    /// body variant, hat/face/back accessories, name tag) and plays emote/locomotion animation.
    ///
    /// Implements <see cref="IPlayerHooks"/> for its *visual* members (BodyRenderer, BodyAnimator, HeadAttachPoint,
    /// PlayEmote, Faint pose). Identity members (ClientId, IsLocal, FirstPersonArms) forward to the Player prefab's own
    /// IPlayerHooks found in a parent, so the Player team can forward its visual members here without referencing
    /// this assembly: <c>bodyHooks = farmerBodyChild.GetComponent&lt;IPlayerHooks&gt;()</c>. See Assets/Scripts/Customization/INTEGRATION.md.
    /// Implements design/gdd/character-customization.md.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FarmerVisual : MonoBehaviour, IPlayerHooks
    {
        /// <summary>Animator parameter: horizontal speed in m/s.</summary>
        public static readonly int SpeedParam = Animator.StringToHash("Speed");
        /// <summary>Animator parameter: grounded flag.</summary>
        public static readonly int GroundedParam = Animator.StringToHash("Grounded");
        /// <summary>Animator trigger: jump.</summary>
        public static readonly int JumpParam = Animator.StringToHash("Jump");
        /// <summary>Animator parameter: fainted (holds the faint pose while true).</summary>
        public static readonly int FaintedParam = Animator.StringToHash("Fainted");
        /// <summary>Animator parameter: hands on the carried object (0 none, 1 one-handed, 2 two-handed).</summary>
        public static readonly int CarryHandsParam = Animator.StringToHash("CarryHands");
        /// <summary>Animator parameter: holding a tool.</summary>
        public static readonly int ToolParam = Animator.StringToHash("Tool");
        /// <summary>Animator parameter: throw wind-up 0..1.</summary>
        public static readonly int ThrowChargeParam = Animator.StringToHash("ThrowCharge");
        /// <summary>Animator trigger: let go of the carried object (toss gesture).</summary>
        public static readonly int TossParam = Animator.StringToHash("Toss");
        /// <summary>Animator parameter: ragdolled / knocked out (limp).</summary>
        public static readonly int DownParam = Animator.StringToHash("Down");
        /// <summary>Animator trigger: get up after being down.</summary>
        public static readonly int GetUpParam = Animator.StringToHash("GetUp");
        /// <summary>Animator layer with the carry / throw upper-body poses (weight driven from code).</summary>
        public const int UpperBodyLayer = 1;

        static readonly int PrimaryId = Shader.PropertyToID("_PrimaryColor"), SecondaryId = Shader.PropertyToID("_SecondaryColor"),
            AccentId = Shader.PropertyToID("_AccentColor"), SkinId = Shader.PropertyToID("_SkinColor"), HairId = Shader.PropertyToID("_HairColor"),
            BandId = Shader.PropertyToID("_BandColor"), HideHatId = Shader.PropertyToID("_HideHat"), PatternId = Shader.PropertyToID("_PatternMap"),
            PatternTilingId = Shader.PropertyToID("_PatternTiling"), ShirtPatternId = Shader.PropertyToID("_ShirtPatternMap"),
            ShirtPatternTilingId = Shader.PropertyToID("_ShirtPatternTiling");

        [SerializeField] CosmeticCatalog catalog;
        [SerializeField] Animator animator;
        [SerializeField, Tooltip("Model root scaled by the body variant.")] Transform modelRoot;
        [SerializeField] Transform headBone;
        [SerializeField, Tooltip("Hat attach (crown top, +Z forward).")] Transform headAttach;
        [SerializeField, Tooltip("Face attach (eye level, face front).")] Transform faceAttach;
        [SerializeField, Tooltip("Back attach (back surface, chest height).")] Transform backAttach;
        [SerializeField, Tooltip("Chest attach (chest bone; outfit parts).")] Transform chestAttach;
        [SerializeField, Tooltip("Hips attach (hips bone; outfit parts).")] Transform hipsAttach;
        [SerializeField] SkinnedMeshRenderer bodyRenderer;
        [SerializeField] List<ZoneBinding> zones = new List<ZoneBinding>();
        [SerializeField] NameTag nameTag;
        [SerializeField, Tooltip("Derive Speed/Grounded/Jump from transform motion. Turn off if the Player drives the Animator parameters.")]
        bool autoLocomotion = true;
        [SerializeField, Tooltip("Standalone (no Player parent): treat as the local player.")] bool offlineIsLocal;

        [Header("Recoloured body (mask shader, e.g. the hero Tripo farmer)")]
        [SerializeField, Tooltip("Renderers using HarvestPanic/Characters/FarmerRecolour; colours go through property blocks.")]
        List<Renderer> recolourRenderers = new List<Renderer>();
        [SerializeField, Tooltip("Key of the hat baked into the body mesh (shown when that hat is equipped, clipped otherwise).")]
        string bakedHatKey;
        [SerializeField, Tooltip("Body-variant head scale that matches this model's own head (chunky heads are already modelled).")]
        float headScaleReference = 1f;
        [SerializeField, Tooltip("Body-variant scale that matches this model's own proportions (variants scale relative to it).")]
        Vector3 bodyScaleReference = Vector3.one;
        [SerializeField, Tooltip("Scalp cap (hair coloured) plugging the head top when the baked hat is clipped; HatAttach space.")]
        Vector3 scalpCapSize = new Vector3(0.3f, 0.14f, 0.32f);
        [SerializeField] Vector3 scalpCapOffset = new Vector3(0f, -0.04f, -0.01f);
        [SerializeField, Tooltip("Per zone (overalls, shirt, boots/gloves, skin, hair, band): the swatch index that shows the painted colour.")]
        int[] homeSwatches = new int[0];
        [SerializeField, Tooltip("Per zone: the painted colour (the recolour shader's reference) shown for the home swatch.")]
        Color[] paintedColours = new Color[0];

        readonly List<GameObject> _accessories = new List<GameObject>();
        IPlayerHooks _owner;
        bool _ownerSearched;
        Vector3 _lastPos;
        float _speed, _airTime, _groundTime;
        bool _grounded = true;
        bool _firstPersonHidden;
        Coroutine _faintRoutine;
        float _headScale = 1f;
        Vector3 _headShape = Vector3.one;
        MaterialPropertyBlock _mpb;
        IPlayerAnimationSource _animSource;
        bool _animSourceSearched, _wasDown;
        int _lastCarry;
        float _upperWeight, _tossHold;
        readonly HashSet<int> _params = new HashSet<int>();

        /// <summary>True when the baked hat is currently clipped (another hat or none is worn).</summary>
        public bool BakedHatHidden { get; private set; }

        /// <summary>Renderers recoloured through property blocks (empty for the zone-material farmer).</summary>
        public IReadOnlyList<Renderer> RecolourRenderers => recolourRenderers;

        /// <summary>Key of the hat baked into the body mesh, or null.</summary>
        public string BakedHatKey => bakedHatKey;

        /// <summary>Body-variant scale that leaves the model at its own proportions (variants scale relative to it).</summary>
        public Vector3 BodyScaleReference => bodyScaleReference;

        /// <summary>The Player's animation state source above this body, or null when standalone.</summary>
        public IPlayerAnimationSource AnimSource
        {
            get
            {
                if (_animSourceSearched && (!(_animSource is UnityEngine.Object uo) || uo != null)) return _animSource;
                _animSourceSearched = true;
                _animSource = Owner as IPlayerAnimationSource;
                return _animSource;
            }
        }
        Vector3 _headBaseScale = Vector3.one;
        Vector3 _hatAttachBase = Vector3.one, _faceAttachBase = Vector3.one;
        Renderer _baseHeadRenderer;

        /// <summary>Raised after a loadout is applied.</summary>
        public event Action<FarmerLoadout> Applied;

        /// <summary>Catalog in use.</summary>
        public CosmeticCatalog Catalog { get => catalog; set => catalog = value; }

        /// <summary>Last applied loadout.</summary>
        public FarmerLoadout Current { get; private set; } = FarmerLoadout.Default;

        /// <summary>True once any loadout has been applied.</summary>
        public bool HasApplied { get; private set; }

        /// <summary>Wardrobe preview instance (never hidden, no name tag, no input).</summary>
        public bool IsPreview { get; set; }

        /// <summary>Accessory instances currently worn (hat, face, back order; nulls skipped).</summary>
        public IReadOnlyList<GameObject> Accessories => _accessories;

        /// <summary>Name tag component (may be null).</summary>
        public NameTag NameTag => nameTag;

        /// <summary>Back attach point (spine) for back items.</summary>
        public Transform BackAttachPoint => backAttach;

        /// <summary>Face attach point.</summary>
        public Transform FaceAttachPoint => faceAttach;

        /// <summary>Chest attach point (outfit parts; falls back to the back attach).</summary>
        public Transform ChestAttachPoint => chestAttach != null ? chestAttach : backAttach;

        /// <summary>Hips attach point (outfit parts; falls back to the model root).</summary>
        public Transform HipsAttachPoint => hipsAttach != null ? hipsAttach : modelRoot;

        /// <summary>Transform for an attach point (null when missing).</summary>
        public Transform AttachFor(AttachPoint p)
        {
            switch (p)
            {
                case AttachPoint.Face: return faceAttach;
                case AttachPoint.Back: return backAttach;
                case AttachPoint.Chest: return ChestAttachPoint;
                case AttachPoint.Hips: return HipsAttachPoint;
                default: return headAttach;
            }
        }

        /// <summary>Colour the first-person sleeves show for the current loadout (outfit-dependent).</summary>
        public Color SleeveColor { get; private set; }

        /// <summary>The Player prefab's IPlayerHooks above this body, or null when standalone.</summary>
        public IPlayerHooks Owner
        {
            get
            {
                if (_ownerSearched && (!(_owner is UnityEngine.Object uo) || uo != null)) return _owner;
                _ownerSearched = true;
                _owner = null;
                if (transform.parent != null)
                    foreach (var h in transform.parent.GetComponentsInParent<IPlayerHooks>(true))
                        if (!ReferenceEquals(h, this)) { _owner = h; break; }
                return _owner;
            }
        }

        // ---- IPlayerHooks -------------------------------------------------------------------------------------------

        /// <inheritdoc/>
        public ulong ClientId
        {
            get
            {
                if (Owner != null) return Owner.ClientId;
                var no = GetComponentInParent<NetworkObject>();
                return no != null && no.IsSpawned ? no.OwnerClientId : 0UL;
            }
        }

        /// <inheritdoc/>
        public bool IsLocal => !IsPreview && (Owner != null ? Owner.IsLocal : offlineIsLocal);

        /// <inheritdoc/>
        public Renderer BodyRenderer => bodyRenderer;

        /// <inheritdoc/>
        public Animator BodyAnimator => animator;

        /// <inheritdoc/>
        public Transform FirstPersonArms => Owner?.FirstPersonArms;

        /// <inheritdoc/>
        public Transform HeadAttachPoint => headAttach;

        /// <summary>Plays the emote's body animation locally (no network, no noise). "faint" holds the faint pose for its duration.</summary>
        public void PlayEmote(string emoteId)
        {
            var def = catalog != null ? catalog.FindEmote(emoteId) : null;
            if (def == null || animator == null) return;
            if (def.kind == EmoteKind.Faint)
            {
                if (_faintRoutine != null) StopCoroutine(_faintRoutine);
                _faintRoutine = StartCoroutine(FaintFor(def.duration));
                return;
            }
            animator.CrossFadeInFixedTime(def.animState, 0.12f, 0);
        }

        /// <summary>Starts or ends the faint pose on the body (visual only; ragdoll/physics belong to the Player).</summary>
        public void Faint(bool fainted)
        {
            if (animator == null) return;
            if (AnimSource != null) return; // a real player ragdolls: the Down pose follows its state, no second fall
            
            animator.SetBool(FaintedParam, fainted);
            if (fainted) animator.CrossFadeInFixedTime("Faint", 0.1f, 0);
        }

        IEnumerator FaintFor(float seconds)
        {
            Faint(true);
            yield return new WaitForSeconds(seconds);
            Faint(false);
            _faintRoutine = null;
        }

        // ---- Loadout ------------------------------------------------------------------------------------------------

        /// <summary>
        /// Applies <paramref name="loadout"/> (assumed already sanitised by the server): body variant, colour zones,
        /// accessories and name tag. <paramref name="poof"/> shows the 0.3 s hay poof others see on Confirm.
        /// </summary>
        public void Apply(FarmerLoadout loadout, bool poof = false)
        {
            if (catalog == null) { Debug.LogWarning("[FarmerVisual] No catalog assigned", this); return; }
            Current = loadout;
            HasApplied = true;
            EnsureBaseScales();

            var body = catalog.Body(loadout.Body);
            if (modelRoot != null)
            {
                var r = bodyScaleReference;
                modelRoot.localScale = new Vector3(body.bodyScale.x / Mathf.Max(0.01f, r.x), body.bodyScale.y / Mathf.Max(0.01f, r.y), body.bodyScale.z / Mathf.Max(0.01f, r.z));
            }
            _headScale = body.headScale / Mathf.Max(0.01f, headScaleReference);

            var headItem = catalog.FindItem(CosmeticSlot.Head, loadout.Head);
            var hairItem = catalog.FindItem(CosmeticSlot.Hair, loadout.Hair);
            var outfitItem = catalog.FindItem(CosmeticSlot.Outfit, loadout.Outfit);
            var hatItem = catalog.FindItem(CosmeticSlot.Hat, loadout.Hat);
            ApplyHeadShape(headItem != null && headItem.headShape != Vector3.zero ? headItem.headShape : Vector3.one);
            if (_baseHeadRenderer != null) _baseHeadRenderer.enabled = headItem == null || headItem.prefab == null; // authored heads replace the mesh

            var tints = TintsFor(loadout);
            var style = outfitItem != null ? outfitItem.outfit : new OutfitStyle { shirtTint = PartTint.Secondary, sleeveTint = PartTint.Secondary };
            Color shirt = tints.Resolve(style.shirtTint == PartTint.Fixed ? PartTint.Secondary : style.shirtTint, tints.Secondary);
            Color hairZone = hairItem != null && hairItem.hideBaseHair ? tints.Skin : tints.Hair;
            var mats = new Dictionary<Renderer, Material[]>();
            foreach (var z in zones)
            {
                if (z.renderer == null) continue;
                if (!mats.TryGetValue(z.renderer, out var arr)) mats[z.renderer] = arr = z.renderer.sharedMaterials;
                if (z.materialIndex < 0 || z.materialIndex >= arr.Length) continue;
                Material m;
                switch (z.zone)
                {
                    case BodyZone.Primary: m = AccessoryBuilder.MaterialFor(catalog.baseMaterial, tints.Primary, style.overallsPattern); break;
                    case BodyZone.Secondary: m = AccessoryBuilder.MaterialFor(catalog.baseMaterial, shirt, style.shirtTint == PartTint.Skin ? OutfitPattern.Plain : style.shirtPattern); break;
                    case BodyZone.Accent: m = AccessoryBuilder.MaterialFor(catalog.baseMaterial, tints.Accent); break;
                    case BodyZone.Skin: m = AccessoryBuilder.MaterialFor(catalog.baseMaterial, tints.Skin); break;
                    case BodyZone.Hair: m = AccessoryBuilder.MaterialFor(catalog.baseMaterial, hairZone); break;
                    default: m = AccessoryBuilder.MaterialFor(catalog.baseMaterial, z.fixedColor); break;
                }
                arr[z.materialIndex] = m;
            }
            foreach (var kv in mats) kv.Key.sharedMaterials = kv.Value;

            bool bakedHat = !string.IsNullOrEmpty(bakedHatKey) && hatItem != null && hatItem.key == bakedHatKey;
            BakedHatHidden = !string.IsNullOrEmpty(bakedHatKey) && !bakedHat;
            if (recolourRenderers.Count > 0)
                ApplyRecolour(tints, style, shirt, hairItem != null && hairItem.hideBaseHair ? tints.Skin : tints.Hair);

            ClearAccessories();
            bool hatWorn = hatItem != null;
            AddAccessory(bakedHat ? null : hatItem, CosmeticSlot.Hat, tints, hatWorn);
            if (BakedHatHidden && headAttach != null && !(hatItem != null && hatItem.hidesHair))
            {
                var cap = new CosmeticPart { shape = PartShape.Sphere, position = scalpCapOffset, size = scalpCapSize, tint = hairItem != null && hairItem.hideBaseHair ? PartTint.Skin : PartTint.Hair };
                var go = AccessoryBuilder.BuildPart(cap, headAttach, catalog.baseMaterial, tints);
                go.name = "ScalpCap";
                _accessories.Add(go);
            }
            AddAccessory(catalog.FindItem(CosmeticSlot.Face, loadout.Face), CosmeticSlot.Face, tints, hatWorn);
            AddAccessory(catalog.FindItem(CosmeticSlot.Back, loadout.Back), CosmeticSlot.Back, tints, hatWorn);
            AddAccessory(headItem, CosmeticSlot.Head, tints, hatWorn);
            if (!(hatItem != null && hatItem.hidesHair)) AddAccessory(hairItem, CosmeticSlot.Hair, tints, hatWorn);
            AddAccessory(outfitItem, CosmeticSlot.Outfit, tints, hatWorn);

            if (nameTag != null) nameTag.SetStyle(catalog.NameTag(loadout.NameTag), catalog.unlitMaterial);
            ApplyFirstPersonVisibility();
            SleeveColor = tints.Resolve(style.sleeveTint == PartTint.Fixed ? PartTint.Secondary : style.sleeveTint, tints.Secondary);
            ApplyArmColors(SleeveColor, tints.Accent, tints.Skin);
            if (poof && !IsPreview) HayPoof.Spawn(transform.position + Vector3.up * 1.1f, catalog.poofDuration, catalog.baseMaterial);
            Applied?.Invoke(loadout);
        }

        /// <summary>Mask-shader body: colours, patterns, hat clip and band (name-tag colour) through one property block per renderer.</summary>
        void ApplyRecolour(in TintColors tints, OutfitStyle style, Color shirt, Color hair)
        {
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            bool tank = style.shirtTint == PartTint.Skin;
            foreach (var r in recolourRenderers)
            {
                if (r == null) continue;
                _mpb.Clear();
                _mpb.SetColor(PrimaryId, tints.Primary);
                _mpb.SetColor(SecondaryId, shirt);
                _mpb.SetColor(AccentId, tints.Accent);
                _mpb.SetColor(SkinId, tints.Skin);
                _mpb.SetColor(HairId, hair);
                _mpb.SetColor(BandId, tints.NameTag);
                _mpb.SetFloat(HideHatId, BakedHatHidden ? 1f : 0f);
                SetPattern(PatternId, PatternTilingId, style.overallsPattern);
                SetPattern(ShirtPatternId, ShirtPatternTilingId, tank ? OutfitPattern.Plain : style.shirtPattern);
                r.SetPropertyBlock(_mpb);
            }
        }

        void SetPattern(int tex, int tiling, OutfitPattern p)
        {
            if (p == OutfitPattern.Plain) { _mpb.SetTexture(tex, Texture2D.whiteTexture); _mpb.SetVector(tiling, Vector4.one); return; }
            var t = OutfitPatterns.Tiling(p);
            _mpb.SetTexture(tex, OutfitPatterns.Texture(p));
            _mpb.SetVector(tiling, new Vector4(t.x, t.y, 0f, 0f));
        }

        void EnsureBaseScales()
        {
            if (_baseScalesCaptured) return;
            _baseScalesCaptured = true;
            if (headBone != null) _headBaseScale = headBone.localScale;
            if (headAttach != null) _hatAttachBase = headAttach.localScale;
            if (faceAttach != null) _faceAttachBase = faceAttach.localScale;
            foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (smr != bodyRenderer && smr.name.IndexOf("Head", StringComparison.OrdinalIgnoreCase) >= 0) { _baseHeadRenderer = smr; break; }
        }

        bool _baseScalesCaptured;

        /// <summary>
        /// Per-axis counter-scale for a child whose local axes are rotated by <paramref name="local"/> inside a parent scaled by
        /// <paramref name="shape"/> (bone axes): each child axis cancels the bone axis it lines up with.
        /// </summary>
        static Vector3 Counter(Quaternion local, Vector3 shape, float uniform)
        {
            var c = Vector3.one;
            for (int i = 0; i < 3; i++)
            {
                Vector3 axis = local * (i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward);
                float ax = Mathf.Abs(axis.x), ay = Mathf.Abs(axis.y), az = Mathf.Abs(axis.z);
                float s = ax >= ay && ax >= az ? shape.x : ay >= az ? shape.y : shape.z;
                c[i] = uniform / s;
            }
            return c;
        }

        /// <summary>Non-uniform head shape on the head bone; hat/face attach points are counter-scaled so accessories keep their proportions.</summary>
        void ApplyHeadShape(Vector3 shape)
        {
            shape = new Vector3(Mathf.Max(0.5f, shape.x), Mathf.Max(0.5f, shape.y), Mathf.Max(0.5f, shape.z));
            _headShape = shape;
            float uniform = (shape.x + shape.z) * 0.5f;
            if (headAttach != null) headAttach.localScale = Vector3.Scale(_hatAttachBase, Counter(headAttach.localRotation, shape, uniform));
            if (faceAttach != null) faceAttach.localScale = Vector3.Scale(_faceAttachBase, Counter(faceAttach.localRotation, shape, uniform));
            if (headBone != null) headBone.localScale = Vector3.Scale(_headBaseScale * _headScale, _headShape);
        }

        /// <summary>Dresses the owner's first-person arms: shirt = Secondary, gloves = Accent (boots/gloves zone).</summary>
        void ApplyArmColors(Color sleeve, Color glove, Color skin)
        {
            if (IsPreview) return;
            var arms = FirstPersonArms;
            if (arms != null && arms.TryGetComponent<IFirstPersonArmsSkin>(out var skinHook)) skinHook.ApplyArmColors(sleeve, glove, skin);
        }

        /// <summary>Recolour zone order of <see cref="HomeSwatches"/> / <see cref="PaintedColours"/>.</summary>
        public const int ZonePrimary = 0, ZoneSecondary = 1, ZoneAccent = 2, ZoneSkin = 3, ZoneHair = 4, ZoneBand = 5;

        /// <summary>Per zone: the swatch that shows the model's painted colour (empty for the zone-material farmer).</summary>
        public IReadOnlyList<int> HomeSwatches => homeSwatches;

        /// <summary>Per zone: the painted colour shown for the home swatch.</summary>
        public IReadOnlyList<Color> PaintedColours => paintedColours;

        Color Shown(int zone, int index, Color swatch) =>
            homeSwatches != null && paintedColours != null && zone < homeSwatches.Length && zone < paintedColours.Length && homeSwatches[zone] == index
                ? paintedColours[zone] : swatch;

        /// <summary>
        /// Resolved colours for recipe tints. A zone on its home swatch resolves to the painted colour, so the shader's
        /// colour transfer is the identity there and accessories tinted from that slot match the painting.
        /// </summary>
        public TintColors TintsFor(FarmerLoadout l) => new TintColors
        {
            Primary = Shown(ZonePrimary, l.Primary, catalog.PaletteColor(l.Primary)),
            Secondary = Shown(ZoneSecondary, l.Secondary, catalog.PaletteColor(l.Secondary)),
            Accent = Shown(ZoneAccent, l.Accent, catalog.PaletteColor(l.Accent)),
            NameTag = Shown(ZoneBand, l.NameTag, catalog.NameTag(l.NameTag).color),
            Skin = Shown(ZoneSkin, l.Skin, catalog.SkinColor(l.Skin)),
            Hair = Shown(ZoneHair, l.HairColor, catalog.PaletteColor(l.HairColor)),
        };

        void AddAccessory(CosmeticItem item, CosmeticSlot slot, in TintColors tints, bool hatWorn)
        {
            if (item == null) return;
            AccessoryBuilder.Build(item, AttachFor, CosmeticCatalog.DefaultAttach(slot), catalog.baseMaterial, tints, hatWorn && slot == CosmeticSlot.Hair, _accessories);
        }

        void ClearAccessories()
        {
            foreach (var a in _accessories)
                if (a != null)
                {
                    a.SetActive(false); // Destroy is deferred: hide now so back-to-back applies (thumbnails) never stack
                    a.transform.SetParent(null, false);
                    if (Application.isPlaying) Destroy(a); else DestroyImmediate(a);
                }
            _accessories.Clear();
        }

        /// <summary>
        /// Re-evaluates first-person hiding: the local player's body and cosmetics render shadows only
        /// (you see your hat in your shadow), and their name tag is hidden. Call after spawn/ownership changes.
        /// </summary>
        public void ApplyFirstPersonVisibility()
        {
            _ownerSearched = false;
            _firstPersonHidden = IsLocal;
            var mode = _firstPersonHidden ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (nameTag != null && r.transform.IsChildOf(nameTag.transform)) continue;
                r.shadowCastingMode = mode;
            }
            if (nameTag != null) nameTag.gameObject.SetActive(!_firstPersonHidden && !IsPreview);
        }

        // ---- Unity --------------------------------------------------------------------------------------------------

        void Awake() => EnsureBaseScales();

        void OnEnable() => _lastPos = transform.position;

        void Update()
        {
            if (animator == null || IsPreview) return;
            DriveGameplayParams();
            if (!autoLocomotion) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Vector3 p = transform.position, v = (p - _lastPos) / dt;
            _lastPos = p;
            float horizontal = new Vector2(v.x, v.z).magnitude;
            _speed = Mathf.Lerp(_speed, horizontal > 20f ? 0f : horizontal, 1f - Mathf.Exp(-12f * dt));
            animator.SetFloat(SpeedParam, _speed);

            if (v.y > 1.8f && _grounded) { _grounded = false; _airTime = 0f; animator.SetTrigger(JumpParam); }
            if (!_grounded)
            {
                _airTime += dt;
                _groundTime = Mathf.Abs(v.y) < 0.25f ? _groundTime + dt : 0f;
                if (_airTime > 0.2f && _groundTime > 0.08f) _grounded = true;
            }
            animator.SetBool(GroundedParam, _grounded);
        }

        void LateUpdate() => ApplyHeadBoneScale();

        bool Has(int param)
        {
            if (_params.Count == 0 && animator != null && animator.runtimeAnimatorController != null)
                foreach (var p in animator.parameters) _params.Add(p.nameHash);
            return _params.Contains(param);
        }

        /// <summary>Carry / throw / down / get-up from the Player's replicated state (every peer).</summary>
        void DriveGameplayParams()
        {
            var src = AnimSource;
            if (src == null || !animator.isActiveAndEnabled) return;
            int hands = src.CarryHands;
            bool down = src.IsDown;
            if (Has(CarryHandsParam)) animator.SetInteger(CarryHandsParam, hands);
            if (Has(ToolParam)) animator.SetBool(ToolParam, src.HoldingTool);
            if (Has(ThrowChargeParam)) animator.SetFloat(ThrowChargeParam, src.ThrowCharge);
            if (Has(DownParam)) animator.SetBool(DownParam, down);
            if (_lastCarry > 0 && hands == 0 && !down && Has(TossParam)) { animator.SetTrigger(TossParam); _tossHold = 0.45f; }
            if (_wasDown && !down && Has(GetUpParam)) animator.SetTrigger(GetUpParam);
            _lastCarry = hands; _wasDown = down;
            if (animator.layerCount > UpperBodyLayer)
            {
                _tossHold -= Time.deltaTime;
                float target = !down && (hands > 0 || _tossHold > 0f) ? 1f : 0f;
                _upperWeight = Mathf.MoveTowards(_upperWeight, target, Time.deltaTime * 6f);
                animator.SetLayerWeight(UpperBodyLayer, _upperWeight);
            }
        }

        /// <summary>
        /// Re-applies the chunky-head scale and head shape to the head bone. Clips carry bone scale keys, so this runs after
        /// animation every frame (and after a manual Animator.Update in thumbnail captures).
        /// </summary>
        public void ApplyHeadBoneScale()
        {
            if (headBone != null && (!Mathf.Approximately(_headScale, 1f) || _headShape != Vector3.one))
                headBone.localScale = Vector3.Scale(_headBaseScale * _headScale, _headShape);
        }

        void OnDestroy() => ClearAccessories();

#if UNITY_EDITOR
        /// <summary>Editor builder hook: wires references in one call.</summary>
        public void EditorSetup(CosmeticCatalog cat, Animator anim, Transform model, Transform head, Transform hatAttach, Transform faceAt,
            Transform backAt, SkinnedMeshRenderer body, List<ZoneBinding> zoneList, NameTag tag)
        {
            catalog = cat; animator = anim; modelRoot = model; headBone = head; headAttach = hatAttach; faceAttach = faceAt;
            backAttach = backAt; bodyRenderer = body; zones = zoneList; nameTag = tag;
        }

        /// <summary>Editor builder hook: outfit attach points.</summary>
        public void EditorSetupOutfitAttach(Transform chest, Transform hips) { chestAttach = chest; hipsAttach = hips; }

        /// <summary>Editor builder hook: mask-recoloured body (hero farmer).</summary>
        public void EditorSetupRecolour(List<Renderer> renderers, string bakedHat, float headRef, Vector3 bodyRef, Vector3 capSize, Vector3 capOffset,
            int[] homes = null, Color[] painted = null)
        {
            recolourRenderers = renderers; bakedHatKey = bakedHat; headScaleReference = headRef; bodyScaleReference = bodyRef; scalpCapSize = capSize; scalpCapOffset = capOffset;
            homeSwatches = homes ?? new int[0]; paintedColours = painted ?? new Color[0];
        }
#endif

        /// <summary>Standalone farmer (no Player parent) counts as the local player (test scenes).</summary>
        public bool OfflineIsLocal { get => offlineIsLocal; set => offlineIsLocal = value; }
    }
}
