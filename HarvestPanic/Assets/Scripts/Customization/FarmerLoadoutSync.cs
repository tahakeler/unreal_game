using System;
using System.Collections.Generic;
using HarvestPanic.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HarvestPanic.Customization
{
    /// <summary>
    /// Replicates one player's <see cref="FarmerLoadout"/> (GDD "Replication &amp; persistence").
    /// Lives on FarmerBody.prefab, which the Player team nests under the player NetworkObject (no NetworkObject of its own).
    /// Flow: owner loads the saved loadout -> <see cref="SubmitLoadoutRpc"/> -> server sanitises (ownership, ranges,
    /// duplicate name-tag colour) -> server-written NetworkVariable -> every peer (incl. late joiners) applies it.
    /// The owner re-sends on every spawn and every scene load so loadouts survive scene changes.
    /// Offline (not spawned, e.g. test scenes) the saved loadout is applied directly.
    /// </summary>
    [RequireComponent(typeof(FarmerVisual))]
    public sealed class FarmerLoadoutSync : NetworkBehaviour
    {
        static readonly List<FarmerLoadoutSync> ActiveList = new List<FarmerLoadoutSync>();

        /// <summary>Every spawned (or offline-local) farmer.</summary>
        public static IReadOnlyList<FarmerLoadoutSync> Active => ActiveList;

        /// <summary>The local player's farmer, or null.</summary>
        public static FarmerLoadoutSync Local { get; private set; }

        /// <summary>Raised when <see cref="Local"/> changes.</summary>
        public static event Action<FarmerLoadoutSync> LocalChanged;

        [SerializeField, Tooltip("Offline only: apply the saved PlayerPrefs loadout on Start (local test farmer).")]
        bool applySavedWhenOffline = true;

        readonly NetworkVariable<FarmerLoadout> _loadout = new NetworkVariable<FarmerLoadout>(
            FarmerLoadout.Default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        FarmerVisual _visual;

        /// <summary>The visual this drives.</summary>
        public FarmerVisual Visual => _visual != null ? _visual : _visual = GetComponent<FarmerVisual>();

        /// <summary>Current loadout (replicated value when spawned, applied value offline).</summary>
        public FarmerLoadout Loadout => IsSpawned ? _loadout.Value : Visual.Current;

        /// <summary>True when this is the local player's farmer.</summary>
        public bool IsLocalFarmer => IsSpawned ? IsOwner : Visual.OfflineIsLocal;

        /// <summary>Raised on every peer after a new loadout is applied.</summary>
        public event Action<FarmerLoadout> LoadoutChanged;

        void Awake() => _visual = GetComponent<FarmerVisual>();

        void Start()
        {
            if (IsSpawned || Visual.IsPreview) return;
            if (Visual.OfflineIsLocal)
            {
                SetLocal(this);
                if (applySavedWhenOffline) Visual.Apply(LoadoutStorage.LoadOrCreate(Visual.Catalog));
            }
            else if (!Visual.HasApplied) Visual.Apply(Visual.Current);
            Visual.ApplyFirstPersonVisibility();
            RefreshName();
        }

        void OnEnable() { if (!ActiveList.Contains(this)) ActiveList.Add(this); }

        void OnDisable()
        {
            ActiveList.Remove(this);
            if (Local == this) SetLocal(null);
        }

        /// <inheritdoc/>
        public override void OnNetworkSpawn()
        {
            _loadout.OnValueChanged += OnLoadoutChanged;
            Visual.Apply(_loadout.Value);
            Visual.ApplyFirstPersonVisibility();
            RefreshName();
            if (Services.TryGet(out ISessionService session)) session.OnSessionChanged += RefreshName;
            if (IsOwner)
            {
                SetLocal(this);
                SendSaved();
                SceneManager.sceneLoaded += OnSceneLoaded;
            }
        }

        /// <inheritdoc/>
        public override void OnNetworkDespawn()
        {
            _loadout.OnValueChanged -= OnLoadoutChanged;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Services.TryGet(out ISessionService session)) session.OnSessionChanged -= RefreshName;
            if (Local == this) SetLocal(null);
        }

        /// <inheritdoc/>
        public override void OnGainedOwnership()
        {
            if (!IsOwner) return;
            SetLocal(this);
            Visual.ApplyFirstPersonVisibility();
            SendSaved();
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (IsSpawned && IsOwner) SendSaved();
        }

        void SendSaved() => SubmitLoadoutRpc(LoadoutStorage.LoadOrCreate(Visual.Catalog));

        /// <summary>
        /// Local player confirms a loadout (wardrobe Confirm): saves it and sends it to the server
        /// (offline: applies immediately). Returns false when called on a non-local farmer.
        /// </summary>
        public bool RequestLoadout(FarmerLoadout loadout)
        {
            if (!IsLocalFarmer) return false;
            LoadoutStorage.Save(loadout);
            if (IsSpawned) SubmitLoadoutRpc(loadout);
            else
            {
                var clean = LoadoutValidator.Sanitize(loadout, Visual.Catalog, null, out _);
                Visual.Apply(clean, poof: true);
                LoadoutChanged?.Invoke(clean);
            }
            return true;
        }

        /// <summary>Owner -> server: proposed loadout. The server validates and replicates.</summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void SubmitLoadoutRpc(FarmerLoadout requested, RpcParams rpcParams = default)
        {
            var clean = LoadoutValidator.Sanitize(requested, Visual.Catalog, null, out string report);
            if (report.Length > 0)
                Debug.LogWarning($"[Customization] Client {OwnerClientId} sent an invalid/unowned loadout; replaced: {report}");

            var taken = new HashSet<byte>();
            foreach (var other in ActiveList)
                if (other != this && other.IsSpawned && other.OwnerClientId < OwnerClientId) taken.Add(other._loadout.Value.NameTag);
            clean.NameTag = LoadoutValidator.ResolveNameTag(clean.NameTag, Visual.Catalog.nameTags.Count, taken);

            bool changed = !clean.Equals(_loadout.Value);
            _loadout.Value = clean;
            if (!changed) Visual.Apply(clean); // OnValueChanged does not fire for equal values; still make sure it is shown
        }

        void OnLoadoutChanged(FarmerLoadout previous, FarmerLoadout current)
        {
            Visual.Apply(current, poof: Visual.HasApplied && !previous.Equals(current));
            LoadoutChanged?.Invoke(current);
        }

        /// <summary>Updates the overhead name from the session player list (fallback "Farmer N").</summary>
        public void RefreshName()
        {
            var tag = Visual.NameTag;
            if (tag == null) return;
            ulong id = IsSpawned ? OwnerClientId : Visual.ClientId;
            string name = null;
            if (Services.TryGet(out ISessionService session) && session.Players != null)
                foreach (var p in session.Players) if (p.ClientId == id) { name = p.Name; break; }
            tag.SetName(string.IsNullOrWhiteSpace(name) ? $"Farmer {id + 1}" : name);
        }

        static void SetLocal(FarmerLoadoutSync sync)
        {
            if (Local == sync) return;
            Local = sync;
            LocalChanged?.Invoke(sync);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            ActiveList.Clear();
            Local = null;
            LocalChanged = null;
        }
    }
}
