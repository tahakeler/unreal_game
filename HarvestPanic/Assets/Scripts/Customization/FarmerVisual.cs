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
    public enum BodyZone : byte { Fixed, Primary, Secondary, Accent, Skin }

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
    /// this assembly: <c>bodyHooks = farmerBodyChild.GetComponent&lt;IPlayerHooks&gt;()</c>. See docs/architecture/customization-integration.md.
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

        [SerializeField] CosmeticCatalog catalog;
        [SerializeField] Animator animator;
        [SerializeField, Tooltip("Model root scaled by the body variant.")] Transform modelRoot;
        [SerializeField] Transform headBone;
        [SerializeField, Tooltip("Hat attach (crown top, +Z forward).")] Transform headAttach;
        [SerializeField, Tooltip("Face attach (eye level, face front).")] Transform faceAttach;
        [SerializeField, Tooltip("Back attach (back surface, chest height).")] Transform backAttach;
        [SerializeField] SkinnedMeshRenderer bodyRenderer;
        [SerializeField] List<ZoneBinding> zones = new List<ZoneBinding>();
        [SerializeField] NameTag nameTag;
        [SerializeField, Tooltip("Derive Speed/Grounded/Jump from transform motion. Turn off if the Player drives the Animator parameters.")]
        bool autoLocomotion = true;
        [SerializeField, Tooltip("Standalone (no Player parent): treat as the local player.")] bool offlineIsLocal;

        readonly List<GameObject> _accessories = new List<GameObject>();
        IPlayerHooks _owner;
        bool _ownerSearched;
        Vector3 _lastPos;
        float _speed, _airTime, _groundTime;
        bool _grounded = true;
        bool _firstPersonHidden;
        Coroutine _faintRoutine;
        float _headScale = 1f;

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

            var body = catalog.Body(loadout.Body);
            if (modelRoot != null) modelRoot.localScale = body.bodyScale;
            _headScale = body.headScale;

            var tints = TintsFor(loadout);
            Color skin = catalog.SkinColor(loadout.Skin);
            var mats = new Dictionary<Renderer, Material[]>();
            foreach (var z in zones)
            {
                if (z.renderer == null) continue;
                if (!mats.TryGetValue(z.renderer, out var arr)) mats[z.renderer] = arr = z.renderer.sharedMaterials;
                if (z.materialIndex < 0 || z.materialIndex >= arr.Length) continue;
                Color c = z.zone == BodyZone.Primary ? tints.Primary : z.zone == BodyZone.Secondary ? tints.Secondary
                    : z.zone == BodyZone.Accent ? tints.Accent : z.zone == BodyZone.Skin ? skin : z.fixedColor;
                arr[z.materialIndex] = AccessoryBuilder.MaterialFor(catalog.baseMaterial, c);
            }
            foreach (var kv in mats) kv.Key.sharedMaterials = kv.Value;

            ClearAccessories();
            AddAccessory(catalog.FindItem(CosmeticSlot.Hat, loadout.Hat), headAttach, tints);
            AddAccessory(catalog.FindItem(CosmeticSlot.Face, loadout.Face), faceAttach, tints);
            AddAccessory(catalog.FindItem(CosmeticSlot.Back, loadout.Back), backAttach, tints);

            if (nameTag != null) nameTag.SetStyle(catalog.NameTag(loadout.NameTag), catalog.unlitMaterial);
            ApplyFirstPersonVisibility();
            if (poof && !IsPreview) HayPoof.Spawn(transform.position + Vector3.up * 1.1f, catalog.poofDuration, catalog.baseMaterial);
            Applied?.Invoke(loadout);
        }

        /// <summary>Resolved colours for recipe tints.</summary>
        public TintColors TintsFor(FarmerLoadout l) => new TintColors
        {
            Primary = catalog.PaletteColor(l.Primary),
            Secondary = catalog.PaletteColor(l.Secondary),
            Accent = catalog.PaletteColor(l.Accent),
            NameTag = catalog.NameTag(l.NameTag).color,
        };

        void AddAccessory(CosmeticItem item, Transform attach, in TintColors tints)
        {
            var go = AccessoryBuilder.Build(item, attach, catalog.baseMaterial, tints);
            if (go != null) _accessories.Add(go);
        }

        void ClearAccessories()
        {
            foreach (var a in _accessories)
                if (a != null) { if (Application.isPlaying) Destroy(a); else DestroyImmediate(a); }
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

        void OnEnable() => _lastPos = transform.position;

        void Update()
        {
            if (!autoLocomotion || animator == null || IsPreview) return;
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

        void LateUpdate()
        {
            // Clips carry bone scale keys, so the chunky-head scale is re-applied after animation every frame.
            if (headBone != null && !Mathf.Approximately(_headScale, 1f)) headBone.localScale = Vector3.one * _headScale;
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
#endif

        /// <summary>Standalone farmer (no Player parent) counts as the local player (test scenes).</summary>
        public bool OfflineIsLocal { get => offlineIsLocal; set => offlineIsLocal = value; }
    }
}
