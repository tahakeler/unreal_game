using System.Collections.Generic;
using HarvestPanic.Core;
using Unity.Netcode;
using UnityEngine;

namespace HarvestPanic.AI.Crops
{
    /// <summary>
    /// A cursed-crop field placed in the level (AI-owned scene object, no new marker role). Server/offline: plants
    /// <see cref="count"/> crops of <see cref="goodsId"/> on a grid around itself once the crop's spawn rule is unlocked
    /// (<see cref="CursedCropTuning.UnlockDay"/>, read from <see cref="IDayModifierService.DayIndex"/>), and tops the field
    /// back up every Morning (<see cref="IDayClockService"/>). With no day services (sandbox, tests) the field is planted
    /// at once. The crop prefab is the goods definition's <c>WorldPrefab</c> (Economy data, read through Core's catalog).
    /// Clients never spawn; NGO replicates.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CursedCropField : MonoBehaviour
    {
        [SerializeField] string goodsId = "Crop.Mandrake";
        [SerializeField, Min(1)] int count = 6;
        [SerializeField, Min(0.3f), Tooltip("Grid spacing (m)")] float spacing = 1.6f;
        [SerializeField, Min(1), Tooltip("Crops per row")] int columns = 3;
        [SerializeField, Min(0), Tooltip("How deep each crop is planted (m)")] float sinkDepth;
        [SerializeField, Tooltip("Ignore the unlock day (dev / screenshots)")] bool ignoreUnlock;

        /// <summary>All fields (server/offline), for tests and tools.</summary>
        public static readonly List<CursedCropField> All = new List<CursedCropField>();

        readonly List<CursedCropGoods> _crops = new List<CursedCropGoods>();
        DayClockPhase _lastPhase = DayClockPhase.None;
        bool _started;

        public string GoodsId => goodsId;
        public int Count => count;
        /// <summary>Crops this field has spawned that still exist.</summary>
        public IReadOnlyList<CursedCropGoods> Crops { get { Prune(); return _crops; } }
        /// <summary>Crops still in the soil.</summary>
        public int PlantedCount { get { Prune(); int n = 0; foreach (var c in _crops) if (c.Planted) n++; return n; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => All.Clear();

        public void Configure(string id, int n, float gridSpacing, int perRow, float depth, bool alwaysUnlocked = false)
        {
            goodsId = id; count = Mathf.Max(1, n); spacing = gridSpacing; columns = Mathf.Max(1, perRow); sinkDepth = depth; ignoreUnlock = alwaysUnlocked;
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Update()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && (!nm.IsListening || !nm.IsServer)) return; // clients never spawn; servers wait until listening
            bool networked = nm != null;
            if (networked && !_started && Time.timeSinceLevelLoad < 1f) return; // let NGO finish the scene load for clients
            DayClockPhase phase = Services.TryGet(out IDayClockService clock) ? clock.Phase : DayClockPhase.None;
            if (!_started) { _started = true; _lastPhase = phase; TopUp(networked); return; }
            if (phase != _lastPhase)
            {
                _lastPhase = phase;
                if (phase == DayClockPhase.Morning) TopUp(networked);
            }
        }

        /// <summary>True when the field may be planted today.</summary>
        public bool IsUnlocked()
        {
            if (ignoreUnlock || !Services.TryGet(out IDayModifierService mods)) return true;
            return CursedCropRules.IsUnlocked(mods.DayIndex, CursedCropTuning.Default.UnlockDay(goodsId));
        }

        /// <summary>Server/offline: plants crops until <see cref="count"/> are in the soil (if unlocked). Returns how many were planted.</summary>
        public int TopUp(bool networked)
        {
            if (!IsUnlocked()) return 0;
            if (!Content.Goods.TryGet(goodsId, out var def) || def.WorldPrefab == null)
            {
                Debug.LogWarning($"[CursedCropField] No goods definition / WorldPrefab for '{goodsId}'", this);
                return 0;
            }
            int add = CursedCropRules.TopUp(count, PlantedCount);
            int planted = 0;
            for (int slot = 0; slot < count && planted < add; slot++)
            {
                Vector3 p = SlotPosition(slot);
                if (Occupied(p)) continue;
                var go = Instantiate(def.WorldPrefab, p, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                var crop = go.GetComponent<CursedCropGoods>();
                if (crop != null) { crop.SetPlanted(true); _crops.Add(crop); }
                if (go.TryGetComponent(out TumbleMelon melon)) melon.Home = transform.position;
                if (networked && go.TryGetComponent(out NetworkObject no)) no.Spawn(true);
                planted++;
            }
            return planted;
        }

        bool Occupied(Vector3 p)
        {
            foreach (var c in _crops)
                if (c != null && c.Planted && (c.transform.position - p).sqrMagnitude < spacing * spacing * 0.2f) return true;
            return false;
        }

        /// <summary>World position of grid slot <paramref name="slot"/> (ground hit minus the planting depth).</summary>
        public Vector3 SlotPosition(int slot)
        {
            int rows = Mathf.CeilToInt(count / (float)columns);
            int r = slot / columns, c = slot % columns;
            Vector3 local = new Vector3((c - (columns - 1) * 0.5f) * spacing, 0f, (r - (rows - 1) * 0.5f) * spacing);
            Vector3 p = transform.TransformPoint(local);
            int mask = ~LayerMask.GetMask(GameLayers.Player, GameLayers.Critter, GameLayers.Carryable, GameLayers.Water);
            if (Physics.Raycast(p + Vector3.up * 10f, Vector3.down, out var hit, 30f, mask, QueryTriggerInteraction.Ignore)) p = hit.point;
            return p + Vector3.down * sinkDepth;
        }

        void Prune() { for (int i = _crops.Count - 1; i >= 0; i--) if (_crops[i] == null) _crops.RemoveAt(i); }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.6f, 0.2f, 0.8f, 0.6f);
            for (int i = 0; i < count; i++) Gizmos.DrawWireSphere(transform.TransformPoint(new Vector3((i % columns - (columns - 1) * 0.5f) * spacing, 0f, (i / columns) * spacing)), 0.25f);
        }
#endif
    }
}
