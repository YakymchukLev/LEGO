using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;

namespace LegoPuzzle.Runtime
{
    /// <summary>
    /// Full-featured Lotto / Slot Machine Panel with an authentic arcade cabinet design,
    /// 3 spinning reels with overshoot bounce, payline win evaluation, and customizable sprite item pool.
    /// Can be opened from both Menu and Game scenes.
    /// </summary>
    [ExecuteAlways]
    public class SlotMachinePanel : MonoBehaviour
    {
        public static SlotMachinePanel Instance { get; private set; }

        [Header("Швидке завантаження спрайтів (Drop Sprites Here)")]
        [Tooltip("Закиньте сюди всі ваші спрайти (Sprite[] / List<Sprite>). Вони автоматично розподіляться у всі 3 колонки слотів!")]
        [SerializeField] private List<Sprite> customSprites = new List<Sprite>();

        [Header("Детальне налаштування предметів (Advanced Slot Items)")]
        [Tooltip("Список предметів/символів, що з'являються на барабанах")]
        [SerializeField] private List<SlotItemData> slotItems = new List<SlotItemData>();

        [Header("Spin Economics & Rules")]
        [Tooltip("If true, spins are always free (e.g. for bonus events or testing)")]
        [SerializeField] private bool freeSpinsOnly = false;

        [Tooltip("Cost in coins per spin when not free")]
        [SerializeField] private int spinCoinCost = 50;

        [Tooltip("Number of free spins granted to the player each session / day")]
        [SerializeField] private int dailyFreeSpins = 3;

        [Tooltip("Multiplier applied to reward amount when hitting a 3-of-a-kind Jackpot")]
        [SerializeField] private int jackpotMultiplier = 3;

        [Header("Reels Configuration")]
        [SerializeField] private SlotReelView reel1;
        [SerializeField] private SlotReelView reel2;
        [SerializeField] private SlotReelView reel3;

        [Tooltip("Base spin duration of the first reel in seconds")]
        [SerializeField] private float reel1Duration = 1.15f;

        [Tooltip("Additional delay before reel 2 stops")]
        [SerializeField] private float reel2ExtraDelay = 0.55f;

        [Tooltip("Additional delay before reel 3 stops (dramatic suspense)")]
        [SerializeField] private float reel3ExtraDelay = 0.60f;

        [Header("Фонове відео (Background Video)")]
        [Tooltip("Відеокліп для фону грального автомата (LottoVideo.mp4)")]
        [SerializeField] private VideoClip backgroundVideoClip;

        [Tooltip("Вимикати звук у фоновому відео")]
        [SerializeField] private bool muteVideoAudio = true;

        [Tooltip("Гучність відео якщо звук увімкнено")]
        [SerializeField] private float videoAudioVolume = 0f;

        [Header("Текстури слотів (Slot Machine Textures)")]
        [Tooltip("Спрайт грального автомата (Arcade Cabinet)")]
        [SerializeField] private Sprite cabinetSprite;

        [Tooltip("Спрайт кнопки спіну (Spin Button)")]
        [SerializeField] private Sprite spinButtonSprite;

        [Header("UI Element References")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RawImage videoRawImage;
        [SerializeField] private AspectRatioFitter videoAspectRatioFitter;
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private RectTransform cabinetCard;
        [SerializeField] private Button backdropButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button spinButton;
        [SerializeField] private TMP_Text spinButtonTMP;
        [SerializeField] private RectTransform spinCostBadge;
        [SerializeField] private TMP_Text spinCostBadgeTMP;
        [SerializeField] private TMP_Text titleTMP;
        [SerializeField] private TMP_Text statusTMP;
        [SerializeField] private TMP_Text coinsBalanceTMP;
        [SerializeField] private RectTransform paylineIndicator;
        [SerializeField] private UIConfettiEffect confettiEffect;

        [Header("Audio Clips (Optional)")]
        [SerializeField] private AudioClip spinStartSound;
        [SerializeField] private AudioClip reelTickSound;
        [SerializeField] private AudioClip reelStopSound;
        [SerializeField] private AudioClip winSound;
        [SerializeField] private AudioClip jackpotSound;

        private AudioSource audioSource;
        private Coroutine animatePanelCoroutine;
        private Coroutine spinSequenceCoroutine;
        private Coroutine statusPulseCoroutine;
        private Coroutine videoFadeCoroutine;
        private RenderTexture videoRenderTexture;

        private int remainingFreeSpins = 3;
        private bool isSpinning = false;
        private bool wasTimePaused = false;

        public bool IsOpen => gameObject.activeSelf && (canvasGroup == null || canvasGroup.alpha > 0.05f);
        public bool IsSpinning => isSpinning;

        public static void OpenSlotMachine()
        {
            if (Instance != null)
            {
                Instance.Open();
                return;
            }

            var panel = FindAnyObjectByType<SlotMachinePanel>(FindObjectsInactive.Include);
            if (panel != null)
            {
                panel.Open();
                return;
            }

            Canvas canvas = FindAnyObjectByType<Canvas>();
            if (canvas != null)
            {
                GameObject obj = new GameObject("SlotMachinePanel", typeof(RectTransform), typeof(SlotMachinePanel));
                obj.transform.SetParent(canvas.transform, false);
                var created = obj.GetComponent<SlotMachinePanel>();
                created.Open();
            }
        }

        public static void CloseSlotMachine()
        {
            if (Instance != null)
            {
                Instance.Close();
            }
        }

        private void Awake()
        {
            if (Application.isPlaying)
            {
                if (Instance != null && Instance != this)
                {
                    Destroy(gameObject);
                    return;
                }
                Instance = this;
                LoadFreeSpins();
            }

            EnsureAudioSource();
            EnsureUIHierarchy();
            PopulateDefaultItemsIfEmpty();
            HookButtons();
        }

        private void Start()
        {
            if (Application.isPlaying)
            {
                HookSlotsButtonInScene();
                UpdateBalanceDisplay();
                UpdateSpinButtonText();
                InitReelsWithRandomSymbols();
            }
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                UpdateBalanceDisplay();
                UpdateSpinButtonText();
                if (CoinManager.HasInstance)
                {
                    CoinManager.Instance.OnCoinsChanged += HandleCoinsChanged;
                }
            }
        }

        private void OnDisable()
        {
            StopVideoPlayback();

            if (Application.isPlaying && CoinManager.HasInstance)
            {
                CoinManager.Instance.OnCoinsChanged -= HandleCoinsChanged;
            }

            if (wasTimePaused && Time.timeScale < 0.01f)
            {
                Time.timeScale = 1f;
            }
        }

        private void OnDestroy()
        {
            ReleaseRenderTexture();
        }

        private void HandleCoinsChanged(int newBalance)
        {
            UpdateBalanceDisplay();
            UpdateSpinButtonText();
        }

        private void EnsureAudioSource()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }
        }

        private void PlayAudio(AudioClip clip, float volume = 1f)
        {
            if (clip == null || audioSource == null) return;
            if (GameSettingsManager.HasInstance && !GameSettingsManager.Instance.SoundEnabled) return;
            audioSource.PlayOneShot(clip, volume);
        }

        // ==========================================
        // Modal Open / Close Animation
        // ==========================================

        public void Open()
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            EnsureUIHierarchy();
            SyncSpritesToSlotItems();
            UpdateBalanceDisplay();
            UpdateSpinButtonText();
            InitReelsWithRandomSymbols();
            SetStatusText("КРУТИ ТА ВИГРАВАЙ!", Color.white);

            wasTimePaused = Time.timeScale < 0.01f;
            if (!wasTimePaused)
            {
                Time.timeScale = 0f; // Pause gameplay while in lotto modal
            }

            if (canvasGroup != null)
            {
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            StartVideoPlayback();

            if (animatePanelCoroutine != null) StopCoroutine(animatePanelCoroutine);
            animatePanelCoroutine = StartCoroutine(AnimateOpenRoutine());

            if (GameSettingsManager.HasInstance)
            {
                GameSettingsManager.Instance.PlayClickSound();
            }
        }

        public void Close()
        {
            if (isSpinning) return; // Prevent closing mid-spin

            if (animatePanelCoroutine != null) StopCoroutine(animatePanelCoroutine);
            animatePanelCoroutine = StartCoroutine(AnimateCloseRoutine());

            if (GameSettingsManager.HasInstance)
            {
                GameSettingsManager.Instance.PlayClickSound();
            }
        }

        private IEnumerator AnimateOpenRoutine()
        {
            if (canvasGroup == null || cabinetCard == null) yield break;

            canvasGroup.alpha = 0f;
            cabinetCard.localScale = new Vector3(0.7f, 0.7f, 1f);

            float duration = 0.32f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(elapsed / duration);

                // Overshoot bounce curve
                float t = Mathf.Sin(p * Mathf.PI * 0.5f);
                float overshoot = 1f + Mathf.Sin(p * Mathf.PI) * 0.12f;
                float scale = Mathf.Lerp(0.7f, 1f, t) * overshoot;

                canvasGroup.alpha = Mathf.Lerp(0f, 1f, p);
                cabinetCard.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }

            canvasGroup.alpha = 1f;
            cabinetCard.localScale = Vector3.one;
            animatePanelCoroutine = null;
        }

        private IEnumerator AnimateCloseRoutine()
        {
            if (!wasTimePaused)
            {
                Time.timeScale = 1f;
            }

            if (canvasGroup != null && cabinetCard != null)
            {
                float duration = 0.20f;
                float elapsed = 0f;

                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float p = Mathf.Clamp01(elapsed / duration);

                    canvasGroup.alpha = Mathf.Lerp(1f, 0f, p);
                    float scale = Mathf.Lerp(1f, 0.8f, p);
                    cabinetCard.localScale = new Vector3(scale, scale, 1f);
                    yield return null;
                }
            }

            StopVideoPlayback();
            gameObject.SetActive(false);
            animatePanelCoroutine = null;
        }

        // ==========================================
        // Spin Logic & Reel Orchestration
        // ==========================================

        public void OnSpinButtonClicked()
        {
            if (isSpinning) return;
            if (slotItems == null || slotItems.Count == 0)
            {
                SetStatusText("Немає налаштованих предметів!", Color.red);
                return;
            }

            // Check payment
            bool isFree = freeSpinsOnly || remainingFreeSpins > 0;
            if (!isFree)
            {
                int currentCoins = CoinManager.HasInstance ? CoinManager.Instance.Coins : 0;
                if (currentCoins < spinCoinCost)
                {
                    SetStatusText("Недостатньо монет для спіну!", new Color(1f, 0.35f, 0.35f));
                    if (GameSettingsManager.HasInstance) GameSettingsManager.Instance.TriggerHaptic();
                    ShakeButton(spinButton.GetComponent<RectTransform>());
                    return;
                }

                // Deduct coins
                if (CoinManager.HasInstance)
                {
                    CoinManager.Instance.SpendCoins(spinCoinCost);
                }
            }
            else if (!freeSpinsOnly)
            {
                remainingFreeSpins--;
                SaveFreeSpins();
            }

            UpdateSpinButtonText();
            UpdateBalanceDisplay();

            if (spinSequenceCoroutine != null) StopCoroutine(spinSequenceCoroutine);
            spinSequenceCoroutine = StartCoroutine(SpinSequenceRoutine());
        }

        private IEnumerator SpinSequenceRoutine()
        {
            isSpinning = true;
            spinButton.interactable = false;
            SetStatusText("КРУТЯТЬСЯ СЛОТИ...", new Color(1f, 0.85f, 0.2f));

            PlayAudio(spinStartSound != null ? spinStartSound : GameSettingsManager.Instance?.ButtonClickSound, 0.85f);
            if (GameSettingsManager.HasInstance) GameSettingsManager.Instance.TriggerHapticMove();

            // Decide outcomes via weighted random selection
            SlotItemData outcome1 = SelectRandomItem();
            SlotItemData outcome2 = SelectRandomItem();
            SlotItemData outcome3 = SelectRandomItem();

            // Hook tick sounds for reels
            Action tickAction = () => PlayAudio(reelTickSound, 0.4f);
            if (reel1 != null) reel1.OnPaylineTick += tickAction;
            if (reel2 != null) reel2.OnPaylineTick += tickAction;
            if (reel3 != null) reel3.OnPaylineTick += tickAction;

            bool r1Done = false, r2Done = false, r3Done = false;

            // Start spinning all 3 reels simultaneously
            if (reel1 != null) reel1.StartSpin(slotItems, outcome1, reel1Duration, () => { r1Done = true; OnReelLanded(); });
            if (reel2 != null) reel2.StartSpin(slotItems, outcome2, reel1Duration + reel2ExtraDelay, () => { r2Done = true; OnReelLanded(); });
            if (reel3 != null) reel3.StartSpin(slotItems, outcome3, reel1Duration + reel2ExtraDelay + reel3ExtraDelay, () => { r3Done = true; OnReelLanded(); });

            while (!r1Done || !r2Done || !r3Done)
            {
                yield return null;
            }

            // Unhook tick sounds
            if (reel1 != null) reel1.OnPaylineTick -= tickAction;
            if (reel2 != null) reel2.OnPaylineTick -= tickAction;
            if (reel3 != null) reel3.OnPaylineTick -= tickAction;

            // Evaluate winning combination
            EvaluateOutcome(outcome1, outcome2, outcome3);

            isSpinning = false;
            spinButton.interactable = true;
            UpdateSpinButtonText();
            spinSequenceCoroutine = null;
        }

        private void OnReelLanded()
        {
            PlayAudio(reelStopSound, 0.85f);
            if (GameSettingsManager.HasInstance) GameSettingsManager.Instance.TriggerHapticMove();
        }

        private void EvaluateOutcome(SlotItemData item1, SlotItemData item2, SlotItemData item3)
        {
            if (item1 == null || item2 == null || item3 == null) return;

            // 1. Three of a kind - JACKPOT!
            if (item1.id == item2.id && item2.id == item3.id)
            {
                int totalPrize = item1.rewardAmount * jackpotMultiplier;
                GrantReward(item1.rewardType, totalPrize);

                SetStatusText($"ДЖЕКПОТ! 3x {item1.displayName.ToUpper()}! (+{totalPrize})", new Color(1f, 0.9f, 0.1f));
                PlayAudio(jackpotSound != null ? jackpotSound : winSound, 1f);

                if (confettiEffect != null) confettiEffect.Play();
                if (GameSettingsManager.HasInstance) GameSettingsManager.Instance.TriggerHapticExit();

                StartCoroutine(PulsePaylineHighlight(item1.themeColor, 3));
            }
            // 2. Two of a kind - MINOR WIN!
            else if (item1.id == item2.id || item2.id == item3.id || item1.id == item3.id)
            {
                SlotItemData winningItem = (item1.id == item2.id) ? item1 : ((item2.id == item3.id) ? item2 : item1);
                int totalPrize = winningItem.rewardAmount;
                GrantReward(winningItem.rewardType, totalPrize);

                SetStatusText($"ВИГРАШ! 2x {winningItem.displayName}! (+{totalPrize})", new Color(0.3f, 1f, 0.5f));
                PlayAudio(winSound, 0.9f);

                if (confettiEffect != null) confettiEffect.Play();
                if (GameSettingsManager.HasInstance) GameSettingsManager.Instance.TriggerHapticExit();

                StartCoroutine(PulsePaylineHighlight(winningItem.themeColor, 2));
            }
            // 3. No match
            else
            {
                SetStatusText("МАЙЖЕ! СПРОБУЙТЕ ЩЕ РАЗ!", new Color(0.85f, 0.85f, 0.9f));
                if (GameSettingsManager.HasInstance) GameSettingsManager.Instance.TriggerHapticMove();
            }

            UpdateBalanceDisplay();
        }

        private void GrantReward(SlotRewardType type, int amount)
        {
            switch (type)
            {
                case SlotRewardType.Coins:
                    if (CoinManager.HasInstance) CoinManager.Instance.AddCoins(amount);
                    break;
                case SlotRewardType.Gems:
                    if (GemManager.HasInstance) GemManager.Instance.AddGems(amount);
                    break;
                case SlotRewardType.Hearts:
                    if (HeartManager.Instance != null) HeartManager.Instance.AddHearts(amount);
                    break;
                case SlotRewardType.HammerBooster:
                    int curHammer = PlayerPrefs.GetInt("LEGO_BoosterCharges_Hammer", 3);
                    PlayerPrefs.SetInt("LEGO_BoosterCharges_Hammer", curHammer + amount);
                    PlayerPrefs.Save();
                    break;
                case SlotRewardType.FreezeBooster:
                    int curFreeze = PlayerPrefs.GetInt("LEGO_BoosterCharges_Freeze", 3);
                    PlayerPrefs.SetInt("LEGO_BoosterCharges_Freeze", curFreeze + amount);
                    PlayerPrefs.Save();
                    break;
                case SlotRewardType.HintBooster:
                    int curHint = PlayerPrefs.GetInt("LEGO_BoosterCharges_Hint", 3);
                    PlayerPrefs.SetInt("LEGO_BoosterCharges_Hint", curHint + amount);
                    PlayerPrefs.Save();
                    break;
                case SlotRewardType.JackpotSpecial:
                    if (CoinManager.HasInstance) CoinManager.Instance.AddCoins(amount * 2);
                    if (GemManager.HasInstance) GemManager.Instance.AddGems(amount);
                    break;
            }
        }

        private SlotItemData SelectRandomItem()
        {
            if (slotItems == null || slotItems.Count == 0) return null;

            int totalWeight = 0;
            foreach (var it in slotItems) totalWeight += Mathf.Max(1, it.weight);

            int rnd = UnityEngine.Random.Range(0, totalWeight);
            int current = 0;
            foreach (var it in slotItems)
            {
                current += Mathf.Max(1, it.weight);
                if (rnd < current) return it;
            }

            return slotItems[UnityEngine.Random.Range(0, slotItems.Count)];
        }

        private void InitReelsWithRandomSymbols()
        {
            if (slotItems == null || slotItems.Count == 0) return;
            if (reel1 != null) reel1.Initialize(slotItems);
            if (reel2 != null) reel2.Initialize(slotItems);
            if (reel3 != null) reel3.Initialize(slotItems);
        }

        private void UpdateBalanceDisplay()
        {
            if (coinsBalanceTMP != null)
            {
                int c = CoinManager.HasInstance ? CoinManager.Instance.Coins : 0;
                coinsBalanceTMP.text = c.ToString();
            }
        }

        private void UpdateSpinButtonText()
        {
            string costStr = "";
            if (freeSpinsOnly)
            {
                costStr = "БЕЗКОШТОВНО";
            }
            else if (remainingFreeSpins > 0)
            {
                costStr = $"БЕЗКОШТОВНО ({remainingFreeSpins})";
            }
            else
            {
                costStr = $"{spinCoinCost} <sprite name=\"coin\">";
            }

            if (spinCostBadgeTMP != null)
            {
                spinCostBadgeTMP.text = costStr;
            }

            if (spinButtonTMP != null)
            {
                // If spinButton uses a custom graphic with "SPIN" already drawn, leave button text blank or set cost
                spinButtonTMP.text = spinButtonSprite != null ? "" : costStr;
            }
        }

        private void SetStatusText(string message, Color color)
        {
            if (statusTMP == null) return;
            statusTMP.text = message;
            statusTMP.color = color;
        }

        private IEnumerator PulsePaylineHighlight(Color glowColor, int pulseCount)
        {
            if (paylineIndicator == null) yield break;
            Image lineImg = paylineIndicator.GetComponent<Image>();
            if (lineImg == null) yield break;

            Color origColor = lineImg.color;
            for (int i = 0; i < pulseCount; i++)
            {
                float el = 0f;
                float dur = 0.22f;
                while (el < dur)
                {
                    el += Time.unscaledDeltaTime;
                    lineImg.color = Color.Lerp(origColor, glowColor, Mathf.Sin((el / dur) * Mathf.PI));
                    yield return null;
                }
            }
            lineImg.color = origColor;
        }

        private void ShakeButton(RectTransform target)
        {
            if (target == null) return;
            StartCoroutine(ShakeRoutine(target));
        }

        private IEnumerator ShakeRoutine(RectTransform rt)
        {
            Vector2 orig = rt.anchoredPosition;
            float dur = 0.32f;
            float el = 0f;
            while (el < dur)
            {
                el += Time.unscaledDeltaTime;
                float x = Mathf.Sin(el * 45f) * (1f - (el / dur)) * 14f;
                rt.anchoredPosition = new Vector2(orig.x + x, orig.y);
                yield return null;
            }
            rt.anchoredPosition = orig;
        }

        private void LoadFreeSpins()
        {
            remainingFreeSpins = PlayerPrefs.GetInt("LEGO_LottoDailyFreeSpins", dailyFreeSpins);
        }

        private void SaveFreeSpins()
        {
            PlayerPrefs.SetInt("LEGO_LottoDailyFreeSpins", remainingFreeSpins);
            PlayerPrefs.Save();
        }

        private void HookButtons()
        {
            if (spinButton != null)
            {
                spinButton.onClick.RemoveListener(OnSpinButtonClicked);
                spinButton.onClick.AddListener(OnSpinButtonClicked);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Close);
                closeButton.onClick.AddListener(Close);
            }

            if (backdropButton != null)
            {
                backdropButton.onClick.RemoveListener(Close);
                backdropButton.onClick.AddListener(Close);
            }

            HookSlotsButtonInScene();
        }

        public void HookSlotsButtonInScene()
        {
            var allButtons = Resources.FindObjectsOfTypeAll<Button>();
            for (int i = 0; i < allButtons.Length; i++)
            {
                Button btn = allButtons[i];
                if (btn == null || btn.gameObject.scene != gameObject.scene) continue;

                if (btn.name.Equals("SlotsButton", StringComparison.OrdinalIgnoreCase) ||
                    btn.GetComponent<SlotsButton>() != null)
                {
                    btn.onClick.RemoveListener(Open);
                    btn.onClick.AddListener(Open);
                    if (btn.GetComponent<UIButtonPressEffect>() == null)
                    {
                        UIButtonPressEffect.AttachTo(btn.gameObject);
                    }
                }
            }
        }

        #region Background Video Handling

        private void EnsureVideoClip()
        {
            if (backgroundVideoClip == null)
            {
#if UNITY_EDITOR
                backgroundVideoClip = UnityEditor.AssetDatabase.LoadAssetAtPath<VideoClip>("Assets/Sprites/LottoVideo.mp4");
                if (backgroundVideoClip == null)
                {
                    string[] guids = UnityEditor.AssetDatabase.FindAssets("LottoVideo t:VideoClip");
                    if (guids.Length > 0)
                    {
                        backgroundVideoClip = UnityEditor.AssetDatabase.LoadAssetAtPath<VideoClip>(UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
                    }
                }
#endif
            }
        }

        private void EnsureRenderTexture()
        {
            EnsureVideoClip();

            int width = 1920;
            int height = 1080;

            if (backgroundVideoClip != null && backgroundVideoClip.width > 0 && backgroundVideoClip.height > 0)
            {
                width = (int)backgroundVideoClip.width;
                height = (int)backgroundVideoClip.height;
            }

            if (videoRenderTexture == null || !videoRenderTexture.IsCreated() || videoRenderTexture.width != width || videoRenderTexture.height != height)
            {
                ReleaseRenderTexture();

                videoRenderTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
                {
                    name = "LottoPanelVideo_RT",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                videoRenderTexture.Create();
            }

            if (videoPlayer != null)
            {
                videoPlayer.targetTexture = videoRenderTexture;
            }

            if (videoRawImage != null)
            {
                videoRawImage.texture = videoRenderTexture;
            }
        }

        private void ReleaseRenderTexture()
        {
            if (videoRenderTexture != null)
            {
                if (videoRenderTexture.IsCreated())
                {
                    videoRenderTexture.Release();
                }
                if (Application.isPlaying)
                {
                    Destroy(videoRenderTexture);
                }
                else
                {
                    DestroyImmediate(videoRenderTexture);
                }
                videoRenderTexture = null;
            }
        }

        private void StartVideoPlayback()
        {
            EnsureVideoClip();
            EnsureRenderTexture();

            if (videoPlayer == null)
            {
                videoPlayer = GetComponent<VideoPlayer>();
                if (videoPlayer == null) videoPlayer = gameObject.AddComponent<VideoPlayer>();
            }

            if (videoPlayer != null && backgroundVideoClip != null)
            {
                videoPlayer.clip = backgroundVideoClip;
                videoPlayer.renderMode = VideoRenderMode.RenderTexture;
                videoPlayer.targetTexture = videoRenderTexture;
                videoPlayer.isLooping = true;
                videoPlayer.playOnAwake = false;

                if (muteVideoAudio)
                {
                    videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
                }
                else
                {
                    videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
                    videoPlayer.SetDirectAudioVolume(0, videoAudioVolume);
                }

                if (!videoPlayer.isPlaying)
                {
                    videoPlayer.Play();
                }

                if (videoRawImage != null)
                {
                    if (videoFadeCoroutine != null) StopCoroutine(videoFadeCoroutine);
                    videoFadeCoroutine = StartCoroutine(VideoFadeInRoutine());
                }
            }
        }

        private void StopVideoPlayback()
        {
            if (videoFadeCoroutine != null)
            {
                StopCoroutine(videoFadeCoroutine);
                videoFadeCoroutine = null;
            }

            if (videoPlayer != null && videoPlayer.isPlaying)
            {
                videoPlayer.Stop();
            }
        }

        private IEnumerator VideoFadeInRoutine()
        {
            if (videoRawImage == null) yield break;

            videoRawImage.color = new Color(1f, 1f, 1f, 0f);

            float waitTimer = 0f;
            while (videoPlayer != null && !videoPlayer.isPlaying && waitTimer < 0.4f)
            {
                waitTimer += Time.unscaledDeltaTime;
                yield return null;
            }

            float elapsed = 0f;
            float duration = 0.25f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                if (videoRawImage != null)
                {
                    videoRawImage.color = new Color(1f, 1f, 1f, t);
                }
                yield return null;
            }

            if (videoRawImage != null) videoRawImage.color = Color.white;
            videoFadeCoroutine = null;
        }

        #endregion

        // ==========================================
        // Sprites Sync & Default Items Setup
        // ==========================================

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (customSprites != null && customSprites.Count > 0)
            {
                SyncSpritesToSlotItems();
                if (reel1 != null && reel2 != null && reel3 != null)
                {
                    InitReelsWithRandomSymbols();
                }
            }
        }
#endif

        [ContextMenu("Оновити слоти з масиву спрайтів (Sync Custom Sprites)")]
        public void RefreshFromCustomSprites()
        {
            SyncSpritesToSlotItems();
            InitReelsWithRandomSymbols();
        }

        public void SyncSpritesToSlotItems()
        {
            if (customSprites != null && customSprites.Count > 0)
            {
                Dictionary<string, SlotItemData> existingMap = new Dictionary<string, SlotItemData>();
                if (slotItems != null)
                {
                    foreach (var item in slotItems)
                    {
                        if (item != null && !string.IsNullOrEmpty(item.id))
                        {
                            existingMap[item.id] = item;
                        }
                    }
                }

                List<SlotItemData> newItems = new List<SlotItemData>();

                for (int i = 0; i < customSprites.Count; i++)
                {
                    Sprite spr = customSprites[i];
                    if (spr == null) continue;

                    string id = spr.name;

                    if (existingMap.TryGetValue(id, out var existingItem))
                    {
                        existingItem.icon = spr;
                        newItems.Add(existingItem);
                    }
                    else
                    {
                        SlotRewardType rewardType = SlotRewardType.Coins;
                        int rewardAmount = 50;
                        Color themeColor = new Color(1f, 0.85f, 0.2f);
                        string lowerName = spr.name.ToLower();

                        if (lowerName.Contains("gem") || lowerName.Contains("diamond"))
                        {
                            rewardType = SlotRewardType.Gems;
                            rewardAmount = 20;
                            themeColor = new Color(0.3f, 0.7f, 1f);
                        }
                        else if (lowerName.Contains("heart") || lowerName.Contains("life"))
                        {
                            rewardType = SlotRewardType.Hearts;
                            rewardAmount = 1;
                            themeColor = new Color(1f, 0.25f, 0.35f);
                        }
                        else if (lowerName.Contains("hammer"))
                        {
                            rewardType = SlotRewardType.HammerBooster;
                            rewardAmount = 1;
                            themeColor = new Color(1f, 0.55f, 0.1f);
                        }
                        else if (lowerName.Contains("freeze") || lowerName.Contains("snow") || lowerName.Contains("ice"))
                        {
                            rewardType = SlotRewardType.FreezeBooster;
                            rewardAmount = 1;
                            themeColor = new Color(0.2f, 0.9f, 1f);
                        }
                        else if (lowerName.Contains("hint") || lowerName.Contains("bulb") || lowerName.Contains("light"))
                        {
                            rewardType = SlotRewardType.HintBooster;
                            rewardAmount = 1;
                            themeColor = new Color(1f, 0.84f, 0.2f);
                        }
                        else if (lowerName.Contains("coin") || lowerName.Contains("gold"))
                        {
                            rewardType = SlotRewardType.Coins;
                            rewardAmount = 100;
                            themeColor = new Color(1f, 0.85f, 0.1f);
                        }
                        else if (lowerName.Contains("jackpot") || lowerName.Contains("star") || lowerName.Contains("crown"))
                        {
                            rewardType = SlotRewardType.Coins;
                            rewardAmount = 300;
                            themeColor = new Color(1f, 0.9f, 0.1f);
                        }

                        newItems.Add(new SlotItemData
                        {
                            id = id,
                            displayName = CleanDisplayName(spr.name),
                            icon = spr,
                            rewardType = rewardType,
                            rewardAmount = rewardAmount,
                            weight = 20,
                            themeColor = themeColor
                        });
                    }
                }

                if (newItems.Count > 0)
                {
                    slotItems = newItems;
                }
            }
            else
            {
                PopulateDefaultItemsIfEmpty();
            }

            ValidateAndFixItemIcons();
        }

        private string CleanDisplayName(string rawName)
        {
            if (string.IsNullOrEmpty(rawName)) return "Item";
            string clean = rawName.Replace("-removebg-preview", "")
                                  .Replace("image-removebg-preview", "Booster")
                                  .Replace("_", " ")
                                  .Replace("-", " ");
            return clean.Trim();
        }

        public static Sprite GetCabinetSprite()
        {
#if UNITY_EDITOR
            string atlasPath = "Assets/Sprites/ChatGPT Image Sep 18, 2026, 01_08_34 AM(1).png";
            var allAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(atlasPath);
            if (allAssets != null)
            {
                foreach (var obj in allAssets)
                {
                    if (obj is Sprite s && s != null)
                    {
                        // Arcade Cabinet is _2 with rect: 919 x 1082
                        if (s.name.EndsWith("_2") || (s.rect.width > 800f && s.rect.height > 900f))
                        {
                            return s;
                        }
                    }
                }
            }
#endif
            var allSprites = Resources.FindObjectsOfTypeAll<Sprite>();
            foreach (var s in allSprites)
            {
                if (s != null && (s.name.EndsWith("_2") || (s.rect.width > 800f && s.rect.height > 900f)))
                {
                    return s;
                }
            }
            return null;
        }

        public static Sprite GetSpinButtonSprite()
        {
#if UNITY_EDITOR
            string atlasPath = "Assets/Sprites/ChatGPT Image Sep 18, 2026, 01_08_34 AM(1).png";
            var allAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(atlasPath);
            if (allAssets != null)
            {
                foreach (var obj in allAssets)
                {
                    if (obj is Sprite s && s != null)
                    {
                        // Spin Button is _3 with rect: 350 x 286
                        if (s.name.EndsWith("_3") || (Mathf.Abs(s.rect.width - 350f) < 5f && Mathf.Abs(s.rect.height - 286f) < 5f))
                        {
                            return s;
                        }
                    }
                }
            }
#endif
            var allSprites = Resources.FindObjectsOfTypeAll<Sprite>();
            foreach (var s in allSprites)
            {
                if (s != null && (s.name.EndsWith("_3") || (Mathf.Abs(s.rect.width - 350f) < 5f && Mathf.Abs(s.rect.height - 286f) < 5f)))
                {
                    return s;
                }
            }
            return null;
        }

        public static Sprite LoadProjectSprite(string pathOrName)
        {
            if (string.IsNullOrEmpty(pathOrName)) return null;

#if UNITY_EDITOR
            if (pathOrName.Contains("/"))
            {
                var direct = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(pathOrName);
                if (direct != null) return direct;
            }

            // Check lotto atlas sub-sprites first with exact slice name matching
            var lottoAtlas = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/ChatGPT Image Sep 18, 2026, 01_08_34 AM(1).png");
            if (lottoAtlas != null)
            {
                foreach (var a in lottoAtlas)
                {
                    if (a is Sprite spr && spr != null && spr.name == pathOrName)
                    {
                        return spr;
                    }
                }
            }

            // Check UI-pack atlas
            var allAtlasAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/300Mind/2D Game UI Kit/Sprites/UI-pack_Sprite_1.png");
            if (allAtlasAssets != null)
            {
                foreach (var a in allAtlasAssets)
                {
                    if (a is Sprite spr && spr != null && spr.name == pathOrName)
                    {
                        return spr;
                    }
                }
            }

            // Search by Sprite name across representations
            string[] guids = UnityEditor.AssetDatabase.FindAssets(pathOrName + " t:Sprite");
            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
                var representations = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(assetPath);
                foreach (var rep in representations)
                {
                    if (rep is Sprite spr && spr.name == pathOrName)
                    {
                        return spr;
                    }
                }
            }
#endif
            var loaded = Resources.FindObjectsOfTypeAll<Sprite>();
            foreach (var s in loaded)
            {
                if (s != null && s.name == pathOrName)
                    return s;
            }
            return null;
        }

        public void ValidateAndFixItemIcons()
        {
            if (slotItems == null || slotItems.Count == 0)
            {
                PopulateDefaultItemsIfEmpty();
                return;
            }

            Sprite coinSprite = LoadProjectSprite("UI-pack_Sprite_1_16");
            Sprite gemSprite = LoadProjectSprite("gem_icon");
            Sprite heartSprite = LoadProjectSprite("heart_icon");
            Sprite hammerSprite = LoadProjectSprite("image-removebg-preview");
            Sprite freezeSprite = LoadProjectSprite("image-removebg-preview(1)");
            Sprite hintSprite = LoadProjectSprite("image-removebg-preview(2)");

            Sprite fallback = gemSprite != null ? gemSprite : (heartSprite != null ? heartSprite : coinSprite);

            foreach (var item in slotItems)
            {
                if (item.icon == null)
                {
                    switch (item.rewardType)
                    {
                        case SlotRewardType.Coins:
                            item.icon = coinSprite != null ? coinSprite : fallback;
                            break;
                        case SlotRewardType.Gems:
                            item.icon = gemSprite != null ? gemSprite : fallback;
                            break;
                        case SlotRewardType.Hearts:
                            item.icon = heartSprite != null ? heartSprite : fallback;
                            break;
                        case SlotRewardType.HammerBooster:
                            item.icon = hammerSprite != null ? hammerSprite : fallback;
                            break;
                        case SlotRewardType.FreezeBooster:
                            item.icon = freezeSprite != null ? freezeSprite : fallback;
                            break;
                        case SlotRewardType.HintBooster:
                            item.icon = hintSprite != null ? hintSprite : fallback;
                            break;
                        default:
                            item.icon = fallback;
                            break;
                    }
                }
            }
        }

        private void PopulateDefaultItemsIfEmpty()
        {
            Sprite coinSprite = LoadProjectSprite("UI-pack_Sprite_1_16");
            Sprite gemSprite = LoadProjectSprite("gem_icon");
            Sprite heartSprite = LoadProjectSprite("heart_icon");
            Sprite hammerSprite = LoadProjectSprite("image-removebg-preview");
            Sprite freezeSprite = LoadProjectSprite("image-removebg-preview(1)");
            Sprite hintSprite = LoadProjectSprite("image-removebg-preview(2)");

            Sprite fallback = gemSprite != null ? gemSprite : (heartSprite != null ? heartSprite : coinSprite);

            if (slotItems == null || slotItems.Count == 0)
            {
                slotItems = new List<SlotItemData>();

                slotItems.Add(new SlotItemData
                {
                    id = "Coins",
                    displayName = "100 Монет",
                    icon = coinSprite != null ? coinSprite : fallback,
                    rewardType = SlotRewardType.Coins,
                    rewardAmount = 100,
                    weight = 25,
                    themeColor = new Color(1f, 0.85f, 0.1f)
                });

                slotItems.Add(new SlotItemData
                {
                    id = "Gems",
                    displayName = "20 Алмазів",
                    icon = gemSprite != null ? gemSprite : fallback,
                    rewardType = SlotRewardType.Gems,
                    rewardAmount = 20,
                    weight = 18,
                    themeColor = new Color(0.2f, 0.75f, 1f)
                });

                slotItems.Add(new SlotItemData
                {
                    id = "Hearts",
                    displayName = "1 Життя",
                    icon = heartSprite != null ? heartSprite : fallback,
                    rewardType = SlotRewardType.Hearts,
                    rewardAmount = 1,
                    weight = 18,
                    themeColor = new Color(1f, 0.25f, 0.35f)
                });

                slotItems.Add(new SlotItemData
                {
                    id = "Hammer",
                    displayName = "1 Молоток",
                    icon = hammerSprite != null ? hammerSprite : fallback,
                    rewardType = SlotRewardType.HammerBooster,
                    rewardAmount = 1,
                    weight = 15,
                    themeColor = new Color(1f, 0.55f, 0.1f)
                });

                slotItems.Add(new SlotItemData
                {
                    id = "Freeze",
                    displayName = "1 Заморозка",
                    icon = freezeSprite != null ? freezeSprite : fallback,
                    rewardType = SlotRewardType.FreezeBooster,
                    rewardAmount = 1,
                    weight = 15,
                    themeColor = new Color(0.2f, 0.9f, 1f)
                });

                slotItems.Add(new SlotItemData
                {
                    id = "Hint",
                    displayName = "1 Підказка",
                    icon = hintSprite != null ? hintSprite : fallback,
                    rewardType = SlotRewardType.HintBooster,
                    rewardAmount = 1,
                    weight = 12,
                    themeColor = new Color(1f, 0.84f, 0.2f)
                });
            }
            else
            {
                ValidateAndFixItemIcons();
            }
        }

        // ==========================================
        // Procedural UI Hierarchy Builder
        // ==========================================

#if UNITY_EDITOR
        [ContextMenu("Build Lotto UI Now")]
        public void BuildUIEditor()
        {
            EnsureUIHierarchy();
            PopulateDefaultItemsIfEmpty();
            InitReelsWithRandomSymbols();
            UnityEditor.EditorUtility.SetDirty(gameObject);
        }
#endif

        public void EnsureUIHierarchy()
        {
            RectTransform rootRect = GetComponent<RectTransform>();
            if (rootRect == null) rootRect = gameObject.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.sizeDelta = Vector2.zero;
            rootRect.anchoredPosition = Vector2.zero;

            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

            // Ensure cabinet and spin button sprites are accurately loaded using exact rect & slice checks
            var foundCabinet = GetCabinetSprite();
            if (foundCabinet != null) cabinetSprite = foundCabinet;

            var foundSpin = GetSpinButtonSprite();
            if (foundSpin != null) spinButtonSprite = foundSpin;

            // 1. Fullscreen Video Display (LottoVideo.mp4)
            Transform videoDisplayTrans = transform.Find("FullscreenVideoDisplay");
            GameObject videoDisplayObj;

            if (videoDisplayTrans != null)
            {
                videoDisplayObj = videoDisplayTrans.gameObject;
            }
            else
            {
                videoDisplayObj = new GameObject("FullscreenVideoDisplay");
                videoDisplayObj.transform.SetParent(transform, false);
                videoDisplayObj.transform.SetAsFirstSibling();
            }

            RectTransform displayRt = videoDisplayObj.GetComponent<RectTransform>();
            if (displayRt == null) displayRt = videoDisplayObj.AddComponent<RectTransform>();
            displayRt.anchorMin = Vector2.zero;
            displayRt.anchorMax = Vector2.one;
            displayRt.offsetMin = Vector2.zero;
            displayRt.offsetMax = Vector2.zero;
            displayRt.pivot = new Vector2(0.5f, 0.5f);

            videoRawImage = videoDisplayObj.GetComponent<RawImage>();
            if (videoRawImage == null) videoRawImage = videoDisplayObj.AddComponent<RawImage>();
            videoRawImage.color = Color.white;

            videoAspectRatioFitter = videoDisplayObj.GetComponent<AspectRatioFitter>();
            if (videoAspectRatioFitter == null) videoAspectRatioFitter = videoDisplayObj.AddComponent<AspectRatioFitter>();
            videoAspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;

            EnsureVideoClip();
            if (backgroundVideoClip != null && backgroundVideoClip.width > 0 && backgroundVideoClip.height > 0)
            {
                videoAspectRatioFitter.aspectRatio = (float)backgroundVideoClip.width / (float)backgroundVideoClip.height;
            }
            else
            {
                videoAspectRatioFitter.aspectRatio = 16f / 9f;
            }

            if (videoPlayer == null)
            {
                videoPlayer = GetComponent<VideoPlayer>();
                if (videoPlayer == null) videoPlayer = gameObject.AddComponent<VideoPlayer>();
            }

            // 2. Semi-transparent backdrop overlay & click-to-close button
            Transform backdropTr = transform.Find("Backdrop");
            if (backdropTr == null)
            {
                GameObject bgObj = new GameObject("Backdrop", typeof(RectTransform), typeof(Image), typeof(Button));
                bgObj.transform.SetParent(transform, false);
                bgObj.transform.SetSiblingIndex(1);
                RectTransform rt = bgObj.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.sizeDelta = Vector2.zero;

                Image bgImg = bgObj.GetComponent<Image>();
                bgImg.color = new Color(0.04f, 0.02f, 0.08f, 0.35f); // Subtle dark tint over the video

                backdropButton = bgObj.GetComponent<Button>();
                backdropButton.transition = Selectable.Transition.None;
            }
            else
            {
                backdropButton = backdropTr.GetComponent<Button>();
                var bgImg = backdropTr.GetComponent<Image>();
                if (bgImg != null) bgImg.color = new Color(0.04f, 0.02f, 0.08f, 0.35f);
            }

            // 3. Arcade Cabinet Card (Custom Pixel-Art Sprite)
            Transform cardTr = transform.Find("ArcadeCabinetCard");
            if (cardTr == null)
            {
                GameObject cardObj = new GameObject("ArcadeCabinetCard", typeof(RectTransform), typeof(Image));
                cardObj.transform.SetParent(transform, false);
                cabinetCard = cardObj.GetComponent<RectTransform>();
            }
            else
            {
                cabinetCard = cardTr as RectTransform;
            }

            cabinetCard.anchorMin = new Vector2(0.5f, 0.5f);
            cabinetCard.anchorMax = new Vector2(0.5f, 0.5f);
            cabinetCard.pivot = new Vector2(0.5f, 0.5f);
            cabinetCard.anchoredPosition = new Vector2(0f, 90f);
            cabinetCard.sizeDelta = new Vector2(640f, 754f); // Significantly bigger, prominent arcade cabinet

            Image cardImg = cabinetCard.GetComponent<Image>();
            if (cardImg == null) cardImg = cabinetCard.gameObject.AddComponent<Image>();
            if (cabinetSprite != null)
            {
                cardImg.sprite = cabinetSprite;
                cardImg.color = Color.white;
                cardImg.preserveAspect = true;
            }
            else
            {
                cardImg.color = new Color(0.18f, 0.10f, 0.28f, 0.98f);
            }

            // Deactivate legacy HeaderMarquee if present (sprite contains built-in "SLOT" arcade header)
            Transform headerTr = cabinetCard.Find("HeaderMarquee");
            if (headerTr != null)
            {
                headerTr.gameObject.SetActive(false);
            }

            // 4. Reels Viewport Window (Precision-aligned over the cabinet's 3 blue display slots)
            Transform reelsWinTr = cabinetCard.Find("ReelsWindow");
            GameObject reelsWinObj;
            if (reelsWinTr == null)
            {
                reelsWinObj = new GameObject("ReelsWindow", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
                reelsWinObj.transform.SetParent(cabinetCard, false);
            }
            else
            {
                reelsWinObj = reelsWinTr.gameObject;
            }

            RectTransform rwRt = reelsWinObj.GetComponent<RectTransform>();
            rwRt.anchorMin = new Vector2(0.5f, 0.5f);
            rwRt.anchorMax = new Vector2(0.5f, 0.5f);
            rwRt.pivot = new Vector2(0.5f, 0.5f);
            rwRt.anchoredPosition = new Vector2(0f, 48f); // Perfectly centered over the 3 blue screen columns
            rwRt.sizeDelta = new Vector2(418f, 289f); // Fills the blue display window completely

            Image rwImg = reelsWinObj.GetComponent<Image>();
            if (rwImg == null) rwImg = reelsWinObj.AddComponent<Image>();
            rwImg.color = new Color(0.06f, 0.08f, 0.16f, 0.95f);

            var mask = reelsWinObj.GetComponent<RectMask2D>();
            if (mask == null) mask = reelsWinObj.AddComponent<RectMask2D>();

            // Create or update the 3 Reel columns with calibrated spacing and 92px items
            reel1 = EnsureReel(reelsWinObj.transform, "Reel_1", -141f, 92f);
            reel2 = EnsureReel(reelsWinObj.transform, "Reel_2", 0f, 92f);
            reel3 = EnsureReel(reelsWinObj.transform, "Reel_3", 141f, 92f);

            // 5. Payline Indicator Line
            Transform paylineTr = cabinetCard.Find("PaylineIndicator");
            GameObject lineObj = paylineTr != null ? paylineTr.gameObject : new GameObject("PaylineIndicator", typeof(RectTransform), typeof(Image));
            if (paylineTr == null) lineObj.transform.SetParent(cabinetCard, false);

            paylineIndicator = lineObj.GetComponent<RectTransform>();
            paylineIndicator.anchorMin = new Vector2(0.5f, 0.5f);
            paylineIndicator.anchorMax = new Vector2(0.5f, 0.5f);
            paylineIndicator.pivot = new Vector2(0.5f, 0.5f);
            paylineIndicator.anchoredPosition = new Vector2(0f, 48f);
            paylineIndicator.sizeDelta = new Vector2(418f, 4f);

            Image pImg = lineObj.GetComponent<Image>();
            if (pImg == null) pImg = lineObj.AddComponent<Image>();
            pImg.color = new Color(1f, 0.85f, 0.2f, 0.45f);

            // 6. Status / Win Announcement Text (in bottom purple tray area)
            Transform statusTr = cabinetCard.Find("StatusBanner");
            GameObject statusObj = statusTr != null ? statusTr.gameObject : new GameObject("StatusBanner", typeof(RectTransform), typeof(TextMeshProUGUI));
            if (statusTr == null) statusObj.transform.SetParent(cabinetCard, false);

            RectTransform sRt = statusObj.GetComponent<RectTransform>();
            sRt.anchorMin = new Vector2(0.5f, 0.5f);
            sRt.anchorMax = new Vector2(0.5f, 0.5f);
            sRt.pivot = new Vector2(0.5f, 0.5f);
            sRt.anchoredPosition = new Vector2(0f, -270f);
            sRt.sizeDelta = new Vector2(320f, 36f);

            statusTMP = statusObj.GetComponent<TextMeshProUGUI>();
            if (statusTMP == null) statusTMP = statusObj.AddComponent<TextMeshProUGUI>();
            statusTMP.text = "КРУТИ ТА ВИГРАВАЙ!";
            statusTMP.fontSize = 18f;
            statusTMP.fontStyle = FontStyles.Bold;
            statusTMP.alignment = TextAlignmentOptions.Center;
            statusTMP.color = Color.white;

            // 7. Large "SPIN" Button (Custom Pixel-Art Sprite)
            Transform spinBtnTr = cabinetCard.Find("SpinButton");
            GameObject btnObj = spinBtnTr != null ? spinBtnTr.gameObject : new GameObject("SpinButton", typeof(RectTransform), typeof(Image), typeof(Button));
            if (spinBtnTr == null) btnObj.transform.SetParent(cabinetCard, false);

            RectTransform bRt = btnObj.GetComponent<RectTransform>();
            bRt.anchorMin = new Vector2(0.5f, 0.5f);
            bRt.anchorMax = new Vector2(0.5f, 0.5f);
            bRt.pivot = new Vector2(0.5f, 0.5f);
            bRt.anchoredPosition = new Vector2(0f, -485f); // Lowered well below the machine with clear breathing room
            bRt.sizeDelta = new Vector2(210f, 171f); // Exact 350:286 aspect ratio, larger to match cabinet

            Image btnImg = btnObj.GetComponent<Image>();
            if (btnImg == null) btnImg = btnObj.AddComponent<Image>();
            if (spinButtonSprite != null)
            {
                btnImg.sprite = spinButtonSprite;
                btnImg.color = Color.white;
                btnImg.preserveAspect = true;
            }
            else
            {
                btnImg.color = new Color(0.2f, 0.82f, 0.38f, 1f);
            }

            spinButton = btnObj.GetComponent<Button>();
            if (spinButton == null) spinButton = btnObj.AddComponent<Button>();
            UIButtonPressEffect.AttachTo(btnObj, 0.94f, 0.12f);

            Transform txtTr = btnObj.transform.Find("Text");
            GameObject txtObj = txtTr != null ? txtTr.gameObject : new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            if (txtTr == null) txtObj.transform.SetParent(btnObj.transform, false);

            spinButtonTMP = txtObj.GetComponent<TextMeshProUGUI>();
            if (spinButtonTMP == null) spinButtonTMP = txtObj.AddComponent<TextMeshProUGUI>();
            spinButtonTMP.text = "";

            // Spin Cost / Free Spins Badge below the button
            Transform badgeTr = btnObj.transform.Find("SpinCostBadge");
            GameObject badgeObj = badgeTr != null ? badgeTr.gameObject : new GameObject("SpinCostBadge", typeof(RectTransform), typeof(Image));
            if (badgeTr == null) badgeObj.transform.SetParent(btnObj.transform, false);

            spinCostBadge = badgeObj.GetComponent<RectTransform>();
            spinCostBadge.anchorMin = new Vector2(0.5f, 0f);
            spinCostBadge.anchorMax = new Vector2(0.5f, 0f);
            spinCostBadge.pivot = new Vector2(0.5f, 1f);
            spinCostBadge.anchoredPosition = new Vector2(0f, -8f);
            spinCostBadge.sizeDelta = new Vector2(160f, 28f);

            Image badgeImg = badgeObj.GetComponent<Image>();
            if (badgeImg == null) badgeImg = badgeObj.AddComponent<Image>();
            badgeImg.color = new Color(0.12f, 0.06f, 0.22f, 0.92f);

            Transform badgeTxtTr = badgeObj.transform.Find("BadgeText");
            GameObject badgeTxtObj = badgeTxtTr != null ? badgeTxtTr.gameObject : new GameObject("BadgeText", typeof(RectTransform), typeof(TextMeshProUGUI));
            if (badgeTxtTr == null) badgeTxtObj.transform.SetParent(badgeObj.transform, false);

            RectTransform btRt = badgeTxtObj.GetComponent<RectTransform>();
            btRt.anchorMin = Vector2.zero;
            btRt.anchorMax = Vector2.one;
            btRt.sizeDelta = Vector2.zero;

            spinCostBadgeTMP = badgeTxtObj.GetComponent<TextMeshProUGUI>();
            if (spinCostBadgeTMP == null) spinCostBadgeTMP = badgeTxtObj.AddComponent<TextMeshProUGUI>();
            spinCostBadgeTMP.fontSize = 14f;
            spinCostBadgeTMP.fontStyle = FontStyles.Bold;
            spinCostBadgeTMP.alignment = TextAlignmentOptions.Center;
            spinCostBadgeTMP.color = new Color(1f, 0.9f, 0.3f, 1f);

            // 8. Close (X) Button
            Transform closeBtnTr = cabinetCard.Find("CloseButton");
            GameObject closeObj = closeBtnTr != null ? closeBtnTr.gameObject : new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            if (closeBtnTr == null) closeObj.transform.SetParent(cabinetCard, false);

            RectTransform cRt = closeObj.GetComponent<RectTransform>();
            cRt.anchorMin = new Vector2(0.5f, 0.5f);
            cRt.anchorMax = new Vector2(0.5f, 0.5f);
            cRt.pivot = new Vector2(0.5f, 0.5f);
            cRt.anchoredPosition = new Vector2(295f, 350f);
            cRt.sizeDelta = new Vector2(44f, 44f);

            Image cImg = closeObj.GetComponent<Image>();
            if (cImg == null) cImg = closeObj.AddComponent<Image>();
            cImg.color = new Color(0.85f, 0.15f, 0.22f, 1f);

            closeButton = closeObj.GetComponent<Button>();
            if (closeButton == null) closeButton = closeObj.AddComponent<Button>();
            UIButtonPressEffect.AttachTo(closeObj, 0.9f, 0.12f);

            Transform xTr = closeObj.transform.Find("XText");
            GameObject xObj = xTr != null ? xTr.gameObject : new GameObject("XText", typeof(RectTransform), typeof(TextMeshProUGUI));
            if (xTr == null) xObj.transform.SetParent(closeObj.transform, false);

            RectTransform xRt = xObj.GetComponent<RectTransform>();
            xRt.anchorMin = Vector2.zero;
            xRt.anchorMax = Vector2.one;
            xRt.sizeDelta = Vector2.zero;

            var xTmp = xObj.GetComponent<TextMeshProUGUI>();
            if (xTmp == null) xTmp = xObj.AddComponent<TextMeshProUGUI>();
            xTmp.text = "X";
            xTmp.fontSize = 24f;
            xTmp.fontStyle = FontStyles.Bold;
            xTmp.alignment = TextAlignmentOptions.Center;
            xTmp.color = Color.white;

            // 9. Confetti Effect
            if (confettiEffect == null)
            {
                confettiEffect = GetComponentInChildren<UIConfettiEffect>(true);
                if (confettiEffect == null)
                {
                    GameObject confObj = new GameObject("LottoConfettiEffect");
                    confObj.transform.SetParent(transform, false);
                    confettiEffect = confObj.AddComponent<UIConfettiEffect>();
                }
            }

            HookButtons();
            UpdateSpinButtonText();
        }

        private SlotReelView EnsureReel(Transform parent, string reelName, float xPos, float itemH = 92f)
        {
            Transform existing = parent.Find(reelName);
            GameObject reelObj;
            if (existing == null)
            {
                reelObj = new GameObject(reelName, typeof(RectTransform), typeof(SlotReelView));
                reelObj.transform.SetParent(parent, false);
            }
            else
            {
                reelObj = existing.gameObject;
            }

            RectTransform rRt = reelObj.GetComponent<RectTransform>();
            rRt.anchorMin = new Vector2(0.5f, 0.5f);
            rRt.anchorMax = new Vector2(0.5f, 0.5f);
            rRt.pivot = new Vector2(0.5f, 0.5f);
            rRt.anchoredPosition = new Vector2(xPos, 0f);
            rRt.sizeDelta = new Vector2(132f, 289f);

            SlotReelView reel = reelObj.GetComponent<SlotReelView>();
            if (reel == null) reel = reelObj.AddComponent<SlotReelView>();
            reel.SetItemHeight(itemH);
            return reel;
        }
    }
}
