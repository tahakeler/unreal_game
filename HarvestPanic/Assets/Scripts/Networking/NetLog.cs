using System.Collections.Generic;
using UnityEngine;

namespace HarvestPanic.Networking
{
    /// <summary>Prefixed, rate-limited network logging (one message per key per <see cref="Cooldown"/> seconds).</summary>
    public static class NetLog
    {
        public static float Cooldown = 5f;
        static readonly Dictionary<string, float> Last = new Dictionary<string, float>();

        public static void Info(string msg) => Debug.Log("[Net] " + msg);

        public static void Warn(string key, string msg)
        {
            if (!Allow(key)) return;
            Debug.LogWarning("[Net] " + msg);
        }

        public static void Error(string key, string msg)
        {
            if (!Allow(key)) return;
            Debug.LogError("[Net] " + msg);
        }

        static bool Allow(string key)
        {
            float now = Time.realtimeSinceStartup;
            if (Last.TryGetValue(key, out var t) && now - t < Cooldown) return false;
            Last[key] = now;
            return true;
        }
    }
}
