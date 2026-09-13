using System;
using UnityEngine;

namespace LegoPuzzle.Runtime
{
    /// <summary>
    /// Manages player lives/hearts balance, persistence, and real-time offline recovery.
    /// Default capacity is 5 hearts, configurable in the Inspector.
    /// </summary>
    public class HeartManager : MonoBehaviour
    {
        public static HeartManager Instance { get; private set; }

        [Header("Hearts Configuration")]
        [Tooltip("Maximum capacity of hearts (default 5)")]
        [SerializeField] private int maxHearts = 5;

        [Tooltip("Time in minutes required to regenerate 1 heart")]
        [SerializeField] private float recoveryTimeMinutes = 15f;

        [Header("Testing & Debug")]
        [Tooltip("If enabled, hearts will never deplete (cheat for testing)")]
        [SerializeField] private bool infiniteHeartsCheat = false;

        private const string PREFS_HEARTS = "LEGO_CurrentHearts";
        private const string PREFS_NEXT_RECOVERY = "LEGO_NextHeartRecoveryTicks";
        private const string PREFS_INIT_FLAG = "LEGO_HeartsInitialized";

        private long nextRecoveryUtcTicks = 0;

        public event Action<int, int> OnHeartsChanged;
        public event Action<float> OnTimerTick;

        public int MaxHearts => maxHearts;
        public int CurrentHearts { get; private set; }

        public float RecoveryTimeMinutes
        {
            get => recoveryTimeMinutes;
            set => recoveryTimeMinutes = Mathf.Max(0.05f, value);
        }

        public float RecoveryIntervalSeconds => Mathf.Max(1f, recoveryTimeMinutes * 60f);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }

            LoadState();
            ProcessOfflineRecovery();
        }

        private void Update()
        {
            if (CurrentHearts < maxHearts && nextRecoveryUtcTicks > 0)
            {
                if (DateTime.UtcNow.Ticks >= nextRecoveryUtcTicks)
                {
                    ProcessRecoveryStep();
                }

                OnTimerTick?.Invoke(GetRemainingRecoverySeconds());
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (!pauseStatus)
            {
                // App resumed from background
                ProcessOfflineRecovery();
            }
            else
            {
                SaveState();
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
            {
                ProcessOfflineRecovery();
            }
            else
            {
                SaveState();
            }
        }

        /// <summary>
        /// Loads saved heart count and recovery timestamp, initializing with max hearts on first run.
        /// </summary>
        private void LoadState()
        {
            if (!PlayerPrefs.HasKey(PREFS_INIT_FLAG))
            {
                CurrentHearts = maxHearts;
                nextRecoveryUtcTicks = 0;
                PlayerPrefs.SetInt(PREFS_INIT_FLAG, 1);
                SaveState();
                return;
            }

            CurrentHearts = Mathf.Clamp(PlayerPrefs.GetInt(PREFS_HEARTS, maxHearts), 0, maxHearts);
            string savedTicks = PlayerPrefs.GetString(PREFS_NEXT_RECOVERY, "0");
            long.TryParse(savedTicks, out nextRecoveryUtcTicks);
        }

        private void SaveState()
        {
            PlayerPrefs.SetInt(PREFS_HEARTS, CurrentHearts);
            PlayerPrefs.SetString(PREFS_NEXT_RECOVERY, nextRecoveryUtcTicks.ToString());
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Checks how much real-world time passed since last recovery timestamp (works offline / app closed).
        /// </summary>
        public void ProcessOfflineRecovery()
        {
            if (CurrentHearts >= maxHearts || nextRecoveryUtcTicks <= 0)
            {
                return;
            }

            DateTime now = DateTime.UtcNow;
            DateTime target = new DateTime(nextRecoveryUtcTicks, DateTimeKind.Utc);

            if (now >= target)
            {
                TimeSpan excess = now - target;
                float interval = RecoveryIntervalSeconds;
                int heartsToAdd = 1 + (int)(excess.TotalSeconds / interval);

                CurrentHearts = Mathf.Min(maxHearts, CurrentHearts + heartsToAdd);

                if (CurrentHearts >= maxHearts)
                {
                    nextRecoveryUtcTicks = 0;
                }
                else
                {
                    double remainder = excess.TotalSeconds % interval;
                    double secondsToNext = interval - remainder;
                    nextRecoveryUtcTicks = now.AddSeconds(secondsToNext).Ticks;
                }

                SaveState();
                OnHeartsChanged?.Invoke(CurrentHearts, maxHearts);
            }
        }

        private void ProcessRecoveryStep()
        {
            DateTime now = DateTime.UtcNow;
            DateTime target = new DateTime(nextRecoveryUtcTicks, DateTimeKind.Utc);
            TimeSpan excess = now - target;
            float interval = RecoveryIntervalSeconds;

            int heartsToAdd = 1 + (int)(excess.TotalSeconds / interval);
            CurrentHearts = Mathf.Min(maxHearts, CurrentHearts + heartsToAdd);

            if (CurrentHearts >= maxHearts)
            {
                nextRecoveryUtcTicks = 0;
            }
            else
            {
                double remainder = excess.TotalSeconds % interval;
                double secondsToNext = interval - remainder;
                nextRecoveryUtcTicks = now.AddSeconds(secondsToNext).Ticks;
            }

            SaveState();
            OnHeartsChanged?.Invoke(CurrentHearts, maxHearts);
        }

        /// <summary>
        /// Returns true if player has at least 1 heart or cheat mode is on.
        /// </summary>
        public bool HasHearts()
        {
            return infiniteHeartsCheat || CurrentHearts > 0;
        }

        /// <summary>
        /// Deducts 1 heart. Initiates recovery timer if not already active.
        /// Returns true if deducted, false if already 0.
        /// </summary>
        public bool DeductHeart()
        {
            if (infiniteHeartsCheat)
            {
                Debug.Log("<color=cyan>HeartManager: Infinite hearts cheat active, heart not deducted.</color>");
                return true;
            }

            if (CurrentHearts <= 0)
            {
                Debug.LogWarning("<color=red>HeartManager: Attempted to deduct heart, but already 0!</color>");
                return false;
            }

            CurrentHearts--;

            // If we were at max hearts, start the recovery timer for this missing heart
            if (nextRecoveryUtcTicks <= 0)
            {
                nextRecoveryUtcTicks = DateTime.UtcNow.AddSeconds(RecoveryIntervalSeconds).Ticks;
            }

            SaveState();
            OnHeartsChanged?.Invoke(CurrentHearts, maxHearts);
            Debug.Log($"<color=red>HeartManager: Lost 1 heart. Remaining: {CurrentHearts}/{maxHearts}</color>");
            return true;
        }

        /// <summary>
        /// Restores a given amount of hearts.
        /// </summary>
        public void AddHeart(int count = 1)
        {
            if (count <= 0) return;

            CurrentHearts = Mathf.Min(maxHearts, CurrentHearts + count);
            if (CurrentHearts >= maxHearts)
            {
                nextRecoveryUtcTicks = 0;
            }

            SaveState();
            OnHeartsChanged?.Invoke(CurrentHearts, maxHearts);
        }

        /// <summary>
        /// Restores a given amount of hearts (alias for AddHeart).
        /// </summary>
        public void AddHearts(int count = 1) => AddHeart(count);

        /// <summary>
        /// Instantly refills hearts to maximum capacity.
        /// </summary>
        public void RefillAllHearts()
        {
            CurrentHearts = maxHearts;
            nextRecoveryUtcTicks = 0;
            SaveState();
            OnHeartsChanged?.Invoke(CurrentHearts, maxHearts);
            Debug.Log("<color=green>HeartManager: All hearts refilled to max!</color>");
        }

        /// <summary>
        /// Returns remaining seconds until the next heart regenerates.
        /// </summary>
        public float GetRemainingRecoverySeconds()
        {
            if (CurrentHearts >= maxHearts || nextRecoveryUtcTicks <= 0)
            {
                return 0f;
            }

            DateTime target = new DateTime(nextRecoveryUtcTicks, DateTimeKind.Utc);
            double diff = (target - DateTime.UtcNow).TotalSeconds;
            return Mathf.Max(0f, (float)diff);
        }

        /// <summary>
        /// Returns formatted string of remaining recovery time (e.g. '14:59' or 'FULL').
        /// </summary>
        public string GetFormattedRecoveryTime()
        {
            if (CurrentHearts >= maxHearts)
            {
                return "FULL";
            }

            float sec = GetRemainingRecoverySeconds();
            int m = Mathf.FloorToInt(sec / 60f);
            int s = Mathf.FloorToInt(sec % 60f);
            return $"{m:D2}:{s:D2}";
        }

        #region Editor Context Menus for Fast Testing
        [ContextMenu("Refill All Hearts (5)")]
        public void EditorRefillHearts() => RefillAllHearts();

        [ContextMenu("Deduct 1 Heart")]
        public void EditorDeductHeart() => DeductHeart();

        [ContextMenu("Set Hearts to 0")]
        public void EditorSetZeroHearts()
        {
            CurrentHearts = 0;
            if (nextRecoveryUtcTicks <= 0)
            {
                nextRecoveryUtcTicks = DateTime.UtcNow.AddSeconds(RecoveryIntervalSeconds).Ticks;
            }
            SaveState();
            OnHeartsChanged?.Invoke(CurrentHearts, maxHearts);
        }

        [ContextMenu("Reset Saved Hearts Progress")]
        public void EditorResetPrefs()
        {
            PlayerPrefs.DeleteKey(PREFS_INIT_FLAG);
            PlayerPrefs.DeleteKey(PREFS_HEARTS);
            PlayerPrefs.DeleteKey(PREFS_NEXT_RECOVERY);
            PlayerPrefs.Save();
            LoadState();
            OnHeartsChanged?.Invoke(CurrentHearts, maxHearts);
            Debug.Log("<color=yellow>HeartManager: PlayerPrefs reset to default!</color>");
        }
        #endregion
    }
}
