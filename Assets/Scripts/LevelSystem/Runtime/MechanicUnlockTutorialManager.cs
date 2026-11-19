using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LegoPuzzle.Runtime
{
    /// <summary>
    /// Shared mechanic unlock panel (scene-authored).
    /// Level 10: one-direction blocks (authored content as-is).
    /// Levels 20 / 30 / 40 / 50: swap BlockIcon sprite + Body text for the show, then restore after hide.
    /// </summary>
    public class MechanicUnlockTutorialManager : MonoBehaviour
    {
        public const int OneWayUnlockLevelIndex = 10;
        public const int FrozenUnlockLevelIndex = 20;
        public const int DoubleLayerUnlockLevelIndex = 30;
        public const int MovableBarrierUnlockLevelIndex = 40;
        public const int FrozenGateUnlockLevelIndex = 50;
        private const string PanelName = "MechanicUnlockTutorialPanel";
        private const string FrozenBrickResourcePath = "Sprites/translucent_ice_brick_four_studs";
        private const string DoubleLayerBrickResourcePath = "Sprites/glossy_blue_and_red_building_block";
        private const string MovableBarrierBrickResourcePath = "Sprites/wooden_movable_barrier_block";
        private const string FrozenGateBrickResourcePath = "Sprites/glossy_ice_play_button_bar";
        private const string FrozenBodyText =
            "Frozen blocks <color=#5CE1FF>can't move</color>. Clear other blocks to crack the ice — the number shows how many clears are left.";
        private const string DoubleLayerBodyText =
            "Double blocks have <color=#5CE1FF>two layers</color>. Clear once to peel the outer shell — the inner block stays until you clear it again.";
        private const string MovableBarrierBodyText =
            "Wooden barriers <color=#5CE1FF>never exit</color>. Slide them aside to clear a path — they aren't needed to win.";
        private const string FrozenGateBodyText =
            "Frozen gates <color=#5CE1FF>won't let blocks exit</color>. Clear other blocks to crack the ice — the number shows how many clears are left.";

        private enum MechanicKind
        {
            None,
            OneWay,
            Frozen,
            DoubleLayer,
            MovableBarrier,
            FrozenGate
        }

        [Header("References")]
        [SerializeField] private LevelLoader levelLoader;
        [Tooltip("Scene-authored panel. Assigned by editor setup; also resolved by name if empty.")]
        [SerializeField] private GameObject tutorialPanel;

        [Header("Visual")]
        [SerializeField] private float fadeDuration = 0.22f;
        [SerializeField] private float glowSpinDegreesPerSecond = 28f;

        private CanvasGroup panelCanvasGroup;
        private RectTransform glowRaysRt;
        private Image blockIconImage;
        private TMP_Text bodyText;
        private Button gotItButton;
        private Coroutine fadeCoroutine;
        private Coroutine showCoroutine;
        private bool tutorialActive;
        private bool pausedTimeScale;
        private float timeScaleBeforePause = 1f;
        private bool gotItWired;
        private MechanicKind pendingKind = MechanicKind.None;

        private Sprite authoredBlockSprite;
        private string authoredBodyText;
        private bool contentOverridesActive;

        private static Sprite cachedFrozenBrickSprite;
        private static Sprite cachedDoubleLayerBrickSprite;
        private static Sprite cachedMovableBarrierBrickSprite;
        private static Sprite cachedFrozenGateBrickSprite;

        public static bool IsMechanicTutorialActive { get; private set; }

        private void Awake()
        {
            if (levelLoader == null) levelLoader = FindAnyObjectByType<LevelLoader>();
            ResolvePanelRefs();
            HidePanel(immediate: true);
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

            EndTutorial(markShown: false, immediate: true);
        }

        private void Update()
        {
            if (!tutorialActive || glowRaysRt == null) return;
            glowRaysRt.Rotate(0f, 0f, -glowSpinDegreesPerSecond * Time.unscaledDeltaTime, Space.Self);
        }

        private void HandleLevelEnd() => EndTutorial(markShown: false, immediate: true);

        private void HandleLevelLoaded(int levelIndex)
        {
            EndTutorial(markShown: false, immediate: true);

            MechanicKind kind = MechanicKind.None;
            if (levelIndex == OneWayUnlockLevelIndex && !WasShown(MechanicKind.OneWay))
                kind = MechanicKind.OneWay;
            else if (levelIndex == FrozenUnlockLevelIndex && !WasShown(MechanicKind.Frozen))
                kind = MechanicKind.Frozen;
            else if (levelIndex == DoubleLayerUnlockLevelIndex && !WasShown(MechanicKind.DoubleLayer))
                kind = MechanicKind.DoubleLayer;
            else if (levelIndex == MovableBarrierUnlockLevelIndex && !WasShown(MechanicKind.MovableBarrier))
                kind = MechanicKind.MovableBarrier;
            else if (levelIndex == FrozenGateUnlockLevelIndex && !WasShown(MechanicKind.FrozenGate))
                kind = MechanicKind.FrozenGate;

            if (kind == MechanicKind.None) return;

            pendingKind = kind;
            if (showCoroutine != null) StopCoroutine(showCoroutine);
            showCoroutine = StartCoroutine(ShowTutorialDelayed());
        }

        private IEnumerator ShowTutorialDelayed()
        {
            yield return new WaitForEndOfFrame();
            yield return null;
            ShowTutorial(pendingKind);
            showCoroutine = null;
        }

        private void ShowTutorial(MechanicKind kind)
        {
            ResolvePanelRefs();
            if (tutorialPanel == null)
            {
                Debug.LogWarning("[MechanicUnlockTutorial] Panel missing on Canvas. In Edit Mode run LEGO → Create Mechanic Unlock Tutorial Panel and save Game.");
                return;
            }

            WireGotItButton();
            ApplyContentForKind(kind);

            pendingKind = kind;
            tutorialActive = true;
            IsMechanicTutorialActive = true;
            PauseGameplayTime();

            tutorialPanel.SetActive(true);
            tutorialPanel.transform.SetAsLastSibling();

            if (panelCanvasGroup != null)
                panelCanvasGroup.alpha = 0f;

            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadePanel(1f));
        }

        private void OnGotItClicked()
        {
            if (!tutorialActive) return;
            EndTutorial(markShown: true, immediate: false);
        }

        private void EndTutorial(bool markShown, bool immediate)
        {
            if (showCoroutine != null)
            {
                StopCoroutine(showCoroutine);
                showCoroutine = null;
            }

            if (markShown && pendingKind != MechanicKind.None)
                MarkShown(pendingKind);

            tutorialActive = false;
            IsMechanicTutorialActive = false;
            RestoreGameplayTime();
            pendingKind = MechanicKind.None;
            // Restore authored sprite/text only after the panel is fully hidden,
            // otherwise the player sees the content snap back during fade-out.
            HidePanel(immediate);
        }

        private void HidePanel(bool immediate)
        {
            if (tutorialPanel == null) return;

            if (immediate || !gameObject.activeInHierarchy)
            {
                if (fadeCoroutine != null)
                {
                    StopCoroutine(fadeCoroutine);
                    fadeCoroutine = null;
                }
                if (panelCanvasGroup != null)
                    panelCanvasGroup.alpha = 0f;
                tutorialPanel.SetActive(false);
                RestoreAuthoredContent();
                return;
            }

            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeOutAndHide());
        }

        private void PauseGameplayTime()
        {
            if (pausedTimeScale) return;
            timeScaleBeforePause = Time.timeScale;
            if (Time.timeScale > 0.01f)
                Time.timeScale = 0f;
            pausedTimeScale = true;
        }

        private void RestoreGameplayTime()
        {
            if (!pausedTimeScale) return;
            Time.timeScale = timeScaleBeforePause > 0.01f ? timeScaleBeforePause : 1f;
            pausedTimeScale = false;
        }

        #region Content swap (levels 20 / 30 / 40 / 50)

        private void ApplyContentForKind(MechanicKind kind)
        {
            // Level 10: keep scene-authored sprite/text untouched.
            if (kind == MechanicKind.OneWay || kind == MechanicKind.None) return;

            CacheAuthoredContentIfNeeded();

            Sprite sprite = null;
            string body = null;

            switch (kind)
            {
                case MechanicKind.Frozen:
                    sprite = GetFrozenBrickSprite();
                    body = FrozenBodyText;
                    break;
                case MechanicKind.DoubleLayer:
                    sprite = GetDoubleLayerBrickSprite();
                    body = DoubleLayerBodyText;
                    break;
                case MechanicKind.MovableBarrier:
                    sprite = GetMovableBarrierBrickSprite();
                    body = MovableBarrierBodyText;
                    break;
                case MechanicKind.FrozenGate:
                    sprite = GetFrozenGateBrickSprite();
                    body = FrozenGateBodyText;
                    break;
            }

            if (blockIconImage != null && sprite != null)
                blockIconImage.sprite = sprite;

            if (bodyText != null && !string.IsNullOrEmpty(body))
                bodyText.text = body;

            contentOverridesActive = true;
        }

        private void CacheAuthoredContentIfNeeded()
        {
            if (contentOverridesActive) return;

            if (blockIconImage != null)
                authoredBlockSprite = blockIconImage.sprite;
            if (bodyText != null)
                authoredBodyText = bodyText.text;
        }

        private void RestoreAuthoredContent()
        {
            if (!contentOverridesActive) return;

            if (blockIconImage != null)
                blockIconImage.sprite = authoredBlockSprite;
            if (bodyText != null)
                bodyText.text = authoredBodyText;

            contentOverridesActive = false;
        }

        private static Sprite GetFrozenBrickSprite()
        {
            if (cachedFrozenBrickSprite != null)
                return cachedFrozenBrickSprite;

            cachedFrozenBrickSprite = Resources.Load<Sprite>(FrozenBrickResourcePath);
            if (cachedFrozenBrickSprite != null)
                return cachedFrozenBrickSprite;

            return FindSpriteByNameHint(
                "Translucent Ice Brick",
                "translucent_ice_brick",
                out cachedFrozenBrickSprite);
        }

        private static Sprite GetDoubleLayerBrickSprite()
        {
            if (cachedDoubleLayerBrickSprite != null)
                return cachedDoubleLayerBrickSprite;

            cachedDoubleLayerBrickSprite = Resources.Load<Sprite>(DoubleLayerBrickResourcePath);
            if (cachedDoubleLayerBrickSprite != null)
                return cachedDoubleLayerBrickSprite;

            return FindSpriteByNameHint(
                "Glossy Blue and Red Building Block",
                "glossy_blue_and_red",
                out cachedDoubleLayerBrickSprite);
        }

        private static Sprite GetMovableBarrierBrickSprite()
        {
            if (cachedMovableBarrierBrickSprite != null)
                return cachedMovableBarrierBrickSprite;

            cachedMovableBarrierBrickSprite = Resources.Load<Sprite>(MovableBarrierBrickResourcePath);
            if (cachedMovableBarrierBrickSprite != null)
                return cachedMovableBarrierBrickSprite;

            return FindSpriteByNameHint(
                "Gemini_Generated_Image_gt9q0pgt9q0pgt9q-Photoroom",
                "wooden_movable_barrier",
                out cachedMovableBarrierBrickSprite);
        }

        private static Sprite GetFrozenGateBrickSprite()
        {
            if (cachedFrozenGateBrickSprite != null)
                return cachedFrozenGateBrickSprite;

            cachedFrozenGateBrickSprite = Resources.Load<Sprite>(FrozenGateBrickResourcePath);
            if (cachedFrozenGateBrickSprite != null)
                return cachedFrozenGateBrickSprite;

            return FindSpriteByNameHint(
                "Glossy Ice Play Button Bar",
                "glossy_ice_play_button_bar",
                out cachedFrozenGateBrickSprite);
        }

        private static Sprite FindSpriteByNameHint(string hintA, string hintB, out Sprite cache)
        {
            cache = null;
            var sprites = Resources.FindObjectsOfTypeAll<Sprite>();
            for (int i = 0; i < sprites.Length; i++)
            {
                Sprite s = sprites[i];
                if (s == null) continue;
                if (s.name.IndexOf(hintA, System.StringComparison.OrdinalIgnoreCase) >= 0
                    || s.name.IndexOf(hintB, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    cache = s;
                    return s;
                }
            }

            return null;
        }

        #endregion

        #region Save

        private static bool WasShown(MechanicKind kind)
        {
            if (SaveDataService.HasInstance && SaveDataService.Instance != null && SaveDataService.Instance.Data != null)
            {
                return kind switch
                {
                    MechanicKind.OneWay => SaveDataService.Instance.Data.mechanicUnlockTutorialOneWayShown,
                    MechanicKind.Frozen => SaveDataService.Instance.Data.mechanicUnlockTutorialFrozenShown,
                    MechanicKind.DoubleLayer => SaveDataService.Instance.Data.mechanicUnlockTutorialDoubleLayerShown,
                    MechanicKind.MovableBarrier => SaveDataService.Instance.Data.mechanicUnlockTutorialMovableBarrierShown,
                    MechanicKind.FrozenGate => SaveDataService.Instance.Data.mechanicUnlockTutorialFrozenGateShown,
                    _ => true
                };
            }

            return kind switch
            {
                MechanicKind.OneWay => PlayerPrefs.GetInt("LEGO_MechanicTutorial_OneWay", 0) == 1,
                MechanicKind.Frozen => PlayerPrefs.GetInt("LEGO_MechanicTutorial_Frozen", 0) == 1,
                MechanicKind.DoubleLayer => PlayerPrefs.GetInt("LEGO_MechanicTutorial_DoubleLayer", 0) == 1,
                MechanicKind.MovableBarrier => PlayerPrefs.GetInt("LEGO_MechanicTutorial_MovableBarrier", 0) == 1,
                MechanicKind.FrozenGate => PlayerPrefs.GetInt("LEGO_MechanicTutorial_FrozenGate", 0) == 1,
                _ => true
            };
        }

        private static void MarkShown(MechanicKind kind)
        {
            if (SaveDataService.HasInstance && SaveDataService.Instance != null && SaveDataService.Instance.Data != null)
            {
                switch (kind)
                {
                    case MechanicKind.OneWay:
                        SaveDataService.Instance.Data.mechanicUnlockTutorialOneWayShown = true;
                        break;
                    case MechanicKind.Frozen:
                        SaveDataService.Instance.Data.mechanicUnlockTutorialFrozenShown = true;
                        break;
                    case MechanicKind.DoubleLayer:
                        SaveDataService.Instance.Data.mechanicUnlockTutorialDoubleLayerShown = true;
                        break;
                    case MechanicKind.MovableBarrier:
                        SaveDataService.Instance.Data.mechanicUnlockTutorialMovableBarrierShown = true;
                        break;
                    case MechanicKind.FrozenGate:
                        SaveDataService.Instance.Data.mechanicUnlockTutorialFrozenGateShown = true;
                        break;
                }
                SaveDataService.Instance.Save();
            }

            switch (kind)
            {
                case MechanicKind.OneWay:
                    PlayerPrefs.SetInt("LEGO_MechanicTutorial_OneWay", 1);
                    break;
                case MechanicKind.Frozen:
                    PlayerPrefs.SetInt("LEGO_MechanicTutorial_Frozen", 1);
                    break;
                case MechanicKind.DoubleLayer:
                    PlayerPrefs.SetInt("LEGO_MechanicTutorial_DoubleLayer", 1);
                    break;
                case MechanicKind.MovableBarrier:
                    PlayerPrefs.SetInt("LEGO_MechanicTutorial_MovableBarrier", 1);
                    break;
                case MechanicKind.FrozenGate:
                    PlayerPrefs.SetInt("LEGO_MechanicTutorial_FrozenGate", 1);
                    break;
            }
            PlayerPrefs.Save();
        }

        #endregion

        #region Resolve (read-only)

        private void ResolvePanelRefs()
        {
            if (tutorialPanel == null)
            {
                Canvas canvas = FindAnyObjectByType<Canvas>();
                if (canvas != null)
                {
                    Transform found = FindNamed(canvas.transform, PanelName);
                    if (found != null)
                        tutorialPanel = found.gameObject;
                }
            }

            if (tutorialPanel == null) return;

            if (panelCanvasGroup == null)
                panelCanvasGroup = tutorialPanel.GetComponent<CanvasGroup>();

            if (glowRaysRt == null)
            {
                Transform glow = FindNamed(tutorialPanel.transform, "GlowRays");
                if (glow != null)
                    glowRaysRt = glow as RectTransform;
            }

            if (blockIconImage == null)
            {
                Transform iconTr = FindNamed(tutorialPanel.transform, "BlockIcon");
                if (iconTr != null)
                    blockIconImage = iconTr.GetComponent<Image>();
            }

            if (bodyText == null)
            {
                Transform bodyTr = FindNamed(tutorialPanel.transform, "Body");
                if (bodyTr != null)
                    bodyText = bodyTr.GetComponent<TMP_Text>();
            }

            if (gotItButton == null)
            {
                Transform btnTr = FindNamed(tutorialPanel.transform, "GotItButton");
                if (btnTr != null)
                    gotItButton = btnTr.GetComponent<Button>();
            }

            EnsureShimmerEffects();
        }

        private void EnsureShimmerEffects()
        {
            Transform titleTr = FindNamed(tutorialPanel.transform, "Title");
            if (titleTr != null)
                UITextShimmer.AttachTo(titleTr.gameObject, new Color(1f, 0.78f, 0.18f, 1f));

            if (gotItButton != null)
                UIButtonShinePulse.AttachTo(gotItButton.gameObject, pulseMax: 1.035f, shineDelay: 1.8f);
        }

        private void WireGotItButton()
        {
            if (gotItButton == null || gotItWired) return;
            gotItButton.onClick.RemoveListener(OnGotItClicked);
            gotItButton.onClick.AddListener(OnGotItClicked);
            gotItWired = true;
        }

        #endregion

        #region Fade

        private IEnumerator FadePanel(float target)
        {
            if (panelCanvasGroup == null)
            {
                fadeCoroutine = null;
                yield break;
            }

            float start = panelCanvasGroup.alpha;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                panelCanvasGroup.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / fadeDuration));
                yield return null;
            }

            panelCanvasGroup.alpha = target;
            fadeCoroutine = null;
        }

        private IEnumerator FadeOutAndHide()
        {
            if (panelCanvasGroup != null)
            {
                float start = panelCanvasGroup.alpha;
                float elapsed = 0f;
                float dur = fadeDuration * 0.8f;
                while (elapsed < dur)
                {
                    elapsed += Time.unscaledDeltaTime;
                    panelCanvasGroup.alpha = Mathf.Lerp(start, 0f, Mathf.Clamp01(elapsed / dur));
                    yield return null;
                }
                panelCanvasGroup.alpha = 0f;
            }

            if (tutorialPanel != null)
                tutorialPanel.SetActive(false);
            RestoreAuthoredContent();
            fadeCoroutine = null;
        }

        #endregion

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
    }
}
