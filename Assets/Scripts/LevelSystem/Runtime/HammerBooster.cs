using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace LegoPuzzle.Runtime
{
    /// <summary>
    /// UI-компонент бустера "Молоток" (Hammer Booster).
    /// Вішається на кнопку (Button) у нижній панелі HUD.
    /// Керує зарядами, режимом прицілювання (Targeting Mode) та запускає
    /// анімацію розтрощення обраного блоку 3D-моделлю hammer.prefab.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class HammerBooster : MonoBehaviour
    {
        [Header("Прив'язки")]
        [Tooltip("Менеджер рівнів LevelLoader (якщо не вказано, шукається автоматично на сцені)")]
        [SerializeField] private LevelLoader levelLoader;

        [Tooltip("Кнопка UI для активації бустера (якщо не вказано, береться з цього ж об'єкта)")]
        [SerializeField] private Button boosterButton;

        [Tooltip("Префаб 3D-моделі молотка (Assets/Prefabs/hammer.prefab)")]
        [SerializeField] private GameObject hammerPrefab;

        [Header("Параметри Зарядів")]
        [Tooltip("Кількість доступних використань (-1 = нескінченно, або ліміт, наприклад 3)")]
        [SerializeField] private int availableCharges = 3;

        [Tooltip("Чи зберігати кількість бустерів між рівнями та сесіями")]
        [SerializeField] private bool saveChargesAcrossLevels = true;

        [Tooltip("Початкова кількість зарядів для нового гравця")]
        [SerializeField] private int defaultInitialCharges = 3;

        private const string PREFS_HAMMER_CHARGES = "LEGO_GlobalBoosterCharges_Hammer";

        [Tooltip("Чи додавати ефект пружного натискання UIButtonPressEffect")]
        [SerializeField] private bool addPressEffect = true;

        [Header("Аудіо")]
        [Tooltip("Власний звук удару молотка (якщо порожньо, використовується системний звук)")]
        [SerializeField] private AudioClip smashSound;

        [Header("Візуальні елементи UI")]
        [Tooltip("Текстовий елемент для відображення кількості зарядів (наприклад, 'x3')")]
        [SerializeField] private TMP_Text chargesTMPText;
        [SerializeField] private Text chargesLegacyText;

        [Tooltip("Префікс перед кількістю зарядів (наприклад, 'x')")]
        [SerializeField] private string chargesPrefix = "x";

        [Tooltip("Іконка або зображення кнопки для підсвічування під час прицілювання")]
        [SerializeField] private Image buttonIconImage;

        [Tooltip("Колір іконки під час активного режиму прицілювання (золотисто-янтарний)")]
        [SerializeField] private Color targetingIconTint = new Color(1f, 0.75f, 0.2f, 1f);

        [Tooltip("Опціональний текстовий банер або підказка на екрані ('Оберіть блок для удару')")]
        [SerializeField] private GameObject targetingHintBanner;

        private Color originalIconColor = Color.white;
        private Coroutine pulseCoroutine;
        private bool isExecutingStrike = false;

        public static bool IsTargeting { get; private set; } = false;
        public static HammerBooster ActiveInstance { get; private set; }

        public int AvailableCharges => availableCharges;

        private void Awake()
        {
            ActiveInstance = this;

            if (boosterButton == null)
            {
                boosterButton = GetComponent<Button>();
            }

            if (buttonIconImage != null)
            {
                originalIconColor = buttonIconImage.color;
            }

            if (addPressEffect && GetComponent<UIButtonPressEffect>() == null)
            {
                UIButtonPressEffect.AttachTo(gameObject);
            }

#if UNITY_EDITOR
            if (hammerPrefab == null)
            {
                hammerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/hammer.prefab");
            }
#endif

            InitOrLoadCharges();
        }

        private void InitOrLoadCharges()
        {
            if (!saveChargesAcrossLevels) return;

            if (PlayerPrefs.HasKey(PREFS_HAMMER_CHARGES))
            {
                availableCharges = PlayerPrefs.GetInt(PREFS_HAMMER_CHARGES);
            }
            else
            {
                int initial = availableCharges >= 0 ? availableCharges : defaultInitialCharges;
                if (initial < 0) initial = 0;
                PlayerPrefs.SetInt(PREFS_HAMMER_CHARGES, initial);
                PlayerPrefs.Save();
                availableCharges = initial;
            }
        }

        [ContextMenu("Set Charges to 0 (Test Shop)")]
        public void SetChargesToZero()
        {
            availableCharges = 0;
            if (saveChargesAcrossLevels)
            {
                PlayerPrefs.SetInt(PREFS_HAMMER_CHARGES, 0);
                PlayerPrefs.Save();
            }
            UpdateChargesUI();
            Debug.Log("<color=yellow>Hammer: Charges set to 0. Next click opens shop panel!</color>");
        }

        [ContextMenu("Set Charges to 3")]
        public void SetChargesToThree()
        {
            availableCharges = 3;
            if (saveChargesAcrossLevels)
            {
                PlayerPrefs.SetInt(PREFS_HAMMER_CHARGES, 3);
                PlayerPrefs.Save();
            }
            UpdateChargesUI();
        }

        private void OnEnable()
        {
            ActiveInstance = this;
            if (boosterButton != null)
            {
                boosterButton.onClick.RemoveListener(OnBoosterButtonClicked);
                boosterButton.onClick.AddListener(OnBoosterButtonClicked);
            }

            EnsureLevelLoader();
            UpdateChargesUI();
            CancelTargeting();
        }

        private void OnDisable()
        {
            if (ActiveInstance == this)
            {
                ActiveInstance = null;
            }

            if (boosterButton != null)
            {
                boosterButton.onClick.RemoveListener(OnBoosterButtonClicked);
            }

            CancelTargeting();
        }

        private void Update()
        {
            EnsureLevelLoader();

            // Якщо натиснуто праву кнопку миші або кнопку назад під час прицілювання - скасовуємо
            if (IsTargeting && (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape)))
            {
                CancelTargeting();
            }
        }

        private void EnsureLevelLoader()
        {
            if (levelLoader == null)
            {
                levelLoader = FindAnyObjectByType<LevelLoader>();
            }
        }

        public void OnBoosterButtonClicked()
        {
            EnsureLevelLoader();

            if (levelLoader != null && !levelLoader.IsGameplayActive) return;
            if (isExecutingStrike) return;

            // Якщо режим прицілювання вже активний - повторний клік скасовує його
            if (IsTargeting)
            {
                CancelTargeting();
                return;
            }

            // Перевіряємо наявність зарядів
            if (availableCharges <= 0)
            {
                CancelTargeting();
                BoosterShopPanel.Open(BoosterType.Hammer);
                return;
            }

            // Вмикаємо режим вибору блоку для удару
            EnterTargeting();
        }

        private void EnterTargeting()
        {
            IsTargeting = true;

            if (targetingHintBanner != null)
            {
                targetingHintBanner.SetActive(true);
            }

            if (pulseCoroutine != null) StopCoroutine(pulseCoroutine);
            pulseCoroutine = StartCoroutine(TargetingPulseRoutine());

            Debug.Log("<color=orange>🔨 Бустер 'Молоток': Режим прицілювання активовано! Оберіть блок для удару.</color>");
        }

        public void CancelTargeting()
        {
            IsTargeting = false;

            if (targetingHintBanner != null)
            {
                targetingHintBanner.SetActive(false);
            }

            if (pulseCoroutine != null)
            {
                StopCoroutine(pulseCoroutine);
                pulseCoroutine = null;
            }

            if (buttonIconImage != null)
            {
                buttonIconImage.color = originalIconColor;
            }

            transform.localScale = Vector3.one;
        }

        /// <summary>
        /// Викликається блоком LegoPieceView при натисканні гравцем у режимі прицілювання.
        /// </summary>
        public static void SelectTarget(LegoPieceView targetPiece)
        {
            if (ActiveInstance != null && IsTargeting)
            {
                ActiveInstance.ExecuteHammerOnPiece(targetPiece);
            }
        }

        public void ExecuteHammerOnPiece(LegoPieceView targetPiece)
        {
            if (targetPiece == null || isExecutingStrike) return;

            CancelTargeting();
            isExecutingStrike = true;

            // Списуємо заряд
            if (availableCharges > 0)
            {
                availableCharges--;
                if (saveChargesAcrossLevels)
                {
                    PlayerPrefs.SetInt(PREFS_HAMMER_CHARGES, availableCharges);
                    PlayerPrefs.Save();
                }
            }
            UpdateChargesUI();

            // Запускаємо послідовність анімації молотка
            EnsureLevelLoader();
            HammerSmashSequence.Play(
                hammerPrefab,
                targetPiece,
                levelLoader,
                smashSound,
                onComplete: () =>
                {
                    isExecutingStrike = false;
                    UpdateChargesUI();
                });
        }

        private IEnumerator TargetingPulseRoutine()
        {
            Vector3 baseScale = Vector3.one;
            while (IsTargeting)
            {
                float t = Mathf.Sin(Time.time * 6f) * 0.5f + 0.5f;

                if (buttonIconImage != null)
                {
                    buttonIconImage.color = Color.Lerp(originalIconColor, targetingIconTint, t);
                }

                float scaleBonus = 1f + 0.08f * t;
                transform.localScale = baseScale * scaleBonus;

                yield return null;
            }

            transform.localScale = baseScale;
            if (buttonIconImage != null)
            {
                buttonIconImage.color = originalIconColor;
            }
        }

        private IEnumerator EmptyChargesShakeRoutine()
        {
            Vector3 origPos = transform.localPosition;
            float duration = 0.22f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float offset = Mathf.Sin(elapsed * 50f) * 8f * (1f - elapsed / duration);
                transform.localPosition = origPos + new Vector3(offset, 0f, 0f);
                yield return null;
            }

            transform.localPosition = origPos;
        }

        private void UpdateChargesUI()
        {
            string textValue = (availableCharges < 0) ? "∞" : $"{chargesPrefix}{availableCharges}";

            if (chargesTMPText != null)
            {
                chargesTMPText.text = textValue;
            }
            if (chargesLegacyText != null)
            {
                chargesLegacyText.text = textValue;
            }

            if (boosterButton != null)
            {
                boosterButton.interactable = !isExecutingStrike;
            }
        }

        public void AddCharges(int count)
        {
            if (availableCharges < 0) return;
            availableCharges += count;
            if (saveChargesAcrossLevels)
            {
                PlayerPrefs.SetInt(PREFS_HAMMER_CHARGES, availableCharges);
                PlayerPrefs.Save();
            }
            UpdateChargesUI();
        }

        public void ResetChargesToDefault()
        {
            availableCharges = defaultInitialCharges;
            if (saveChargesAcrossLevels)
            {
                PlayerPrefs.SetInt(PREFS_HAMMER_CHARGES, availableCharges);
                PlayerPrefs.Save();
            }
            UpdateChargesUI();
        }
    }
}
