#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using LegoPuzzle.Runtime;

namespace LegoPuzzle.Editor
{
    [InitializeOnLoad]
    public static class BoosterShopSceneSetup
    {
        static BoosterShopSceneSetup()
        {
            EditorApplication.delayCall += CheckAndSetupBoosterShop;
        }

        [MenuItem("LEGO/Setup Booster Shop Panel in Scene")]
        public static void SetupBoosterShop()
        {
            CheckAndSetupBoosterShop();
        }

        private static void CheckAndSetupBoosterShop()
        {
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (activeScene == null || !activeScene.isLoaded) return;

            // Only setup in scenes with gameplay / LevelLoader / LevelUIController or "Game"
            bool isGameScene = activeScene.name.IndexOf("Game", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                               Object.FindAnyObjectByType<LevelLoader>() != null ||
                               Object.FindAnyObjectByType<LevelUIController>() != null;

            if (!isGameScene) return;

            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null) return;

            BoosterShopPanel existing = canvas.GetComponentInChildren<BoosterShopPanel>(true);
            if (existing == null)
            {
                Transform existingChild = canvas.transform.Find("BoosterShopPanel");
                if (existingChild != null)
                {
                    existing = existingChild.GetComponent<BoosterShopPanel>();
                    if (existing == null)
                    {
                        existing = Undo.AddComponent<BoosterShopPanel>(existingChild.gameObject);
                    }
                }
                else
                {
                    GameObject shopObj = new GameObject("BoosterShopPanel");
                    shopObj.transform.SetParent(canvas.transform, false);
                    existing = shopObj.AddComponent<BoosterShopPanel>();
                    Undo.RegisterCreatedObjectUndo(shopObj, "Create BoosterShopPanel");
                }
            }

            if (existing != null)
            {
                existing.EnsureUIHierarchy();
                existing.gameObject.SetActive(false); // Hidden by default
                EditorUtility.SetDirty(existing.gameObject);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
            }

            SetupBoosterBadges();

            EditorUtility.SetDirty(canvas.gameObject);
        }

        [MenuItem("LEGO/Setup Booster Balance Badges in Scene")]
        public static void SetupBoosterBadges()
        {
            var freeze = Object.FindAnyObjectByType<TimeFreezeBooster>(FindObjectsInactive.Include);
            if (freeze != null)
            {
                freeze.EnsureChargesBadge();
                EditorUtility.SetDirty(freeze.gameObject);
            }

            var hint = Object.FindAnyObjectByType<HintBooster>(FindObjectsInactive.Include);
            if (hint != null)
            {
                hint.EnsureChargesBadge();
                EditorUtility.SetDirty(hint.gameObject);
            }

            var hammer = Object.FindAnyObjectByType<HammerBooster>(FindObjectsInactive.Include);
            if (hammer != null)
            {
                hammer.EnsureChargesBadge();
                EditorUtility.SetDirty(hammer.gameObject);
            }

            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (activeScene != null && activeScene.isLoaded)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
            }
        }
    }
}
#endif
