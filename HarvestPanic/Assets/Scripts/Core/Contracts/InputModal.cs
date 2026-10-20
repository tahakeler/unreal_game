using System;
using System.Collections.Generic;
using UnityEngine;

namespace HarvestPanic.Core
{
    /// <summary>
    /// Stack of owners that want exclusive input (pause menu, crew overlay, wardrobe). UI and Customization
    /// Push/Pop with a stable owner object; the Player controller skips look/move/interact while
    /// <see cref="IsBlocking"/>. Pushing the same owner twice counts once. Main thread only; cleared on entering Play mode.
    /// </summary>
    public static class InputModal
    {
        static readonly List<object> Owners = new List<object>();

        /// <summary>Raised after the blocking state or owner count changes.</summary>
        public static event Action Changed;

        /// <summary>True while at least one owner holds input.</summary>
        public static bool IsBlocking => Owners.Count > 0;

        /// <summary>Number of distinct owners currently holding input.</summary>
        public static int Count => Owners.Count;

        /// <summary>Takes input for <paramref name="owner"/> (no-op if it already holds it).</summary>
        public static void Push(object owner)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (Owners.Contains(owner)) return;
            Owners.Add(owner);
            Changed?.Invoke();
        }

        /// <summary>Releases input held by <paramref name="owner"/> (no-op if it holds none).</summary>
        public static void Pop(object owner)
        {
            if (Owners.Remove(owner)) Changed?.Invoke();
        }

        /// <summary>Drops every owner (tests, session end).</summary>
        public static void Clear()
        {
            bool had = Owners.Count > 0;
            Owners.Clear();
            if (had) Changed?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            Owners.Clear();
            Changed = null;
        }
    }
}
