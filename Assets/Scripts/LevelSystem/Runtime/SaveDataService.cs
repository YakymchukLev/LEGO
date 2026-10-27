using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace LegoPuzzle.Runtime
{
    [Serializable]
    public class SettingsSaveData
    {
        public bool soundEnabled = true;
        public bool musicEnabled = true;
        public bool vibrationEnabled = true;
        public bool notificationsEnabled = true;
        public bool colorblindModeEnabled = false;
        public float soundVolume = 1f;
        public float musicVolume = 1f;
    }

    [Serializable]
    public class GameSaveData
    {
        public int version = 1;
        public string lastSavedUtc;

        // Ігровий прогрес
        public int currentLevelIndex = 0;

        // Економіка
        public int coins = 100;
        public int gems = 10;

        // Життя / Енергія
        public int hearts = 5;
        public long nextHeartRecoveryUtcTicks = 0;

        // Бустери
        public int hammerCharges = 3;
        public int freezeCharges = 3;
        public int hintCharges = 3;

        // Міні-гра Колесо Фортуни / Слоти
        public int lottoDailyFreeSpins = 3;
        public string lottoLastSpinDate = "";

        // Дошка лото. Раунд триває до місцевої опівночі на пристрої.
        public string lottoRoundLocalDate;
        public bool lottoUsesShortTimer;
        public long lottoRoundEndsUtcTicks;
        public int[] lottoHeaderNumbers;
        public int[] lottoSlotNumbers;
        public bool[] lottoSlotsOpen;
        public int lottoClaimedPrizeMask;

        // Налаштування гри
        public SettingsSaveData settings = new SettingsSaveData();

        public GameSaveData Clone()
        {
            string json = JsonUtility.ToJson(this);
            return JsonUtility.FromJson<GameSaveData>(json);
        }
    }

    [Serializable]
    public class SaveFileContainer
    {
        public string hash;
        public string dataJson;
    }

    /// <summary>
    /// Єдиний сервіс збереження гри. Зберігає всі дані (прогрес, валюти, серця, бустери, налаштування)
    /// в один серіалізований JSON-файл з базовим хешуванням SHA-256 для контролю цілісності та захисту від чітів.
    /// </summary>
    public class SaveDataService : MonoBehaviour
    {
        private const string HASH_SALT = "LEGO_Puzzle_BlockSaveData_Salt_2026_#Sec";
        private const string SAVE_FILE_NAME = "savedata.json";

        // Legacy PlayerPrefs ключі для міграції та сумісності
        private const string PREFS_LEVEL_INDEX = "LEGO_CurrentLevelIndex";
        private const string PREFS_COINS = "LEGO_CoinsBalance";
        private const string PREFS_COINS_INIT = "LEGO_CoinsInitialized";
        private const string PREFS_GEMS = "LEGO_GemsBalance";
        private const string PREFS_GEMS_INIT = "LEGO_GemsInitialized";
        private const string PREFS_HEARTS = "LEGO_CurrentHearts";
        private const string PREFS_HEARTS_INIT = "LEGO_HeartsInitialized";
        private const string PREFS_NEXT_RECOVERY = "LEGO_NextHeartRecoveryTicks";
        private const string PREFS_HAMMER_CHARGES = "LEGO_GlobalBoosterCharges_Hammer";
        private const string PREFS_HAMMER_CHARGES_ALT = "LEGO_BoosterCharges_Hammer";
        private const string PREFS_FREEZE_CHARGES = "LEGO_GlobalBoosterCharges_TimeFreeze";
        private const string PREFS_FREEZE_CHARGES_ALT = "LEGO_BoosterCharges_Freeze";
        private const string PREFS_HINT_CHARGES = "LEGO_GlobalBoosterCharges_Hint";
        private const string PREFS_HINT_CHARGES_ALT = "LEGO_BoosterCharges_Hint";
        private const string PREFS_LOTTO_FREE_SPINS = "LEGO_LottoDailyFreeSpins";
        private const string PREFS_SOUND_ENABLED = "LEGO_SoundEnabled";
        private const string PREFS_MUSIC_ENABLED = "LEGO_MusicEnabled";
        private const string PREFS_VIBRATION_ENABLED = "LEGO_VibrationEnabled";
        private const string PREFS_NOTIFICATIONS_ENABLED = "LEGO_NotificationsEnabled";
        private const string PREFS_COLORBLIND_ENABLED = "LEGO_ColorblindEnabled";
        private const string PREFS_SOUND_VOLUME = "LEGO_SoundVolume";
        private const string PREFS_MUSIC_VOLUME = "LEGO_MusicVolume";

        private static SaveDataService instance;
        private static bool isInitialized = false;

        public static SaveDataService Instance
        {
            get
            {
                if (instance == null)
                {
                    EnsureInstance();
                }
                return instance;
            }
        }

        public static bool HasInstance => instance != null;

        public GameSaveData Data { get; private set; } = new GameSaveData();

        public static event Action<GameSaveData> OnLoaded;
        public static event Action<GameSaveData> OnSaved;
        public static event Action OnReset;

        public static string SaveFilePath => Path.Combine(Application.persistentDataPath, SAVE_FILE_NAME);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            EnsureInstance();
        }

        private static void EnsureInstance()
        {
            if (instance != null) return;

            instance = FindAnyObjectByType<SaveDataService>();
            if (instance == null)
            {
                if (Application.isPlaying)
                {
                    GameObject go = new GameObject("[SaveDataService]");
                    instance = go.AddComponent<SaveDataService>();
                    DontDestroyOnLoad(go);
                }
                else
                {
                    // В режимі редактора (Edit mode) створюємо прихований тимчасовий екземпляр
                    // без виклику DontDestroyOnLoad, щоб не викликати InvalidOperationException.
                    GameObject go = new GameObject("[SaveDataService]")
                    {
                        hideFlags = HideFlags.HideAndDontSave
                    };
                    instance = go.AddComponent<SaveDataService>();
                }
            }

            if (!isInitialized)
            {
                instance.Load();
                isInitialized = true;
            }
        }

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                if (Application.isPlaying && transform.parent == null)
                {
                    DontDestroyOnLoad(gameObject);
                }

                if (!isInitialized)
                {
                    Load();
                    isInitialized = true;
                }
            }
            else if (instance != this)
            {
                if (Application.isPlaying)
                {
                    Destroy(gameObject);
                }
                else
                {
                    DestroyImmediate(gameObject);
                }
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                Save();
            }
        }

        private void OnApplicationQuit()
        {
            Save();
        }

        /// <summary>
        /// Зберігає поточний стан гри в JSON-файл з контрольною сумою SHA-256.
        /// Запис виконується атомарно (через тимчасовий файл) для запобігання пошкодженню сейву при аварійному закритті.
        /// </summary>
        public bool Save()
        {
            try
            {
                Data.lastSavedUtc = DateTime.UtcNow.ToString("o");
                string json = JsonUtility.ToJson(Data, true);
                string hash = ComputeHash(json);

                SaveFileContainer container = new SaveFileContainer
                {
                    hash = hash,
                    dataJson = json
                };

                string fileContent = JsonUtility.ToJson(container, true);
                string filePath = SaveFilePath;
                string tempPath = filePath + ".tmp";

                string directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(tempPath, fileContent, Encoding.UTF8);

                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                File.Move(tempPath, filePath);

                SyncToLegacyPlayerPrefs();

                OnSaved?.Invoke(Data);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveDataService] Помилка при збереженні гри: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Завантажує стан гри з JSON-файлу, перевіряючи контрольну суму SHA-256.
        /// Якщо файл відсутній, автоматично виконує міграцію існуючих даних з PlayerPrefs або створює чистий сейв.
        /// </summary>
        public bool Load()
        {
            string filePath = SaveFilePath;

            if (!File.Exists(filePath))
            {
                if (HasLegacyPlayerPrefs())
                {
                    Debug.Log("<color=cyan>[SaveDataService] Файл збереження не знайдено. Виконується міграція з PlayerPrefs...</color>");
                    Data = MigrateFromLegacyPlayerPrefs();
                    Save();
                    OnLoaded?.Invoke(Data);
                    return true;
                }

                Debug.Log("<color=cyan>[SaveDataService] Перший запуск гри. Створення нового файлу збереження.</color>");
                Data = new GameSaveData();
                Save();
                OnLoaded?.Invoke(Data);
                return true;
            }

            try
            {
                string fileContent = File.ReadAllText(filePath, Encoding.UTF8);
                SaveFileContainer container = JsonUtility.FromJson<SaveFileContainer>(fileContent);

                if (container != null && !string.IsNullOrEmpty(container.dataJson))
                {
                    string expectedHash = ComputeHash(container.dataJson);
                    if (string.Equals(container.hash, expectedHash, StringComparison.OrdinalIgnoreCase))
                    {
                        Data = JsonUtility.FromJson<GameSaveData>(container.dataJson);
                        if (Data == null)
                        {
                            Data = new GameSaveData();
                        }
                        Debug.Log($"<color=green>[SaveDataService] Успішно завантажено збереження (Рівень: {Data.currentLevelIndex + 1}, Монети: {Data.coins}, Геми: {Data.gems}, Серця: {Data.hearts})</color>");
                        SyncToLegacyPlayerPrefs();
                        OnLoaded?.Invoke(Data);
                        return true;
                    }
                    else
                    {
                        Debug.LogWarning($"<color=red>[SaveDataService] ПОМИЛКА ЦІЛІСНОСТІ! Хеш-сума сейву не збігається (можлива модифікація файлу). Очікувалось: {expectedHash}, Отримано: {container.hash}</color>");
                        BackupCorruptedFile(filePath);

                        if (HasLegacyPlayerPrefs())
                        {
                            Data = MigrateFromLegacyPlayerPrefs();
                        }
                        else
                        {
                            Data = new GameSaveData();
                        }
                        Save();
                        OnLoaded?.Invoke(Data);
                        return false;
                    }
                }
                else
                {
                    // Спроба розпарсити сирий JSON без контейнера (якщо файл був збережений вручну)
                    GameSaveData raw = JsonUtility.FromJson<GameSaveData>(fileContent);
                    if (raw != null)
                    {
                        Data = raw;
                        Save();
                        OnLoaded?.Invoke(Data);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveDataService] Критична помилка читання сейву: {ex.Message}");
                BackupCorruptedFile(filePath);
            }

            Data = new GameSaveData();
            Save();
            OnLoaded?.Invoke(Data);
            return false;
        }

        private static string ComputeHash(string rawData)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(rawData + HASH_SALT);
                byte[] hashBytes = sha256.ComputeHash(bytes);
                StringBuilder sb = new StringBuilder(hashBytes.Length * 2);
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }

        private void BackupCorruptedFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    string backupPath = $"{filePath}.corrupted_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
                    File.Copy(filePath, backupPath, true);
                    Debug.LogWarning($"[SaveDataService] Створено резервну копію пошкодженого файлу: {backupPath}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveDataService] Не вдалося створити резервну копію: {ex.Message}");
            }
        }

        private bool HasLegacyPlayerPrefs()
        {
            return PlayerPrefs.HasKey(PREFS_LEVEL_INDEX) ||
                   PlayerPrefs.HasKey(PREFS_COINS) ||
                   PlayerPrefs.HasKey(PREFS_GEMS) ||
                   PlayerPrefs.HasKey(PREFS_HEARTS);
        }

        private GameSaveData MigrateFromLegacyPlayerPrefs()
        {
            GameSaveData migrated = new GameSaveData();

            if (PlayerPrefs.HasKey(PREFS_LEVEL_INDEX))
                migrated.currentLevelIndex = Mathf.Max(0, PlayerPrefs.GetInt(PREFS_LEVEL_INDEX, 0));

            if (PlayerPrefs.HasKey(PREFS_COINS))
                migrated.coins = Mathf.Max(0, PlayerPrefs.GetInt(PREFS_COINS, 100));

            if (PlayerPrefs.HasKey(PREFS_GEMS))
                migrated.gems = Mathf.Max(0, PlayerPrefs.GetInt(PREFS_GEMS, 10));

            if (PlayerPrefs.HasKey(PREFS_HEARTS))
                migrated.hearts = Mathf.Clamp(PlayerPrefs.GetInt(PREFS_HEARTS, 5), 0, 5);

            if (PlayerPrefs.HasKey(PREFS_NEXT_RECOVERY))
            {
                string ticksStr = PlayerPrefs.GetString(PREFS_NEXT_RECOVERY, "0");
                long.TryParse(ticksStr, out migrated.nextHeartRecoveryUtcTicks);
            }

            if (PlayerPrefs.HasKey(PREFS_HAMMER_CHARGES))
                migrated.hammerCharges = PlayerPrefs.GetInt(PREFS_HAMMER_CHARGES, 3);
            else if (PlayerPrefs.HasKey(PREFS_HAMMER_CHARGES_ALT))
                migrated.hammerCharges = PlayerPrefs.GetInt(PREFS_HAMMER_CHARGES_ALT, 3);

            if (PlayerPrefs.HasKey(PREFS_FREEZE_CHARGES))
                migrated.freezeCharges = PlayerPrefs.GetInt(PREFS_FREEZE_CHARGES, 3);
            else if (PlayerPrefs.HasKey(PREFS_FREEZE_CHARGES_ALT))
                migrated.freezeCharges = PlayerPrefs.GetInt(PREFS_FREEZE_CHARGES_ALT, 3);

            if (PlayerPrefs.HasKey(PREFS_HINT_CHARGES))
                migrated.hintCharges = PlayerPrefs.GetInt(PREFS_HINT_CHARGES, 3);
            else if (PlayerPrefs.HasKey(PREFS_HINT_CHARGES_ALT))
                migrated.hintCharges = PlayerPrefs.GetInt(PREFS_HINT_CHARGES_ALT, 3);

            if (PlayerPrefs.HasKey(PREFS_LOTTO_FREE_SPINS))
                migrated.lottoDailyFreeSpins = PlayerPrefs.GetInt(PREFS_LOTTO_FREE_SPINS, 3);

            migrated.settings.soundEnabled = PlayerPrefs.GetInt(PREFS_SOUND_ENABLED, 1) == 1;
            migrated.settings.musicEnabled = PlayerPrefs.GetInt(PREFS_MUSIC_ENABLED, 1) == 1;
            migrated.settings.vibrationEnabled = PlayerPrefs.GetInt(PREFS_VIBRATION_ENABLED, 1) == 1;
            migrated.settings.notificationsEnabled = PlayerPrefs.GetInt(PREFS_NOTIFICATIONS_ENABLED, 1) == 1;
            migrated.settings.colorblindModeEnabled = PlayerPrefs.GetInt(PREFS_COLORBLIND_ENABLED, 0) == 1;
            migrated.settings.soundVolume = PlayerPrefs.GetFloat(PREFS_SOUND_VOLUME, 1f);
            migrated.settings.musicVolume = PlayerPrefs.GetFloat(PREFS_MUSIC_VOLUME, 1f);

            return migrated;
        }

        private void SyncToLegacyPlayerPrefs()
        {
            PlayerPrefs.SetInt(PREFS_LEVEL_INDEX, Data.currentLevelIndex);
            PlayerPrefs.SetInt(PREFS_COINS, Data.coins);
            PlayerPrefs.SetInt(PREFS_COINS_INIT, 1);
            PlayerPrefs.SetInt(PREFS_GEMS, Data.gems);
            PlayerPrefs.SetInt(PREFS_GEMS_INIT, 1);
            PlayerPrefs.SetInt(PREFS_HEARTS, Data.hearts);
            PlayerPrefs.SetInt(PREFS_HEARTS_INIT, 1);
            PlayerPrefs.SetString(PREFS_NEXT_RECOVERY, Data.nextHeartRecoveryUtcTicks.ToString());
            PlayerPrefs.SetInt(PREFS_HAMMER_CHARGES, Data.hammerCharges);
            PlayerPrefs.SetInt(PREFS_HAMMER_CHARGES_ALT, Data.hammerCharges);
            PlayerPrefs.SetInt(PREFS_FREEZE_CHARGES, Data.freezeCharges);
            PlayerPrefs.SetInt(PREFS_FREEZE_CHARGES_ALT, Data.freezeCharges);
            PlayerPrefs.SetInt(PREFS_HINT_CHARGES, Data.hintCharges);
            PlayerPrefs.SetInt(PREFS_HINT_CHARGES_ALT, Data.hintCharges);
            PlayerPrefs.SetInt(PREFS_LOTTO_FREE_SPINS, Data.lottoDailyFreeSpins);
            PlayerPrefs.SetInt(PREFS_SOUND_ENABLED, Data.settings.soundEnabled ? 1 : 0);
            PlayerPrefs.SetInt(PREFS_MUSIC_ENABLED, Data.settings.musicEnabled ? 1 : 0);
            PlayerPrefs.SetInt(PREFS_VIBRATION_ENABLED, Data.settings.vibrationEnabled ? 1 : 0);
            PlayerPrefs.SetInt(PREFS_NOTIFICATIONS_ENABLED, Data.settings.notificationsEnabled ? 1 : 0);
            PlayerPrefs.SetInt(PREFS_COLORBLIND_ENABLED, Data.settings.colorblindModeEnabled ? 1 : 0);
            PlayerPrefs.SetFloat(PREFS_SOUND_VOLUME, Data.settings.soundVolume);
            PlayerPrefs.SetFloat(PREFS_MUSIC_VOLUME, Data.settings.musicVolume);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Скидає весь прогрес, баланс монет, гемів, бустерів та налаштування до початкових значень.
        /// </summary>
        public void ResetAllData()
        {
            Data = new GameSaveData();
            Save();

            PlayerPrefs.DeleteKey(PREFS_LEVEL_INDEX);
            PlayerPrefs.DeleteKey(PREFS_COINS);
            PlayerPrefs.DeleteKey(PREFS_COINS_INIT);
            PlayerPrefs.DeleteKey(PREFS_GEMS);
            PlayerPrefs.DeleteKey(PREFS_GEMS_INIT);
            PlayerPrefs.DeleteKey(PREFS_HEARTS);
            PlayerPrefs.DeleteKey(PREFS_HEARTS_INIT);
            PlayerPrefs.DeleteKey(PREFS_NEXT_RECOVERY);
            PlayerPrefs.DeleteKey(PREFS_HAMMER_CHARGES);
            PlayerPrefs.DeleteKey(PREFS_HAMMER_CHARGES_ALT);
            PlayerPrefs.DeleteKey(PREFS_FREEZE_CHARGES);
            PlayerPrefs.DeleteKey(PREFS_FREEZE_CHARGES_ALT);
            PlayerPrefs.DeleteKey(PREFS_HINT_CHARGES);
            PlayerPrefs.DeleteKey(PREFS_HINT_CHARGES_ALT);
            PlayerPrefs.DeleteKey(PREFS_LOTTO_FREE_SPINS);
            PlayerPrefs.Save();

            OnReset?.Invoke();
            Debug.Log("<color=yellow>[SaveDataService] Всі дані збереження скинуто до дефолтних значень.</color>");
        }
    }
}
