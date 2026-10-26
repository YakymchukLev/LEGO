#if UNITY_EDITOR
using LegoPuzzle.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace LegoPuzzle.Editor
{
    [InitializeOnLoad]
    public static class LottoBoardSceneSetup
    {
        static LottoBoardSceneSetup()
        {
            EditorApplication.delayCall += EnsureLottoInSceneDelay;
        }

        private static void EnsureLottoInSceneDelay()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying) return;
            EnsureLottoInScene(false);
        }

        [MenuItem("LEGO/Create Lotto Board")]
        public static void CreateLottoInScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying)
            {
                Debug.LogWarning("[LottoBoard] Не можна змінювати сцену під час Play Mode.");
                return;
            }

            EnsureLottoInScene(true);
        }

        private static void EnsureLottoInScene(bool selectCreated)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying) return;

            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!activeScene.isLoaded) return;

            bool isMenuScene = activeScene.name.IndexOf("Menu", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                               Object.FindAnyObjectByType<MenuButtonController>() != null;
            if (!isMenuScene) return;

            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null) return;

            bool created = false;

            Transform buttonTransform = FindNamed(canvas.transform, "LottoButton");
            if (buttonTransform == null)
            {
                var buttonGo = new GameObject("LottoButton", typeof(RectTransform), typeof(Image), typeof(Button));
                Undo.RegisterCreatedObjectUndo(buttonGo, "Create LottoButton");
                buttonGo.transform.SetParent(canvas.transform, false);

                var rect = buttonGo.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0f);
                rect.anchorMax = new Vector2(0.5f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.anchoredPosition = new Vector2(0f, 36f);
                rect.sizeDelta = new Vector2(240f, 76f);

                var image = buttonGo.GetComponent<Image>();
                image.color = new Color(0.20f, 0.55f, 0.95f, 1f);

                var button = buttonGo.GetComponent<Button>();
                button.targetGraphic = image;

                var labelGo = new GameObject("Label", typeof(RectTransform));
                labelGo.transform.SetParent(buttonGo.transform, false);
                var labelRect = labelGo.GetComponent<RectTransform>();
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;
                var label = labelGo.AddComponent<TextMeshProUGUI>();
                label.text = "LOTTO";
                label.fontSize = 32f;
                label.fontStyle = FontStyles.Bold;
                label.alignment = TextAlignmentOptions.Center;
                label.color = Color.white;
                label.raycastTarget = false;

                buttonTransform = buttonGo.transform;
                created = true;
            }

            LottoBoard board = canvas.GetComponentInChildren<LottoBoard>(true);
            if (board == null)
            {
                var boardGo = new GameObject("LottoBoard", typeof(RectTransform), typeof(LottoBoard));
                Undo.RegisterCreatedObjectUndo(boardGo, "Create LottoBoard");
                boardGo.transform.SetParent(canvas.transform, false);
                var rect = boardGo.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                board = boardGo.GetComponent<LottoBoard>();
                created = true;
            }

            if (board != null)
            {
                bool missingVisual = board.transform.Find("LottoVisual") == null;
                board.EnsureBoardExists();
                if (missingVisual || board.ApplyProjectFontToBoard() || board.ApplySlotSprites())
                    created = true;
            }

            if (buttonTransform != null)
            {
                TMP_FontAsset font = MenuSettingsPanel.GetGameFontAsset();
                if (font != null)
                {
                    TMP_Text[] labels = buttonTransform.GetComponentsInChildren<TMP_Text>(true);
                    for (int i = 0; i < labels.Length; i++)
                    {
                        if (labels[i] == null || labels[i].font == font) continue;
                        labels[i].font = font;
                        EditorUtility.SetDirty(labels[i]);
                        created = true;
                    }
                }
            }

            if (selectCreated && board != null)
                Selection.activeGameObject = board.gameObject;

            if (created)
            {
                EditorUtility.SetDirty(canvas.gameObject);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
            }
        }

        private static Transform FindNamed(Transform root, string objectName)
        {
            if (root.name == objectName) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindNamed(root.GetChild(i), objectName);
                if (found != null) return found;
            }

            return null;
        }
    }
}
#endif
