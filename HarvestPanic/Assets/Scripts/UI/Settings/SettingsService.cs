using System;
using UnityEngine;

namespace HarvestPanic.UI
{
    /// <summary>Where settings live between runs. Default is PlayerPrefs; tests inject a memory store.</summary>
    public interface ISettingsStore
    {
        bool TryRead(out string json);
        void Write(string json);
    }

    public sealed class PlayerPrefsSettingsStore : ISettingsStore
    {
        const string Key = "HarvestPanic.Settings.v1";
        public bool TryRead(out string json) { json = PlayerPrefs.GetString(Key, null); return !string.IsNullOrEmpty(json); }
        public void Write(string json) { PlayerPrefs.SetString(Key, json); PlayerPrefs.Save(); }
    }

    public sealed class MemorySettingsStore : ISettingsStore
    {
        public string Json;
        public bool TryRead(out string json) { json = Json; return !string.IsNullOrEmpty(json); }
        public void Write(string json) => Json = json;
    }

    /// <summary>
    /// Single source of truth for options. Other teams subscribe to <see cref="Changed"/> (fires once on load and on every
    /// apply) and read <see cref="Current"/> (a clamped snapshot; never mutate it, call <see cref="Apply"/>).
    /// </summary>
    public static class SettingsService
    {
        static ISettingsStore _store = new PlayerPrefsSettingsStore();
        static GameSettings _current;

        /// <summary>Raised after settings change, with the new clamped snapshot.</summary>
        public static event Action<GameSettings> Changed;

        public static GameSettings Current
        {
            get { if (_current == null) Load(); return _current; }
        }

        /// <summary>Swap the backing store (tests). Reloads from it.</summary>
        public static void UseStore(ISettingsStore store)
        {
            _store = store ?? new PlayerPrefsSettingsStore();
            _current = null;
        }

        /// <summary>Reads from the store (defaults when absent or corrupt) and broadcasts.</summary>
        public static GameSettings Load()
        {
            GameSettings loaded = null;
            if (_store.TryRead(out var json))
            {
                try { loaded = JsonUtility.FromJson<GameSettings>(json); }
                catch (Exception e) { Debug.LogWarning("[Settings] Corrupt settings ignored: " + e.Message); }
            }
            _current = (loaded ?? new GameSettings()).Clamp();
            Changed?.Invoke(_current);
            return _current;
        }

        /// <summary>Clamps, stores, persists and broadcasts <paramref name="next"/>.</summary>
        public static void Apply(GameSettings next, bool persist = true)
        {
            if (next == null) throw new ArgumentNullException(nameof(next));
            _current = next.Copy().Clamp();
            if (persist) Persist();
            Changed?.Invoke(_current);
        }

        /// <summary>Writes the current snapshot to the store (use after a batch of persist:false applies).</summary>
        public static void Persist() => _store.Write(JsonUtility.ToJson(Current));

        public static void ResetToDefaults() => Apply(new GameSettings());
    }
}
