using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace LegoPuzzle.Runtime
{
    /// <summary>
    /// Displays and manages the Coins UI in the Menu scene.
    /// Clean horizontal layout: [Coin Icon] [Coin Count], placed at the top right of the Canvas.
    /// Supports [ExecuteAlways] for instant scene view editing and automatic hierarchy setup.
    /// Includes a smooth rolling counter and subtle icon bounce animation when coins are added.
    /// </summary>
    [ExecuteAlways]
    public class MenuCoinsUI : MonoBehaviour
    {
        public static MenuCoinsUI Instance { get; private set; }

        [Header("UI Element References")]
        [SerializeField] private RectTransform widgetContainer;
        [SerializeField] private Image coinIconImage;
        [SerializeField] private TMP_Text coinsCountTMP;
        [SerializeField] private Text coinsCountLegacy;
        [SerializeField] private Sprite coinSprite;

        [Header("Display Settings")]
        [Tooltip("Format string for the coin counter (e.g. '{0}', '{0:N0}')")]
        [SerializeField] private string countFormat = "{0}";

        [Tooltip("Color of the coins count text")]
        [SerializeField] private Color textColor = Color.white;

        [Header("Animations")]
        [Tooltip("If true, numbers roll up smoothly when coins change")]
        [SerializeField] private bool animateCounter = true;

        [Tooltip("Duration of the roll-up number animation in seconds")]
        [SerializeField] private float countDuration = 0.65f;

        [Tooltip("If true, the coin icon punches/bounces on coin changes")]
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
            EnsureCoinManager();
            EnsureUIHierarchy();
            BindEvents();
            UpdateUI(false);
        }

        private void Start()
        {
            EnsureCoinManager();
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
            if (CoinManager.HasInstance)
            {
                CoinManager.Instance.OnCoinsChanged -= HandleCoinsChanged;
            }
        }

        private void EnsureCoinManager()
        {
            if (!Application.isPlaying) return;

            if (CoinManager.HasInstance) return;

            CoinManager existing = FindAnyObjectByType<CoinManager>();
            if (existing == null)
            {
                GameObject obj = new GameObject("[CoinManager]");
                obj.AddComponent<CoinManager>();
            }
        }

        private void BindEvents()
        {
            if (CoinManager.HasInstance)
            {
                CoinManager.Instance.OnCoinsChanged -= HandleCoinsChanged;
                CoinManager.Instance.OnCoinsChanged += HandleCoinsChanged;
            }
        }

        private void HandleCoinsChanged(int newBalance)
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

                if (enableIconBounce && coinIconImage != null)
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
            if (CoinManager.HasInstance)
            {
                balance = CoinManager.Instance.Coins;
            }
            else
            {
                balance = PlayerPrefs.GetInt("LEGO_CoinsBalance", 0);
            }

            if (animate && Application.isPlaying && animateCounter)
            {
                HandleCoinsChanged(balance);
            }
            else
            {
                currentDisplayedCount = balance;
                SetText(balance);
            }

            if (coinIconImage != null)
            {
                if (coinIconImage.sprite == null)
                {
                    coinIconImage.sprite = GetCoinSprite();
                }
            }
        }

        private void SetText(int value)
        {
            string formatted = string.Format(countFormat, value);
            if (coinsCountTMP != null)
            {
                coinsCountTMP.text = formatted;
                coinsCountTMP.color = textColor;
            }
            if (coinsCountLegacy != null)
            {
                coinsCountLegacy.text = formatted;
                coinsCountLegacy.color = textColor;
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
                // Ease-out curve for satisfying deceleration
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
            if (coinIconImage == null) yield break;

            Transform iconTr = coinIconImage.transform;
            float elapsed = 0f;
            float duration = 0.35f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float p = elapsed / duration;
                // Bounce scale up then ease back to 1.0
                float scale = 1f + Mathf.Sin(p * Mathf.PI) * 0.28f;
                iconTr.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }

            iconTr.localScale = Vector3.one;
            bounceCoroutine = null;
        }

        /// <summary>
        /// Links existing hierarchy elements if present on scene, or builds horizontal row [Coin] [Count] at Top Right.
        /// </summary>
        public void EnsureUIHierarchy()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null) return;

            // 1. Look for pre-existing CoinsWidget in scene
            if (widgetContainer == null)
            {
                Transform existing = canvas.transform.Find("CoinsWidget");
                if (existing != null)
                {
                    widgetContainer = existing.GetComponent<RectTransform>();
                }
            }

            if (widgetContainer != null)
            {
                // Auto-hook children
                if (coinIconImage == null)
                {
                    Transform iconTr = widgetContainer.Find("CoinIcon");
                    if (iconTr != null) coinIconImage = iconTr.GetComponent<Image>();
                }
                if (coinsCountTMP == null)
                {
                    Transform countTr = widgetContainer.Find("CoinsCountText");
                    if (countTr != null) coinsCountTMP = countTr.GetComponent<TMP_Text>();
                }
                if (coinsCountLegacy == null)
                {
                    Transform countTr = widgetContainer.Find("CoinsCountText");
                    if (countTr != null) coinsCountLegacy = countTr.GetComponent<Text>();
                }

                if (coinIconImage != null && coinIconImage.sprite == null)
                {
                    coinIconImage.sprite = GetCoinSprite();
                }
                EnsureButtonListener();
                return;
            }

            // 2. If completely missing in scene, create horizontal row: [CoinIcon] [CoinsCountText]
            GameObject widgetObj = new GameObject("CoinsWidget");
            widgetObj.transform.SetParent(canvas.transform, false);
            widgetContainer = widgetObj.AddComponent<RectTransform>();

            // Anchor Top-Right (positioned nicely to the left of SettingsButton)
            widgetContainer.anchorMin = new Vector2(1f, 1f);
            widgetContainer.anchorMax = new Vector2(1f, 1f);
            widgetContainer.pivot = new Vector2(1f, 1f);
            widgetContainer.anchoredPosition = new Vector2(-170f, -70f);
            widgetContainer.sizeDelta = new Vector2(200f, 70f);

            // Clean horizontal layout
            HorizontalLayoutGroup hlg = widgetObj.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 10f;
            hlg.childAlignment = TextAnchor.MiddleRight;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            // Coin Icon (64x64)
            GameObject iconObj = new GameObject("CoinIcon");
            iconObj.transform.SetParent(widgetContainer, false);
            RectTransform iconRt = iconObj.AddComponent<RectTransform>();
            iconRt.sizeDelta = new Vector2(64f, 64f);

            coinIconImage = iconObj.AddComponent<Image>();
            coinIconImage.sprite = GetCoinSprite();
            coinIconImage.preserveAspect = true;

            // Coin Count Text (Bold 52pt)
            GameObject countObj = new GameObject("CoinsCountText");
            countObj.transform.SetParent(widgetContainer, false);
            RectTransform countRt = countObj.AddComponent<RectTransform>();
            countRt.sizeDelta = new Vector2(120f, 70f);

            coinsCountTMP = countObj.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset font = GetDefaultTMPFont();
            if (font != null) coinsCountTMP.font = font;
            coinsCountTMP.fontSize = 52f;
            coinsCountTMP.fontStyle = FontStyles.Bold;
            coinsCountTMP.alignment = TextAlignmentOptions.MidlineLeft;
            coinsCountTMP.color = textColor;
            coinsCountTMP.text = "0";

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

        private Sprite GetCoinSprite()
        {
            if (coinSprite != null) return coinSprite;

#if UNITY_EDITOR
            // Try loading from 2D Game UI Kit atlas
            var allAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/300Mind/2D Game UI Kit/Sprites/UI-pack_Sprite_1.png");
            if (allAssets != null)
            {
                foreach (var a in allAssets)
                {
                    if (a is Sprite s && (s.name == "UI-pack_Sprite_1_16" || s.name.IndexOf("coin", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        coinSprite = s;
                        return s;
                    }
                }
            }
#endif

            // Search in loaded resources/scenes
            var sprites = Resources.FindObjectsOfTypeAll<Sprite>();
            foreach (var s in sprites)
            {
                if (s.name == "UI-pack_Sprite_1_16" || s.name.IndexOf("coin", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    coinSprite = s;
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
        [ContextMenu("Build Coins Widget Now")]
        public void BuildCoinsWidgetEditor()
        {
            EnsureCoinManager();
            EnsureUIHierarchy();
            UpdateUI(false);
            UnityEditor.EditorUtility.SetDirty(gameObject);
            if (widgetContainer != null)
            {
                UnityEditor.EditorUtility.SetDirty(widgetContainer.gameObject);
            }
        }

        [UnityEditor.MenuItem("LEGO/Setup Menu Coins UI")]
        public static void SetupMenuCoinsUIEditor()
        {
            Canvas canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("No Canvas found in current scene.");
                return;
            }

            MenuCoinsUI coinsUI = canvas.GetComponent<MenuCoinsUI>();
            if (coinsUI == null)
            {
                coinsUI = canvas.gameObject.AddComponent<MenuCoinsUI>();
            }

            coinsUI.EnsureCoinManager();
            coinsUI.EnsureUIHierarchy();
            coinsUI.UpdateUI(false);
            UnityEditor.EditorUtility.SetDirty(canvas.gameObject);
            Debug.Log("<color=green>MenuCoinsUI: Coins widget successfully configured on Canvas!</color>");
        }
#endif
    }
}
