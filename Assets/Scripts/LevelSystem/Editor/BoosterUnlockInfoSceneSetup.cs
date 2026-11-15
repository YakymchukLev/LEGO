#if UNITY_EDITOR
using LegoPuzzle.Runtime;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace LegoPuzzle.Editor
{
    /// <summary>
    /// Creates authored BoosterUnlockInfoPanel once under Canvas in the Game scene.
    /// Runtime only shows/hides and fills text — never generates the hierarchy.
    /// </summary>
    [InitializeOnLoad]
    public static class BoosterUnlockInfoSceneSetup
    {
        private const string PanelName = "BoosterUnlockInfoPanel";

        static BoosterUnlockInfoSceneSetup()
        {
            EditorApplication.delayCall += EnsureInSceneDelay;
            EditorApplication.playModeStateChanged -= OnPlayMode;
            EditorApplication.playModeStateChanged += OnPlayMode;
            UnityEditor.SceneManagement.EditorSceneManager.sceneOpened -= OnSceneOpened;
            UnityEditor.SceneManagement.EditorSceneManager.sceneOpened += OnSceneOpened;
        }

        private static void OnPlayMode(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += EnsureInSceneDelay;
        }

        private static void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, UnityEditor.SceneManagement.OpenSceneMode mode)
        {
            EditorApplication.delayCall += EnsureInSceneDelay;
        }

        private static void EnsureInSceneDelay()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying)
                return;
            EnsureInScene(forceLog: false);
        }

        [MenuItem("LEGO/Create Booster Unlock Info Panel")]
        public static void CreateFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying)
            {
                Debug.LogWarning("[BoosterUnlockInfo] Cannot edit scene during Play Mode.");
                return;
            }

            EnsureInScene(forceLog: true);
        }

        private static void EnsureInScene(bool forceLog)
        {
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!activeScene.isLoaded) return;

            bool isGameScene = activeScene.name.IndexOf("Game", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                               Object.FindAnyObjectByType<LevelLoader>() != null;
            if (!isGameScene) return;

            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                if (forceLog)
                    Debug.LogWarning("[BoosterUnlockInfo] No Canvas found.");
                return;
            }

            Transform existing = FindNamed(canvas.transform, PanelName);
            GameObject panelGo;
            bool created = false;

            if (existing == null)
            {
                panelGo = BuildPanel(canvas.transform);
                Undo.RegisterCreatedObjectUndo(panelGo, "Create BoosterUnlockInfoPanel");
                created = true;
            }
            else
            {
                panelGo = existing.gameObject;
                EnsureChildrenWired(panelGo);
            }

            panelGo.SetActive(false);

            WireTutorialManager(panelGo);

            EditorUtility.SetDirty(panelGo);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);

            if (forceLog || created)
                Debug.Log("<color=green>[BoosterUnlockInfo] Panel ready on Canvas. Save the Game scene — runtime will not generate it.</color>");
        }

        private static void WireTutorialManager(GameObject panelGo)
        {
            BoosterTutorialManager mgr = Object.FindAnyObjectByType<BoosterTutorialManager>(FindObjectsInactive.Include);
            if (mgr == null)
            {
                LevelLoader loader = Object.FindAnyObjectByType<LevelLoader>(FindObjectsInactive.Include);
                if (loader != null)
                {
                    mgr = loader.GetComponent<BoosterTutorialManager>();
                    if (mgr == null)
                        mgr = Undo.AddComponent<BoosterTutorialManager>(loader.gameObject);
                }
            }

            if (mgr == null) return;

            Image icon = panelGo.transform.Find("Icon")?.GetComponent<Image>();
            TMP_Text title = panelGo.transform.Find("Texts/Title")?.GetComponent<TMP_Text>();
            TMP_Text body = panelGo.transform.Find("Texts/Body")?.GetComponent<TMP_Text>();

            SerializedObject so = new SerializedObject(mgr);
            so.FindProperty("unlockInfoPanel").objectReferenceValue = panelGo;
            if (icon != null)
                so.FindProperty("unlockIconImage").objectReferenceValue = icon;
            if (title != null)
                so.FindProperty("unlockTitleText").objectReferenceValue = title;
            if (body != null)
                so.FindProperty("unlockBodyText").objectReferenceValue = body;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(mgr);
        }

        private static void EnsureChildrenWired(GameObject panelGo)
        {
            if (panelGo.GetComponent<CanvasGroup>() == null)
                Undo.AddComponent<CanvasGroup>(panelGo);

            if (panelGo.transform.Find("Icon") == null || panelGo.transform.Find("Texts") == null)
            {
                // Incomplete hierarchy — rebuild children while keeping root.
                for (int i = panelGo.transform.childCount - 1; i >= 0; i--)
                    Undo.DestroyObjectImmediate(panelGo.transform.GetChild(i).gameObject);
                BuildPanelChildren(panelGo);
            }
        }

        private static GameObject BuildPanel(Transform canvas)
        {
            var root = new GameObject(PanelName, typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            root.transform.SetParent(canvas, false);

            var panelRt = root.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0f);
            panelRt.anchorMax = new Vector2(0.5f, 0f);
            panelRt.pivot = new Vector2(0.5f, 0f);
            panelRt.anchoredPosition = new Vector2(0f, 220f);
            panelRt.sizeDelta = new Vector2(980f, 188f);

            var panelImg = root.GetComponent<Image>();
            panelImg.color = new Color(0.08f, 0.10f, 0.18f, 0.92f);
            panelImg.raycastTarget = false;

            var cg = root.GetComponent<CanvasGroup>();
            cg.alpha = 1f;
            cg.blocksRaycasts = false;
            cg.interactable = false;

            BuildPanelChildren(root);
            root.SetActive(false);
            return root;
        }

        private static void BuildPanelChildren(GameObject root)
        {
            TMP_FontAsset font = GetFont();

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(root.transform, false);
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0f, 0.5f);
            iconRt.anchorMax = new Vector2(0f, 0.5f);
            iconRt.pivot = new Vector2(0f, 0.5f);
            iconRt.anchoredPosition = new Vector2(22f, 0f);
            iconRt.sizeDelta = new Vector2(112f, 112f);
            var iconImg = iconGo.GetComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            var texts = new GameObject("Texts", typeof(RectTransform));
            texts.transform.SetParent(root.transform, false);
            var textsRt = texts.GetComponent<RectTransform>();
            textsRt.anchorMin = Vector2.zero;
            textsRt.anchorMax = Vector2.one;
            textsRt.offsetMin = new Vector2(150f, 18f);
            textsRt.offsetMax = new Vector2(-28f, -18f);

            var title = CreateTmp(texts.transform, "Title", 28f, FontStyles.Bold, font);
            var titleRt = title.rectTransform;
            titleRt.anchorMin = new Vector2(0f, 0.48f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.offsetMin = Vector2.zero;
            titleRt.offsetMax = Vector2.zero;
            title.alignment = TextAlignmentOptions.BottomLeft;
            title.text = "Congratulations! New booster unlocked";
            title.enableWordWrapping = true;
            title.overflowMode = TextOverflowModes.Overflow;
            title.enableAutoSizing = true;
            title.fontSizeMin = 18f;
            title.fontSizeMax = 28f;

            var body = CreateTmp(texts.transform, "Body", 22f, FontStyles.Normal, font);
            var bodyRt = body.rectTransform;
            bodyRt.anchorMin = new Vector2(0f, 0f);
            bodyRt.anchorMax = new Vector2(1f, 0.48f);
            bodyRt.offsetMin = Vector2.zero;
            bodyRt.offsetMax = Vector2.zero;
            body.alignment = TextAlignmentOptions.TopLeft;
            body.color = new Color(0.90f, 0.92f, 0.96f, 0.95f);
            body.text = "Short booster description goes here.";
            body.enableWordWrapping = true;
            body.overflowMode = TextOverflowModes.Overflow;
            body.enableAutoSizing = true;
            body.fontSizeMin = 16f;
            body.fontSizeMax = 22f;
        }

        private static TMP_Text CreateTmp(Transform parent, string name, float size, FontStyles style, TMP_FontAsset font)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            if (font != null) tmp.font = font;
            return tmp;
        }

        private static TMP_FontAsset GetFont()
        {
            TMP_FontAsset fromResources = Resources.Load<TMP_FontAsset>("Fonts/rimouski sb SDF");
            if (fromResources != null) return fromResources;

            string[] guids = AssetDatabase.FindAssets("rimouski t:TMP_FontAsset");
            if (guids.Length > 0)
                return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guids[0]));

            return null;
        }

        private static Transform FindNamed(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindNamed(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
#endif
