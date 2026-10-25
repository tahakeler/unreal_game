using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HarvestPanic.Core
{
    /// <summary>
    /// Mutes the master listener in -batchmode runs (test runners, headless players) so automated runs are silent.
    /// Opt out with the <c>-hpAudible</c> command-line argument.
    /// </summary>
    public static class BatchModeSilence
    {
        public const string AudibleArg = "-hpAudible";

        /// <summary>True when this process must stay silent; settings code must not raise AudioListener.volume.</summary>
        public static bool Active { get; private set; }

        public static bool ShouldSilence(bool isBatchMode, string[] args)
        {
            if (!isBatchMode) return false;
            if (args != null)
                foreach (var a in args)
                    if (string.Equals(a, AudibleArg, StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            Active = ShouldSilence(Application.isBatchMode, Environment.GetCommandLineArgs());
            if (!Active) return;
            Apply();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene s, LoadSceneMode m) => Apply();

        /// <summary>Re-mutes the listener if silence is active; call after anything that may change the volume.</summary>
        public static void Apply()
        {
            if (Active) AudioListener.volume = 0f;
        }
    }
}
