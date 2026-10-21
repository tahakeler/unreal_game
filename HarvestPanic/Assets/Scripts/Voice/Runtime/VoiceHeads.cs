using System.Collections.Generic;
using HarvestPanic.Core;
using Unity.Netcode;
using UnityEngine;

namespace HarvestPanic.Voice
{
    /// <summary>Finds the transform a player's voice comes from (the replicated head). Null when unknown.</summary>
    public interface IVoiceHeadLocator
    {
        Transform FindHead(NetworkManager nm, ulong clientId);
        /// <summary>Drops cached transforms (session end).</summary>
        void Clear();
    }

    /// <summary>
    /// Default locator: the client's NGO player object, child "HeadPivot" (Player prefab), else
    /// <see cref="IPlayerHooks.HeadAttachPoint"/>, else the root. Works on host and clients (uses the spawn manager's
    /// player-object table, which NGO keeps on every peer).
    /// </summary>
    public sealed class PlayerHeadLocator : IVoiceHeadLocator
    {
        public const string HeadName = "HeadPivot";
        readonly Dictionary<ulong, Transform> _cache = new Dictionary<ulong, Transform>();

        public Transform FindHead(NetworkManager nm, ulong clientId)
        {
            if (_cache.TryGetValue(clientId, out var t) && t != null) return t;
            _cache.Remove(clientId);
            if (nm == null || nm.SpawnManager == null) return null;
            var list = nm.SpawnManager.GetPlayerNetworkObjects(clientId);
            NetworkObject po = list != null && list.Count > 0 ? list[0] : null;
            if (po == null) return null;
            t = FindDeep(po.transform, HeadName);
            if (t == null)
            {
                var hooks = po.GetComponentInChildren<IPlayerHooks>(true);
                t = hooks != null && hooks.HeadAttachPoint != null ? hooks.HeadAttachPoint : po.transform;
            }
            _cache[clientId] = t;
            return t;
        }

        public void Clear() => _cache.Clear();

        public static Transform FindDeep(Transform t, string name)
        {
            if (t == null) return null;
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++) { var r = FindDeep(t.GetChild(i), name); if (r != null) return r; }
            return null;
        }
    }

    /// <summary>Explicit client id -> head map (tests, bots, dev scenes without player prefabs).</summary>
    public sealed class MapHeadLocator : IVoiceHeadLocator
    {
        public readonly Dictionary<ulong, Transform> Heads = new Dictionary<ulong, Transform>();
        public Transform FindHead(NetworkManager nm, ulong clientId) => Heads.TryGetValue(clientId, out var t) && t != null ? t : null;
        public void Clear() { }
    }
}
