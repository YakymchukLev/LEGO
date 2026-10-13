using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LegoPuzzle.Data;

namespace LegoPuzzle.Runtime
{
    public class LevelLoader : MonoBehaviour
    {
        [Header("Конфігурація")]
        [SerializeField] private BlockPalette palette;
        [SerializeField] private LevelData testLevelData;
        [Tooltip("Розмір однієї клітинки сітки (підганяйте під масштаб ваших 3D-моделей)")]
        [SerializeField] private float cellSize = 1f;
        public float CellSize => cellSize;

        [Tooltip("Поворот 3D-моделей блоків (якщо вони лежать горизонтально, ставте -90 по X або 0)")]
        [SerializeField] private Vector3 blockModelRotation = new Vector3(0f, 0f, 0f);

        [Header("Камера")]
        [Tooltip("Автоматично центрувати та підганяти зум камери під розмір поля (вимкніть, якщо позиціонуєте камеру вручну)")]
        [SerializeField] private bool autoCenterCamera = true;
        [SerializeField] private Camera gameCamera;

        [Header("Адаптивне масштабування за розміром рівня")]
        [Tooltip("Автоматично визначати реальні межі активних тайлів та блоків рівня (ігнорує порожні зони та центрує точно по грі)")]
        [SerializeField] private bool autoDetectLevelBounds = true;

        [Tooltip("Динамічно змінювати масштаб: підтягувати малі та великі рівні для оптимального заповнення екрана")]
        [SerializeField] private bool dynamicScaleByLevelSize = true;

        [Tooltip("Частка ширини екрана для малих рівнів (<= 6 клітинок). Наприклад, 0.94 = 94% ширини, значно наближає малі рівні")]
        [Range(0.6f, 0.98f)]
        [SerializeField] private float smallLevelScreenWidthRatio = 0.94f;

        [Tooltip("Частка висоти екрана для малих рівнів (<= 6 клітинок)")]
        [Range(0.5f, 0.92f)]
        [SerializeField] private float smallLevelScreenHeightRatio = 0.82f;

        [Tooltip("Частка ширини екрана для великих рівнів (>= 12 клітинок). Наприклад, 0.93 = 93% ширини, наближає великі рівні")]
        [Range(0.5f, 0.98f)]
        [SerializeField] private float largeLevelScreenWidthRatio = 0.93f;

        [Tooltip("Частка висоти екрана для великих рівнів (>= 12 клітинок). Захищає від перекриття верхнім таймером та нижніми бустерами")]
        [Range(0.4f, 0.88f)]
        [SerializeField] private float largeLevelScreenHeightRatio = 0.74f;

        [Tooltip("Пороговий розмір малого рівня в клітинках")]
        [SerializeField] private float smallLevelThreshold = 6f;

        [Tooltip("Пороговий розмір великого рівня в клітинках")]
        [SerializeField] private float largeLevelThreshold = 12f;

        [Tooltip("Множник загального наближення (1.0 = норма, > 1.0 наближає ще ближче до поля)")]
        [Range(0.8f, 1.5f)]
        [SerializeField] private float cameraZoomMultiplier = 1.0f;

        [Tooltip("Додатковий відступ навколо поля у клітинках (додає трохи простору по краях)")]
        [Range(0f, 0.5f)]
        [SerializeField] private float boardPaddingCells = 0.05f;

        [Header("Статичні параметри (коли динамічне масштабування вимкнено)")]
        [Tooltip("Частка ширини екрана, яку займає поле")]
        [Range(0.5f, 0.98f)]
        [SerializeField] private float targetScreenWidthRatio = 0.94f;

        [Tooltip("Максимальна частка висоти екрана, яку займає поле")]
        [Range(0.4f, 0.92f)]
        [SerializeField] private float targetScreenHeightRatio = 0.76f;

        [Header("Зсув та кут камери (3D-вигляд)")]
        [Tooltip("Кут нахилу камери по осі X у градусах (90 = вигляд строго зверху 2D, 65-75 = нахил під кутом для об'ємного 3D вигляду деталей і виступів)")]
        [Range(45f, 90f)]
        [SerializeField] private float cameraPitchAngle = 70f;

        [Tooltip("Кут повороту камери навколо вертикальної осі Y у градусах (0 = строго прямо)")]
        [Range(-30f, 30f)]
        [SerializeField] private float cameraYawAngle = 0f;

        public float CameraPitchAngle
        {
            get => (cameraPitchAngle > 20f) ? cameraPitchAngle : 70f;
            set
            {
                cameraPitchAngle = Mathf.Clamp(value, 30f, 90f);
                RefreshCameraFraming();
            }
        }

        public float CameraYawAngle
        {
            get => cameraYawAngle;
            set
            {
                cameraYawAngle = Mathf.Clamp(value, -45f, 45f);
                RefreshCameraFraming();
            }
        }

        [Tooltip("Додатковий зсув камери по осі Z у світових одиницях (+Z опускає поле нижче на екрані для звільнення місця таймеру, -Z піднімає вище)")]
        [SerializeField] private float cameraVerticalOffset = 0f;

        [Tooltip("Мінімальна дистанція камери (запобігає надмірному наближенню на мікро-рівнях)")]
        [SerializeField] private float minCameraDistance = 3.5f;

        [Tooltip("Максимальна дистанція камери")]
        [SerializeField] private float maxCameraDistance = 45f;

        [Tooltip("Чи використовувати плавний перехід камери при зміні рівня чи оновленні")]
        [SerializeField] private bool smoothCameraTransition = false;

        [Tooltip("Тривалість плавного переходу камери у секундах")]
        [SerializeField] private float cameraTransitionDuration = 0.35f;

        [Header("Батьківські контейнери (Опціонально)")]
        [SerializeField] private Transform boardContainer;
        [SerializeField] private Transform piecesContainer;

        [Header("Відео-фон (Опціонально)")]
        [Tooltip("Автоматично вмикати та відтворювати фонове відео на рівні (BackGroundVideo.mp4)")]
        [SerializeField] private bool enableVideoBackground = true;

        [Tooltip("Компонент відео-фону (якщо не вказано, шукається на сцені або створюється автоматично)")]
        [SerializeField] private LevelVideoBackground videoBackground;

        [Header("Звукові ефекти (SFX)")]
        [Tooltip("Перший звук переміщення деталі при утримуванні та русі (чергується)")]
        [SerializeField] private AudioClip pieceMoveSound1;

        [Tooltip("Другий звук переміщення деталі при утримуванні та русі (чергується)")]
        [SerializeField] private AudioClip pieceMoveSound2;

        [Tooltip("Звук кроку/зміни клітинки блоком (шарудіння/клацання)")]
        [SerializeField] private AudioClip pieceStepSound;

        [Tooltip("Звук вильоту блоку у ворота")]
        [SerializeField] private AudioClip pieceExitSound;

        [Tooltip("Звук перемоги на рівні")]
        [SerializeField] private AudioClip levelWonSound;

        [Tooltip("Звук поразки на рівні (коли закінчився час)")]
        [SerializeField] private AudioClip levelLostSound;

        [SerializeField] private AudioSource audioSource;

        [Header("UI Панелі (Опціонально)")]
        [Tooltip("Панель перемоги (з'являється автоматично, коли всі блоки вийшли у ворота)")]
        [SerializeField] private GameObject winPanel;

        [Tooltip("Панель поразки (з'являється, якщо закінчився ліміт часу)")]
        [SerializeField] private GameObject losePanel;

        [Tooltip("Затримка перед показом панелі перемоги (секунди, щоб встиг дограти виліт блоку)")]
        [SerializeField] private float winPanelDelay = 0.5f;

        [Header("UI Верхня Панель (Upper Panel)")]
        [Tooltip("Текстовий елемент TextMeshPro для відображення номера рівня (наприклад, 'Level 1' або 'LVL 1')")]
        [SerializeField] private TMP_Text levelNumberTextTMP;

        [Tooltip("Текстовий елемент Legacy Text для номера рівня (зворотна сумісність)")]
        [SerializeField] private Text levelNumberTextLegacy;

        [Tooltip("Формат тексту номера рівня (наприклад: 'Level {0}', 'LVL {0}' або 'Рівень {0}'). Якщо порожньо, береться з levelTitle.")]
        [SerializeField] private string levelTextFormat = "Level {0}";

        [Tooltip("Кнопка паузи у верхній панелі для повернення на сцену меню")]
        [SerializeField] private Button pauseButton;

        [Tooltip("Назва сцени, на яку повертає гравець при натисканні кнопки паузи (за замовчуванням 'Menu')")]
        [SerializeField] private string pauseMenuSceneName = "Menu";

        [Header("UI Таймер (In-Game HUD)")]

        [Tooltip("Текстовий елемент TextMeshPro для відображення зворотного відліку (01:35)")]
        [SerializeField] private TMP_Text timerTextTMP;

        [Tooltip("Текстовий елемент Legacy Text для зворотної сумісності")]
        [SerializeField] private Text timerTextLegacy;

        [Tooltip("Батьківський контейнер віджета таймера (плашка/іконка)")]
        [SerializeField] private GameObject timerContainer;

        [Tooltip("Колір таймера у звичайному стані")]
        [SerializeField] private Color timerNormalColor = Color.white;

        [Tooltip("Колір таймера, коли залишається мало часу (<= 10 сек)")]
        [SerializeField] private Color timerWarningColor = new Color(1f, 0.28f, 0.28f, 1f);

        [Tooltip("Коефіцієнт збільшення тексту при пульсації (наприклад, 1.30 = збільшення на 30%)")]
        [SerializeField] private float timerPulseScaleMultiplier = 1.3f;

        [Tooltip("Тривалість анімації збільшення та повернення тексту (у секундах)")]
        [SerializeField] private float timerPulseDuration = 0.55f;

        [Header("Бустер Заморозки Часу (Time Freeze Booster)")]
        [Tooltip("Тривалість дії бустера заморозки часу (у секундах)")]
        [SerializeField] private float freezeDuration = 10f;

        [Tooltip("Колір тексту таймера під час заморозки (крижаний блакитний)")]
        [SerializeField] private Color timerFreezeColor = new Color(0.35f, 0.85f, 1f, 1f);

        [Tooltip("Чи додавати іконки сніжинки ❄ до таймера під час заморозки")]
        [SerializeField] private bool showSnowflakeIcon = true;

        [Tooltip("Звук активації заморозки часу (опціонально)")]
        [SerializeField] private AudioClip freezeSound;

        [Tooltip("Звук завершення дії заморозки часу (опціонально)")]
        [SerializeField] private AudioClip unfreezeSound;

        [Header("Туторіал (Навчання)")]
        [Tooltip("Префаб руки-показчика для 1-го рівня (якщо порожньо, буде створено базовий спрайт)")]
        [SerializeField] private GameObject tutorialHandPrefab;
        public GameObject TutorialHandPrefab => tutorialHandPrefab;

        [Tooltip("Показувати туторіал лише на 1-му рівні")]
        [SerializeField] private bool showTutorialOnFirstLevel = true;

        [Header("Список рівнів та Прогрес")]
        [Tooltip("Список усіх LevelData гри по порядку (Level_001, Level_002, ...)")]
        [SerializeField] private List<LevelData> allLevels = new List<LevelData>();
        [SerializeField] private int currentLevelIndex = 0;

        public static LevelLoader Instance { get; private set; }
        public static bool HasInstance => Instance != null;

        public int CurrentLevelIndex => currentLevelIndex;

        [Tooltip("Автоматично зберігати номер останнього пройденого рівня між переходами в меню")]
        [SerializeField] private bool saveProgress = true;

        [Tooltip("Примусово завантажувати тільки TestLevelData (для швидкого тестування)")]
        [SerializeField] private bool overrideWithTestLevel = false;

        private const string PREFS_LEVEL_INDEX = "LEGO_CurrentLevelIndex";

        public LevelData CurrentLevel { get; private set; }
        public bool IsGameplayActive { get; private set; } = false;
        public float RemainingTime { get; private set; }

        private readonly Dictionary<Vector2Int, GridCellView> spawnedCells = new Dictionary<Vector2Int, GridCellView>();
        private readonly List<LegoPieceView> activePieces = new List<LegoPieceView>();
        public IReadOnlyList<LegoPieceView> ActivePieces => activePieces;
        private int requiredPiecesToWin = 0;
        private int piecesExited = 0;
        private float lastStepSoundTime = 0f;
        private int lastScreenWidth;
        private int lastScreenHeight;

        public int RequiredPiecesToWin => requiredPiecesToWin;
        public int PiecesExited => piecesExited;
        public float LevelProgress => requiredPiecesToWin > 0 ? Mathf.Clamp01((float)piecesExited / requiredPiecesToWin) : 0f;
        public Transform BoardContainer => boardContainer;
        public float BoardBaseY => (boardContainer != null) ? boardContainer.position.y : transform.position.y;

        public bool IsTimeFrozen { get; private set; } = false;
        public float RemainingFreezeTime { get; private set; } = 0f;
        public float FreezeDuration => freezeDuration;

        public Camera GameCamera { get => gameCamera; set => gameCamera = value; }
        public bool AutoCenterCamera => autoCenterCamera;
        public bool AutoDetectLevelBounds => autoDetectLevelBounds;
        public bool DynamicScaleByLevelSize => dynamicScaleByLevelSize;
        public float SmallLevelScreenWidthRatio => smallLevelScreenWidthRatio;
        public float SmallLevelScreenHeightRatio => smallLevelScreenHeightRatio;
        public float LargeLevelScreenWidthRatio => largeLevelScreenWidthRatio;
        public float LargeLevelScreenHeightRatio => largeLevelScreenHeightRatio;
        public float SmallLevelThreshold => smallLevelThreshold;
        public float LargeLevelThreshold => largeLevelThreshold;
        public float CameraZoomMultiplier => cameraZoomMultiplier;
        public float BoardPaddingCells => boardPaddingCells;
        public float TargetScreenWidthRatio => targetScreenWidthRatio;
        public float TargetScreenHeightRatio => targetScreenHeightRatio;
        public float CameraVerticalOffset => cameraVerticalOffset;
        public float MinCameraDistance => minCameraDistance;
        public float MaxCameraDistance => maxCameraDistance;
        public bool SmoothCameraTransition => smoothCameraTransition;
        public float CameraTransitionDuration => cameraTransitionDuration;
        public bool EnableVideoBackground => enableVideoBackground;
        public LevelVideoBackground VideoBackground { get => videoBackground; set => videoBackground = value; }
        public BlockPalette Palette => palette;
        public LevelData TestLevelData => testLevelData;
        public IReadOnlyList<LevelData> AllLevels => allLevels;
        public AudioClip PieceExitSound => pieceExitSound;
        public AudioSource AudioSource => audioSource;
        public IReadOnlyDictionary<Vector2Int, GridCellView> SpawnedCells => spawnedCells;

        private LevelCameraController cameraController;
        public LevelCameraController CameraController
        {
            get
            {
                if (cameraController == null)
                {
                    cameraController = GetComponent<LevelCameraController>();
                    if (cameraController == null && gameObject != null)
                    {
                        cameraController = gameObject.AddComponent<LevelCameraController>();
                    }
                    if (cameraController != null) cameraController.Initialize(this);
                }
                return cameraController;
            }
        }

        private LevelMovementValidator movementValidator;
        public LevelMovementValidator MovementValidator
        {
            get
            {
                if (movementValidator == null)
                {
                    movementValidator = GetComponent<LevelMovementValidator>();
                    if (movementValidator == null && gameObject != null)
                    {
                        movementValidator = gameObject.AddComponent<LevelMovementValidator>();
                    }
                    if (movementValidator != null) movementValidator.Initialize(this);
                }
                return movementValidator;
            }
        }

        private LevelBoosterService boosterService;
        public LevelBoosterService BoosterService
        {
            get
            {
                if (boosterService == null)
                {
                    boosterService = GetComponent<LevelBoosterService>();
                    if (boosterService == null && gameObject != null)
                    {
                        boosterService = gameObject.AddComponent<LevelBoosterService>();
                    }
                    if (boosterService != null) boosterService.Initialize(this);
                }
                return boosterService;
            }
        }

        public event Action<float> OnTimeFrozen;
        public event Action OnTimeUnfrozen;

        public event Action<int> OnLevelLoaded;
        public event Action<float> OnTimerUpdated;
        public event Action OnLevelWon;
        public event Action OnLevelLost;
        public event Action OnFirstMoveMade;

        public bool HasFirstMoveOccurred { get; private set; } = false;

        private bool isLevelWon = false;

        /// <summary>
        /// Starts the level countdown timer upon the player's first block movement.
        /// </summary>
        public void NotifyBlockMoved()
        {
            if (HasFirstMoveOccurred) return;
            HasFirstMoveOccurred = true;
            OnFirstMoveMade?.Invoke();
            
            // Вимикаємо туторіал, якщо він був активний
            TutorialHandEffect.Dismiss();

            Debug.Log("<color=green>[LevelLoader] First block move detected! Level timer started.</color>");
        }

        private void EnsureServices()
        {
            _ = CameraController;
            _ = MovementValidator;
            _ = BoosterService;
        }

        private void Awake()
        {
            Instance = this;
            EnsureServices();
            EnsureCamera();

            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;

            EnsurePalette();
            EnsureEventSystem();
            EnsureAudioSource();
            EnsureUIElements();
            EnsureVideoBackground();

            // Автоматично додаємо менеджер туторіалів бустерів, якщо його немає
            if (GetComponent<BoosterTutorialManager>() == null)
            {
                gameObject.AddComponent<BoosterTutorialManager>();
            }

            if (winPanel != null) winPanel.SetActive(false);
            if (losePanel != null) losePanel.SetActive(false);
        }

        private void EnsureCamera()
        {
            if (gameCamera == null)
            {
                gameCamera = CameraController.EnsureCamera();
            }
        }

        private void EnsureAudioSource()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
            }
            audioSource.playOnAwake = false;

            if (GameSettingsManager.HasInstance)
            {
                audioSource.mute = !GameSettingsManager.Instance.SoundEnabled;
                GameSettingsManager.Instance.OnSettingsChanged -= UpdateAudioFromSettings;
                GameSettingsManager.Instance.OnSettingsChanged += UpdateAudioFromSettings;
                GameSettingsManager.Instance.OnColorblindModeChanged -= HandleColorblindModeChanged;
                GameSettingsManager.Instance.OnColorblindModeChanged += HandleColorblindModeChanged;
            }
        }

        private void UpdateAudioFromSettings()
        {
            if (audioSource != null && GameSettingsManager.HasInstance)
            {
                audioSource.mute = !GameSettingsManager.Instance.SoundEnabled;
            }
        }

        private void HandleColorblindModeChanged(bool enabled)
        {
            RefreshAllBlockAndGateColors();
        }

        /// <summary>
        /// Updates the colors of all active pieces and exit gates in the level (e.g. when toggling Colorblind Mode).
        /// </summary>
        public void RefreshAllBlockAndGateColors()
        {
            if (activePieces != null)
            {
                foreach (var piece in activePieces)
                {
                    if (piece != null) piece.RefreshColor();
                }
            }

            if (spawnedCells != null)
            {
                foreach (var cell in spawnedCells.Values)
                {
                    if (cell != null) cell.RefreshGateColor();
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            if (GameSettingsManager.HasInstance)
            {
                GameSettingsManager.Instance.OnSettingsChanged -= UpdateAudioFromSettings;
                GameSettingsManager.Instance.OnColorblindModeChanged -= HandleColorblindModeChanged;
            }
        }

        private void OnValidate()
        {
            EnsurePalette();
            EnsureCamera();
#if UNITY_EDITOR
            if (autoCenterCamera && gameCamera != null)
            {
                RefreshCameraFraming();
            }
#endif
        }

        private void EnsurePalette()
        {
            if (palette == null)
            {
                palette = Resources.Load<BlockPalette>("MainBlockPalette");
                if (palette == null)
                {
                    palette = Resources.Load<BlockPalette>("BlockPalette");
                }

                if (palette == null)
                {
                    var palettes = Resources.FindObjectsOfTypeAll<BlockPalette>();
                    if (palettes != null && palettes.Length > 0)
                    {
                        palette = palettes[0];
                    }
                }

#if UNITY_EDITOR
                if (palette == null)
                {
                    string[] guids = UnityEditor.AssetDatabase.FindAssets("t:BlockPalette");
                    if (guids.Length > 0)
                    {
                        string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                        palette = UnityEditor.AssetDatabase.LoadAssetAtPath<BlockPalette>(path);
                    }
                }
#endif
            }
        }

        private void EnsureEventSystem()
        {
            // Перевіряємо наявність PhysicsRaycaster на камері
            if (gameCamera != null && gameCamera.GetComponent<UnityEngine.EventSystems.PhysicsRaycaster>() == null)
            {
                gameCamera.gameObject.AddComponent<UnityEngine.EventSystems.PhysicsRaycaster>();
            }

            // Перевіряємо наявність EventSystem на сцені
            if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject eventSystemObj = new GameObject("EventSystem");
                eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystemObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
        }

        private void Start()
        {
            EnsurePalette();

            if (overrideWithTestLevel && testLevelData != null)
            {
                Debug.Log($"<color=cyan>LevelLoader: Loading test level '{testLevelData.name}' (Override enabled).</color>");
                LoadLevel(testLevelData);
                return;
            }

            EnsureAllLevelsPopulated();

            if (allLevels != null && allLevels.Count > 0)
            {
                allLevels.RemoveAll(l => l == null);
            }

            if (allLevels != null && allLevels.Count > 0)
            {
                if (saveProgress)
                {
                    if (SaveDataService.Instance != null && SaveDataService.Instance.Data != null)
                    {
                        currentLevelIndex = SaveDataService.Instance.Data.currentLevelIndex;
                    }
                    else
                    {
                        currentLevelIndex = PlayerPrefs.GetInt(PREFS_LEVEL_INDEX, 0);
                    }
                }

                if (currentLevelIndex < 0 || currentLevelIndex >= allLevels.Count)
                {
                    currentLevelIndex = 0;
                }

                LevelData levelToLoad = allLevels[currentLevelIndex];
                if (levelToLoad == null)
                {
                    for (int i = 0; i < allLevels.Count; i++)
                    {
                        if (allLevels[i] != null)
                        {
                            currentLevelIndex = i;
                            levelToLoad = allLevels[i];
                            break;
                        }
                    }
                }

                if (levelToLoad != null)
                {
                    Debug.Log($"<color=cyan>LevelLoader: Loading level #{currentLevelIndex + 1} ('{levelToLoad.name}').</color>");
                    LoadLevel(levelToLoad);
                }
            }
            else if (testLevelData != null)
            {
                LoadLevel(testLevelData);
            }
            else
            {
                Debug.LogWarning("LevelLoader: 'All Levels' list is empty! Add LevelData in inspector or Right Click -> 'Auto-populate all LevelData in project'.");
            }
        }

        private void Update()
        {
            if (autoCenterCamera && CurrentLevel != null)
            {
                if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
                {
                    lastScreenWidth = Screen.width;
                    lastScreenHeight = Screen.height;
                    CenterCamera(CurrentLevel, true);
                }
            }

            if (!IsGameplayActive) return;

            if (CurrentLevel != null && CurrentLevel.timeLimitSeconds > 0)
            {
                // Таймер не починає відлік, доки гравець не здійснить свій перший рух блоком
                if (!HasFirstMoveOccurred) return;

                if (IsTimeFrozen)
                {
                    RemainingFreezeTime -= Time.deltaTime;
                    if (RemainingFreezeTime <= 0f)
                    {
                        UnfreezeTime();
                    }
                    else
                    {
                        UpdateTimerUI(RemainingTime);
                    }
                }
                else
                {
                    RemainingTime -= Time.deltaTime;

                    if (RemainingTime <= 0f)
                    {
                        RemainingTime = 0f;
                        IsGameplayActive = false;
                        UpdateTimerUI(0f);
                        OnTimerUpdated?.Invoke(0f);
                        PlayLevelLostSound();
                        OnLevelLost?.Invoke();
                        Debug.Log("<color=red>Time's up! Level failed.</color>");

                        // Deduct heart on level loss
                        if (HeartManager.Instance != null)
                        {
                            HeartManager.Instance.DeductHeart();
                        }

                        if (losePanel != null)
                        {
                            UIButtonPressEffect.AttachToAllIn(losePanel);
                            UIPanelSlideIn.AttachTo(losePanel).EnableImpactShake(true);
                            UILostPanelProgress.AttachTo(losePanel);
                            UILegoBrickDebris.AttachTo(losePanel);
                            AttachPrimaryButtonPolish(losePanel);
                            losePanel.SetActive(true);
                        }
                    }
                    else
                    {
                        UpdateTimerUI(RemainingTime);
                        OnTimerUpdated?.Invoke(RemainingTime);
                    }
                }
            }
        }

        public void LoadLevel(LevelData levelData)
        {
            if (levelData == null)
            {
                Debug.LogError("LevelData is null!");
                return;
            }

            EnsurePalette();
            CurrentLevel = levelData;

            if (allLevels != null && allLevels.Count > 0)
            {
                int foundIndex = allLevels.IndexOf(levelData);
                if (foundIndex >= 0)
                {
                    currentLevelIndex = foundIndex;
                }
            }

            isTransitioningLevel = false;
            isClaimingWinReward = false;

            // Ховаємо UI панелі результату перед початком рівня
            if (winPanel != null) winPanel.SetActive(false);
            if (losePanel != null) losePanel.SetActive(false);

            // Кнопка повернення в меню активна під час гри на рівні
            if (pauseButton != null)
            {
                pauseButton.interactable = true;
            }


            ClearBoard();
            DismissMoveHint();

            RemainingTime = levelData.timeLimitSeconds;
            IsTimeFrozen = false;
            RemainingFreezeTime = 0f;
            HasFirstMoveOccurred = false;
            piecesExited = 0;
            requiredPiecesToWin = 0;
            isLevelWon = false;

            if (levelData.timeLimitSeconds > 0)
            {
                if (timerContainer != null) timerContainer.SetActive(true);
                else if (timerTextTMP != null) timerTextTMP.gameObject.SetActive(true);
                else if (timerTextLegacy != null) timerTextLegacy.gameObject.SetActive(true);

                UpdateTimerUI(RemainingTime);
            }
            else
            {
                if (timerContainer != null) timerContainer.SetActive(false);
                else if (timerTextTMP != null) timerTextTMP.gameObject.SetActive(false);
                else if (timerTextLegacy != null) timerTextLegacy.gameObject.SetActive(false);
            }

            BuildGrid(levelData);
            SpawnPieces(levelData);
            CenterCamera(levelData);

            UpdateLevelNumberUI(levelData);

            IsGameplayActive = true;

            OnLevelLoaded?.Invoke(levelData.levelIndex);

            // Перевіряємо, чи є блоки, які одразу стоять впритик до воріт свого кольору
            CheckAndTriggerAutoExits();

            // Перевіряємо, чи потрібно показати туторіал на 1 рівні
            EnsureFirstLevelTutorialHand();
        }

        /// <summary>
        /// Гарантує відображення руки-підказки на 1-му рівні, доки гравець дійсно не здійснить хід.
        /// </summary>
        public void EnsureFirstLevelTutorialHand()
        {
            if (showTutorialOnFirstLevel && (currentLevelIndex == 0 || (CurrentLevel != null && CurrentLevel.levelIndex == 1)) && !HasFirstMoveOccurred)
            {
                if (TutorialHandEffect.ActiveInstance == null)
                {
                    if (FindBestHintMove(out LegoPieceView tutorialPiece, out Vector2Int tutorialDelta, out bool _))
                    {
                        TutorialHandEffect.Show(tutorialPiece, tutorialDelta, cellSize, tutorialHandPrefab);
                    }
                }
            }
        }

        public void RestartCurrentLevel()
        {
            // If restarting mid-game during active play, deduct a heart
            if (IsGameplayActive)
            {
                if (HeartManager.Instance != null)
                {
                    HeartManager.Instance.DeductHeart();
                }
                IsGameplayActive = false;
            }

            // Verify if player has hearts to play
            if (HeartManager.Instance != null && !HeartManager.Instance.HasHearts())
            {
                Debug.Log("<color=yellow>LevelLoader: No hearts left to restart level! Redirecting to Menu.</color>");
                LoadMenuScene();
                return;
            }

            if (CurrentLevel != null)
            {
                LoadLevel(CurrentLevel);
            }
            else if (allLevels != null && allLevels.Count > 0)
            {
                LoadLevel(allLevels[currentLevelIndex]);
            }
        }

        private bool isTransitioningLevel = false;

        public void LoadNextLevel()
        {
            if (isTransitioningLevel) return;
            isTransitioningLevel = true;

            EnsureAllLevelsPopulated();

            if (allLevels != null && allLevels.Count > 0)
            {
                allLevels.RemoveAll(l => l == null);
            }

            if (allLevels != null && allLevels.Count > 0)
            {
                currentLevelIndex = (currentLevelIndex + 1) % allLevels.Count;
                if (saveProgress)
                {
                    if (SaveDataService.Instance != null && SaveDataService.Instance.Data != null)
                    {
                        SaveDataService.Instance.Data.currentLevelIndex = currentLevelIndex;
                        SaveDataService.Instance.Save();
                    }
                    else
                    {
                        PlayerPrefs.SetInt(PREFS_LEVEL_INDEX, currentLevelIndex);
                        PlayerPrefs.Save();
                    }
                }

                LoadLevel(allLevels[currentLevelIndex]);
            }
            else if (CurrentLevel != null)
            {
                RestartCurrentLevel();
            }

            StartCoroutine(ResetTransitioningLevelFlag());
        }

        private IEnumerator ResetTransitioningLevelFlag()
        {
            yield return new WaitForSecondsRealtime(0.4f);
            isTransitioningLevel = false;
        }

        public void LoadLevelByIndex(int index)
        {
            if (allLevels != null && index >= 0 && index < allLevels.Count)
            {
                currentLevelIndex = index;
                if (saveProgress)
                {
                    if (SaveDataService.Instance != null && SaveDataService.Instance.Data != null)
                    {
                        SaveDataService.Instance.Data.currentLevelIndex = currentLevelIndex;
                        SaveDataService.Instance.Save();
                    }
                    else
                    {
                        PlayerPrefs.SetInt(PREFS_LEVEL_INDEX, currentLevelIndex);
                        PlayerPrefs.Save();
                    }
                }
                LoadLevel(allLevels[currentLevelIndex]);
            }
        }

        public void LoadPrevLevel()
        {
            EnsureAllLevelsPopulated();
            if (allLevels == null || allLevels.Count == 0) return;
            int prev = currentLevelIndex - 1;
            if (prev < 0) prev = allLevels.Count - 1;
            LoadLevelByIndex(prev);
        }

        public void CompleteCurrentLevel()
        {
            if (isLevelWon || requiredPiecesToWin <= 0) return;
            piecesExited = requiredPiecesToWin - 1;
            HandleRequiredPieceVictoryCheck(false);
        }

        public void AddRemainingTime(float seconds)
        {
            if (!IsGameplayActive) return;
            RemainingTime += seconds;
            UpdateTimerUI(RemainingTime);
        }

        public void UpdateTimerUI() => UpdateTimerUI(RemainingTime);

        public void OnPauseButtonClicked()
        {
            if (GameSettingsManager.HasInstance)
            {
                GameSettingsManager.Instance.PlayClickSound();
            }

            Time.timeScale = 1f;
            if (IsGameplayActive)
            {
                if (HeartManager.Instance != null)
                {
                    HeartManager.Instance.DeductHeart();
                }
                IsGameplayActive = false;
            }
            LoadMenuScene();
        }

        public void LoadMenuScene()
        {
            if (GameSettingsManager.HasInstance)
            {
                GameSettingsManager.Instance.PlayClickSound();
            }

            Time.timeScale = 1f;
            if (IsGameplayActive)
            {
                if (HeartManager.Instance != null)
                {
                    HeartManager.Instance.DeductHeart();
                }
                IsGameplayActive = false;
            }

            if (!string.IsNullOrEmpty(pauseMenuSceneName))
            {
                try
                {
                    UnityEngine.SceneManagement.SceneManager.LoadScene(pauseMenuSceneName);
                    return;
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"LevelLoader: Failed to load scene '{pauseMenuSceneName}' by name ({ex.Message}), falling back to build index 0.");
                }
            }
            UnityEngine.SceneManagement.SceneManager.LoadScene(0);
        }


        public void LoadSceneByIndex(int sceneIndex)
        {
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene(sceneIndex);
        }

        public void LoadSceneByName(string sceneName)
        {
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
        }

        private void ClearBoard()
        {
            foreach (var cell in spawnedCells.Values)
            {
                if (cell != null) Destroy(cell.gameObject);
            }
            spawnedCells.Clear();

            foreach (var piece in activePieces)
            {
                if (piece != null) Destroy(piece.gameObject);
            }
            activePieces.Clear();
        }

        private void BuildGrid(LevelData levelData)
        {
            EnsurePalette();
            levelData.EnsureGridCapacity();

            Transform parent = boardContainer != null ? boardContainer : transform;

            for (int y = 0; y < levelData.gridHeight; y++)
            {
                for (int x = 0; x < levelData.gridWidth; x++)
                {
                    CellData cellData = levelData.GetCell(x, y);
                    if (cellData.cellType == CellType.Empty) continue;

                    GameObject prefab = cellData.cellType switch
                    {
                        CellType.Obstacle => palette?.obstacleTilePrefab,
                        CellType.HalfObstacle => (palette?.halfObstaclePrefab != null) ? palette.halfObstaclePrefab : palette?.obstacleTilePrefab,
                        CellType.QuarterObstacle => (palette?.quarterObstaclePrefab != null) ? palette.quarterObstaclePrefab : (palette?.halfObstaclePrefab != null ? palette.halfObstaclePrefab : palette?.obstacleTilePrefab),
                        CellType.ExitGate => palette?.exitGatePrefab,
                        CellType.HalfExitGate => (palette?.halfExitGatePrefab != null) ? palette.halfExitGatePrefab : palette?.exitGatePrefab,
                        _ => palette?.walkableTilePrefab
                    };

                    GameObject cellObj;
                    if (prefab != null)
                    {
                        cellObj = Instantiate(prefab, parent);
                    }
                    else
                    {
                        cellObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
                        cellObj.transform.SetParent(parent);
                        Collider col = cellObj.GetComponent<Collider>();
                        if (col != null)
                        {
                            Destroy(col);
                        }
                    }

                    cellObj.name = $"Cell_{x}_{y}_{cellData.cellType}";
                    cellObj.transform.localPosition = new Vector3(x * cellSize, -0.01f, y * cellSize);
                    cellObj.transform.localRotation = Quaternion.identity;
                    cellObj.transform.localScale = Vector3.one * cellSize;

                    GridCellView cellView = cellObj.GetComponent<GridCellView>();
                    if (cellView == null)
                    {
                        cellView = cellObj.AddComponent<GridCellView>();
                    }

                    cellView.Initialize(cellData, palette, cellSize);
                    spawnedCells[new Vector2Int(x, y)] = cellView;
                }
            }
        }

        private void SpawnPieces(LevelData levelData)
        {
            Transform parent = piecesContainer != null ? piecesContainer : transform;

            foreach (var pieceData in levelData.pieces)
            {
                // Обираємо 3D-префаб: з форми або дефолтний з палітри
                GameObject prefabToSpawn = null;
                if (pieceData.shape != null && pieceData.shape.prefab != null)
                {
                    prefabToSpawn = pieceData.shape.prefab;
                }
                else if (palette != null && palette.defaultLegoPiecePrefab != null)
                {
                    prefabToSpawn = palette.defaultLegoPiecePrefab;
                }

                // Створюємо чистий корінь деталі на сітці
                GameObject pieceObj = new GameObject($"Piece_{pieceData.pieceId}");
                pieceObj.transform.SetParent(parent);

                // Якщо є 3D-префаб, спавнимо його як дочірній об'єкт Model
                GameObject visualChild = null;
                if (prefabToSpawn != null)
                {
                    visualChild = Instantiate(prefabToSpawn, pieceObj.transform);
                    visualChild.name = "Model";

                    // Очищуємо від фізичних тіл Rigidbody та сторонніх скриптів префабу, щоб уникнути дрейфу
                    var rbs = visualChild.GetComponentsInChildren<Rigidbody>(true);
                    foreach (var rb in rbs)
                    {
                        rb.isKinematic = true;
                        rb.detectCollisions = false;
                        Destroy(rb);
                    }

                    var oldCols = visualChild.GetComponentsInChildren<Collider>(true);
                    foreach (var c in oldCols)
                    {
                        c.enabled = false;
                        Destroy(c);
                    }

                    var oldMono = visualChild.GetComponentsInChildren<MonoBehaviour>(true);
                    foreach (var m in oldMono)
                    {
                        m.enabled = false;
                        Destroy(m);
                    }
                }

                LegoPieceView pieceView = pieceObj.AddComponent<LegoPieceView>();
                pieceView.Initialize(pieceData, this, palette, cellSize, visualChild);
                pieceView.OnExited += HandlePieceExited;
                activePieces.Add(pieceView);

                if (pieceData.isRequiredForWin)
                {
                    requiredPiecesToWin++;
                }
            }
        }

        // =========================================================================
        // Camera & Framing (Делегування до LevelCameraController)
        // =========================================================================

        public LevelActiveBounds GetLevelActiveBounds(LevelData levelData) => CameraController.GetLevelActiveBounds(levelData);

        private void CenterCamera(LevelData levelData, bool forceInstant = false)
        {
            CameraController.CenterCamera(levelData, forceInstant);
        }

        private void EnsureVideoBackground()
        {
            CameraController.EnsureVideoBackground();
        }

        public void RefreshCameraFraming()
        {
            CameraController.RefreshCameraFraming();
        }


        // =========================================================================
        // Movement & Collision Validation (Делегування до LevelMovementValidator)
        // =========================================================================

        public bool CanMovePieceTo(LegoPieceView movingPiece, Vector2Int newOrigin) => MovementValidator.CanMovePieceTo(movingPiece, newOrigin);

        public void GetSlideRangeHorizontal(LegoPieceView piece, Vector2Int currentOrigin, out int minStepX, out int maxStepX)
        {
            MovementValidator.GetSlideRangeHorizontal(piece, currentOrigin, out minStepX, out maxStepX);
        }

        public void GetSlideRangeVertical(LegoPieceView piece, Vector2Int currentOrigin, out int minStepY, out int maxStepY)
        {
            MovementValidator.GetSlideRangeVertical(piece, currentOrigin, out minStepY, out maxStepY);
        }

        public void GetSlideRange(LegoPieceView piece, Vector2Int currentOrigin, out int minStepX, out int maxStepX, out int minStepY, out int maxStepY)
        {
            MovementValidator.GetSlideRange(piece, currentOrigin, out minStepX, out maxStepX, out minStepY, out maxStepY);
        }

        public bool CheckIfPieceExits(LegoPieceView piece, Vector2Int currentOrigin, out ExitDirection exitDirection)
        {
            return MovementValidator.CheckIfPieceExits(piece, currentOrigin, out exitDirection);
        }

        public bool ColorsMatch(Color a, Color b)
        {
            return MovementValidator.ColorsMatch(a, b);
        }

        /// <summary>
        /// Сповіщає ворота, через які виходить блок, про запуск соковитої анімації (сплеск, спалах, ударна хвиля).
        /// </summary>
        public void NotifyGateReaction(LegoPieceView piece, ExitDirection dir)
        {
            if (piece == null || piece.PieceData == null) return;
            var occupied = piece.GetCurrentOccupiedCells();
            if (occupied == null || occupied.Count == 0) return;

            Color pieceColor = piece.PieceData.GetColor();

            HashSet<GridCellView> triggeredGates = new HashSet<GridCellView>();
            foreach (var cell in occupied)
            {
                Vector2Int checkPos = dir switch
                {
                    ExitDirection.Up => new Vector2Int(cell.x, cell.y + 1),
                    ExitDirection.Down => new Vector2Int(cell.x, cell.y - 1),
                    ExitDirection.Left => new Vector2Int(cell.x - 1, cell.y),
                    ExitDirection.Right => new Vector2Int(cell.x + 1, cell.y),
                    _ => cell
                };

                if (spawnedCells.TryGetValue(checkPos, out GridCellView gateView))
                {
                    if ((gateView.CellType == CellType.ExitGate || gateView.CellType == CellType.HalfExitGate) && triggeredGates.Add(gateView))
                    {
                        gateView.PlayGateExitReaction(dir, pieceColor);
                    }
                }
                else if (spawnedCells.TryGetValue(cell, out GridCellView curGateView))
                {
                    if ((curGateView.CellType == CellType.ExitGate || curGateView.CellType == CellType.HalfExitGate) && triggeredGates.Add(curGateView))
                    {
                        curGateView.PlayGateExitReaction(dir, pieceColor);
                    }
                }
            }
        }

        private Coroutine autoExitCoroutine;

        /// <summary>
        /// Автоматично перевіряє всі блоки на полі. Якщо блок стоїть поруч впритик до відповідних воріт напряму,
        /// він виводиться негайно без зайвих дій гравця.
        /// </summary>
        public void CheckAndTriggerAutoExits()
        {
            if (!gameObject.activeInHierarchy || isLevelWon) return;
            if (autoExitCoroutine != null) StopCoroutine(autoExitCoroutine);
            autoExitCoroutine = StartCoroutine(AutoExitRoutine());
        }

        private IEnumerator AutoExitRoutine()
        {
            yield return new WaitForSeconds(0.08f);

            for (int i = 0; i < activePieces.Count; i++)
            {
                var piece = activePieces[i];
                if (piece != null && piece.gameObject.activeSelf && !piece.IsExiting)
                {
                    if (CheckIfPieceExits(piece, piece.CurrentOrigin, out ExitDirection dir))
                    {
                        piece.TriggerAutoExit(dir);
                        yield return new WaitForSeconds(0.14f);
                    }
                }
            }

            autoExitCoroutine = null;
        }

        private int moveSoundToggle = 0;

        public void PlayPieceMoveSound()
        {
            NotifyBlockMoved();

            if (Time.time - lastStepSoundTime < 0.05f) return;
            lastStepSoundTime = Time.time;

            if (GameSettingsManager.HasInstance)
            {
                GameSettingsManager.Instance.TriggerHapticMove();
            }

            AudioClip sound1 = pieceMoveSound1 != null ? pieceMoveSound1 : palette?.pieceMoveSound1;
            AudioClip sound2 = pieceMoveSound2 != null ? pieceMoveSound2 : palette?.pieceMoveSound2;

            AudioClip clip = null;
            if (sound1 != null && sound2 != null)
            {
                clip = (moveSoundToggle++ % 2 == 0) ? sound1 : sound2;
            }
            else if (sound1 != null)
            {
                clip = sound1;
            }
            else if (sound2 != null)
            {
                clip = sound2;
            }
            else
            {
                clip = pieceStepSound != null ? pieceStepSound : palette?.pieceStepSound;
            }

            if (clip != null && audioSource != null)
            {
                audioSource.pitch = UnityEngine.Random.Range(0.96f, 1.04f);
                audioSource.PlayOneShot(clip, 0.9f);
            }
        }

        public void PlayPieceStepSound()
        {
            PlayPieceMoveSound();
        }

        public void PlayPieceExitSound()
        {
            AudioClip clip = pieceExitSound != null ? pieceExitSound : palette?.pieceExitSound;
            if (clip != null && audioSource != null)
            {
                audioSource.pitch = UnityEngine.Random.Range(0.98f, 1.02f);
                audioSource.PlayOneShot(clip, 1f);
            }
        }

        public void PlayLevelWonSound()
        {
            AudioClip clip = levelWonSound != null ? levelWonSound : palette?.levelWonSound;
            if (clip != null && audioSource != null)
            {
                audioSource.pitch = 1f;
                audioSource.PlayOneShot(clip, 1f);
            }
        }

        public void PlayLevelLostSound()
        {
            AudioClip clip = levelLostSound != null ? levelLostSound : palette?.levelWonSound;
            if (clip != null && audioSource != null)
            {
                audioSource.pitch = 0.82f;
                audioSource.PlayOneShot(clip, 1f);
            }
        }

        /// <summary>
        /// Активація бустера заморозки часу (можна викликати безпосередньо з OnClick кнопки у Inspector)
        /// </summary>
        public void ActivateFreezeTime()
        {
            FreezeTime(freezeDuration);
        }

        /// <summary>
        /// Заморозити зворотний відлік часу на вказану кількість секунд
        /// </summary>
        public void FreezeTime(float seconds)
        {
            if (!IsGameplayActive || CurrentLevel == null || CurrentLevel.timeLimitSeconds <= 0f) return;

            IsTimeFrozen = true;
            RemainingFreezeTime = Mathf.Max(RemainingFreezeTime + seconds, seconds);

            if (freezeSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(freezeSound, 1f);
            }

            UpdateTimerUI(RemainingTime);
            OnTimeFrozen?.Invoke(RemainingFreezeTime);
            Debug.Log($"<color=cyan>❄ Бустер заморозки активовано на {seconds} сек! (Залишилось: {RemainingFreezeTime:F1}с)</color>");
        }

        /// <summary>
        /// Дострокове розморожування часу
        /// </summary>
        public void UnfreezeTime()
        {
            if (!IsTimeFrozen) return;

            IsTimeFrozen = false;
            RemainingFreezeTime = 0f;

            if (unfreezeSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(unfreezeSound, 1f);
            }

            UpdateTimerUI(RemainingTime);
            OnTimeUnfrozen?.Invoke();
            Debug.Log("<color=yellow>❄ Дія заморозки часу завершилася. Зворотний відлік відновлено!</color>");
        }

        public void PlayBlockCrackSound()
        {
            EnsureAudioSource();
            if (freezeSound != null && audioSource != null)
            {
                audioSource.pitch = UnityEngine.Random.Range(1.2f, 1.45f);
                audioSource.PlayOneShot(freezeSound, 0.9f);
            }
            else
            {
                PlayPieceMoveSound();
            }
        }

        public void PlayBlockUnfreezeSound()
        {
            EnsureAudioSource();
            if (unfreezeSound != null && audioSource != null)
            {
                audioSource.pitch = 1.0f;
                audioSource.PlayOneShot(unfreezeSound, 1f);
            }
            else if (freezeSound != null && audioSource != null)
            {
                audioSource.pitch = 0.85f;
                audioSource.PlayOneShot(freezeSound, 1f);
            }
            else
            {
                PlayPieceExitSound();
            }
        }

        public void UpdateTimerUI(float remainingSeconds)
        {
            int clamped = Mathf.Max(0, Mathf.CeilToInt(remainingSeconds));
            int minutes = clamped / 60;
            int seconds = clamped % 60;
            string formatted = $"{minutes:00}:{seconds:00}";

            Color currentColor = timerNormalColor;
            float scaleX = 1f;
            float scaleY = 1f;

            if (IsTimeFrozen)
            {
                // Оформлення таймера в крижаному стилі під час заморозки часу
                if (showSnowflakeIcon)
                {
                    formatted = $"❄ {formatted} ❄";
                }

                if (RemainingFreezeTime <= 3f)
                {
                    // Останні 3 секунди: помітне блимання, яке сигналізує про завершення заморозки
                    bool blink = Mathf.Sin(RemainingFreezeTime * 14f) > 0f;
                    currentColor = blink ? timerFreezeColor : Color.white;
                    float pulse = 1f + 0.12f * Mathf.Abs(Mathf.Sin(RemainingFreezeTime * 8f));
                    scaleX = pulse;
                    scaleY = pulse;
                }
                else
                {
                    // М'яке крижане дихання
                    float breath = Mathf.Sin(Time.time * 3f) * 0.05f;
                    currentColor = Color.Lerp(timerFreezeColor, Color.white, 0.2f + 0.2f * Mathf.Sin(Time.time * 2.5f));
                    scaleX = 1.05f + breath;
                    scaleY = 1.05f + breath;
                }
            }
            else if (remainingSeconds <= 10f && remainingSeconds > 0f)
            {
                // Частка часу поточної секунди (від 0.0 на початку кожної секунди до 1.0 в кінці)
                float fraction = remainingSeconds - Mathf.Floor(remainingSeconds);
                float elapsedInSecond = 1f - fraction;

                if (elapsedInSecond < timerPulseDuration && timerPulseDuration > 0f)
                {
                    float t = elapsedInSecond / timerPulseDuration; // від 0.0 до 1.0

                    if (t < 0.5f)
                    {
                        // 1. Спочатку збільшується по вертикалі
                        float p1 = t / 0.5f; // 0..1
                        float bumpY = Mathf.Sin(p1 * Mathf.PI);
                        scaleY = 1f + bumpY * (timerPulseScaleMultiplier - 1f);
                        scaleX = 1f;
                    }
                    else
                    {
                        // 2. Потім збільшується по горизонталі
                        float p2 = (t - 0.5f) / 0.5f; // 0..1
                        float bumpX = Mathf.Sin(p2 * Mathf.PI);
                        scaleX = 1f + bumpX * (timerPulseScaleMultiplier - 1f);
                        scaleY = 1f;
                    }

                    float colorPulse = Mathf.Sin(t * Mathf.PI);
                    currentColor = Color.Lerp(timerNormalColor, timerWarningColor, 0.4f + 0.6f * colorPulse);
                }
                else
                {
                    // 3. У кінці повертається в початковий стан
                    currentColor = timerWarningColor;
                }
            }
            else if (remainingSeconds <= 0f)
            {
                currentColor = timerWarningColor;
                scaleX = 1f;
                scaleY = 1f;
            }

            Vector3 targetScale = new Vector3(scaleX, scaleY, 1f);

            if (timerTextTMP != null)
            {
                timerTextTMP.text = formatted;
                timerTextTMP.color = currentColor;
                timerTextTMP.transform.localScale = targetScale;
            }
            if (timerTextLegacy != null)
            {
                timerTextLegacy.text = formatted;
                timerTextLegacy.color = currentColor;
                timerTextLegacy.transform.localScale = targetScale;
            }
        }

        public void EnsureUIElements()
        {
            Canvas canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null) return;

            // 1. Пошук або прив'язка Timer HUD
            if (timerTextTMP == null && timerTextLegacy == null)
            {
                var tmpTexts = canvas.GetComponentsInChildren<TMP_Text>(true);
                foreach (var t in tmpTexts)
                {
                    if (t.gameObject.name.IndexOf("Timer", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        t.gameObject.name.IndexOf("Time", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        timerTextTMP = t;
                        if (timerContainer == null && t.transform.parent != null && t.transform.parent != canvas.transform)
                        {
                            timerContainer = t.transform.parent.gameObject;
                        }
                        break;
                    }
                }

                if (timerTextTMP == null)
                {
                    var legacyTexts = canvas.GetComponentsInChildren<Text>(true);
                    foreach (var t in legacyTexts)
                    {
                        if (t.gameObject.name.IndexOf("Timer", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            t.gameObject.name.IndexOf("Time", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            timerTextLegacy = t;
                            if (timerContainer == null && t.transform.parent != null && t.transform.parent != canvas.transform)
                            {
                                timerContainer = t.transform.parent.gameObject;
                            }
                            break;
                        }
                    }
                }
            }

            // 2. Пошук або прив'язка Lose Panel
            if (losePanel == null)
            {
                for (int i = 0; i < canvas.transform.childCount; i++)
                {
                    Transform child = canvas.transform.GetChild(i);
                    string lower = child.name.ToLowerInvariant();
                    if (lower.Contains("lost") || lower.Contains("lose") || lower.Contains("defeat") || lower.Contains("fail"))
                    {
                        losePanel = child.gameObject;
                        break;
                    }
                }
            }

            // 3. Якщо панелі поразки немає навіть на сцені - створюємо її процедурно
            if (losePanel == null)
            {
                CreateRuntimeLostPanel(canvas);
            }
            else
            {
                HookupLostPanelButtons(losePanel);
            }

            if (winPanel != null)
            {
                UIButtonPressEffect.AttachToAllIn(winPanel);
                UIPanelSlideIn.AttachTo(winPanel);
                UIConfettiEffect.AttachTo(winPanel);
                AttachPrimaryButtonPolish(winPanel);
                HookupWinPanelButtons(winPanel);
            }
            if (losePanel != null)
            {
                UIButtonPressEffect.AttachToAllIn(losePanel);
                UIPanelSlideIn.AttachTo(losePanel).EnableImpactShake(true);
                UILostPanelProgress.AttachTo(losePanel);
                UILegoBrickDebris.AttachTo(losePanel);
                AttachPrimaryButtonPolish(losePanel);
            }

            // 4. Якщо таймера немає на сцені - створюємо його процедурно
            if (timerTextTMP == null && timerTextLegacy == null)
            {
                CreateRuntimeTimerHUD(canvas);
            }

            // 5. Пошук або прив'язка елементів Upper Panel (кнопка паузи та номер рівня)
            EnsureUpperPanelElements(canvas);

            // 6. Гарантування наявності панелі магазину бустерів під Canvas
            EnsureBoosterShopPanel(canvas);

            // 7. Гарантування наявності бейджів балансу на кнопках бустерів
            EnsureBoosterBadges();
        }

        private void EnsureBoosterBadges()
        {
            var freeze = FindAnyObjectByType<TimeFreezeBooster>(FindObjectsInactive.Include);
            if (freeze != null) freeze.EnsureChargesBadge();

            var hint = FindAnyObjectByType<HintBooster>(FindObjectsInactive.Include);
            if (hint != null) hint.EnsureChargesBadge();

            var hammer = FindAnyObjectByType<HammerBooster>(FindObjectsInactive.Include);
            if (hammer != null) hammer.EnsureChargesBadge();
        }

        private void EnsureBoosterShopPanel(Canvas canvas)
        {
            if (canvas == null) return;
            var shop = canvas.GetComponentInChildren<BoosterShopPanel>(true);
            if (shop == null)
            {
                var shopObj = new GameObject("BoosterShopPanel", typeof(RectTransform));
                shopObj.transform.SetParent(canvas.transform, false);
                shop = shopObj.AddComponent<BoosterShopPanel>();
            }
            shop.EnsureUIHierarchy();
            shop.gameObject.SetActive(false);
        }

        private void EnsureUpperPanelElements(Canvas canvas)
        {
            if (canvas == null) return;

            Transform upperPanel = null;
            for (int i = 0; i < canvas.transform.childCount; i++)
            {
                Transform child = canvas.transform.GetChild(i);
                string name = child.name.ToLowerInvariant();
                if (name.Contains("upper") || name.Contains("top"))
                {
                    upperPanel = child;
                    break;
                }
            }

            // 1. Пошук кнопки паузи
            if (pauseButton == null)
            {
                if (upperPanel != null)
                {
                    var buttons = upperPanel.GetComponentsInChildren<Button>(true);
                    foreach (var btn in buttons)
                    {
                        if (btn.gameObject.name.IndexOf("Pause", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            btn.gameObject.name.IndexOf("Menu", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            pauseButton = btn;
                            break;
                        }
                    }
                    if (pauseButton == null && buttons.Length > 0)
                    {
                        pauseButton = buttons[0];
                    }
                }

                if (pauseButton == null)
                {
                    var allButtons = canvas.GetComponentsInChildren<Button>(true);
                    foreach (var btn in allButtons)
                    {
                        if (btn.gameObject.name.IndexOf("Pause", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            pauseButton = btn;
                            break;
                        }
                    }
                }
            }

            if (pauseButton != null)
            {
                UIButtonPressEffect.AttachTo(pauseButton.gameObject);
                pauseButton.onClick.RemoveListener(OnPauseButtonClicked);
                pauseButton.onClick.AddListener(OnPauseButtonClicked);
                pauseButton.interactable = IsGameplayActive;
            }


            // 2. Пошук тексту номера рівня
            if (levelNumberTextTMP == null && levelNumberTextLegacy == null)
            {
                if (upperPanel != null)
                {
                    var tmpTexts = upperPanel.GetComponentsInChildren<TMP_Text>(true);
                    foreach (var t in tmpTexts)
                    {
                        if (timerTextTMP != null && t == timerTextTMP) continue;
                        if (t.gameObject.name.IndexOf("Timer", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            t.gameObject.name.IndexOf("Time", StringComparison.OrdinalIgnoreCase) >= 0) continue;

                        levelNumberTextTMP = t;
                        break;
                    }

                    if (levelNumberTextTMP == null)
                    {
                        var legacyTexts = upperPanel.GetComponentsInChildren<Text>(true);
                        foreach (var t in legacyTexts)
                        {
                            if (timerTextLegacy != null && t == timerTextLegacy) continue;
                            if (t.gameObject.name.IndexOf("Timer", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                t.gameObject.name.IndexOf("Time", StringComparison.OrdinalIgnoreCase) >= 0) continue;

                            levelNumberTextLegacy = t;
                            break;
                        }
                    }
                }

                if (levelNumberTextTMP == null && levelNumberTextLegacy == null)
                {
                    var allTmp = canvas.GetComponentsInChildren<TMP_Text>(true);
                    foreach (var t in allTmp)
                    {
                        if (timerTextTMP != null && t == timerTextTMP) continue;
                        string tName = t.gameObject.name.ToLowerInvariant();
                        if (tName.Contains("level") || tName.Contains("lvl") || tName.Contains("stage"))
                        {
                            levelNumberTextTMP = t;
                            break;
                        }
                    }
                }
            }

            // Оновлюємо початкове відображення номера рівня
            if (CurrentLevel != null)
            {
                UpdateLevelNumberUI(CurrentLevel);
            }
            else if (allLevels != null && allLevels.Count > 0 && currentLevelIndex >= 0 && currentLevelIndex < allLevels.Count)
            {
                UpdateLevelNumberUI(allLevels[currentLevelIndex]);
            }
            else if (testLevelData != null)
            {
                UpdateLevelNumberUI(testLevelData);
            }
        }

        public void UpdateLevelNumberUI(LevelData levelData)
        {
            if (levelData == null) return;

            string displayText;
            if (!string.IsNullOrEmpty(levelTextFormat))
            {
                try
                {
                    displayText = string.Format(levelTextFormat, levelData.levelIndex);
                }
                catch
                {
                    displayText = $"Level {levelData.levelIndex}";
                }
            }
            else if (!string.IsNullOrEmpty(levelData.levelTitle))
            {
                displayText = levelData.levelTitle;
            }
            else
            {
                displayText = $"Level {levelData.levelIndex}";
            }

            if (levelNumberTextTMP != null)
            {
                levelNumberTextTMP.text = displayText;
            }
            if (levelNumberTextLegacy != null)
            {
                levelNumberTextLegacy.text = displayText;
            }
        }


        private void HookupLostPanelButtons(GameObject panel)
        {
            if (panel == null) return;
            UIButtonPressEffect.AttachToAllIn(panel);
            var buttons = panel.GetComponentsInChildren<Button>(true);
            foreach (var btn in buttons)
            {
                string btnName = btn.gameObject.name.ToLowerInvariant();
                string btnText = "";
                var tmp = btn.GetComponentInChildren<TMP_Text>(true);
                if (tmp != null) btnText = tmp.text.ToLowerInvariant();
                var legacy = btn.GetComponentInChildren<Text>(true);
                if (legacy != null) btnText += " " + legacy.text.ToLowerInvariant();

                if (btnName.Contains("retry") || btnName.Contains("restart") || btnText.Contains("спочатку") || btnText.Contains("retry") || btnText.Contains("заново"))
                {
                    btn.onClick.RemoveListener(RestartCurrentLevel);
                    btn.onClick.AddListener(RestartCurrentLevel);
                }
                else if (btnName.Contains("menu") || btnText.Contains("меню") || btnText.Contains("menu") || btnText.Contains("вийти"))
                {
                    btn.onClick.RemoveListener(LoadMenuScene);
                    btn.onClick.AddListener(LoadMenuScene);
                }
            }
        }

        private bool isClaimingWinReward = false;

        private void HookupWinPanelButtons(GameObject panel)
        {
            if (panel == null) return;
            isClaimingWinReward = false;
            UIButtonPressEffect.AttachToAllIn(panel);
            var buttons = panel.GetComponentsInChildren<Button>(true);
            foreach (var btn in buttons)
            {
                string btnName = btn.gameObject.name.ToLowerInvariant();
                string btnText = "";
                var tmp = btn.GetComponentInChildren<TMP_Text>(true);
                if (tmp != null) btnText = tmp.text.ToLowerInvariant();
                var legacy = btn.GetComponentInChildren<Text>(true);
                if (legacy != null) btnText += " " + legacy.text.ToLowerInvariant();

                if (btnName.Contains("80") || btnText.Contains("80"))
                {
                    btn.onClick = new Button.ButtonClickedEvent();
                    btn.onClick.AddListener(() => ClaimWinRewardAndContinue(80));
                }
                else if (btnName.Contains("40") || btnText.Contains("40"))
                {
                    btn.onClick = new Button.ButtonClickedEvent();
                    btn.onClick.AddListener(() => ClaimWinRewardAndContinue(40));
                }
                else if (btnName.Contains("next") || btnName.Contains("continue") || btnText.Contains("далі") || btnText.Contains("next") || btnText.Contains("continue"))
                {
                    btn.onClick = new Button.ButtonClickedEvent();
                    btn.onClick.AddListener(() => ClaimWinRewardAndContinue(40));
                }
                else if (btnName.Contains("menu") || btnText.Contains("меню") || btnText.Contains("menu") || btnText.Contains("вийти"))
                {
                    btn.onClick = new Button.ButtonClickedEvent();
                    btn.onClick.AddListener(LoadMenuScene);
                }
            }
        }

        private void ClaimWinRewardAndContinue(int coinsReward)
        {
            if (isClaimingWinReward) return;
            isClaimingWinReward = true;

            if (CoinManager.Instance != null)
            {
                CoinManager.Instance.AddCoins(coinsReward);
            }

            LoadNextLevel();
        }


        private void EnsureAllLevelsPopulated()
        {
            if (allLevels != null && allLevels.Count > 0 && !allLevels.Exists(l => l == null))
            {
                return;
            }

            // 1. Завантаження з Resources/Levels (працює і в Editor, і в Standalone/Mobile Build)
            var loadedLevels = Resources.LoadAll<LevelData>("Levels");
            if (loadedLevels != null && loadedLevels.Length > 0)
            {
                var list = new List<LevelData>(loadedLevels);
                list.Sort((a, b) => a.levelIndex.CompareTo(b.levelIndex));
                allLevels = list;
                return;
            }

#if UNITY_EDITOR
            // 2. Fallback для Unity Editor: пошук через AssetDatabase
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:LevelData");
            if (guids != null && guids.Length > 0)
            {
                var list = new List<LevelData>();
                foreach (var guid in guids)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                    var level = UnityEditor.AssetDatabase.LoadAssetAtPath<LevelData>(path);
                    if (level != null && !list.Contains(level)) list.Add(level);
                }
                list.Sort((a, b) => a.levelIndex.CompareTo(b.levelIndex));
                allLevels = list;
            }
#endif
        }

        private TMP_FontAsset GetGameFontAsset()
        {
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts/rimouski sb SDF");
#if UNITY_EDITOR
            if (font == null)
            {
                font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/rimouski sb SDF.asset");
            }
#endif
            if (font == null)
            {
                var allFonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                foreach (var f in allFonts)
                {
                    if (f.name.IndexOf("rimouski", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        font = f;
                        break;
                    }
                }
                if (font == null && allFonts.Length > 0)
                {
                    font = allFonts[0];
                }
            }
            if (font == null)
            {
                font = TMP_Settings.defaultFontAsset;
            }
            return font;
        }

        private Sprite GetDefaultUISprite()
        {
            var sprites = Resources.FindObjectsOfTypeAll<Sprite>();
            foreach (var s in sprites)
            {
                if (s.name == "UISprite" || s.name == "Background" || s.name == "Knob")
                {
                    return s;
                }
            }
            return null;
        }

        private void CreateRuntimeTimerHUD(Canvas canvas)
        {
            GameObject hudObj = new GameObject("TimerDisplay");
            hudObj.transform.SetParent(canvas.transform, false);

            RectTransform rt = hudObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -50f);
            rt.sizeDelta = new Vector2(260f, 75f);

            Image bg = hudObj.AddComponent<Image>();
            bg.sprite = GetDefaultUISprite();
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.12f, 0.16f, 0.22f, 0.85f);

            GameObject textObj = new GameObject("TimerText");
            textObj.transform.SetParent(hudObj.transform, false);

            RectTransform textRt = textObj.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.pivot = new Vector2(0.5f, 0.5f);
            textRt.offsetMin = new Vector2(15f, 5f);
            textRt.offsetMax = new Vector2(-15f, -5f);

            TMP_Text tmp = textObj.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset font = GetGameFontAsset();
            if (font != null) tmp.font = font;
            tmp.fontSize = 42;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = timerNormalColor;
            tmp.text = "00:00";

            timerTextTMP = tmp;
            timerContainer = hudObj;
        }

        private void CreateRuntimeLostPanel(Canvas canvas)
        {
            GameObject lostPanelObj = new GameObject("Lost Panel");
            lostPanelObj.transform.SetParent(canvas.transform, false);

            RectTransform fullRt = lostPanelObj.AddComponent<RectTransform>();
            fullRt.anchorMin = Vector2.zero;
            fullRt.anchorMax = Vector2.one;
            fullRt.offsetMin = Vector2.zero;
            fullRt.offsetMax = Vector2.zero;

            Image dimBg = lostPanelObj.AddComponent<Image>();
            dimBg.color = new Color(0f, 0f, 0f, 0.7f);

            GameObject dialogObj = new GameObject("DialogPanel");
            dialogObj.transform.SetParent(lostPanelObj.transform, false);

            RectTransform dialogRt = dialogObj.AddComponent<RectTransform>();
            dialogRt.anchorMin = new Vector2(0.5f, 0.5f);
            dialogRt.anchorMax = new Vector2(0.5f, 0.5f);
            dialogRt.pivot = new Vector2(0.5f, 0.5f);
            dialogRt.anchoredPosition = Vector2.zero;
            dialogRt.sizeDelta = new Vector2(700f, 950f);

            Image dialogImg = dialogObj.AddComponent<Image>();
            dialogImg.sprite = GetDefaultUISprite();
            dialogImg.type = Image.Type.Sliced;
            dialogImg.color = new Color(0.72f, 0.22f, 0.22f, 1f);

            TMP_FontAsset font = GetGameFontAsset();

            GameObject titleObj = new GameObject("TitleText");
            titleObj.transform.SetParent(dialogObj.transform, false);
            RectTransform titleRt = titleObj.AddComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0.5f, 1f);
            titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -80f);
            titleRt.sizeDelta = new Vector2(620f, 100f);

            TMP_Text titleTmp = titleObj.AddComponent<TextMeshProUGUI>();
            if (font != null) titleTmp.font = font;
            titleTmp.fontSize = 54;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.color = Color.white;
            titleTmp.text = "TIME'S UP!";

            GameObject subObj = new GameObject("SubtitleText");
            subObj.transform.SetParent(dialogObj.transform, false);
            RectTransform subRt = subObj.AddComponent<RectTransform>();
            subRt.anchorMin = new Vector2(0.5f, 1f);
            subRt.anchorMax = new Vector2(0.5f, 1f);
            subRt.pivot = new Vector2(0.5f, 1f);
            subRt.anchoredPosition = new Vector2(0f, -180f);
            subRt.sizeDelta = new Vector2(620f, 70f);

            TMP_Text subTmp = subObj.AddComponent<TextMeshProUGUI>();
            if (font != null) subTmp.font = font;
            subTmp.fontSize = 30;
            subTmp.alignment = TextAlignmentOptions.Center;
            subTmp.color = new Color(1f, 0.85f, 0.85f, 0.95f);
            subTmp.text = "LEVEL FAILED";

            GameObject retryBtnObj = new GameObject("RetryButton");
            retryBtnObj.transform.SetParent(dialogObj.transform, false);
            RectTransform retryRt = retryBtnObj.AddComponent<RectTransform>();
            retryRt.anchorMin = new Vector2(0.5f, 0.5f);
            retryRt.anchorMax = new Vector2(0.5f, 0.5f);
            retryRt.pivot = new Vector2(0.5f, 0.5f);
            retryRt.anchoredPosition = new Vector2(0f, -60f);
            retryRt.sizeDelta = new Vector2(460f, 120f);

            Image retryImg = retryBtnObj.AddComponent<Image>();
            retryImg.sprite = GetDefaultUISprite();
            retryImg.type = Image.Type.Sliced;
            retryImg.color = new Color(0.96f, 0.72f, 0.15f, 1f);

            Button retryBtn = retryBtnObj.AddComponent<Button>();
            retryBtn.targetGraphic = retryImg;
            retryBtn.onClick.AddListener(RestartCurrentLevel);

            GameObject retryTxtObj = new GameObject("Text");
            retryTxtObj.transform.SetParent(retryBtnObj.transform, false);
            RectTransform retryTxtRt = retryTxtObj.AddComponent<RectTransform>();
            retryTxtRt.anchorMin = Vector2.zero;
            retryTxtRt.anchorMax = Vector2.one;
            retryTxtRt.offsetMin = Vector2.zero;
            retryTxtRt.offsetMax = Vector2.zero;

            TMP_Text retryTmp = retryTxtObj.AddComponent<TextMeshProUGUI>();
            if (font != null) retryTmp.font = font;
            retryTmp.fontSize = 40;
            retryTmp.fontStyle = FontStyles.Bold;
            retryTmp.alignment = TextAlignmentOptions.Center;
            retryTmp.color = Color.white;
            retryTmp.text = "RETRY";

            GameObject menuBtnObj = new GameObject("MenuButton");
            menuBtnObj.transform.SetParent(dialogObj.transform, false);
            RectTransform menuRt = menuBtnObj.AddComponent<RectTransform>();
            menuRt.anchorMin = new Vector2(0.5f, 0.5f);
            menuRt.anchorMax = new Vector2(0.5f, 0.5f);
            menuRt.pivot = new Vector2(0.5f, 0.5f);
            menuRt.anchoredPosition = new Vector2(0f, -220f);
            menuRt.sizeDelta = new Vector2(460f, 100f);

            Image menuImg = menuBtnObj.AddComponent<Image>();
            menuImg.sprite = GetDefaultUISprite();
            menuImg.type = Image.Type.Sliced;
            menuImg.color = new Color(0.25f, 0.28f, 0.35f, 1f);

            Button menuBtn = menuBtnObj.AddComponent<Button>();
            menuBtn.targetGraphic = menuImg;
            menuBtn.onClick.AddListener(LoadMenuScene);

            GameObject menuTxtObj = new GameObject("Text");
            menuTxtObj.transform.SetParent(menuBtnObj.transform, false);
            RectTransform menuTxtRt = menuTxtObj.AddComponent<RectTransform>();
            menuTxtRt.anchorMin = Vector2.zero;
            menuTxtRt.anchorMax = Vector2.one;
            menuTxtRt.offsetMin = Vector2.zero;
            menuTxtRt.offsetMax = Vector2.zero;

            TMP_Text menuTmp = menuTxtObj.AddComponent<TextMeshProUGUI>();
            if (font != null) menuTmp.font = font;
            menuTmp.fontSize = 36;
            menuTmp.fontStyle = FontStyles.Bold;
            menuTmp.alignment = TextAlignmentOptions.Center;
            menuTmp.color = Color.white;
            menuTmp.text = "MENU";

            losePanel = lostPanelObj;
            UIButtonPressEffect.AttachToAllIn(lostPanelObj);
            UIPanelSlideIn.AttachTo(lostPanelObj).EnableImpactShake(true);
            UILostPanelProgress.AttachTo(lostPanelObj);
            UILegoBrickDebris.AttachTo(lostPanelObj);
            AttachPrimaryButtonPolish(lostPanelObj);
            lostPanelObj.SetActive(false);
        }

        public void RemoveActivePiece(LegoPieceView piece)
        {
            if (activePieces != null && piece != null)
            {
                activePieces.Remove(piece);
            }
        }

        public void HandleRequiredPieceVictoryCheck(bool fromHammer = false)
        {
            piecesExited++;
            if (fromHammer)
            {
                Debug.Log($"<color=orange>🔨 Блок знищено молотком! Зараховано прогрес: {piecesExited}/{requiredPiecesToWin}</color>");
            }

            if (GameSettingsManager.HasInstance)
            {
                GameSettingsManager.Instance.TriggerHapticExit();
            }

            if (piecesExited >= requiredPiecesToWin)
            {
                if (isLevelWon) return;
                isLevelWon = true;

                IsGameplayActive = false;
                PlayLevelWonSound();

                if (pauseButton != null)
                {
                    pauseButton.interactable = false;
                }

                if (saveProgress && allLevels != null && allLevels.Count > 0)
                {
                    int nextIndex = (currentLevelIndex + 1) % allLevels.Count;
                    if (SaveDataService.Instance != null && SaveDataService.Instance.Data != null)
                    {
                        SaveDataService.Instance.Data.currentLevelIndex = nextIndex;
                        SaveDataService.Instance.Save();
                    }
                    else
                    {
                        PlayerPrefs.SetInt(PREFS_LEVEL_INDEX, nextIndex);
                        PlayerPrefs.Save();
                    }
                }

                if (GemManager.Instance != null)
                {
                    GemManager.Instance.AddGems(1);
                }

                OnLevelWon?.Invoke();
                if (fromHammer)
                {
                    Debug.Log("<color=green>Level completed! Victory through Hammer! Saved next level progress and awarded +1 Gem.</color>");
                }
                else
                {
                    Debug.Log("<color=green>Level completed! Victory! Saved next level progress and awarded +1 Gem.</color>");
                }

                if (winPanel != null)
                {
                    StartCoroutine(ShowWinPanelDelayed(winPanelDelay));
                }
            }
            else if (!fromHammer)
            {
                CheckAndTriggerAutoExits();
            }
        }

        private void HandlePieceExited(LegoPieceView piece)
        {
            NotifyBlockMoved();
            RemoveActivePiece(piece);
            NotifyPieceDestroyed(piece);
            HandleRequiredPieceVictoryCheck(fromHammer: false);
        }

        /// <summary>
        /// Знищує деталь бустером "Молоток", звільняє зайняті клітинки на сітці
        /// та зараховує прогрес, якщо деталь була необхідна для перемоги.
        /// </summary>
        public void DestroyPieceWithHammer(LegoPieceView piece)
        {
            BoosterService.DestroyPieceWithHammer(piece);
        }

        /// <summary>
        /// Сповіщає всі заморожені блоки на полі про знищення або вихід іншого блоку,
        /// щоб зменшити їхній індивідуальний лічильник заморозки.
        /// </summary>
        public void NotifyPieceDestroyed(LegoPieceView destroyedPiece)
        {
            if (activePieces == null || activePieces.Count == 0) return;

            List<LegoPieceView> frozenPieces = new List<LegoPieceView>();
            for (int i = 0; i < activePieces.Count; i++)
            {
                var p = activePieces[i];
                if (p != null && p != destroyedPiece && p.gameObject.activeSelf && p.IsFrozen)
                {
                    frozenPieces.Add(p);
                }
            }

            for (int i = 0; i < frozenPieces.Count; i++)
            {
                var fp = frozenPieces[i];
                if (fp != null && fp.gameObject.activeSelf && fp.IsFrozen)
                {
                    fp.DecrementFreeze();
                }
            }
        }

        // =========================================================================
        // Booster & Camera Effects (Делегування до LevelBoosterService та LevelCameraController)
        // =========================================================================

        public void ShakeCamera(float duration = 0.18f, float intensity = 0.35f)
        {
            CameraController.ShakeCamera(duration, intensity);
        }

        public void PlaySmashSound(AudioClip customClip = null)
        {
            BoosterService.PlaySmashSound(customClip);
        }

        public bool ShowMoveHint(AudioClip customChime = null)
        {
            return BoosterService.ShowMoveHint(customChime);
        }

        public void DismissMoveHint()
        {
            BoosterService.DismissMoveHint();
        }

        public void PlayHintChime(AudioClip customClip = null)
        {
            BoosterService.PlayHintChime(customClip);
        }

        public bool FindBestHintMove(out LegoPieceView bestPiece, out Vector2Int bestDelta, out bool leadsToExit)
        {
            return BoosterService.FindBestHintMove(out bestPiece, out bestDelta, out leadsToExit);
        }

        private System.Collections.IEnumerator ShowWinPanelDelayed(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (winPanel != null)
            {
                UIButtonPressEffect.AttachToAllIn(winPanel);
                UIPanelSlideIn.AttachTo(winPanel);
                UIConfettiEffect.AttachTo(winPanel);
                AttachPrimaryButtonPolish(winPanel);
                HookupWinPanelButtons(winPanel);
                winPanel.SetActive(true);
            }
        }

        private static void AttachPrimaryButtonPolish(GameObject panel)
        {
            if (panel == null) return;
            var buttons = panel.GetComponentsInChildren<Button>(true);
            if (buttons == null || buttons.Length == 0) return;

            Button bestCandidate = null;
            foreach (var btn in buttons)
            {
                string name = btn.gameObject.name.ToLowerInvariant();
                if (name.Contains("80") || name.Contains("next") || name.Contains("retry") || name.Contains("reward"))
                {
                    bestCandidate = btn;
                    break;
                }
            }

            if (bestCandidate == null && buttons.Length > 0)
            {
                bestCandidate = buttons[buttons.Length - 1];
            }

            if (bestCandidate != null)
            {
                UIButtonShinePulse.AttachTo(bestCandidate.gameObject);
            }
        }

        [ContextMenu("Reset saved progress (to Level 1)")]
        public void ResetProgress()
        {
            if (SaveDataService.Instance != null && SaveDataService.Instance.Data != null)
            {
                SaveDataService.Instance.Data.currentLevelIndex = 0;
                SaveDataService.Instance.Save();
            }
            PlayerPrefs.DeleteKey(PREFS_LEVEL_INDEX);
            PlayerPrefs.Save();
            currentLevelIndex = 0;
            Debug.Log("<color=yellow>LevelLoader: Level progress reset! Next start will begin from Level 1.</color>");
        }

#if UNITY_EDITOR
        [ContextMenu("Auto-populate all LevelData in project")]
        public void AutoPopulateAllLevels()
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:LevelData");
            List<LevelData> foundLevels = new List<LevelData>();

            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                LevelData lvl = UnityEditor.AssetDatabase.LoadAssetAtPath<LevelData>(path);
                if (lvl != null && !foundLevels.Contains(lvl))
                {
                    foundLevels.Add(lvl);
                }
            }

            foundLevels.Sort((a, b) =>
            {
                if (a.levelIndex != b.levelIndex) return a.levelIndex.CompareTo(b.levelIndex);
                return string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
            });

            allLevels = foundLevels;
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"<color=green>LevelLoader: Found and added {allLevels.Count} levels to All Levels list!</color>");
        }
#endif
    }
}
