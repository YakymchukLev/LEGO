using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

namespace LegoPuzzle.Runtime
{
    public class LevelUIController : MonoBehaviour
    {
        [Header("Посилання на LevelLoader")]
        [SerializeField] private LevelLoader levelLoader;

        [Header("Панель перемоги (Win Panel)")]
        [Tooltip("Кореневий GameObject панелі перемоги")]
        [SerializeField] private GameObject winPanel;

        [Tooltip("Кнопка переходу на наступний рівень")]
        [SerializeField] private Button nextLevelButton;

        [Tooltip("Кнопка перезапуску рівня з панелі перемоги")]
        [SerializeField] private Button winRestartButton;

        [Tooltip("Текст заголовка перемоги (наприклад, 'Рівень 1 пройдено!')")]
        [SerializeField] private Text winTitleText;
        [SerializeField] private TMP_Text winTitleTMP;

        [Header("Панель поразки (Lose Panel)")]
        [Tooltip("Кореневий GameObject панелі поразки (коли вийшов час)")]
        [SerializeField] private GameObject losePanel;

        [Tooltip("Кнопка повторної спроби")]
        [SerializeField] private Button retryButton;

        [Header("Ігровий інтерфейс (In-Game HUD)")]
        [Tooltip("Текст номера або назви рівня")]
        [SerializeField] private Text levelIndexText;
        [SerializeField] private TMP_Text levelIndexTMP;

        [Tooltip("Текст таймера (наприклад, '01:30')")]
        [SerializeField] private Text timerText;
        [SerializeField] private TMP_Text timerTextTMP;

        [Tooltip("Кнопка швидкого перезапуску під час гри")]
        [SerializeField] private Button inGameRestartButton;

        [Tooltip("Кнопка виходу в головне меню")]
        [SerializeField] private Button inGameMenuButton;

        [Tooltip("Назва сцени головного меню")]
        [SerializeField] private string menuSceneName = "Menu";


        [Header("Магазин бустерів (Booster Shop)")]
        [Tooltip("Панель купівлі додаткових зарядів бустерів")]
        [SerializeField] private BoosterShopPanel boosterShopPanel;

        [Header("Налаштування")]
        [Tooltip("Затримка перед показом панелі перемоги (щоб встигла дограти анімація вильоту)")]
        [SerializeField] private float winPanelDelay = 0.5f;

        private void Awake()
        {
            if (levelLoader == null)
            {
                levelLoader = FindAnyObjectByType<LevelLoader>();
            }

            SetupButtons();
        }

        private void OnEnable()
        {
            if (levelLoader != null)
            {
                levelLoader.OnLevelLoaded += HandleLevelLoaded;
                levelLoader.OnTimerUpdated += HandleTimerUpdated;
                levelLoader.OnLevelWon += HandleLevelWon;
                levelLoader.OnLevelLost += HandleLevelLost;
            }
        }

        private void OnDisable()
        {
            if (levelLoader != null)
            {
                levelLoader.OnLevelLoaded -= HandleLevelLoaded;
                levelLoader.OnTimerUpdated -= HandleTimerUpdated;
                levelLoader.OnLevelWon -= HandleLevelWon;
                levelLoader.OnLevelLost -= HandleLevelLost;
            }
        }

        private void Start()
        {
            HidePanels();
            EnsureBoosterShopPanel();
        }

        private void EnsureBoosterShopPanel()
        {
            if (boosterShopPanel == null)
            {
                boosterShopPanel = FindAnyObjectByType<BoosterShopPanel>(FindObjectsInactive.Include);
                if (boosterShopPanel == null)
                {
                    Canvas canvas = GetComponentInParent<Canvas>();
                    if (canvas == null) canvas = FindAnyObjectByType<Canvas>();
                    if (canvas != null)
                    {
                        GameObject shopObj = new GameObject("BoosterShopPanel");
                        shopObj.transform.SetParent(canvas.transform, false);
                        boosterShopPanel = shopObj.AddComponent<BoosterShopPanel>();
                    }
                }
            }

            if (boosterShopPanel != null)
            {
                boosterShopPanel.EnsureUIHierarchy();
                boosterShopPanel.gameObject.SetActive(false);
            }
        }

        private void SetupButtons()
        {
            if (winPanel != null)
            {
                UIButtonPressEffect.AttachToAllIn(winPanel);
                UIPanelSlideIn.AttachTo(winPanel);
                UIConfettiEffect.AttachTo(winPanel);
                AttachPrimaryButtonPolish(winPanel);
            }

            if (losePanel != null)
            {
                UIButtonPressEffect.AttachToAllIn(losePanel);
                UIPanelSlideIn.AttachTo(losePanel).EnableImpactShake(true);
                UILostPanelProgress.AttachTo(losePanel);
                UILegoBrickDebris.AttachTo(losePanel);
                AttachPrimaryButtonPolish(losePanel);
            }

            if (nextLevelButton != null)
            {
                nextLevelButton.onClick.AddListener(OnNextLevelClicked);
            }

            if (winRestartButton != null)
            {
                winRestartButton.onClick.AddListener(OnRestartClicked);
            }

            if (retryButton != null)
            {
                retryButton.onClick.AddListener(OnRestartClicked);
            }

            if (inGameRestartButton != null)
            {
                inGameRestartButton.onClick.AddListener(OnRestartClicked);
            }

            if (inGameMenuButton != null)
            {
                inGameMenuButton.onClick.AddListener(OnMenuClicked);
            }
        }

        private void HidePanels()
        {
            if (winPanel != null) winPanel.SetActive(false);
            if (losePanel != null) losePanel.SetActive(false);
        }

        private bool isClaimingReward = false;

        private void HandleLevelLoaded(int levelIndex)
        {
            isClaimingReward = false;
            HidePanels();
            if (inGameMenuButton != null)
            {
                inGameMenuButton.interactable = true;
            }


            if (levelIndexTMP != null)
            {
                string title = levelLoader.CurrentLevel != null && !string.IsNullOrEmpty(levelLoader.CurrentLevel.levelTitle)
                    ? levelLoader.CurrentLevel.levelTitle
                    : $"Level {levelIndex}";

                levelIndexTMP.text = title;
            }
            if (levelIndexText != null)
            {
                string title = levelLoader.CurrentLevel != null && !string.IsNullOrEmpty(levelLoader.CurrentLevel.levelTitle)
                    ? levelLoader.CurrentLevel.levelTitle
                    : $"Level {levelIndex}";

                levelIndexText.text = title;
            }
        }

        private void HandleTimerUpdated(float remainingSeconds)
        {
            int clamped = Mathf.Max(0, Mathf.CeilToInt(remainingSeconds));
            int minutes = clamped / 60;
            int seconds = clamped % 60;
            string formatted = $"{minutes:00}:{seconds:00}";

            float scaleX = 1f;
            float scaleY = 1f;

            if (remainingSeconds <= 10f && remainingSeconds > 0f)
            {
                float fraction = remainingSeconds - Mathf.Floor(remainingSeconds);
                float elapsedInSecond = 1f - fraction;
                float duration = 0.55f;

                if (elapsedInSecond < duration)
                {
                    float t = elapsedInSecond / duration;
                    if (t < 0.5f)
                    {
                        float p1 = t / 0.5f;
                        float bumpY = Mathf.Sin(p1 * Mathf.PI);
                        scaleY = 1f + bumpY * 0.30f;
                    }
                    else
                    {
                        float p2 = (t - 0.5f) / 0.5f;
                        float bumpX = Mathf.Sin(p2 * Mathf.PI);
                        scaleX = 1f + bumpX * 0.30f;
                    }
                }
            }

            Vector3 targetScale = new Vector3(scaleX, scaleY, 1f);

            if (timerTextTMP != null)
            {
                timerTextTMP.text = formatted;
                timerTextTMP.transform.localScale = targetScale;
            }
            if (timerText != null)
            {
                timerText.text = formatted;
                timerText.transform.localScale = targetScale;
            }
        }

        private void HandleLevelWon()
        {
            if (inGameMenuButton != null)
            {
                inGameMenuButton.interactable = false;
            }
            StartCoroutine(ShowWinPanelDelayedRoutine());
        }

        private IEnumerator ShowWinPanelDelayedRoutine()
        {
            yield return new WaitForSeconds(winPanelDelay);

            if (winPanel != null)
            {
                UIButtonPressEffect.AttachToAllIn(winPanel);
                UIPanelSlideIn.AttachTo(winPanel);
                UIConfettiEffect.AttachTo(winPanel);
                AttachPrimaryButtonPolish(winPanel);
                HookupWinPanelRewardButtons(winPanel);
                winPanel.SetActive(true);
            }

            if (winTitleTMP != null && levelLoader != null && levelLoader.CurrentLevel != null)
            {
                winTitleTMP.text = $"Level {levelLoader.CurrentLevel.levelIndex}\nCompleted!";
            }
            if (winTitleText != null && levelLoader != null && levelLoader.CurrentLevel != null)
            {
                winTitleText.text = $"Level {levelLoader.CurrentLevel.levelIndex}\nCompleted!";
            }
        }

        private void HookupWinPanelRewardButtons(GameObject panel)
        {
            if (panel == null) return;
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
                    btn.onClick.AddListener(() => ClaimRewardAndNext(80));
                }
                else if (btnName.Contains("40") || btnText.Contains("40") || btnName.Contains("next") || btnName.Contains("continue") || btnText.Contains("далі") || btnText.Contains("next") || btnText.Contains("continue"))
                {
                    btn.onClick = new Button.ButtonClickedEvent();
                    btn.onClick.AddListener(() => ClaimRewardAndNext(40));
                }
            }
        }

        private void ClaimRewardAndNext(int amount)
        {
            if (isClaimingReward) return;
            isClaimingReward = true;

            if (CoinManager.Instance != null)
            {
                CoinManager.Instance.AddCoins(amount);
            }

            OnNextLevelClicked();
        }

        private void HandleLevelLost()
        {
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

        public void OnNextLevelClicked()
        {
            HidePanels();
            if (levelLoader != null)
            {
                levelLoader.LoadNextLevel();
            }
        }

        public void OnRestartClicked()
        {
            HidePanels();
            if (levelLoader != null)
            {
                levelLoader.RestartCurrentLevel();
            }
        }

        public void OnMenuClicked()
        {
            if (GameSettingsManager.HasInstance)
            {
                GameSettingsManager.Instance.PlayClickSound();
            }

            if (!string.IsNullOrEmpty(menuSceneName))
            {
                SceneManager.LoadScene(menuSceneName);
            }
        }
    }
}
