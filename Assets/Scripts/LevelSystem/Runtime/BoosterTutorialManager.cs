using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LegoPuzzle.Runtime
{
    /// <summary>
    /// Туторіал бустерів (р.2 Freeze, р.3 Hint, р.4 Hammer):
    /// 1) Scene-authored BoosterUnlockInfoPanel on Canvas — stays until tap anywhere.
    /// 2) After tap, panel hides; then hand + dimming on the booster button.
    /// Unlock panel is never created at runtime (LEGO → Create Booster Unlock Info Panel).
    /// </summary>
    public class BoosterTutorialManager : MonoBehaviour
    {
        private enum TutorialPhase
        {
            None,
            Intro,
            PointAtBooster
        }

        [Header("References")]
        [SerializeField] private LevelLoader levelLoader;

        [Header("Boosters")]
        [SerializeField] private TimeFreezeBooster timeFreezeBooster;
        [SerializeField] private HintBooster hintBooster;
        [SerializeField] private HammerBooster hammerBooster;

        [Header("Панель затемнення (Dimming Overlay)")]
        [SerializeField] private GameObject dimmingOverlayPanel;
        [Range(0.1f, 0.95f)]
        [SerializeField] private float dimAlpha = 0.55f;
        [SerializeField] private float fadeDuration = 0.22f;

        [Header("Unlock Info Panel")]
        [SerializeField] private GameObject unlockInfoPanel;
        [SerializeField] private Image unlockIconImage;
        [SerializeField] private TMP_Text unlockTitleText;
        [SerializeField] private TMP_Text unlockBodyText;

        private CanvasGroup dimCanvasGroup;
        private CanvasGroup unlockInfoCanvasGroup;
        private GameObject introTapCatcher;
        private Coroutine fadeCoroutine;
        private Button elevatedBoosterButton;
        private Canvas temporaryBoosterCanvas;
        private GraphicRaycaster temporaryBoosterRaycaster;
        private bool addedBoosterCanvas;
        private bool addedBoosterRaycaster;

        private TutorialPhase phase = TutorialPhase.None;
        private Button pendingBoosterButton;
        private GameObject pendingHandPrefab;

        public static bool IsBoosterTutorialActive { get; private set; }

        private void Awake()
        {
            if (levelLoader == null) levelLoader = FindAnyObjectByType<LevelLoader>();
            if (timeFreezeBooster == null) timeFreezeBooster = FindAnyObjectByType<TimeFreezeBooster>(FindObjectsInactive.Include);
            if (hintBooster == null) hintBooster = FindAnyObjectByType<HintBooster>(FindObjectsInactive.Include);
            if (hammerBooster == null) hammerBooster = FindAnyObjectByType<HammerBooster>(FindObjectsInactive.Include);

            EnsureDimmingOverlay();
            ResolveUnlockInfoPanelRefs();
            EnsureIntroTapCatcher();
            HideUnlockInfoPanel(immediate: true);
            SetIntroTapCatcherActive(false);
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

            EndTutorialHard();
        }

        private void HandleLevelEnd()
        {
            EndTutorialHard();
        }

        private void EndTutorialHard()
        {
            phase = TutorialPhase.None;
            pendingBoosterButton = null;
            pendingHandPrefab = null;
            IsBoosterTutorialActive = false;
            TutorialHandEffect.Dismiss();
            HideDimmingOverlay(immediate: true);
            HideUnlockInfoPanel(immediate: true);
            SetIntroTapCatcherActive(false);
        }

        private void HandleLevelLoaded(int levelIndex)
        {
            IsBoosterTutorialActive = (levelIndex >= 2 && levelIndex <= 4);
            phase = TutorialPhase.None;
            HideUnlockInfoPanel(immediate: true);
            SetIntroTapCatcherActive(false);
            HideDimmingOverlay(immediate: true);
            TutorialHandEffect.Dismiss();
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

            GameObject handPrefab = levelLoader != null ? levelLoader.TutorialHandPrefab : null;

            if (levelIndex == 2 && timeFreezeBooster != null)
            {
                var btn = timeFreezeBooster.GetComponent<Button>();
                if (btn != null)
                {
                    BeginIntroPhase(
                        btn,
                        handPrefab,
                        ExtractIconFromBooster(btn.transform),
                        "Congratulations! New booster unlocked: Freeze Time",
                        "Pauses the level timer so you can calmly plan your next moves.");
                }
            }
            else if (levelIndex == 3 && hintBooster != null)
            {
                var btn = hintBooster.GetComponent<Button>();
                if (btn != null)
                {
                    BeginIntroPhase(
                        btn,
                        handPrefab,
                        ExtractIconFromBooster(btn.transform),
                        "Congratulations! New booster unlocked: Hint",
                        "Highlights the best next move to help you clear the board.");
                }
            }
            else if (levelIndex == 4 && hammerBooster != null)
            {
                var btn = hammerBooster.GetComponent<Button>();
                if (btn != null)
                {
                    BeginIntroPhase(
                        btn,
                        handPrefab,
                        ExtractIconFromBooster(btn.transform),
                        "Congratulations! New booster unlocked: Hammer",
                        "Smash any piece on the board to clear a blocked path.");
                }
            }
            else
            {
                IsBoosterTutorialActive = false;
                phase = TutorialPhase.None;
                HideDimmingOverlay(immediate: true);
                HideUnlockInfoPanel(immediate: true);
                SetIntroTapCatcherActive(false);
            }
        }

        private void BeginIntroPhase(Button boosterBtn, GameObject handPrefab, Sprite icon, string title, string body)
        {
            IsBoosterTutorialActive = true;
            phase = TutorialPhase.Intro;
            pendingBoosterButton = boosterBtn;
            pendingHandPrefab = handPrefab;

            // No hand yet — only scene-authored description + soft dim + tap-anywhere catcher.
            TutorialHandEffect.Dismiss();
            ShowDimmingOverlay(elevateBooster: null);
            ShowUnlockInfoPanel(icon, title, body);
            SetIntroTapCatcherActive(true);
        }

        private void BeginPointAtBoosterPhase()
        {
            if (phase != TutorialPhase.Intro || pendingBoosterButton == null)
                return;

            phase = TutorialPhase.PointAtBooster;
            SetIntroTapCatcherActive(false);
            HideUnlockInfoPanel(immediate: false);

            Button btn = pendingBoosterButton;
            ShowDimmingOverlay(elevateBooster: btn);
            TutorialHandEffect.ShowUI(btn.GetComponent<RectTransform>(), pendingHandPrefab);

            btn.onClick.RemoveListener(OnTutorialBoosterClicked);
            btn.onClick.AddListener(OnTutorialBoosterClicked);
        }

        private void OnTutorialBoosterClicked()
        {
            phase = TutorialPhase.None;
            IsBoosterTutorialActive = false;
            pendingBoosterButton = null;
            pendingHandPrefab = null;

            if (!HammerBooster.IsTargeting)
                TutorialHandEffect.Dismiss();

            HideDimmingOverlay(immediate: true);
            HideUnlockInfoPanel(immediate: true);
            SetIntroTapCatcherActive(false);
        }

        private void OnIntroTapAnywhere()
        {
            if (phase == TutorialPhase.Intro)
                BeginPointAtBoosterPhase();
        }

        #region Unlock Info Panel

        private void ShowUnlockInfoPanel(Sprite icon, string title, string body)
        {
            ResolveUnlockInfoPanelRefs();
            if (unlockInfoPanel == null)
            {
                Debug.LogWarning("[BoosterTutorial] BoosterUnlockInfoPanel missing on Canvas. In Edit Mode run LEGO → Create Booster Unlock Info Panel and save Game.");
                return;
            }

            if (unlockIconImage != null)
            {
                unlockIconImage.sprite = icon;
                unlockIconImage.enabled = icon != null;
                unlockIconImage.preserveAspect = true;
            }

            if (unlockTitleText != null)
                unlockTitleText.text = title ?? "";
            if (unlockBodyText != null)
                unlockBodyText.text = body ?? "";

            ApplyUnlockTextLayout();

            unlockInfoPanel.SetActive(true);
            // Keep authored canvas parent/pose — only raise draw order while visible.
            unlockInfoPanel.transform.SetAsLastSibling();

            if (unlockInfoCanvasGroup != null)
            {
                unlockInfoCanvasGroup.alpha = 0f;
                unlockInfoCanvasGroup.blocksRaycasts = false;
                unlockInfoCanvasGroup.interactable = false;
            }

            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeUnlockInfoIn());
        }

        private void HideUnlockInfoPanel(bool immediate)
        {
            if (unlockInfoPanel == null) return;

            if (immediate || !gameObject.activeInHierarchy)
            {
                if (unlockInfoCanvasGroup != null)
                    unlockInfoCanvasGroup.alpha = 0f;
                unlockInfoPanel.SetActive(false);
                return;
            }

            StartCoroutine(FadeUnlockInfoOut());
        }

        private IEnumerator FadeUnlockInfoIn()
        {
            if (unlockInfoCanvasGroup == null) yield break;

            float duration = fadeDuration;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                unlockInfoCanvasGroup.alpha = Mathf.Clamp01(elapsed / duration);
                yield return null;
            }

            unlockInfoCanvasGroup.alpha = 1f;
            fadeCoroutine = null;
        }

        private IEnumerator FadeUnlockInfoOut()
        {
            if (unlockInfoCanvasGroup == null)
            {
                if (unlockInfoPanel != null) unlockInfoPanel.SetActive(false);
                yield break;
            }

            float start = unlockInfoCanvasGroup.alpha;
            float duration = fadeDuration * 0.8f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                unlockInfoCanvasGroup.alpha = Mathf.Lerp(start, 0f, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            unlockInfoCanvasGroup.alpha = 0f;
            unlockInfoPanel.SetActive(false);
        }

        /// <summary>
        /// Scene-authored panel only. Never creates hierarchy at runtime.
        /// </summary>
        private void ResolveUnlockInfoPanelRefs()
        {
            if (unlockInfoPanel == null)
            {
                Transform found = null;
                Canvas canvas = FindAnyObjectByType<Canvas>();
                if (canvas != null)
                    found = FindNamed(canvas.transform, "BoosterUnlockInfoPanel");
                if (found != null)
                    unlockInfoPanel = found.gameObject;
            }

            if (unlockInfoPanel == null)
                return;

            if (unlockInfoCanvasGroup == null)
                unlockInfoCanvasGroup = unlockInfoPanel.GetComponent<CanvasGroup>();

            if (unlockIconImage == null)
            {
                Transform iconTr = unlockInfoPanel.transform.Find("Icon");
                if (iconTr != null)
                    unlockIconImage = iconTr.GetComponent<Image>();
            }

            if (unlockTitleText == null)
            {
                Transform titleTr = unlockInfoPanel.transform.Find("Texts/Title");
                if (titleTr != null)
                    unlockTitleText = titleTr.GetComponent<TMP_Text>();
            }

            if (unlockBodyText == null)
            {
                Transform bodyTr = unlockInfoPanel.transform.Find("Texts/Body");
                if (bodyTr != null)
                    unlockBodyText = bodyTr.GetComponent<TMP_Text>();
            }

            ApplyUnlockTextLayout();
        }

        private void ApplyUnlockTextLayout()
        {
            if (unlockTitleText != null)
            {
                unlockTitleText.enableWordWrapping = true;
                unlockTitleText.overflowMode = TextOverflowModes.Overflow;
                unlockTitleText.enableAutoSizing = true;
                unlockTitleText.fontSizeMin = 18f;
                unlockTitleText.fontSizeMax = 28f;
            }

            if (unlockBodyText != null)
            {
                unlockBodyText.enableWordWrapping = true;
                unlockBodyText.overflowMode = TextOverflowModes.Overflow;
                unlockBodyText.enableAutoSizing = true;
                unlockBodyText.fontSizeMin = 16f;
                unlockBodyText.fontSizeMax = 22f;
            }
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

        private static Sprite ExtractIconFromBooster(Transform boosterTransform)
        {
            if (boosterTransform == null) return null;

            Transform childImg = boosterTransform.Find("Image");
            if (childImg != null)
            {
                var img = childImg.GetComponent<Image>();
                if (img != null && img.sprite != null)
                    return img.sprite;
            }

            var images = boosterTransform.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                Image img = images[i];
                if (img != null && img.gameObject != boosterTransform.gameObject && img.sprite != null)
                    return img.sprite;
            }

            var selfImg = boosterTransform.GetComponent<Image>();
            return selfImg != null ? selfImg.sprite : null;
        }

        #endregion

        #region Intro Tap Catcher

        private void EnsureIntroTapCatcher()
        {
            if (introTapCatcher != null) return;

            // Never use GameObject.Find here — it asserts during OnDisable / inactive hierarchies.
            Canvas canvas = FindAnyObjectByType<Canvas>();
            if (canvas != null)
            {
                Transform existing = FindNamed(canvas.transform, "BoosterTutorialIntroTapCatcher");
                if (existing != null)
                {
                    introTapCatcher = existing.gameObject;
                    return;
                }
            }

            if (canvas == null) return;

            introTapCatcher = new GameObject("BoosterTutorialIntroTapCatcher", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Canvas));
            introTapCatcher.transform.SetParent(canvas.transform, false);

            var rt = introTapCatcher.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var canvasComp = introTapCatcher.GetComponent<Canvas>();
            canvasComp.overrideSorting = true;
            canvasComp.sortingOrder = 30; // Above info panel so any tap is caught

            introTapCatcher.AddComponent<GraphicRaycaster>();

            var img = introTapCatcher.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.01f); // nearly invisible, still raycastable
            img.raycastTarget = true;

            var btn = introTapCatcher.GetComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(OnIntroTapAnywhere);

            introTapCatcher.SetActive(false);
        }

        private void SetIntroTapCatcherActive(bool active)
        {
            // Cleanup / OnDisable: never create or Find — only disable an already-known instance.
            if (!active)
            {
                if (introTapCatcher != null)
                    introTapCatcher.SetActive(false);
                return;
            }

            EnsureIntroTapCatcher();
            if (introTapCatcher == null) return;
            introTapCatcher.SetActive(true);
            introTapCatcher.transform.SetAsLastSibling();
        }

        #endregion

        #region Dimming Overlay Logic

        private GameObject EnsureDimmingOverlay()
        {
            if (dimmingOverlayPanel != null)
            {
                if (dimCanvasGroup == null) dimCanvasGroup = dimmingOverlayPanel.GetComponent<CanvasGroup>();
                return dimmingOverlayPanel;
            }

            Canvas canvas = FindAnyObjectByType<Canvas>();
            if (canvas != null)
            {
                Transform existing = FindNamed(canvas.transform, "BoosterTutorialDimOverlay");
                if (existing != null)
                {
                    dimmingOverlayPanel = existing.gameObject;
                    dimCanvasGroup = dimmingOverlayPanel.GetComponent<CanvasGroup>();
                    return dimmingOverlayPanel;
                }
            }

            if (canvas == null) return null;

            dimmingOverlayPanel = new GameObject("BoosterTutorialDimOverlay");
            dimmingOverlayPanel.transform.SetParent(canvas.transform, false);

            RectTransform rt = dimmingOverlayPanel.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

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

            Button overlayBtn = dimmingOverlayPanel.AddComponent<Button>();
            overlayBtn.transition = Selectable.Transition.None;
            overlayBtn.onClick.AddListener(OnDimOverlayClicked);

            dimmingOverlayPanel.SetActive(false);
            return dimmingOverlayPanel;
        }

        private void OnDimOverlayClicked()
        {
            if (phase == TutorialPhase.Intro)
            {
                BeginPointAtBoosterPhase();
                return;
            }

            if (elevatedBoosterButton != null)
            {
                var pressEffect = elevatedBoosterButton.GetComponent<UIButtonPressEffect>();
                if (pressEffect != null)
                    pressEffect.TriggerHorizontalJelly();
            }
        }

        private void ShowDimmingOverlay(Button elevateBooster)
        {
            EnsureDimmingOverlay();
            if (dimmingOverlayPanel == null) return;

            RestoreElevatedBooster();

            if (elevateBooster != null)
            {
                elevatedBoosterButton = elevateBooster;

                temporaryBoosterCanvas = elevateBooster.gameObject.GetComponent<Canvas>();
                if (temporaryBoosterCanvas == null)
                {
                    temporaryBoosterCanvas = elevateBooster.gameObject.AddComponent<Canvas>();
                    addedBoosterCanvas = true;
                }

                temporaryBoosterCanvas.enabled = true;
                temporaryBoosterCanvas.overrideSorting = true;
                temporaryBoosterCanvas.sortingOrder = 15;

                temporaryBoosterRaycaster = elevateBooster.gameObject.GetComponent<GraphicRaycaster>();
                if (temporaryBoosterRaycaster == null)
                {
                    temporaryBoosterRaycaster = elevateBooster.gameObject.AddComponent<GraphicRaycaster>();
                    addedBoosterRaycaster = true;
                }

                temporaryBoosterRaycaster.enabled = true;
            }

            dimmingOverlayPanel.SetActive(true);
            dimmingOverlayPanel.transform.SetAsLastSibling();

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
                dimmingOverlayPanel.SetActive(false);

            RestoreElevatedBooster();
            fadeCoroutine = null;
        }

        private void RestoreElevatedBooster()
        {
            if (elevatedBoosterButton == null) return;

            if (temporaryBoosterCanvas != null)
            {
                temporaryBoosterCanvas.overrideSorting = false;
                temporaryBoosterCanvas.sortingOrder = 0;
                temporaryBoosterCanvas.enabled = true;
            }

            if (temporaryBoosterRaycaster != null)
                temporaryBoosterRaycaster.enabled = true;

            elevatedBoosterButton = null;
            temporaryBoosterCanvas = null;
            temporaryBoosterRaycaster = null;
            addedBoosterCanvas = false;
            addedBoosterRaycaster = false;
        }

        #endregion
    }
}
