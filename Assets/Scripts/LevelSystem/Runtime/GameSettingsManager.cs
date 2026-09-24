using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LegoPuzzle.Runtime
{
    /// <summary>
    /// Central manager for game settings: Sound FX, Background Music, Vibration (Haptics),
    /// and Notifications, with PlayerPrefs persistence and runtime event dispatching.
    /// Persistent singleton that automatically survives scene transitions and auto-creates if missing.
    /// </summary>
    public class GameSettingsManager : MonoBehaviour
    {
        private static GameSettingsManager instance;
        private static bool isApplicationQuitting = false;

        public static bool HasInstance => instance != null && !isApplicationQuitting;

        public static GameSettingsManager Instance
        {
            get
            {
                if (isApplicationQuitting) return instance;

                if (instance == null)
                {
                    instance = FindAnyObjectByType<GameSettingsManager>();
                    if (instance == null && Application.isPlaying)
                    {
                        GameObject managerObj = new GameObject("[GameSettingsManager]");
                        instance = managerObj.AddComponent<GameSettingsManager>();
                    }
                }
                return instance;
            }
        }

        private const string PREFS_SOUND_ENABLED = "LEGO_SoundEnabled";
        private const string PREFS_MUSIC_ENABLED = "LEGO_MusicEnabled";
        private const string PREFS_VIBRATION_ENABLED = "LEGO_VibrationEnabled";
        private const string PREFS_NOTIFICATIONS_ENABLED = "LEGO_NotificationsEnabled";
        private const string PREFS_COLORBLIND_ENABLED = "LEGO_ColorblindEnabled";
        private const string PREFS_SOUND_VOLUME = "LEGO_SoundVolume";
        private const string PREFS_MUSIC_VOLUME = "LEGO_MusicVolume";
        private const string PREFS_LEVEL_INDEX = "LEGO_CurrentLevelIndex";

        [Header("Audio Clip References")]
        [SerializeField] private AudioClip buttonClickSound;

        [Header("Background Music Clips")]
        [Tooltip("Фонова музика для головного меню (Assets/Sound/main-menu.wav)")]
        [SerializeField] private AudioClip menuMusicClip;

        [Tooltip("Фоновий звук для сцени гри (Assets/Sound/lvlsound.mp3)")]
        [SerializeField] private AudioClip gameMusicClip;

        [Header("Background Music Settings")]
        [Range(0f, 1f)] [SerializeField] private float baseMusicVolume = 0.55f;
        [SerializeField] private float musicFadeDuration = 0.45f;

        [Header("Runtime Audio Sources")]
        private AudioSource uiAudioSource;
        private AudioSource musicAudioSource;
        private Coroutine musicFadeCoroutine;

        public event Action OnSettingsChanged;
        public event Action<bool> OnColorblindModeChanged;

        public bool SoundEnabled { get; private set; } = true;
        public bool MusicEnabled { get; private set; } = true;
        public bool VibrationEnabled { get; private set; } = true;
        public bool NotificationsEnabled { get; private set; } = true;
        public bool ColorblindModeEnabled { get; private set; } = false;
        public float SoundVolume { get; private set; } = 1f;
        public float MusicVolume { get; private set; } = 1f;

        public AudioClip ButtonClickSound { get => buttonClickSound; set => buttonClickSound = value; }
        public AudioClip MenuMusicClip { get => menuMusicClip; set => menuMusicClip = value; }
        public AudioClip GameMusicClip { get => gameMusicClip; set => gameMusicClip = value; }

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

            EnsureAudioSources();
            LoadSettings();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            if (buttonWatchCoroutine != null)
            {
                StopCoroutine(buttonWatchCoroutine);
                buttonWatchCoroutine = null;
            }
        }

        private void Start()
        {
            EnsureAudioSources();
            UpdateMusicForActiveScene();
            StartButtonAudioTracking(SceneManager.GetActiveScene().name);
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            UpdateMusicForActiveScene(scene.name);
            StartButtonAudioTracking(scene.name);
        }

        public void EnsureAudioSources()
        {
            if (uiAudioSource == null)
            {
                uiAudioSource = GetComponent<AudioSource>();
                if (uiAudioSource == null)
                {
                    uiAudioSource = gameObject.AddComponent<AudioSource>();
                }
                uiAudioSource.playOnAwake = false;
            }

            if (musicAudioSource == null)
            {
                Transform musicTr = transform.Find("MusicSource");
                if (musicTr == null)
                {
                    GameObject musicObj = new GameObject("MusicSource");
                    musicObj.transform.SetParent(transform, false);
                    musicTr = musicObj.transform;
                }

                musicAudioSource = musicTr.GetComponent<AudioSource>();
                if (musicAudioSource == null)
                {
                    musicAudioSource = musicTr.gameObject.AddComponent<AudioSource>();
                }

                musicAudioSource.loop = true;
                musicAudioSource.playOnAwake = false;
                musicAudioSource.spatialBlend = 0f; // 2D Sound
            }

            if (uiAudioSource != null)
            {
                uiAudioSource.spatialBlend = 0f; // 2D Sound
            }

            if (buttonClickSound == null || buttonClickSound.name != "Button_Click")
            {
#if UNITY_EDITOR
                AudioClip clickClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/Button_Click.wav");
                if (clickClip != null)
                {
                    buttonClickSound = clickClip;
                }
                else if (buttonClickSound == null)
                {
                    buttonClickSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/610524__pietheanimator45__clicking-lego-brick.wav");
                }
#endif
            }

            if (menuMusicClip == null)
            {
#if UNITY_EDITOR
                menuMusicClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/main-menu.wav");
#endif
            }

            if (gameMusicClip == null)
            {
#if UNITY_EDITOR
                gameMusicClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/lvlsound.mp3");
#endif
            }
        }

        /// <summary>
        /// Автоматично перемикає фоновий трек відповідно до поточної або переданої сцени.
        /// </summary>
        public void UpdateMusicForActiveScene(string sceneName = null)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                sceneName = SceneManager.GetActiveScene().name;
            }

            EnsureAudioSources();

            bool isGameScene = sceneName.IndexOf("Game", StringComparison.OrdinalIgnoreCase) >= 0;
            AudioClip targetClip = isGameScene ? gameMusicClip : menuMusicClip;

            if (targetClip != null)
            {
                PlayMusic(targetClip, musicFadeDuration);
            }
        }

        /// <summary>
        /// Відтворює вказаний аудіокліп у фоновому аудіосорсі з плавним переходом (фейдом).
        /// </summary>
        public void PlayMusic(AudioClip clip, float fadeDuration = 0.45f)
        {
            EnsureAudioSources();
            if (musicAudioSource == null) return;

            if (clip == null)
            {
                StopMusic(fadeDuration);
                return;
            }

            // Якщо вже грає цей самий кліп — просто переконуємося в правильній гучності та mute
            if (musicAudioSource.clip == clip && musicAudioSource.isPlaying)
            {
                musicAudioSource.mute = !MusicEnabled;
                if (musicFadeCoroutine == null)
                {
                    musicAudioSource.volume = MusicVolume * baseMusicVolume;
                }
                return;
            }

            if (musicFadeCoroutine != null)
            {
                StopCoroutine(musicFadeCoroutine);
            }

            if (gameObject.activeInHierarchy)
            {
                musicFadeCoroutine = StartCoroutine(FadeMusicRoutine(clip, fadeDuration));
            }
            else
            {
                musicAudioSource.clip = clip;
                musicAudioSource.mute = !MusicEnabled;
                musicAudioSource.volume = MusicVolume * baseMusicVolume;
                musicAudioSource.Play();
            }
        }

        /// <summary>
        /// Плавно зупиняє фонову музику.
        /// </summary>
        public void StopMusic(float fadeDuration = 0.45f)
        {
            if (musicAudioSource == null || !musicAudioSource.isPlaying) return;

            if (musicFadeCoroutine != null)
            {
                StopCoroutine(musicFadeCoroutine);
            }

            if (gameObject.activeInHierarchy && fadeDuration > 0.05f)
            {
                musicFadeCoroutine = StartCoroutine(FadeMusicRoutine(null, fadeDuration));
            }
            else
            {
                musicAudioSource.Stop();
                musicAudioSource.clip = null;
                musicFadeCoroutine = null;
            }
        }

        public void PlayMenuMusic(float fadeDuration = 0.45f)
        {
            if (menuMusicClip != null)
            {
                PlayMusic(menuMusicClip, fadeDuration);
            }
        }

        public void PlayGameMusic(float fadeDuration = 0.45f)
        {
            if (gameMusicClip != null)
            {
                PlayMusic(gameMusicClip, fadeDuration);
            }
        }

        private IEnumerator FadeMusicRoutine(AudioClip newClip, float duration)
        {
            if (musicAudioSource == null) yield break;

            float startVol = musicAudioSource.volume;
            float targetVol = MusicVolume * baseMusicVolume;

            if (duration > 0.05f && musicAudioSource.isPlaying && musicAudioSource.volume > 0.01f)
            {
                float halfDuration = duration * 0.5f;
                float elapsed = 0f;
                while (elapsed < halfDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    musicAudioSource.volume = Mathf.Lerp(startVol, 0f, elapsed / halfDuration);
                    yield return null;
                }
            }

            musicAudioSource.Stop();
            musicAudioSource.clip = newClip;
            musicAudioSource.mute = !MusicEnabled;

            if (newClip != null)
            {
                musicAudioSource.Play();

                if (duration > 0.05f)
                {
                    float halfDuration = duration * 0.5f;
                    float elapsed = 0f;
                    while (elapsed < halfDuration)
                    {
                        elapsed += Time.unscaledDeltaTime;
                        musicAudioSource.volume = Mathf.Lerp(0f, targetVol, elapsed / halfDuration);
                        yield return null;
                    }
                }
            }

            musicAudioSource.volume = targetVol;
            musicFadeCoroutine = null;
        }

        /// <summary>
        /// Loads all saved settings from PlayerPrefs. Defaults to enabled (true).
        /// </summary>
        public void LoadSettings()
        {
            SoundEnabled = PlayerPrefs.GetInt(PREFS_SOUND_ENABLED, 1) == 1;
            MusicEnabled = PlayerPrefs.GetInt(PREFS_MUSIC_ENABLED, 1) == 1;
            VibrationEnabled = PlayerPrefs.GetInt(PREFS_VIBRATION_ENABLED, 1) == 1;
            NotificationsEnabled = PlayerPrefs.GetInt(PREFS_NOTIFICATIONS_ENABLED, 1) == 1;
            ColorblindModeEnabled = PlayerPrefs.GetInt(PREFS_COLORBLIND_ENABLED, 0) == 1;
            SoundVolume = PlayerPrefs.GetFloat(PREFS_SOUND_VOLUME, 1f);
            MusicVolume = PlayerPrefs.GetFloat(PREFS_MUSIC_VOLUME, 1f);

            ApplySettings();
            OnSettingsChanged?.Invoke();
        }

        /// <summary>
        /// Saves all settings to PlayerPrefs.
        /// </summary>
        public void SaveSettings()
        {
            PlayerPrefs.SetInt(PREFS_SOUND_ENABLED, SoundEnabled ? 1 : 0);
            PlayerPrefs.SetInt(PREFS_MUSIC_ENABLED, MusicEnabled ? 1 : 0);
            PlayerPrefs.SetInt(PREFS_VIBRATION_ENABLED, VibrationEnabled ? 1 : 0);
            PlayerPrefs.SetInt(PREFS_NOTIFICATIONS_ENABLED, NotificationsEnabled ? 1 : 0);
            PlayerPrefs.SetInt(PREFS_COLORBLIND_ENABLED, ColorblindModeEnabled ? 1 : 0);
            PlayerPrefs.SetFloat(PREFS_SOUND_VOLUME, SoundVolume);
            PlayerPrefs.SetFloat(PREFS_MUSIC_VOLUME, MusicVolume);
            PlayerPrefs.Save();
        }

        public void SetSoundEnabled(bool enabled)
        {
            if (SoundEnabled == enabled) return;
            SoundEnabled = enabled;
            SaveSettings();
            ApplySettings();
            OnSettingsChanged?.Invoke();
            if (enabled) PlayClickSound();
        }

        public void SetMusicEnabled(bool enabled)
        {
            if (MusicEnabled == enabled) return;
            MusicEnabled = enabled;
            SaveSettings();
            ApplySettings();

            if (enabled && musicAudioSource != null && !musicAudioSource.isPlaying)
            {
                UpdateMusicForActiveScene();
            }

            OnSettingsChanged?.Invoke();
        }

        public void SetVibrationEnabled(bool enabled)
        {
            if (VibrationEnabled == enabled) return;
            VibrationEnabled = enabled;
            SaveSettings();
            OnSettingsChanged?.Invoke();
            if (enabled) TriggerHaptic();
        }

        public void SetNotificationsEnabled(bool enabled)
        {
            if (NotificationsEnabled == enabled) return;
            NotificationsEnabled = enabled;
            SaveSettings();
            OnSettingsChanged?.Invoke();
        }

        public void SetColorblindModeEnabled(bool enabled)
        {
            if (ColorblindModeEnabled == enabled) return;
            ColorblindModeEnabled = enabled;
            SaveSettings();
            OnColorblindModeChanged?.Invoke(enabled);
            OnSettingsChanged?.Invoke();
            if (enabled) TriggerHaptic();
        }

        public void SetSoundVolume(float volume)
        {
            SoundVolume = Mathf.Clamp01(volume);
            SaveSettings();
            ApplySettings();
            OnSettingsChanged?.Invoke();
        }

        public void SetMusicVolume(float volume)
        {
            MusicVolume = Mathf.Clamp01(volume);
            SaveSettings();
            ApplySettings();
            OnSettingsChanged?.Invoke();
        }

        /// <summary>
        /// Applies settings in real-time (e.g. Master/SFX/Music audio volumes).
        /// </summary>
        public void ApplySettings()
        {
            if (musicAudioSource != null)
            {
                musicAudioSource.mute = !MusicEnabled;
                if (musicFadeCoroutine == null)
                {
                    musicAudioSource.volume = MusicVolume * baseMusicVolume;
                }

                if (MusicEnabled && !musicAudioSource.isPlaying && musicAudioSource.clip != null)
                {
                    musicAudioSource.Play();
                }
            }

            // If neither sound nor music is enabled, master listener is muted
            if (!SoundEnabled && !MusicEnabled)
            {
                AudioListener.volume = 0f;
            }
            else
            {
                AudioListener.volume = 1f;
            }
        }

        private float lastClickSoundTime = -1f;

        /// <summary>
        /// Plays standard UI click feedback sound if sound is enabled.
        /// </summary>
        public void PlayClickSound()
        {
            PlayClickSound(1f);
        }

        /// <summary>
        /// Plays standard UI click feedback sound if sound is enabled.
        /// </summary>
        public void PlayClickSound(float volumeMultiplier)
        {
            if (!SoundEnabled) return;
            if (Time.unscaledTime - lastClickSoundTime < 0.035f) return;
            lastClickSoundTime = Time.unscaledTime;

            EnsureAudioSources();
            if (uiAudioSource != null && buttonClickSound != null)
            {
                uiAudioSource.pitch = UnityEngine.Random.Range(0.97f, 1.03f);
                uiAudioSource.PlayOneShot(buttonClickSound, SoundVolume * volumeMultiplier);
            }
        }

        private float lastMoveHapticTime = 0f;

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject vibratorInstance;
        private static AndroidJavaClass vibrationEffectClass;
        private static bool vibratorInitialized = false;
        private static bool hasVibratorService = false;
        private static bool supportsAmplitudeControl = false;
        private static int androidApiLevel = 0;

        private static void EnsureVibrator()
        {
            if (vibratorInitialized) return;
            vibratorInitialized = true;

            try
            {
                using (var versionClass = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    androidApiLevel = versionClass.GetStatic<int>("SDK_INT");
                }

                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    if (activity != null)
                    {
                        vibratorInstance = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                        if (vibratorInstance != null)
                        {
                            hasVibratorService = vibratorInstance.Call<bool>("hasVibrator");
                            if (hasVibratorService && androidApiLevel >= 26)
                            {
                                try
                                {
                                    supportsAmplitudeControl = vibratorInstance.Call<bool>("hasAmplitudeControl");
                                }
                                catch
                                {
                                    supportsAmplitudeControl = false;
                                }
                            }
                        }
                    }
                }

                if (androidApiLevel >= 26)
                {
                    vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GameSettingsManager] Android vibrator init notice: " + ex.Message);
                hasVibratorService = false;
            }
        }

        private static void VibrateDevice(long milliseconds, int amplitude)
        {
            EnsureVibrator();

            if (!hasVibratorService || vibratorInstance == null)
            {
                Handheld.Vibrate();
                return;
            }

            try
            {
                if (androidApiLevel >= 26 && vibrationEffectClass != null)
                {
                    // -1 is DEFAULT_AMPLITUDE on Android, which drives the motor at maximum default power
                    int amp = supportsAmplitudeControl ? Mathf.Clamp(amplitude, 1, 255) : -1;
                    using (var effect = vibrationEffectClass.CallStatic<AndroidJavaObject>("createOneShot", milliseconds, amp))
                    {
                        vibratorInstance.Call("vibrate", effect);
                    }
                }
                else
                {
                    vibratorInstance.Call("vibrate", milliseconds);
                }
            }
            catch
            {
                try
                {
                    vibratorInstance.Call("vibrate", milliseconds);
                }
                catch
                {
                    Handheld.Vibrate();
                }
            }
        }

        private static void VibratePattern(long[] timings, int[] amplitudes = null)
        {
            EnsureVibrator();

            if (!hasVibratorService || vibratorInstance == null)
            {
                Handheld.Vibrate();
                return;
            }

            try
            {
                if (androidApiLevel >= 26 && vibrationEffectClass != null)
                {
                    if (supportsAmplitudeControl && amplitudes != null && amplitudes.Length == timings.Length)
                    {
                        using (var effect = vibrationEffectClass.CallStatic<AndroidJavaObject>("createWaveform", timings, amplitudes, -1))
                        {
                            vibratorInstance.Call("vibrate", effect);
                        }
                    }
                    else
                    {
                        using (var effect = vibrationEffectClass.CallStatic<AndroidJavaObject>("createWaveform", timings, -1))
                        {
                            vibratorInstance.Call("vibrate", effect);
                        }
                    }
                }
                else
                {
                    vibratorInstance.Call("vibrate", timings, -1);
                }
            }
            catch
            {
                Handheld.Vibrate();
            }
        }
#endif

        /// <summary>
        /// Triggers distinct, powerful haptic feedback when dragging/moving blocks onto a new grid cell (~10x boost).
        /// </summary>
        public void TriggerHapticMove()
        {
            if (!VibrationEnabled) return;
            if (Time.unscaledTime - lastMoveHapticTime < 0.08f) return;
            lastMoveHapticTime = Time.unscaledTime;

#if UNITY_EDITOR
            Debug.Log("<color=#33D17A><b>[Haptic]</b> 📳 Block Move Tick (90ms, Full Power)</color>");
#elif UNITY_ANDROID
            VibrateDevice(90, 255);
#elif UNITY_IOS
            Handheld.Vibrate();
#endif
        }

        /// <summary>
        /// Triggers massive, punchy double-buzz haptic feedback when a block successfully exits the level (~10x boost).
        /// </summary>
        public void TriggerHapticExit()
        {
            if (!VibrationEnabled) return;

#if UNITY_EDITOR
            Debug.Log("<color=#FFD700><b>[Haptic]</b> 🚪 Block Exited Level! (400ms Double Pulse)</color>");
#elif UNITY_ANDROID
            // Double-pulse waveform: 100ms vibration, 50ms pause, 250ms strong vibration
            VibratePattern(new long[] { 0, 100, 50, 250 }, new int[] { 0, 255, 0, 255 });
#elif UNITY_IOS
            Handheld.Vibrate();
#endif
        }

        /// <summary>
        /// Triggers distinct haptic pulse for UI switches and button clicks (~10x boost).
        /// </summary>
        public void TriggerHaptic()
        {
            if (!VibrationEnabled) return;

#if UNITY_EDITOR
            Debug.Log("<color=#33D17A><b>[Haptic]</b> ⚡ General Pulse (120ms, Full Power)</color>");
#elif UNITY_ANDROID
            VibrateDevice(120, 255);
#elif UNITY_IOS
            Handheld.Vibrate();
#endif
        }

        /// <summary>
        /// Resets level progress back to Level 1.
        /// </summary>
        public void ResetLevelProgress()
        {
            PlayerPrefs.DeleteKey(PREFS_LEVEL_INDEX);
            PlayerPrefs.Save();
            Debug.Log("<color=yellow><b>[GameSettingsManager]</b> Level progress reset to Level 1!</color>");

            // If LevelLoader is present in the scene, inform it
            LevelLoader loader = FindAnyObjectByType<LevelLoader>();
            if (loader != null)
            {
                loader.ResetProgress();
            }
        }

        #region Dynamic Button Audio Tracking

        private readonly HashSet<Button> registeredButtons = new HashSet<Button>();
        private Coroutine buttonWatchCoroutine;

        /// <summary>
        /// Starts background tracking of UI buttons for the current scene.
        /// In Menu scene: ANY button clicked plays Button_Click sound.
        /// In Game scene: ONLY level exit / pause / menu buttons play Button_Click sound.
        /// </summary>
        public void StartButtonAudioTracking(string sceneName = null)
        {
            if (buttonWatchCoroutine != null)
            {
                StopCoroutine(buttonWatchCoroutine);
                buttonWatchCoroutine = null;
            }

            registeredButtons.Clear();

            if (string.IsNullOrEmpty(sceneName))
            {
                sceneName = SceneManager.GetActiveScene().name;
            }

            if (gameObject.activeInHierarchy)
            {
                buttonWatchCoroutine = StartCoroutine(ButtonAudioWatchRoutine(sceneName));
            }
            else
            {
                bool isGameScene = sceneName != null && sceneName.IndexOf("Game", StringComparison.OrdinalIgnoreCase) >= 0;
                RegisterSceneButtons(isGameScene);
            }
        }

        private IEnumerator ButtonAudioWatchRoutine(string sceneName)
        {
            bool isGameScene = sceneName != null && sceneName.IndexOf("Game", StringComparison.OrdinalIgnoreCase) >= 0;
            var waitInterval = new WaitForSecondsRealtime(0.25f);

            // Immediate scan on scene load
            RegisterSceneButtons(isGameScene);

            while (true)
            {
                yield return waitInterval;
                RegisterSceneButtons(isGameScene);
            }
        }

        /// <summary>
        /// Scans the active scene hierarchy and binds Button_Click feedback.
        /// </summary>
        public void RegisterSceneButtons(bool isGameScene)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded) return;

            GameObject[] roots = activeScene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                GameObject root = roots[r];
                if (root == null) continue;

                Button[] buttons = root.GetComponentsInChildren<Button>(true);
                for (int b = 0; b < buttons.Length; b++)
                {
                    Button btn = buttons[b];
                    if (btn == null) continue;

                    if (registeredButtons.Add(btn))
                    {
                        if (!isGameScene)
                        {
                            // In Menu: ANY button plays click sound
                            btn.onClick.AddListener(PlayClickSound);
                            UIButtonAudioClick.AttachTo(btn.gameObject);
                        }
                        else
                        {
                            // In Game: ONLY exit / pause / menu buttons play click sound
                            if (IsGameExitButton(btn))
                            {
                                btn.onClick.AddListener(PlayClickSound);
                                UIButtonAudioClick.AttachTo(btn.gameObject);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Checks whether a button is an exit / pause / menu return button in Game.
        /// </summary>
        public static bool IsGameExitButton(Button btn)
        {
            if (btn == null) return false;
            string name = btn.gameObject.name;
            return name.IndexOf("Pause", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Exit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Menu", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Home", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Leave", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Quit", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        #endregion

        private void OnApplicationQuit()
        {
            isApplicationQuitting = true;
            SaveSettings();
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
