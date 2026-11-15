using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LegoPuzzle.Runtime
{
    public enum DailyRewardKind
    {
        Coins,
        Gems,
        Hint,
        Freeze,
        Hammer
    }

    [Serializable]
    public class DailyRewardItem
    {
        public DailyRewardKind kind = DailyRewardKind.Coins;
        public int amount = 1;
        [Tooltip("Optional override. If null, panel default sprite for this kind is used.")]
        public Sprite iconOverride;
    }

    [Serializable]
    public class DailyRewardDaySlot
    {
        [Tooltip("Day number shown to player (1-7).")]
        public int dayNumber = 1;

        public RectTransform root;
        public Image background;
        public TMP_Text dayLabel;
        public Image[] rewardIcons;
        public TMP_Text[] rewardAmountLabels;
        public Button actionButton;
        public TMP_Text actionButtonLabel;
        public GameObject adIcon;
        public GameObject claimedOverlay;
        public GameObject lockedOverlay;

        [Tooltip("Rewards granted when this day is claimed.")]
        public DailyRewardItem[] rewards;
    }

    /// <summary>
    /// 7-day daily rewards modal. Scene-authored UI only — never rewrites layout/size/position.
    /// Free claim for today's day; missed days require a rewarded ad (placeholder for now).
    /// </summary>
    public class DailyRewardsPanel : MonoBehaviour
    {
        public static DailyRewardsPanel Instance { get; private set; }

        [Header("UI References (scene authored)")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform cardRect;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button backdropButton;
        [SerializeField] private DailyRewardDaySlot[] days = new DailyRewardDaySlot[7];

        [Header("Default Sprites (Inspector)")]
        [SerializeField] private Sprite hintSprite;
        [SerializeField] private Sprite freezeSprite;
        [SerializeField] private Sprite hammerSprite;
        [SerializeField] private Sprite coinSprite;
        [SerializeField] private Sprite gemSprite;

        [Header("State Colors (Inspector — applied only as day state feedback)")]
        [SerializeField] private Color availableColor = new Color(1f, 0.85f, 0.2f, 1f);
        [SerializeField] private Color missedColor = new Color(0.95f, 0.45f, 0.55f, 1f);
        [SerializeField] private Color lockedColor = new Color(0.62f, 0.62f, 0.66f, 1f);
        [SerializeField] private Color claimedColor = new Color(0.45f, 0.78f, 0.45f, 1f);
        [SerializeField] private bool applyStateColors = true;

        [Header("Button Labels")]
        [SerializeField] private string claimLabel = "CLAIM";
        [SerializeField] private string restoreLabel = "RESTORE";

        [Header("Test Mode")]
        [Tooltip("When on, each reward day lasts 1 minute instead of a calendar day.")]
        [SerializeField] private bool testModeOneMinuteDays;
        [SerializeField] private float testDayDurationSeconds = 60f;

        /// <summary>Inspector/debug: every unclaimed day is free CLAIM (no order / date gate).</summary>
        private bool debugUnlockAllDays;
        private float testModeRefreshTimer;
        private bool lastTestModeOneMinuteDays;

        private enum DayUiState
        {
            Locked,
            AvailableFree,
            MissedNeedsAd,
            Claimed
        }

        private Coroutine animateRoutine;
        private bool wasTimePaused;
        private Vector2 cardRestPosition;
        private bool cardPoseCached;
        private bool openRequested;

        private const float OpenDuration = 0.46f;
        private const float CloseDuration = 0.24f;
        private const float JellyIntensity = 0.12f;
        private const float OvershootDistance = 28f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            lastTestModeOneMinuteDays = testModeOneMinuteDays;
            CacheCardPose();
            BindButtons();
            // Rewards / icons / amounts are scene-authored. Do not load or paint sprites at runtime.
            EnsureDefaultRewards();
            // Never SetActive(false) here: if the object started inactive, Awake runs during Open()
            // and would immediately deactivate again, blocking StartCoroutine.
        }

        private void Start()
        {
            // Scene left the panel active for editing — hide once at boot unless Open already ran.
            if (Application.isPlaying && !openRequested && gameObject.activeSelf)
                gameObject.SetActive(false);
        }

        private void Update()
        {
            // Detect inspector toggle flips during Play (serialized field or custom editor).
            if (testModeOneMinuteDays != lastTestModeOneMinuteDays)
            {
                lastTestModeOneMinuteDays = testModeOneMinuteDays;
                OnTestModeChanged();
            }

            if (!testModeOneMinuteDays || !isActiveAndEnabled)
                return;

            testModeRefreshTimer += Time.unscaledDeltaTime;
            if (testModeRefreshTimer < 0.5f)
                return;

            testModeRefreshTimer = 0f;
            EnsureCycleStarted();
            RefreshDaysUi();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>Inspector: enable/disable 1-minute days test clock.</summary>
        public bool TestModeOneMinuteDays
        {
            get => testModeOneMinuteDays;
            set
            {
                if (testModeOneMinuteDays == value) return;
                testModeOneMinuteDays = value;
                lastTestModeOneMinuteDays = value;
                OnTestModeChanged();
            }
        }

        private void OnTestModeChanged()
        {
            if (!Application.isPlaying) return;
            if (!TryGetSave(out GameSaveData data)) return;

            // Restart cycle at "now" so day 1 is immediately available in the new clock.
            data.dailyRewardCycleStartDate = FormatCycleStartKey(DateTime.Now);
            data.dailyRewardClaimedMask = 0;
            data.dailyRewardLastClaimLocalTicks = 0;
            debugUnlockAllDays = false;
            SaveDataService.Instance.Save();
            if (isActiveAndEnabled)
                RefreshDaysUi();
            Debug.Log(testModeOneMinuteDays
                ? $"<color=cyan>[DailyRewards] Test mode ON — new day every {testDayDurationSeconds:0}s.</color>"
                : "<color=cyan>[DailyRewards] Test mode OFF — calendar days.</color>");
        }

        public void Open()
        {
            if (!DailyRewardsButton.IsUnlocked())
            {
                DailyRewardsButton.ShowLockedFloatingHint();
                return;
            }

            EnsureCycleStarted();
            openRequested = true;
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
            transform.SetAsLastSibling();
            RefreshDaysUi();

            wasTimePaused = Time.timeScale < 0.01f;
            if (!wasTimePaused)
                Time.timeScale = 0f;

            if (canvasGroup != null)
            {
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            if (animateRoutine != null)
                StopCoroutine(animateRoutine);
            animateRoutine = StartCoroutine(AnimateOpen());

            if (GameSettingsManager.HasInstance)
                GameSettingsManager.Instance.PlayClickSound();
        }

        public void Close()
        {
            if (!isActiveAndEnabled) return;
            if (animateRoutine != null)
                StopCoroutine(animateRoutine);
            animateRoutine = StartCoroutine(AnimateClose());
        }

        private void BindButtons()
        {
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

            if (days == null) return;
            for (int i = 0; i < days.Length; i++)
            {
                int dayIndex = i;
                DailyRewardDaySlot slot = days[i];
                if (slot == null || slot.actionButton == null) continue;
                slot.actionButton.onClick.RemoveAllListeners();
                slot.actionButton.onClick.AddListener(() => OnDayClicked(dayIndex));
            }
        }

        private void EnsureDefaultRewards()
        {
            if (days == null || days.Length != 7)
                days = new DailyRewardDaySlot[7];

            for (int i = 0; i < 7; i++)
            {
                if (days[i] == null)
                    days[i] = new DailyRewardDaySlot();
                days[i].dayNumber = i + 1;
                if (days[i].rewards != null && days[i].rewards.Length > 0)
                    continue;

                days[i].rewards = i switch
                {
                    0 => new[] { Item(DailyRewardKind.Hint, 3) },
                    1 => new[] { Item(DailyRewardKind.Coins, 200) },
                    2 => new[] { Item(DailyRewardKind.Freeze, 3) },
                    3 => new[] { Item(DailyRewardKind.Coins, 400) },
                    4 => new[] { Item(DailyRewardKind.Hammer, 3) },
                    5 => new[] { Item(DailyRewardKind.Coins, 800) },
                    _ => new[]
                    {
                        Item(DailyRewardKind.Hammer, 5),
                        Item(DailyRewardKind.Freeze, 5),
                        Item(DailyRewardKind.Hint, 5),
                        Item(DailyRewardKind.Gems, 10)
                    }
                };
            }
        }

        private static DailyRewardItem Item(DailyRewardKind kind, int amount)
        {
            return new DailyRewardItem { kind = kind, amount = amount };
        }

        private void EnsureCycleStarted()
        {
            if (!SaveDataService.HasInstance || SaveDataService.Instance.Data == null)
                return;

            GameSaveData data = SaveDataService.Instance.Data;
            if (string.IsNullOrEmpty(data.dailyRewardCycleStartDate))
            {
                data.dailyRewardCycleStartDate = FormatCycleStartKey(DateTime.Now);
                data.dailyRewardClaimedMask = 0;
                data.dailyRewardLastClaimLocalTicks = 0;
                SaveDataService.Instance.Save();
            }
            else
            {
                MaybeRollNewCycle(data);
                MigrateLastClaimIfNeeded(data);
            }
        }

        /// <summary>
        /// Older saves claimed days without last-claim timestamps — backdate so the next day can unlock.
        /// </summary>
        private void MigrateLastClaimIfNeeded(GameSaveData data)
        {
            if (data == null || data.dailyRewardLastClaimLocalTicks > 0)
                return;
            if (data.dailyRewardClaimedMask == 0)
                return;

            // One full day-length ago → next streak day is AvailableFree immediately.
            if (testModeOneMinuteDays)
                data.dailyRewardLastClaimLocalTicks = DateTime.Now.AddSeconds(-Mathf.Max(1f, testDayDurationSeconds)).Ticks;
            else
                data.dailyRewardLastClaimLocalTicks = DateTime.Now.Date.AddDays(-1).Ticks;

            SaveDataService.Instance.Save();
        }

        private void MaybeRollNewCycle(GameSaveData data)
        {
            if (data == null) return;
            if ((data.dailyRewardClaimedMask & 0x7F) != 0x7F)
                return;

            // All 7 claimed — start next cycle after the last day of the previous cycle.
            if (!TryParseCycleStart(data.dailyRewardCycleStartDate, out DateTime start))
                return;

            DateTime nextCycleStart = testModeOneMinuteDays
                ? start.AddSeconds(7d * Mathf.Max(1f, testDayDurationSeconds))
                : start.Date.AddDays(7);
            DateTime now = DateTime.Now;
            if (now >= nextCycleStart)
            {
                data.dailyRewardCycleStartDate = FormatCycleStartKey(now);
                data.dailyRewardClaimedMask = 0;
                data.dailyRewardLastClaimLocalTicks = 0;
                SaveDataService.Instance.Save();
            }
        }

        private void RefreshDaysUi()
        {
            if (days == null) return;
            DateTime now = DateTime.Now;

            for (int i = 0; i < days.Length; i++)
            {
                DailyRewardDaySlot slot = days[i];
                if (slot == null) continue;

                DayUiState state = GetDayState(i, now);
                ApplyDayState(slot, state);
            }
        }

        private DayUiState GetDayState(int dayIndex, DateTime now)
        {
            if (!SaveDataService.HasInstance || SaveDataService.Instance.Data == null)
                return DayUiState.Locked;

            GameSaveData data = SaveDataService.Instance.Data;
            int bit = 1 << dayIndex;
            if ((data.dailyRewardClaimedMask & bit) != 0)
                return DayUiState.Claimed;

            if (debugUnlockAllDays)
                return DayUiState.AvailableFree;

            if (string.IsNullOrEmpty(data.dailyRewardCycleStartDate))
                return dayIndex == 0 ? DayUiState.AvailableFree : DayUiState.Locked;

            if (!TryParseCycleStart(data.dailyRewardCycleStartDate, out DateTime start))
                return DayUiState.Locked;

            int elapsed = GetElapsedDayIndex(now, start);
            int nextUnclaimed = GetFirstUnclaimedDayIndex(data.dailyRewardClaimedMask);

            // Calendar / test clock: any day whose slot time has arrived.
            if (dayIndex < elapsed)
                return DayUiState.MissedNeedsAd;
            if (dayIndex == elapsed)
                return DayUiState.AvailableFree;

            // Ahead of the absolute clock, but next in streak: unlock one "day" after last claim.
            // Fixes claiming 1–4 early (unlock-all / catch-up) then waiting for the next day.
            if (dayIndex == nextUnclaimed && IsProgressUnlockReady(data, now))
                return DayUiState.AvailableFree;

            return DayUiState.Locked;
        }

        private int GetElapsedDayIndex(DateTime now, DateTime start)
        {
            if (testModeOneMinuteDays)
            {
                float daySeconds = Mathf.Max(1f, testDayDurationSeconds);
                double elapsed = (now - start).TotalSeconds / daySeconds;
                return Mathf.Clamp(Mathf.FloorToInt((float)elapsed), 0, 6);
            }

            int days = (now.Date - start.Date).Days;
            return Mathf.Clamp(days, 0, 6);
        }

        private static int GetFirstUnclaimedDayIndex(int claimedMask)
        {
            for (int i = 0; i < 7; i++)
            {
                if ((claimedMask & (1 << i)) == 0)
                    return i;
            }

            return 7;
        }

        private bool IsProgressUnlockReady(GameSaveData data, DateTime now)
        {
            if (data.dailyRewardLastClaimLocalTicks <= 0)
                return false;

            DateTime lastClaim;
            try
            {
                lastClaim = new DateTime(data.dailyRewardLastClaimLocalTicks, DateTimeKind.Local);
            }
            catch
            {
                return false;
            }

            if (testModeOneMinuteDays)
                return now >= lastClaim.AddSeconds(Mathf.Max(1f, testDayDurationSeconds));

            return now.Date >= lastClaim.Date.AddDays(1);
        }

        private void ApplyDayState(DailyRewardDaySlot slot, DayUiState state)
        {
            if (applyStateColors && slot.background != null)
            {
                slot.background.color = state switch
                {
                    DayUiState.AvailableFree => availableColor,
                    DayUiState.MissedNeedsAd => missedColor,
                    DayUiState.Claimed => claimedColor,
                    _ => lockedColor
                };
            }

            if (slot.claimedOverlay != null)
                slot.claimedOverlay.SetActive(state == DayUiState.Claimed);

            if (slot.lockedOverlay != null)
                slot.lockedOverlay.SetActive(state == DayUiState.Locked);

            bool showAction = state == DayUiState.AvailableFree || state == DayUiState.MissedNeedsAd;
            if (slot.actionButton != null)
            {
                slot.actionButton.gameObject.SetActive(showAction);
                slot.actionButton.interactable = showAction;
            }

            if (slot.actionButtonLabel != null)
            {
                slot.actionButtonLabel.text = state == DayUiState.MissedNeedsAd ? restoreLabel : claimLabel;
            }

            if (slot.adIcon != null)
                slot.adIcon.SetActive(state == DayUiState.MissedNeedsAd);
        }

        private void OnDayClicked(int dayIndex)
        {
            DayUiState state = GetDayState(dayIndex, DateTime.Now);

            if (state == DayUiState.AvailableFree)
            {
                ClaimDay(dayIndex, requiredAd: false);
                return;
            }

            if (state == DayUiState.MissedNeedsAd)
            {
                // Placeholder rewarded ad — always succeeds for now.
                Debug.Log("<color=cyan>[DailyRewards] Rewarded ad placeholder watched for missed day.</color>");
                ClaimDay(dayIndex, requiredAd: true);
            }
        }

        private void ClaimDay(int dayIndex, bool requiredAd)
        {
            if (!SaveDataService.HasInstance || SaveDataService.Instance.Data == null)
                return;

            GameSaveData data = SaveDataService.Instance.Data;
            EnsureCycleStarted();

            int bit = 1 << dayIndex;
            if ((data.dailyRewardClaimedMask & bit) != 0)
                return;

            if (days == null || dayIndex < 0 || dayIndex >= days.Length || days[dayIndex] == null)
                return;

            // Missed days stay restorable while later days can be claimed independently.
            GrantRewards(days[dayIndex].rewards);
            data.dailyRewardClaimedMask |= bit;
            data.dailyRewardLastClaimLocalTicks = DateTime.Now.Ticks;
            SaveDataService.Instance.Save();
            RefreshDaysUi();

            if (GameSettingsManager.HasInstance)
            {
                GameSettingsManager.Instance.PlayClickSound();
                GameSettingsManager.Instance.TriggerHaptic();
            }

            Debug.Log($"<color=green>[DailyRewards] Claimed day {dayIndex + 1} (ad={requiredAd}).</color>");
        }

        /// <summary>Inspector debug: clear claims so the cycle can be claimed again from day 1.</summary>
        public void DebugResetClaims()
        {
            if (!TryGetSave(out GameSaveData data))
            {
                Debug.LogWarning("[DailyRewards] Reset failed — SaveDataService not ready (enter Play Mode).");
                return;
            }

            debugUnlockAllDays = false;
            data.dailyRewardCycleStartDate = FormatCycleStartKey(DateTime.Now);
            data.dailyRewardClaimedMask = 0;
            data.dailyRewardLastClaimLocalTicks = 0;
            SaveDataService.Instance.Save();
            if (isActiveAndEnabled)
                RefreshDaysUi();
            Debug.Log("<color=yellow>[DailyRewards] Claims reset — day 1 available again.</color>");
        }

        /// <summary>Inspector debug: every unclaimed day shows free CLAIM (no date/order lock).</summary>
        public void DebugUnlockAllRewards()
        {
            if (!TryGetSave(out GameSaveData data))
            {
                Debug.LogWarning("[DailyRewards] Unlock-all failed — SaveDataService not ready (enter Play Mode).");
                return;
            }

            debugUnlockAllDays = true;
            if (string.IsNullOrEmpty(data.dailyRewardCycleStartDate))
                data.dailyRewardCycleStartDate = FormatCycleStartKey(DateTime.Now);
            SaveDataService.Instance.Save();

            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
            RefreshDaysUi();
            Debug.Log("<color=yellow>[DailyRewards] All unclaimed days unlocked for free CLAIM.</color>");
        }

        private static bool TryGetSave(out GameSaveData data)
        {
            data = null;
            if (!SaveDataService.HasInstance || SaveDataService.Instance.Data == null)
                return false;
            data = SaveDataService.Instance.Data;
            return true;
        }

        private void GrantRewards(DailyRewardItem[] rewards)
        {
            if (rewards == null) return;

            for (int i = 0; i < rewards.Length; i++)
            {
                DailyRewardItem item = rewards[i];
                if (item == null || item.amount <= 0) continue;

                switch (item.kind)
                {
                    case DailyRewardKind.Coins:
                        if (CoinManager.HasInstance)
                            CoinManager.Instance.AddCoins(item.amount);
                        else if (SaveDataService.HasInstance)
                        {
                            SaveDataService.Instance.Data.coins += item.amount;
                        }
                        break;
                    case DailyRewardKind.Gems:
                        if (GemManager.HasInstance)
                            GemManager.Instance.AddGems(item.amount);
                        else if (SaveDataService.HasInstance)
                        {
                            SaveDataService.Instance.Data.gems += item.amount;
                        }
                        break;
                    case DailyRewardKind.Hint:
                        if (SaveDataService.HasInstance && SaveDataService.Instance.Data != null)
                            SaveDataService.Instance.Data.hintCharges += item.amount;
                        break;
                    case DailyRewardKind.Freeze:
                        if (SaveDataService.HasInstance && SaveDataService.Instance.Data != null)
                            SaveDataService.Instance.Data.freezeCharges += item.amount;
                        break;
                    case DailyRewardKind.Hammer:
                        if (SaveDataService.HasInstance && SaveDataService.Instance.Data != null)
                            SaveDataService.Instance.Data.hammerCharges += item.amount;
                        break;
                }
            }
        }

        private void CacheCardPose()
        {
            if (cardRect == null)
            {
                Transform card = transform.Find("Card");
                if (card != null)
                    cardRect = card as RectTransform;
            }

            if (cardRect != null && !cardPoseCached)
            {
                cardRestPosition = cardRect.anchoredPosition;
                cardPoseCached = true;
            }

            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
        }

        private void ResetCardPose()
        {
            CacheCardPose();
            if (cardRect != null)
            {
                cardRect.anchoredPosition = cardRestPosition;
                cardRect.localScale = Vector3.one;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.blocksRaycasts = true;
            }
        }

        private Vector2 OffscreenCardPosition()
        {
            if (cardRect == null)
                return new Vector2(cardRestPosition.x, cardRestPosition.y - 1600f);

            float parentHeight = 1920f;
            RectTransform parentRt = cardRect.parent as RectTransform;
            if (parentRt != null && parentRt.rect.height > 100f)
                parentHeight = parentRt.rect.height;

            float cardHeight = cardRect.rect.height > 50f ? cardRect.rect.height : 980f;
            float cardTopExtent = cardHeight * (1f - cardRect.pivot.y);
            float offscreenY = -(parentHeight * 0.5f + cardTopExtent + 140f);
            return new Vector2(cardRestPosition.x, offscreenY);
        }

        private IEnumerator AnimateOpen()
        {
            CacheCardPose();
            if (cardRect == null)
            {
                // Fallback fade if Card is missing.
                if (canvasGroup != null)
                {
                    float t = 0f;
                    canvasGroup.alpha = 0f;
                    while (t < 0.22f)
                    {
                        t += Time.unscaledDeltaTime;
                        canvasGroup.alpha = Mathf.Clamp01(t / 0.22f);
                        yield return null;
                    }

                    canvasGroup.alpha = 1f;
                }

                animateRoutine = null;
                yield break;
            }

            Vector2 startPos = OffscreenCardPosition();
            cardRect.anchoredPosition = startPos;
            cardRect.localScale = new Vector3(0.58f, 0.58f, 1f);
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = true;
            }

            float elapsed = 0f;
            while (elapsed < OpenDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / OpenDuration);

                float travelT = Mathf.Clamp01(t / 0.62f);
                float baseTravel = 1f - Mathf.Pow(1f - travelT, 3f);
                float baseY = Mathf.Lerp(startPos.y, cardRestPosition.y, baseTravel);

                float bounceOffset = 0f;
                if (t >= 0.45f)
                {
                    float bt = (t - 0.45f) / 0.55f;
                    float decay = (1f - bt) * (1f - bt);
                    bounceOffset = Mathf.Sin(bt * Mathf.PI * 2.2f) * OvershootDistance * decay;
                }

                cardRect.anchoredPosition = new Vector2(cardRestPosition.x, baseY + bounceOffset);

                float growT = 1f - Mathf.Pow(1f - t, 2.6f);
                float baseScale = Mathf.Lerp(0.58f, 1f, growT);
                float jelly;
                if (t < 0.45f)
                {
                    float flightP = t / 0.45f;
                    jelly = Mathf.Sin(flightP * Mathf.PI) * JellyIntensity;
                }
                else
                {
                    float impactP = (t - 0.45f) / 0.55f;
                    float decay = (1f - impactP) * (1f - impactP);
                    jelly = -Mathf.Sin(impactP * Mathf.PI * 2f) * (JellyIntensity * 0.85f) * decay;
                }

                cardRect.localScale = new Vector3(baseScale * (1f - jelly), baseScale * (1f + jelly), 1f);
                if (canvasGroup != null)
                    canvasGroup.alpha = Mathf.Clamp01(t * 2.8f);

                yield return null;
            }

            ResetCardPose();
            animateRoutine = null;
        }

        private IEnumerator AnimateClose()
        {
            CacheCardPose();
            if (cardRect == null)
            {
                if (canvasGroup != null)
                {
                    float start = canvasGroup.alpha;
                    float t = 0f;
                    while (t < CloseDuration)
                    {
                        t += Time.unscaledDeltaTime;
                        canvasGroup.alpha = Mathf.Lerp(start, 0f, Mathf.Clamp01(t / CloseDuration));
                        yield return null;
                    }

                    canvasGroup.alpha = 0f;
                }

                if (!wasTimePaused)
                    Time.timeScale = 1f;
                gameObject.SetActive(false);
                animateRoutine = null;
                yield break;
            }

            Vector2 startPos = cardRect.anchoredPosition;
            Vector2 offscreenPos = OffscreenCardPosition();
            Vector3 initialScale = cardRect.localScale;
            float startAlpha = canvasGroup != null ? canvasGroup.alpha : 1f;

            float elapsed = 0f;
            while (elapsed < CloseDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / CloseDuration);
                float moveProgress;
                float sx;
                float sy;

                if (t < 0.18f)
                {
                    float p = t / 0.18f;
                    float anticip = Mathf.Sin(p * Mathf.PI);
                    sx = Mathf.Lerp(initialScale.x, 1.04f, anticip);
                    sy = Mathf.Lerp(initialScale.y, 0.95f, anticip);
                    moveProgress = 0f;
                }
                else
                {
                    float p = (t - 0.18f) / 0.82f;
                    moveProgress = p * p * p;
                    sx = Mathf.Lerp(1.04f, 0.88f, p);
                    sy = Mathf.Lerp(0.95f, 1.10f, p);
                }

                cardRect.anchoredPosition = Vector2.Lerp(startPos, offscreenPos, moveProgress);
                cardRect.localScale = new Vector3(sx, sy, 1f);
                if (canvasGroup != null)
                    canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, Mathf.Clamp01(t * 1.5f));

                yield return null;
            }

            ResetCardPose();
            if (!wasTimePaused)
                Time.timeScale = 1f;
            gameObject.SetActive(false);
            animateRoutine = null;
        }

        private string FormatCycleStartKey(DateTime moment)
        {
            if (testModeOneMinuteDays)
            {
                return moment.ToString(
                    "yyyy-MM-dd HH:mm:ss",
                    System.Globalization.CultureInfo.InvariantCulture);
            }

            return moment.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static bool TryParseCycleStart(string key, out DateTime date)
        {
            if (string.IsNullOrEmpty(key))
            {
                date = default;
                return false;
            }

            if (DateTime.TryParseExact(
                    key,
                    "yyyy-MM-dd HH:mm:ss",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out date))
                return true;

            if (DateTime.TryParseExact(
                    key,
                    "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out date))
                return true;

            return DateTime.TryParse(
                key,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out date);
        }

    }
}
