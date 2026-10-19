using System;
using System.Collections.Generic;
using UnityEngine;

namespace HarvestPanic.Core
{
    /// <summary>
    /// Tiny runtime service registry so systems in assemblies that do not reference each other
    /// can find one another through <c>HarvestPanic.Core</c> interfaces. Main-thread only.
    /// The registry is cleared on every entry to Play mode (domain-reload safe).
    /// </summary>
    /// <example><code>
    /// Services.Register&lt;INoiseService&gt;(this);                 // in OnEnable
    /// if (Services.TryGet(out INoiseService noise)) noise.Emit(...);
    /// Services.WhenAvailable&lt;IEconomyService&gt;(e =&gt; _economy = e); // order-independent
    /// </code></example>
    public static class Services
    {
        static readonly Dictionary<Type, object> Map = new Dictionary<Type, object>();
        static readonly Dictionary<Type, List<Delegate>> Pending = new Dictionary<Type, List<Delegate>>();

        /// <summary>Registers <paramref name="instance"/> as the provider of <typeparamref name="T"/>; replaces any previous one.</summary>
        public static void Register<T>(T instance) where T : class
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            Map[typeof(T)] = instance;
            if (!Pending.TryGetValue(typeof(T), out var waiting)) return;
            Pending.Remove(typeof(T));
            foreach (var d in waiting) ((Action<T>)d)(instance);
        }

        /// <summary>Removes the provider only if it is still <paramref name="instance"/> (safe to call from OnDisable).</summary>
        public static void Unregister<T>(T instance) where T : class
        {
            if (Map.TryGetValue(typeof(T), out var current) && ReferenceEquals(current, instance))
                Map.Remove(typeof(T));
        }

        /// <summary>Returns the provider of <typeparamref name="T"/> or throws when none is registered.</summary>
        public static T Get<T>() where T : class
        {
            if (TryGet(out T service)) return service;
            throw new InvalidOperationException($"No service registered for {typeof(T).Name}. Is its provider in the scene?");
        }

        /// <summary>Returns true and the provider when registered and (for Unity objects) not destroyed.</summary>
        public static bool TryGet<T>(out T service) where T : class
        {
            service = null;
            if (!Map.TryGetValue(typeof(T), out var obj)) return false;
            if (obj is UnityEngine.Object uo && uo == null) { Map.Remove(typeof(T)); return false; }
            service = (T)obj;
            return true;
        }

        /// <summary>Invokes <paramref name="callback"/> now if a provider exists, otherwise once when it registers.</summary>
        public static void WhenAvailable<T>(Action<T> callback) where T : class
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            if (TryGet(out T service)) { callback(service); return; }
            if (!Pending.TryGetValue(typeof(T), out var list)) Pending[typeof(T)] = list = new List<Delegate>();
            list.Add(callback);
        }

        /// <summary>Drops every registration and pending callback (session end / tests).</summary>
        public static void Clear()
        {
            Map.Clear();
            Pending.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => Clear();
    }
}
