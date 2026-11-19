#if UNITY_EDITOR
using LegoPuzzle.Runtime;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace LegoPuzzle.Editor
{
    /// <summary>
    /// Creates MechanicUnlockTutorialPanel once under Canvas in the Game scene.
    /// Runtime only shows/hides — never rebuilds or overwrites authored layout.
    /// </summary>
    [InitializeOnLoad]
    public static class MechanicUnlockTutorialSceneSetup
    {
        private const string PanelName = "MechanicUnlockTutorialPanel";
        private const string ButtonSpritePath = "Assets/300Mind/2D Game UI Kit/Sprites/UI-pack_Sprite_1.png";
        private const string ButtonSpriteName = "UI-pack_Sprite_1_21";
        private const string TitleFontPath = "Assets/Fonts/GoodNews-RegularCYR_aLiNcE_0 SDF.asset";
        private const string BrickSpritePath = "Assets/Resources/Sprites/glossy_blue_brick_with_arrow.png";
        private const string BrickSpritePathAlt = "Assets/Sprites/Glossy Blue Brick with Arrow.png";

        static MechanicUnlockTutorialSceneSetup()
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

        [MenuItem("LEGO/Create Mechanic Unlock Tutorial Panel")]
        public static void CreateFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying)
            {
                Debug.LogWarning("[MechanicUnlockTutorial] Cannot edit scene during Play Mode.");
                return;
            }

            EnsureInScene(forceLog: true);
        }

        [MenuItem("LEGO/Recreate Mechanic Unlock Tutorial Panel")]
        public static void RecreateFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying)
            {
                Debug.LogWarning("[MechanicUnlockTutorial] Cannot edit scene during Play Mode.");
                return;
            }

            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas != null)
            {
                Transform existing = FindNamed(canvas.transform, PanelName);
                if (existing != null)
                    Undo.DestroyObjectImmediate(existing.gameObject);
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
                    Debug.LogWarning("[MechanicUnlockTutorial] No Canvas found.");
                return;
            }

            Transform existing = FindNamed(canvas.transform, PanelName);
            bool created = false;

            if (existing == null)
            {
                GameObject panelGo = BuildPanel(canvas.transform);
                Undo.RegisterCreatedObjectUndo(panelGo, "Create MechanicUnlockTutorialPanel");
                created = true;
                existing = panelGo.transform;
            }

            // Не чіпаємо layout існуючої панелі — лише wiring + англ. заголовок, якщо лишився UA.
            existing.gameObject.SetActive(false);
            EnsureEnglishTitle(existing.gameObject);
            WireTutorialManager(existing.gameObject);

            EditorUtility.SetDirty(existing.gameObject);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);

            if (forceLog || created)
                Debug.Log("<color=green>[MechanicUnlockTutorial] Panel ready on Canvas. Save the Game scene — runtime will not generate or reset it.</color>");
        }

        private static void EnsureEnglishTitle(GameObject panelGo)
        {
            if (panelGo == null) return;
            Transform titleTr = FindNamed(panelGo.transform, "Title");
            if (titleTr == null) return;
            var title = titleTr.GetComponent<TMP_Text>();
            if (title == null) return;

            string t = title.text ?? "";
            if (t.IndexOf("Механіка", System.StringComparison.OrdinalIgnoreCase) >= 0
                || t.IndexOf("механіку", System.StringComparison.OrdinalIgnoreCase) >= 0
                || string.IsNullOrWhiteSpace(t))
            {
                Undo.RecordObject(title, "Set Mechanic Tutorial Title EN");
                title.text = "MECHANIC UNLOCKED!";
                EditorUtility.SetDirty(title);
            }
        }

        private static void WireTutorialManager(GameObject panelGo)
        {
            MechanicUnlockTutorialManager mgr = Object.FindAnyObjectByType<MechanicUnlockTutorialManager>(FindObjectsInactive.Include);
            if (mgr == null)
            {
                LevelLoader loader = Object.FindAnyObjectByType<LevelLoader>(FindObjectsInactive.Include);
                if (loader != null)
                {
                    mgr = loader.GetComponent<MechanicUnlockTutorialManager>();
                    if (mgr == null)
                        mgr = Undo.AddComponent<MechanicUnlockTutorialManager>(loader.gameObject);
                }
            }

            if (mgr == null) return;

            SerializedObject so = new SerializedObject(mgr);
            so.FindProperty("tutorialPanel").objectReferenceValue = panelGo;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(mgr);
        }

        private static GameObject BuildPanel(Transform canvas)
        {
            TMP_FontAsset titleFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitleFontPath);
            TMP_FontAsset bodyFont = BoosterBadgeUI.GetDefaultTMPFont();
            if (bodyFont == null) bodyFont = titleFont;

            Sprite buttonSprite = LoadNamedSprite(ButtonSpritePath, ButtonSpriteName);
            Sprite brickSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BrickSpritePath);
            if (brickSprite == null)
                brickSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BrickSpritePathAlt);
            Sprite glowSprite = CreateOrLoadGlowSprite();

            var root = new GameObject(PanelName, typeof(RectTransform), typeof(CanvasGroup), typeof(Canvas), typeof(GraphicRaycaster));
            root.transform.SetParent(canvas, false);

            var rootRt = root.GetComponent<RectTransform>();
            StretchFull(rootRt);

            var subCanvas = root.GetComponent<Canvas>();
            subCanvas.overrideSorting = true;
            subCanvas.sortingOrder = 80;

            var cg = root.GetComponent<CanvasGroup>();
            cg.alpha = 1f;
            cg.blocksRaycasts = true;
            cg.interactable = true;

            // Dim
            var dimGo = new GameObject("Dim", typeof(RectTransform), typeof(Image));
            dimGo.transform.SetParent(root.transform, false);
            StretchFull(dimGo.GetComponent<RectTransform>());
            var dimImg = dimGo.GetComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.72f);
            dimImg.raycastTarget = true;

            // Content
            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(root.transform, false);
            var contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = contentRt.anchorMax = contentRt.pivot = new Vector2(0.5f, 0.5f);
            contentRt.sizeDelta = new Vector2(720f, 900f);
            contentRt.anchoredPosition = new Vector2(0f, 20f);

            // Title
            var title = CreateTmp(content.transform, "Title", 58f, FontStyles.Bold, titleFont != null ? titleFont : bodyFont);
            var titleRt = title.rectTransform;
            titleRt.anchorMin = titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -10f);
            titleRt.sizeDelta = new Vector2(680f, 110f);
            title.alignment = TextAlignmentOptions.Center;
            title.text = "MECHANIC UNLOCKED!";
            title.color = new Color(1f, 0.78f, 0.18f, 1f);
            title.enableAutoSizing = true;
            title.fontSizeMin = 44f;
            title.fontSizeMax = 64f;
            title.outlineWidth = 0.28f;
            title.outlineColor = new Color32(28, 24, 70, 255);
            UITextShimmer.AttachTo(title.gameObject, new Color(1f, 0.78f, 0.18f, 1f));

            // Illustration
            var illu = new GameObject("Illustration", typeof(RectTransform));
            illu.transform.SetParent(content.transform, false);
            var illuRt = illu.GetComponent<RectTransform>();
            illuRt.anchorMin = illuRt.anchorMax = illuRt.pivot = new Vector2(0.5f, 0.5f);
            illuRt.anchoredPosition = new Vector2(0f, 80f);
            illuRt.sizeDelta = new Vector2(520f, 520f);

            var glow = CreateImage(illu.transform, "GlowRays", new Vector2(560f, 560f), glowSprite, Color.white);
            glow.rectTransform.SetAsFirstSibling();

            var brick = CreateImage(illu.transform, "BlockIcon", new Vector2(380f, 340f), brickSprite, Color.white);
            brick.rectTransform.anchoredPosition = new Vector2(0f, -4f);

            // Body
            var body = CreateTmp(content.transform, "Body", 40f, FontStyles.Bold, bodyFont);
            var bodyRt = body.rectTransform;
            bodyRt.anchorMin = bodyRt.anchorMax = new Vector2(0.5f, 0f);
            bodyRt.pivot = new Vector2(0.5f, 0f);
            bodyRt.anchoredPosition = new Vector2(0f, 145f);
            bodyRt.sizeDelta = new Vector2(680f, 170f);
            body.alignment = TextAlignmentOptions.Center;
            body.enableWordWrapping = true;
            body.richText = true;
            body.text = "The block can only move in <color=#5CE1FF>one direction</color>, without changing its path.";
            body.color = Color.white;
            body.enableAutoSizing = true;
            body.fontSizeMin = 30f;
            body.fontSizeMax = 44f;
            body.outlineWidth = 0.22f;
            body.outlineColor = new Color32(20, 20, 35, 255);

            // GOT IT button
            var btnGo = new GameObject("GotItButton", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(content.transform, false);
            var btnRt = btnGo.GetComponent<RectTransform>();
            btnRt.anchorMin = btnRt.anchorMax = new Vector2(0.5f, 0f);
            btnRt.pivot = new Vector2(0.5f, 0f);
            btnRt.anchoredPosition = new Vector2(0f, 18f);
            btnRt.sizeDelta = new Vector2(360f, 110f);

            var btnImg = btnGo.GetComponent<Image>();
            btnImg.sprite = buttonSprite;
            btnImg.type = Image.Type.Simple;
            btnImg.preserveAspect = true;
            // Жовтий спрайт → зелений tint.
            btnImg.color = new Color(0.32f, 1f, 0.42f, 1f);

            var btn = btnGo.GetComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.transition = Selectable.Transition.ColorTint;

            var btnLabel = CreateTmp(btnGo.transform, "Label", 42f, FontStyles.Bold, bodyFont);
            StretchFull(btnLabel.rectTransform);
            btnLabel.alignment = TextAlignmentOptions.Center;
            btnLabel.text = "GOT IT";
            btnLabel.color = new Color(0.25f, 0.12f, 0.05f, 1f);

            UIButtonPressEffect.AttachTo(btnGo);
            UIButtonShinePulse.AttachTo(btnGo, pulseMax: 1.035f, shineDelay: 1.8f);

            root.SetActive(false);
            return root;
        }

        private static Sprite LoadNamedSprite(string texturePath, string spriteName)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(texturePath);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite sp && sp.name == spriteName)
                    return sp;
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
        }

        private static Sprite CreateOrLoadGlowSprite()
        {
            const string glowPath = "Assets/Resources/Sprites/mechanic_tutorial_glow.png";
            Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(glowPath);
            if (existing != null) return existing;

            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear
            };

            Color[] pixels = new Color[size * size];
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            const int rays = 12;
            float maxR = size * 0.48f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x, y) - center;
                float r = p.magnitude;
                if (r > maxR)
                {
                    pixels[y * size + x] = Color.clear;
                    continue;
                }

                float ang = Mathf.Atan2(p.y, p.x);
                float rayPhase = (ang / (Mathf.PI * 2f)) * rays;
                float rayDist = Mathf.Abs(rayPhase - Mathf.Round(rayPhase));
                float rayWidth = Mathf.Lerp(0.18f, 0.07f, r / maxR);
                float rayMask = Mathf.Clamp01(1f - rayDist / rayWidth);
                float radial = Mathf.Clamp01(1f - r / maxR);
                float softCore = Mathf.Clamp01(1f - r / (maxR * 0.35f)) * 0.35f;
                float alpha = Mathf.Clamp01(rayMask * radial * 0.95f + softCore) * Mathf.SmoothStep(0f, 1f, radial);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }

            tex.SetPixels(pixels);
            tex.Apply(false, false);

            System.IO.Directory.CreateDirectory("Assets/Resources/Sprites");
            System.IO.File.WriteAllBytes(glowPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(glowPath);

            var importer = AssetImporter.GetAtPath(glowPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(glowPath);
        }

        private static Image CreateImage(Transform parent, string name, Vector2 size, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            img.preserveAspect = true;
            return img;
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

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
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
