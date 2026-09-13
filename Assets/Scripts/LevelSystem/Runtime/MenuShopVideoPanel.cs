using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace LegoPuzzle.Runtime
{
    /// <summary>
    /// Full-screen Video Panel in the Menu scene.
    /// Plays BackGroundVideo.mp4 across the entire screen (like in Game scene).
    /// - Opened via the Shop kiosk button on DownPanel.
    /// - Closed via the 'Main Menu Button' (home icon) on DownPanel.
    /// - Features juicy, smooth cinematic transitions (scale & fade).
    /// - No offer popups or clutter: clean full-screen video experience.
    /// </summary>
    [ExecuteAlways]
    public class MenuShopVideoPanel : MonoBehaviour
    {
        public static MenuShopVideoPanel Instance { get; private set; }

        [Header("Video Configuration")]
        [Tooltip("Video clip to play full-screen (BackGroundVideo.mp4)")]
        [SerializeField] private VideoClip videoClip;

        [Tooltip("Mute audio from the background video")]
        [SerializeField] private bool muteAudio = true;

        [Range(0f, 1f)]
        [Tooltip("Audio volume if not muted")]
        [SerializeField] private float audioVolume = 0f;

        [Header("UI Element References")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RawImage videoRawImage;
        [SerializeField] private AspectRatioFitter aspectRatioFitter;
        [SerializeField] private VideoPlayer videoPlayer;

        [Header("Buttons")]
        [Tooltip("Shop kiosk button that opens this panel (auto-detected in DownPanel if null)")]
        [SerializeField] private Button shopTriggerButton;

        [Tooltip("Main Menu Home button that closes this panel (auto-detected in DownPanel if null)")]
        [SerializeField] private Button mainMenuCloseButton;

        [Header("Animation Settings")]
        [SerializeField] private float openDuration = 0.32f;
        [SerializeField] private float closeDuration = 0.22f;

        private RenderTexture videoRenderTexture;
        private Coroutine animateCoroutine;
        private Coroutine videoFadeCoroutine;
        private bool isAnimating = false;

        public bool IsOpen => gameObject.activeSelf && (canvasGroup == null || canvasGroup.alpha > 0.05f);

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
            }

            EnsureUIHierarchy();
            HookButtons();

            if (Application.isPlaying)
            {
                if (canvasGroup != null) canvasGroup.alpha = 0f;
                gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                Instance = this;
            }

            EnsureVideoClip();
            EnsureRenderTexture();
            HookButtons();
        }

        private void Start()
        {
            EnsureUIHierarchy();
            HookButtons();
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            isAnimating = false;

            if (videoPlayer != null && videoPlayer.isPlaying)
            {
                videoPlayer.Pause();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            ReleaseRenderTexture();
        }

        /// <summary>
        /// Automatically hooks the Shop kiosk button to Open and the Main Menu home button to Close.
        /// </summary>
        public void HookButtons()
        {
            Transform downPanel = FindDownPanel();

            if (downPanel != null)
            {
                // 1. Shop Kiosk Button (named "Button" in DownPanel)
                if (shopTriggerButton == null)
                {
                    Transform btn = downPanel.Find("Button");
                    if (btn != null) shopTriggerButton = btn.GetComponent<Button>();
                }

                // 2. Main Menu Button (named "Main Menu Button" in DownPanel)
                if (mainMenuCloseButton == null)
                {
                    Transform homeBtn = downPanel.Find("Main Menu Button");
                    if (homeBtn != null) mainMenuCloseButton = homeBtn.GetComponent<Button>();
                }
            }

            if (shopTriggerButton != null)
            {
                shopTriggerButton.onClick.RemoveListener(OpenPanel);
                shopTriggerButton.onClick.AddListener(OpenPanel);

                if (shopTriggerButton.GetComponent<UIButtonPressEffect>() == null)
                {
                    UIButtonPressEffect.AttachTo(shopTriggerButton.gameObject);
                }
            }

            if (mainMenuCloseButton != null)
            {
                mainMenuCloseButton.onClick.RemoveListener(ClosePanel);
                mainMenuCloseButton.onClick.AddListener(ClosePanel);

                if (mainMenuCloseButton.GetComponent<UIButtonPressEffect>() == null)
                {
                    UIButtonPressEffect.AttachTo(mainMenuCloseButton.gameObject);
                }
            }
        }

        private Transform FindDownPanel()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                Transform dp = canvas.transform.Find("DownPanel");
                if (dp != null) return dp;
            }

            var anyCanvas = FindAnyObjectByType<Canvas>();
            if (anyCanvas != null)
            {
                Transform dp = anyCanvas.transform.Find("DownPanel");
                if (dp != null) return dp;
            }

            return null;
        }

        /// <summary>
        /// Opens the full-screen video panel with smooth cinematic zoom & fade.
        /// </summary>
        public void OpenPanel()
        {
            if (isAnimating && IsOpen) return;

            gameObject.SetActive(true);
            EnsureRenderTexture();
            StartVideoPlayback();

            if (animateCoroutine != null) StopCoroutine(animateCoroutine);
            animateCoroutine = StartCoroutine(AnimateOpenRoutine());
        }

        /// <summary>
        /// Closes the full-screen video panel and returns to the main menu.
        /// </summary>
        public void ClosePanel()
        {
            if (!gameObject.activeSelf) return;

            if (animateCoroutine != null) StopCoroutine(animateCoroutine);
            animateCoroutine = StartCoroutine(AnimateCloseRoutine());
        }

        private IEnumerator AnimateOpenRoutine()
        {
            isAnimating = true;

            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            RectTransform rt = GetComponent<RectTransform>();

            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;

            if (rt != null) rt.localScale = new Vector3(1.06f, 1.06f, 1f);

            float elapsed = 0f;

            while (elapsed < openDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(elapsed / openDuration);

                // Smooth elastic settle (1.06 -> 1.0)
                float ease = 1f - Mathf.Pow(1f - p, 3f);
                float scale = Mathf.Lerp(1.06f, 1f, ease);

                canvasGroup.alpha = Mathf.Lerp(0f, 1f, ease);
                if (rt != null) rt.localScale = new Vector3(scale, scale, 1f);

                yield return null;
            }

            canvasGroup.alpha = 1f;
            if (rt != null) rt.localScale = Vector3.one;

            isAnimating = false;
            animateCoroutine = null;
        }

        private IEnumerator AnimateCloseRoutine()
        {
            isAnimating = true;

            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            RectTransform rt = GetComponent<RectTransform>();

            float elapsed = 0f;

            while (elapsed < closeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(elapsed / closeDuration);

                // Smooth fade out & subtle shrink (1.0 -> 0.96)
                float ease = p * p;
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, ease);
                if (rt != null)
                {
                    float scale = Mathf.Lerp(1f, 0.96f, ease);
                    rt.localScale = new Vector3(scale, scale, 1f);
                }

                yield return null;
            }

            if (videoPlayer != null && videoPlayer.isPlaying)
            {
                videoPlayer.Pause();
            }

            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            if (rt != null) rt.localScale = Vector3.one;

            gameObject.SetActive(false);

            isAnimating = false;
            animateCoroutine = null;
        }

        #region Video Playback Handling

        private void EnsureVideoClip()
        {
            if (videoClip == null)
            {
#if UNITY_EDITOR
                videoClip = UnityEditor.AssetDatabase.LoadAssetAtPath<VideoClip>("Assets/Sprites/BackGroundVideo.mp4");
#endif
            }
        }

        private void EnsureRenderTexture()
        {
            int width = 1920;
            int height = 1080;

            if (videoClip != null && videoClip.width > 0 && videoClip.height > 0)
            {
                width = (int)videoClip.width;
                height = (int)videoClip.height;
            }

            if (videoRenderTexture == null || !videoRenderTexture.IsCreated() || videoRenderTexture.width != width || videoRenderTexture.height != height)
            {
                ReleaseRenderTexture();

                videoRenderTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
                {
                    name = "MenuShopFullscreenVideo_RT",
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
            if (videoPlayer == null) return;

            videoPlayer.clip = videoClip;
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.targetTexture = videoRenderTexture;
            videoPlayer.isLooping = true;
            videoPlayer.playOnAwake = false;

            if (muteAudio)
            {
                videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
            }
            else
            {
                videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
                videoPlayer.SetDirectAudioVolume(0, audioVolume);
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

        private IEnumerator VideoFadeInRoutine()
        {
            if (videoRawImage == null) yield break;

            videoRawImage.color = new Color(0f, 0f, 0f, 0f);

            float waitTimer = 0f;
            while (videoPlayer != null && !videoPlayer.isPlaying && waitTimer < 0.4f)
            {
                waitTimer += Time.unscaledDeltaTime;
                yield return null;
            }

            float elapsed = 0f;
            float duration = 0.20f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                if (videoRawImage != null)
                {
                    videoRawImage.color = Color.Lerp(new Color(0f, 0f, 0f, 0f), Color.white, t);
                }
                yield return null;
            }

            if (videoRawImage != null) videoRawImage.color = Color.white;
            videoFadeCoroutine = null;
        }

        #endregion

        #region UI Hierarchy Setup

        /// <summary>
        /// Sets up the full-screen video display and removes any unwanted child offer cards.
        /// </summary>
        public void EnsureUIHierarchy()
        {
            // Full-screen RectTransform
            RectTransform rootRt = GetComponent<RectTransform>();
            if (rootRt == null) rootRt = gameObject.AddComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;
            rootRt.pivot = new Vector2(0.5f, 0.5f);

            // CanvasGroup for transitions
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            // Remove any old card or offer popups if they exist
            Transform oldCard = transform.Find("DialogCard");
            if (oldCard != null)
            {
                if (Application.isPlaying) Destroy(oldCard.gameObject);
                else DestroyImmediate(oldCard.gameObject);
            }

            Transform oldBd = transform.Find("Backdrop");
            if (oldBd != null)
            {
                if (Application.isPlaying) Destroy(oldBd.gameObject);
                else DestroyImmediate(oldBd.gameObject);
            }

            // Create or hook the Fullscreen Video RawImage
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

            // AspectRatioFitter ensures the video fills the screen proportionally without distortion
            aspectRatioFitter = videoDisplayObj.GetComponent<AspectRatioFitter>();
            if (aspectRatioFitter == null) aspectRatioFitter = videoDisplayObj.AddComponent<AspectRatioFitter>();
            aspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;

            EnsureVideoClip();
            if (videoClip != null && videoClip.width > 0 && videoClip.height > 0)
            {
                aspectRatioFitter.aspectRatio = (float)videoClip.width / (float)videoClip.height;
            }
            else
            {
                aspectRatioFitter.aspectRatio = 16f / 9f;
            }

            // VideoPlayer
            if (videoPlayer == null)
            {
                videoPlayer = GetComponent<VideoPlayer>();
                if (videoPlayer == null) videoPlayer = gameObject.AddComponent<VideoPlayer>();
            }

            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = true;
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.clip = videoClip;
        }

        #endregion
    }
}
