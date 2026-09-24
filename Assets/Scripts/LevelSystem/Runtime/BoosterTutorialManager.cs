using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace LegoPuzzle.Runtime
{
    /// <summary>
    /// Керує туторіалами бустерів (заморозка часу на р.2, підказка на р.3, молоток на р.4).
    /// Затемнює екран напівпрозорою панеллю під час показу руки та виділяє цільовий бустер.
    /// </summary>
    public class BoosterTutorialManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private LevelLoader levelLoader;

        [Header("Boosters")]
        [SerializeField] private TimeFreezeBooster timeFreezeBooster;
        [SerializeField] private HintBooster hintBooster;
        [SerializeField] private HammerBooster hammerBooster;

        [Header("Панель затемнення (Dimming Overlay)")]
        [Tooltip("Об'єкт панелі затемнення на Canvas (якщо не вказано, шукається або створюється автоматично)")]
        [SerializeField] private GameObject dimmingOverlayPanel;

        [Tooltip("Максимальна прозорість затемнення (0.55 = 55% чорного)")]
        [Range(0.1f, 0.95f)]
        [SerializeField] private float dimAlpha = 0.55f;

        [Tooltip("Тривалість плавного фейду панелі затемнення в секундах")]
        [SerializeField] private float fadeDuration = 0.22f;

        private CanvasGroup dimCanvasGroup;
        private Coroutine fadeCoroutine;
        private Button elevatedBoosterButton;
        private Canvas temporaryBoosterCanvas;
        private GraphicRaycaster temporaryBoosterRaycaster;
        private bool addedBoosterCanvas = false;
        private bool addedBoosterRaycaster = false;

        public static bool IsBoosterTutorialActive { get; private set; }

        private void Awake()
        {
            if (levelLoader == null) levelLoader = FindAnyObjectByType<LevelLoader>();
            if (timeFreezeBooster == null) timeFreezeBooster = FindAnyObjectByType<TimeFreezeBooster>(FindObjectsInactive.Include);
            if (hintBooster == null) hintBooster = FindAnyObjectByType<HintBooster>(FindObjectsInactive.Include);
            if (hammerBooster == null) hammerBooster = FindAnyObjectByType<HammerBooster>(FindObjectsInactive.Include);

            EnsureDimmingOverlay();
        }

        private void OnEnable()
        {
            if (levelLoader != null)
            {
                levelLoader.OnLevelLoaded += HandleLevelLoaded;
                levelLoader.OnLevelWon += HandleLevelEnd;
                levelLoader.OnLevelLost += HandleLevelEnd;
            }
        }

        private void OnDisable()
        {
            if (levelLoader != null)
            {
                levelLoader.OnLevelLoaded -= HandleLevelLoaded;
                levelLoader.OnLevelWon -= HandleLevelEnd;
                levelLoader.OnLevelLost -= HandleLevelEnd;
            }
            IsBoosterTutorialActive = false;
            HideDimmingOverlay(immediate: true);
        }

        private void HandleLevelEnd()
        {
            IsBoosterTutorialActive = false;
            TutorialHandEffect.Dismiss();
            HideDimmingOverlay(immediate: true);
        }

        private void HandleLevelLoaded(int levelIndex)
        {
            // Якщо це рівень з туторіалом бустера (2, 3 або 4), блокування активується миттєво
            IsBoosterTutorialActive = (levelIndex >= 2 && levelIndex <= 4);
            StartCoroutine(UpdateBoostersRoutine(levelIndex));
        }

        private IEnumerator UpdateBoostersRoutine(int levelIndex)
        {
            yield return new WaitForEndOfFrame();

            if (timeFreezeBooster == null) timeFreezeBooster = FindAnyObjectByType<TimeFreezeBooster>(FindObjectsInactive.Include);
            if (hintBooster == null) hintBooster = FindAnyObjectByType<HintBooster>(FindObjectsInactive.Include);
            if (hammerBooster == null) hammerBooster = FindAnyObjectByType<HammerBooster>(FindObjectsInactive.Include);

            bool freezeEnabled = levelIndex >= 2;
            bool hintEnabled = levelIndex >= 3;
            bool hammerEnabled = levelIndex >= 4;

            if (timeFreezeBooster != null)
            {
                var btn = timeFreezeBooster.GetComponent<Button>();
                if (btn != null) btn.gameObject.SetActive(freezeEnabled);
            }

            if (hintBooster != null)
            {
                var btn = hintBooster.GetComponent<Button>();
                if (btn != null) btn.gameObject.SetActive(hintEnabled);
            }

            if (hammerBooster != null)
            {
                var btn = hammerBooster.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.RemoveListener(OnTutorialBoosterClicked);
                    btn.gameObject.SetActive(hammerEnabled);
                }
            }

            // Туторіали
            GameObject handPrefab = levelLoader != null ? levelLoader.TutorialHandPrefab : null;

            if (levelIndex == 2 && timeFreezeBooster != null)
            {
                IsBoosterTutorialActive = true;
                var btn = timeFreezeBooster.GetComponent<Button>();
                if (btn != null)
                {
                    ShowDimmingOverlay(btn);
                    TutorialHandEffect.ShowUI(btn.GetComponent<RectTransform>(), handPrefab);
                    btn.onClick.RemoveListener(OnTutorialBoosterClicked);
                    btn.onClick.AddListener(OnTutorialBoosterClicked);
                }
            }
            else if (levelIndex == 3 && hintBooster != null)
            {
                IsBoosterTutorialActive = true;
                var btn = hintBooster.GetComponent<Button>();
                if (btn != null)
                {
                    ShowDimmingOverlay(btn);
                    TutorialHandEffect.ShowUI(btn.GetComponent<RectTransform>(), handPrefab);
                    btn.onClick.RemoveListener(OnTutorialBoosterClicked);
                    btn.onClick.AddListener(OnTutorialBoosterClicked);
                }
            }
            else if (levelIndex == 4 && hammerBooster != null)
            {
                IsBoosterTutorialActive = true;
                var btn = hammerBooster.GetComponent<Button>();
                if (btn != null)
                {
                    ShowDimmingOverlay(btn);
                    TutorialHandEffect.ShowUI(btn.GetComponent<RectTransform>(), handPrefab);
                    btn.onClick.RemoveListener(OnTutorialBoosterClicked);
                    btn.onClick.AddListener(OnTutorialBoosterClicked);
                }
            }
            else
            {
                IsBoosterTutorialActive = false;
                HideDimmingOverlay(immediate: true);
            }
        }

        private void OnTutorialBoosterClicked()
        {
            IsBoosterTutorialActive = false;
            // Не знищуємо руку, якщо бустер перейшов у режим вибору блоку (молоток сам показує руку на блоках)
            if (!HammerBooster.IsTargeting)
            {
                TutorialHandEffect.Dismiss();
            }
            HideDimmingOverlay(immediate: true);
        }

        #region Dimming Overlay Logic

        private GameObject EnsureDimmingOverlay()
        {
            if (dimmingOverlayPanel != null)
            {
                if (dimCanvasGroup == null) dimCanvasGroup = dimmingOverlayPanel.GetComponent<CanvasGroup>();
                return dimmingOverlayPanel;
            }

            // 1. Пошук існуючого об'єкта на сцені
            var existing = GameObject.Find("BoosterTutorialDimOverlay");
            if (existing != null)
            {
                dimmingOverlayPanel = existing;
                dimCanvasGroup = dimmingOverlayPanel.GetComponent<CanvasGroup>();
                return dimmingOverlayPanel;
            }

            // 2. Створення нового оверлею в Canvas
            Canvas canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null) return null;

            dimmingOverlayPanel = new GameObject("BoosterTutorialDimOverlay");
            dimmingOverlayPanel.transform.SetParent(canvas.transform, false);

            RectTransform rt = dimmingOverlayPanel.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // Задаємо Canvas сортування затемнення (вище основного UI, але нижче виділеного бустера)
            Canvas subCanvas = dimmingOverlayPanel.AddComponent<Canvas>();
            subCanvas.overrideSorting = true;
            subCanvas.sortingOrder = 10;

            dimmingOverlayPanel.AddComponent<GraphicRaycaster>();

            dimCanvasGroup = dimmingOverlayPanel.AddComponent<CanvasGroup>();
            dimCanvasGroup.alpha = 0f;
            dimCanvasGroup.blocksRaycasts = true;
            dimCanvasGroup.interactable = true;

            Image img = dimmingOverlayPanel.AddComponent<Image>();
            img.color = Color.black;
            img.raycastTarget = true;

            // Блокування кліків повз бустер + легкий імпульс підказки
            Button overlayBtn = dimmingOverlayPanel.AddComponent<Button>();
            overlayBtn.transition = Selectable.Transition.None;
            overlayBtn.onClick.AddListener(OnDimOverlayClicked);

            dimmingOverlayPanel.SetActive(false);
            return dimmingOverlayPanel;
        }

        private void OnDimOverlayClicked()
        {
            if (elevatedBoosterButton != null)
            {
                var pressEffect = elevatedBoosterButton.GetComponent<UIButtonPressEffect>();
                if (pressEffect != null)
                {
                    pressEffect.TriggerHorizontalJelly();
                }
            }
        }

        private void ShowDimmingOverlay(Button targetBooster)
        {
            EnsureDimmingOverlay();
            if (dimmingOverlayPanel == null) return;

            // Відновлюємо попередню кнопку, якщо була
            RestoreElevatedBooster();

            // Піднімаємо цільову кнопку бустера над шаром затемнення
            if (targetBooster != null)
            {
                elevatedBoosterButton = targetBooster;

                temporaryBoosterCanvas = targetBooster.gameObject.GetComponent<Canvas>();
                if (temporaryBoosterCanvas == null)
                {
                    temporaryBoosterCanvas = targetBooster.gameObject.AddComponent<Canvas>();
                }
                temporaryBoosterCanvas.enabled = true;
                temporaryBoosterCanvas.overrideSorting = true;
                temporaryBoosterCanvas.sortingOrder = 15;

                temporaryBoosterRaycaster = targetBooster.gameObject.GetComponent<GraphicRaycaster>();
                if (temporaryBoosterRaycaster == null)
                {
                    temporaryBoosterRaycaster = targetBooster.gameObject.AddComponent<GraphicRaycaster>();
                }
                temporaryBoosterRaycaster.enabled = true;
            }

            dimmingOverlayPanel.SetActive(true);

            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeRoutine(dimAlpha, fadeDuration));
        }

        private void HideDimmingOverlay(bool immediate = false)
        {
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
                fadeCoroutine = null;
            }

            if (immediate || dimmingOverlayPanel == null || !gameObject.activeInHierarchy)
            {
                if (dimCanvasGroup != null) dimCanvasGroup.alpha = 0f;
                if (dimmingOverlayPanel != null) dimmingOverlayPanel.SetActive(false);
                RestoreElevatedBooster();
                return;
            }

            fadeCoroutine = StartCoroutine(FadeOutAndDisableRoutine());
        }

        private IEnumerator FadeRoutine(float targetAlpha, float duration)
        {
            if (dimCanvasGroup == null) yield break;

            float startAlpha = dimCanvasGroup.alpha;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                dimCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                yield return null;
            }

            dimCanvasGroup.alpha = targetAlpha;
            fadeCoroutine = null;
        }

        private IEnumerator FadeOutAndDisableRoutine()
        {
            if (dimCanvasGroup != null)
            {
                float startAlpha = dimCanvasGroup.alpha;
                float elapsed = 0f;
                float duration = fadeDuration * 0.8f;

                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    dimCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
                    yield return null;
                }

                dimCanvasGroup.alpha = 0f;
            }

            if (dimmingOverlayPanel != null)
            {
                dimmingOverlayPanel.SetActive(false);
            }

            RestoreElevatedBooster();
            fadeCoroutine = null;
        }

        private void RestoreElevatedBooster()
        {
            if (elevatedBoosterButton != null)
            {
                if (temporaryBoosterCanvas != null)
                {
                    temporaryBoosterCanvas.overrideSorting = false;
                    temporaryBoosterCanvas.sortingOrder = 0;
                    temporaryBoosterCanvas.enabled = true;
                }

                if (temporaryBoosterRaycaster != null)
                {
                    temporaryBoosterRaycaster.enabled = true;
                }

                elevatedBoosterButton = null;
                temporaryBoosterCanvas = null;
                temporaryBoosterRaycaster = null;
                addedBoosterCanvas = false;
                addedBoosterRaycaster = false;
            }
        }

        #endregion
    }
}
