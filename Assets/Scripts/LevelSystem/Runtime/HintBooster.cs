using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace LegoPuzzle.Runtime
{
    /// <summary>
    /// UI-компонент бустера "Підказка" (Hint Booster).
    /// Вішається на кнопку (Button) у нижній панелі HUD.
    /// Керує зарядами, знаходить найкращий наступний хід і запускає візуальний ефект підказки над деталлю.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class HintBooster : MonoBehaviour
    {
        [Header("Прив'язки")]
        [Tooltip("Менеджер рівнів LevelLoader (якщо не вказано, шукається автоматично на сцені)")]
        [SerializeField] private LevelLoader levelLoader;

        [Tooltip("Кнопка UI для активації бустера (якщо не вказано, береться з цього ж об'єкта)")]
        [SerializeField] private Button boosterButton;

        [Header("Параметри Зарядів")]
        [Tooltip("Кількість доступних підказок (-1 = нескінченно, або ліміт, наприклад 3)")]
        [SerializeField] private int availableCharges = 3;

        [Tooltip("Чи зберігати кількість бустерів між рівнями та сесіями")]
        [SerializeField] private bool saveChargesAcrossLevels = true;

        [Tooltip("Початкова кількість зарядів для нового гравця")]
        [SerializeField] private int defaultInitialCharges = 3;

        private const string PREFS_HINT_CHARGES = "LEGO_GlobalBoosterCharges_Hint";

        [Tooltip("Чи додавати ефект пружного натискання UIButtonPressEffect")]
        [SerializeField] private bool addPressEffect = true;

        [Header("Аудіо")]
        [Tooltip("Власний звук активації підказки (дзвіночок/еврика)")]
        [SerializeField] private AudioClip hintChimeSound;

        [Header("Візуальні елементи UI")]
        [Tooltip("Текстовий елемент для відображення кількості зарядів (наприклад, 'x3')")]
        [SerializeField] private TMP_Text chargesTMPText;
        [SerializeField] private Text chargesLegacyText;

        [Tooltip("Префікс перед кількістю зарядів (наприклад, 'x')")]
        [SerializeField] private string chargesPrefix = "x";

        [Tooltip("Іконка або зображення кнопки для зміни кольору")]
        [SerializeField] private Image buttonIconImage;

        [Tooltip("Колір спалаху іконки при активації підказки")]
        [SerializeField] private Color activeIconTint = new Color(1f, 0.9f, 0.3f, 1f);

        private Color originalIconColor = Color.white;
        private Coroutine flashCoroutine;

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

            if (PlayerPrefs.HasKey(PREFS_HINT_CHARGES))
            {
                availableCharges = PlayerPrefs.GetInt(PREFS_HINT_CHARGES);
            }
            else
            {
                int initial = availableCharges >= 0 ? availableCharges : defaultInitialCharges;
                if (initial < 0) initial = 0;
                PlayerPrefs.SetInt(PREFS_HINT_CHARGES, initial);
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
                PlayerPrefs.SetInt(PREFS_HINT_CHARGES, 0);
                PlayerPrefs.Save();
            }
            UpdateChargesUI();
            Debug.Log("<color=yellow>Hint: Charges set to 0. Next click opens shop panel!</color>");
        }

        [ContextMenu("Set Charges to 3")]
        public void SetChargesToThree()
        {
            availableCharges = 3;
            if (saveChargesAcrossLevels)
            {
                PlayerPrefs.SetInt(PREFS_HINT_CHARGES, 3);
                PlayerPrefs.Save();
            }
            UpdateChargesUI();
        }

        private void OnEnable()
        {
            if (boosterButton != null)
            {
                boosterButton.onClick.RemoveListener(OnBoosterButtonClicked);
                boosterButton.onClick.AddListener(OnBoosterButtonClicked);
            }

            EnsureLevelLoader();
            UpdateChargesUI();
        }

        private void OnDisable()
        {
            if (boosterButton != null)
            {
                boosterButton.onClick.RemoveListener(OnBoosterButtonClicked);
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

            // Перевіряємо наявність зарядів
            if (availableCharges <= 0)
            {
                BoosterShopPanel.Open(BoosterType.Hint);
                return;
            }

            // Показуємо підказку на полі
            bool shown = levelLoader != null && levelLoader.ShowMoveHint(hintChimeSound);

            if (shown)
            {
                // Списуємо 1 заряд
                if (availableCharges > 0)
                {
                    availableCharges--;
                    if (saveChargesAcrossLevels)
                    {
                        PlayerPrefs.SetInt(PREFS_HINT_CHARGES, availableCharges);
                        PlayerPrefs.Save();
                    }
                }
                UpdateChargesUI();

                if (flashCoroutine != null) StopCoroutine(flashCoroutine);
                flashCoroutine = StartCoroutine(ButtonFlashRoutine());
            }
            else
            {
                // Якщо ходів немає (наприклад, завал), показуємо тремтіння кнопки
                StartCoroutine(EmptyChargesShakeRoutine());
            }
        }

        private IEnumerator ButtonFlashRoutine()
        {
            if (buttonIconImage == null) yield break;

            buttonIconImage.color = activeIconTint;
            float elapsed = 0f;
            float duration = 0.45f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                buttonIconImage.color = Color.Lerp(activeIconTint, originalIconColor, elapsed / duration);
                yield return null;
            }

            buttonIconImage.color = originalIconColor;
            flashCoroutine = null;
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
                boosterButton.interactable = true;
            }
        }

        public void AddCharges(int count)
        {
            if (availableCharges < 0) return;
            availableCharges += count;
            if (saveChargesAcrossLevels)
            {
                PlayerPrefs.SetInt(PREFS_HINT_CHARGES, availableCharges);
                PlayerPrefs.Save();
            }
            UpdateChargesUI();
        }

        public void ResetChargesToDefault()
        {
            availableCharges = defaultInitialCharges;
            if (saveChargesAcrossLevels)
            {
                PlayerPrefs.SetInt(PREFS_HINT_CHARGES, availableCharges);
                PlayerPrefs.Save();
            }
            UpdateChargesUI();
        }
    }
}
