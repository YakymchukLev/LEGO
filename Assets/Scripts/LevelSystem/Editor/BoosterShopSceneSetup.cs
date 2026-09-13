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

            EditorUtility.SetDirty(canvas.gameObject);
        }
    }
}
#endif
