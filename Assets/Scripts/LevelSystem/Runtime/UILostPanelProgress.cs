using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace LegoPuzzle.Runtime
{
    /// <summary>
    /// Displays a motivating "SO CLOSE!" progress bar on the Lost Panel:
    /// Shows how many LEGO pieces were cleared (e.g. "4 / 5 Pieces Cleared • 80%")
    /// with a smooth animated progress bar fill that triggers when the panel lands.
    /// </summary>
    public class UILostPanelProgress : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Delay in seconds after panel appears before animating the progress bar")]
        [SerializeField] private float startDelay = 0.38f;

        [Tooltip("Duration of the progress bar fill animation in seconds")]
        [SerializeField] private float fillDuration = 0.65f;

        private RectTransform widgetRoot;
        private TMP_Text headerText;
        private TMP_Text statsText;
        private RectTransform barTrack;
        private RectTransform barFill;
        private Image fillImage;
        private TMP_Text percentText;

        private LevelLoader levelLoader;
        private Coroutine animateCoroutine;
        private bool isBuilt = false;

        private const float BAR_WIDTH = 420f;
        private const float BAR_HEIGHT = 24f;

        private void Awake()
        {
            EnsureWidgetBuilt();
        }

        private void OnEnable()
        {
            EnsureWidgetBuilt();
            UpdateAndPlayProgress();
        }

        private void OnDisable()
        {
            if (animateCoroutine != null)
            {
                StopCoroutine(animateCoroutine);
                animateCoroutine = null;
            }
        }

        private void EnsureWidgetBuilt()
        {
            if (isBuilt && widgetRoot != null) return;

            Transform targetParent = transform.Find("DialogPanel");
            if (targetParent == null) targetParent = transform;

            Transform existing = targetParent.Find("__ProgressWidget");
            if (existing != null)
            {
                widgetRoot = existing.GetComponent<RectTransform>();
            }
            else
            {
                GameObject wObj = new GameObject("__ProgressWidget");
                wObj.transform.SetParent(targetParent, false);
                widgetRoot = wObj.AddComponent<RectTransform>();
                widgetRoot.anchorMin = new Vector2(0.5f, 0.5f);
                widgetRoot.anchorMax = new Vector2(0.5f, 0.5f);
                widgetRoot.pivot = new Vector2(0.5f, 0.5f);
                // Placed in the comfortable space between subtitle and retry button
                widgetRoot.anchoredPosition = new Vector2(0f, 25f);
                widgetRoot.sizeDelta = new Vector2(520f, 150f);
            }

            TMP_FontAsset sampleFont = GetSampleFont();

            // 1. Header ("SO CLOSE!")
            Transform hTr = widgetRoot.Find("Header");
            if (hTr == null)
            {
                GameObject hObj = new GameObject("Header");
                hObj.transform.SetParent(widgetRoot, false);
                RectTransform hRt = hObj.AddComponent<RectTransform>();
                hRt.anchorMin = new Vector2(0.5f, 1f);
                hRt.anchorMax = new Vector2(0.5f, 1f);
                hRt.pivot = new Vector2(0.5f, 1f);
                hRt.anchoredPosition = new Vector2(0f, 0f);
                hRt.sizeDelta = new Vector2(500f, 40f);

                headerText = hObj.AddComponent<TextMeshProUGUI>();
                if (sampleFont != null) headerText.font = sampleFont;
                headerText.fontSize = 28;
                headerText.fontStyle = FontStyles.Bold;
                headerText.alignment = TextAlignmentOptions.Center;
                headerText.color = new Color(1f, 0.84f, 0.25f, 1f); // Warm gold / yellow
                headerText.text = "SO CLOSE!";
            }
            else
            {
                headerText = hTr.GetComponent<TMP_Text>();
            }

            // 2. Stats Text ("4 / 5 Pieces Cleared")
            Transform sTr = widgetRoot.Find("StatsText");
            if (sTr == null)
            {
                GameObject sObj = new GameObject("StatsText");
                sObj.transform.SetParent(widgetRoot, false);
                RectTransform sRt = sObj.AddComponent<RectTransform>();
                sRt.anchorMin = new Vector2(0.5f, 1f);
                sRt.anchorMax = new Vector2(0.5f, 1f);
                sRt.pivot = new Vector2(0.5f, 1f);
                sRt.anchoredPosition = new Vector2(0f, -44f);
                sRt.sizeDelta = new Vector2(500f, 32f);

                statsText = sObj.AddComponent<TextMeshProUGUI>();
                if (sampleFont != null) statsText.font = sampleFont;
                statsText.fontSize = 22;
                statsText.fontStyle = FontStyles.Normal;
                statsText.alignment = TextAlignmentOptions.Center;
                statsText.color = new Color(0.92f, 0.95f, 1f, 0.95f);
                statsText.text = "0 / 0 Pieces Cleared";
            }
            else
            {
                statsText = sTr.GetComponent<TMP_Text>();
            }

            // 3. Progress Bar Track (Background)
            Transform trkTr = widgetRoot.Find("BarTrack");
            if (trkTr == null)
            {
                GameObject trkObj = new GameObject("BarTrack");
                trkObj.transform.SetParent(widgetRoot, false);
                barTrack = trkObj.AddComponent<RectTransform>();
                barTrack.anchorMin = new Vector2(0.5f, 0f);
                barTrack.anchorMax = new Vector2(0.5f, 0f);
                barTrack.pivot = new Vector2(0.5f, 0f);
                barTrack.anchoredPosition = new Vector2(0f, 15f);
                barTrack.sizeDelta = new Vector2(BAR_WIDTH, BAR_HEIGHT);

                Image trkImg = trkObj.AddComponent<Image>();
                trkImg.color = new Color(0f, 0f, 0f, 0.42f);
                trkImg.type = Image.Type.Sliced;
                trkImg.sprite = GetDefaultRoundedSprite();

                // 4. Progress Bar Fill
                GameObject fillObj = new GameObject("BarFill");
                fillObj.transform.SetParent(barTrack, false);
                barFill = fillObj.AddComponent<RectTransform>();
                barFill.anchorMin = new Vector2(0f, 0f);
                barFill.anchorMax = new Vector2(0f, 1f);
                barFill.pivot = new Vector2(0f, 0.5f);
                barFill.anchoredPosition = Vector2.zero;
                barFill.sizeDelta = new Vector2(0f, 0f);

                fillImage = fillObj.AddComponent<Image>();
                fillImage.color = new Color(1f, 0.65f, 0.08f, 1f); // Vibrant amber-orange
                fillImage.type = Image.Type.Sliced;
                fillImage.sprite = GetDefaultRoundedSprite();

                // 5. Percent Text on top of the bar
                GameObject pctObj = new GameObject("PercentText");
                pctObj.transform.SetParent(barTrack, false);
                RectTransform pctRt = pctObj.AddComponent<RectTransform>();
                pctRt.anchorMin = Vector2.zero;
                pctRt.anchorMax = Vector2.one;
                pctRt.offsetMin = Vector2.zero;
                pctRt.offsetMax = Vector2.zero;

                percentText = pctObj.AddComponent<TextMeshProUGUI>();
                if (sampleFont != null) percentText.font = sampleFont;
                percentText.fontSize = 17;
                percentText.fontStyle = FontStyles.Bold;
                percentText.alignment = TextAlignmentOptions.Center;
                percentText.color = Color.white;
                percentText.text = "0%";
            }
            else
            {
                barTrack = trkTr.GetComponent<RectTransform>();
                Transform fTr = barTrack.Find("BarFill");
                if (fTr != null) barFill = fTr.GetComponent<RectTransform>();
                Transform pTr = barTrack.Find("PercentText");
                if (pTr != null) percentText = pTr.GetComponent<TMP_Text>();
            }

            isBuilt = true;
        }

        public void UpdateAndPlayProgress()
        {
            if (levelLoader == null)
            {
                levelLoader = FindAnyObjectByType<LevelLoader>();
            }

            int cleared = levelLoader != null ? levelLoader.PiecesExited : 0;
            int total = levelLoader != null ? levelLoader.RequiredPiecesToWin : 1;
            if (total <= 0) total = 1;
            float progress = Mathf.Clamp01((float)cleared / total);

            if (headerText != null)
            {
                headerText.text = progress >= 0.6f ? "SO CLOSE!" : (progress >= 0.35f ? "ALMOST HAD IT!" : "GOOD EFFORT!");
            }

            if (statsText != null)
            {
                statsText.text = $"{cleared} / {total} Pieces Cleared";
            }

            if (barFill != null)
            {
                barFill.sizeDelta = new Vector2(0f, 0f);
            }

            if (percentText != null)
            {
                percentText.text = "0%";
            }

            if (animateCoroutine != null)
            {
                StopCoroutine(animateCoroutine);
            }

            if (gameObject.activeInHierarchy)
            {
                animateCoroutine = StartCoroutine(AnimateProgressRoutine(progress));
            }
        }

        private IEnumerator AnimateProgressRoutine(float targetProgress)
        {
            if (startDelay > 0f)
            {
                yield return new WaitForSecondsRealtime(startDelay);
            }

            float targetW = BAR_WIDTH * targetProgress;
            float elapsed = 0f;

            while (elapsed < fillDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fillDuration);
                // Smooth cubic ease-out
                float eased = 1f - Mathf.Pow(1f - t, 3f);

                float currentW = Mathf.Lerp(0f, targetW, eased);
                if (barFill != null)
                {
                    barFill.sizeDelta = new Vector2(currentW, 0f);
                }

                if (percentText != null)
                {
                    int pct = Mathf.RoundToInt(Mathf.Lerp(0f, targetProgress * 100f, eased));
                    percentText.text = $"{pct}%";
                }

                yield return null;
            }

            if (barFill != null) barFill.sizeDelta = new Vector2(targetW, 0f);
            if (percentText != null) percentText.text = $"{Mathf.RoundToInt(targetProgress * 100f)}%";

            animateCoroutine = null;
        }

        private TMP_FontAsset GetSampleFont()
        {
            var tmp = GetComponentInChildren<TMP_Text>(true);
            if (tmp != null && tmp.font != null) return tmp.font;

#if UNITY_EDITOR
            var f = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/rimouski sb SDF.asset");
            if (f != null) return f;
#endif
            return null;
        }

        private static Sprite cachedRoundedSprite;

        private Sprite GetDefaultRoundedSprite()
        {
            if (cachedRoundedSprite != null) return cachedRoundedSprite;

            int size = 32;
            int cornerRadius = 8;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color32[] cols = new Color32[size * size];
            Color32 white = new Color32(255, 255, 255, 255);
            Color32 clear = new Color32(255, 255, 255, 0);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int cx = x < cornerRadius ? cornerRadius - x : (x >= size - cornerRadius ? x - (size - cornerRadius - 1) : 0);
                    int cy = y < cornerRadius ? cornerRadius - y : (y >= size - cornerRadius ? y - (size - cornerRadius - 1) : 0);
                    if (cx * cx + cy * cy <= cornerRadius * cornerRadius)
                    {
                        cols[y * size + x] = white;
                    }
                    else
                    {
                        cols[y * size + x] = clear;
                    }
                }
            }
            tex.SetPixels32(cols);
            tex.Apply();

            cachedRoundedSprite = Sprite.Create(
                tex,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(cornerRadius, cornerRadius, cornerRadius, cornerRadius)
            );
            return cachedRoundedSprite;
        }

        public static UILostPanelProgress AttachTo(GameObject panel)
        {
            if (panel == null) return null;
            var progress = panel.GetComponent<UILostPanelProgress>();
            if (progress == null)
            {
                progress = panel.AddComponent<UILostPanelProgress>();
            }
            return progress;
        }
    }
}
