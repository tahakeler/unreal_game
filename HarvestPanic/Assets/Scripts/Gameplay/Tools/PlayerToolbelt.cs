using System;
using HarvestPanic.Core;
using Unity.Netcode;
using UnityEngine;

namespace HarvestPanic.Gameplay
{
    /// <summary>Toolbelt slots: bare hands plus one pocket per tool.</summary>
    public enum ToolSlot : byte { Hands = 0, Net = 1, Bucket = 2, Lasso = 3, Whistle = 4 }

    /// <summary>
    /// Tool hotbar (design/gdd/farm-tools.md rule 1, GDD 4.8 "Tool slot swap"): Hands / Net / Feed bucket / Lasso /
    /// Whistle. Keys 1-4 and Q, the mouse wheel, or LB/RB (PlayerControls ToolSlot1-5 / ToolNext / ToolPrev /
    /// ToolScroll, rebindable). The server spawns a personal set of tools for each player, holstered (hidden, no
    /// collision). Selecting a slot puts the held pocket tool away and, after the 0.4 s equip time, takes the chosen
    /// one out into the right hand through <see cref="PlayerCarry.ServerGrab"/>. A pocket tool released any way other
    /// than a throw (E, ragdoll, lasso auto-grab) goes back into its pocket; a thrown tool (or a lasso tied to a post)
    /// stays in the world until its owner picks it up again.
    /// Deviation (docs/qa/tools-progress.md): the GDD's 2 tool slots + barn racks became a 4-tool belt because the
    /// Muddlebury slice has no tool racks.
    /// </summary>
    [DefaultExecutionOrder(-35)]
    public sealed class PlayerToolbelt : NetworkBehaviour
    {
        /// <summary>Hotbar entries including Hands.</summary>
        public const int SlotCount = 5;
        static readonly string[] Names = { "Hands", "Net", "Bucket", "Lasso", "Whistle" };

        [SerializeField] ToolTuning tuning;
        [SerializeField] GameObject netPrefab, bucketPrefab, lassoPrefab, whistlePrefab;
        [SerializeField, Tooltip("Server spawns a personal tool set when the player spawns")] bool spawnPersonalTools = true;

        readonly NetworkVariable<ulong> _net = new NetworkVariable<ulong>(PlayerCarry.NoObject);
        readonly NetworkVariable<ulong> _bucket = new NetworkVariable<ulong>(PlayerCarry.NoObject);
        readonly NetworkVariable<ulong> _lasso = new NetworkVariable<ulong>(PlayerCarry.NoObject);
        readonly NetworkVariable<ulong> _whistle = new NetworkVariable<ulong>(PlayerCarry.NoObject);
        readonly NetworkVariable<byte> _selected = new NetworkVariable<byte>(0);

        PlayerCharacter _pc;
        PlayerCarry _carry;
        int _pending = -1;
        float _equipAt;

        /// <summary>Raised on every peer when the selected slot changes.</summary>
        public event Action<int> SelectionChanged;

        /// <summary>Selected hotbar slot (0 = hands).</summary>
        public int Selected => _selected.Value;
        /// <summary>Server: a tool is being taken out (equip delay running).</summary>
        public bool Equipping => _pending > 0;
        /// <summary>Display name for a slot.</summary>
        public static string SlotName(int slot) => slot >= 0 && slot < Names.Length ? Names[slot] : "?";
        /// <summary>Tuning in use.</summary>
        public ToolTuning Tuning => tuning;

        /// <summary>Next slot when cycling by <paramref name="step"/> (wraps, Hands included).</summary>
        public static int Cycle(int current, int step) => ((current + step) % SlotCount + SlotCount) % SlotCount;

        void Awake()
        {
            _pc = GetComponent<PlayerCharacter>();
            _carry = GetComponent<PlayerCarry>();
            if (tuning == null) tuning = ScriptableObject.CreateInstance<ToolTuning>();
        }

        /// <summary>Editor/tests: assigns prefabs and tuning.</summary>
        public void Configure(ToolTuning t, GameObject net, GameObject bucket, GameObject lasso, GameObject whistle)
        {
            tuning = t; netPrefab = net; bucketPrefab = bucket; lassoPrefab = lasso; whistlePrefab = whistle;
        }

        NetworkVariable<ulong> Pocket(int slot)
        {
            switch ((ToolSlot)slot)
            {
                case ToolSlot.Net: return _net;
                case ToolSlot.Bucket: return _bucket;
                case ToolSlot.Lasso: return _lasso;
                case ToolSlot.Whistle: return _whistle;
                default: return null;
            }
        }

        /// <summary>NetworkObjectId in a pocket (<see cref="PlayerCarry.NoObject"/> when empty).</summary>
        public ulong PocketId(int slot) => Pocket(slot)?.Value ?? PlayerCarry.NoObject;

        /// <summary>Pocket slot that owns <paramref name="objectId"/>, or -1.</summary>
        public int SlotOf(ulong objectId)
        {
            if (objectId == PlayerCarry.NoObject) return -1;
            for (int s = 1; s < SlotCount; s++) if (PocketId(s) == objectId) return s;
            return -1;
        }

        /// <summary>True when the held object is one of this player's pocket tools.</summary>
        public bool HeldIsPocketTool => _carry != null && _carry.IsHoldingReplicated && SlotOf(_carry.HeldId) > 0;

        /// <summary>The spawned tool in a pocket (any peer), or null.</summary>
        public CarryableProp PocketTool(int slot)
        {
            ulong id = PocketId(slot);
            if (id == PlayerCarry.NoObject || NetworkManager == null || NetworkManager.SpawnManager == null) return null;
            return NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(id, out var no) && no != null ? no.GetComponent<CarryableProp>() : null;
        }

        /// <summary>Typed pocket tool (any peer), or null.</summary>
        public T PocketTool<T>(ToolSlot slot) where T : CarryableProp => PocketTool((int)slot) as T;

        /// <summary>True when the slot's tool is on the belt or in this player's hand (false: thrown, tied, lost).</summary>
        public bool SlotAvailable(int slot)
        {
            if (slot == 0) return true;
            var t = PocketTool(slot);
            if (t == null) return false;
            return t.HolsteredBy == OwnerClientId || (_carry != null && _carry.HeldId == t.NetworkObjectId);
        }

        public override void OnNetworkSpawn()
        {
            _selected.OnValueChanged += OnSelected;
            if (!IsServer || !spawnPersonalTools) return;
            SpawnInto(_net, netPrefab);
            SpawnInto(_bucket, bucketPrefab);
            SpawnInto(_lasso, lassoPrefab);
            SpawnInto(_whistle, whistlePrefab);
        }

        public override void OnNetworkDespawn()
        {
            _selected.OnValueChanged -= OnSelected;
            if (!IsServer) return;
            for (int s = 1; s < SlotCount; s++)
            {
                var t = PocketTool(s);
                if (t != null && t.IsSpawned && t.IsHolstered) t.NetworkObject.Despawn(true);
            }
        }

        void OnSelected(byte prev, byte next) => SelectionChanged?.Invoke(next);

        void SpawnInto(NetworkVariable<ulong> pocket, GameObject prefab)
        {
            if (prefab == null) return;
            // Parked below the player, hidden; it never needs to move until it is taken out (no per-frame sync cost).
            var no = NetworkObject.InstantiateAndSpawn(prefab, NetworkManager, position: transform.position + Vector3.up * 0.5f, rotation: transform.rotation);
            var prop = no.GetComponent<CarryableProp>();
            if (prop != null) prop.ServerSetHolstered(OwnerClientId, transform);
            pocket.Value = no.NetworkObjectId;
        }

        // ---- Owner input -----------------------------------------------------------------------------------------

        void Update()
        {
            if (IsSpawned && IsServer) ServerTick();
            if (!IsSpawned || !IsOwner || _pc == null || _pc.Input == null || InputModal.IsBlocking) return;
            if (!_pc.HasControl) return;
            int want = _pc.Input.ToolSlotPressed;
            int step = _pc.Input.ToolCycle;
            if (want < 0 && step != 0) want = Cycle(Selected, step);
            if (want >= 0 && want != Selected) RequestSelect(want);
        }

        /// <summary>Owner: select a hotbar slot (tests and bots call this directly).</summary>
        public void RequestSelect(int slot)
        {
            if (IsSpawned && IsOwner) SelectRpc((byte)Mathf.Clamp(slot, 0, SlotCount - 1));
        }

        [Rpc(SendTo.Server)]
        void SelectRpc(byte slot) => ServerSelect(slot);

        /// <summary>
        /// Server: puts the held pocket tool away and starts taking <paramref name="slot"/> out. Refused while the hands
        /// hold something that is not a pocket tool (drop it first).
        /// </summary>
        public bool ServerSelect(int slot)
        {
            if (!IsServer) return false;
            slot = Mathf.Clamp(slot, 0, SlotCount - 1);
            var body = _carry.ServerBody;
            if (body != null)
            {
                if (body.NetObject != null && SlotOf(body.NetObject.NetworkObjectId) > 0) _carry.ServerRelease(HandlingMode.Dropped); // holsters via ServerOnReleased
                else if (slot != 0) return false;
            }
            _selected.Value = (byte)slot;
            _pending = slot == 0 ? -1 : slot;
            _equipAt = Time.time + tuning.EquipSeconds;
            return true;
        }

        void ServerTick()
        {
            if (_pending <= 0 || Time.time < _equipAt) return;
            int slot = _pending;
            _pending = -1;
            if (_carry.ServerBody != null || !_pc.HasControlServer) return;
            var tool = PocketTool(slot);
            if (tool == null || tool.HolsteredBy != OwnerClientId) { if (_selected.Value == slot) _selected.Value = 0; return; } // thrown, tied or lost
            tool.ServerSetHolstered(PlayerIds.None, null);
            tool.ServerTeleport(_carry.HoldPoint(true), _carry.ToolRotation);
            if (!_carry.ServerGrab(tool.NetworkObject, Vector3.zero, false))
            {
                tool.ServerSetHolstered(OwnerClientId, transform);
                if (_selected.Value == slot) _selected.Value = 0;
            }
        }

        /// <summary>
        /// Server (from <see cref="PlayerCarry.ServerRelease"/>): a pocket tool let go other than by a throw goes back
        /// into its pocket and the hotbar returns to Hands. A lasso that was just tied to a post stays where it is.
        /// </summary>
        internal void ServerOnReleased(CarryBody body, HandlingMode mode)
        {
            if (!IsServer || body == null || body.NetObject == null || mode == HandlingMode.Thrown) return;
            int slot = SlotOf(body.NetObject.NetworkObjectId);
            if (slot <= 0) return;
            var prop = body.NetObject.GetComponent<CarryableProp>();
            if (prop == null || !prop.IsSpawned) return;
            if (!(prop is IStaysOutWhenReleased stay && stay.StayOut)) prop.ServerSetHolstered(OwnerClientId, transform);
            if (_selected.Value == slot) _selected.Value = 0;
        }

        /// <summary>Server: a thrown pocket tool leaves the hand; the hotbar shows Hands.</summary>
        internal void ServerOnThrown(CarryBody body)
        {
            if (!IsServer || body == null || body.NetObject == null) return;
            if (SlotOf(body.NetObject.NetworkObjectId) == _selected.Value) _selected.Value = 0;
        }

        /// <summary>Hotbar key text for slot <paramref name="slot"/> (owner's bindings; defaults 1-4, Q).</summary>
        public string SlotKey(int slot) => _pc != null && _pc.Input != null ? _pc.Input.ToolSlotKey(slot) : (slot == SlotCount - 1 ? "Q" : (slot + 1).ToString());

        /// <summary>Hotbar text, e.g. "[1 Hands]   2 Net   3 Bucket   4 Lasso   Q Whistle" ("-" marks a tool not on the belt).</summary>
        public string HotbarText()
        {
            var sb = new System.Text.StringBuilder(64);
            for (int s = 0; s < SlotCount; s++)
            {
                string label = SlotKey(s) + " " + Names[s] + (SlotAvailable(s) ? "" : "-");
                sb.Append(s == Selected ? "[" + label + "]" : label);
                if (s < SlotCount - 1) sb.Append("   ");
            }
            return sb.ToString();
        }
    }

    /// <summary>A pocket tool that stays in the world when released (a lasso tied to a post), instead of being holstered.</summary>
    public interface IStaysOutWhenReleased
    {
        /// <summary>True right now.</summary>
        bool StayOut { get; }
    }
}
