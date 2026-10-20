using System;
using HarvestPanic.Core;
using Unity.Netcode;
using UnityEngine;

namespace HarvestPanic.Gameplay
{
    /// <summary>
    /// Server-side Noise Event provider (design/gdd/proximity-voice.md, "Contract: Noise Event").
    /// Created automatically after the first scene loads and registered as <see cref="INoiseService"/>.
    /// Applies the day-modifier radius params <see cref="DayParams.NoiseEnvMult"/> and, for voice/shout,
    /// <see cref="DayParams.NoiseVoiceRadiusMult"/>. Offline (no NetworkManager listening) counts as server.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class NoiseService : MonoBehaviour, INoiseService
    {
        NoiseBroadcaster _core;

        /// <inheritdoc />
        public event Action<NoiseEvent> OnNoise;

        /// <summary>Log every accepted event (debug view).</summary>
        [SerializeField] bool logEvents;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Services.TryGet(out INoiseService _)) return;
            var go = new GameObject("[NoiseService]");
            DontDestroyOnLoad(go);
            go.AddComponent<NoiseService>();
        }

        void Awake()
        {
            var t = Resources.Load<NoiseTuning>(NoiseTuning.ResourcePath);
            _core = t != null
                ? new NoiseBroadcaster(t.RadiusMin, t.RadiusMax, t.Exponent, t.RateWindow, t.GlobalCap)
                : new NoiseBroadcaster();
            _core.OnNoise += Relay;
        }

        void OnEnable()
        {
            Services.Register<INoiseService>(this);
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDisable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            Services.Unregister<INoiseService>(this);
        }

        // A temporary provider (a test fake, a replay tool) that registers and later unregisters would otherwise
        // leave the registry empty for the rest of the session; this persistent provider reclaims the slot.
        void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (!Services.TryGet(out INoiseService _)) Services.Register<INoiseService>(this);
        }

        void Relay(NoiseEvent e)
        {
            if (logEvents) Debug.Log($"[Noise] {e.Source} L={e.Loudness:0.00} R={e.Radius:0.0} at {e.Location} tags={e.Tags} by {e.BlamedPlayerId}");
            OnNoise?.Invoke(e);
            NoiseCueFeed.Raise(e); // every-peer presentation feed (clients get it via Audio's relay)
        }

        /// <summary>True when this process is allowed to create noise events.</summary>
        public static bool IsAuthority
        {
            get
            {
                var nm = NetworkManager.Singleton;
                return nm == null || !nm.IsListening || nm.IsServer;
            }
        }

        /// <inheritdoc />
        public bool Emit(NoiseSource source, float loudness, Vector3 location, ulong instigatorId, ulong blamedPlayerId, NoiseTag tags)
        {
            if (!IsAuthority) return false;
            float mult = 1f;
            if (Services.TryGet(out IDayModifierService day))
            {
                mult = day.Resolve(DayParams.NoiseEnvMult, 1f);
                if (source == NoiseSource.Voice || source == NoiseSource.Shout) mult *= day.Resolve(DayParams.NoiseVoiceRadiusMult, 1f);
            }
            var nm = NetworkManager.Singleton;
            double now = nm != null && nm.IsListening ? nm.ServerTime.Time : Time.timeAsDouble;
            return _core.TryEmit(source, loudness, location, instigatorId, blamedPlayerId, tags, now, mult, out _);
        }

        /// <summary>Convenience: emit through whichever service is registered (no-op when none).</summary>
        public static bool TryEmit(NoiseSource source, float loudness, Vector3 location, ulong instigatorId, ulong blamedPlayerId, NoiseTag tags = NoiseTag.None) =>
            Services.TryGet(out INoiseService s) && s.Emit(source, loudness, location, instigatorId, blamedPlayerId, tags);
    }
}
