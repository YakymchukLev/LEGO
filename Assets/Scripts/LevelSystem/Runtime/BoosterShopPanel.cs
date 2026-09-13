using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace LegoPuzzle.Runtime
{
    public enum BoosterType
    {
        Freeze,
        Hammer,
        Hint
    }

    /// <summary>
    /// Modal dialog panel in Game scene offering the player to buy more booster charges
    /// with coins (configurable in Inspector) or by watching a rewarded ad.
    /// </summary>
    public class BoosterShopPanel : MonoBehaviour
    {
        public static BoosterShopPanel Instance { get; private set; }

        [Header("Prices Configuration (Coins)")]
        [Tooltip("Cost in coins to buy charges for Time Freeze booster")]
        [SerializeField] private int freezeCoinPrice = 80;

        [Tooltip("Cost in coins to buy charges for Hammer booster")]
        [SerializeField] private int hammerCoinPrice = 100;

        [Tooltip("Cost in coins to buy charges for Hint booster")]
        [SerializeField] private int hintCoinPrice = 60;

        [Header("Charges Configuration")]
        [Tooltip("Number of charges awarded when purchased with coins")]
        [SerializeField] private int chargesPerCoinPurchase = 3;

        [Tooltip("Number of charges awarded when watching an ad")]
        [SerializeField] private int chargesPerAdWatch = 1;

        [Header("Booster Custom Sprites (Optional, auto-fetches from buttons if null)")]
        [SerializeField] private Sprite freezeIconSprite;
        [SerializeField] private Sprite hammerIconSprite;
        [SerializeField] private Sprite hintIconSprite;
        [SerializeField] private Sprite coinSprite;

        [Header("UI Element References")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform dialogCard;
        [SerializeField] private TMP_Text titleTMP;
        [SerializeField] private TMP_Text descriptionTMP;
        [SerializeField] private Image boosterIconImage;
        [SerializeField] private TMP_Text chargesBadgeTMP;

        [Header("Action Buttons")]
        [SerializeField] private Button buyWithCoinsButton;
        [SerializeField] private TMP_Text coinsPriceTMP;
        [SerializeField] private Image buttonCoinIcon;

        [SerializeField] private Button watchAdButton;
        [SerializeField] private TMP_Text adButtonTMP;

        [SerializeField] private Button closeButton;
        [SerializeField] private Button backdropButton;

        [Header("Feedback Toast")]
        [SerializeField] private GameObject toastRoot;
        [SerializeField] private TMP_Text toastTMP;

        private BoosterType currentBoosterType = BoosterType.Hammer;
        private Coroutine animatePanelCoroutine;
        private Coroutine shakeButtonCoroutine;
        private Coroutine toastCoroutine;
        private Coroutine delayedCloseCoroutine;
        private bool wasTimePaused = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            panelRoot = gameObject;
            EnsureUIHierarchy();
            BindButtonListeners();

            // Modal shop starts hidden
            gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            HideToast();

            if (delayedCloseCoroutine != null)
            {
                StopCoroutine(delayedCloseCoroutine);
                delayedCloseCoroutine = null;
            }

            // ALWAYS guarantee timeScale is restored if panel is disabled
            if (Time.timeScale < 0.01f)
            {
                Time.timeScale = 1f;
            }
        }

        private void OnDestroy()
        {
            if (Time.timeScale < 0.01f)
            {
                Time.timeScale = 1f;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Build Shop Hierarchy Now")]
        public void BuildShopHierarchyEditor()
        {
            EnsureUIHierarchy();
            UnityEditor.EditorUtility.SetDirty(gameObject);
        }
#endif

        private void BindButtonListeners()
        {
            if (buyWithCoinsButton != null)
            {
                buyWithCoinsButton.onClick.RemoveListener(OnBuyWithCoinsClicked);
                buyWithCoinsButton.onClick.AddListener(OnBuyWithCoinsClicked);
            }

            if (watchAdButton != null)
            {
                watchAdButton.onClick.RemoveListener(OnWatchAdClicked);
                watchAdButton.onClick.AddListener(OnWatchAdClicked);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(ClosePanel);
                closeButton.onClick.AddListener(ClosePanel);
            }

            if (backdropButton != null)
            {
                backdropButton.onClick.RemoveListener(ClosePanel);
                backdropButton.onClick.AddListener(ClosePanel);
            }
        }

        /// <summary>
        /// Global shortcut to open the booster shop for a specific booster.
        /// </summary>
        public static void Open(BoosterType type)
        {
            if (Instance == null)
            {
                Instance = FindAnyObjectByType<BoosterShopPanel>(FindObjectsInactive.Include);
                if (Instance == null)
                {
                    Canvas canvas = FindAnyObjectByType<Canvas>();
                    if (canvas != null)
                    {
                        GameObject shopObj = new GameObject("BoosterShopPanel");
                        shopObj.transform.SetParent(canvas.transform, false);
                        Instance = shopObj.AddComponent<BoosterShopPanel>();
                    }
                }
            }

            if (Instance != null)
            {
                Instance.gameObject.SetActive(true);
                Instance.OpenForBooster(type);
            }
            else
            {
                Debug.LogWarning("BoosterShopPanel: Booster shop instance not found on scene!");
            }
        }

        /// <summary>
        /// Opens the panel for the specified booster type and populates details.
        /// </summary>
        public void OpenForBooster(BoosterType type)
        {
            EnsureUIHierarchy();
            currentBoosterType = type;

            HideToast();

            if (delayedCloseCoroutine != null)
            {
                StopCoroutine(delayedCloseCoroutine);
                delayedCloseCoroutine = null;
            }

            int price = GetPriceFor(type);
            string boosterName = "";
            string boosterDesc = "";
            Sprite icon = GetIconFor(type);

            switch (type)
            {
                case BoosterType.Freeze:
                    boosterName = "FREEZING TIME";
                    boosterDesc = "Pauses the level timer for 10 seconds, giving you time to calmly plan your moves!";
                    break;
                case BoosterType.Hammer:
                    boosterName = "HAMMER";
                    boosterDesc = "Smash any piece on the game board with a juicy hit and cracks!";
                    break;
                case BoosterType.Hint:
                    boosterName = "HINT";
                    boosterDesc = "Analyze the situation and visually show the best move to the exit or to unlock details!";
                    break;
            }

            if (titleTMP != null) titleTMP.text = boosterName;
            if (descriptionTMP != null)
            {
                descriptionTMP.enableWordWrapping = true;
                descriptionTMP.overflowMode = TextOverflowModes.Ellipsis;
                descriptionTMP.enableAutoSizing = true;
                descriptionTMP.fontSizeMin = 16f;
                descriptionTMP.fontSizeMax = 24f;
                descriptionTMP.alignment = TextAlignmentOptions.Center;
                descriptionTMP.text = boosterDesc;
                if (descriptionTMP.rectTransform != null)
                {
                    descriptionTMP.rectTransform.sizeDelta = new Vector2(540f, 90f);
                }
            }
            if (chargesBadgeTMP != null) chargesBadgeTMP.text = $"+{chargesPerCoinPurchase} {GetChargesWord(chargesPerCoinPurchase)}";

            if (boosterIconImage != null)
            {
                boosterIconImage.sprite = icon;
                boosterIconImage.enabled = icon != null;
            }

            if (coinsPriceTMP != null)
            {
                coinsPriceTMP.text = $"BUY ({price})";
            }

            if (adButtonTMP != null)
            {
                adButtonTMP.text = $"WATCH AD (+{chargesPerAdWatch})";
            }

            if (buttonCoinIcon != null && buttonCoinIcon.sprite == null)
            {
                buttonCoinIcon.sprite = GetCoinSprite();
            }

            // Pause gameplay timer while modal dialog is shown
            wasTimePaused = Time.timeScale < 0.01f;
            if (!wasTimePaused)
            {
                Time.timeScale = 0f;
            }

            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
            }

            if (animatePanelCoroutine != null) StopCoroutine(animatePanelCoroutine);
            animatePanelCoroutine = StartCoroutine(AnimateOpenRoutine());
        }

        public void ClosePanel()
        {
            if (!wasTimePaused)
            {
                Time.timeScale = 1f;
            }

            HideToast();

            if (delayedCloseCoroutine != null)
            {
                StopCoroutine(delayedCloseCoroutine);
                delayedCloseCoroutine = null;
            }

            if (animatePanelCoroutine != null) StopCoroutine(animatePanelCoroutine);
            animatePanelCoroutine = StartCoroutine(AnimateCloseRoutine());
        }

        private IEnumerator AnimateOpenRoutine()
        {
            if (dialogCard == null || canvasGroup == null) yield break;

            canvasGroup.alpha = 0f;
            dialogCard.localScale = new Vector3(0.6f, 0.6f, 1f);

            float elapsed = 0f;
            float duration = 0.24f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(elapsed / duration);

                // Overshoot spring curve
                float scaleT = Mathf.Sin(p * Mathf.PI * 0.5f);
                float overshoot = 1f + Mathf.Sin(p * Mathf.PI) * 0.12f;
                float scale = Mathf.Lerp(0.6f, 1f, scaleT) * overshoot;

                canvasGroup.alpha = Mathf.Lerp(0f, 1f, p);
                dialogCard.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }

            canvasGroup.alpha = 1f;
            dialogCard.localScale = Vector3.one;
            animatePanelCoroutine = null;
        }

        private IEnumerator AnimateCloseRoutine()
        {
            if (!wasTimePaused)
            {
                Time.timeScale = 1f;
            }

            if (dialogCard != null && canvasGroup != null)
            {
                float elapsed = 0f;
                float duration = 0.18f;

                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float p = Mathf.Clamp01(elapsed / duration);

                    canvasGroup.alpha = Mathf.Lerp(1f, 0f, p);
                    float s = Mathf.Lerp(1f, 0.75f, p);
                    dialogCard.localScale = new Vector3(s, s, 1f);
                    yield return null;
                }
            }

            gameObject.SetActive(false);
            animatePanelCoroutine = null;
        }

        private void OnBuyWithCoinsClicked()
        {
            int price = GetPriceFor(currentBoosterType);

            if (CoinManager.Instance != null && CoinManager.Instance.SpendCoins(price))
            {
                // Successful purchase
                AwardChargesToCurrentBooster(chargesPerCoinPurchase);
                ShowToast($"SUCCESSFULLY PURCHASED!\n+{chargesPerCoinPurchase} {GetChargesWord(chargesPerCoinPurchase)}", true);

                if (delayedCloseCoroutine != null) StopCoroutine(delayedCloseCoroutine);
                delayedCloseCoroutine = StartCoroutine(DelayedCloseRoutine(0.85f));
            }
            else
            {
                // Insufficient coins
                if (shakeButtonCoroutine != null) StopCoroutine(shakeButtonCoroutine);
                shakeButtonCoroutine = StartCoroutine(ShakeButtonRoutine(buyWithCoinsButton.transform));

                int currentCoins = CoinManager.Instance != null ? CoinManager.Instance.Coins : 0;
                ShowToast($"INSUFFICIENT COINS!\n(Need: {price}, You Have: {currentCoins})", false);
            }
        }

        private void OnWatchAdClicked()
        {
            // Placeholder for future AdMob/Unity Ads rewarded video callback
            Debug.Log($"<color=cyan>BoosterShopPanel: Watching ad for booster {currentBoosterType}...</color>");

            // Simulating successful ad watch
            AwardChargesToCurrentBooster(chargesPerAdWatch);
            ShowToast($"AWARD RECEIVED!\n+{chargesPerAdWatch} {GetChargesWord(chargesPerAdWatch)} FOR WATCHING AD", true);

            if (delayedCloseCoroutine != null) StopCoroutine(delayedCloseCoroutine);
            delayedCloseCoroutine = StartCoroutine(DelayedCloseRoutine(0.85f));
        }

        private string GetChargesWord(int count)
        {
            int abs = Mathf.Abs(count);
            int mod100 = abs % 100;
            int mod10 = abs % 10;
            if (mod100 >= 11 && mod100 <= 19) return "CHARGES";
            if (mod10 == 1) return "CHARGE";
            if (mod10 >= 2 && mod10 <= 4) return "CHARGES";
            return "CHARGES";
        }

        private IEnumerator DelayedCloseRoutine(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            delayedCloseCoroutine = null;
            ClosePanel();
        }

        private void AwardChargesToCurrentBooster(int count)
        {
            switch (currentBoosterType)
            {
                case BoosterType.Freeze:
                    TimeFreezeBooster freeze = FindAnyObjectByType<TimeFreezeBooster>();
                    if (freeze != null) freeze.AddCharges(count);
                    break;
                case BoosterType.Hammer:
                    HammerBooster hammer = FindAnyObjectByType<HammerBooster>();
                    if (hammer != null) hammer.AddCharges(count);
                    break;
                case BoosterType.Hint:
                    HintBooster hint = FindAnyObjectByType<HintBooster>();
                    if (hint != null) hint.AddCharges(count);
                    break;
            }
        }

        private int GetPriceFor(BoosterType type)
        {
            switch (type)
            {
                case BoosterType.Freeze: return Mathf.Max(0, freezeCoinPrice);
                case BoosterType.Hammer: return Mathf.Max(0, hammerCoinPrice);
                case BoosterType.Hint: return Mathf.Max(0, hintCoinPrice);
                default: return 100;
            }
        }

        private Sprite GetIconFor(BoosterType type)
        {
            Sprite result = null;
            switch (type)
            {
                case BoosterType.Freeze:
                    if (freezeIconSprite != null) return freezeIconSprite;
                    var freeze = FindAnyObjectByType<TimeFreezeBooster>();
                    if (freeze != null)
                    {
                        result = ExtractIconFromBooster(freeze.transform);
                        if (result != null) return result;
                    }
#if UNITY_EDITOR
                    result = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/image-removebg-preview(1).png");
                    if (result != null) return result;
#endif
                    break;

                case BoosterType.Hammer:
                    if (hammerIconSprite != null) return hammerIconSprite;
                    var hammer = FindAnyObjectByType<HammerBooster>();
                    if (hammer != null)
                    {
                        result = ExtractIconFromBooster(hammer.transform);
                        if (result != null) return result;
                    }
#if UNITY_EDITOR
                    result = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/image-removebg-preview.png");
                    if (result != null) return result;
#endif
                    break;

                case BoosterType.Hint:
                    if (hintIconSprite != null) return hintIconSprite;
                    var hint = FindAnyObjectByType<HintBooster>();
                    if (hint != null)
                    {
                        result = ExtractIconFromBooster(hint.transform);
                        if (result != null) return result;
                    }
#if UNITY_EDITOR
                    result = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/image-removebg-preview(2).png");
                    if (result != null) return result;
#endif
                    break;
            }
            return null;
        }

        private Sprite ExtractIconFromBooster(Transform boosterTransform)
        {
            if (boosterTransform == null) return null;

            // 1. Look for child "Image"
            var childImg = boosterTransform.Find("Image");
            if (childImg != null && childImg.GetComponent<Image>() != null && childImg.GetComponent<Image>().sprite != null)
            {
                return childImg.GetComponent<Image>().sprite;
            }

            // 2. Look for any child Image component
            var images = boosterTransform.GetComponentsInChildren<Image>(true);
            foreach (var img in images)
            {
                if (img.gameObject != boosterTransform.gameObject && img.sprite != null)
                {
                    return img.sprite;
                }
            }

            // 3. Fallback to self
            var selfImg = boosterTransform.GetComponent<Image>();
            return selfImg != null ? selfImg.sprite : null;
        }

        private IEnumerator ShakeButtonRoutine(Transform target)
        {
            if (target == null) yield break;

            Vector3 origLocalPos = target.localPosition;
            float duration = 0.28f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float offset = Mathf.Sin(elapsed * 55f) * 12f * (1f - elapsed / duration);
                target.localPosition = origLocalPos + new Vector3(offset, 0f, 0f);
                yield return null;
            }

            target.localPosition = origLocalPos;
            shakeButtonCoroutine = null;
        }

        private void ShowToast(string message, bool isSuccess = false)
        {
            if (toastRoot == null || toastTMP == null) return;

            var img = toastRoot.GetComponent<Image>();
            if (img != null)
            {
                img.color = isSuccess
                    ? new Color(0.12f, 0.68f, 0.32f, 0.96f)
                    : new Color(0.92f, 0.22f, 0.24f, 0.96f);
            }

            toastTMP.text = message;
            toastRoot.SetActive(true);

            if (toastCoroutine != null) StopCoroutine(toastCoroutine);
            toastCoroutine = StartCoroutine(ToastRoutine());
        }

        private IEnumerator ToastRoutine()
        {
            yield return new WaitForSecondsRealtime(2f);
            HideToast();
        }

        private void HideToast()
        {
            if (toastCoroutine != null)
            {
                StopCoroutine(toastCoroutine);
                toastCoroutine = null;
            }
            if (toastRoot != null)
            {
                toastRoot.SetActive(false);
            }
        }

        /// <summary>
        /// Automatically constructs or connects the UI dialog if missing in the Canvas.
        /// </summary>
        public void EnsureUIHierarchy()
        {
            panelRoot = gameObject;

            RectTransform rootRt = GetComponent<RectTransform>();
            if (rootRt == null) rootRt = gameObject.AddComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;
            rootRt.pivot = new Vector2(0.5f, 0.5f);

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            // 1. Check if DialogCard already exists under this transform
            Transform cardTrans = transform.Find("DialogCard");
            if (cardTrans != null)
            {
                dialogCard = cardTrans as RectTransform;
                AutoHookChildren();

                // If buttons are missing (e.g. from earlier interrupted run), destroy broken card and rebuild cleanly!
                if (buyWithCoinsButton == null || watchAdButton == null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(dialogCard.gameObject);
                    }
                    else
                    {
                        DestroyImmediate(dialogCard.gameObject);
                    }
                    dialogCard = null;
                }
                else
                {
                    Transform bd = transform.Find("Backdrop");
                    if (bd != null) backdropButton = bd.GetComponent<Button>();
                    BindButtonListeners();
                    return;
                }
            }

            // Dimmed backdrop
            Transform bdTrans = transform.Find("Backdrop");
            if (bdTrans != null)
            {
                backdropButton = bdTrans.GetComponent<Button>();
            }
            else
            {
                GameObject backdrop = new GameObject("Backdrop");
                backdrop.transform.SetParent(transform, false);
                RectTransform bdRt = backdrop.AddComponent<RectTransform>();
                bdRt.anchorMin = Vector2.zero;
                bdRt.anchorMax = Vector2.one;
                bdRt.offsetMin = Vector2.zero;
                bdRt.offsetMax = Vector2.zero;

                Image bdImg = backdrop.AddComponent<Image>();
                bdImg.color = new Color(0.04f, 0.05f, 0.08f, 0.85f);
                backdropButton = backdrop.AddComponent<Button>();
            }

            // Central Dialog Card (620 x 780)
            GameObject card = new GameObject("DialogCard");
            card.transform.SetParent(transform, false);
            dialogCard = card.AddComponent<RectTransform>();
            dialogCard.anchorMin = new Vector2(0.5f, 0.5f);
            dialogCard.anchorMax = new Vector2(0.5f, 0.5f);
            dialogCard.pivot = new Vector2(0.5f, 0.5f);
            dialogCard.anchoredPosition = Vector2.zero;
            dialogCard.sizeDelta = new Vector2(620f, 780f);

            Image cardBg = card.AddComponent<Image>();
            cardBg.sprite = GetRoundedBgSprite();
            cardBg.type = Image.Type.Sliced;
            cardBg.color = new Color(0.12f, 0.15f, 0.22f, 0.98f); // Deep modern navy

            Outline cardOutline = card.AddComponent<Outline>();
            cardOutline.effectColor = new Color(0.35f, 0.45f, 0.70f, 0.60f);
            cardOutline.effectDistance = new Vector2(3f, -3f);

            TMP_FontAsset defaultFont = GetDefaultTMPFont();

            // Close Button (X) at Top-Right
            GameObject closeObj = new GameObject("CloseButton");
            closeObj.transform.SetParent(dialogCard, false);
            RectTransform closeRt = closeObj.AddComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(1f, 1f);
            closeRt.anchorMax = new Vector2(1f, 1f);
            closeRt.pivot = new Vector2(1f, 1f);
            closeRt.anchoredPosition = new Vector2(-20f, -20f);
            closeRt.sizeDelta = new Vector2(60f, 60f);

            Image closeImg = closeObj.AddComponent<Image>();
            closeImg.color = new Color(0.85f, 0.25f, 0.25f, 0.9f);
            closeImg.sprite = GetRoundedBgSprite();
            closeImg.type = Image.Type.Sliced;
            closeButton = closeObj.AddComponent<Button>();
            UIButtonPressEffect.AttachTo(closeObj);

            GameObject closeTextObj = new GameObject("XText");
            closeTextObj.transform.SetParent(closeObj.transform, false);
            RectTransform ctRt = closeTextObj.AddComponent<RectTransform>();
            ctRt.anchorMin = Vector2.zero;
            ctRt.anchorMax = Vector2.one;
            ctRt.offsetMin = Vector2.zero;
            ctRt.offsetMax = Vector2.zero;
            TMP_Text ctTMP = closeTextObj.AddComponent<TextMeshProUGUI>();
            if (defaultFont != null) ctTMP.font = defaultFont;
            ctTMP.fontSize = 38f;
            ctTMP.fontStyle = FontStyles.Bold;
            ctTMP.alignment = TextAlignmentOptions.Center;
            ctTMP.color = Color.white;
            ctTMP.text = "X";

            // Header Title
            GameObject titleObj = new GameObject("TitleText");
            titleObj.transform.SetParent(dialogCard, false);
            RectTransform titleRt = titleObj.AddComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0.5f, 1f);
            titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -45f);
            titleRt.sizeDelta = new Vector2(500f, 60f);
            titleTMP = titleObj.AddComponent<TextMeshProUGUI>();
            if (defaultFont != null) titleTMP.font = defaultFont;
            titleTMP.fontSize = 42f;
            titleTMP.fontStyle = FontStyles.Bold;
            titleTMP.alignment = TextAlignmentOptions.Center;
            titleTMP.color = new Color(1f, 0.88f, 0.35f, 1f); // Vibrant gold
            titleTMP.text = "BOOSTER SHOP";

            // Booster Icon Preview Circle
            GameObject iconBg = new GameObject("IconContainer");
            iconBg.transform.SetParent(dialogCard, false);
            RectTransform iconBgRt = iconBg.AddComponent<RectTransform>();
            iconBgRt.anchorMin = new Vector2(0.5f, 1f);
            iconBgRt.anchorMax = new Vector2(0.5f, 1f);
            iconBgRt.pivot = new Vector2(0.5f, 1f);
            iconBgRt.anchoredPosition = new Vector2(0f, -125f);
            iconBgRt.sizeDelta = new Vector2(170f, 170f);

            Image iconBgImg = iconBg.AddComponent<Image>();
            iconBgImg.sprite = GetRoundedBgSprite();
            iconBgImg.type = Image.Type.Sliced;
            iconBgImg.color = new Color(0.18f, 0.22f, 0.33f, 1f);

            GameObject iconObj = new GameObject("BoosterIcon");
            iconObj.transform.SetParent(iconBg.transform, false);
            RectTransform iconRt = iconObj.AddComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.5f, 0.5f);
            iconRt.anchorMax = new Vector2(0.5f, 0.5f);
            iconRt.pivot = new Vector2(0.5f, 0.5f);
            iconRt.anchoredPosition = Vector2.zero;
            iconRt.sizeDelta = new Vector2(120f, 120f);
            boosterIconImage = iconObj.AddComponent<Image>();
            boosterIconImage.preserveAspect = true;

            // Description text
            GameObject descObj = new GameObject("DescText");
            descObj.transform.SetParent(dialogCard, false);
            RectTransform descRt = descObj.AddComponent<RectTransform>();
            descRt.anchorMin = new Vector2(0.5f, 1f);
            descRt.anchorMax = new Vector2(0.5f, 1f);
            descRt.pivot = new Vector2(0.5f, 1f);
            descRt.anchoredPosition = new Vector2(0f, -315f);
            descRt.sizeDelta = new Vector2(540f, 90f);
            descriptionTMP = descObj.AddComponent<TextMeshProUGUI>();
            if (defaultFont != null) descriptionTMP.font = defaultFont;
            descriptionTMP.fontSize = 24f;
            descriptionTMP.enableAutoSizing = true;
            descriptionTMP.fontSizeMin = 16f;
            descriptionTMP.fontSizeMax = 24f;
            descriptionTMP.enableWordWrapping = true;
            descriptionTMP.overflowMode = TextOverflowModes.Ellipsis;
            descriptionTMP.alignment = TextAlignmentOptions.Center;
            descriptionTMP.color = new Color(0.85f, 0.90f, 0.95f, 1f);
            descriptionTMP.text = "Booster description and effect on board";

            // Charges badge (pill container + child text)
            GameObject badgeObj = new GameObject("ChargesBadge");
            badgeObj.transform.SetParent(dialogCard, false);
            RectTransform badgeRt = badgeObj.AddComponent<RectTransform>();
            badgeRt.anchorMin = new Vector2(0.5f, 1f);
            badgeRt.anchorMax = new Vector2(0.5f, 1f);
            badgeRt.pivot = new Vector2(0.5f, 1f);
            badgeRt.anchoredPosition = new Vector2(0f, -415f);
            badgeRt.sizeDelta = new Vector2(260f, 44f);
            Image badgeImg = badgeObj.AddComponent<Image>();
            badgeImg.sprite = GetRoundedBgSprite();
            badgeImg.type = Image.Type.Sliced;
            badgeImg.color = new Color(0.20f, 0.42f, 0.76f, 0.90f);

            GameObject badgeTxtObj = new GameObject("BadgeText");
            badgeTxtObj.transform.SetParent(badgeObj.transform, false);
            RectTransform btRt = badgeTxtObj.AddComponent<RectTransform>();
            btRt.anchorMin = Vector2.zero;
            btRt.anchorMax = Vector2.one;
            btRt.offsetMin = Vector2.zero;
            btRt.offsetMax = Vector2.zero;
            chargesBadgeTMP = badgeTxtObj.AddComponent<TextMeshProUGUI>();
            if (defaultFont != null) chargesBadgeTMP.font = defaultFont;
            chargesBadgeTMP.fontSize = 24f;
            chargesBadgeTMP.fontStyle = FontStyles.Bold;
            chargesBadgeTMP.alignment = TextAlignmentOptions.Center;
            chargesBadgeTMP.color = Color.white;
            chargesBadgeTMP.text = "+3 CHARGES";

            // Button 1: Buy with Coins (Green Action Button)
            GameObject coinBtnObj = new GameObject("BuyWithCoinsButton");
            coinBtnObj.transform.SetParent(dialogCard, false);
            RectTransform cBtnRt = coinBtnObj.AddComponent<RectTransform>();
            cBtnRt.anchorMin = new Vector2(0.5f, 0f);
            cBtnRt.anchorMax = new Vector2(0.5f, 0f);
            cBtnRt.pivot = new Vector2(0.5f, 0f);
            cBtnRt.anchoredPosition = new Vector2(0f, 155f);
            cBtnRt.sizeDelta = new Vector2(480f, 95f);

            Image cBtnImg = coinBtnObj.AddComponent<Image>();
            cBtnImg.sprite = GetRoundedBgSprite();
            cBtnImg.type = Image.Type.Sliced;
            cBtnImg.color = new Color(0.14f, 0.72f, 0.35f, 1f); // Vibrant Emerald Green
            buyWithCoinsButton = coinBtnObj.AddComponent<Button>();
            UIButtonPressEffect.AttachTo(coinBtnObj);

            // Button Coin Icon (54x54)
            GameObject bCoinIconObj = new GameObject("CoinIcon");
            bCoinIconObj.transform.SetParent(coinBtnObj.transform, false);
            RectTransform bciRt = bCoinIconObj.AddComponent<RectTransform>();
            bciRt.anchorMin = new Vector2(0f, 0.5f);
            bciRt.anchorMax = new Vector2(0f, 0.5f);
            bciRt.pivot = new Vector2(0f, 0.5f);
            bciRt.anchoredPosition = new Vector2(35f, 0f);
            bciRt.sizeDelta = new Vector2(54f, 54f);
            buttonCoinIcon = bCoinIconObj.AddComponent<Image>();
            buttonCoinIcon.sprite = GetCoinSprite();
            buttonCoinIcon.preserveAspect = true;

            // Button Price Text
            GameObject bCoinTextObj = new GameObject("PriceText");
            bCoinTextObj.transform.SetParent(coinBtnObj.transform, false);
            RectTransform bctRt = bCoinTextObj.AddComponent<RectTransform>();
            bctRt.anchorMin = Vector2.zero;
            bctRt.anchorMax = Vector2.one;
            bctRt.offsetMin = new Vector2(100f, 0f);
            bctRt.offsetMax = new Vector2(-20f, 0f);
            coinsPriceTMP = bCoinTextObj.AddComponent<TextMeshProUGUI>();
            if (defaultFont != null) coinsPriceTMP.font = defaultFont;
            coinsPriceTMP.fontSize = 36f;
            coinsPriceTMP.fontStyle = FontStyles.Bold;
            coinsPriceTMP.alignment = TextAlignmentOptions.MidlineLeft;
            coinsPriceTMP.color = Color.white;
            coinsPriceTMP.text = "BUY (100)";

            // Button 2: Watch Ad (Purple Action Button)
            GameObject adBtnObj = new GameObject("WatchAdButton");
            adBtnObj.transform.SetParent(dialogCard, false);
            RectTransform adBtnRt = adBtnObj.AddComponent<RectTransform>();
            adBtnRt.anchorMin = new Vector2(0.5f, 0f);
            adBtnRt.anchorMax = new Vector2(0.5f, 0f);
            adBtnRt.pivot = new Vector2(0.5f, 0f);
            adBtnRt.anchoredPosition = new Vector2(0f, 45f);
            adBtnRt.sizeDelta = new Vector2(480f, 85f);

            Image adBtnImg = adBtnObj.AddComponent<Image>();
            adBtnImg.sprite = GetRoundedBgSprite();
            adBtnImg.type = Image.Type.Sliced;
            adBtnImg.color = new Color(0.38f, 0.32f, 0.88f, 1f); // Royal Indigo
            watchAdButton = adBtnObj.AddComponent<Button>();
            UIButtonPressEffect.AttachTo(adBtnObj);

            GameObject adTextObj = new GameObject("AdText");
            adTextObj.transform.SetParent(adBtnObj.transform, false);
            RectTransform adtRt = adTextObj.AddComponent<RectTransform>();
            adtRt.anchorMin = Vector2.zero;
            adtRt.anchorMax = Vector2.one;
            adtRt.offsetMin = Vector2.zero;
            adtRt.offsetMax = Vector2.zero;
            adButtonTMP = adTextObj.AddComponent<TextMeshProUGUI>();
            if (defaultFont != null) adButtonTMP.font = defaultFont;
            adButtonTMP.fontSize = 28f;
            adButtonTMP.fontStyle = FontStyles.Bold;
            adButtonTMP.alignment = TextAlignmentOptions.Center;
            adButtonTMP.color = Color.white;
            adButtonTMP.text = "WATCH AD (+1)";

            // Toast feedback message (bottom overlay)
            GameObject toastObj = new GameObject("ToastMessage");
            toastObj.transform.SetParent(dialogCard, false);
            toastRoot = toastObj;
            RectTransform toastRt = toastObj.AddComponent<RectTransform>();
            toastRt.anchorMin = new Vector2(0.5f, 0.5f);
            toastRt.anchorMax = new Vector2(0.5f, 0.5f);
            toastRt.pivot = new Vector2(0.5f, 0.5f);
            toastRt.anchoredPosition = new Vector2(0f, -20f);
            toastRt.sizeDelta = new Vector2(480f, 110f);

            Image toastBg = toastObj.AddComponent<Image>();
            toastBg.sprite = GetRoundedBgSprite();
            toastBg.type = Image.Type.Sliced;
            toastBg.color = new Color(0.92f, 0.22f, 0.24f, 0.96f);

            GameObject toastTxtObj = new GameObject("Text");
            toastTxtObj.transform.SetParent(toastObj.transform, false);
            RectTransform ttRt = toastTxtObj.AddComponent<RectTransform>();
            ttRt.anchorMin = Vector2.zero;
            ttRt.anchorMax = Vector2.one;
            ttRt.offsetMin = Vector2.zero;
            ttRt.offsetMax = Vector2.zero;
            toastTMP = toastTxtObj.AddComponent<TextMeshProUGUI>();
            if (defaultFont != null) toastTMP.font = defaultFont;
            toastTMP.fontSize = 26f;
            toastTMP.fontStyle = FontStyles.Bold;
            toastTMP.alignment = TextAlignmentOptions.Center;
            toastTMP.color = Color.white;
            toastTMP.text = "INSUFFICIENT COINS!";
            toastRoot.SetActive(false);

            BindButtonListeners();
        }

        private void AutoHookChildren()
        {
            if (dialogCard == null) return;

            if (titleTMP == null)
            {
                var t = dialogCard.Find("TitleText");
                if (t != null) titleTMP = t.GetComponent<TMP_Text>();
            }
            if (descriptionTMP == null)
            {
                var d = dialogCard.Find("DescText");
                if (d != null) descriptionTMP = d.GetComponent<TMP_Text>();
            }
            if (boosterIconImage == null)
            {
                var ic = dialogCard.Find("IconContainer/BoosterIcon");
                if (ic != null) boosterIconImage = ic.GetComponent<Image>();
            }
            if (chargesBadgeTMP == null)
            {
                var b = dialogCard.Find("ChargesBadge");
                if (b != null) chargesBadgeTMP = b.GetComponentInChildren<TMP_Text>();
            }
            if (buyWithCoinsButton == null)
            {
                var cb = dialogCard.Find("BuyWithCoinsButton");
                if (cb != null)
                {
                    buyWithCoinsButton = cb.GetComponent<Button>();
                    var pt = cb.Find("PriceText");
                    if (pt != null) coinsPriceTMP = pt.GetComponent<TMP_Text>();
                    var ci = cb.Find("CoinIcon");
                    if (ci != null) buttonCoinIcon = ci.GetComponent<Image>();
                }
            }
            if (watchAdButton == null)
            {
                var ab = dialogCard.Find("WatchAdButton");
                if (ab != null)
                {
                    watchAdButton = ab.GetComponent<Button>();
                    var at = ab.Find("AdText");
                    if (at != null) adButtonTMP = at.GetComponent<TMP_Text>();
                }
            }
            if (closeButton == null)
            {
                var cl = dialogCard.Find("CloseButton");
                if (cl != null) closeButton = cl.GetComponent<Button>();
            }
            if (toastRoot == null)
            {
                var tr = dialogCard.Find("ToastMessage");
                if (tr != null)
                {
                    toastRoot = tr.gameObject;
                    toastTMP = tr.GetComponentInChildren<TMP_Text>();
                }
            }
        }

        private Sprite GetRoundedBgSprite()
        {
            var sprites = Resources.FindObjectsOfTypeAll<Sprite>();
            foreach (var s in sprites)
            {
                if (s.name == "UISprite" || s.name == "Background" || s.name == "Knob")
                    return s;
            }
            return null;
        }

        private Sprite GetCoinSprite()
        {
            if (coinSprite != null) return coinSprite;

#if UNITY_EDITOR
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
    }
}
