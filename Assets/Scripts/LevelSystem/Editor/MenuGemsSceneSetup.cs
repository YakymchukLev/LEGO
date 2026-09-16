#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using LegoPuzzle.Runtime;

namespace LegoPuzzle.Editor
{
    [InitializeOnLoad]
    public static class MenuGemsSceneSetup
    {
        static MenuGemsSceneSetup()
        {
            EditorApplication.delayCall += CheckAndSetupMenuGems;
        }

        [MenuItem("LEGO/Setup Menu Gems UI in Scene")]
        public static void SetupMenuGems()
        {
            CheckAndSetupMenuGems();
        }

        private static void CheckAndSetupMenuGems()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;

            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (activeScene == null || !activeScene.isLoaded) return;

            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null) return;

            // Only setup in Menu scenes or scenes with MenuHeartsUI / MenuCoinsUI
            bool isMenuScene = activeScene.name.IndexOf("Menu", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                               Object.FindAnyObjectByType<MenuHeartsUI>() != null ||
                               Object.FindAnyObjectByType<MenuCoinsUI>() != null;
            if (!isMenuScene) return;

            // 1. Ensure [GemManager] in scene
            GemManager gemManager = Object.FindAnyObjectByType<GemManager>();
            if (gemManager == null)
            {
                GameObject gmObj = new GameObject("[GemManager]");
                gmObj.AddComponent<GemManager>();
                Undo.RegisterCreatedObjectUndo(gmObj, "Create GemManager");
            }

            // 2. Ensure MenuGemsUI component on Canvas
            MenuGemsUI gemsUI = canvas.GetComponent<MenuGemsUI>();
            if (gemsUI == null)
            {
                gemsUI = Undo.AddComponent<MenuGemsUI>(canvas.gameObject);
            }

            // 3. Ensure UI Hierarchy
            gemsUI.EnsureUIHierarchy();
            gemsUI.UpdateUI(false);

            EditorUtility.SetDirty(canvas.gameObject);
            if (!Application.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(activeScene);
            }
        }
    }
}
#endif
