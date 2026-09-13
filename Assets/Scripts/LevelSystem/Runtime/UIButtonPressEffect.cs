using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LegoPuzzle.Runtime
{
    /// <summary>
    /// Smoothly enlarges the UI button when pressed (Pointer Down)
    /// and returns it to its original scale when released (Pointer Up / Exit).
    /// </summary>
    public class UIButtonPressEffect : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [Tooltip("Scale multiplier when pressed (e.g. 1.08 = 8% larger, 1.10 = 10% larger)")]
        [SerializeField] private float pressedScaleMultiplier = 1.08f;

        [Tooltip("Duration of the scale animation in seconds")]
        [SerializeField] private float animationDuration = 0.08f;

        private Vector3 originalScale = Vector3.one;
        private Coroutine scaleCoroutine;
        private bool isPressed = false;
        private bool isInitialized = false;
        private Selectable selectable;

        public bool IsPressed => isPressed;
        public Vector3 OriginalScale => originalScale;

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

            if (!isInitialized)
            {
                originalScale = transform.localScale.sqrMagnitude > 0.001f ? transform.localScale : Vector3.one;
                isInitialized = true;
            }

            isPressed = true;
            StartScaleAnimation(originalScale * pressedScaleMultiplier);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (isPressed)
            {
                isPressed = false;
                StartScaleAnimation(originalScale);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (isPressed)
            {
                isPressed = false;
                StartScaleAnimation(originalScale);
            }
        }

        private void StartScaleAnimation(Vector3 targetScale)
        {
            if (scaleCoroutine != null)
            {
                StopCoroutine(scaleCoroutine);
            }

            if (gameObject.activeInHierarchy)
            {
                scaleCoroutine = StartCoroutine(AnimateScaleRoutine(targetScale));
            }
            else
            {
                transform.localScale = targetScale;
            }
        }

        private IEnumerator AnimateScaleRoutine(Vector3 targetScale)
        {
            Vector3 startScale = transform.localScale;
            float elapsed = 0f;

            while (elapsed < animationDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / animationDuration);
                // Smooth sine ease out
                t = Mathf.Sin(t * Mathf.PI * 0.5f);
                transform.localScale = Vector3.LerpUnclamped(startScale, targetScale, t);
                yield return null;
            }

            transform.localScale = targetScale;
            scaleCoroutine = null;
        }

        /// <summary>
        /// Attaches UIButtonPressEffect to a single GameObject (or Button).
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
        /// Attaches UIButtonPressEffect to all Button components within the given root GameObject.
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
