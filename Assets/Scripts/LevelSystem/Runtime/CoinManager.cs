using System;
using UnityEngine;

namespace LegoPuzzle.Runtime
{
    /// <summary>
    /// Manages the player's coin currency balance with PlayerPrefs persistence and event dispatching.
    /// Persistent singleton that automatically survives scene transitions and auto-creates if missing.
    /// </summary>
    public class CoinManager : MonoBehaviour
    {
        private static CoinManager instance;
        private static bool isApplicationQuitting = false;

        public static bool HasInstance => instance != null && !isApplicationQuitting;

        public static CoinManager Instance
        {
            get
            {
                if (isApplicationQuitting) return instance;

                if (instance == null)
                {
                    instance = FindAnyObjectByType<CoinManager>();
                    if (instance == null && Application.isPlaying)
                    {
                        GameObject managerObj = new GameObject("[CoinManager]");
                        instance = managerObj.AddComponent<CoinManager>();
                    }
                }
                return instance;
            }
        }

        private const string PREFS_COINS = "LEGO_CoinsBalance";
        private const string PREFS_INIT_FLAG = "LEGO_CoinsInitialized";

        [Header("Initial Configuration")]
        [Tooltip("Starting coins for a brand new player")]
        [SerializeField] private int defaultInitialCoins = 0;

        [Header("Debug & Cheats")]
        [Tooltip("If enabled, logs coin transactions to Console")]
        [SerializeField] private bool logTransactions = false;

        public event Action<int> OnCoinsChanged;
        public event Action<int, int> OnCoinsAdded; // (amountAdded, newTotal)

        public int Coins { get; private set; }

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

            LoadCoins();
        }

        /// <summary>
        /// Loads coins from PlayerPrefs or initializes starting amount.
        /// </summary>
        public void LoadCoins()
        {
            if (!PlayerPrefs.HasKey(PREFS_INIT_FLAG))
            {
                Coins = Mathf.Max(0, defaultInitialCoins);
                PlayerPrefs.SetInt(PREFS_COINS, Coins);
                PlayerPrefs.SetInt(PREFS_INIT_FLAG, 1);
                PlayerPrefs.Save();
            }
            else
            {
                Coins = Mathf.Max(0, PlayerPrefs.GetInt(PREFS_COINS, defaultInitialCoins));
            }

            OnCoinsChanged?.Invoke(Coins);
        }

        /// <summary>
        /// Saves current coin balance to PlayerPrefs.
        /// </summary>
        public void SaveCoins()
        {
            PlayerPrefs.SetInt(PREFS_COINS, Coins);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Awards coins to the player (e.g. from level completion rewards +40 or +80).
        /// </summary>
        public void AddCoins(int amount)
        {
            if (amount <= 0) return;

            int previous = Coins;
            Coins += amount;
            SaveCoins();

            if (logTransactions)
            {
                Debug.Log($"[CoinManager] +{amount} coins awarded! Balance: {previous} -> {Coins}");
            }

            OnCoinsChanged?.Invoke(Coins);
            OnCoinsAdded?.Invoke(amount, Coins);
        }

        /// <summary>
        /// Attempts to spend coins. Returns true if successful, false if insufficient balance.
        /// </summary>
        public bool SpendCoins(int amount)
        {
            if (amount <= 0) return true;

            if (Coins < amount)
            {
                if (logTransactions)
                {
                    Debug.LogWarning($"[CoinManager] Insufficient coins! Required: {amount}, current: {Coins}");
                }
                return false;
            }

            int previous = Coins;
            Coins -= amount;
            SaveCoins();

            if (logTransactions)
            {
                Debug.Log($"[CoinManager] -{amount} coins spent. Balance: {previous} -> {Coins}");
            }

            OnCoinsChanged?.Invoke(Coins);
            return true;
        }

        /// <summary>
        /// Checks if the player has at least the required amount of coins.
        /// </summary>
        public bool HasCoins(int amount)
        {
            return Coins >= amount;
        }

        /// <summary>
        /// Manually sets the coin balance (useful for testing or editor resets).
        /// </summary>
        public void SetCoins(int newAmount)
        {
            Coins = Mathf.Max(0, newAmount);
            SaveCoins();
            OnCoinsChanged?.Invoke(Coins);
        }

        /// <summary>
        /// Resets the coins balance back to default.
        /// </summary>
        public void ResetCoins()
        {
            SetCoins(defaultInitialCoins);
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                SaveCoins();
            }
        }

        private void OnApplicationQuit()
        {
            isApplicationQuitting = true;
            SaveCoins();
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
