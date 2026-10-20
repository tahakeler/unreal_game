using HarvestPanic.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace HarvestPanic.Customization
{
    /// <summary>
    /// The physical wardrobe (GDD: "A physical wardrobe stands inside the barn ... and another in the lobby map.
    /// Interact (E) to open it."). Spawned locally on every peer at each <see cref="MarkerRole.Wardrobe"/> marker
    /// (it holds no replicated state; the loadout itself is replicated by <see cref="FarmerLoadoutSync"/>).
    /// Built from primitives: a cabinet with a solid collider plus an Interactable-layer trigger in front.
    /// </summary>
    public sealed class WardrobeInteractable : MonoBehaviour
    {
        /// <summary>Interaction range from the wardrobe front (m).</summary>
        public const float Range = 2.2f;

        /// <summary>Default binding of the open action.</summary>
        public const string DefaultBinding = "<Keyboard>/e";

        InputAction _open;
        TextMesh _prompt;

        /// <summary>True when the local farmer stands close enough to open it.</summary>
        public bool LocalInRange { get; private set; }

        /// <summary>Builds a wardrobe at <paramref name="position"/> facing <paramref name="rotation"/>'s forward.</summary>
        public static WardrobeInteractable Build(Vector3 position, Quaternion rotation, Transform parent = null)
        {
            var go = new GameObject("Wardrobe");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            Material mat = null;
            foreach (var s in FarmerLoadoutSync.Active) if (s != null && s.Visual.Catalog != null) { mat = s.Visual.Catalog.baseMaterial; break; }

            Color wood = new Color(0.55f, 0.36f, 0.22f), dark = new Color(0.36f, 0.23f, 0.14f), brass = new Color(0.96f, 0.77f, 0.26f), straw = new Color(0.91f, 0.78f, 0.45f);
            void Part(PartShape s, Vector3 p, Vector3 size, Color c, Vector3 e = default) =>
                AccessoryBuilder.BuildPart(new CosmeticPart { shape = s, position = p, size = size, color = c, euler = e }, go.transform, mat, default);
            Part(PartShape.Cube, new Vector3(0, 1.1f, 0), new Vector3(1.3f, 2.2f, 0.65f), wood);
            Part(PartShape.Cube, new Vector3(0, 2.25f, 0), new Vector3(1.45f, 0.12f, 0.75f), dark);
            Part(PartShape.Cube, new Vector3(-0.32f, 1.15f, 0.33f), new Vector3(0.6f, 1.9f, 0.04f), dark);
            Part(PartShape.Cube, new Vector3(0.32f, 1.15f, 0.33f), new Vector3(0.6f, 1.9f, 0.04f), dark);
            Part(PartShape.Sphere, new Vector3(-0.07f, 1.15f, 0.37f), new Vector3(0.07f, 0.07f, 0.07f), brass);
            Part(PartShape.Sphere, new Vector3(0.07f, 1.15f, 0.37f), new Vector3(0.07f, 0.07f, 0.07f), brass);
            Part(PartShape.Cylinder, new Vector3(0.1f, 2.33f, 0), new Vector3(0.75f, 0.03f, 0.75f), straw, new Vector3(0, 0, 8));
            Part(PartShape.Cylinder, new Vector3(0.1f, 2.4f, 0), new Vector3(0.34f, 0.14f, 0.34f), straw, new Vector3(0, 0, 8));
            Part(PartShape.Cube, new Vector3(0.2f, 1.75f, 0.36f), new Vector3(0.25f, 0.4f, 0.02f), new Color(0.6f, 0.75f, 0.85f));

            var solid = go.AddComponent<BoxCollider>();
            solid.center = new Vector3(0, 1.1f, 0); solid.size = new Vector3(1.3f, 2.2f, 0.65f);

            var trigger = new GameObject("InteractTrigger");
            trigger.transform.SetParent(go.transform, false);
            int interactable = LayerMask.NameToLayer(GameLayers.Interactable);
            if (interactable >= 0) trigger.layer = interactable;
            var tc = trigger.AddComponent<BoxCollider>();
            tc.isTrigger = true; tc.center = new Vector3(0, 1f, 1.1f); tc.size = new Vector3(2f, 2f, 1.8f);

            var w = go.AddComponent<WardrobeInteractable>();
            var promptGo = new GameObject("Prompt");
            promptGo.transform.SetParent(go.transform, false);
            promptGo.transform.localPosition = new Vector3(0, 2.75f, 0.2f);
            w._prompt = promptGo.AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            w._prompt.font = font;
            promptGo.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            w._prompt.text = "[E] Wardrobe";
            w._prompt.anchor = TextAnchor.MiddleCenter;
            w._prompt.characterSize = 0.05f; w._prompt.fontSize = 64;
            w._prompt.color = new Color(1f, 0.96f, 0.84f);
            promptGo.SetActive(false);
            return w;
        }

        void Awake()
        {
            _open = new InputAction("OpenWardrobe", InputActionType.Button, DefaultBinding);
            _open.performed += _ => TryOpen();
            _open.Enable();
        }

        void OnDestroy() => _open?.Dispose();

        /// <summary>Opens the wardrobe for the local farmer when in range. Returns true when opened.</summary>
        public bool TryOpen()
        {
            if (!LocalInRange || WardrobeUI.IsOpen || InputModal.IsBlocking) return false;
            return WardrobeUI.Open(FarmerLoadoutSync.Local) != null;
        }

        void Update()
        {
            var local = FarmerLoadoutSync.Local;
            Vector3 front = transform.position + transform.forward * 0.9f;
            LocalInRange = local != null && Vector3.Distance(local.transform.position, front) < Range;
            if (_prompt != null)
            {
                bool show = LocalInRange && !WardrobeUI.IsOpen;
                if (_prompt.gameObject.activeSelf != show) _prompt.gameObject.SetActive(show);
                var cam = Camera.main;
                if (show && cam != null) _prompt.transform.rotation = Quaternion.LookRotation(_prompt.transform.position - cam.transform.position);
            }
        }
    }

    /// <summary>
    /// Spawns a <see cref="WardrobeInteractable"/> at every Wardrobe <see cref="LevelMarker"/> in each loaded scene
    /// (Customization owns the Wardrobe marker role per docs/architecture/unity-contracts.md).
    /// </summary>
    public static class WardrobeSpawner
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SpawnAll();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => SpawnAll();

        /// <summary>Spawns wardrobes at markers that do not have one yet. Returns how many were created.</summary>
        public static int SpawnAll()
        {
            int n = 0;
            foreach (var marker in LevelMarker.FindAll(MarkerRole.Wardrobe))
            {
                if (marker.GetComponentInChildren<WardrobeInteractable>() != null) continue;
                WardrobeInteractable.Build(marker.transform.position, marker.transform.rotation, marker.transform);
                n++;
            }
            return n;
        }
    }
}
