using UnityEngine;
using UnityEngine.SceneManagement;

namespace LegoPuzzle.Runtime
{
    /// <summary>
    /// Cold-start routing: first launch and early progress (levels 1–5) open Game directly.
    /// After the player advances past level 5, Menu remains the entry scene.
    /// Only runs once per app session so Pause → Menu still works on early levels.
    /// </summary>
    public static class AppStartupRouter
    {
        public const int EarlyProgressMaxLevelNumber = 5;
        private const int MenuBuildIndex = 0;
        private const int GameBuildIndex = 1;

        private static bool bootHandled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetBootFlag()
        {
            bootHandled = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void HandleColdStart()
        {
            if (bootHandled) return;
            bootHandled = true;

            if (!ShouldStartOnGameScene()) return;

            Scene active = SceneManager.GetActiveScene();
            bool onMenu = active.buildIndex == MenuBuildIndex
                          || string.Equals(active.name, "Menu", System.StringComparison.OrdinalIgnoreCase);

            if (!onMenu) return;

            if (SceneManager.sceneCountInBuildSettings <= GameBuildIndex)
            {
                Debug.LogWarning("[AppStartupRouter] Game scene missing from Build Settings.");
                return;
            }

            Debug.Log("<color=cyan>[AppStartupRouter] Early progress / first launch → loading Game scene.</color>");
            SceneManager.LoadScene(GameBuildIndex);
        }

        /// <summary>
        /// True while the player is still on levels 1–5 (currentLevelIndex 0..4).
        /// </summary>
        public static bool ShouldStartOnGameScene()
        {
            int levelNumber = GetPlayerLevelNumber();
            return levelNumber <= EarlyProgressMaxLevelNumber;
        }

        private static int GetPlayerLevelNumber()
        {
            if (SaveDataService.HasInstance
                && SaveDataService.Instance != null
                && SaveDataService.Instance.Data != null)
            {
                return Mathf.Max(1, SaveDataService.Instance.Data.currentLevelIndex + 1);
            }

            return Mathf.Max(1, PlayerPrefs.GetInt("LEGO_CurrentLevelIndex", 0) + 1);
        }
    }
}
