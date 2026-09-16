using UnityEngine;

namespace LegoPuzzle.Runtime
{
    /// <summary>
    /// Автоматично адаптує RectTransform під безпечну зону екрана (Safe Area),
    /// запобігаючи перекриттю інтерфейсу вирізом камери (notch), Dynamic Island,
    /// закругленими кутами та смугою жестів (Home Bar) на iOS та Android.
    /// Підтримує зміну орієнтації, симуляцію в Editor та вибіркові сторони відступу.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    [AddComponentMenu("UI/Safe Area")]
    public class SafeArea : MonoBehaviour
    {
        public enum SimulationMode
        {
            None,
            iPhoneXNotch,         // 132px top, 102px bottom (на стандартному 3x ретина)
            iPhoneDynamicIsland,  // 177px top, 102px bottom
            AndroidPunchHole      // 100px top
        }

        [Header("Сторони застосування Safe Area")]
        [Tooltip("Відступ зверху (для вирізу камери, Dynamic Island, статус-бару)")]
        [SerializeField] private bool conformTop = true;

        [Tooltip("Відступ знизу (для смуги жестів Home Bar / навігаційних кнопок)")]
        [SerializeField] private bool conformBottom = true;

        [Tooltip("Відступ зліва (при горизонтальній орієнтації з вирізом зліва)")]
        [SerializeField] private bool conformLeft = true;

        [Tooltip("Відступ справа (при горизонтальній орієнтації з вирізом справа)")]
        [SerializeField] private bool conformRight = true;

        [Header("Симуляція в редакторі (Editor Testing)")]
        [Tooltip("Дозволяє перевірити відступи прямо в редакторі Unity")]
        [SerializeField] private SimulationMode editorSimulation = SimulationMode.None;

        private RectTransform rectTransform;
        private Rect lastSafeArea = Rect.zero;
        private Vector2Int lastScreenSize = Vector2Int.zero;
        private ScreenOrientation lastOrientation = ScreenOrientation.AutoRotation;
        private SimulationMode lastSimulationMode = SimulationMode.None;

        public bool ConformTop { get => conformTop; set { conformTop = value; ApplySafeArea(true); } }
        public bool ConformBottom { get => conformBottom; set { conformBottom = value; ApplySafeArea(true); } }
        public bool ConformLeft { get => conformLeft; set { conformLeft = value; ApplySafeArea(true); } }
        public bool ConformRight { get => conformRight; set { conformRight = value; ApplySafeArea(true); } }

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            ApplySafeArea(true);
        }

        private void OnEnable()
        {
            ApplySafeArea(true);
        }

        private void Update()
        {
            CheckForSafeAreaChange();
        }

        private void OnRectTransformDimensionsChange()
        {
            CheckForSafeAreaChange();
        }

        private void CheckForSafeAreaChange()
        {
            if (rectTransform == null)
                rectTransform = GetComponent<RectTransform>();

            Rect currentSafeArea = GetEffectiveSafeArea();
            Vector2Int currentScreenSize = new Vector2Int(Screen.width, Screen.height);
            ScreenOrientation currentOrientation = Screen.orientation;

            if (currentSafeArea != lastSafeArea ||
                currentScreenSize != lastScreenSize ||
                currentOrientation != lastOrientation ||
                editorSimulation != lastSimulationMode)
            {
                ApplySafeArea(false);
            }
        }

        /// <summary>
        /// Застосовує розрахунок Safe Area до якорів RectTransform.
        /// </summary>
        [ContextMenu("Apply Safe Area")]
        public void ApplySafeArea(bool force = false)
        {
            if (rectTransform == null)
                rectTransform = GetComponent<RectTransform>();

            if (rectTransform == null)
                return;

            Rect safeArea = GetEffectiveSafeArea();
            float screenWidth = Screen.width;
            float screenHeight = Screen.height;

            if (screenWidth <= 0f || screenHeight <= 0f)
                return;

            lastSafeArea = safeArea;
            lastScreenSize = new Vector2Int(Screen.width, Screen.height);
            lastOrientation = Screen.orientation;
            lastSimulationMode = editorSimulation;

            // Базові координати
            Vector2 anchorMin = Vector2.zero;
            Vector2 anchorMax = Vector2.one;

            if (conformLeft)
                anchorMin.x = safeArea.xMin / screenWidth;
            if (conformBottom)
                anchorMin.y = safeArea.yMin / screenHeight;

            if (conformRight)
                anchorMax.x = safeArea.xMax / screenWidth;
            if (conformTop)
                anchorMax.y = safeArea.yMax / screenHeight;

            // Захист від некоректних значень
            anchorMin.x = Mathf.Clamp01(anchorMin.x);
            anchorMin.y = Mathf.Clamp01(anchorMin.y);
            anchorMax.x = Mathf.Clamp(anchorMax.x, anchorMin.x, 1f);
            anchorMax.y = Mathf.Clamp(anchorMax.y, anchorMin.y, 1f);

            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Скидає RectTransform на повний екран.
        /// </summary>
        [ContextMenu("Reset to Fullscreen")]
        public void ResetToFullscreen()
        {
            if (rectTransform == null)
                rectTransform = GetComponent<RectTransform>();

            if (rectTransform == null)
                return;

            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        private Rect GetEffectiveSafeArea()
        {
#if UNITY_EDITOR
            if (editorSimulation != SimulationMode.None)
            {
                float w = Screen.width;
                float h = Screen.height;

                switch (editorSimulation)
                {
                    case SimulationMode.iPhoneXNotch:
                        // Приблизні пропорції вирізу
                        float topNotch = h * 0.052f;
                        float bottomBar = h * 0.040f;
                        return new Rect(0, bottomBar, w, h - topNotch - bottomBar);

                    case SimulationMode.iPhoneDynamicIsland:
                        float topIsland = h * 0.065f;
                        float bottomIslandBar = h * 0.040f;
                        return new Rect(0, bottomIslandBar, w, h - topIsland - bottomIslandBar);

                    case SimulationMode.AndroidPunchHole:
                        float punchTop = h * 0.042f;
                        return new Rect(0, 0, w, h - punchTop);
                }
            }
#endif
            return Screen.safeArea;
        }
    }
}
