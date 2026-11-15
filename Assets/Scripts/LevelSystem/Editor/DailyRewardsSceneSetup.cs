#if UNITY_EDITOR
using LegoPuzzle.Runtime;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace LegoPuzzle.Editor
{
    /// <summary>
    /// Creates Daily Rewards button + panel hierarchy once in the Menu scene (Edit Mode only).
    /// Runtime never creates or rebuilds these objects.
    /// </summary>
    [InitializeOnLoad]
    public static class DailyRewardsSceneSetup
    {
        private const string ButtonName = "DailyRewardsButton";
        private const string PanelName = "DailyRewardsPanel";

        static DailyRewardsSceneSetup()
        {
            EditorApplication.delayCall += EnsureDailyRewardsInSceneDelay;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            UnityEditor.SceneManagement.EditorSceneManager.sceneOpened -= OnSceneOpened;
            UnityEditor.SceneManagement.EditorSceneManager.sceneOpened += OnSceneOpened;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += EnsureDailyRewardsInSceneDelay;
        }

        private static void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, UnityEditor.SceneManagement.OpenSceneMode mode)
        {
            EditorApplication.delayCall += EnsureDailyRewardsInSceneDelay;
        }

        private static void EnsureDailyRewardsInSceneDelay()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying) return;
            EnsureDailyRewardsInScene(selectCreated: false, forceLog: false);
        }

        [MenuItem("LEGO/Create Daily Rewards Panel")]
        public static void CreateDailyRewardsInScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying)
            {
                Debug.LogWarning("[DailyRewards] Cannot edit scene during Play Mode.");
                return;
            }

            EnsureDailyRewardsInScene(selectCreated: true, forceLog: true);
        }

        private static void EnsureDailyRewardsInScene(bool selectCreated, bool forceLog)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying)
                return;

            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!activeScene.isLoaded) return;

            bool isMenuScene = activeScene.name.IndexOf("Menu", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                               Object.FindAnyObjectByType<MenuButtonController>() != null;
            if (!isMenuScene) return;

            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                if (forceLog)
                    Debug.LogWarning("[DailyRewards] No Canvas found.");
                return;
            }

            DailyRewardsPanel existingPanel =
                Object.FindAnyObjectByType<DailyRewardsPanel>(FindObjectsInactive.Include);
            bool alreadyPresent =
                FindNamed(canvas.transform, PanelName) != null
                && existingPanel != null
                && Object.FindAnyObjectByType<DailyRewardsButton>(FindObjectsInactive.Include) != null;
            bool needsSpriteBake = existingPanel != null && DayIconsMissingSprites(existingPanel);
            bool needsRowFix = existingPanel != null && DayRowsOverlap(existingPanel);

            if (alreadyPresent && !needsSpriteBake && !needsRowFix && !forceLog)
                return;

            CreateDailyRewardsInSceneInternal(
                canvas,
                activeScene,
                selectCreated,
                forceLog || !alreadyPresent || needsSpriteBake || needsRowFix);
        }

        private static Vector2[] GetDaySlotPositions()
        {
            // Day cards are 300 tall; keep ≥40px gap so CLAIM on row 1 stays clickable.
            const float row1 = 300f;
            const float row2 = -60f;
            const float row3 = -400f;
            return new[]
            {
                new Vector2(-280f, row1), new Vector2(0f, row1), new Vector2(280f, row1),
                new Vector2(-280f, row2), new Vector2(0f, row2), new Vector2(280f, row2),
                new Vector2(0f, row3)
            };
        }

        private static bool DayRowsOverlap(DailyRewardsPanel panel)
        {
            if (panel == null) return false;
            Transform card = panel.transform.Find("Card");
            if (card == null) return false;

            RectTransform day1 = card.Find("Day1") as RectTransform;
            RectTransform day4 = card.Find("Day4") as RectTransform;
            if (day1 == null || day4 == null) return false;

            float gap = day1.anchoredPosition.y - day4.anchoredPosition.y;
            float minGap = (day1.sizeDelta.y + day4.sizeDelta.y) * 0.5f + 36f;
            return gap < minGap;
        }

        private static void ApplyDayRowSpacing(DailyRewardsPanel panel)
        {
            if (panel == null) return;
            Transform card = panel.transform.Find("Card");
            if (card == null) return;

            Vector2[] positions = GetDaySlotPositions();
            for (int i = 0; i < 7; i++)
            {
                Transform day = card.Find($"Day{i + 1}");
                if (day == null) continue;
                var rt = day as RectTransform;
                if (rt == null) continue;
                Undo.RecordObject(rt, "Daily Rewards row spacing");
                rt.anchoredPosition = positions[i];
                EditorUtility.SetDirty(rt);
            }
        }

        private static bool DayIconsMissingSprites(DailyRewardsPanel panel)
        {
            if (panel == null) return true;
            SerializedObject so = new SerializedObject(panel);
            SerializedProperty daysProp = so.FindProperty("days");
            if (daysProp == null || !daysProp.isArray || daysProp.arraySize == 0)
                return true;

            for (int i = 0; i < daysProp.arraySize; i++)
            {
                SerializedProperty day = daysProp.GetArrayElementAtIndex(i);
                SerializedProperty icons = day.FindPropertyRelative("rewardIcons");
                SerializedProperty rewards = day.FindPropertyRelative("rewards");
                if (icons == null || !icons.isArray || icons.arraySize == 0)
                    return true;
                if (rewards == null || !rewards.isArray || rewards.arraySize == 0)
                    return true;

                int check = Mathf.Min(icons.arraySize, rewards.arraySize);
                for (int r = 0; r < check; r++)
                {
                    var icon = icons.GetArrayElementAtIndex(r).objectReferenceValue as Image;
                    if (icon == null || icon.sprite == null)
                        return true;
                }
            }

            return false;
        }

        private static void CreateDailyRewardsInSceneInternal(
            Canvas canvas,
            UnityEngine.SceneManagement.Scene activeScene,
            bool selectCreated,
            bool log)
        {

            Sprite coin = FindSprite("UI-pack_Sprite_1_11");
            Sprite hammer = FindSprite("image-removebg-preview");
            Sprite freeze = FindSprite("image-removebg-preview(1)");
            Sprite hint = FindSprite("image-removebg-preview(2)");
            Sprite gem = FindSprite("gem") ?? FindSprite("Gem") ?? FindSprite("UI-pack_Sprite_1_11");
            Sprite rounded = FindSprite("UI-pack_Sprite_1_01") ?? FindSprite("UI-pack_Sprite_1_37");

            Transform lottoBtn = FindNamed(canvas.transform, "LottoButton");
            Transform buttonTr = FindNamed(canvas.transform, "Daily Rewards Button")
                ?? FindNamed(canvas.transform, ButtonName)
                ?? FindNamed(canvas.transform, "DailyRewards Button");

            if (buttonTr == null)
            {
                var buttonGo = new GameObject(ButtonName, typeof(RectTransform), typeof(Image), typeof(Button));
                Undo.RegisterCreatedObjectUndo(buttonGo, "Create DailyRewardsButton");
                buttonGo.transform.SetParent(canvas.transform, false);

                var rect = buttonGo.GetComponent<RectTransform>();
                if (lottoBtn != null)
                {
                    var lottoRect = lottoBtn as RectTransform;
                    rect.anchorMin = lottoRect.anchorMin;
                    rect.anchorMax = lottoRect.anchorMax;
                    rect.pivot = lottoRect.pivot;
                    rect.sizeDelta = lottoRect.sizeDelta;
                    rect.anchoredPosition = lottoRect.anchoredPosition + new Vector2(0f, -200f);
                    buttonGo.transform.SetSiblingIndex(lottoBtn.GetSiblingIndex() + 1);
                }
                else
                {
                    rect.anchorMin = new Vector2(0f, 0.5f);
                    rect.anchorMax = new Vector2(0f, 0.5f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.anchoredPosition = new Vector2(90f, -200f);
                    rect.sizeDelta = new Vector2(180f, 180f);
                }

                var image = buttonGo.GetComponent<Image>();
                image.color = new Color(0.95f, 0.75f, 0.2f, 1f);
                if (rounded != null)
                {
                    image.sprite = rounded;
                    image.type = Image.Type.Sliced;
                }

                var labelGo = new GameObject("Label", typeof(RectTransform));
                labelGo.transform.SetParent(buttonGo.transform, false);
                Stretch(labelGo.GetComponent<RectTransform>());
                var label = labelGo.AddComponent<TextMeshProUGUI>();
                label.text = "DAILY";
                label.fontSize = 28f;
                label.fontStyle = FontStyles.Bold;
                label.alignment = TextAlignmentOptions.Center;
                label.color = Color.white;
                label.raycastTarget = false;
                ApplyFont(label);

                buttonTr = buttonGo.transform;
            }

            var dailyBtn = buttonTr.GetComponent<DailyRewardsButton>();
            if (dailyBtn == null)
                dailyBtn = Undo.AddComponent<DailyRewardsButton>(buttonTr.gameObject);

            Transform panelTr = FindNamed(canvas.transform, PanelName);
            DailyRewardsPanel panel;
            if (panelTr == null)
            {
                panel = BuildPanel(canvas.transform, rounded, coin, hammer, freeze, hint, gem);
                panelTr = panel.transform;
                Undo.RegisterCreatedObjectUndo(panel.gameObject, "Create DailyRewardsPanel");
            }
            else
            {
                panel = panelTr.GetComponent<DailyRewardsPanel>();
                if (panel == null)
                    panel = Undo.AddComponent<DailyRewardsPanel>(panelTr.gameObject);
            }

            SerializedObject soBtn = new SerializedObject(dailyBtn);
            soBtn.FindProperty("targetPanel").objectReferenceValue = panel;
            soBtn.ApplyModifiedPropertiesWithoutUndo();

            WirePanelIfNeeded(panel, coin, hammer, freeze, hint, gem);
            AssignDefaultRewards(panel);
            BakeAuthoredDayVisuals(panel, coin, hammer, freeze, hint, gem);
            ApplyDayRowSpacing(panel);

            // Visible in Edit Mode; Open animation starts from alpha 0 at runtime.
            var cg = panel.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 1f;
                EditorUtility.SetDirty(cg);
            }

            EditorUtility.SetDirty(buttonTr.gameObject);
            EditorUtility.SetDirty(panel.gameObject);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
            if (selectCreated)
                Selection.activeGameObject = panel.gameObject;
            if (log)
                Debug.Log("<color=green>[DailyRewards] Button + panel ready with authored sprites. Save Menu — runtime will not load/paint icons.</color>");
        }

        /// <summary>
        /// Batch / CI: open Menu, create panel if missing, save scene.
        /// </summary>
        public static void BakeDailyRewardsIntoMenuScene()
        {
            string menuPath = "Assets/Scenes/Menu.unity";
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(menuPath, UnityEditor.SceneManagement.OpenSceneMode.Single);
            EnsureDailyRewardsInScene(selectCreated: false, forceLog: true);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Debug.Log("[DailyRewards] Menu.unity saved with DailyRewardsPanel.");
        }

        private static DailyRewardsPanel BuildPanel(
            Transform canvas,
            Sprite rounded,
            Sprite coin,
            Sprite hammer,
            Sprite freeze,
            Sprite hint,
            Sprite gem)
        {
            var root = new GameObject(PanelName, typeof(RectTransform), typeof(CanvasGroup), typeof(DailyRewardsPanel));
            root.transform.SetParent(canvas, false);
            Stretch(root.GetComponent<RectTransform>());
            var cg = root.GetComponent<CanvasGroup>();
            cg.alpha = 0f;

            var backdrop = CreateUi("Backdrop", root.transform);
            Stretch(backdrop.GetComponent<RectTransform>());
            var backdropImg = backdrop.AddComponent<Image>();
            backdropImg.color = new Color(0.02f, 0.04f, 0.12f, 0.72f);
            var backdropBtn = backdrop.AddComponent<Button>();
            backdropBtn.targetGraphic = backdropImg;

            var card = CreateUi("Card", root.transform);
            var cardRect = card.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(920f, 1180f);
            cardRect.anchoredPosition = Vector2.zero;
            var cardImg = card.AddComponent<Image>();
            cardImg.color = new Color(0.35f, 0.72f, 0.95f, 1f);
            if (rounded != null)
            {
                cardImg.sprite = rounded;
                cardImg.type = Image.Type.Sliced;
            }

            var title = CreateLabel(card.transform, "Title", "DAILY REWARDS", 48f, new Vector2(0f, 520f), new Vector2(700f, 60f));
            title.color = Color.white;

            var close = CreateUi("CloseButton", card.transform);
            var closeRect = close.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-18f, -18f);
            closeRect.sizeDelta = new Vector2(72f, 72f);
            var closeImg = close.AddComponent<Image>();
            closeImg.color = new Color(0.2f, 0.45f, 0.95f, 1f);
            var closeBtn = close.AddComponent<Button>();
            closeBtn.targetGraphic = closeImg;
            var closeLabel = CreateLabel(close.transform, "X", "X", 36f, Vector2.zero, new Vector2(72f, 72f));
            closeLabel.color = Color.white;

            var panel = root.GetComponent<DailyRewardsPanel>();
            var days = new DailyRewardDaySlot[7];

            // Days 1-6 grid 3x2
            // Card height 300 → row centers need ≥324 apart so CLAIM buttons don't overlap.
            Vector2[] positions = GetDaySlotPositions();

            for (int i = 0; i < 7; i++)
            {
                bool big = i == 6;
                days[i] = BuildDaySlot(
                    card.transform,
                    i + 1,
                    positions[i],
                    big ? new Vector2(820f, 280f) : new Vector2(260f, 300f),
                    rounded);
            }

            SerializedObject so = new SerializedObject(panel);
            so.FindProperty("canvasGroup").objectReferenceValue = cg;
            so.FindProperty("closeButton").objectReferenceValue = closeBtn;
            so.FindProperty("backdropButton").objectReferenceValue = backdropBtn;
            so.FindProperty("hintSprite").objectReferenceValue = hint;
            so.FindProperty("freezeSprite").objectReferenceValue = freeze;
            so.FindProperty("hammerSprite").objectReferenceValue = hammer;
            so.FindProperty("coinSprite").objectReferenceValue = coin;
            so.FindProperty("gemSprite").objectReferenceValue = gem;

            SerializedProperty daysProp = so.FindProperty("days");
            daysProp.arraySize = 7;
            for (int i = 0; i < 7; i++)
                WriteDayProperty(daysProp.GetArrayElementAtIndex(i), days[i]);

            so.ApplyModifiedPropertiesWithoutUndo();
            AssignDefaultRewards(panel);
            return panel;
        }

        private static void WirePanelIfNeeded(
            DailyRewardsPanel panel,
            Sprite coin,
            Sprite hammer,
            Sprite freeze,
            Sprite hint,
            Sprite gem)
        {
            SerializedObject so = new SerializedObject(panel);
            if (so.FindProperty("hintSprite").objectReferenceValue == null)
                so.FindProperty("hintSprite").objectReferenceValue = hint;
            if (so.FindProperty("freezeSprite").objectReferenceValue == null)
                so.FindProperty("freezeSprite").objectReferenceValue = freeze;
            if (so.FindProperty("hammerSprite").objectReferenceValue == null)
                so.FindProperty("hammerSprite").objectReferenceValue = hammer;
            if (so.FindProperty("coinSprite").objectReferenceValue == null)
                so.FindProperty("coinSprite").objectReferenceValue = coin;
            if (so.FindProperty("gemSprite").objectReferenceValue == null)
                so.FindProperty("gemSprite").objectReferenceValue = gem;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Writes reward icons + amount labels onto scene Image/TMP so they show in Edit Mode.
        /// Only fills missing sprites / empty amount text — never overwrites user-assigned art.
        /// </summary>
        private static void BakeAuthoredDayVisuals(
            DailyRewardsPanel panel,
            Sprite coin,
            Sprite hammer,
            Sprite freeze,
            Sprite hint,
            Sprite gem)
        {
            if (panel == null) return;

            SerializedObject so = new SerializedObject(panel);
            SerializedProperty daysProp = so.FindProperty("days");
            if (daysProp == null || !daysProp.isArray) return;

            Sprite Hint() => (Sprite)so.FindProperty("hintSprite").objectReferenceValue ?? hint;
            Sprite Freeze() => (Sprite)so.FindProperty("freezeSprite").objectReferenceValue ?? freeze;
            Sprite Hammer() => (Sprite)so.FindProperty("hammerSprite").objectReferenceValue ?? hammer;
            Sprite Coin() => (Sprite)so.FindProperty("coinSprite").objectReferenceValue ?? coin;
            Sprite Gem() => (Sprite)so.FindProperty("gemSprite").objectReferenceValue ?? gem;

            for (int i = 0; i < daysProp.arraySize; i++)
            {
                SerializedProperty day = daysProp.GetArrayElementAtIndex(i);
                SerializedProperty rewards = day.FindPropertyRelative("rewards");
                SerializedProperty icons = day.FindPropertyRelative("rewardIcons");
                SerializedProperty amounts = day.FindPropertyRelative("rewardAmountLabels");
                var dayLabel = day.FindPropertyRelative("dayLabel").objectReferenceValue as TMP_Text;
                int dayNumber = day.FindPropertyRelative("dayNumber").intValue;
                if (dayLabel != null && string.IsNullOrEmpty(dayLabel.text))
                {
                    dayLabel.text = $"DAY {dayNumber}";
                    EditorUtility.SetDirty(dayLabel);
                }

                if (rewards == null || !rewards.isArray) continue;

                for (int r = 0; r < rewards.arraySize; r++)
                {
                    SerializedProperty item = rewards.GetArrayElementAtIndex(r);
                    var kind = (DailyRewardKind)item.FindPropertyRelative("kind").enumValueIndex;
                    int amount = item.FindPropertyRelative("amount").intValue;
                    var iconOverride = item.FindPropertyRelative("iconOverride").objectReferenceValue as Sprite;

                    Sprite spr = iconOverride;
                    if (spr == null)
                    {
                        spr = kind switch
                        {
                            DailyRewardKind.Hint => Hint(),
                            DailyRewardKind.Freeze => Freeze(),
                            DailyRewardKind.Hammer => Hammer(),
                            DailyRewardKind.Coins => Coin(),
                            DailyRewardKind.Gems => Gem(),
                            _ => Coin()
                        };
                    }

                    if (icons != null && icons.isArray && r < icons.arraySize)
                    {
                        var icon = icons.GetArrayElementAtIndex(r).objectReferenceValue as Image;
                        if (icon != null)
                        {
                            if (icon.sprite == null && spr != null)
                                icon.sprite = spr;
                            icon.preserveAspect = true;
                            icon.gameObject.SetActive(true);
                            EditorUtility.SetDirty(icon);
                        }
                    }

                    if (amounts != null && amounts.isArray && r < amounts.arraySize)
                    {
                        var label = amounts.GetArrayElementAtIndex(r).objectReferenceValue as TMP_Text;
                        if (label != null)
                        {
                            if (string.IsNullOrEmpty(label.text) || label.text == "x1")
                            {
                                label.text = (kind == DailyRewardKind.Coins || kind == DailyRewardKind.Gems)
                                    ? amount.ToString()
                                    : $"x{amount}";
                            }

                            label.gameObject.SetActive(true);
                            EditorUtility.SetDirty(label);
                        }
                    }
                }

                // Hide unused icon slots.
                if (icons != null && icons.isArray && rewards.arraySize < icons.arraySize)
                {
                    for (int r = rewards.arraySize; r < icons.arraySize; r++)
                    {
                        var icon = icons.GetArrayElementAtIndex(r).objectReferenceValue as Image;
                        if (icon != null)
                            icon.gameObject.SetActive(false);
                    }
                }

                if (amounts != null && amounts.isArray && rewards.arraySize < amounts.arraySize)
                {
                    for (int r = rewards.arraySize; r < amounts.arraySize; r++)
                    {
                        var label = amounts.GetArrayElementAtIndex(r).objectReferenceValue as TMP_Text;
                        if (label != null)
                            label.gameObject.SetActive(false);
                    }
                }
            }

            EditorUtility.SetDirty(panel);
        }

        private static void AssignDefaultRewards(DailyRewardsPanel panel)
        {
            // Force Awake-style defaults via serialized rewards if empty.
            SerializedObject so = new SerializedObject(panel);
            SerializedProperty daysProp = so.FindProperty("days");
            if (daysProp == null || !daysProp.isArray) return;

            DailyRewardItem[][] defaults =
            {
                new[] { new DailyRewardItem { kind = DailyRewardKind.Hint, amount = 3 } },
                new[] { new DailyRewardItem { kind = DailyRewardKind.Coins, amount = 200 } },
                new[] { new DailyRewardItem { kind = DailyRewardKind.Freeze, amount = 3 } },
                new[] { new DailyRewardItem { kind = DailyRewardKind.Coins, amount = 400 } },
                new[] { new DailyRewardItem { kind = DailyRewardKind.Hammer, amount = 3 } },
                new[] { new DailyRewardItem { kind = DailyRewardKind.Coins, amount = 800 } },
                new[]
                {
                    new DailyRewardItem { kind = DailyRewardKind.Hammer, amount = 5 },
                    new DailyRewardItem { kind = DailyRewardKind.Freeze, amount = 5 },
                    new DailyRewardItem { kind = DailyRewardKind.Hint, amount = 5 },
                    new DailyRewardItem { kind = DailyRewardKind.Gems, amount = 10 }
                }
            };

            for (int i = 0; i < Mathf.Min(7, daysProp.arraySize); i++)
            {
                SerializedProperty day = daysProp.GetArrayElementAtIndex(i);
                SerializedProperty rewards = day.FindPropertyRelative("rewards");
                if (rewards == null) continue;
                if (rewards.arraySize > 0) continue;

                rewards.arraySize = defaults[i].Length;
                for (int r = 0; r < defaults[i].Length; r++)
                {
                    SerializedProperty item = rewards.GetArrayElementAtIndex(r);
                    item.FindPropertyRelative("kind").enumValueIndex = (int)defaults[i][r].kind;
                    item.FindPropertyRelative("amount").intValue = defaults[i][r].amount;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static DailyRewardDaySlot BuildDaySlot(
            Transform parent,
            int dayNumber,
            Vector2 pos,
            Vector2 size,
            Sprite rounded)
        {
            var root = CreateUi($"Day{dayNumber}", parent);
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = pos;
            rootRect.sizeDelta = size;

            var bg = root.AddComponent<Image>();
            bg.color = new Color(0.62f, 0.62f, 0.66f, 1f);
            if (rounded != null)
            {
                bg.sprite = rounded;
                bg.type = Image.Type.Sliced;
            }

            var dayLabel = CreateLabel(root.transform, "DayLabel", $"DAY {dayNumber}", 22f, new Vector2(0f, size.y * 0.5f - 28f), new Vector2(size.x - 20f, 36f));
            dayLabel.color = Color.white;

            int iconCount = dayNumber == 7 ? 4 : 1;
            var icons = new Image[iconCount];
            var amounts = new TMP_Text[iconCount];
            float iconY = dayNumber == 7 ? 20f : 30f;
            float spacing = dayNumber == 7 ? 170f : 0f;
            float startX = dayNumber == 7 ? -spacing * 1.5f : 0f;

            for (int i = 0; i < iconCount; i++)
            {
                var iconGo = CreateUi($"Icon{i}", root.transform);
                var iconRect = iconGo.GetComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.anchoredPosition = new Vector2(startX + spacing * i, iconY);
                iconRect.sizeDelta = new Vector2(72f, 72f);
                icons[i] = iconGo.AddComponent<Image>();
                icons[i].preserveAspect = true;
                icons[i].raycastTarget = false;

                amounts[i] = CreateLabel(
                    root.transform,
                    $"Amount{i}",
                    "x1",
                    24f,
                    new Vector2(startX + spacing * i, iconY - 55f),
                    new Vector2(100f, 32f));
                amounts[i].color = Color.white;
            }

            var actionGo = CreateUi("ActionButton", root.transform);
            var actionRect = actionGo.GetComponent<RectTransform>();
            actionRect.anchorMin = new Vector2(0.5f, 0f);
            actionRect.anchorMax = new Vector2(0.5f, 0f);
            actionRect.pivot = new Vector2(0.5f, 0f);
            actionRect.anchoredPosition = new Vector2(0f, 16f);
            actionRect.sizeDelta = new Vector2(Mathf.Min(220f, size.x - 30f), 56f);
            var actionImg = actionGo.AddComponent<Image>();
            actionImg.color = new Color(0.25f, 0.78f, 0.35f, 1f);
            if (rounded != null)
            {
                actionImg.sprite = rounded;
                actionImg.type = Image.Type.Sliced;
            }

            var actionBtn = actionGo.AddComponent<Button>();
            actionBtn.targetGraphic = actionImg;
            var actionLabel = CreateLabel(actionGo.transform, "Label", "CLAIM", 26f, Vector2.zero, actionRect.sizeDelta);
            actionLabel.color = Color.white;

            var adIconGo = CreateUi("AdIcon", actionGo.transform);
            var adRect = adIconGo.GetComponent<RectTransform>();
            adRect.anchorMin = new Vector2(0f, 0.5f);
            adRect.anchorMax = new Vector2(0f, 0.5f);
            adRect.pivot = new Vector2(0.5f, 0.5f);
            adRect.anchoredPosition = new Vector2(28f, 0f);
            adRect.sizeDelta = new Vector2(28f, 28f);
            var adImg = adIconGo.AddComponent<Image>();
            adImg.color = new Color(1f, 1f, 1f, 0.9f);
            adImg.raycastTarget = false;
            adIconGo.SetActive(false);

            var claimed = CreateUi("ClaimedOverlay", root.transform);
            Stretch(claimed.GetComponent<RectTransform>(), 8f);
            var claimedImg = claimed.AddComponent<Image>();
            claimedImg.color = new Color(0f, 0f, 0f, 0.35f);
            claimedImg.raycastTarget = false;
            var claimedLabel = CreateLabel(claimed.transform, "Check", "CLAIMED", 28f, Vector2.zero, new Vector2(200f, 40f));
            claimedLabel.color = Color.white;
            claimed.SetActive(false);

            var locked = CreateUi("LockedOverlay", root.transform);
            Stretch(locked.GetComponent<RectTransform>(), 8f);
            var lockedImg = locked.AddComponent<Image>();
            lockedImg.color = new Color(0f, 0f, 0f, 0.25f);
            lockedImg.raycastTarget = false;
            locked.SetActive(true);

            return new DailyRewardDaySlot
            {
                dayNumber = dayNumber,
                root = rootRect,
                background = bg,
                dayLabel = dayLabel,
                rewardIcons = icons,
                rewardAmountLabels = amounts,
                actionButton = actionBtn,
                actionButtonLabel = actionLabel,
                adIcon = adIconGo,
                claimedOverlay = claimed,
                lockedOverlay = locked
            };
        }

        private static void WriteDayProperty(SerializedProperty dayProp, DailyRewardDaySlot slot)
        {
            dayProp.FindPropertyRelative("dayNumber").intValue = slot.dayNumber;
            dayProp.FindPropertyRelative("root").objectReferenceValue = slot.root;
            dayProp.FindPropertyRelative("background").objectReferenceValue = slot.background;
            dayProp.FindPropertyRelative("dayLabel").objectReferenceValue = slot.dayLabel;
            dayProp.FindPropertyRelative("actionButton").objectReferenceValue = slot.actionButton;
            dayProp.FindPropertyRelative("actionButtonLabel").objectReferenceValue = slot.actionButtonLabel;
            dayProp.FindPropertyRelative("adIcon").objectReferenceValue = slot.adIcon;
            dayProp.FindPropertyRelative("claimedOverlay").objectReferenceValue = slot.claimedOverlay;
            dayProp.FindPropertyRelative("lockedOverlay").objectReferenceValue = slot.lockedOverlay;

            SerializedProperty icons = dayProp.FindPropertyRelative("rewardIcons");
            icons.arraySize = slot.rewardIcons != null ? slot.rewardIcons.Length : 0;
            for (int i = 0; i < icons.arraySize; i++)
                icons.GetArrayElementAtIndex(i).objectReferenceValue = slot.rewardIcons[i];

            SerializedProperty amounts = dayProp.FindPropertyRelative("rewardAmountLabels");
            amounts.arraySize = slot.rewardAmountLabels != null ? slot.rewardAmountLabels.Length : 0;
            for (int i = 0; i < amounts.arraySize; i++)
                amounts.GetArrayElementAtIndex(i).objectReferenceValue = slot.rewardAmountLabels[i];
        }

        private static GameObject CreateUi(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static TMP_Text CreateLabel(Transform parent, string name, string text, float size, Vector2 pos, Vector2 sizeDelta)
        {
            var go = CreateUi(name, parent);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;
            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            ApplyFont(label);
            return label;
        }

        private static void Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        private static void ApplyFont(TMP_Text label)
        {
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts/rimouski sb SDF");
            if (font == null)
                font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/rimouski sb SDF.asset");
            if (font != null)
                label.font = font;
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

        private static Sprite FindSprite(string spriteName)
        {
            if (string.IsNullOrEmpty(spriteName)) return null;
            string[] guids = AssetDatabase.FindAssets(spriteName + " t:Sprite");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
                for (int a = 0; a < assets.Length; a++)
                {
                    if (assets[a] is Sprite sp && sp.name == spriteName)
                        return sp;
                }

                Sprite direct = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (direct != null)
                    return direct;
            }

            return null;
        }
    }
}
#endif
