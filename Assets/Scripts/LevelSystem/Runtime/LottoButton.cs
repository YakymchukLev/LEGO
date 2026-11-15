using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LegoPuzzle.Runtime
{
    /// <summary>
    /// Menu Lotto button. Locked until the player reaches level 20.
    /// When locked: gray button with "20 / LEVEL". Tap shows a floating unlock hint.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class LottoButton : MonoBehaviour
    {
        public const int UnlockLevel = 20;
        public const string LockedHint = "Available from Level 20";

        [Header("Juicy Polish")]
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
                    if (Application.isPlaying)
                        Destroy(shine);
                    else
                        DestroyImmediate(shine);

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
            if (!IsLottoUnlocked())
            {
                ShowLockedFloatingHint();
                return;
            }

            if (LottoBoard.Instance != null)
            {
                LottoBoard.Instance.OnLottoButton();
                return;
            }

            var board = FindAnyObjectByType<LottoBoard>(FindObjectsInactive.Include);
            if (board != null)
                board.OnLottoButton();
        }

        public static bool IsLottoUnlocked()
        {
            return SlotsButton.GetPlayerLevelNumber() >= UnlockLevel;
        }

        public void RefreshLockVisual()
        {
            bool locked = !IsLottoUnlocked();
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

            if (button != null)
                button.interactable = true;
        }

        private void LateUpdate()
        {
            bool locked = !IsLottoUnlocked();
            if (locked != wasLocked)
                RefreshLockVisual();
        }

        private void EnsureLockLabel()
        {
            if (lockLabel != null) return;

            Transform existing = transform.Find("LottoLockLabel");
            GameObject labelGo;
            if (existing != null)
            {
                labelGo = existing.gameObject;
                lockLabel = labelGo.GetComponent<TextMeshProUGUI>();
            }
            else
            {
                labelGo = new GameObject("LottoLockLabel", typeof(RectTransform));
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
                if (texts[i].name == "LottoLockLabel" || texts[i].name == "SlotsLockLabel") continue;

                CanvasGroup cg = texts[i].GetComponent<CanvasGroup>();
                if (cg == null)
                    cg = texts[i].gameObject.AddComponent<CanvasGroup>();
                groups.Add(cg);
            }

            var images = GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] == null || images[i] == buttonImage) continue;
                if (images[i].transform == transform) continue;
                if (images[i].name.StartsWith("__") || images[i].name == "LottoLockLabel") continue;

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
}
