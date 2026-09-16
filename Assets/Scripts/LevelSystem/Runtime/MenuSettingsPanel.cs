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

        [Header("Notifications Toggle")]
        [SerializeField] private Button notificationsToggleBtn;
        [SerializeField] private Image notificationsToggleBg;
        [SerializeField] private RectTransform notificationsKnob;
        [SerializeField] private TMP_Text notificationsStateTMP;

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

        private const float KNOB_OFFSET_X = 26f;
        private const float TEXT_OFFSET_X = 22f;
        private Coroutine animateCoroutine;
        private Coroutine soundKnobCoroutine;
        private Coroutine musicKnobCoroutine;
        private Coroutine vibrationKnobCoroutine;
        private Coroutine notificationsKnobCoroutine;
        private Coroutine toastCoroutine;

        public bool IsOpen => gameObject.activeSelf && (canvasGroup == null || canvasGroup.alpha > 0.05f);

        private void Awake()
        {
            Instance = this;
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

        private void Start()
        {
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

            if (notificationsToggleBtn != null)
            {
                notificationsToggleBtn.onClick.RemoveListener(OnNotificationsToggleClicked);
                notificationsToggleBtn.onClick.AddListener(OnNotificationsToggleClicked);
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

        /// <summary>
        /// Opens the settings panel with a punchy bounce and fade transition.
        /// </summary>
        public void Open()
        {
            gameObject.SetActive(true);
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
            }

            if (animateCoroutine != null) StopCoroutine(animateCoroutine);
            animateCoroutine = StartCoroutine(AnimateOpenRoutine());
        }

        /// <summary>
        /// Closes the settings panel with a smooth scale-down and fade.
        /// </summary>
        public void Close()
        {
            if (GameSettingsManager.HasInstance)
            {
                GameSettingsManager.Instance.PlayClickSound();
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

            canvasGroup.alpha = 0f;
            dialogCard.localScale = new Vector3(0.65f, 0.65f, 1f);

            // Staggered rows entrance
            Transform[] rows = new Transform[]
            {
                dialogCard.Find("SoundRow"),
                dialogCard.Find("MusicRow"),
                dialogCard.Find("VibrationRow"),
                dialogCard.Find("NotificationsRow"),
                dialogCard.Find("ResetProgressButton")
            };

            Vector2[] origPositions = new Vector2[rows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i] != null)
                {
                    RectTransform rt = rows[i].GetComponent<RectTransform>();
                    origPositions[i] = rt.anchoredPosition;
                    rt.anchoredPosition = new Vector2(origPositions[i].x, origPositions[i].y - 28f);
                    rows[i].localScale = new Vector3(0.9f, 0.9f, 1f);
                }
            }

            float duration = 0.34f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // DialogCard elastic overshoot bounce
                float scaleT;
                if (t < 0.7f)
                {
                    float p = t / 0.7f;
                    scaleT = Mathf.Lerp(0.65f, 1.06f, 1f - Mathf.Pow(1f - p, 2f));
                }
                else
                {
                    float p = (t - 0.7f) / 0.3f;
                    scaleT = Mathf.Lerp(1.06f, 1.0f, p * (2f - p));
                }

                dialogCard.localScale = new Vector3(scaleT, scaleT, 1f);
                canvasGroup.alpha = Mathf.Clamp01(t * 2.2f);

                // Staggered row slide-in
                for (int i = 0; i < rows.Length; i++)
                {
                    if (rows[i] != null)
                    {
                        float rowStart = 0.05f + i * 0.04f;
                        float rowDuration = 0.18f;
                        if (elapsed >= rowStart)
                        {
                            float rowT = Mathf.Clamp01((elapsed - rowStart) / rowDuration);
                            float smoothRowT = rowT * rowT * (3f - 2f * rowT);
                            RectTransform rt = rows[i].GetComponent<RectTransform>();
                            rt.anchoredPosition = Vector2.Lerp(new Vector2(origPositions[i].x, origPositions[i].y - 28f), origPositions[i], smoothRowT);
                            rows[i].localScale = Vector3.Lerp(new Vector3(0.9f, 0.9f, 1f), Vector3.one, smoothRowT);
                        }
                    }
                }

                yield return null;
            }

            dialogCard.localScale = Vector3.one;
            canvasGroup.alpha = 1f;

            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i] != null)
                {
                    RectTransform rt = rows[i].GetComponent<RectTransform>();
                    rt.anchoredPosition = origPositions[i];
                    rows[i].localScale = Vector3.one;
                }
            }

            animateCoroutine = null;
        }

        private IEnumerator AnimateCloseRoutine()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (dialogCard == null) yield break;

            float duration = 0.18f;
            float elapsed = 0f;
            Vector3 startScale = dialogCard.localScale;
            float startAlpha = canvasGroup.alpha;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                float scale;
                if (t < 0.25f)
                {
                    float p = t / 0.25f;
                    scale = Mathf.Lerp(startScale.x, 1.03f, p);
                }
                else
                {
                    float p = (t - 0.25f) / 0.75f;
                    scale = Mathf.Lerp(1.03f, 0.70f, p * p);
                }

                dialogCard.localScale = new Vector3(scale, scale, 1f);
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
                yield return null;
            }

            canvasGroup.alpha = 0f;
            dialogCard.localScale = Vector3.one;
            gameObject.SetActive(false);
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

        private void OnNotificationsToggleClicked()
        {
            bool current = GameSettingsManager.HasInstance ? GameSettingsManager.Instance.NotificationsEnabled : true;
            bool newState = !current;

            if (GameSettingsManager.HasInstance)
            {
                GameSettingsManager.Instance.SetNotificationsEnabled(newState);
                GameSettingsManager.Instance.PlayClickSound();
            }

            if (notificationsToggleBtn != null)
            {
                StartCoroutine(PunchScaleRoutine(notificationsToggleBtn.transform, 1.09f, 0.16f));
            }

            AnimateToggle(notificationsKnob, notificationsToggleBg, notificationsStateTMP, newState, ref notificationsKnobCoroutine);
        }

        public void RefreshAllToggleStates(bool animate)
        {
            bool soundOn = GameSettingsManager.HasInstance ? GameSettingsManager.Instance.SoundEnabled : true;
            bool musicOn = GameSettingsManager.HasInstance ? GameSettingsManager.Instance.MusicEnabled : true;
            bool vibOn = GameSettingsManager.HasInstance ? GameSettingsManager.Instance.VibrationEnabled : true;
            bool notifOn = GameSettingsManager.HasInstance ? GameSettingsManager.Instance.NotificationsEnabled : true;

            SetToggleImmediate(soundKnob, soundToggleBg, soundStateTMP, soundOn);
            SetToggleImmediate(musicKnob, musicToggleBg, musicStateTMP, musicOn);
            SetToggleImmediate(vibrationKnob, vibrationToggleBg, vibrationStateTMP, vibOn);
            SetToggleImmediate(notificationsKnob, notificationsToggleBg, notificationsStateTMP, notifOn);
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
                HookToggleRow("NotificationsRow", ref notificationsToggleBtn, ref notificationsToggleBg, ref notificationsKnob, ref notificationsStateTMP);

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

        #endregion
    }
}
