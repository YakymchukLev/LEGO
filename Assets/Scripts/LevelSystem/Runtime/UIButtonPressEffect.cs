using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LegoPuzzle.Runtime
{
    /// <summary>
    /// Smoothly animates UI buttons when pressed (Pointer Down) and released (Pointer Up / Exit).
    /// Supports uniform enlargement as well as juicy horizontal squash & stretch jelly wobble.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIButtonPressEffect : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [Header("Standard Press Effect")]
        [Tooltip("Scale multiplier when pressed (e.g. 1.08 = 8% larger, 1.10 = 10% larger)")]
        [SerializeField] private float pressedScaleMultiplier = 1.08f;

        [Tooltip("Duration of the standard scale animation in seconds")]
        [SerializeField] private float animationDuration = 0.08f;

        [Header("Horizontal Jelly Animation")]
        [Tooltip("Enables juicy horizontal squash & stretch jelly wobble when pressed and released")]
        [SerializeField] private bool useHorizontalJelly = false;

        [Tooltip("Horizontal stretch multiplier when pressed (e.g. 1.25 = 25% wider)")]
        [Range(1.0f, 2.0f)]
        [SerializeField] private float horizontalStretchMultiplier = 1.25f;

        [Tooltip("Vertical squash multiplier when pressed (e.g. 0.80 = 20% squashed)")]
        [Range(0.4f, 1.0f)]
        [SerializeField] private float verticalSquashMultiplier = 0.80f;

        [Tooltip("Time to reach max squash/stretch on press")]
        [SerializeField] private float jellyPressDuration = 0.065f;

        [Tooltip("Duration of the release jelly spring oscillation")]
        [SerializeField] private float jellyWobbleDuration = 0.45f;

        [Tooltip("Frequency of jelly oscillation")]
        [SerializeField] private float wobbleFrequency = 22f;

        [Tooltip("Exponential damping factor for jelly spring")]
        [SerializeField] private float wobbleDamping = 6.5f;

        private Vector3 originalScale = Vector3.one;
        private Coroutine scaleCoroutine;
        private bool isPressed = false;
        private bool isInitialized = false;
        private Selectable selectable;

        public bool IsPressed => isPressed;
        public Vector3 OriginalScale => originalScale;
        public bool UseHorizontalJelly
        {
            get => useHorizontalJelly;
            set => useHorizontalJelly = value;
        }

        private void Awake()
        {
            selectable = GetComponent<Selectable>();
            InitializeOriginalScale();
        }

        private void InitializeOriginalScale()
        {
            if (!isInitialized && transform.localScale.sqrMagnitude > 0.001f)
            {
                originalScale = transform.localScale;
                isInitialized = true;
            }
        }

        private void OnEnable()
        {
            InitializeOriginalScale();
            if (isInitialized)
            {
                transform.localScale = originalScale;
            }
            isPressed = false;
        }

        private void OnDisable()
        {
            if (scaleCoroutine != null)
            {
                StopCoroutine(scaleCoroutine);
                scaleCoroutine = null;
            }
            if (isInitialized)
            {
                transform.localScale = originalScale;
            }
            isPressed = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (selectable == null) selectable = GetComponent<Selectable>();
            if (selectable != null && !selectable.IsInteractable()) return;

            InitializeOriginalScale();
            isPressed = true;

            if (useHorizontalJelly)
            {
                StartHorizontalJellyPress();
            }
            else
            {
                StartScaleAnimation(originalScale * pressedScaleMultiplier, animationDuration);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (isPressed)
            {
                isPressed = false;
                if (useHorizontalJelly)
                {
                    StartHorizontalJellyRelease();
                }
                else
                {
                    StartScaleAnimation(originalScale, animationDuration);
                }
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (isPressed)
            {
                isPressed = false;
                if (useHorizontalJelly)
                {
                    StartHorizontalJellyRelease();
                }
                else
                {
                    StartScaleAnimation(originalScale, animationDuration);
                }
            }
        }

        private void StartScaleAnimation(Vector3 targetScale, float duration)
        {
            if (scaleCoroutine != null)
            {
                StopCoroutine(scaleCoroutine);
            }

            if (gameObject.activeInHierarchy)
            {
                scaleCoroutine = StartCoroutine(AnimateScaleRoutine(targetScale, duration));
            }
            else
            {
                transform.localScale = targetScale;
            }
        }

        private IEnumerator AnimateScaleRoutine(Vector3 targetScale, float duration)
        {
            Vector3 startScale = transform.localScale;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // Smooth sine ease out
                t = Mathf.Sin(t * Mathf.PI * 0.5f);
                transform.localScale = Vector3.LerpUnclamped(startScale, targetScale, t);
                yield return null;
            }

            transform.localScale = targetScale;
            scaleCoroutine = null;
        }

        private void StartHorizontalJellyPress()
        {
            if (scaleCoroutine != null)
            {
                StopCoroutine(scaleCoroutine);
            }

            Vector3 targetScale = new Vector3(
                originalScale.x * horizontalStretchMultiplier,
                originalScale.y * verticalSquashMultiplier,
                originalScale.z
            );

            if (gameObject.activeInHierarchy)
            {
                scaleCoroutine = StartCoroutine(AnimateJellyPressRoutine(targetScale));
            }
            else
            {
                transform.localScale = targetScale;
            }
        }

        private IEnumerator AnimateJellyPressRoutine(Vector3 targetScale)
        {
            Vector3 startScale = transform.localScale;
            float elapsed = 0f;

            while (elapsed < jellyPressDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / jellyPressDuration);
                // Punchy cubic ease-out
                t = 1f - Mathf.Pow(1f - t, 3f);
                transform.localScale = Vector3.LerpUnclamped(startScale, targetScale, t);
                yield return null;
            }

            transform.localScale = targetScale;
            scaleCoroutine = null;
        }

        private void StartHorizontalJellyRelease()
        {
            if (scaleCoroutine != null)
            {
                StopCoroutine(scaleCoroutine);
            }

            if (gameObject.activeInHierarchy)
            {
                scaleCoroutine = StartCoroutine(AnimateJellyReleaseRoutine());
            }
            else
            {
                transform.localScale = originalScale;
            }
        }

        private IEnumerator AnimateJellyReleaseRoutine()
        {
            Vector3 currentScale = transform.localScale;
            float startOffsetX = currentScale.x - originalScale.x;
            float maxOffsetX = originalScale.x * (horizontalStretchMultiplier - 1f);

            // If the user tapped very briefly, ensure full punchy wobble impulse
            if (Mathf.Abs(startOffsetX) < maxOffsetX * 0.7f)
            {
                startOffsetX = maxOffsetX;
            }

            float maxOffsetY = originalScale.y * (1f - verticalSquashMultiplier);
            float elapsed = 0f;

            while (elapsed < jellyWobbleDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed;
                float decay = Mathf.Exp(-wobbleDamping * t);
                float cos = Mathf.Cos(wobbleFrequency * t);

                float deltaX = startOffsetX * decay * cos;
                float deltaY = maxOffsetY * decay * cos;

                transform.localScale = new Vector3(
                    originalScale.x + deltaX,
                    originalScale.y - deltaY,
                    originalScale.z
                );

                yield return null;
            }

            transform.localScale = originalScale;
            scaleCoroutine = null;
        }

        /// <summary>
        /// Explicitly triggers a horizontal jelly wobble animation on this button.
        /// </summary>
        public void TriggerHorizontalJelly()
        {
            InitializeOriginalScale();
            StartHorizontalJellyRelease();
        }

        /// <summary>
        /// Attaches standard UIButtonPressEffect to a single GameObject (or Button).
        /// </summary>
        public static UIButtonPressEffect AttachTo(GameObject target, float scaleMultiplier = 1.08f, float duration = 0.08f)
        {
            if (target == null) return null;
            var effect = target.GetComponent<UIButtonPressEffect>();
            if (effect == null)
            {
                effect = target.AddComponent<UIButtonPressEffect>();
            }
            effect.pressedScaleMultiplier = scaleMultiplier;
            effect.animationDuration = duration;
            return effect;
        }

        /// <summary>
        /// Attaches and configures horizontal jelly squash & stretch on a target GameObject.
        /// </summary>
        public static UIButtonPressEffect AttachHorizontalJelly(
            GameObject target,
            float stretchX = 1.25f,
            float squashY = 0.80f,
            float wobbleDuration = 0.45f)
        {
            if (target == null) return null;
            var effect = target.GetComponent<UIButtonPressEffect>();
            if (effect == null)
            {
                effect = target.AddComponent<UIButtonPressEffect>();
            }
            effect.useHorizontalJelly = true;
            effect.horizontalStretchMultiplier = stretchX;
            effect.verticalSquashMultiplier = squashY;
            effect.jellyWobbleDuration = wobbleDuration;
            return effect;
        }

        /// <summary>
        /// Attaches standard UIButtonPressEffect to all Button components within the given root GameObject.
        /// </summary>
        public static void AttachToAllIn(GameObject root, float scaleMultiplier = 1.08f, float duration = 0.08f)
        {
            if (root == null) return;
            var buttons = root.GetComponentsInChildren<Button>(true);
            foreach (var btn in buttons)
            {
                if (btn == null) continue;
                AttachTo(btn.gameObject, scaleMultiplier, duration);
            }
        }
    }
}
