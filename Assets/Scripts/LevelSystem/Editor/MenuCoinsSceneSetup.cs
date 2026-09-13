#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LegoPuzzle.Runtime;

namespace LegoPuzzle.Editor
{
    [InitializeOnLoad]
    public static class MenuCoinsSceneSetup
    {
        static MenuCoinsSceneSetup()
        {
            EditorApplication.delayCall += CheckAndSetupMenuCoins;
        }

        [MenuItem("LEGO/Setup Menu Coins UI in Scene")]
        public static void SetupMenuCoins()
        {
            CheckAndSetupMenuCoins();
        }

        private static void CheckAndSetupMenuCoins()
        {
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (activeScene == null || !activeScene.isLoaded) return;

            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null) return;

            // Only setup in Menu scenes or scenes with MenuHeartsUI
            bool isMenuScene = activeScene.name.IndexOf("Menu", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                               Object.FindAnyObjectByType<MenuHeartsUI>() != null;
            if (!isMenuScene) return;

            // 1. Ensure [CoinManager]
            CoinManager coinManager = Object.FindAnyObjectByType<CoinManager>();
            if (coinManager == null)
            {
                GameObject cmObj = new GameObject("[CoinManager]");
                cmObj.AddComponent<CoinManager>();
                Undo.RegisterCreatedObjectUndo(cmObj, "Create CoinManager");
            }

            // 2. Ensure MenuCoinsUI component on Canvas
            MenuCoinsUI coinsUI = canvas.GetComponent<MenuCoinsUI>();
            if (coinsUI == null)
            {
                coinsUI = Undo.AddComponent<MenuCoinsUI>(canvas.gameObject);
            }

            // 3. Ensure UI Hierarchy
            coinsUI.EnsureUIHierarchy();
            coinsUI.UpdateUI(false);

            EditorUtility.SetDirty(canvas.gameObject);
        }
    }
}
#endif
