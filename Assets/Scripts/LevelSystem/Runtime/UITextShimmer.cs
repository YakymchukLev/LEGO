using TMPro;
using UnityEngine;

namespace LegoPuzzle.Runtime
{
    /// <summary>
    /// Soft iridescent color shift for TMP titles (gold / highlight shimmer).
    /// Uses unscaled time so it keeps flowing while tutorials pause Time.timeScale.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class UITextShimmer : MonoBehaviour
    {
        [SerializeField] private float cycleSeconds = 2.4f;
        [SerializeField] private Color baseColor = new Color(1f, 0.78f, 0.18f, 1f);
        [SerializeField] private Color highlightColor = new Color(1f, 0.97f, 0.72f, 1f);
        [SerializeField] private Color accentColor = new Color(1f, 0.55f, 0.12f, 1f);

        private TMP_Text label;
        private float phase;

        private void Awake()
        {
            label = GetComponent<TMP_Text>();
            if (label != null)
            {
                label.enableVertexGradient = true;
                label.color = Color.white;
            }
        }

        private void OnEnable()
        {
            phase = 0f;
            Apply(0f);
        }

        private void Update()
        {
            if (label == null) return;
            float dur = Mathf.Max(0.25f, cycleSeconds);
            phase = (phase + Time.unscaledDeltaTime / dur) % 1f;
            Apply(phase);
        }

        private void Apply(float t)
        {
            // Two overlapping waves so the gleam travels across the glyph corners.
            float waveA = (Mathf.Sin(t * Mathf.PI * 2f) + 1f) * 0.5f;
            float waveB = (Mathf.Sin((t + 0.33f) * Mathf.PI * 2f) + 1f) * 0.5f;
            float waveC = (Mathf.Sin((t + 0.66f) * Mathf.PI * 2f) + 1f) * 0.5f;

            Color tl = Color.Lerp(baseColor, highlightColor, waveA);
            Color tr = Color.Lerp(baseColor, accentColor, waveB);
            Color bl = Color.Lerp(accentColor, baseColor, waveC);
            Color br = Color.Lerp(highlightColor, baseColor, waveA);

            label.colorGradient = new VertexGradient(tl, tr, bl, br);
        }

        public static UITextShimmer AttachTo(GameObject target, Color? baseCol = null)
        {
            if (target == null) return null;
            UITextShimmer shimmer = target.GetComponent<UITextShimmer>();
            if (shimmer == null)
                shimmer = target.AddComponent<UITextShimmer>();
            if (baseCol.HasValue)
                shimmer.baseColor = baseCol.Value;
            return shimmer;
        }
    }
}
