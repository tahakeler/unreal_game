using System;
using UnityEngine.SceneManagement;

namespace HarvestPanic.UI
{
    /// <summary>
    /// Frontend scene changes (MainMenu, Lobby). In the editor a scene missing from Build Settings is loaded by path so
    /// PlayMode tests work before the Networking team adds them. <see cref="Requested"/> lets tests observe requests.
    /// </summary>
    public static class SceneLoader
    {
        public const string MainMenu = "MainMenu";
        public const string Lobby = "Lobby";

        public static string LastRequested { get; private set; }
        public static event Action<string> Requested;

        public static void Load(string sceneName)
        {
            LastRequested = sceneName;
            Requested?.Invoke(sceneName);
#if UNITY_EDITOR
            if (!UnityEngine.Application.CanStreamedLevelBeLoaded(sceneName))
            {
                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                    $"Assets/Scenes/{sceneName}.unity", new LoadSceneParameters(LoadSceneMode.Single));
                return;
            }
#endif
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }
    }
}
