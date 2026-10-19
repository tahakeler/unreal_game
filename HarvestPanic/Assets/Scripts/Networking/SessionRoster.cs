using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HarvestPanic.Core;

namespace HarvestPanic.Networking
{
    /// <summary>
    /// Authoritative player list (host keeps it, clients mirror it from the "HP.Roster" named message).
    /// Ordered host first, then by join order. Slot = index used for PlayerStart markers.
    /// Wire format: [byte version=1][byte count] { [u64 id][string name][bool ready][bool host][byte slot] }.
    /// ~25 B per player, sent only on change (&lt;= ~110 B for 4 players).
    /// </summary>
    public sealed class SessionRoster
    {
        public const byte WireVersion = 1;

        readonly List<PlayerInfo> _players = new List<PlayerInfo>();
        readonly Dictionary<ulong, int> _slots = new Dictionary<ulong, int>();

        /// <summary>Players, host first.</summary>
        public IReadOnlyList<PlayerInfo> Players => _players;
        public int Count => _players.Count;

        public void Clear() { _players.Clear(); _slots.Clear(); }

        public bool Contains(ulong id) => IndexOf(id) >= 0;

        /// <summary>Adds a player in the lowest free slot (0..3). Returns the slot.</summary>
        public int Add(ulong id, string name, bool isHost)
        {
            int existing = IndexOf(id);
            if (existing >= 0) return _slots[id];
            int slot = 0;
            while (_slots.ContainsValue(slot)) slot++;
            _slots[id] = slot;
            var info = new PlayerInfo { ClientId = id, Name = name, IsHost = isHost, IsReady = false };
            if (isHost) _players.Insert(0, info); else _players.Add(info);
            return slot;
        }

        public bool Remove(ulong id)
        {
            int i = IndexOf(id);
            if (i < 0) return false;
            _players.RemoveAt(i);
            _slots.Remove(id);
            return true;
        }

        public bool SetReady(ulong id, bool ready)
        {
            int i = IndexOf(id);
            if (i < 0 || _players[i].IsReady == ready) return false;
            var p = _players[i]; p.IsReady = ready; _players[i] = p;
            return true;
        }

        public void ClearReady()
        {
            for (int i = 0; i < _players.Count; i++) { var p = _players[i]; p.IsReady = false; _players[i] = p; }
        }

        /// <summary>Spawn slot of <paramref name="id"/> or -1.</summary>
        public int SlotOf(ulong id) => _slots.TryGetValue(id, out var s) ? s : -1;

        public IEnumerable<string> Names { get { foreach (var p in _players) yield return p.Name; } }

        int IndexOf(ulong id)
        {
            for (int i = 0; i < _players.Count; i++) if (_players[i].ClientId == id) return i;
            return -1;
        }

        public byte[] Serialize()
        {
            using var ms = new MemoryStream();
            using (var w = new BinaryWriter(ms, Encoding.UTF8, true))
            {
                w.Write(WireVersion);
                w.Write((byte)_players.Count);
                foreach (var p in _players)
                {
                    w.Write(p.ClientId); w.Write(p.Name ?? ""); w.Write(p.IsReady); w.Write(p.IsHost);
                    w.Write((byte)Math.Max(0, SlotOf(p.ClientId)));
                }
            }
            return ms.ToArray();
        }

        /// <summary>Replaces contents from <see cref="Serialize"/> output. False (unchanged) when malformed or a newer major version.</summary>
        public bool TryDeserialize(byte[] data)
        {
            if (data == null || data.Length < 2 || data[0] != WireVersion) return false;
            try
            {
                var players = new List<PlayerInfo>();
                var slots = new Dictionary<ulong, int>();
                using var r = new BinaryReader(new MemoryStream(data), Encoding.UTF8);
                r.ReadByte();
                int n = r.ReadByte();
                if (n > GameInfo.MaxPlayers * 2) return false;
                for (int i = 0; i < n; i++)
                {
                    var p = new PlayerInfo { ClientId = r.ReadUInt64(), Name = PlayerNameSanitizer.Sanitize(r.ReadString()), IsReady = r.ReadBoolean(), IsHost = r.ReadBoolean() };
                    slots[p.ClientId] = r.ReadByte();
                    players.Add(p);
                }
                _players.Clear(); _players.AddRange(players);
                _slots.Clear(); foreach (var kv in slots) _slots[kv.Key] = kv.Value;
                return true;
            }
            catch (Exception) { return false; }
        }
    }
}
