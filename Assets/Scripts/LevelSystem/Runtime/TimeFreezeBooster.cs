using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace LegoPuzzle.Runtime
{
    /// <summary>
    /// Компонент кнопки бустера заморозки часу.
    /// Можна повісити безпосередньо на кнопку (Button), яку ви додасте знизу екрана.
    /// Він автоматично знайде LevelLoader, підключиться до кнопки та керуватиме її станом і зарядами.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class TimeFreezeBooster : MonoBehaviour
    {
        [Header("Прив'язки")]
        [Tooltip("Менеджер рівнів LevelLoader (якщо не вказано, шукається автоматично на сцені)")]
        [SerializeField] private LevelLoader levelLoader;

        [Tooltip("Кнопка UI для активації бустера (якщо не вказано, береться з цього ж об'єкта)")]
        [SerializeField] private Button boosterButton;

        [Header("Параметри Бустера")]
        [Tooltip("Тривалість дії заморозки в секундах (якщо 0, використовується налаштування з LevelLoader)")]
        [SerializeField] private float customDuration = 0f;

        [Tooltip("Кількість доступних використань (-1 = нескінченно, або вкажіть ліміт, наприклад 3)")]
        [SerializeField] private int availableCharges = -1;

        [Tooltip("Чи зберігати кількість бустерів між рівнями та ігровими сесіями")]
        [SerializeField] private bool saveChargesAcrossLevels = true;

        [Tooltip("Початкова кількість зарядів для нового гравця (якщо ще немає збереження)")]
        [SerializeField] private int defaultInitialCharges = 3;

        private const string PREFS_BOOSTER_CHARGES = "LEGO_GlobalBoosterCharges_TimeFreeze";

        [Tooltip("Чи вимикати кнопку на час дії заморозки (щоб не витрачати заряди дарма)")]
        [SerializeField] private bool disableDuringFreeze = true;

        [Tooltip("Чи додавати соковитий ефект пружного натискання (UIButtonPressEffect)")]
        [SerializeField] private bool addPressEffect = true;

        [Header("Візуальні елементи (Опціонально)")]
        [Tooltip("Текстовий елемент для відображення кількості зарядів (наприклад, 'x3' або '3')")]
        [SerializeField] private TMP_Text chargesTMPText;
        [SerializeField] private Text chargesLegacyText;

        [Tooltip("Префікс перед кількістю зарядів (наприклад, 'x' або '')")]
        [SerializeField] private string chargesPrefix = "x";

        [Tooltip("Зображення-індикатор дії заморозки (Image з ImageType.Filled для радіального/лінійного прогресу)")]
        [SerializeField] private Image activeProgressFill;

        [Tooltip("Іконка або зображення кнопки для зміни відтінку під час заморозки")]
        [SerializeField] private Image buttonIconImage;

        [Tooltip("Колір іконки під час активної заморозки")]
        [SerializeField] private Color activeIconTint = new Color(0.4f, 0.9f, 1f, 1f);

        private Color originalIconColor = Color.white;
        private float maxActiveDuration = 10f;

        public int AvailableCharges => availableCharges;

        private void Awake()
        {
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

            InitOrLoadCharges();
        }

        private void InitOrLoadCharges()
        {
            if (!saveChargesAcrossLevels) return;

            if (PlayerPrefs.HasKey(PREFS_BOOSTER_CHARGES))
            {
                availableCharges = PlayerPrefs.GetInt(PREFS_BOOSTER_CHARGES);
            }
            else
            {
                int initial = availableCharges >= 0 ? availableCharges : defaultInitialCharges;
                if (initial < 0) initial = 0;
                PlayerPrefs.SetInt(PREFS_BOOSTER_CHARGES, initial);
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
                PlayerPrefs.SetInt(PREFS_BOOSTER_CHARGES, 0);
                PlayerPrefs.Save();
            }
            UpdateChargesUI();
            Debug.Log("<color=yellow>Freeze: Charges set to 0. Next click opens shop panel!</color>");
        }

        [ContextMenu("Set Charges to 3")]
        public void SetChargesToThree()
        {
            availableCharges = 3;
            if (saveChargesAcrossLevels)
            {
                PlayerPrefs.SetInt(PREFS_BOOSTER_CHARGES, 3);
                PlayerPrefs.Save();
            }
            UpdateChargesUI();
        }

        private void OnEnable()
        {
            if (boosterButton != null)
            {
                boosterButton.onClick.RemoveListener(UseBooster);
                boosterButton.onClick.AddListener(UseBooster);
            }

            EnsureLevelLoader();
            if (levelLoader != null)
            {
                levelLoader.OnLevelLoaded -= HandleLevelLoaded;
                levelLoader.OnLevelLoaded += HandleLevelLoaded;
            }

            InitOrLoadCharges();
            UpdateChargesUI();
        }

        private void OnDisable()
        {
            if (boosterButton != null)
            {
                boosterButton.onClick.RemoveListener(UseBooster);
            }

            if (levelLoader != null)
            {
                levelLoader.OnLevelLoaded -= HandleLevelLoaded;
            }
        }

        private void HandleLevelLoaded(int levelIndex)
        {
            InitOrLoadCharges();
            UpdateChargesUI();
        }


        private void Update()
        {
            EnsureLevelLoader();

            if (levelLoader == null) return;

            bool isFrozen = levelLoader.IsTimeFrozen;

            // 1. Оновлення індикатора прогресу дії заморозки
            if (activeProgressFill != null)
            {
                if (isFrozen && maxActiveDuration > 0f)
                {
                    activeProgressFill.fillAmount = Mathf.Clamp01(levelLoader.RemainingFreezeTime / maxActiveDuration);
                }
                else
                {
                    activeProgressFill.fillAmount = 0f;
                }
            }

            // 2. Керування клікабельністю кнопки
            if (boosterButton != null)
            {
                bool isLevelActiveWithTimer = levelLoader.IsGameplayActive && levelLoader.CurrentLevel != null && levelLoader.CurrentLevel.timeLimitSeconds > 0f;

                if (disableDuringFreeze && isFrozen)
                {
                    boosterButton.interactable = false;
                }
                else
                {
                    // Дозволяємо натискання навіть при 0 зарядів для виклику магазину бустерів
                    boosterButton.interactable = isLevelActiveWithTimer;
                }
            }

            // 3. Колір іконки під час заморозки
            if (buttonIconImage != null)
            {
                if (isFrozen)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 4f);
                    buttonIconImage.color = Color.Lerp(activeIconTint, Color.white, pulse * 0.35f);
                }
                else
                {
                    buttonIconImage.color = originalIconColor;
                }
            }
        }

        public void UseBooster()
        {
            EnsureLevelLoader();

            if (levelLoader == null)
            {
                Debug.LogWarning("TimeFreezeBooster: Не знайдено LevelLoader на сцені!");
                return;
            }

            if (!levelLoader.IsGameplayActive || levelLoader.CurrentLevel == null || levelLoader.CurrentLevel.timeLimitSeconds <= 0f)
            {
                Debug.Log("TimeFreezeBooster: Заморозка недоступна (рівень не активний або час не обмежений).");
                return;
            }

            if (disableDuringFreeze && levelLoader.IsTimeFrozen)
            {
                return;
            }

            if (availableCharges <= 0)
            {
                BoosterShopPanel.Open(BoosterType.Freeze);
                return;
            }

            float duration = customDuration > 0f ? customDuration : levelLoader.FreezeDuration;
            maxActiveDuration = duration;

            if (availableCharges > 0)
            {
                availableCharges--;
                if (saveChargesAcrossLevels)
                {
                    PlayerPrefs.SetInt(PREFS_BOOSTER_CHARGES, availableCharges);
                    PlayerPrefs.Save();
                }
                UpdateChargesUI();
            }

            levelLoader.FreezeTime(duration);
        }

        public void AddCharges(int count)
        {
            if (availableCharges == -1) return; // Нескінченно
            availableCharges = Mathf.Max(0, availableCharges + count);
            if (saveChargesAcrossLevels)
            {
                PlayerPrefs.SetInt(PREFS_BOOSTER_CHARGES, availableCharges);
                PlayerPrefs.Save();
            }
            UpdateChargesUI();
        }

        public void SetCharges(int count)
        {
            availableCharges = Mathf.Max(0, count);
            if (saveChargesAcrossLevels)
            {
                PlayerPrefs.SetInt(PREFS_BOOSTER_CHARGES, availableCharges);
                PlayerPrefs.Save();
            }
            UpdateChargesUI();
        }

        public static int GetGlobalCharges(int fallback = 3)
        {
            return PlayerPrefs.GetInt(PREFS_BOOSTER_CHARGES, fallback);
        }

        public static void SetGlobalCharges(int count)
        {
            PlayerPrefs.SetInt(PREFS_BOOSTER_CHARGES, Mathf.Max(0, count));
            PlayerPrefs.Save();
        }

        public static void AddGlobalCharges(int delta)
        {
            int current = GetGlobalCharges();
            SetGlobalCharges(current + delta);
        }

#if UNITY_EDITOR
        [ContextMenu("Скинути заряди бустера (до 3)")]
        public void ResetBoosterChargesToDefault()
        {
            SetCharges(defaultInitialCharges);
            Debug.Log($"<color=yellow>TimeFreezeBooster: Заряди скинуто до {defaultInitialCharges} у PlayerPrefs!</color>");
        }

        [ContextMenu("Додати +3 заряди бустера")]
        public void AddThreeBoosterCharges()
        {
            AddCharges(3);
            Debug.Log($"<color=green>TimeFreezeBooster: Додано 3 заряди. Загалом: {availableCharges}</color>");
        }
#endif


        private void EnsureLevelLoader()
        {
            if (levelLoader == null)
            {
                levelLoader = FindAnyObjectByType<LevelLoader>();
            }
        }

        private void UpdateChargesUI()
        {
            if (availableCharges < 0)
            {
                // Нескінченна кількість — ховаємо лічильник
                if (chargesTMPText != null) chargesTMPText.gameObject.SetActive(false);
                if (chargesLegacyText != null) chargesLegacyText.gameObject.SetActive(false);
            }
            else
            {
                string textStr = $"{chargesPrefix}{availableCharges}";
                if (chargesTMPText != null)
                {
                    chargesTMPText.gameObject.SetActive(true);
                    chargesTMPText.text = textStr;
                }
                if (chargesLegacyText != null)
                {
                    chargesLegacyText.gameObject.SetActive(true);
                    chargesLegacyText.text = textStr;
                }
            }
        }
    }
}
