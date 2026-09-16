using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace LegoPuzzle.Runtime
{
    /// <summary>
    /// Displays and manages the Hearts / Lives UI exclusively in the Menu scene.
    /// Clean horizontal layout: [Heart Icon]  [Count]  [FULL / Timer], matching user's reference.
    /// Supports [ExecuteAlways] for live scene view editing and repositioning.
    /// </summary>
    [ExecuteAlways]
    public class MenuHeartsUI : MonoBehaviour
    {
        public static MenuHeartsUI Instance { get; private set; }

        [Header("UI Element References (Editable in Hierarchy & Scene)")]
        [SerializeField] private RectTransform widgetContainer;
        [SerializeField] private Image heartIconImage;
        [SerializeField] private TMP_Text heartCountText;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private Sprite heartSprite;

        [Header("Display Format")]
        [Tooltip("If true, shows just '5' like in reference. If false, shows '5 / 5'.")]
        [SerializeField] private bool showOnlyCurrentCount = true;

        [Header("Style Settings")]
        [SerializeField] private Color fullTimerColor = new Color(0.28f, 0.92f, 0.48f, 1f); // Vibrant lime green from reference
        [SerializeField] private Color recoveringTimerColor = new Color(1f, 0.85f, 0.3f, 1f); // Soft gold
        [SerializeField] private Color warningColor = new Color(1f, 0.35f, 0.35f, 1f); // Red
        [SerializeField] private Color normalCountColor = Color.white;

        [Header("Animations")]
        [SerializeField] private bool enableHeartPulse = true;

        private GameObject toastBanner;
        private Coroutine shakeCoroutine;
        private Coroutine toastCoroutine;

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            Instance = this;
            EnsureHeartManager();
            EnsureUIHierarchy();
            BindEvents();
            UpdateUI();
        }

        private void Start()
        {
            EnsureHeartManager();
            EnsureUIHierarchy();
            BindEvents();
            UpdateUI();
        }

        private void OnValidate()
        {
            EnsureUIHierarchy();
            UpdateUI();
        }

        private void OnDestroy()
        {
            if (HeartManager.Instance != null)
            {
                HeartManager.Instance.OnHeartsChanged -= HandleHeartsChanged;
                HeartManager.Instance.OnTimerTick -= HandleTimerTick;
            }
        }

        private void Update()
        {
            // Subtle heartbeat pulse in play mode
            if (Application.isPlaying && enableHeartPulse && heartIconImage != null)
            {
                bool hasHearts = HeartManager.Instance != null && HeartManager.Instance.HasHearts();
                if (hasHearts)
                {
                    float t = Time.time * 2.5f;
                    float beat = Mathf.Sin(t);
                    float scale = 1f + (beat > 0.65f ? (beat - 0.65f) * 0.18f : 0f);
                    heartIconImage.transform.localScale = new Vector3(scale, scale, 1f);
                }
                else
                {
                    heartIconImage.transform.localScale = Vector3.one;
                }
            }
        }

        private void EnsureHeartManager()
        {
            if (!Application.isPlaying) return;

            if (HeartManager.Instance == null)
            {
                HeartManager existing = FindAnyObjectByType<HeartManager>();
                if (existing == null)
                {
                    GameObject hmObj = new GameObject("[HeartManager]");
                    hmObj.AddComponent<HeartManager>();
                }
            }
        }

        private void BindEvents()
        {
            if (HeartManager.Instance != null)
            {
                HeartManager.Instance.OnHeartsChanged -= HandleHeartsChanged;
                HeartManager.Instance.OnHeartsChanged += HandleHeartsChanged;
                HeartManager.Instance.OnTimerTick -= HandleTimerTick;
                HeartManager.Instance.OnTimerTick += HandleTimerTick;
            }
        }

        private void HandleHeartsChanged(int current, int max)
        {
            UpdateUI();
        }

        private void HandleTimerTick(float remainingSeconds)
        {
            if (timerText != null && HeartManager.Instance != null)
            {
                if (HeartManager.Instance.CurrentHearts >= HeartManager.Instance.MaxHearts)
                {
                    timerText.text = "FULL";
                    timerText.color = fullTimerColor;
                }
                else
                {
                    timerText.text = HeartManager.Instance.GetFormattedRecoveryTime();
                    timerText.color = recoveringTimerColor;
                }
            }
        }

        public void UpdateUI()
        {
            int current = 5;
            int max = 5;
            string timeStr = "FULL";
            Color timeCol = fullTimerColor;

            if (HeartManager.Instance != null)
            {
                current = HeartManager.Instance.CurrentHearts;
                max = HeartManager.Instance.MaxHearts;
                if (current >= max)
                {
                    timeStr = "FULL";
                    timeCol = fullTimerColor;
                }
                else
                {
                    timeStr = HeartManager.Instance.GetFormattedRecoveryTime();
                    timeCol = recoveringTimerColor;
                }
            }

            if (heartCountText != null)
            {
                heartCountText.text = showOnlyCurrentCount ? current.ToString() : $"{current} / {max}";
                heartCountText.color = current == 0 ? warningColor : normalCountColor;
            }

            if (timerText != null)
            {
                timerText.text = timeStr;
                timerText.color = timeCol;
            }

            if (heartIconImage != null)
            {
                if (current == 0)
                {
                    heartIconImage.color = new Color(0.55f, 0.55f, 0.55f, 0.7f); // Dimmed / empty
                }
                else
                {
                    heartIconImage.color = Color.white; // Full vibrant red
                }

                if (heartIconImage.sprite == null)
                {
                    heartIconImage.sprite = GetHeartSprite();
                }
            }
        }

        /// <summary>
        /// Public entry point called when player has 0 hearts and presses Play.
        /// </summary>
        public static void TriggerNoHeartsFeedback()
        {
            if (Instance != null)
            {
                Instance.PlayNoHeartsAnimation();
            }
        }

        public void PlayNoHeartsAnimation()
        {
            if (shakeCoroutine != null) StopCoroutine(shakeCoroutine);
            shakeCoroutine = StartCoroutine(ShakeWidgetRoutine());

            if (toastCoroutine != null) StopCoroutine(toastCoroutine);
            toastCoroutine = StartCoroutine(ShowToastRoutine("НЕМАЄ ЖИТТІВ!\nЗАЧЕКАЙТЕ ВІДНОВЛЕННЯ"));
        }

        private IEnumerator ShakeWidgetRoutine()
        {
            if (widgetContainer == null) yield break;

            Vector2 originalPos = widgetContainer.anchoredPosition;
            float elapsed = 0f;
            float duration = 0.45f;
            float intensity = 20f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float decay = 1f - (elapsed / duration);
                float offsetX = Mathf.Sin(elapsed * 50f) * intensity * decay;
                widgetContainer.anchoredPosition = originalPos + new Vector2(offsetX, 0f);
                yield return null;
            }

            widgetContainer.anchoredPosition = originalPos;
        }

        private IEnumerator ShowToastRoutine(string message)
        {
            if (toastBanner == null)
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                if (canvas == null) canvas = FindAnyObjectByType<Canvas>();
                if (canvas != null)
                {
                    toastBanner = CreateRuntimeToast(canvas);
                }
            }

            if (toastBanner == null) yield break;

            TMP_Text msgText = toastBanner.GetComponentInChildren<TMP_Text>();
            if (msgText != null) msgText.text = message;

            CanvasGroup cg = toastBanner.GetComponent<CanvasGroup>();
            RectTransform rt = toastBanner.GetComponent<RectTransform>();

            toastBanner.SetActive(true);
            cg.alpha = 0f;
            Vector2 targetPos = new Vector2(0f, 40f);
            Vector2 startPos = new Vector2(0f, -20f);

            // Pop in
            float inDuration = 0.25f;
            float inElapsed = 0f;
            while (inElapsed < inDuration)
            {
                inElapsed += Time.unscaledDeltaTime;
                float progress = inElapsed / inDuration;
                float ease = Mathf.Sin(progress * Mathf.PI * 0.5f);
                cg.alpha = Mathf.Lerp(0f, 1f, progress);
                rt.anchoredPosition = Vector2.Lerp(startPos, targetPos, ease);
                yield return null;
            }

            rt.anchoredPosition = targetPos;
            cg.alpha = 1f;

            yield return new WaitForSecondsRealtime(2.2f);

            // Fade out
            float outDuration = 0.35f;
            float outElapsed = 0f;
            while (outElapsed < outDuration)
            {
                outElapsed += Time.unscaledDeltaTime;
                float progress = outElapsed / outDuration;
                cg.alpha = Mathf.Lerp(1f, 0f, progress);
                yield return null;
            }

            toastBanner.SetActive(false);
        }

        /// <summary>
        /// Links existing hierarchy elements if present on scene, or creates clean horizontal row [Heart] [5] [FULL].
        /// </summary>
        public void EnsureUIHierarchy()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null) return;

            if (Application.isPlaying && canvas.GetComponent<MenuCoinsUI>() == null && FindAnyObjectByType<MenuCoinsUI>() == null)
            {
                canvas.gameObject.AddComponent<MenuCoinsUI>();
            }

            // 1. Look for pre-existing HeartsWidget in scene
            if (widgetContainer == null)
            {
                Transform existing = canvas.transform.Find("HeartsWidget");
                if (existing != null)
                {
                    widgetContainer = existing.GetComponent<RectTransform>();
                }
            }

            if (widgetContainer != null)
            {
                // Auto-hook children
                if (heartIconImage == null)
                {
                    Transform iconTr = widgetContainer.Find("HeartIcon");
                    if (iconTr != null) heartIconImage = iconTr.GetComponent<Image>();
                }
                if (heartCountText == null)
                {
                    Transform countTr = widgetContainer.Find("HeartCountText");
                    if (countTr != null) heartCountText = countTr.GetComponent<TMP_Text>();
                }
                if (timerText == null)
                {
                    Transform timerTr = widgetContainer.Find("TimerText");
                    if (timerTr != null) timerText = timerTr.GetComponent<TMP_Text>();
                }

                if (heartIconImage != null && heartIconImage.sprite == null)
                {
                    heartIconImage.sprite = GetHeartSprite();
                }
                EnsureButtonListener();
                return;
            }

            // 2. If completely missing in scene, create horizontal row: [HeartIcon] [5] [FULL]
            GameObject widgetObj = new GameObject("HeartsWidget");
            widgetObj.transform.SetParent(canvas.transform, false);
            widgetContainer = widgetObj.AddComponent<RectTransform>();

            // Anchor Top-Left / Top-Center
            widgetContainer.anchorMin = new Vector2(0.5f, 1f);
            widgetContainer.anchorMax = new Vector2(0.5f, 1f);
            widgetContainer.pivot = new Vector2(0.5f, 1f);
            widgetContainer.anchoredPosition = new Vector2(0f, -70f);
            widgetContainer.sizeDelta = new Vector2(340f, 76f);

            // Clean horizontal layout
            HorizontalLayoutGroup hlg = widgetObj.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 16f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            // Heart Icon (70x70)
            GameObject iconObj = new GameObject("HeartIcon");
            iconObj.transform.SetParent(widgetContainer, false);
            RectTransform iconRt = iconObj.AddComponent<RectTransform>();
            iconRt.sizeDelta = new Vector2(70f, 70f);

            heartIconImage = iconObj.AddComponent<Image>();
            heartIconImage.sprite = GetHeartSprite();
            heartIconImage.preserveAspect = true;

            // Heart Count Text (Bold 54pt, e.g. "5")
            GameObject countObj = new GameObject("HeartCountText");
            countObj.transform.SetParent(widgetContainer, false);
            RectTransform countRt = countObj.AddComponent<RectTransform>();
            countRt.sizeDelta = new Vector2(50f, 70f);

            heartCountText = countObj.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset font = GetDefaultTMPFont();
            if (font != null) heartCountText.font = font;
            heartCountText.fontSize = 54f;
            heartCountText.fontStyle = FontStyles.Bold;
            heartCountText.alignment = TextAlignmentOptions.MidlineLeft;
            heartCountText.color = normalCountColor;
            heartCountText.text = "5";

            // Timer Text (Bold 48pt, e.g. "FULL")
            GameObject timerObj = new GameObject("TimerText");
            timerObj.transform.SetParent(widgetContainer, false);
            RectTransform timerRt = timerObj.AddComponent<RectTransform>();
            timerRt.sizeDelta = new Vector2(160f, 70f);

            timerText = timerObj.AddComponent<TextMeshProUGUI>();
            if (font != null) timerText.font = font;
            timerText.fontSize = 48f;
            timerText.fontStyle = FontStyles.Bold;
            timerText.alignment = TextAlignmentOptions.MidlineLeft;
            timerText.color = fullTimerColor;
            timerText.text = "FULL";

            EnsureButtonListener();
        }

        private void EnsureButtonListener()
        {
            if (widgetContainer == null) return;

            // Ensure an Image on widgetContainer for full-area raycasting
            Image img = widgetContainer.GetComponent<Image>();
            if (img == null)
            {
                img = widgetContainer.gameObject.AddComponent<Image>();
                img.color = new Color(0f, 0f, 0f, 0f); // Completely transparent
            }
            img.raycastTarget = true;

            Button btn = widgetContainer.GetComponent<Button>();
            if (btn == null)
            {
                btn = widgetContainer.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
            }
            btn.onClick.RemoveListener(OnWidgetClicked);
            btn.onClick.AddListener(OnWidgetClicked);

            if (widgetContainer.GetComponent<UIButtonPressEffect>() == null)
            {
                UIButtonPressEffect.AttachTo(widgetContainer.gameObject);
            }
        }

        private void OnWidgetClicked()
        {
            if (MenuShopVideoPanel.IsShopOpen) return;

            if (GameSettingsManager.HasInstance)
            {
                GameSettingsManager.Instance.PlayClickSound();
                GameSettingsManager.Instance.TriggerHaptic();
            }

            MenuShopVideoPanel.OpenShop();
        }

        private GameObject CreateRuntimeToast(Canvas canvas)
        {
            GameObject toastObj = new GameObject("NoHeartsToastBanner");
            toastObj.transform.SetParent(canvas.transform, false);

            RectTransform rt = toastObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(560f, 130f);

            CanvasGroup cg = toastObj.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;

            Image bg = toastObj.AddComponent<Image>();
            bg.sprite = GetDefaultRoundedSprite();
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.85f, 0.15f, 0.2f, 0.95f);

            Outline outline = toastObj.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.7f, 0.7f, 0.8f);
            outline.effectDistance = new Vector2(3f, -3f);

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(toastObj.transform, false);
            RectTransform textRt = textObj.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(20f, 12f);
            textRt.offsetMax = new Vector2(-20f, -12f);

            TMP_Text tmp = textObj.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset font = GetDefaultTMPFont();
            if (font != null) tmp.font = font;
            tmp.fontSize = 30f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.text = "НЕМАЄ ЖИТТІВ!\nЗАЧЕКАЙТЕ ВІДНОВЛЕННЯ";

            toastObj.SetActive(false);
            return toastObj;
        }

        private Sprite GetHeartSprite()
        {
            if (heartSprite != null) return heartSprite;
            var sprites = Resources.FindObjectsOfTypeAll<Sprite>();
            foreach (var s in sprites)
            {
                if (s.name.IndexOf("heart", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    heartSprite = s;
                    return s;
                }
            }
            return null;
        }

        private Sprite GetDefaultRoundedSprite()
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

        private TMP_FontAsset GetDefaultTMPFont()
        {
            var fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            if (fonts != null && fonts.Length > 0)
            {
                foreach (var f in fonts)
                {
                    if (f.name.Contains("rimouski") || f.name.Contains("GROBOLD") || f.name.Contains("Bold"))
                        return f;
                }
                return fonts[0];
            }
            return null;
        }
    }
}
