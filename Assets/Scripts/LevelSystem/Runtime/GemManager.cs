using System;
using UnityEngine;

namespace LegoPuzzle.Runtime
{
    /// <summary>
    /// Manages the player's gem currency balance with PlayerPrefs persistence and event dispatching.
    /// Persistent singleton that automatically survives scene transitions and auto-creates if missing.
    /// </summary>
    public class GemManager : MonoBehaviour
    {
        private static GemManager instance;
        private static bool isApplicationQuitting = false;

        public static bool HasInstance => instance != null && !isApplicationQuitting;

        public static GemManager Instance
        {
            get
            {
                if (isApplicationQuitting) return instance;

                if (instance == null)
                {
                    instance = FindAnyObjectByType<GemManager>();
                    if (instance == null && Application.isPlaying)
                    {
                        GameObject managerObj = new GameObject("[GemManager]");
                        instance = managerObj.AddComponent<GemManager>();
                    }
                }
                return instance;
            }
        }

        private const string PREFS_GEMS = "LEGO_GemsBalance";
        private const string PREFS_INIT_FLAG = "LEGO_GemsInitialized";

        [Header("Initial Configuration")]
        [Tooltip("Starting gems for a brand new player")]
        [SerializeField] private int defaultInitialGems = 0;

        [Header("Debug & Cheats")]
        [Tooltip("If enabled, logs gem transactions to Console")]
        [SerializeField] private bool logTransactions = true;

        public event Action<int> OnGemsChanged;
        public event Action<int, int> OnGemsAdded; // (amountAdded, newTotal)

        public int Gems { get; private set; }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }

            LoadGems();
        }

        /// <summary>
        /// Loads gems from PlayerPrefs or initializes starting amount.
        /// </summary>
        public void LoadGems()
        {
            if (!PlayerPrefs.HasKey(PREFS_INIT_FLAG))
            {
                Gems = Mathf.Max(0, defaultInitialGems);
                PlayerPrefs.SetInt(PREFS_GEMS, Gems);
                PlayerPrefs.SetInt(PREFS_INIT_FLAG, 1);
                PlayerPrefs.Save();
            }
            else
            {
                Gems = Mathf.Max(0, PlayerPrefs.GetInt(PREFS_GEMS, defaultInitialGems));
            }

            OnGemsChanged?.Invoke(Gems);
        }

        /// <summary>
        /// Saves current gem balance to PlayerPrefs.
        /// </summary>
        public void SaveGems()
        {
            PlayerPrefs.SetInt(PREFS_GEMS, Gems);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Awards gems to the player (e.g. +1 gem upon winning any level).
        /// </summary>
        public void AddGems(int amount)
        {
            if (amount <= 0) return;

            int previous = Gems;
            Gems += amount;
            SaveGems();

            if (logTransactions)
            {
                Debug.Log($"<color=#D946EF><b>[GemManager]</b> +{amount} Gem awarded! Balance: {previous} -> {Gems}</color>");
            }

            OnGemsChanged?.Invoke(Gems);
            OnGemsAdded?.Invoke(amount, Gems);
        }

        /// <summary>
        /// Attempts to spend gems. Returns true if successful, false if insufficient balance.
        /// </summary>
        public bool SpendGems(int amount)
        {
            if (amount <= 0) return true;

            if (Gems < amount)
            {
                if (logTransactions)
                {
                    Debug.LogWarning($"[GemManager] Insufficient gems! Required: {amount}, current: {Gems}");
                }
                return false;
            }

            int previous = Gems;
            Gems += amount;
            SaveGems();

            if (logTransactions)
            {
                Debug.Log($"[GemManager] -{amount} gems spent. Balance: {previous} -> {Gems}");
            }

            OnGemsChanged?.Invoke(Gems);
            return true;
        }

        /// <summary>
        /// Checks if the player has at least the required amount of gems.
        /// </summary>
        public bool HasGems(int amount)
        {
            return Gems >= amount;
        }

        /// <summary>
        /// Manually sets the gem balance (useful for testing or editor resets).
        /// </summary>
        public void SetGems(int newAmount)
        {
            Gems = Mathf.Max(0, newAmount);
            SaveGems();
            OnGemsChanged?.Invoke(Gems);
        }

        /// <summary>
        /// Resets the gems balance back to default.
        /// </summary>
        public void ResetGems()
        {
            SetGems(defaultInitialGems);
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                SaveGems();
            }
        }

        private void OnApplicationQuit()
        {
            isApplicationQuitting = true;
            SaveGems();
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
