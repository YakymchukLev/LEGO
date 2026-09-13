using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace LegoPuzzle.Runtime
{
    /// <summary>
    /// Smoothly slides the panel in from the bottom of the screen to its designated anchored position.
    /// Uses an overshoot curve for a punchy, polished mobile game popup feel.
    /// </summary>
    public class UIPanelSlideIn : MonoBehaviour
    {
        [Tooltip("The RectTransform to animate. If null, automatically finds 'DialogPanel' child or uses self.")]
        [SerializeField] private RectTransform targetRect;

        [Tooltip("Duration of the slide-in animation in seconds")]
        [SerializeField] private float duration = 0.42f;

        [Tooltip("If true, uses a subtle overshoot ease-out for elastic feel")]
        [SerializeField] private bool useOvershoot = true;

        [Tooltip("Overshoot intensity (e.g. 1.05 = ~2.5% overshoot)")]
        [SerializeField] private float overshootAmount = 1.05f;

        [Tooltip("Optional dim background image that fades in alongside the slide animation")]
        [SerializeField] private Image dimBackground;

        [Header("Impact Shake")]
        [Tooltip("If true, performs a punchy micro-shake when landing at the target position (great for defeat panels)")]
        [SerializeField] private bool enableImpactShake = false;
        [SerializeField] private float impactShakeDuration = 0.22f;
        [SerializeField] private float impactShakeIntensity = 14f;

        private Vector2 targetAnchoredPosition;
        private bool isInitialized = false;
        private Coroutine slideCoroutine;
        private float originalDimAlpha = 0.75f;
        private bool isAnimating = false;

        public bool IsAnimating => isAnimating;

        private void Awake()
        {
            InitTarget();
        }

        private void InitTarget()
        {
            if (isInitialized) return;

            if (targetRect == null)
            {
                Transform dialogChild = transform.Find("DialogPanel");
                if (dialogChild != null && dialogChild.GetComponent<RectTransform>() != null)
                {
                    targetRect = dialogChild.GetComponent<RectTransform>();
                    dimBackground = GetComponent<Image>();
                }
                else
                {
                    targetRect = GetComponent<RectTransform>();
                }
            }

            if (dimBackground == null && targetRect != null && targetRect.gameObject != gameObject)
            {
                dimBackground = GetComponent<Image>();
            }

            if (dimBackground != null)
            {
                originalDimAlpha = dimBackground.color.a;
                if (originalDimAlpha < 0.01f) originalDimAlpha = 0.75f;
            }

            if (targetRect != null)
            {
                targetAnchoredPosition = targetRect.anchoredPosition;
                isInitialized = true;
            }
        }

        private void OnEnable()
        {
            InitTarget();
            Play();
        }

        private void OnDisable()
        {
            if (slideCoroutine != null)
            {
                StopCoroutine(slideCoroutine);
                slideCoroutine = null;
            }

            if (isInitialized && targetRect != null)
            {
                targetRect.anchoredPosition = targetAnchoredPosition;
            }

            if (dimBackground != null)
            {
                Color c = dimBackground.color;
                c.a = originalDimAlpha;
                dimBackground.color = c;
            }

            isAnimating = false;
        }

        /// <summary>
        /// Starts the slide-in animation from bottom to the target anchored position.
        /// </summary>
        public void Play()
        {
            InitTarget();
            if (!gameObject.activeInHierarchy || targetRect == null) return;

            if (slideCoroutine != null)
            {
                StopCoroutine(slideCoroutine);
            }

            slideCoroutine = StartCoroutine(SlideInRoutine());
        }

        private Vector2 CalculateStartPosition()
        {
            InitTarget();
            if (targetRect == null) return Vector2.zero;

            RectTransform parentRt = targetRect.parent as RectTransform;
            float parentHeight = 1920f;
            float parentPivotY = 0.5f;

            if (parentRt != null && parentRt.rect.height > 50f)
            {
                parentHeight = parentRt.rect.height;
                parentPivotY = parentRt.pivot.y;
            }
            else
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                if (canvas != null)
                {
                    RectTransform canvasRt = canvas.GetComponent<RectTransform>();
                    if (canvasRt != null && canvasRt.rect.height > 50f)
                    {
                        parentHeight = canvasRt.rect.height;
                        parentPivotY = canvasRt.pivot.y;
                    }
                }
            }

            float panelHeight = targetRect.rect.height > 50f ? targetRect.rect.height : 1200f;
            float panelPivotY = targetRect.pivot.y;

            // Calculate distance needed so panel top is 80 units below canvas bottom
            float bottomOfParentRelative = -parentHeight * parentPivotY;
            float topOfPanelRelative = targetAnchoredPosition.y + panelHeight * (1f - panelPivotY);
            float deltaY = (topOfPanelRelative - bottomOfParentRelative) + 80f;

            if (deltaY < 200f)
            {
                deltaY = parentHeight + panelHeight * 0.5f;
            }

            return new Vector2(targetAnchoredPosition.x, targetAnchoredPosition.y - deltaY);
        }

        private IEnumerator SlideInRoutine()
        {
            isAnimating = true;
            Vector2 startPos = CalculateStartPosition();
            targetRect.anchoredPosition = startPos;

            if (dimBackground != null)
            {
                Color c = dimBackground.color;
                c.a = 0f;
                dimBackground.color = c;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                float progress;
                if (useOvershoot)
                {
                    float c1 = overshootAmount;
                    float c3 = c1 + 1f;
                    float t1 = t - 1f;
                    progress = 1f + c3 * t1 * t1 * t1 + c1 * t1 * t1;
                }
                else
                {
                    float t1 = 1f - t;
                    progress = 1f - t1 * t1 * t1;
                }

                targetRect.anchoredPosition = Vector2.LerpUnclamped(startPos, targetAnchoredPosition, progress);

                if (dimBackground != null)
                {
                    Color c = dimBackground.color;
                    c.a = Mathf.Lerp(0f, originalDimAlpha, Mathf.Clamp01(t * 1.5f));
                    dimBackground.color = c;
                }

                yield return null;
            }

            targetRect.anchoredPosition = targetAnchoredPosition;
            if (dimBackground != null)
            {
                Color c = dimBackground.color;
                c.a = originalDimAlpha;
                dimBackground.color = c;
            }

            if (enableImpactShake)
            {
                float shakeElapsed = 0f;
                while (shakeElapsed < impactShakeDuration)
                {
                    shakeElapsed += Time.unscaledDeltaTime;
                    float st = Mathf.Clamp01(shakeElapsed / impactShakeDuration);
                    float decay = 1f - st;
                    float offsetY = Mathf.Sin(st * Mathf.PI * 8f) * impactShakeIntensity * decay;
                    float offsetX = Mathf.Cos(st * Mathf.PI * 6f) * (impactShakeIntensity * 0.4f) * decay;
                    targetRect.anchoredPosition = targetAnchoredPosition + new Vector2(offsetX, offsetY);
                    yield return null;
                }
                targetRect.anchoredPosition = targetAnchoredPosition;
            }

            isAnimating = false;
            slideCoroutine = null;
        }

        public UIPanelSlideIn EnableImpactShake(bool enable, float intensity = 14f)
        {
            enableImpactShake = enable;
            impactShakeIntensity = intensity;
            return this;
        }

        /// <summary>
        /// Attaches UIPanelSlideIn to the specified panel GameObject and configures animation settings.
        /// </summary>
        public static UIPanelSlideIn AttachTo(GameObject panel, float duration = 0.42f, float overshoot = 1.05f)
        {
            if (panel == null) return null;
            var slide = panel.GetComponent<UIPanelSlideIn>();
            if (slide == null)
            {
                slide = panel.AddComponent<UIPanelSlideIn>();
            }
            slide.duration = duration;
            slide.overshootAmount = overshoot;
            return slide;
        }
    }
}
