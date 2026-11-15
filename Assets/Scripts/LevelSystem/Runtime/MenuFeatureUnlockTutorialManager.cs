using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace LegoPuzzle.Runtime
{
    /// <summary>
    /// Menu FTUE: when Daily (10) / Slots (15) / Lotto (20) unlock, show the same
    /// dim + pointing hand used for booster unlocks, aimed at the newly unlocked button.
    /// Runs once per feature on the unlock level.
    /// </summary>
    public class MenuFeatureUnlockTutorialManager : MonoBehaviour
    {
        private enum FeatureKind
        {
            Daily,
            Slots,
            Lotto
        }

        [Header("Visual")]
        [SerializeField] private GameObject tutorialHandPrefab;
        [SerializeField] private GameObject dimmingOverlayPanel;
        [Range(0.1f, 0.95f)]
        [SerializeField] private float dimAlpha = 0.55f;
        [SerializeField] private float fadeDuration = 0.22f;

        private CanvasGroup dimCanvasGroup;
        private Coroutine fadeCoroutine;
        private Coroutine runCoroutine;

        private Button elevatedButton;
        private Canvas temporaryButtonCanvas;
        private GraphicRaycaster temporaryButtonRaycaster;
        private bool addedButtonCanvas;
        private bool addedButtonRaycaster;

        private Button pendingButton;
        private FeatureKind pendingKind;
        private bool tutorialActive;

        public static bool IsMenuUnlockTutorialActive { get; private set; }

        public static MenuFeatureUnlockTutorialManager EnsureOn(GameObject host)
        {
            if (host == null) return null;
            var existing = host.GetComponent<MenuFeatureUnlockTutorialManager>();
            if (existing != null) return existing;
            return host.AddComponent<MenuFeatureUnlockTutorialManager>();
        }

        private void Start()
        {
            if (tutorialHandPrefab == null)
                tutorialHandPrefab = Resources.Load<GameObject>("Prefabs/tutorial_hand");

            EnsureDimmingOverlay();
            runCoroutine = StartCoroutine(TryShowUnlockTutorialsRoutine());
        }

        private void OnDisable()
        {
            CleanupTutorialVisuals(removeClickListener: true);
        }

        private IEnumerator TryShowUnlockTutorialsRoutine()
        {
            yield return null;
            yield return new WaitForEndOfFrame();

            RefreshAllLockVisuals();

            if (TryBegin(FeatureKind.Daily, DailyRewardsButton.UnlockLevel, FindDailyButton))
                yield break;
            if (TryBegin(FeatureKind.Slots, SlotsButton.UnlockLevel, FindSlotsButton))
                yield break;
            if (TryBegin(FeatureKind.Lotto, LottoButton.UnlockLevel, FindLottoButton))
                yield break;
        }

        private bool TryBegin(FeatureKind kind, int unlockLevel, System.Func<Button> findButton)
        {
            if (WasShown(kind)) return false;
            if (SlotsButton.GetPlayerLevelNumber() != unlockLevel) return false;

            Button btn = findButton();
            if (btn == null || !btn.gameObject.activeInHierarchy) return false;

            BeginPointAtButton(kind, btn);
            return true;
        }

        private void BeginPointAtButton(FeatureKind kind, Button btn)
        {
            CleanupTutorialVisuals(removeClickListener: true);

            pendingKind = kind;
            pendingButton = btn;
            tutorialActive = true;
            IsMenuUnlockTutorialActive = true;

            btn.onClick.RemoveListener(OnTutorialTargetClicked);
            btn.onClick.AddListener(OnTutorialTargetClicked);

            ShowDimmingOverlay(btn);
            TutorialHandEffect.ShowUI(btn.GetComponent<RectTransform>(), tutorialHandPrefab);
        }

        private void OnTutorialTargetClicked()
        {
            if (!tutorialActive) return;

            MarkShown(pendingKind);
            CleanupTutorialVisuals(removeClickListener: true);
        }

        private void CleanupTutorialVisuals(bool removeClickListener)
        {
            if (runCoroutine != null)
            {
                StopCoroutine(runCoroutine);
                runCoroutine = null;
            }

            if (removeClickListener && pendingButton != null)
                pendingButton.onClick.RemoveListener(OnTutorialTargetClicked);

            pendingButton = null;
            tutorialActive = false;
            IsMenuUnlockTutorialActive = false;

            TutorialHandEffect.Dismiss();
            HideDimmingOverlay(immediate: true);
        }

        #region Seen flags

        private static bool WasShown(FeatureKind kind)
        {
            var data = GetSaveData();
            if (data == null) return false;
            return kind switch
            {
                FeatureKind.Daily => data.menuUnlockTutorialDailyShown,
                FeatureKind.Slots => data.menuUnlockTutorialSlotsShown,
                FeatureKind.Lotto => data.menuUnlockTutorialLottoShown,
                _ => false
            };
        }

        private static void MarkShown(FeatureKind kind)
        {
            var data = GetSaveData();
            if (data == null) return;

            switch (kind)
            {
                case FeatureKind.Daily: data.menuUnlockTutorialDailyShown = true; break;
                case FeatureKind.Slots: data.menuUnlockTutorialSlotsShown = true; break;
                case FeatureKind.Lotto: data.menuUnlockTutorialLottoShown = true; break;
            }

            SaveDataService.Instance.Save();
        }

        private static GameSaveData GetSaveData()
        {
            if (SaveDataService.HasInstance && SaveDataService.Instance != null)
                return SaveDataService.Instance.Data;
            return null;
        }

        #endregion

        #region Find targets

        private static void RefreshAllLockVisuals()
        {
            var daily = FindAnyObjectByType<DailyRewardsButton>(FindObjectsInactive.Include);
            if (daily != null) daily.RefreshLockVisual();

            var slots = FindAnyObjectByType<SlotsButton>(FindObjectsInactive.Include);
            if (slots != null) slots.RefreshLockVisual();

            var lotto = FindAnyObjectByType<LottoButton>(FindObjectsInactive.Include);
            if (lotto != null) lotto.RefreshLockVisual();
        }

        private static Button FindDailyButton()
        {
            var c = FindAnyObjectByType<DailyRewardsButton>(FindObjectsInactive.Include);
            return c != null ? c.GetComponent<Button>() : null;
        }

        private static Button FindSlotsButton()
        {
            var c = FindAnyObjectByType<SlotsButton>(FindObjectsInactive.Include);
            return c != null ? c.GetComponent<Button>() : null;
        }

        private static Button FindLottoButton()
        {
            var c = FindAnyObjectByType<LottoButton>(FindObjectsInactive.Include);
            return c != null ? c.GetComponent<Button>() : null;
        }

        #endregion

        #region Dimming

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
                Transform existing = FindNamed(canvas.transform, "MenuFeatureUnlockDimOverlay");
                if (existing != null)
                {
                    dimmingOverlayPanel = existing.gameObject;
                    dimCanvasGroup = dimmingOverlayPanel.GetComponent<CanvasGroup>();
                    return dimmingOverlayPanel;
                }
            }

            if (canvas == null) return null;

            dimmingOverlayPanel = new GameObject("MenuFeatureUnlockDimOverlay");
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
            if (elevatedButton == null) return;
            var pressEffect = elevatedButton.GetComponent<UIButtonPressEffect>();
            if (pressEffect != null)
                pressEffect.TriggerHorizontalJelly();
        }

        private void ShowDimmingOverlay(Button elevateButton)
        {
            EnsureDimmingOverlay();
            if (dimmingOverlayPanel == null) return;

            RestoreElevatedButton();

            if (elevateButton != null)
            {
                elevatedButton = elevateButton;

                temporaryButtonCanvas = elevateButton.gameObject.GetComponent<Canvas>();
                if (temporaryButtonCanvas == null)
                {
                    temporaryButtonCanvas = elevateButton.gameObject.AddComponent<Canvas>();
                    addedButtonCanvas = true;
                }

                temporaryButtonCanvas.enabled = true;
                temporaryButtonCanvas.overrideSorting = true;
                temporaryButtonCanvas.sortingOrder = 15;

                temporaryButtonRaycaster = elevateButton.gameObject.GetComponent<GraphicRaycaster>();
                if (temporaryButtonRaycaster == null)
                {
                    temporaryButtonRaycaster = elevateButton.gameObject.AddComponent<GraphicRaycaster>();
                    addedButtonRaycaster = true;
                }

                temporaryButtonRaycaster.enabled = true;
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
                RestoreElevatedButton();
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
                dimCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, Mathf.Clamp01(elapsed / duration));
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
                    dimCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, Mathf.Clamp01(elapsed / duration));
                    yield return null;
                }
                dimCanvasGroup.alpha = 0f;
            }

            if (dimmingOverlayPanel != null)
                dimmingOverlayPanel.SetActive(false);

            RestoreElevatedButton();
            fadeCoroutine = null;
        }

        private void RestoreElevatedButton()
        {
            if (elevatedButton == null) return;

            // Keep added Canvas/Raycaster (same as booster tutorial) — only drop override sort.
            if (temporaryButtonCanvas != null)
            {
                temporaryButtonCanvas.overrideSorting = false;
                temporaryButtonCanvas.sortingOrder = 0;
            }

            elevatedButton = null;
            temporaryButtonCanvas = null;
            temporaryButtonRaycaster = null;
            addedButtonCanvas = false;
            addedButtonRaycaster = false;
        }

        #endregion
    }
}
