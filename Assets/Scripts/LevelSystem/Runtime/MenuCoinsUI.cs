using System;
using System.Collections;
using System.Collections.Generic;
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
            if (Application.isPlaying)
            {
                if (Instance != null && Instance != this)
                {
                    Destroy(this);
                    return;
                }
            }
            Instance = this;
        }

        private void OnEnable()
        {
            if (Application.isPlaying && Instance != null && Instance != this) return;
            Instance = this;
            EnsureCoinManager();
            EnsureUIHierarchy();
            BindEvents();
            UpdateUI(false);
        }

        private void Start()
        {
            if (Application.isPlaying && Instance != null && Instance != this) return;
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
            else if (SaveDataService.HasInstance && SaveDataService.Instance.Data != null)
            {
                balance = SaveDataService.Instance.Data.coins;
            }
            else if (Application.isPlaying && SaveDataService.Instance != null && SaveDataService.Instance.Data != null)
            {
                balance = SaveDataService.Instance.Data.coins;
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

        private Canvas FindMainCanvas()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null && !canvas.name.Contains("Dev")) return canvas;

            Canvas[] allCanvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var c in allCanvases)
            {
                if (c == null) continue;
                if (c.name.Contains("Dev")) continue;
                if (c.transform.Find("DownPanel") != null ||
                    c.transform.Find("GemsWidget") != null ||
                    c.transform.Find("CoinsWidget") != null ||
                    c.transform.Find("SettingsButton") != null)
                {
                    return c;
                }
            }

            foreach (var c in allCanvases)
            {
                if (c != null && !c.name.Contains("Dev") && c.name == "Canvas")
                    return c;
            }

            foreach (var c in allCanvases)
            {
                if (c != null && !c.name.Contains("Dev"))
                    return c;
            }

            return null;
        }

        private void CleanupAndResolveCoinsWidget(Canvas canvas)
        {
            // 1. If widgetContainer is already assigned, ensure it is not overlapping GemsWidget
            if (widgetContainer != null && widgetContainer.gameObject != null)
            {
                if (widgetContainer.anchoredPosition.x > -250f)
                {
                    widgetContainer.anchoredPosition = new Vector2(-456.76f, -70f);
                }
            }

            // 2. Find all RectTransforms named "CoinsWidget" in the scene
            var allWidgets = FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var coinWidgets = new List<RectTransform>();
            foreach (var rt in allWidgets)
            {
                if (rt != null && rt.name == "CoinsWidget")
                {
                    coinWidgets.Add(rt);
                }
            }

            if (coinWidgets.Count > 0)
            {
                // Prefer widgetContainer if already assigned and valid
                RectTransform best = widgetContainer;
                if (best == null || !coinWidgets.Contains(best))
                {
                    // Look for one directly under canvas positioned properly (x < -300)
                    foreach (var cw in coinWidgets)
                    {
                        if (cw.transform.parent == canvas.transform && cw.anchoredPosition.x < -300f)
                        {
                            best = cw;
                            break;
                        }
                    }
                    if (best == null)
                    {
                        foreach (var cw in coinWidgets)
                        {
                            if (cw.transform.parent == canvas.transform)
                            {
                                best = cw;
                                break;
                            }
                        }
                    }
                    if (best == null) best = coinWidgets[0];
                }

                widgetContainer = best;
                if (widgetContainer.anchoredPosition.x > -250f)
                {
                    widgetContainer.anchoredPosition = new Vector2(-456.76f, -70f);
                }

                // Destroy any extra / duplicate CoinsWidget!
                for (int i = 0; i < coinWidgets.Count; i++)
                {
                    var cw = coinWidgets[i];
                    if (cw != null && cw != best)
                    {
                        if (Application.isPlaying)
                        {
                            Destroy(cw.gameObject);
                        }
                        else
                        {
                            DestroyImmediate(cw.gameObject);
                        }
                    }
                }
            }

            // 3. Specifically check if any rogue CoinsWidget or CoinIcon was attached to DevPanel
            var devPanels = FindObjectsByType<LevelDevPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var dev in devPanels)
            {
                if (dev != null)
                {
                    Transform rogue = dev.transform.Find("CoinsWidget");
                    if (rogue != null)
                    {
                        if (Application.isPlaying) Destroy(rogue.gameObject);
                        else DestroyImmediate(rogue.gameObject);
                    }
                }
            }

            // 4. Also check if GemsWidget accidentally has a CoinsWidget or CoinIcon child
            Transform gemsWidget = canvas.transform.Find("GemsWidget");
            if (gemsWidget != null)
            {
                Transform rogueChild = gemsWidget.Find("CoinsWidget");
                if (rogueChild != null)
                {
                    if (Application.isPlaying) Destroy(rogueChild.gameObject);
                    else DestroyImmediate(rogueChild.gameObject);
                }
                Transform rogueIcon = gemsWidget.Find("CoinIcon");
                if (rogueIcon != null)
                {
                    if (Application.isPlaying) Destroy(rogueIcon.gameObject);
                    else DestroyImmediate(rogueIcon.gameObject);
                }
                Transform rogueText = gemsWidget.Find("CoinsCountText");
                if (rogueText != null)
                {
                    if (Application.isPlaying) Destroy(rogueText.gameObject);
                    else DestroyImmediate(rogueText.gameObject);
                }
            }
        }

        private void CreateCoinsWidget(Canvas canvas)
        {
            GameObject widgetObj = new GameObject("CoinsWidget");
            widgetObj.transform.SetParent(canvas.transform, false);
            widgetContainer = widgetObj.AddComponent<RectTransform>();

            // Anchor Top-Right (positioned nicely to the left of GemsWidget: -456.76f, -70f)
            widgetContainer.anchorMin = new Vector2(1f, 1f);
            widgetContainer.anchorMax = new Vector2(1f, 1f);
            widgetContainer.pivot = new Vector2(1f, 1f);
            widgetContainer.anchoredPosition = new Vector2(-456.76f, -70f);
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

        /// <summary>
        /// Links existing hierarchy elements if present on scene, or builds horizontal row [Coin] [Count] at Top Right.
        /// </summary>
        public void EnsureUIHierarchy()
        {
            Canvas canvas = FindMainCanvas();
            if (canvas == null) return;

            // Do not setup CoinsWidget on DevPanel or secondary canvases
            if (canvas.name.Contains("Dev") || gameObject.name.Contains("Dev")) return;

            // Clean up any duplicate or rogue CoinsWidgets
            CleanupAndResolveCoinsWidget(canvas);

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

            CreateCoinsWidget(canvas);
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

#endif
    }
}
