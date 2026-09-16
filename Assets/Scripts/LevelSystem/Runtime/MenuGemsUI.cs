using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace LegoPuzzle.Runtime
{
    /// <summary>
    /// Displays and manages the Gems UI in the Menu scene.
    /// Clean horizontal layout: [Gem Icon] [Gem Count], placed at the top bar of the Canvas.
    /// Supports [ExecuteAlways] for instant scene view editing and automatic hierarchy setup.
    /// Includes a smooth rolling counter and subtle icon bounce animation when gems are added.
    /// </summary>
    [ExecuteAlways]
    public class MenuGemsUI : MonoBehaviour
    {
        public static MenuGemsUI Instance { get; private set; }

        [Header("UI Element References")]
        [SerializeField] private RectTransform widgetContainer;
        [SerializeField] private Image gemIconImage;
        [SerializeField] private TMP_Text gemsCountTMP;
        [SerializeField] private Text gemsCountLegacy;
        [SerializeField] private Sprite gemSprite;

        [Header("Display Settings")]
        [Tooltip("Format string for the gem counter (e.g. '{0}', '{0:N0}')")]
        [SerializeField] private string countFormat = "{0}";

        [Tooltip("Color of the gems count text")]
        [SerializeField] private Color textColor = Color.white;

        [Header("Animations")]
        [Tooltip("If true, numbers roll up smoothly when gems change")]
        [SerializeField] private bool animateCounter = true;

        [Tooltip("Duration of the roll-up number animation in seconds")]
        [SerializeField] private float countDuration = 0.65f;

        [Tooltip("If true, the gem icon punches/bounces on gem changes")]
        [SerializeField] private bool enableIconBounce = true;

        private int currentDisplayedCount = 0;
        private Coroutine countCoroutine;
        private Coroutine bounceCoroutine;

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            Instance = this;
            EnsureGemManager();
            EnsureUIHierarchy();
            BindEvents();
            UpdateUI(false);
        }

        private void Start()
        {
            EnsureGemManager();
            EnsureUIHierarchy();
            BindEvents();
            UpdateUI(false);
        }

        private void OnValidate()
        {
            EnsureUIHierarchy();
            UpdateUI(false);
        }

        private void OnDestroy()
        {
            if (GemManager.HasInstance)
            {
                GemManager.Instance.OnGemsChanged -= HandleGemsChanged;
            }
        }

        private void EnsureGemManager()
        {
            if (!Application.isPlaying) return;

            if (GemManager.HasInstance) return;

            GemManager existing = FindAnyObjectByType<GemManager>();
            if (existing == null)
            {
                GameObject obj = new GameObject("[GemManager]");
                obj.AddComponent<GemManager>();
            }
        }

        private void BindEvents()
        {
            if (GemManager.HasInstance)
            {
                GemManager.Instance.OnGemsChanged -= HandleGemsChanged;
                GemManager.Instance.OnGemsChanged += HandleGemsChanged;
            }
        }

        private void HandleGemsChanged(int newBalance)
        {
            if (!gameObject.activeInHierarchy)
            {
                currentDisplayedCount = newBalance;
                SetText(newBalance);
                return;
            }

            if (Application.isPlaying && animateCounter && currentDisplayedCount != newBalance)
            {
                if (countCoroutine != null) StopCoroutine(countCoroutine);
                countCoroutine = StartCoroutine(AnimateCounterRoutine(currentDisplayedCount, newBalance));

                if (enableIconBounce && gemIconImage != null)
                {
                    if (bounceCoroutine != null) StopCoroutine(bounceCoroutine);
                    bounceCoroutine = StartCoroutine(IconBounceRoutine());
                }
            }
            else
            {
                currentDisplayedCount = newBalance;
                SetText(newBalance);
            }
        }

        /// <summary>
        /// Updates the UI text immediately or smoothly.
        /// </summary>
        public void UpdateUI(bool animate = false)
        {
            int balance = 0;
            if (GemManager.HasInstance)
            {
                balance = GemManager.Instance.Gems;
            }
            else
            {
                balance = PlayerPrefs.GetInt("LEGO_GemsBalance", 0);
            }

            if (animate && Application.isPlaying && animateCounter)
            {
                HandleGemsChanged(balance);
            }
            else
            {
                currentDisplayedCount = balance;
                SetText(balance);
            }

            if (gemIconImage != null)
            {
                if (gemIconImage.sprite == null)
                {
                    gemIconImage.sprite = GetGemSprite();
                }
            }
        }

        private void SetText(int value)
        {
            string formatted = string.Format(countFormat, value);
            if (gemsCountTMP != null)
            {
                gemsCountTMP.text = formatted;
                gemsCountTMP.color = textColor;
            }
            if (gemsCountLegacy != null)
            {
                gemsCountLegacy.text = formatted;
                gemsCountLegacy.color = textColor;
            }
        }

        private IEnumerator AnimateCounterRoutine(int fromVal, int toVal)
        {
            float elapsed = 0f;
            float duration = Mathf.Max(0.1f, countDuration);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                int current = Mathf.RoundToInt(Mathf.Lerp((float)fromVal, (float)toVal, eased));
                currentDisplayedCount = current;
                SetText(current);
                yield return null;
            }

            currentDisplayedCount = toVal;
            SetText(toVal);
            countCoroutine = null;
        }

        private IEnumerator IconBounceRoutine()
        {
            if (gemIconImage == null) yield break;

            Transform iconTr = gemIconImage.transform;
            float elapsed = 0f;
            float duration = 0.35f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float p = elapsed / duration;
                float scale = 1f + Mathf.Sin(p * Mathf.PI) * 0.28f;
                iconTr.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }

            iconTr.localScale = Vector3.one;
            bounceCoroutine = null;
        }

        /// <summary>
        /// Links existing hierarchy elements if present on scene, or builds horizontal row [Gem] [Count] at Top Right.
        /// </summary>
        public void EnsureUIHierarchy()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null) return;

            // 1. Look for pre-existing GemsWidget in scene
            if (widgetContainer == null)
            {
                Transform existing = canvas.transform.Find("GemsWidget");
                if (existing != null)
                {
                    widgetContainer = existing.GetComponent<RectTransform>();
                }
            }

            if (widgetContainer != null)
            {
                // Auto-hook children
                if (gemIconImage == null)
                {
                    Transform iconTr = widgetContainer.Find("GemIcon");
                    if (iconTr != null) gemIconImage = iconTr.GetComponent<Image>();
                }
                if (gemsCountTMP == null)
                {
                    Transform countTr = widgetContainer.Find("GemsCountText");
                    if (countTr != null) gemsCountTMP = countTr.GetComponent<TMP_Text>();
                }
                if (gemsCountLegacy == null)
                {
                    Transform countTr = widgetContainer.Find("GemsCountText");
                    if (countTr != null) gemsCountLegacy = countTr.GetComponent<Text>();
                }

                if (gemIconImage != null && gemIconImage.sprite == null)
                {
                    gemIconImage.sprite = GetGemSprite();
                }
                EnsureButtonListener();
                return;
            }

            // 2. If completely missing in scene, create horizontal row: [GemIcon] [GemsCountText]
            GameObject widgetObj = new GameObject("GemsWidget");
            widgetObj.transform.SetParent(canvas.transform, false);
            widgetContainer = widgetObj.AddComponent<RectTransform>();

            // Anchor Top-Right (positioned cleanly next to CoinsWidget and SettingsButton)
            widgetContainer.anchorMin = new Vector2(1f, 1f);
            widgetContainer.anchorMax = new Vector2(1f, 1f);
            widgetContainer.pivot = new Vector2(1f, 1f);
            widgetContainer.anchoredPosition = new Vector2(-170f, -70f);
            widgetContainer.sizeDelta = new Vector2(200f, 70f);

            // Gem Icon (64x64)
            GameObject iconObj = new GameObject("GemIcon");
            iconObj.transform.SetParent(widgetContainer, false);
            RectTransform iconRt = iconObj.AddComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0f, 0.5f);
            iconRt.anchorMax = new Vector2(0f, 0.5f);
            iconRt.pivot = new Vector2(0.5f, 0.5f);
            iconRt.anchoredPosition = new Vector2(32f, 0f);
            iconRt.sizeDelta = new Vector2(64f, 64f);

            gemIconImage = iconObj.AddComponent<Image>();
            gemIconImage.sprite = GetGemSprite();
            gemIconImage.preserveAspect = true;
            gemIconImage.raycastTarget = false;

            // Gem Count Text (Bold 52pt)
            GameObject countObj = new GameObject("GemsCountText");
            countObj.transform.SetParent(widgetContainer, false);
            RectTransform countRt = countObj.AddComponent<RectTransform>();
            countRt.anchorMin = new Vector2(0f, 0.5f);
            countRt.anchorMax = new Vector2(0f, 0.5f);
            countRt.pivot = new Vector2(0f, 0.5f);
            countRt.anchoredPosition = new Vector2(80f, 0f);
            countRt.sizeDelta = new Vector2(120f, 64f);

            gemsCountTMP = countObj.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset font = GetDefaultTMPFont();
            if (font != null) gemsCountTMP.font = font;
            gemsCountTMP.fontSize = 52f;
            gemsCountTMP.fontStyle = FontStyles.Bold;
            gemsCountTMP.alignment = TextAlignmentOptions.MidlineLeft;
            gemsCountTMP.color = textColor;
            gemsCountTMP.text = "0";
            gemsCountTMP.raycastTarget = false;

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

        private Sprite GetGemSprite()
        {
            if (gemSprite != null) return gemSprite;

#if UNITY_EDITOR
            // Try loading dedicated high-res gem sprite
            var directAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/gem_icon.png");
            if (directAsset != null)
            {
                gemSprite = directAsset;
                return directAsset;
            }
#endif

            // Search in loaded resources/scenes
            var sprites = Resources.FindObjectsOfTypeAll<Sprite>();
            foreach (var s in sprites)
            {
                if (s.name.IndexOf("gem", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    s.name.IndexOf("diamond", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    gemSprite = s;
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

#if UNITY_EDITOR
        [ContextMenu("Build Gems Widget Now")]
        public void BuildGemsWidgetEditor()
        {
            EnsureGemManager();
            EnsureUIHierarchy();
            UpdateUI(false);
            UnityEditor.EditorUtility.SetDirty(gameObject);
            if (widgetContainer != null)
            {
                UnityEditor.EditorUtility.SetDirty(widgetContainer.gameObject);
            }
        }

        [UnityEditor.MenuItem("LEGO/Setup Menu Gems UI")]
        public static void SetupMenuGemsUIEditor()
        {
            Canvas canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("No Canvas found in current scene.");
                return;
            }

            MenuGemsUI gemsUI = canvas.GetComponent<MenuGemsUI>();
            if (gemsUI == null)
            {
                gemsUI = canvas.gameObject.AddComponent<MenuGemsUI>();
            }

            gemsUI.EnsureGemManager();
            gemsUI.EnsureUIHierarchy();
            gemsUI.UpdateUI(false);
            UnityEditor.EditorUtility.SetDirty(canvas.gameObject);
            Debug.Log("<color=#D946EF><b>MenuGemsUI:</b> Gems widget successfully configured on Canvas!</color>");
        }
#endif
    }
}
