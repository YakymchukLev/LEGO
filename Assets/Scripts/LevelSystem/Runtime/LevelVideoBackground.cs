using System.Collections;
using UnityEngine;
using UnityEngine.Video;

namespace LegoPuzzle.Runtime
{
    public enum BackgroundFitMode
    {
        [Tooltip("Повністю заповнює екран зі збереженням пропорцій відео (без чорних смуг)")]
        AspectFill,
        [Tooltip("Розтягує відео точно під розмір екрана (можливе легке спотворення пропорцій)")]
        Stretch,
        [Tooltip("Вміщує все відео в межі екрана (можуть з'являтися смуги)")]
        AspectFit
    }

    [ExecuteAlways]
    public class LevelVideoBackground : MonoBehaviour
    {
        [Header("Конфігурація Відео")]
        [Tooltip("Відео-ролик для фону (BackGroundVideo.mp4)")]
        [SerializeField] private VideoClip videoClip;

        [Tooltip("Чи зациклювати відтворення відео")]
        [SerializeField] private bool isLooping = true;

        [Tooltip("Автоматично починати відтворення при старті")]
        [SerializeField] private bool playOnAwake = true;

        [Header("Звук Відео")]
        [Tooltip("Вимкнути звук у відео (фонове відео зазвичай беззвучне)")]
        [SerializeField] private bool muteAudio = true;

        [Range(0f, 1f)]
        [Tooltip("Гучність аудіодоріжки відео (якщо звук не вимкнено)")]
        [SerializeField] private float audioVolume = 0f;

        [Header("Вигляд та Масштабування")]
        [Tooltip("Режим підгонки під екран (AspectFill - заповнює екран без чорних смуг)")]
        [SerializeField] private BackgroundFitMode fitMode = BackgroundFitMode.AspectFill;

        [Tooltip("Колір/затемнення фону (дозволяє зробити відео трохи темнішим для кращого контрасту з блоками LEGO)")]
        [SerializeField] private Color tintColor = Color.white;

        [Tooltip("Плавна поява (Fade-in) відео при запуску, щоб не було ривка першого кадру")]
        [SerializeField] private bool fadeInOnStart = true;

        [SerializeField] private float fadeInDuration = 0.35f;

        [Header("Позиціонування")]
        [Tooltip("Камера, за якою слідує фон (якщо не вказано, використовується Camera.main)")]
        [SerializeField] private Camera targetCamera;

        [Tooltip("Відступ площини фону нижче рівня ігрового поля (1.5 = на 1.5 одиниці нижче/глибше за тайли і блоки)")]
        [SerializeField] private float depthBelowBoard = 1.5f;

        [Tooltip("Запас масштабування квада (1.05 = запас 5%, гарантує відсутність щілин по краях)")]
        [SerializeField] private float paddingMultiplier = 1.05f;

        private LevelLoader levelLoader;
        private VideoPlayer videoPlayer;
        private RenderTexture renderTexture;
        private GameObject quadObject;
        private MeshRenderer quadRenderer;
        private Material backgroundMaterial;
        private Coroutine fadeInCoroutine;
        private bool isInitialized = false;

        public VideoPlayer Player => videoPlayer;
        public RenderTexture TargetTexture => renderTexture;

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            SetupVideoBackground();
        }

        private void OnEnable()
        {
            if (!isInitialized)
            {
                SetupVideoBackground();
            }
            else if (videoPlayer != null && playOnAwake && Application.isPlaying)
            {
                videoPlayer.Play();
            }
        }

        private void Start()
        {
            if (Application.isPlaying && videoPlayer != null && playOnAwake)
            {
                StartPlayback();
            }
        }

        private void LateUpdate()
        {
            UpdateQuadTransform();
        }

        public void SetupVideoBackground()
        {
            EnsureTargetCamera();
            EnsureVideoClip();
            EnsureVideoPlayer();
            EnsureRenderTexture();
            EnsureBackgroundQuad();
            UpdateQuadTransform();

            isInitialized = true;
        }

        private void EnsureTargetCamera()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
        }

        private void EnsureVideoClip()
        {
#if UNITY_EDITOR
            if (videoClip == null)
            {
                videoClip = UnityEditor.AssetDatabase.LoadAssetAtPath<VideoClip>("Assets/Sprites/BackGroundVideo.mp4");
            }
#endif
        }

        private void EnsureVideoPlayer()
        {
            if (videoPlayer == null)
            {
                videoPlayer = GetComponent<VideoPlayer>();
                if (videoPlayer == null)
                {
                    videoPlayer = gameObject.AddComponent<VideoPlayer>();
                }
            }

            videoPlayer.playOnAwake = playOnAwake;
            videoPlayer.isLooping = isLooping;
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.aspectRatio = VideoAspectRatio.FitInside;

            if (videoClip != null)
            {
                videoPlayer.clip = videoClip;
            }

            if (muteAudio)
            {
                videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
            }
            else
            {
                videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
                videoPlayer.SetDirectAudioVolume(0, audioVolume);
            }
        }

        private void EnsureRenderTexture()
        {
            int width = 720;
            int height = 1280;

            if (videoClip != null && videoClip.width > 0 && videoClip.height > 0)
            {
                width = (int)videoClip.width;
                height = (int)videoClip.height;
            }

            if (renderTexture != null && (renderTexture.width != width || renderTexture.height != height))
            {
                renderTexture.Release();
                DestroyImmediate(renderTexture);
                renderTexture = null;
            }

            if (renderTexture == null)
            {
                renderTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
                {
                    name = "VideoBackground_RT",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                renderTexture.Create();
            }

            if (videoPlayer != null)
            {
                videoPlayer.targetTexture = renderTexture;
            }
        }

        private void EnsureBackgroundQuad()
        {
            Transform existingChild = transform.Find("VideoBackgroundQuad");
            if (existingChild != null)
            {
                quadObject = existingChild.gameObject;
            }

            if (quadObject == null)
            {
                quadObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quadObject.name = "VideoBackgroundQuad";
                quadObject.transform.SetParent(transform, false);

                // Обов'язково видаляємо колайдер, щоб він не блокував тапи та драг блоків
                Collider col = quadObject.GetComponent<Collider>();
                if (col != null)
                {
                    DestroyImmediate(col);
                }
            }

            quadRenderer = quadObject.GetComponent<MeshRenderer>();

            if (backgroundMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Unlit/Texture");
                if (shader == null) shader = Shader.Find("Sprites/Default");

                backgroundMaterial = new Material(shader)
                {
                    name = "VideoBackground_Material"
                };
            }

            backgroundMaterial.mainTexture = renderTexture;

            // Встановлюємо чергу Background (1000) — малюється найпершим, перед будь-якими 3D-тайлами та блоками (Geometry 2000)
            backgroundMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Background;
            backgroundMaterial.SetFloat("_Surface", 0f); // Opaque
            backgroundMaterial.SetFloat("_ZWrite", 1f);  // Запис у Z-Buffer
            backgroundMaterial.SetInt("_Cull", 0);      // Cull Off (двосторонній)

            Color initialColor = (fadeInOnStart && Application.isPlaying) ? Color.black : tintColor;
            backgroundMaterial.color = initialColor;

            if (quadRenderer != null)
            {
                quadRenderer.sharedMaterial = backgroundMaterial;
                quadRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                quadRenderer.receiveShadows = false;
                quadRenderer.sortingOrder = -1000;
            }
        }

        public void StartPlayback()
        {
            if (videoPlayer == null) return;

            if (!videoPlayer.isPlaying)
            {
                videoPlayer.Play();
            }

            if (fadeInOnStart && gameObject.activeInHierarchy)
            {
                if (fadeInCoroutine != null) StopCoroutine(fadeInCoroutine);
                fadeInCoroutine = StartCoroutine(FadeInRoutine());
            }
        }

        private IEnumerator FadeInRoutine()
        {
            if (backgroundMaterial == null) yield break;

            // Чекаємо першого кадру від плеєра
            while (videoPlayer != null && !videoPlayer.isPlaying)
            {
                yield return null;
            }

            float elapsed = 0f;
            Color startColor = Color.black;

            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fadeInDuration);
                if (backgroundMaterial != null)
                {
                    backgroundMaterial.color = Color.Lerp(startColor, tintColor, t);
                }
                yield return null;
            }

            if (backgroundMaterial != null)
            {
                backgroundMaterial.color = tintColor;
            }
        }

        private float GetBackgroundPlaneY()
        {
            if (levelLoader == null)
            {
                levelLoader = GetComponent<LevelLoader>();
                if (levelLoader == null)
                {
                    levelLoader = FindAnyObjectByType<LevelLoader>();
                }
            }

            float boardY = (levelLoader != null) ? levelLoader.BoardBaseY : transform.position.y;
            return boardY - Mathf.Abs(depthBelowBoard);
        }

        public void UpdateQuadTransform()
        {
            if (quadObject == null) return;
            EnsureTargetCamera();
            if (targetCamera == null) return;

            Vector3 camPos = targetCamera.transform.position;
            float quadY = GetBackgroundPlaneY();

            float visibleHeight;
            float visibleWidth;
            float aspect = (targetCamera.aspect > 0.01f) ? targetCamera.aspect : ((float)Screen.width / Mathf.Max(1, Screen.height));

            if (targetCamera.orthographic)
            {
                visibleHeight = targetCamera.orthographicSize * 2f;
                visibleWidth = visibleHeight * aspect;
            }
            else
            {
                float dist = Mathf.Max(0.1f, camPos.y - quadY);
                float fovRad = targetCamera.fieldOfView * Mathf.Deg2Rad;
                visibleHeight = 2f * dist * Mathf.Tan(fovRad * 0.5f);
                visibleWidth = visibleHeight * aspect;
            }

            float videoW = (videoClip != null && videoClip.width > 0) ? videoClip.width : 720f;
            float videoH = (videoClip != null && videoClip.height > 0) ? videoClip.height : 1280f;
            float videoAspect = videoW / videoH;

            float quadWidth;
            float quadHeight;

            switch (fitMode)
            {
                case BackgroundFitMode.Stretch:
                    quadWidth = visibleWidth;
                    quadHeight = visibleHeight;
                    break;

                case BackgroundFitMode.AspectFit:
                    if (aspect > videoAspect)
                    {
                        quadHeight = visibleHeight;
                        quadWidth = visibleHeight * videoAspect;
                    }
                    else
                    {
                        quadWidth = visibleWidth;
                        quadHeight = visibleWidth / videoAspect;
                    }
                    break;

                case BackgroundFitMode.AspectFill:
                default:
                    if (aspect > videoAspect)
                    {
                        // Екран ширший за відео: підганяємо по ширині, надлишок висоти обрізається
                        quadWidth = visibleWidth;
                        quadHeight = visibleWidth / videoAspect;
                    }
                    else
                    {
                        // Екран вужчий за відео (портретний): підганяємо по висоті
                        quadHeight = visibleHeight;
                        quadWidth = visibleHeight * videoAspect;
                    }
                    break;
            }

            quadWidth *= paddingMultiplier;
            quadHeight *= paddingMultiplier;

            quadObject.transform.position = new Vector3(camPos.x, quadY, camPos.z);
            quadObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            quadObject.transform.localScale = new Vector3(quadWidth, quadHeight, 1f);
        }

        public void SetClip(VideoClip newClip)
        {
            videoClip = newClip;
            if (videoPlayer != null)
            {
                videoPlayer.clip = newClip;
            }
            EnsureRenderTexture();
            if (backgroundMaterial != null)
            {
                backgroundMaterial.mainTexture = renderTexture;
            }
            UpdateQuadTransform();
            if (Application.isPlaying && playOnAwake)
            {
                StartPlayback();
            }
        }

        public void SetTintColor(Color color)
        {
            tintColor = color;
            if (backgroundMaterial != null)
            {
                backgroundMaterial.color = tintColor;
            }
        }

        public void SetFitMode(BackgroundFitMode mode)
        {
            fitMode = mode;
            UpdateQuadTransform();
        }

        private void OnDestroy()
        {
            if (renderTexture != null)
            {
                renderTexture.Release();
                DestroyImmediate(renderTexture);
                renderTexture = null;
            }

            if (backgroundMaterial != null)
            {
                DestroyImmediate(backgroundMaterial);
                backgroundMaterial = null;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (videoPlayer != null)
            {
                videoPlayer.isLooping = isLooping;
                if (videoClip != null) videoPlayer.clip = videoClip;
            }

            if (backgroundMaterial != null)
            {
                backgroundMaterial.color = tintColor;
            }

            UpdateQuadTransform();
        }
#endif
    }
}
