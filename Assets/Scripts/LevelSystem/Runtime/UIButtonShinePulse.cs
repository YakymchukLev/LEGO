using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace LegoPuzzle.Runtime
{
    /// <summary>
    /// Adds visual polish to important CTA buttons:
    /// 1. Gentle rhythmic breathing pulse (e.g. 1.0x -> 1.04x) to attract attention.
    /// 2. Glassy shine sweep (light beam passing diagonally across the button).
    /// Works seamlessly with UIButtonPressEffect without interfering with press interactions.
    /// </summary>
    public class UIButtonShinePulse : MonoBehaviour
    {
        [Header("Breathing Pulse")]
        [Tooltip("Enable subtle rhythmic breathing pulse")]
        [SerializeField] private bool enablePulse = true;

        [Tooltip("Scale multiplier at peak of pulse (e.g. 1.04 = +4%)")]
        [SerializeField] private float pulseScale = 1.04f;

        [Tooltip("Speed of the breathing cycle in cycles per second")]
        [SerializeField] private float pulseSpeed = 1.2f;

        [Header("Shine Sweep")]
        [Tooltip("Enable periodic diagonal shine sweep across the button")]
        [SerializeField] private bool enableShine = true;

        [Tooltip("Delay between consecutive shine sweeps in seconds")]
        [SerializeField] private float shineInterval = 2.4f;

        [Tooltip("Duration of a single shine sweep across the button in seconds")]
        [SerializeField] private float shineDuration = 0.55f;

        [Tooltip("Angle of the shine beam in degrees")]
        [SerializeField] private float shineAngle = -22f;

        [Tooltip("Opacity of the shine effect (0 to 1)")]
        [SerializeField] private float shineAlpha = 0.38f;

        private RectTransform buttonRect;
        private RectTransform shineContainer;
        private RectTransform shineBeamMain;
        private RectTransform shineBeamGlow;
        private Image shineMainImage;
        private Image shineGlowImage;

        private Vector3 baseScale = Vector3.one;
        private bool isInitialized = false;
        private UIButtonPressEffect pressEffect;
        private Coroutine shineCoroutine;
        private float pulsePhase = 0f;

        private void Awake()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (isInitialized) return;

            buttonRect = GetComponent<RectTransform>();
            pressEffect = GetComponent<UIButtonPressEffect>();

            if (pressEffect != null && pressEffect.OriginalScale.sqrMagnitude > 0.001f)
            {
                baseScale = pressEffect.OriginalScale;
            }
            else if (transform.localScale.sqrMagnitude > 0.001f)
            {
                baseScale = transform.localScale;
            }

            SetupShineObjects();
            isInitialized = true;
        }

        private void SetupShineObjects()
        {
            if (!enableShine || buttonRect == null) return;

            // Ensure button has RectMask2D so shine stays cleanly inside button shape
            var mask = GetComponent<RectMask2D>();
            if (mask == null)
            {
                // If there's no Mask or RectMask2D, add RectMask2D
                var legacyMask = GetComponent<Mask>();
                if (legacyMask == null)
                {
                    gameObject.AddComponent<RectMask2D>();
                }
            }

            // Create shine container if not already present
            Transform existingContainer = transform.Find("__ShineContainer");
            if (existingContainer != null)
            {
                shineContainer = existingContainer.GetComponent<RectTransform>();
            }
            else
            {
                GameObject containerObj = new GameObject("__ShineContainer");
                containerObj.transform.SetParent(transform, false);
                shineContainer = containerObj.AddComponent<RectTransform>();
                shineContainer.anchorMin = Vector2.zero;
                shineContainer.anchorMax = Vector2.one;
                shineContainer.offsetMin = Vector2.zero;
                shineContainer.offsetMax = Vector2.zero;
                shineContainer.SetAsLastSibling();
            }

            // Create main crisp beam
            Transform existingMain = shineContainer.Find("MainBeam");
            if (existingMain != null)
            {
                shineBeamMain = existingMain.GetComponent<RectTransform>();
                shineMainImage = existingMain.GetComponent<Image>();
            }
            else
            {
                GameObject mainObj = new GameObject("MainBeam");
                mainObj.transform.SetParent(shineContainer, false);
                shineBeamMain = mainObj.AddComponent<RectTransform>();
                shineBeamMain.sizeDelta = new Vector2(24f, 800f);
                shineBeamMain.localRotation = Quaternion.Euler(0f, 0f, shineAngle);

                shineMainImage = mainObj.AddComponent<Image>();
                shineMainImage.color = new Color(1f, 1f, 1f, shineAlpha);
                shineMainImage.raycastTarget = false;
            }

            // Create wider softer glow beam alongside main beam
            Transform existingGlow = shineContainer.Find("GlowBeam");
            if (existingGlow != null)
            {
                shineBeamGlow = existingGlow.GetComponent<RectTransform>();
                shineGlowImage = existingGlow.GetComponent<Image>();
            }
            else
            {
                GameObject glowObj = new GameObject("GlowBeam");
                glowObj.transform.SetParent(shineContainer, false);
                shineBeamGlow = glowObj.AddComponent<RectTransform>();
                shineBeamGlow.sizeDelta = new Vector2(60f, 800f);
                shineBeamGlow.localRotation = Quaternion.Euler(0f, 0f, shineAngle);

                shineGlowImage = glowObj.AddComponent<Image>();
                shineGlowImage.color = new Color(1f, 1f, 1f, shineAlpha * 0.45f);
                shineGlowImage.raycastTarget = false;
            }

            HideShine();
        }

        private void HideShine()
        {
            if (shineContainer != null)
            {
                shineContainer.gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            Initialize();
            pulsePhase = 0f;

            if (enableShine)
            {
                if (shineCoroutine != null) StopCoroutine(shineCoroutine);
                shineCoroutine = StartCoroutine(ShineLoopRoutine());
            }
        }

        private void OnDisable()
        {
            if (shineCoroutine != null)
            {
                StopCoroutine(shineCoroutine);
                shineCoroutine = null;
            }

            HideShine();

            if (isInitialized && (pressEffect == null || !pressEffect.IsPressed))
            {
                transform.localScale = baseScale;
            }
        }

        private void Update()
        {
            if (!enablePulse || buttonRect == null) return;

            // If user is currently pressing down the button, let UIButtonPressEffect handle the scale
            if (pressEffect != null && pressEffect.IsPressed)
            {
                return;
            }

            // Smooth gentle sine pulse (breathing)
            pulsePhase += Time.unscaledDeltaTime * pulseSpeed;
            float wave = (Mathf.Sin(pulsePhase * Mathf.PI * 2f) + 1f) * 0.5f; // 0..1
            // Smooth ease in out sine
            wave = Mathf.SmoothStep(0f, 1f, wave);

            float currentScaleMultiplier = Mathf.Lerp(1f, pulseScale, wave);
            transform.localScale = baseScale * currentScaleMultiplier;
        }

        private IEnumerator ShineLoopRoutine()
        {
            // Initial delay before first shine so the entrance animation can finish cleanly
            yield return new WaitForSecondsRealtime(0.6f);

            while (true)
            {
                if (buttonRect != null && shineContainer != null)
                {
                    yield return StartCoroutine(PlaySingleShineSweep());
                }

                yield return new WaitForSecondsRealtime(shineInterval);
            }
        }

        private IEnumerator PlaySingleShineSweep()
        {
            if (buttonRect == null || shineContainer == null || shineBeamMain == null) yield break;

            float width = buttonRect.rect.width > 20f ? buttonRect.rect.width : 300f;
            float startX = -width * 0.8f - 80f;
            float endX = width * 0.8f + 80f;

            shineContainer.gameObject.SetActive(true);

            float elapsed = 0f;
            while (elapsed < shineDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / shineDuration);

                // Smooth quad ease in-out
                float progress = t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
                float currentX = Mathf.Lerp(startX, endX, progress);

                Vector2 pos = new Vector2(currentX, 0f);
                if (shineBeamMain != null) shineBeamMain.anchoredPosition = pos;
                if (shineBeamGlow != null) shineBeamGlow.anchoredPosition = pos;

                yield return null;
            }

            HideShine();
        }

        /// <summary>
        /// Attaches or configures UIButtonShinePulse on the target button GameObject.
        /// </summary>
        public static UIButtonShinePulse AttachTo(GameObject buttonObj, float pulseMax = 1.04f, float shineDelay = 2.4f)
        {
            if (buttonObj == null) return null;
            var juice = buttonObj.GetComponent<UIButtonShinePulse>();
            if (juice == null)
            {
                juice = buttonObj.AddComponent<UIButtonShinePulse>();
            }
            juice.pulseScale = pulseMax;
            juice.shineInterval = shineDelay;
            return juice;
        }
    }
}
