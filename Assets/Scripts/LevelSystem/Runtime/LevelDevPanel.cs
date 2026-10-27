using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;
using LegoPuzzle.Data;

namespace LegoPuzzle.Runtime
{
    /// <summary>
    /// Панель розробника (Developer / Debug Panel).
    /// Дозволяє миттєво перестрибувати на будь-який рівень (1-26+), перезавантажувати рівень,
    /// переходити вперед/назад, перемагати на рівні та поповнювати ресурси для тестування.
    /// Працює як у Unity Editor, так і в білді на телефоні (Android/iOS).
    /// </summary>
    public class LevelDevPanel : MonoBehaviour
    {
        public static LevelDevPanel Instance { get; private set; }

        private Canvas devCanvas;
        private CanvasScaler devCanvasScaler;
        private GameObject floatingButtonObj;
        private RectTransform floatingButtonRect;
        private Image floatingButtonBg;
        private TMP_Text floatingButtonText;

        private GameObject windowRootObj;
        private RectTransform windowCardRect;
        private TMP_Text titleText;
        private TMP_Text statusText;
        private Transform levelGridContent;
        private ScrollRect levelScrollRect;

        private readonly List<Button> spawnedLevelButtons = new List<Button>();
        private readonly List<TMP_Text> spawnedLevelTexts = new List<TMP_Text>();
        private readonly List<Image> spawnedLevelBgs = new List<Image>();

        private bool isOpen = false;

        private static Sprite cachedRoundedRectSprite;
        private static Sprite cachedBorderSprite;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance == null && Application.isPlaying)
            {
                GameObject root = new GameObject("[LevelDevPanel]");
                Instance = root.AddComponent<LevelDevPanel>();
                DontDestroyOnLoad(root);
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (transform.parent == null && Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }

            BuildUI();
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Start()
        {
            RefreshLevelList();
            UpdateStatus();
        }

        private void Update()
        {
            // Гарячі клавіші для швидкого відкриття на ПК/Editor (~ або F1 або F12)
            if (Input.GetKeyDown(KeyCode.BackQuote) || Input.GetKeyDown(KeyCode.F1) || Input.GetKeyDown(KeyCode.F12))
            {
                TogglePanel();
            }

            // Періодичне оновлення статусу, якщо вікно відкрите
            if (isOpen && Time.frameCount % 20 == 0)
            {
                UpdateStatus();
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureEventSystem();
            RefreshLevelList();
            UpdateStatus();
        }

        public void TogglePanel()
        {
            SetPanelOpen(!isOpen);
        }

        public void SetPanelOpen(bool open)
        {
            isOpen = open;
            if (windowRootObj != null)
            {
                windowRootObj.SetActive(isOpen);
            }

            if (isOpen)
            {
                RefreshLevelList();
                UpdateStatus();
                HighlightActiveLevelButton();
                ScrollToActiveLevel();
                StartCoroutine(ScrollToActiveLevelRoutine());
            }
        }

        // ==========================================
        // Побудова процедурного інтерфейсу
        // ==========================================

        private void BuildUI()
        {
            EnsureEventSystem();

            // 1. Створення Canvas
            devCanvas = gameObject.AddComponent<Canvas>();
            devCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            devCanvas.sortingOrder = 9999; // Завжди поверх усіх вікон

            devCanvasScaler = gameObject.AddComponent<CanvasScaler>();
            devCanvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            devCanvasScaler.referenceResolution = new Vector2(1080, 1920);
            devCanvasScaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();

            TMP_FontAsset font = BoosterBadgeUI.GetDefaultTMPFont();

            // 2. Плаваюча кнопка відкриття панелі (DEV)
            BuildFloatingButton(font);

            // 3. Основне вікно DevPanel
            BuildDevWindow(font);

            windowRootObj.SetActive(false);
        }

        private void BuildFloatingButton(TMP_FontAsset font)
        {
            floatingButtonObj = new GameObject("DevFloatingButton");
            floatingButtonObj.transform.SetParent(transform, false);

            floatingButtonRect = floatingButtonObj.AddComponent<RectTransform>();
            floatingButtonRect.anchorMin = new Vector2(0f, 1f);
            floatingButtonRect.anchorMax = new Vector2(0f, 1f);
            floatingButtonRect.pivot = new Vector2(0f, 1f);
            floatingButtonRect.sizeDelta = new Vector2(140f, 65f);
            floatingButtonRect.anchoredPosition = new Vector2(25f, -120f); // Трохи нижче верхнього краю під статус-бар

            floatingButtonBg = floatingButtonObj.AddComponent<Image>();
            floatingButtonBg.sprite = GetRoundedRectSprite();
            floatingButtonBg.type = Image.Type.Sliced;
            floatingButtonBg.color = new Color(0.08f, 0.12f, 0.20f, 0.88f);

            // Обводка кнопки
            GameObject borderObj = new GameObject("Border");
            borderObj.transform.SetParent(floatingButtonObj.transform, false);
            RectTransform borderRect = borderObj.AddComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.sizeDelta = Vector2.zero;
            Image borderImg = borderObj.AddComponent<Image>();
            borderImg.sprite = GetBorderSprite();
            borderImg.type = Image.Type.Sliced;
            borderImg.color = new Color(0.20f, 0.85f, 1.0f, 0.90f);
            borderImg.raycastTarget = false;

            // Текст на кнопці
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(floatingButtonObj.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            floatingButtonText = textObj.AddComponent<TextMeshProUGUI>();
            if (font != null) floatingButtonText.font = font;
            floatingButtonText.text = "🛠️ DEV";
            floatingButtonText.fontSize = 28f;
            floatingButtonText.fontStyle = FontStyles.Bold;
            floatingButtonText.alignment = TextAlignmentOptions.Center;
            floatingButtonText.color = new Color(0.9f, 0.98f, 1f, 1f);
            floatingButtonText.raycastTarget = false;

            Button btn = floatingButtonObj.AddComponent<Button>();
            btn.targetGraphic = floatingButtonBg;
            btn.onClick.AddListener(TogglePanel);

            // Автономний обробник перетягування плаваючої кнопки
            var dragHandler = floatingButtonObj.AddComponent<DevFloatingButtonDragHandler>();
            dragHandler.buttonRect = floatingButtonRect;
            dragHandler.canvas = devCanvas;
        }

        private void BuildDevWindow(TMP_FontAsset font)
        {
            windowRootObj = new GameObject("DevWindow");
            windowRootObj.transform.SetParent(transform, false);

            RectTransform rootRect = windowRootObj.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.sizeDelta = Vector2.zero;

            // Напівпрозорий фон-підкладка (клік по ньому закриває панель)
            Image backdrop = windowRootObj.AddComponent<Image>();
            backdrop.color = new Color(0f, 0f, 0f, 0.65f);
            Button backdropBtn = windowRootObj.AddComponent<Button>();
            backdropBtn.targetGraphic = backdrop;
            backdropBtn.onClick.AddListener(() => SetPanelOpen(false));

            // Центральна картка DevPanel
            GameObject cardObj = new GameObject("Card");
            cardObj.transform.SetParent(windowRootObj.transform, false);

            windowCardRect = cardObj.AddComponent<RectTransform>();
            windowCardRect.anchorMin = new Vector2(0.5f, 0.5f);
            windowCardRect.anchorMax = new Vector2(0.5f, 0.5f);
            windowCardRect.pivot = new Vector2(0.5f, 0.5f);
            windowCardRect.sizeDelta = new Vector2(980f, 1420f);

            Image cardBg = cardObj.AddComponent<Image>();
            cardBg.sprite = GetRoundedRectSprite();
            cardBg.type = Image.Type.Sliced;
            cardBg.color = new Color(0.07f, 0.09f, 0.15f, 0.98f);

            // Зовнішня неонова рамка
            GameObject cardBorderObj = new GameObject("CardBorder");
            cardBorderObj.transform.SetParent(cardObj.transform, false);
            RectTransform cardBorderRect = cardBorderObj.AddComponent<RectTransform>();
            cardBorderRect.anchorMin = Vector2.zero;
            cardBorderRect.anchorMax = Vector2.one;
            cardBorderRect.sizeDelta = Vector2.zero;
            Image cardBorderImg = cardBorderObj.AddComponent<Image>();
            cardBorderImg.sprite = GetBorderSprite();
            cardBorderImg.type = Image.Type.Sliced;
            cardBorderImg.color = new Color(0.25f, 0.75f, 1.0f, 0.60f);
            cardBorderImg.raycastTarget = false;

            // Внутрішній VerticalLayout
            GameObject contentObj = new GameObject("VerticalContent");
            contentObj.transform.SetParent(cardObj.transform, false);
            RectTransform contentRect = contentObj.AddComponent<RectTransform>();
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.sizeDelta = new Vector2(-40f, -40f); // 20px padding
            contentRect.anchoredPosition = Vector2.zero;

            VerticalLayoutGroup vLayout = contentObj.AddComponent<VerticalLayoutGroup>();
            vLayout.padding = new RectOffset(15, 15, 15, 15);
            vLayout.spacing = 16f;
            vLayout.childAlignment = TextAnchor.UpperCenter;
            vLayout.childControlWidth = true;
            vLayout.childControlHeight = false;

            // --- 1. Шапка (Заголовок + Кнопка закриття) ---
            BuildHeader(contentObj.transform, font);

            // --- 2. Статус-бар (Поточний рівень, час, фігури) ---
            BuildStatusBar(contentObj.transform, font);

            // --- 3. Панель швидких дій (Prev, Restart, Next, Win) ---
            BuildQuickActionsBar(contentObj.transform, font);

            // --- 4. Сітка рівнів (Level Selector Grid) ---
            BuildLevelSelectorGrid(contentObj.transform, font);

            // --- 5. Чіти та ресурси (+Монети, +Головоломки, Бустери) ---
            BuildCheatsBar(contentObj.transform, font);
        }

        private void BuildHeader(Transform parent, TMP_FontAsset font)
        {
            GameObject headerObj = new GameObject("HeaderRow");
            headerObj.transform.SetParent(parent, false);
            RectTransform headerRect = headerObj.AddComponent<RectTransform>();
            headerRect.sizeDelta = new Vector2(0f, 65f);

            HorizontalLayoutGroup hLayout = headerObj.AddComponent<HorizontalLayoutGroup>();
            hLayout.childAlignment = TextAnchor.MiddleCenter;
            hLayout.childControlWidth = false;
            hLayout.childControlHeight = true;
            hLayout.spacing = 20f;

            // Заголовок
            GameObject titleObj = new GameObject("TitleText");
            titleObj.transform.SetParent(headerObj.transform, false);
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.sizeDelta = new Vector2(740f, 60f);

            titleText = titleObj.AddComponent<TextMeshProUGUI>();
            if (font != null) titleText.font = font;
            titleText.text = "🛠️ ПАНЕЛЬ РОЗРОБНИКА";
            titleText.fontSize = 38f;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.MidlineLeft;
            titleText.color = new Color(0.35f, 0.88f, 1f, 1f);

            // Кнопка закрити [✕]
            GameObject closeBtnObj = new GameObject("CloseButton");
            closeBtnObj.transform.SetParent(headerObj.transform, false);
            RectTransform closeRect = closeBtnObj.AddComponent<RectTransform>();
            closeRect.sizeDelta = new Vector2(65f, 65f);

            Image closeBg = closeBtnObj.AddComponent<Image>();
            closeBg.sprite = GetRoundedRectSprite();
            closeBg.type = Image.Type.Sliced;
            closeBg.color = new Color(0.75f, 0.2f, 0.25f, 0.85f);

            Button closeBtn = closeBtnObj.AddComponent<Button>();
            closeBtn.targetGraphic = closeBg;
            closeBtn.onClick.AddListener(() => SetPanelOpen(false));

            GameObject closeTextObj = new GameObject("CloseText");
            closeTextObj.transform.SetParent(closeBtnObj.transform, false);
            RectTransform ctRect = closeTextObj.AddComponent<RectTransform>();
            ctRect.anchorMin = Vector2.zero;
            ctRect.anchorMax = Vector2.one;
            ctRect.sizeDelta = Vector2.zero;
            TMP_Text ct = closeTextObj.AddComponent<TextMeshProUGUI>();
            if (font != null) ct.font = font;
            ct.text = "✕";
            ct.fontSize = 32f;
            ct.fontStyle = FontStyles.Bold;
            ct.alignment = TextAlignmentOptions.Center;
            ct.color = Color.white;
            ct.raycastTarget = false;
        }

        private void BuildStatusBar(Transform parent, TMP_FontAsset font)
        {
            GameObject statusObj = new GameObject("StatusBar");
            statusObj.transform.SetParent(parent, false);
            RectTransform statusRect = statusObj.AddComponent<RectTransform>();
            statusRect.sizeDelta = new Vector2(0f, 60f);

            Image statusBg = statusObj.AddComponent<Image>();
            statusBg.sprite = GetRoundedRectSprite();
            statusBg.type = Image.Type.Sliced;
            statusBg.color = new Color(0.12f, 0.16f, 0.26f, 0.85f);

            GameObject textObj = new GameObject("StatusText");
            textObj.transform.SetParent(statusObj.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = new Vector2(-20f, 0f);

            statusText = textObj.AddComponent<TextMeshProUGUI>();
            if (font != null) statusText.font = font;
            statusText.text = "Сцена: Game | Рівень: 1";
            statusText.fontSize = 24f;
            statusText.alignment = TextAlignmentOptions.Center;
            statusText.color = new Color(0.85f, 0.92f, 1f, 1f);
        }

        private void BuildQuickActionsBar(Transform parent, TMP_FontAsset font)
        {
            GameObject actionsRow = new GameObject("QuickActionsRow");
            actionsRow.transform.SetParent(parent, false);
            RectTransform rowRect = actionsRow.AddComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(0f, 85f);

            HorizontalLayoutGroup hLayout = actionsRow.AddComponent<HorizontalLayoutGroup>();
            hLayout.childAlignment = TextAnchor.MiddleCenter;
            hLayout.childControlWidth = true;
            hLayout.childControlHeight = true;
            hLayout.spacing = 14f;

            CreateStyledButton(actionsRow.transform, "◀ Попер.", new Color(0.15f, 0.45f, 0.75f), font, () =>
            {
                if (LevelLoader.HasInstance)
                {
                    LevelLoader.Instance.LoadPrevLevel();
                    UpdateStatus();
                    HighlightActiveLevelButton();
                }
            });

            CreateStyledButton(actionsRow.transform, "↻ Перезапуск", new Color(0.35f, 0.25f, 0.65f), font, () =>
            {
                if (LevelLoader.HasInstance)
                {
                    LevelLoader.Instance.RestartCurrentLevel();
                    UpdateStatus();
                }
            });

            CreateStyledButton(actionsRow.transform, "Наст. ▶", new Color(0.15f, 0.55f, 0.65f), font, () =>
            {
                if (LevelLoader.HasInstance)
                {
                    LevelLoader.Instance.LoadNextLevel();
                    UpdateStatus();
                    HighlightActiveLevelButton();
                }
            });

            CreateStyledButton(actionsRow.transform, "★ Перемога", new Color(0.85f, 0.55f, 0.12f), font, () =>
            {
                if (LevelLoader.HasInstance)
                {
                    LevelLoader.Instance.CompleteCurrentLevel();
                    UpdateStatus();
                }
            });
        }

        private void BuildLevelSelectorGrid(Transform parent, TMP_FontAsset font)
        {
            // Підпис секції
            GameObject labelObj = new GameObject("GridLabel");
            labelObj.transform.SetParent(parent, false);
            RectTransform labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.sizeDelta = new Vector2(0f, 38f);
            TMP_Text lbl = labelObj.AddComponent<TextMeshProUGUI>();
            if (font != null) lbl.font = font;
            lbl.text = "ШВИДКИЙ ПЕРЕХІД НА РІВЕНЬ (Оберіть номер):";
            lbl.fontSize = 24f;
            lbl.fontStyle = FontStyles.Bold;
            lbl.alignment = TextAlignmentOptions.MidlineLeft;
            lbl.color = new Color(0.7f, 0.85f, 1f, 0.95f);

            // ScrollView Container
            GameObject scrollObj = new GameObject("LevelScrollView");
            scrollObj.transform.SetParent(parent, false);
            RectTransform scrollRect = scrollObj.AddComponent<RectTransform>();
            scrollRect.sizeDelta = new Vector2(0f, 630f);

            Image scrollBg = scrollObj.AddComponent<Image>();
            scrollBg.sprite = GetRoundedRectSprite();
            scrollBg.type = Image.Type.Sliced;
            scrollBg.color = new Color(0.04f, 0.06f, 0.10f, 0.85f);

            levelScrollRect = scrollObj.AddComponent<ScrollRect>();
            levelScrollRect.horizontal = false;
            levelScrollRect.vertical = true;
            levelScrollRect.movementType = ScrollRect.MovementType.Clamped;
            levelScrollRect.inertia = true;
            levelScrollRect.decelerationRate = 0.135f;
            levelScrollRect.scrollSensitivity = 50f;

            // Viewport
            GameObject viewportObj = new GameObject("Viewport");
            viewportObj.transform.SetParent(scrollObj.transform, false);
            RectTransform vpRect = viewportObj.AddComponent<RectTransform>();
            vpRect.anchorMin = Vector2.zero;
            vpRect.anchorMax = Vector2.one;
            vpRect.sizeDelta = new Vector2(-36f, -16f); // Запас праворуч під Scrollbar
            vpRect.anchoredPosition = new Vector2(-10f, 0f);

            Image vpMask = viewportObj.AddComponent<Image>();
            vpMask.color = Color.white;
            Mask mask = viewportObj.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            levelScrollRect.viewport = vpRect;

            // Content
            GameObject gridContentObj = new GameObject("Content");
            gridContentObj.transform.SetParent(viewportObj.transform, false);
            RectTransform contentRect = gridContentObj.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = new Vector2(0f, 600f);

            // Прозора підкладка для захоплення кліків/скролу в проміжках між кнопками
            Image contentHitImage = gridContentObj.AddComponent<Image>();
            contentHitImage.color = Color.clear;
            contentHitImage.raycastTarget = true;

            GridLayoutGroup grid = gridContentObj.AddComponent<GridLayoutGroup>();
            grid.padding = new RectOffset(10, 10, 10, 30);
            grid.cellSize = new Vector2(165f, 105f);
            grid.spacing = new Vector2(14f, 14f);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;

            levelScrollRect.content = contentRect;
            levelGridContent = gridContentObj.transform;

            BuildScrollbar(scrollObj.transform);
        }

        private void BuildCheatsBar(Transform parent, TMP_FontAsset font)
        {
            // Підпис секції
            GameObject labelObj = new GameObject("CheatsLabel");
            labelObj.transform.SetParent(parent, false);
            RectTransform labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.sizeDelta = new Vector2(0f, 35f);
            TMP_Text lbl = labelObj.AddComponent<TextMeshProUGUI>();
            if (font != null) lbl.font = font;
            lbl.text = "РЕСУРСИ ТА БУСТЕРИ:";
            lbl.fontSize = 22f;
            lbl.fontStyle = FontStyles.Bold;
            lbl.alignment = TextAlignmentOptions.MidlineLeft;
            lbl.color = new Color(0.7f, 0.85f, 1f, 0.95f);

            // Рядок 1: Монети, Самоцвіти, Життя, Час
            GameObject row1 = new GameObject("CheatsRow1");
            row1.transform.SetParent(parent, false);
            RectTransform r1Rect = row1.AddComponent<RectTransform>();
            r1Rect.sizeDelta = new Vector2(0f, 75f);
            HorizontalLayoutGroup h1 = row1.AddComponent<HorizontalLayoutGroup>();
            h1.childControlWidth = true;
            h1.childControlHeight = true;
            h1.spacing = 10f;

            CreateStyledButton(row1.transform, "+100 🪙", new Color(0.75f, 0.60f, 0.10f), font, () =>
            {
                if (CoinManager.Instance != null)
                {
                    CoinManager.Instance.AddCoins(100);
                    UpdateStatus();
                }
            });

            CreateStyledButton(row1.transform, "+25 💎", new Color(0.18f, 0.70f, 0.85f), font, () =>
            {
                if (GemManager.Instance != null)
                {
                    GemManager.Instance.AddGems(25);
                    UpdateStatus();
                }
            });

            CreateStyledButton(row1.transform, "❤️ 5 Життів", new Color(0.85f, 0.22f, 0.35f), font, () =>
            {
                if (HeartManager.Instance != null)
                {
                    HeartManager.Instance.AddHearts(5);
                    UpdateStatus();
                }
            });

            CreateStyledButton(row1.transform, "+60с ⏱️", new Color(0.25f, 0.65f, 0.35f), font, () =>
            {
                if (LevelLoader.HasInstance)
                {
                    LevelLoader.Instance.AddRemainingTime(60f);
                    UpdateStatus();
                }
            });

            // Рядок 2: Заряди бустерів та Скидання прогресу
            GameObject row2 = new GameObject("CheatsRow2");
            row2.transform.SetParent(parent, false);
            RectTransform r2Rect = row2.AddComponent<RectTransform>();
            r2Rect.sizeDelta = new Vector2(0f, 75f);
            HorizontalLayoutGroup h2 = row2.AddComponent<HorizontalLayoutGroup>();
            h2.childControlWidth = true;
            h2.childControlHeight = true;
            h2.spacing = 10f;

            CreateStyledButton(row2.transform, "+1 🔨 Молот", new Color(0.35f, 0.45f, 0.55f), font, () =>
            {
                AddBoosterCharge(1, 0, 0);
            });

            CreateStyledButton(row2.transform, "+1 💡 Підказка", new Color(0.65f, 0.55f, 0.15f), font, () =>
            {
                AddBoosterCharge(0, 1, 0);
            });

            CreateStyledButton(row2.transform, "+1 ❄️ Заморозка", new Color(0.20f, 0.55f, 0.75f), font, () =>
            {
                AddBoosterCharge(0, 0, 1);
            });

            CreateStyledButton(row2.transform, "🧹 Рівень 1", new Color(0.55f, 0.20f, 0.20f), font, () =>
            {
                JumpToLevel(0);
            });
        }

        private void AddBoosterCharge(int hammer, int hint, int freeze)
        {
            // 1. Оновлюємо та зберігаємо у центральному SaveDataService
            if (SaveDataService.Instance != null && SaveDataService.Instance.Data != null)
            {
                var d = SaveDataService.Instance.Data;
                if (hammer != 0) d.hammerCharges = Mathf.Max(0, d.hammerCharges + hammer);
                if (hint != 0) d.hintCharges = Mathf.Max(0, d.hintCharges + hint);
                if (freeze != 0) d.freezeCharges = Mathf.Max(0, d.freezeCharges + freeze);
                SaveDataService.Instance.Save();
            }

            // 2. Якщо сцена гри активна — оновлюємо інстанси компонентів бустерів у сцені та їхні графічні бейджі
            if (hammer != 0)
            {
                var hammers = FindObjectsByType<HammerBooster>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var h in hammers)
                {
                    if (h != null)
                    {
                        if (SaveDataService.Instance != null && SaveDataService.Instance.Data != null)
                        {
                            h.SetCharges(SaveDataService.Instance.Data.hammerCharges);
                        }
                        else
                        {
                            h.AddCharges(hammer);
                        }
                    }
                }
            }

            if (hint != 0)
            {
                var hints = FindObjectsByType<HintBooster>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var h in hints)
                {
                    if (h != null)
                    {
                        if (SaveDataService.Instance != null && SaveDataService.Instance.Data != null)
                        {
                            h.SetCharges(SaveDataService.Instance.Data.hintCharges);
                        }
                        else
                        {
                            h.AddCharges(hint);
                        }
                    }
                }
            }

            if (freeze != 0)
            {
                var freezes = FindObjectsByType<TimeFreezeBooster>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var f in freezes)
                {
                    if (f != null)
                    {
                        if (SaveDataService.Instance != null && SaveDataService.Instance.Data != null)
                        {
                            f.SetCharges(SaveDataService.Instance.Data.freezeCharges);
                        }
                        else
                        {
                            f.AddCharges(freeze);
                        }
                    }
                }
            }

            UpdateStatus();

            int hCount = SaveDataService.Instance?.Data != null ? SaveDataService.Instance.Data.hammerCharges : 0;
            int hintCount = SaveDataService.Instance?.Data != null ? SaveDataService.Instance.Data.hintCharges : 0;
            int fCount = SaveDataService.Instance?.Data != null ? SaveDataService.Instance.Data.freezeCharges : 0;
            Debug.Log($"<color=cyan>[LevelDevPanel] Заряди бустерів успішно оновлено! Молот={hCount}, Підказка={hintCount}, Заморозка={fCount}</color>");
        }

        // ==========================================
        // Заповнення списку рівнів
        // ==========================================

        private void RefreshLevelList()
        {
            if (levelGridContent == null) return;

            // Очищення старих кнопок
            foreach (Transform child in levelGridContent)
            {
                Destroy(child.gameObject);
            }
            spawnedLevelButtons.Clear();
            spawnedLevelTexts.Clear();
            spawnedLevelBgs.Clear();

            List<LevelData> levels = GetAllAvailableLevels();
            TMP_FontAsset font = BoosterBadgeUI.GetDefaultTMPFont();

            // Точний динамічний розрахунок висоти контенту під кількість рівнів
            int totalLevels = levels.Count;
            int columns = 5;
            int rows = Mathf.CeilToInt(totalLevels / (float)columns);
            float rowHeight = 105f;
            float rowSpacing = 14f;
            float padTop = 10f;
            float padBottom = 30f;
            float calculatedHeight = padTop + padBottom + rows * rowHeight + Mathf.Max(0, rows - 1) * rowSpacing;

            RectTransform contentRect = levelGridContent as RectTransform;
            if (contentRect != null)
            {
                contentRect.sizeDelta = new Vector2(0f, Mathf.Max(calculatedHeight, 620f));
            }

            for (int i = 0; i < levels.Count; i++)
            {
                int levelIndex = i;
                LevelData data = levels[i];
                string levelDisplayName = data != null && !string.IsNullOrEmpty(data.levelTitle)
                    ? data.levelTitle
                    : $"Рівень {levelIndex + 1}";

                GameObject btnObj = new GameObject($"LvlBtn_{levelIndex + 1}");
                btnObj.transform.SetParent(levelGridContent, false);
                RectTransform btnRect = btnObj.AddComponent<RectTransform>();
                btnRect.sizeDelta = new Vector2(165f, 105f);

                Image btnBg = btnObj.AddComponent<Image>();
                btnBg.sprite = GetRoundedRectSprite();
                btnBg.type = Image.Type.Sliced;
                btnBg.color = new Color(0.12f, 0.16f, 0.26f, 0.95f);

                // Рамка кнопки
                GameObject borderObj = new GameObject("Border");
                borderObj.transform.SetParent(btnObj.transform, false);
                RectTransform bRect = borderObj.AddComponent<RectTransform>();
                bRect.anchorMin = Vector2.zero;
                bRect.anchorMax = Vector2.one;
                bRect.sizeDelta = Vector2.zero;
                Image borderImg = borderObj.AddComponent<Image>();
                borderImg.sprite = GetBorderSprite();
                borderImg.type = Image.Type.Sliced;
                borderImg.color = new Color(0.35f, 0.55f, 0.85f, 0.50f);
                borderImg.raycastTarget = false;

                // Вертикальний контейнер тексту
                GameObject textObj = new GameObject("Text");
                textObj.transform.SetParent(btnObj.transform, false);
                RectTransform textRect = textObj.AddComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.sizeDelta = Vector2.zero;

                TMP_Text txt = textObj.AddComponent<TextMeshProUGUI>();
                if (font != null) txt.font = font;
                txt.text = $"<size=34><b>{levelIndex + 1}</b></size>\n<size=18><color=#88bbdd>{data?.name ?? $"Lvl {levelIndex + 1}"}</color></size>";
                txt.alignment = TextAlignmentOptions.Center;
                txt.color = Color.white;
                txt.raycastTarget = false;

                Button btn = btnObj.AddComponent<Button>();
                btn.targetGraphic = btnBg;
                btn.onClick.AddListener(() =>
                {
                    JumpToLevel(levelIndex);
                });

                // Перенаправлення Drag і Scroll подій до ScrollRect (гарантує скрол на сенсорних екранах і мишкою)
                var forwarder = btnObj.AddComponent<ScrollRectDragForwarder>();
                forwarder.targetScrollRect = levelScrollRect;

                spawnedLevelButtons.Add(btn);
                spawnedLevelTexts.Add(txt);
                spawnedLevelBgs.Add(btnBg);
            }

            if (contentRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
            }

            HighlightActiveLevelButton();
        }

        private List<LevelData> GetAllAvailableLevels()
        {
            List<LevelData> list;
            if (LevelLoader.HasInstance && LevelLoader.Instance.AllLevels != null && LevelLoader.Instance.AllLevels.Count > 0)
            {
                list = new List<LevelData>(LevelLoader.Instance.AllLevels);
            }
            else
            {
                LevelData[] loaded = Resources.LoadAll<LevelData>("Levels");
                list = loaded != null ? new List<LevelData>(loaded) : new List<LevelData>();
            }

            list.RemoveAll(l => l == null);
            list.Sort((a, b) =>
            {
                int ai = a.levelIndex > 0 ? a.levelIndex : 0;
                int bi = b.levelIndex > 0 ? b.levelIndex : 0;
                if (ai != bi) return ai.CompareTo(bi);
                return string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
            });
            return list;
        }

        public void JumpToLevel(int targetIndex)
        {
            Debug.Log($"<color=cyan><b>[LevelDevPanel]</b> Перехід на рівень #{targetIndex + 1}...</color>");

            // Зберігаємо індекс рівня в збереженні
            if (SaveDataService.Instance != null && SaveDataService.Instance.Data != null)
            {
                SaveDataService.Instance.Data.currentLevelIndex = targetIndex;
                SaveDataService.Instance.Save();
            }

            // Якщо ми на сцені гри
            if (LevelLoader.HasInstance)
            {
                LevelLoader.Instance.LoadLevelByIndex(targetIndex);
                SetPanelOpen(false); // Авто-закриваємо для зручності тестування
            }
            else
            {
                // Якщо ми в головному меню - завантажуємо сцену гри
                SceneManager.LoadScene("Game");
                SetPanelOpen(false);
            }

            UpdateStatus();
        }

        private void HighlightActiveLevelButton()
        {
            int currentIdx = GetCurrentActiveLevelIndex();

            for (int i = 0; i < spawnedLevelBgs.Count; i++)
            {
                if (spawnedLevelBgs[i] == null) continue;

                Transform border = spawnedLevelBgs[i].transform.Find("Border");
                Image borderImg = border != null ? border.GetComponent<Image>() : null;

                if (i == currentIdx)
                {
                    // Активний рівень: яскравий золотаво-блакитний колір
                    spawnedLevelBgs[i].color = new Color(0.18f, 0.35f, 0.55f, 1f);
                    if (borderImg != null)
                    {
                        borderImg.color = new Color(1f, 0.85f, 0.20f, 1f); // Золота рамка
                    }
                }
                else
                {
                    spawnedLevelBgs[i].color = new Color(0.10f, 0.14f, 0.22f, 0.95f);
                    if (borderImg != null)
                    {
                        borderImg.color = new Color(0.35f, 0.55f, 0.85f, 0.45f);
                    }
                }
            }
        }

        private int GetCurrentActiveLevelIndex()
        {
            if (LevelLoader.HasInstance)
            {
                return LevelLoader.Instance.CurrentLevelIndex;
            }

            if (SaveDataService.Instance != null && SaveDataService.Instance.Data != null)
            {
                return SaveDataService.Instance.Data.currentLevelIndex;
            }

            return PlayerPrefs.GetInt("LEGO_CurrentLevelIndex", 0);
        }

        private void UpdateStatus()
        {
            if (statusText == null) return;

            string sceneName = SceneManager.GetActiveScene().name;
            int currentIdx = GetCurrentActiveLevelIndex();
            int totalLevels = GetAllAvailableLevels().Count;

            string extra = "";
            if (LevelLoader.HasInstance)
            {
                int remaining = Mathf.CeilToInt(LevelLoader.Instance.RemainingTime);
                int m = remaining / 60;
                int s = remaining % 60;
                extra = $" | Час: {m:00}:{s:00} | Блоки: {LevelLoader.Instance.PiecesExited}/{LevelLoader.Instance.RequiredPiecesToWin}";
            }

            int coins = SaveDataService.Instance != null && SaveDataService.Instance.Data != null ? SaveDataService.Instance.Data.coins : 0;
            int gems = SaveDataService.Instance != null && SaveDataService.Instance.Data != null ? SaveDataService.Instance.Data.gems : 0;
            int hCharges = SaveDataService.Instance != null && SaveDataService.Instance.Data != null ? SaveDataService.Instance.Data.hammerCharges : 0;
            int hintCharges = SaveDataService.Instance != null && SaveDataService.Instance.Data != null ? SaveDataService.Instance.Data.hintCharges : 0;
            int fCharges = SaveDataService.Instance != null && SaveDataService.Instance.Data != null ? SaveDataService.Instance.Data.freezeCharges : 0;

            statusText.text = $"<b>Рівень {currentIdx + 1}/{totalLevels}</b> | 🪙 {coins}  💎 {gems} | 🔨 {hCharges}  💡 {hintCharges}  ❄️ {fCharges}";

            if (floatingButtonText != null)
            {
                floatingButtonText.text = $"🛠️ Lvl {currentIdx + 1}";
            }
        }

        // ==========================================
        // Скрол до активного рівня
        // ==========================================

        public void ScrollToActiveLevel()
        {
            if (levelScrollRect == null || levelGridContent == null) return;

            int currentIdx = GetCurrentActiveLevelIndex();
            int totalLevels = spawnedLevelButtons.Count;
            if (totalLevels <= 0) return;

            int columns = 5;
            int totalRows = Mathf.CeilToInt(totalLevels / (float)columns);
            if (totalRows <= 1)
            {
                levelScrollRect.verticalNormalizedPosition = 1f;
                return;
            }

            int activeRow = currentIdx / columns;
            // 1f = верх (перші рівні), 0f = низ (останні рівні)
            float normPos = 1f - (float)activeRow / Mathf.Max(1, totalRows - 1);
            levelScrollRect.verticalNormalizedPosition = Mathf.Clamp01(normPos);
        }

        private IEnumerator ScrollToActiveLevelRoutine()
        {
            yield return null; // Очікуємо 1 кадр завершення розрахунку Canvas
            ScrollToActiveLevel();
        }

        private void BuildScrollbar(Transform parent)
        {
            GameObject sbObj = new GameObject("ScrollbarVertical");
            sbObj.transform.SetParent(parent, false);
            RectTransform sbRect = sbObj.AddComponent<RectTransform>();
            sbRect.anchorMin = new Vector2(1f, 0f);
            sbRect.anchorMax = new Vector2(1f, 1f);
            sbRect.pivot = new Vector2(1f, 0.5f);
            sbRect.sizeDelta = new Vector2(16f, -24f);
            sbRect.anchoredPosition = new Vector2(-8f, 0f);

            Image sbBg = sbObj.AddComponent<Image>();
            sbBg.sprite = GetRoundedRectSprite();
            sbBg.type = Image.Type.Sliced;
            sbBg.color = new Color(0.1f, 0.15f, 0.25f, 0.6f);

            Scrollbar sb = sbObj.AddComponent<Scrollbar>();
            sb.direction = Scrollbar.Direction.BottomToTop;

            GameObject slideArea = new GameObject("SlidingArea");
            slideArea.transform.SetParent(sbObj.transform, false);
            RectTransform saRect = slideArea.AddComponent<RectTransform>();
            saRect.anchorMin = Vector2.zero;
            saRect.anchorMax = Vector2.one;
            saRect.sizeDelta = new Vector2(-4f, -4f);

            GameObject handle = new GameObject("Handle");
            handle.transform.SetParent(slideArea.transform, false);
            RectTransform hRect = handle.AddComponent<RectTransform>();
            hRect.anchorMin = Vector2.zero;
            hRect.anchorMax = Vector2.one;
            hRect.sizeDelta = Vector2.zero;

            Image handleImg = handle.AddComponent<Image>();
            handleImg.sprite = GetRoundedRectSprite();
            handleImg.type = Image.Type.Sliced;
            handleImg.color = new Color(0.35f, 0.85f, 1f, 0.85f);

            sb.handleRect = hRect;
            sb.targetGraphic = handleImg;

            levelScrollRect.verticalScrollbar = sb;
            levelScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            levelScrollRect.verticalScrollbarSpacing = 4f;
        }

        // ==========================================
        // Допоміжні графічні процедурні утиліти
        // ==========================================

        private Button CreateStyledButton(Transform parent, string title, Color baseColor, TMP_FontAsset font, UnityEngine.Events.UnityAction onClick)
        {
            GameObject btnObj = new GameObject($"Btn_{title}");
            btnObj.transform.SetParent(parent, false);

            RectTransform rt = btnObj.AddComponent<RectTransform>();
            Image bg = btnObj.AddComponent<Image>();
            bg.sprite = GetRoundedRectSprite();
            bg.type = Image.Type.Sliced;
            bg.color = baseColor;

            // Border
            GameObject bObj = new GameObject("Border");
            bObj.transform.SetParent(btnObj.transform, false);
            RectTransform bRect = bObj.AddComponent<RectTransform>();
            bRect.anchorMin = Vector2.zero;
            bRect.anchorMax = Vector2.one;
            bRect.sizeDelta = Vector2.zero;
            Image bImg = bObj.AddComponent<Image>();
            bImg.sprite = GetBorderSprite();
            bImg.type = Image.Type.Sliced;
            bImg.color = Color.Lerp(baseColor, Color.white, 0.40f);
            bImg.raycastTarget = false;

            // Text
            GameObject txtObj = new GameObject("Text");
            txtObj.transform.SetParent(btnObj.transform, false);
            RectTransform tRect = txtObj.AddComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.sizeDelta = Vector2.zero;

            TMP_Text txt = txtObj.AddComponent<TextMeshProUGUI>();
            if (font != null) txt.font = font;
            txt.text = title;
            txt.fontSize = 24f;
            txt.fontStyle = FontStyles.Bold;
            txt.alignment = TextAlignmentOptions.Center;
            txt.color = Color.white;
            txt.raycastTarget = false;

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = bg;
            if (onClick != null) btn.onClick.AddListener(onClick);

            return btn;
        }

        private static Sprite GetRoundedRectSprite()
        {
            if (cachedRoundedRectSprite != null) return cachedRoundedRectSprite;

            const int size = 64;
            const int radius = 16;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "DevPanel_RoundedRect",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int cx = x < radius ? radius : (x >= size - radius ? size - radius - 1 : x);
                    int cy = y < radius ? radius : (y >= size - radius ? size - radius - 1 : y);
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                    float alpha = Mathf.Clamp01(radius - dist + 0.5f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            // 9-slice border
            Vector4 border = new Vector4(radius, radius, radius, radius);
            cachedRoundedRectSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            return cachedRoundedRectSprite;
        }

        private static Sprite GetBorderSprite()
        {
            if (cachedBorderSprite != null) return cachedBorderSprite;

            const int size = 64;
            const int radius = 16;
            const float stroke = 3f;

            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "DevPanel_Border",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int cx = x < radius ? radius : (x >= size - radius ? size - radius - 1 : x);
                    int cy = y < radius ? radius : (y >= size - radius ? size - radius - 1 : y);
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));

                    float outer = Mathf.Clamp01(radius - dist + 0.5f);
                    float inner = Mathf.Clamp01((radius - stroke) - dist + 0.5f);
                    float alpha = Mathf.Clamp01(outer - inner);

                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            Vector4 border = new Vector4(radius, radius, radius, radius);
            cachedBorderSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            return cachedBorderSprite;
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                GameObject es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }
        }
    }

    /// <summary>
    /// Локальний обробник перетягування для плаваючої кнопки DEV
    /// </summary>
    public class DevFloatingButtonDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public RectTransform buttonRect;
        public Canvas canvas;

        private Vector2 dragOffset;
        private bool isDragging;

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (buttonRect == null) return;
            isDragging = true;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                buttonRect,
                eventData.position,
                eventData.pressEventCamera,
                out dragOffset
            );
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (buttonRect == null || !isDragging) return;

            RectTransform parentRect = buttonRect.parent as RectTransform;
            if (parentRect == null) return;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint))
            {
                Vector2 targetPos = localPoint - dragOffset;

                Rect pRect = parentRect.rect;
                float minX = 0f;
                float maxX = Mathf.Max(0f, pRect.width - buttonRect.rect.width);
                float minY = -Mathf.Max(0f, pRect.height - buttonRect.rect.height);
                float maxY = 0f;

                targetPos.x = Mathf.Clamp(targetPos.x, minX, maxX);
                targetPos.y = Mathf.Clamp(targetPos.y, minY, maxY);

                buttonRect.anchoredPosition = targetPos;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            isDragging = false;
        }
    }

    /// <summary>
    /// Перенаправляє події драгу та скролу від елементів списку (кнопок) до ScrollRect
    /// </summary>
    public class ScrollRectDragForwarder : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler, IInitializePotentialDragHandler
    {
        public ScrollRect targetScrollRect;

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            if (targetScrollRect != null)
            {
                targetScrollRect.OnInitializePotentialDrag(eventData);
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (targetScrollRect != null)
            {
                targetScrollRect.OnBeginDrag(eventData);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (targetScrollRect != null)
            {
                targetScrollRect.OnDrag(eventData);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (targetScrollRect != null)
            {
                targetScrollRect.OnEndDrag(eventData);
            }
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (targetScrollRect != null)
            {
                targetScrollRect.OnScroll(eventData);
            }
        }
    }
}
