using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace LegoPuzzle.Runtime
{
    /// <summary>
    /// Controller for the Settings Panel in the Menu scene.
    /// Manages sound, music, vibration, and notifications toggles with smooth sliding animations,
    /// juicy opening/closing pop transitions, level progress reset with confirmation,
    /// and full raycast backdrop dismissal.
    /// </summary>
    [ExecuteAlways]
    public class MenuSettingsPanel : MonoBehaviour
    {
        public static MenuSettingsPanel Instance { get; private set; }

        [Header("Dialog & Backdrop References")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform dialogCard;
        [SerializeField] private Button backdropButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button settingsTriggerButton;

        [Header("Sound FX Toggle")]
        [SerializeField] private Button soundToggleBtn;
        [SerializeField] private Image soundToggleBg;
        [SerializeField] private RectTransform soundKnob;
        [SerializeField] private TMP_Text soundStateTMP;

        [Header("Music Toggle")]
        [SerializeField] private Button musicToggleBtn;
        [SerializeField] private Image musicToggleBg;
        [SerializeField] private RectTransform musicKnob;
        [SerializeField] private TMP_Text musicStateTMP;

        [Header("Vibration Toggle")]
        [SerializeField] private Button vibrationToggleBtn;
        [SerializeField] private Image vibrationToggleBg;
        [SerializeField] private RectTransform vibrationKnob;
        [SerializeField] private TMP_Text vibrationStateTMP;

        [Header("Colorblind Mode Toggle")]
        [SerializeField] private Button colorblindToggleBtn;
        [SerializeField] private Image colorblindToggleBg;
        [SerializeField] private RectTransform colorblindKnob;
        [SerializeField] private TMP_Text colorblindStateTMP;

        [SerializeField, HideInInspector] private Button notificationsToggleBtn;
        [SerializeField, HideInInspector] private Image notificationsToggleBg;
        [SerializeField, HideInInspector] private RectTransform notificationsKnob;
        [SerializeField, HideInInspector] private TMP_Text notificationsStateTMP;

        [Header("Reset Progress Section")]
        [SerializeField] private Button resetProgressBtn;
        [SerializeField] private GameObject confirmationCard;
        [SerializeField] private Button confirmResetBtn;
        [SerializeField] private Button cancelResetBtn;
        [SerializeField] private GameObject toastRoot;
        [SerializeField] private TMP_Text toastTMP;

        [Header("Colors & Styling")]
        [SerializeField] private Color toggleOnColor = new Color(0.2f, 0.82f, 0.48f, 1f); // Vibrant emerald green
        [SerializeField] private Color toggleOffColor = new Color(0.32f, 0.36f, 0.44f, 1f); // Slate grey

        [Header("Slide & Jelly Animation")]
        [Tooltip("Duration of the slide-in and jelly bounce animation in seconds")]
        [SerializeField] private float openDuration = 0.46f;

        [Tooltip("Intensity of the jelly squash-and-stretch effect (0.0 = none, 0.12 = balanced jelly, 0.2 = bouncy)")]
        [Range(0f, 0.25f)]
        [SerializeField] private float jellyIntensity = 0.12f;

        [Tooltip("Vertical overshoot bounce distance in pixels as the panel lands")]
        [SerializeField] private float overshootDistance = 28f;

        [Tooltip("Duration of the slide-out close animation in seconds")]
        [SerializeField] private float closeDuration = 0.24f;

        private const float KNOB_OFFSET_X = 26f;
        private const float TEXT_OFFSET_X = 22f;
        private Coroutine animateCoroutine;
        private Coroutine soundKnobCoroutine;
        private Coroutine musicKnobCoroutine;
        private Coroutine vibrationKnobCoroutine;
        private Coroutine colorblindKnobCoroutine;
        private Coroutine notificationsKnobCoroutine;
        private Coroutine toastCoroutine;

        private Vector2 targetCardAnchoredPosition = Vector2.zero;
        private Transform[] rowTransforms;
        private Vector2[] rowOriginalPositions;
        private bool hasCachedPositions = false;
        private bool isAnimating = false;

        public bool IsOpen => gameObject.activeSelf && (canvasGroup == null || canvasGroup.alpha > 0.05f);
        public bool IsAnimating => isAnimating;

        private void Awake()
        {
            Instance = this;
            EnsurePositionsCached();
            EnsureUIHierarchy();
            BindButtonListeners();

            if (Application.isPlaying)
            {
                gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            Instance = this;
            EnsurePositionsCached();
            EnsureUIHierarchy();
            BindButtonListeners();
            RefreshAllToggleStates(false);

            if (confirmationCard != null)
            {
                confirmationCard.SetActive(false);
            }
            if (toastRoot != null)
            {
                toastRoot.SetActive(false);
            }
        }

        private void OnDisable()
        {
            if (animateCoroutine != null)
            {
                StopCoroutine(animateCoroutine);
                animateCoroutine = null;
            }
            if (dialogCard != null && hasCachedPositions)
            {
                dialogCard.anchoredPosition = targetCardAnchoredPosition;
                dialogCard.localScale = Vector3.one;
            }
            RestoreRowPositions();
            isAnimating = false;
        }

        private void Start()
        {
            EnsurePositionsCached();
            EnsureUIHierarchy();
            BindButtonListeners();
            RefreshAllToggleStates(false);
        }

        private void OnValidate()
        {
            EnsureUIHierarchy();
        }

        public void BindButtonListeners()
        {
            if (backdropButton != null)
            {
                backdropButton.onClick.RemoveListener(Close);
                backdropButton.onClick.AddListener(Close);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Close);
                closeButton.onClick.AddListener(Close);
            }

            if (settingsTriggerButton == null)
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                if (canvas == null) canvas = FindAnyObjectByType<Canvas>();
                if (canvas != null)
                {
                    Transform btnTr = canvas.transform.Find("SettingsButton");
                    if (btnTr != null) settingsTriggerButton = btnTr.GetComponent<Button>();
                }
            }

            if (settingsTriggerButton != null)
            {
                settingsTriggerButton.onClick.RemoveListener(Open);
                settingsTriggerButton.onClick.AddListener(Open);
            }

            if (soundToggleBtn != null)
            {
                soundToggleBtn.onClick.RemoveListener(OnSoundToggleClicked);
                soundToggleBtn.onClick.AddListener(OnSoundToggleClicked);
            }

            if (musicToggleBtn != null)
            {
                musicToggleBtn.onClick.RemoveListener(OnMusicToggleClicked);
                musicToggleBtn.onClick.AddListener(OnMusicToggleClicked);
            }

            if (vibrationToggleBtn != null)
            {
                vibrationToggleBtn.onClick.RemoveListener(OnVibrationToggleClicked);
                vibrationToggleBtn.onClick.AddListener(OnVibrationToggleClicked);
            }

            if (colorblindToggleBtn == null && notificationsToggleBtn != null)
            {
                colorblindToggleBtn = notificationsToggleBtn;
                colorblindToggleBg = notificationsToggleBg;
                colorblindKnob = notificationsKnob;
                colorblindStateTMP = notificationsStateTMP;
            }

            if (colorblindToggleBtn != null)
            {
                colorblindToggleBtn.onClick.RemoveListener(OnColorblindToggleClicked);
                colorblindToggleBtn.onClick.AddListener(OnColorblindToggleClicked);
            }

            if (notificationsToggleBtn != null && notificationsToggleBtn != colorblindToggleBtn)
            {
                notificationsToggleBtn.onClick.RemoveListener(OnColorblindToggleClicked);
                notificationsToggleBtn.onClick.AddListener(OnColorblindToggleClicked);
            }

            if (resetProgressBtn != null)
            {
                resetProgressBtn.onClick.RemoveListener(OnResetProgressClicked);
                resetProgressBtn.onClick.AddListener(OnResetProgressClicked);
            }

            if (confirmResetBtn != null)
            {
                confirmResetBtn.onClick.RemoveListener(OnConfirmResetClicked);
                confirmResetBtn.onClick.AddListener(OnConfirmResetClicked);
            }

            if (cancelResetBtn != null)
            {
                cancelResetBtn.onClick.RemoveListener(OnCancelResetClicked);
                cancelResetBtn.onClick.AddListener(OnCancelResetClicked);
            }
        }

        private void EnsurePositionsCached()
        {
            if (hasCachedPositions) return;

            if (dialogCard != null)
            {
                // If it's already off-screen for some reason, default to Vector2.zero
                if (Mathf.Abs(dialogCard.anchoredPosition.y) > 600f)
                {
                    targetCardAnchoredPosition = Vector2.zero;
                }
                else
                {
                    targetCardAnchoredPosition = dialogCard.anchoredPosition;
                }
            }
            EnsureRowReferences();
            hasCachedPositions = true;
        }

        private void EnsureRowReferences()
        {
            if (dialogCard == null) return;
            if (rowTransforms != null && rowTransforms.Length > 0 && rowOriginalPositions != null) return;

            Transform row4 = dialogCard.Find("ColorblindRow");
            if (row4 == null) row4 = dialogCard.Find("NotificationsRow");

            Transform[] candidates = new Transform[]
            {
                dialogCard.Find("SoundRow"),
                dialogCard.Find("MusicRow"),
                dialogCard.Find("VibrationRow"),
                row4,
                dialogCard.Find("ResetProgressButton")
            };

            int count = 0;
            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i] != null) count++;
            }

            rowTransforms = new Transform[count];
            rowOriginalPositions = new Vector2[count];

            int idx = 0;
            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i] != null)
                {
                    rowTransforms[idx] = candidates[i];
                    RectTransform rt = candidates[i].GetComponent<RectTransform>();
                    rowOriginalPositions[idx] = rt != null ? rt.anchoredPosition : Vector2.zero;
                    idx++;
                }
            }
        }

        private void RestoreRowPositions()
        {
            if (rowTransforms == null || rowOriginalPositions == null) return;
            for (int i = 0; i < rowTransforms.Length; i++)
            {
                if (rowTransforms[i] != null && i < rowOriginalPositions.Length)
                {
                    RectTransform rt = rowTransforms[i].GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.anchoredPosition = rowOriginalPositions[i];
                        rowTransforms[i].localScale = Vector3.one;
                    }
                }
            }
        }

        private Vector2 CalculateOffscreenBottomPosition()
        {
            if (dialogCard == null) return new Vector2(0f, -1600f);

            float parentHeight = 1920f;
            RectTransform parentRt = dialogCard.parent as RectTransform;
            if (parentRt != null && parentRt.rect.height > 100f)
            {
                parentHeight = parentRt.rect.height;
            }
            else
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                if (canvas == null) canvas = FindAnyObjectByType<Canvas>();
                if (canvas != null)
                {
                    RectTransform canvasRt = canvas.GetComponent<RectTransform>();
                    if (canvasRt != null && canvasRt.rect.height > 100f)
                    {
                        parentHeight = canvasRt.rect.height;
                    }
                }
            }

            float cardHeight = dialogCard.rect.height > 50f ? dialogCard.rect.height : 860f;
            float cardTopExtent = cardHeight * (1f - dialogCard.pivot.y);
            float offscreenY = -(parentHeight * 0.5f + cardTopExtent + 140f);

            return new Vector2(targetCardAnchoredPosition.x, offscreenY);
        }

        /// <summary>
        /// Opens the settings panel: slides in smoothly from the bottom with a juicy jelly squash & stretch scale effect.
        /// </summary>
        public void Open()
        {
            if (IsOpen && isAnimating) return;

            gameObject.SetActive(true);
            EnsurePositionsCached();
            RefreshAllToggleStates(false);

            if (confirmationCard != null)
            {
                confirmationCard.SetActive(false);
            }
            if (toastRoot != null)
            {
                toastRoot.SetActive(false);
            }

            if (GameSettingsManager.HasInstance)
            {
                GameSettingsManager.Instance.PlayClickSound();
                GameSettingsManager.Instance.TriggerHaptic();
            }

            if (animateCoroutine != null) StopCoroutine(animateCoroutine);
            animateCoroutine = StartCoroutine(AnimateOpenRoutine());
        }

        /// <summary>
        /// Closes the settings panel with a smooth slide-down and fade.
        /// </summary>
        public void Close()
        {
            if (!gameObject.activeSelf) return;

            if (GameSettingsManager.HasInstance)
            {
                GameSettingsManager.Instance.PlayClickSound();
                GameSettingsManager.Instance.TriggerHaptic();
            }

            if (closeButton != null)
            {
                StartCoroutine(PunchScaleRoutine(closeButton.transform, 0.88f, 0.14f));
            }

            if (animateCoroutine != null) StopCoroutine(animateCoroutine);
            animateCoroutine = StartCoroutine(AnimateCloseRoutine());
        }

        private IEnumerator AnimateOpenRoutine()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (dialogCard == null) yield break;

            EnsurePositionsCached();
            isAnimating = true;

            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = true;

            Vector2 startPos = CalculateOffscreenBottomPosition();
            dialogCard.anchoredPosition = startPos;
            dialogCard.localScale = new Vector3(0.58f, 0.58f, 1f);

            // Prepare staggered rows
            EnsureRowReferences();
            for (int i = 0; i < rowTransforms.Length; i++)
            {
                if (rowTransforms[i] != null && i < rowOriginalPositions.Length)
                {
                    RectTransform rt = rowTransforms[i].GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.anchoredPosition = new Vector2(rowOriginalPositions[i].x, rowOriginalPositions[i].y - 20f);
                    }
                }
            }

            float duration = openDuration;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // 1. POSITION: Fly up from bottom with overshoot bounce
                float travelT = Mathf.Clamp01(t / 0.62f);
                float baseTravel = 1f - Mathf.Pow(1f - travelT, 3f);
                float baseY = Mathf.Lerp(startPos.y, targetCardAnchoredPosition.y, baseTravel);

                // Add physical overshoot bounce in pixel space as panel arrives
                float bounceOffset = 0f;
                if (t >= 0.45f)
                {
                    float bt = (t - 0.45f) / 0.55f;
                    float decay = (1f - bt) * (1f - bt);
                    bounceOffset = Mathf.Sin(bt * Mathf.PI * 2.2f) * overshootDistance * decay;
                }
                dialogCard.anchoredPosition = new Vector2(targetCardAnchoredPosition.x, baseY + bounceOffset);

                // 2. SCALE: Grow with jelly squash & stretch
                float growT = 1f - Mathf.Pow(1f - t, 2.6f);
                float baseScale = Mathf.Lerp(0.58f, 1.0f, growT);

                // Jelly deformation (squash & stretch volume-preserving oscillation)
                float jelly = 0f;
                if (t < 0.45f)
                {
                    // Vertical elongation during upward flight
                    float flightP = t / 0.45f;
                    jelly = Mathf.Sin(flightP * Mathf.PI) * jellyIntensity;
                }
                else
                {
                    // Squash wide upon landing, then spring back
                    float impactP = (t - 0.45f) / 0.55f;
                    float decay = (1f - impactP) * (1f - impactP);
                    jelly = -Mathf.Sin(impactP * Mathf.PI * 2f) * (jellyIntensity * 0.85f) * decay;
                }

                float sx = baseScale * (1f - jelly);
                float sy = baseScale * (1f + jelly);
                dialogCard.localScale = new Vector3(sx, sy, 1f);

                // 3. FADE: Fade in background and dialog
                canvasGroup.alpha = Mathf.Clamp01(t * 2.8f);

                // 4. STAGGERED ROWS: Subtle cascade into rest positions
                for (int i = 0; i < rowTransforms.Length; i++)
                {
                    if (rowTransforms[i] != null && i < rowOriginalPositions.Length)
                    {
                        float rowStart = 0.38f + i * 0.035f;
                        float rowDur = 0.20f;
                        if (elapsed >= rowStart)
                        {
                            float rowT = Mathf.Clamp01((elapsed - rowStart) / rowDur);
                            float smoothRowT = rowT * rowT * (3f - 2f * rowT);
                            RectTransform rt = rowTransforms[i].GetComponent<RectTransform>();
                            if (rt != null)
                            {
                                Vector2 orig = rowOriginalPositions[i];
                                rt.anchoredPosition = Vector2.Lerp(new Vector2(orig.x, orig.y - 20f), orig, smoothRowT);
                            }
                        }
                    }
                }

                yield return null;
            }

            dialogCard.anchoredPosition = targetCardAnchoredPosition;
            dialogCard.localScale = Vector3.one;
            canvasGroup.alpha = 1f;
            RestoreRowPositions();

            isAnimating = false;
            animateCoroutine = null;
        }

        private IEnumerator AnimateCloseRoutine()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (dialogCard == null) yield break;

            EnsurePositionsCached();
            isAnimating = true;

            Vector2 startPos = dialogCard.anchoredPosition;
            Vector2 offscreenPos = CalculateOffscreenBottomPosition();
            Vector3 initialScale = dialogCard.localScale;
            float startAlpha = canvasGroup.alpha;

            float duration = closeDuration;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                float moveProgress;
                float sx, sy;

                if (t < 0.18f)
                {
                    // Brief anticipation squash before sliding down
                    float p = t / 0.18f;
                    float anticip = Mathf.Sin(p * Mathf.PI);
                    sx = Mathf.Lerp(initialScale.x, 1.04f, anticip);
                    sy = Mathf.Lerp(initialScale.y, 0.95f, anticip);
                    moveProgress = 0f;
                }
                else
                {
                    float p = (t - 0.18f) / 0.82f;
                    // Ease-in cubic down to offscreen
                    moveProgress = p * p * p;
                    // Elongate slightly in falling direction
                    sx = Mathf.Lerp(1.04f, 0.88f, p);
                    sy = Mathf.Lerp(0.95f, 1.10f, p);
                }

                dialogCard.anchoredPosition = Vector2.Lerp(startPos, offscreenPos, moveProgress);
                dialogCard.localScale = new Vector3(sx, sy, 1f);
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, Mathf.Clamp01(t * 1.5f));

                yield return null;
            }

            canvasGroup.alpha = 0f;
            dialogCard.anchoredPosition = targetCardAnchoredPosition;
            dialogCard.localScale = Vector3.one;
            RestoreRowPositions();
            gameObject.SetActive(false);

            isAnimating = false;
            animateCoroutine = null;
        }

        #region Toggle Handlers

        private void OnSoundToggleClicked()
        {
            bool current = GameSettingsManager.HasInstance ? GameSettingsManager.Instance.SoundEnabled : true;
            bool newState = !current;

            if (GameSettingsManager.HasInstance)
            {
                GameSettingsManager.Instance.SetSoundEnabled(newState);
            }

            if (soundToggleBtn != null)
            {
                StartCoroutine(PunchScaleRoutine(soundToggleBtn.transform, 1.09f, 0.16f));
            }

            AnimateToggle(soundKnob, soundToggleBg, soundStateTMP, newState, ref soundKnobCoroutine);
        }

        private void OnMusicToggleClicked()
        {
            bool current = GameSettingsManager.HasInstance ? GameSettingsManager.Instance.MusicEnabled : true;
            bool newState = !current;

            if (GameSettingsManager.HasInstance)
            {
                GameSettingsManager.Instance.SetMusicEnabled(newState);
                GameSettingsManager.Instance.PlayClickSound();
            }

            if (musicToggleBtn != null)
            {
                StartCoroutine(PunchScaleRoutine(musicToggleBtn.transform, 1.09f, 0.16f));
            }

            AnimateToggle(musicKnob, musicToggleBg, musicStateTMP, newState, ref musicKnobCoroutine);
        }

        private void OnVibrationToggleClicked()
        {
            bool current = GameSettingsManager.HasInstance ? GameSettingsManager.Instance.VibrationEnabled : true;
            bool newState = !current;

            if (GameSettingsManager.HasInstance)
            {
                GameSettingsManager.Instance.SetVibrationEnabled(newState);
                GameSettingsManager.Instance.PlayClickSound();
            }

            if (vibrationToggleBtn != null)
            {
                StartCoroutine(PunchScaleRoutine(vibrationToggleBtn.transform, 1.09f, 0.16f));
            }

            AnimateToggle(vibrationKnob, vibrationToggleBg, vibrationStateTMP, newState, ref vibrationKnobCoroutine);
        }

        private void OnColorblindToggleClicked()
        {
            bool current = GameSettingsManager.HasInstance ? GameSettingsManager.Instance.ColorblindModeEnabled : false;
            bool newState = !current;

            if (GameSettingsManager.HasInstance)
            {
                GameSettingsManager.Instance.SetColorblindModeEnabled(newState);
                GameSettingsManager.Instance.PlayClickSound();
                GameSettingsManager.Instance.TriggerHaptic();
            }

            if (colorblindToggleBtn != null)
            {
                StartCoroutine(PunchScaleRoutine(colorblindToggleBtn.transform, 1.09f, 0.16f));
            }

            AnimateToggle(colorblindKnob, colorblindToggleBg, colorblindStateTMP, newState, ref colorblindKnobCoroutine);
        }

        private void OnNotificationsToggleClicked()
        {
            OnColorblindToggleClicked();
        }

        public void RefreshAllToggleStates(bool animate)
        {
            bool soundOn = GameSettingsManager.HasInstance ? GameSettingsManager.Instance.SoundEnabled : true;
            bool musicOn = GameSettingsManager.HasInstance ? GameSettingsManager.Instance.MusicEnabled : true;
            bool vibOn = GameSettingsManager.HasInstance ? GameSettingsManager.Instance.VibrationEnabled : true;
            bool colorblindOn = GameSettingsManager.HasInstance ? GameSettingsManager.Instance.ColorblindModeEnabled : false;

            SetToggleImmediate(soundKnob, soundToggleBg, soundStateTMP, soundOn);
            SetToggleImmediate(musicKnob, musicToggleBg, musicStateTMP, musicOn);
            SetToggleImmediate(vibrationKnob, vibrationToggleBg, vibrationStateTMP, vibOn);
            SetToggleImmediate(colorblindKnob, colorblindToggleBg, colorblindStateTMP, colorblindOn);
        }

        private void SetToggleImmediate(RectTransform knob, Image bg, TMP_Text stateTMP, bool isOn)
        {
            if (knob != null)
            {
                knob.anchoredPosition = new Vector2(isOn ? KNOB_OFFSET_X : -KNOB_OFFSET_X, 0f);
                knob.localScale = Vector3.one;
            }
            if (bg != null)
            {
                bg.color = isOn ? toggleOnColor : toggleOffColor;
            }
            if (stateTMP != null)
            {
                RectTransform stateRt = stateTMP.rectTransform;
                stateRt.anchorMin = new Vector2(0.5f, 0.5f);
                stateRt.anchorMax = new Vector2(0.5f, 0.5f);
                stateRt.pivot = new Vector2(0.5f, 0.5f);
                stateRt.sizeDelta = new Vector2(48f, 36f);
                // When ON: text is on the LEFT (-22f). When OFF: text is on the RIGHT (+22f).
                stateRt.anchoredPosition = new Vector2(isOn ? -TEXT_OFFSET_X : TEXT_OFFSET_X, 0f);
                stateTMP.alignment = TextAlignmentOptions.Center;
                stateTMP.text = isOn ? "ON" : "OFF";
                stateTMP.color = Color.white;
                stateTMP.alpha = 1f;
                stateTMP.raycastTarget = false;
            }
        }

        private void AnimateToggle(RectTransform knob, Image bg, TMP_Text stateTMP, bool isOn, ref Coroutine coroutineRef)
        {
            if (coroutineRef != null) StopCoroutine(coroutineRef);
            coroutineRef = StartCoroutine(AnimateKnobRoutine(knob, bg, stateTMP, isOn));
        }

        private IEnumerator AnimateKnobRoutine(RectTransform knob, Image bg, TMP_Text stateTMP, bool isOn)
        {
            if (knob == null) yield break;

            float startKnobX = knob.anchoredPosition.x;
            float targetKnobX = isOn ? KNOB_OFFSET_X : -KNOB_OFFSET_X;

            float targetTextX = isOn ? -TEXT_OFFSET_X : TEXT_OFFSET_X;

            Color startColor = bg != null ? bg.color : (isOn ? toggleOffColor : toggleOnColor);
            Color targetColor = isOn ? toggleOnColor : toggleOffColor;

            float duration = 0.20f;
            float elapsed = 0f;
            bool textSwapped = false;

            if (stateTMP != null)
            {
                RectTransform stateRt = stateTMP.rectTransform;
                stateRt.anchorMin = new Vector2(0.5f, 0.5f);
                stateRt.anchorMax = new Vector2(0.5f, 0.5f);
                stateRt.pivot = new Vector2(0.5f, 0.5f);
                stateRt.sizeDelta = new Vector2(48f, 36f);
                stateTMP.alignment = TextAlignmentOptions.Center;
                stateTMP.raycastTarget = false;
            }

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float smoothT = t * t * (3f - 2f * t);

                // 1. Knob Position with spring overshoot
                float curKnobX;
                if (t < 0.8f)
                {
                    float p = t / 0.8f;
                    curKnobX = Mathf.Lerp(startKnobX, targetKnobX * 1.05f, p);
                }
                else
                {
                    float p = (t - 0.8f) / 0.2f;
                    curKnobX = Mathf.Lerp(targetKnobX * 1.05f, targetKnobX, p);
                }
                knob.anchoredPosition = new Vector2(curKnobX, 0f);

                // 2. Knob Squash & Stretch
                float sx = 1f;
                float sy = 1f;
                if (t < 0.5f)
                {
                    float p = t / 0.5f;
                    sx = Mathf.Lerp(1f, 1.20f, p);
                    sy = Mathf.Lerp(1f, 0.85f, p);
                }
                else if (t < 0.8f)
                {
                    float p = (t - 0.5f) / 0.3f;
                    sx = Mathf.Lerp(1.20f, 0.88f, p);
                    sy = Mathf.Lerp(0.85f, 1.14f, p);
                }
                else
                {
                    float p = (t - 0.8f) / 0.2f;
                    sx = Mathf.Lerp(0.88f, 1f, p);
                    sy = Mathf.Lerp(1.14f, 1f, p);
                }
                knob.localScale = new Vector3(sx, sy, 1f);

                // 3. Background Color
                if (bg != null)
                {
                    bg.color = Color.Lerp(startColor, targetColor, smoothT);
                }

                // 4. State Text Cross-fade & Snap (ON on left, OFF on right)
                if (stateTMP != null)
                {
                    if (t < 0.45f)
                    {
                        stateTMP.alpha = 1f - (t / 0.45f);
                    }
                    else
                    {
                        if (!textSwapped)
                        {
                            stateTMP.text = isOn ? "ON" : "OFF";
                            stateTMP.rectTransform.anchoredPosition = new Vector2(targetTextX, 0f);
                            textSwapped = true;
                        }
                        float fadeIn = (t - 0.45f) / 0.55f;
                        stateTMP.alpha = Mathf.Clamp01(fadeIn * 1.25f);
                    }
                }

                yield return null;
            }

            knob.anchoredPosition = new Vector2(targetKnobX, 0f);
            knob.localScale = Vector3.one;
            if (bg != null) bg.color = targetColor;

            if (stateTMP != null)
            {
                stateTMP.text = isOn ? "ON" : "OFF";
                stateTMP.rectTransform.anchoredPosition = new Vector2(targetTextX, 0f);
                stateTMP.alpha = 1f;
            }
        }

        private IEnumerator PunchScaleRoutine(Transform target, float punchScale = 1.08f, float duration = 0.16f)
        {
            if (target == null) yield break;
            Vector3 baseScale = Vector3.one;
            float half = duration * 0.45f;
            float elapsed = 0f;

            while (elapsed < half)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / half);
                target.localScale = Vector3.Lerp(baseScale, baseScale * punchScale, t);
                yield return null;
            }

            elapsed = 0f;
            float rest = duration - half;
            while (elapsed < rest)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / rest);
                target.localScale = Vector3.Lerp(baseScale * punchScale, baseScale, t);
                yield return null;
            }

            target.localScale = baseScale;
        }

        #endregion

        #region Reset Progress Section

        private void OnResetProgressClicked()
        {
            if (GameSettingsManager.HasInstance)
            {
                GameSettingsManager.Instance.PlayClickSound();
            }

            if (resetProgressBtn != null)
            {
                StartCoroutine(PunchScaleRoutine(resetProgressBtn.transform, 0.93f, 0.14f));
            }

            if (confirmationCard != null)
            {
                confirmationCard.SetActive(true);
                confirmationCard.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
                StartCoroutine(PopConfirmationRoutine());
            }
        }

        private IEnumerator PopConfirmationRoutine()
        {
            if (confirmationCard == null) yield break;

            float duration = 0.18f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float s = Mathf.Lerp(0.8f, 1.03f, 1f - Mathf.Pow(1f - t, 2f));
                confirmationCard.transform.localScale = new Vector3(s, s, 1f);
                yield return null;
            }

            confirmationCard.transform.localScale = Vector3.one;
        }

        private void OnCancelResetClicked()
        {
            if (GameSettingsManager.HasInstance)
            {
                GameSettingsManager.Instance.PlayClickSound();
            }

            if (cancelResetBtn != null)
            {
                StartCoroutine(PunchScaleRoutine(cancelResetBtn.transform, 0.93f, 0.14f));
            }

            if (confirmationCard != null)
            {
                confirmationCard.SetActive(false);
            }
        }

        private void OnConfirmResetClicked()
        {
            if (GameSettingsManager.HasInstance)
            {
                GameSettingsManager.Instance.PlayClickSound();
                GameSettingsManager.Instance.ResetLevelProgress();
            }

            if (confirmResetBtn != null)
            {
                StartCoroutine(PunchScaleRoutine(confirmResetBtn.transform, 0.93f, 0.14f));
            }

            if (confirmationCard != null)
            {
                confirmationCard.SetActive(false);
            }

            ShowToast("Progress reset to Level 1!");
        }

        private void ShowToast(string message)
        {
            if (toastRoot == null || toastTMP == null) return;

            toastTMP.text = message;
            toastRoot.SetActive(true);

            if (toastCoroutine != null) StopCoroutine(toastCoroutine);
            toastCoroutine = StartCoroutine(ToastRoutine());
        }

        private IEnumerator ToastRoutine()
        {
            yield return new WaitForSecondsRealtime(2.2f);
            if (toastRoot != null)
            {
                toastRoot.SetActive(false);
            }
            toastCoroutine = null;
        }

        #endregion

        #region Hierarchy Construction

        /// <summary>
        /// Automatically hooks existing child elements or builds a complete, beautiful Settings Panel hierarchy.
        /// </summary>
        public void EnsureUIHierarchy()
        {
            RectTransform rootRt = GetComponent<RectTransform>();
            if (rootRt == null) rootRt = gameObject.AddComponent<RectTransform>();

            // Fullscreen stretch
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;
            rootRt.pivot = new Vector2(0.5f, 0.5f);

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            // 1. Ensure Backdrop
            Transform backdropTr = transform.Find("Backdrop");
            if (backdropTr == null)
            {
                GameObject bdObj = new GameObject("Backdrop");
                bdObj.transform.SetParent(transform, false);
                bdObj.transform.SetAsFirstSibling();
                RectTransform bdRt = bdObj.AddComponent<RectTransform>();
                bdRt.anchorMin = Vector2.zero;
                bdRt.anchorMax = Vector2.one;
                bdRt.offsetMin = Vector2.zero;
                bdRt.offsetMax = Vector2.zero;

                Image bdImg = bdObj.AddComponent<Image>();
                bdImg.color = new Color(0f, 0f, 0f, 0.68f);
                bdImg.raycastTarget = true;

                backdropButton = bdObj.AddComponent<Button>();
                backdropButton.transition = Selectable.Transition.None;
            }
            else
            {
                backdropButton = backdropTr.GetComponent<Button>();
            }

            // 2. Ensure Dialog Card
            Transform cardTr = transform.Find("DialogCard");
            if (cardTr == null)
            {
                // Check if current transform has legacy DialogCard elements
                cardTr = transform.Find("Card");
            }

            if (dialogCard == null && cardTr != null)
            {
                dialogCard = cardTr.GetComponent<RectTransform>();
            }

            // Hook settings button on Canvas if missing
            if (settingsTriggerButton == null)
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                if (canvas == null) canvas = FindAnyObjectByType<Canvas>();
                if (canvas != null)
                {
                    Transform triggerTr = canvas.transform.Find("SettingsButton");
                    if (triggerTr != null) settingsTriggerButton = triggerTr.GetComponent<Button>();
                }
            }

            // Auto-hook toggles if children exist
            if (dialogCard != null)
            {
                if (closeButton == null)
                {
                    Transform cb = dialogCard.Find("CloseButton");
                    if (cb != null) closeButton = cb.GetComponent<Button>();
                }

                HookToggleRow("SoundRow", ref soundToggleBtn, ref soundToggleBg, ref soundKnob, ref soundStateTMP);
                HookToggleRow("MusicRow", ref musicToggleBtn, ref musicToggleBg, ref musicKnob, ref musicStateTMP);
                HookToggleRow("VibrationRow", ref vibrationToggleBtn, ref vibrationToggleBg, ref vibrationKnob, ref vibrationStateTMP);

                Transform row4 = dialogCard.Find("ColorblindRow");
                if (row4 == null) row4 = dialogCard.Find("NotificationsRow");
                if (row4 != null)
                {
                    row4.name = "ColorblindRow";
                    row4.gameObject.SetActive(true);

                    Transform labelTr = row4.Find("Label");
                    if (labelTr != null)
                    {
                        TMP_Text labelTMP = labelTr.GetComponent<TMP_Text>();
                        if (labelTMP != null) labelTMP.text = "Color Blind";
                    }

                    Transform iconTr = row4.Find("Icon");
                    if (iconTr != null)
                    {
                        Image iconImg = iconTr.GetComponent<Image>();
                        if (iconImg != null)
                        {
                            iconImg.sprite = GetOrCreateEyeIconSprite();
                        }
                    }
                }

                HookToggleRow("ColorblindRow", ref colorblindToggleBtn, ref colorblindToggleBg, ref colorblindKnob, ref colorblindStateTMP);
                if (colorblindToggleBtn == null)
                {
                    HookToggleRow("NotificationsRow", ref colorblindToggleBtn, ref colorblindToggleBg, ref colorblindKnob, ref colorblindStateTMP);
                }

                if (resetProgressBtn == null)
                {
                    Transform rpb = dialogCard.Find("ResetProgressButton");
                    if (rpb != null) resetProgressBtn = rpb.GetComponent<Button>();
                }

                if (confirmationCard == null)
                {
                    Transform conf = dialogCard.Find("ConfirmationDialog");
                    if (conf != null)
                    {
                        confirmationCard = conf.gameObject;
                        Transform confirmTr = conf.Find("ConfirmButton");
                        if (confirmTr != null) confirmResetBtn = confirmTr.GetComponent<Button>();
                        Transform cancelTr = conf.Find("CancelButton");
                        if (cancelTr != null) cancelResetBtn = cancelTr.GetComponent<Button>();
                    }
                }

                if (toastRoot == null)
                {
                    Transform tRoot = dialogCard.Find("ToastRoot");
                    if (tRoot != null)
                    {
                        toastRoot = tRoot.gameObject;
                        toastTMP = tRoot.GetComponentInChildren<TMP_Text>();
                    }
                }
            }
        }

        private void HookToggleRow(string rowName, ref Button btn, ref Image bg, ref RectTransform knob, ref TMP_Text stateTMP)
        {
            if (dialogCard == null) return;
            Transform row = dialogCard.Find(rowName);
            if (row == null) return;

            Transform switchTr = row.Find("ToggleSwitch");
            if (switchTr == null) switchTr = row.Find("Switch");
            if (switchTr != null)
            {
                if (btn == null) btn = switchTr.GetComponent<Button>();
                if (bg == null) bg = switchTr.GetComponent<Image>();

                Transform knobTr = switchTr.Find("Knob");
                if (knobTr != null && knob == null) knob = knobTr.GetComponent<RectTransform>();

                Transform txtTr = switchTr.Find("StateText");
                if (txtTr != null && stateTMP == null) stateTMP = txtTr.GetComponent<TMP_Text>();
            }
        }

        #endregion

        #region Helper Loaders

        public static Sprite LoadAtlasSprite(string atlasPath, string spriteName)
        {
#if UNITY_EDITOR
            var allAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(atlasPath);
            if (allAssets != null)
            {
                foreach (var a in allAssets)
                {
                    if (a is Sprite s && s.name == spriteName) return s;
                }
            }
#endif
            var allSprites = Resources.FindObjectsOfTypeAll<Sprite>();
            foreach (var s in allSprites)
            {
                if (s.name == spriteName) return s;
            }
            return null;
        }

        public static TMP_FontAsset GetGameFontAsset()
        {
#if UNITY_EDITOR
            TMP_FontAsset font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/rimouski sb SDF.asset");
            if (font != null) return font;
#endif
            var fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            foreach (var f in fonts)
            {
                if (f.name.IndexOf("rimouski", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    f.name.IndexOf("GROBOLD", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return f;
                }
            }
            return fonts.Length > 0 ? fonts[0] : null;
        }

        private static Sprite cachedEyeSprite;

        /// <summary>
        /// Generates a crisp procedural eye/vision icon sprite for the Colorblind toggle row.
        /// </summary>
        public static Sprite GetOrCreateEyeIconSprite()
        {
            if (cachedEyeSprite != null) return cachedEyeSprite;

            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = "Procedural_EyeIcon";
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[size * size];
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = (size - 1) * 0.46f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x - center.x) / radius; // -1 to 1
                    float ny = (y - center.y) / (radius * 0.62f); // Flatten vertically for eye shape

                    float dX = Mathf.Abs(nx);
                    float eyeShapeY = 1f - dX * dX;
                    float distFromLid = eyeShapeY - Mathf.Abs(ny);

                    Color c = Color.clear;
                    if (distFromLid > -0.1f && dX <= 1.05f)
                    {
                        float alphaLid = Mathf.Clamp01((distFromLid + 0.05f) / 0.15f);
                        float innerLid = (eyeShapeY * 0.72f) - Mathf.Abs(ny);
                        float distPupil = Mathf.Sqrt(nx * nx + ny * ny * 0.7f);

                        if (distPupil < 0.36f)
                        {
                            c = new Color(0.18f, 0.22f, 0.32f, alphaLid);
                            if (Mathf.Abs(nx - 0.10f) < 0.09f && Mathf.Abs(ny - 0.10f) < 0.09f)
                            {
                                c = new Color(1f, 1f, 1f, alphaLid);
                            }
                        }
                        else if (innerLid < 0f)
                        {
                            c = new Color(0.18f, 0.22f, 0.32f, alphaLid);
                        }
                        else
                        {
                            c = new Color(0.95f, 0.96f, 0.98f, alphaLid * 0.9f);
                        }
                    }

                    pixels[y * size + x] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            cachedEyeSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return cachedEyeSprite;
        }

        #endregion
    }
}
