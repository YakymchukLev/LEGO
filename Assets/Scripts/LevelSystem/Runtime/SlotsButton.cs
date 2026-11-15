using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LegoPuzzle.Runtime
{
    /// <summary>
    /// Menu Slots button. Locked until the player reaches level 15.
    /// When locked: gray button with "15 / LEVEL". Tap shows a floating unlock hint.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class SlotsButton : MonoBehaviour
    {
        public const int UnlockLevel = 15;
        public const string LockedHint = "Available from Level 15";

        [Tooltip("Optional direct reference to the SlotMachinePanel. If null, auto-finds in scene.")]
        [SerializeField] private SlotMachinePanel targetSlotPanel;

        [Header("Juicy Polish")]
        [Tooltip("Attach spring jelly press effect automatically")]
        [SerializeField] private bool addPressEffect = true;

        private Button button;
        private Image buttonImage;
        private Color originalImageColor = Color.white;
        private bool capturedOriginalColor;
        private TextMeshProUGUI lockLabel;
        private CanvasGroup[] hiddenContentGroups;
        private bool wasLocked = true;

        private void Awake()
        {
            button = GetComponent<Button>();
            buttonImage = GetComponent<Image>();
            if (buttonImage != null)
            {
                originalImageColor = buttonImage.color;
                capturedOriginalColor = true;
            }

            if (button != null)
            {
                button.onClick.RemoveListener(OnClick);
                button.onClick.AddListener(OnClick);
            }

            if (addPressEffect)
            {
                UIButtonShinePulse shine = GetComponent<UIButtonShinePulse>();
                if (shine != null)
                {
                    if (Application.isPlaying)
                        Destroy(shine);
                    else
                        DestroyImmediate(shine);
                }

                UIButtonPressEffect.AttachHorizontalJelly(gameObject);
            }

            RefreshLockVisual();
        }

        private void OnEnable()
        {
            RefreshLockVisual();
        }

        private void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveListener(OnClick);
        }

        public void OnClick()
        {
            if (!IsSlotsUnlocked())
            {
                ShowLockedFloatingHint();
                return;
            }

            if (targetSlotPanel != null)
            {
                targetSlotPanel.Open();
                return;
            }

            SlotMachinePanel.OpenSlotMachine();
        }

        public static bool IsSlotsUnlocked()
        {
            return GetPlayerLevelNumber() >= UnlockLevel;
        }

        public static int GetPlayerLevelNumber()
        {
            if (SaveDataService.HasInstance
                && SaveDataService.Instance != null
                && SaveDataService.Instance.Data != null)
            {
                return Mathf.Max(1, SaveDataService.Instance.Data.currentLevelIndex + 1);
            }

            return Mathf.Max(1, PlayerPrefs.GetInt("LEGO_CurrentLevelIndex", 0) + 1);
        }

        public void RefreshLockVisual()
        {
            bool locked = !IsSlotsUnlocked();
            wasLocked = locked;

            EnsureLockLabel();
            CacheHiddenContent();

            if (buttonImage != null)
            {
                if (!capturedOriginalColor)
                {
                    originalImageColor = buttonImage.color;
                    capturedOriginalColor = true;
                }

                buttonImage.color = locked
                    ? new Color(0.55f, 0.55f, 0.58f, originalImageColor.a)
                    : originalImageColor;
            }

            if (lockLabel != null)
            {
                lockLabel.gameObject.SetActive(locked);
                if (locked)
                    lockLabel.text = $"{UnlockLevel}\n<size=55%>LEVEL</size>";
            }

            if (hiddenContentGroups != null)
            {
                for (int i = 0; i < hiddenContentGroups.Length; i++)
                {
                    if (hiddenContentGroups[i] == null) continue;
                    hiddenContentGroups[i].alpha = locked ? 0f : 1f;
                    hiddenContentGroups[i].blocksRaycasts = !locked;
                }
            }

            // Keep interactable so locked taps can show the hint.
            if (button != null)
                button.interactable = true;
        }

        private void LateUpdate()
        {
            // Progress can change while Menu stays loaded.
            bool locked = !IsSlotsUnlocked();
            if (locked != wasLocked)
                RefreshLockVisual();
        }

        private void EnsureLockLabel()
        {
            if (lockLabel != null) return;

            Transform existing = transform.Find("SlotsLockLabel");
            GameObject labelGo;
            if (existing != null)
            {
                labelGo = existing.gameObject;
                lockLabel = labelGo.GetComponent<TextMeshProUGUI>();
            }
            else
            {
                labelGo = new GameObject("SlotsLockLabel", typeof(RectTransform));
                labelGo.transform.SetParent(transform, false);
                lockLabel = labelGo.AddComponent<TextMeshProUGUI>();
            }

            if (lockLabel == null) return;

            var rt = lockLabel.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            lockLabel.alignment = TextAlignmentOptions.Center;
            lockLabel.fontStyle = FontStyles.Bold;
            lockLabel.fontSize = 42f;
            lockLabel.color = new Color(0.92f, 0.92f, 0.94f, 1f);
            lockLabel.raycastTarget = false;
            lockLabel.enableWordWrapping = false;
            lockLabel.text = $"{UnlockLevel}\n<size=55%>LEVEL</size>";

            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts/rimouski sb SDF");
            if (font != null)
                lockLabel.font = font;
        }

        private void CacheHiddenContent()
        {
            if (hiddenContentGroups != null) return;

            var texts = GetComponentsInChildren<TMP_Text>(true);
            var groups = new System.Collections.Generic.List<CanvasGroup>();
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] == null || texts[i] == lockLabel) continue;
                if (texts[i].transform.IsChildOf(transform) == false) continue;
                if (texts[i].name == "SlotsLockLabel") continue;

                CanvasGroup cg = texts[i].GetComponent<CanvasGroup>();
                if (cg == null)
                    cg = texts[i].gameObject.AddComponent<CanvasGroup>();
                groups.Add(cg);
            }

            // Also hide non-lock decorative images that are children (not the button root image).
            var images = GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] == null || images[i] == buttonImage) continue;
                if (images[i].transform == transform) continue;
                if (images[i].name.StartsWith("__") || images[i].name == "SlotsLockLabel") continue;

                CanvasGroup cg = images[i].GetComponent<CanvasGroup>();
                if (cg == null)
                    cg = images[i].gameObject.AddComponent<CanvasGroup>();
                if (!groups.Contains(cg))
                    groups.Add(cg);
            }

            hiddenContentGroups = groups.ToArray();
        }

        public static void ShowLockedFloatingHint()
        {
            Canvas canvas = FindMenuCanvas();
            if (canvas == null) return;

            SlotsUnlockHintRunner runner = canvas.GetComponent<SlotsUnlockHintRunner>();
            if (runner == null)
                runner = canvas.gameObject.AddComponent<SlotsUnlockHintRunner>();

            runner.Play(LockedHint);
        }

        private static Canvas FindMenuCanvas()
        {
            Canvas[] all = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                Canvas c = all[i];
                if (c == null || c.name.Contains("Dev")) continue;
                if (c.renderMode == RenderMode.ScreenSpaceOverlay || c.renderMode == RenderMode.ScreenSpaceCamera)
                    return c.rootCanvas != null ? c.rootCanvas : c;
            }

            return all.Length > 0 ? all[0] : null;
        }
    }

    /// <summary>
    /// Tiny host on the menu canvas that runs the floating unlock hint coroutine.
    /// </summary>
    public class SlotsUnlockHintRunner : MonoBehaviour
    {
        private Coroutine routine;
        private GameObject hintObj;

        public void Play(string message)
        {
            if (routine != null)
                StopCoroutine(routine);
            routine = StartCoroutine(PlayRoutine(message));
        }

        private IEnumerator PlayRoutine(string message)
        {
            EnsureHintObject();
            if (hintObj == null) yield break;

            var tmp = hintObj.GetComponent<TextMeshProUGUI>();
            var cg = hintObj.GetComponent<CanvasGroup>();
            var rt = hintObj.GetComponent<RectTransform>();
            if (tmp == null || cg == null || rt == null) yield break;

            tmp.text = message;
            hintObj.SetActive(true);
            hintObj.transform.SetAsLastSibling();

            // Start a bit below screen center, float upward.
            Vector2 start = new Vector2(0f, -40f);
            Vector2 end = new Vector2(0f, 70f);
            rt.anchoredPosition = start;
            cg.alpha = 0f;

            float fadeIn = 0.28f;
            float hold = 2.2f;
            float fadeOut = 0.45f;
            float total = fadeIn + hold + fadeOut;
            float elapsed = 0f;

            while (elapsed < total)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / total);
                // Smooth upward drift over the whole lifetime.
                float ease = 1f - Mathf.Pow(1f - t, 2.2f);
                rt.anchoredPosition = Vector2.Lerp(start, end, ease);

                if (elapsed < fadeIn)
                    cg.alpha = Mathf.Clamp01(elapsed / fadeIn);
                else if (elapsed < fadeIn + hold)
                    cg.alpha = 1f;
                else
                    cg.alpha = 1f - Mathf.Clamp01((elapsed - fadeIn - hold) / fadeOut);

                yield return null;
            }

            cg.alpha = 0f;
            hintObj.SetActive(false);
            routine = null;
        }

        private void EnsureHintObject()
        {
            if (hintObj != null) return;

            Transform existing = transform.Find("SlotsUnlockFloatingHint");
            if (existing != null)
            {
                hintObj = existing.gameObject;
                return;
            }

            hintObj = new GameObject("SlotsUnlockFloatingHint", typeof(RectTransform), typeof(CanvasGroup));
            hintObj.transform.SetParent(transform, false);

            var rt = hintObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(720f, 120f);
            rt.anchoredPosition = new Vector2(0f, -40f);

            var tmp = hintObj.AddComponent<TextMeshProUGUI>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            tmp.fontSize = 34f;
            tmp.color = Color.white;
            tmp.enableWordWrapping = true;
            tmp.raycastTarget = false;

            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts/rimouski sb SDF");
            if (font != null)
                tmp.font = font;

            hintObj.SetActive(false);
        }
    }
}
