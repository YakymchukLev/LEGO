using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LegoPuzzle.Runtime
{
    /// <summary>
    /// Дошка лото: закритий слот показує «ВІДКРИТИ», відкритий — двозначне число.
    /// Верхній ряд чисел змінюється, коли закінчується таймер.
    /// Відкриття панелі і слотів іде через кнопку з іменем LottoButton.
    /// </summary>
    [ExecuteAlways]
    public class LottoBoard : MonoBehaviour
    {
        public static LottoBoard Instance { get; private set; }

        [Header("Таймер")]
        [Tooltip("Якщо увімкнено, лото оновлюється кожні 30 секунд. Інакше оновлення опівночі за годинником пристрою.")]
        [SerializeField] private bool refreshEveryThirtySeconds;

        [Header("Поле")]
        [SerializeField] private int headerCount = 6;
        [SerializeField] private int columns = 3;
        [SerializeField] private int rows = 3;

        [Tooltip("Скільки віконець гарантовано отримують число з верхнього ряду.")]
        [Min(2)]
        [SerializeField] private int guaranteedMatches = 2;

        [Tooltip("Шанс додаткового збігу для кожного віконця понад гарантовані.")]
        [Range(0f, 1f)]
        [SerializeField] private float extraMatchChance = 0.1f;

        [Tooltip("Спрайт жовтих закритих віконець лото.")]
        [SerializeField] private Sprite slotSprite;

        [Tooltip("Спрайт відкритих віконець лото.")]
        [SerializeField] private Sprite openSlotSprite;

        [Header("Оплата слота")]
        [Tooltip("Скільки алмазів коштує відкрити одне віконце.")]
        [Min(0)]
        [SerializeField] private int slotGemCost = 10;

        private const string ClosedLabel = "OPEN";
        private const float ShortRoundSeconds = 30f;

        private GameObject visualRoot;
        private TMP_Text timerLabel;
        private TMP_Text[] headerLabels;
        private Image[] slotFaces;
        private TMP_Text[] slotLabels;
        private bool[] slotOpen;
        private int[] headerNumbers;
        private int[] slotNumbers;
        private Coroutine[] slotFlips;

        private Button lottoButton;
        private float timeLeft;
        private long roundEndsUtcTicks;
        private string roundLocalDate;
        private bool boardVisible;
        private RectTransform boardRect;
        private CanvasGroup panelGroup;
        private Vector2 boardRestPosition;
        private bool boardPoseCached;
        private Coroutine panelRoutine;
        private GameObject payPanel;
        private Image payGemIcon;
        private TMP_Text payGemCostLabel;
        private TMP_Text payMessage;
        private RectTransform payGemButtonRect;
        private int pendingSlot = -1;
        private static Sprite gemSprite;
        private UIConfettiEffect matchConfetti;

        private const float OpenDuration = 0.46f;
        private const float CloseDuration = 0.24f;
        private const float JellyIntensity = 0.12f;
        private const float OvershootDistance = 28f;

        private static Sprite roundedSprite;

        private readonly Color headerColor = new Color(0.49f, 0.36f, 0.95f, 1f);
        private readonly Color headerInner = new Color(0.62f, 0.50f, 1f, 1f);
        private readonly Color closedColor = new Color(1f, 0.78f, 0.16f, 1f);
        private readonly Color openColor = new Color(0.10f, 0.62f, 0.22f, 1f);
        private readonly Color missColor = new Color(0.22f, 0.24f, 0.28f, 1f);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            EnsureBoardExists();
            if (Application.isPlaying)
                HideBoard(true);
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                EnsureBoardExists();
                return;
            }

            BindLottoButton();
        }

        private void Start()
        {
            if (!Application.isPlaying) return;
            LoadOrStartRound();
            RefreshTimerLabel();
        }

        private void OnApplicationPause(bool paused)
        {
            if (!Application.isPlaying) return;
            if (paused)
                SaveLottoState();
            else
                CatchRoundExpiry();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!Application.isPlaying) return;
            if (hasFocus)
                CatchRoundExpiry();
            else
                SaveLottoState();
        }

        private void OnApplicationQuit()
        {
            SaveLottoState();
        }

        private void OnDisable()
        {
            UnbindLottoButton();
            if (Application.isPlaying)
            {
                SaveLottoState();
                StopPanelAnimation(true);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            if (!Application.isPlaying) return;

            CatchRoundExpiry();
            RefreshTimerLabel();
        }

        /// <summary>
        /// Кнопка LottoButton: спочатку показує дошку, далі кожне натискання відкриває наступний закритий слот.
        /// </summary>
        public void OnLottoButton()
        {
            EnsureVisual();

            if (!boardVisible)
            {
                ShowBoard();
                return;
            }

            RevealNextClosedSlot();
        }

        public void ShowBoard()
        {
            EnsureVisual();
            CatchRoundExpiry();
            if (headerNumbers == null)
                BeginNewRound();

            visualRoot.SetActive(true);
            boardVisible = true;
            if (!Application.isPlaying)
            {
                ResetPanelPose();
                return;
            }

            if (panelRoutine != null)
                StopCoroutine(panelRoutine);
            panelRoutine = StartCoroutine(AnimateOpenRoutine());
        }

        public void HideBoard()
        {
            HideBoard(false);
        }

        private void HideBoard(bool instant)
        {
            boardVisible = false;
            HidePayPanel();
            if (visualRoot == null) return;

            if (instant || !Application.isPlaying || !visualRoot.activeInHierarchy)
            {
                StopPanelAnimation(true);
                visualRoot.SetActive(false);
                return;
            }

            if (panelRoutine != null)
                StopCoroutine(panelRoutine);
            panelRoutine = StartCoroutine(AnimateCloseRoutine());
        }

        private void CacheBoardPose()
        {
            if (visualRoot == null) return;

            if (boardRect == null)
            {
                Transform board = visualRoot.transform.Find("Board");
                if (board != null)
                    boardRect = board as RectTransform;
            }

            if (boardRect != null && !boardPoseCached)
            {
                boardRestPosition = Mathf.Abs(boardRect.anchoredPosition.y) > 600f
                    ? Vector2.zero
                    : boardRect.anchoredPosition;
                boardPoseCached = true;
            }

            if (panelGroup == null)
                panelGroup = visualRoot.GetComponent<CanvasGroup>();
            if (panelGroup == null && Application.isPlaying)
                panelGroup = visualRoot.AddComponent<CanvasGroup>();
        }

        private void ResetPanelPose()
        {
            CacheBoardPose();
            if (boardRect != null)
            {
                boardRect.anchoredPosition = boardRestPosition;
                boardRect.localScale = Vector3.one;
            }

            if (panelGroup != null)
            {
                panelGroup.alpha = 1f;
                panelGroup.blocksRaycasts = true;
            }
        }

        private void StopPanelAnimation(bool resetPose)
        {
            if (panelRoutine != null)
            {
                StopCoroutine(panelRoutine);
                panelRoutine = null;
            }

            if (resetPose)
                ResetPanelPose();
        }

        private Vector2 OffscreenBoardPosition()
        {
            if (boardRect == null) return new Vector2(boardRestPosition.x, boardRestPosition.y - 1600f);

            float parentHeight = 1920f;
            RectTransform parentRt = boardRect.parent as RectTransform;
            if (parentRt != null && parentRt.rect.height > 100f)
                parentHeight = parentRt.rect.height;

            float cardHeight = boardRect.rect.height > 50f ? boardRect.rect.height : 980f;
            float cardTopExtent = cardHeight * (1f - boardRect.pivot.y);
            float offscreenY = -(parentHeight * 0.5f + cardTopExtent + 140f);
            return new Vector2(boardRestPosition.x, offscreenY);
        }

        private IEnumerator AnimateOpenRoutine()
        {
            CacheBoardPose();
            if (boardRect == null) yield break;

            Vector2 startPos = OffscreenBoardPosition();
            boardRect.anchoredPosition = startPos;
            boardRect.localScale = new Vector3(0.58f, 0.58f, 1f);
            if (panelGroup != null)
            {
                panelGroup.alpha = 0f;
                panelGroup.blocksRaycasts = true;
            }

            float elapsed = 0f;
            while (elapsed < OpenDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / OpenDuration);

                float travelT = Mathf.Clamp01(t / 0.62f);
                float baseTravel = 1f - Mathf.Pow(1f - travelT, 3f);
                float baseY = Mathf.Lerp(startPos.y, boardRestPosition.y, baseTravel);

                float bounceOffset = 0f;
                if (t >= 0.45f)
                {
                    float bt = (t - 0.45f) / 0.55f;
                    float decay = (1f - bt) * (1f - bt);
                    bounceOffset = Mathf.Sin(bt * Mathf.PI * 2.2f) * OvershootDistance * decay;
                }

                boardRect.anchoredPosition = new Vector2(boardRestPosition.x, baseY + bounceOffset);

                float growT = 1f - Mathf.Pow(1f - t, 2.6f);
                float baseScale = Mathf.Lerp(0.58f, 1f, growT);
                float jelly;
                if (t < 0.45f)
                {
                    float flightP = t / 0.45f;
                    jelly = Mathf.Sin(flightP * Mathf.PI) * JellyIntensity;
                }
                else
                {
                    float impactP = (t - 0.45f) / 0.55f;
                    float decay = (1f - impactP) * (1f - impactP);
                    jelly = -Mathf.Sin(impactP * Mathf.PI * 2f) * (JellyIntensity * 0.85f) * decay;
                }

                boardRect.localScale = new Vector3(baseScale * (1f - jelly), baseScale * (1f + jelly), 1f);
                if (panelGroup != null)
                    panelGroup.alpha = Mathf.Clamp01(t * 2.8f);

                yield return null;
            }

            ResetPanelPose();
            panelRoutine = null;
        }

        private IEnumerator AnimateCloseRoutine()
        {
            CacheBoardPose();
            if (boardRect == null)
            {
                if (visualRoot != null)
                    visualRoot.SetActive(false);
                yield break;
            }

            Vector2 startPos = boardRect.anchoredPosition;
            Vector2 offscreenPos = OffscreenBoardPosition();
            Vector3 initialScale = boardRect.localScale;
            float startAlpha = panelGroup != null ? panelGroup.alpha : 1f;

            float elapsed = 0f;
            while (elapsed < CloseDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / CloseDuration);
                float moveProgress;
                float sx;
                float sy;

                if (t < 0.18f)
                {
                    float p = t / 0.18f;
                    float anticip = Mathf.Sin(p * Mathf.PI);
                    sx = Mathf.Lerp(initialScale.x, 1.04f, anticip);
                    sy = Mathf.Lerp(initialScale.y, 0.95f, anticip);
                    moveProgress = 0f;
                }
                else
                {
                    float p = (t - 0.18f) / 0.82f;
                    moveProgress = p * p * p;
                    sx = Mathf.Lerp(1.04f, 0.88f, p);
                    sy = Mathf.Lerp(0.95f, 1.10f, p);
                }

                boardRect.anchoredPosition = Vector2.Lerp(startPos, offscreenPos, moveProgress);
                boardRect.localScale = new Vector3(sx, sy, 1f);
                if (panelGroup != null)
                    panelGroup.alpha = Mathf.Lerp(startAlpha, 0f, Mathf.Clamp01(t * 1.5f));

                yield return null;
            }

            ResetPanelPose();
            if (visualRoot != null)
                visualRoot.SetActive(false);
            panelRoutine = null;
        }

        private void RevealNextClosedSlot()
        {
            if (slotOpen == null) return;

            for (int i = 0; i < slotOpen.Length; i++)
            {
                if (slotOpen[i]) continue;
                AskToOpenSlot(i);
                return;
            }
        }

        private void RevealSlot(int index)
        {
            if (slotOpen == null || index < 0 || index >= slotOpen.Length || slotOpen[index])
                return;

            slotOpen[index] = true;
            if (slotNumbers == null || index >= slotNumbers.Length)
                AssignSlotNumbers();
            SaveLottoState();

            int number = slotNumbers[index];
            Transform slotRoot = slotFaces[index].transform.parent;
            var slotButton = slotRoot.GetComponent<Button>();
            if (slotButton != null)
            {
                KeepSlotOpaque(slotButton);
                slotButton.interactable = false;
            }

            if (slotFlips == null || slotFlips.Length != slotOpen.Length)
                slotFlips = new Coroutine[slotOpen.Length];

            if (slotFlips[index] != null)
                StopCoroutine(slotFlips[index]);

            slotFlips[index] = StartCoroutine(FlipSlotOpen(index, number, slotRoot));
        }

        private IEnumerator FlipSlotOpen(int index, int number, Transform slotRoot)
        {
            const float halfDuration = 0.14f;

            float elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float width = Mathf.Lerp(1f, 0f, Mathf.Clamp01(elapsed / halfDuration));
                slotRoot.localScale = new Vector3(width, 1f, 1f);
                yield return null;
            }

            slotRoot.localScale = new Vector3(0f, 1f, 1f);
            ShowOpenedSlot(index, number);
            if (NumberIsInHeader(number))
                PlayMatchConfetti();

            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float width = Mathf.Lerp(0f, 1f, Mathf.Clamp01(elapsed / halfDuration));
                slotRoot.localScale = new Vector3(width, 1f, 1f);
                yield return null;
            }

            slotRoot.localScale = Vector3.one;
            if (slotFlips != null && index < slotFlips.Length)
                slotFlips[index] = null;
        }

        private void RollHeaderNumbers()
        {
            headerCount = Mathf.Max(1, headerCount);
            headerNumbers = new int[headerCount];
            var used = new HashSet<int>();

            for (int i = 0; i < headerCount; i++)
            {
                int value;
                do
                {
                    value = Random.Range(10, 100);
                }
                while (!used.Add(value));

                headerNumbers[i] = value;
                if (headerLabels != null && i < headerLabels.Length && headerLabels[i] != null)
                    headerLabels[i].text = value.ToString();
            }

            AssignSlotNumbers();
            CloseAllSlots();
        }

        private void AssignSlotNumbers()
        {
            int count = slotOpen != null ? slotOpen.Length : Mathf.Max(1, columns * rows);
            slotNumbers = new int[count];

            var headerSet = new HashSet<int>();
            if (headerNumbers != null)
            {
                for (int i = 0; i < headerNumbers.Length; i++)
                    headerSet.Add(headerNumbers[i]);
            }

            var otherPool = new List<int>(90);
            var matchPool = new List<int>(headerSet);
            for (int n = 10; n <= 99; n++)
            {
                if (!headerSet.Contains(n))
                    otherPool.Add(n);
            }

            Shuffle(otherPool);
            Shuffle(matchPool);

            var used = new HashSet<int>();
            int otherIndex = 0;
            int matchIndex = 0;
            int matchesToPlace = Mathf.Min(guaranteedMatches, count, matchPool.Count);

            var matchSlots = new HashSet<int>();
            while (matchSlots.Count < matchesToPlace)
                matchSlots.Add(Random.Range(0, count));

            for (int i = 0; i < count; i++)
            {
                bool extraMatch = !matchSlots.Contains(i)
                    && matchIndex < matchPool.Count
                    && Random.value < extraMatchChance;

                int value = matchSlots.Contains(i) || extraMatch
                    ? TakeUnused(matchPool, ref matchIndex, used)
                    : TakeUnused(otherPool, ref otherIndex, used);

                if (value < 0)
                    value = TakeUnused(otherPool, ref otherIndex, used);
                if (value < 0)
                    value = TakeUnused(matchPool, ref matchIndex, used);

                if (value < 0)
                {
                    for (int n = 10; n <= 99; n++)
                    {
                        if (used.Add(n))
                        {
                            value = n;
                            break;
                        }
                    }
                }

                slotNumbers[i] = value;
            }
        }

        private static int TakeUnused(List<int> source, ref int index, HashSet<int> used)
        {
            while (index < source.Count)
            {
                int value = source[index++];
                if (used.Add(value))
                    return value;
            }

            return -1;
        }

        private static void Shuffle(List<int> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int swap = Random.Range(0, i + 1);
                int temp = list[i];
                list[i] = list[swap];
                list[swap] = temp;
            }
        }

        private bool NumberIsInHeader(int number)
        {
            if (headerNumbers == null) return false;
            for (int i = 0; i < headerNumbers.Length; i++)
            {
                if (headerNumbers[i] == number)
                    return true;
            }

            return false;
        }

        private void CloseAllSlots()
        {
            if (slotOpen == null) return;

            for (int i = 0; i < slotOpen.Length; i++)
                CloseSlot(i);
        }

        private void CloseSlot(int index)
        {
            if (slotOpen == null || index < 0 || index >= slotOpen.Length) return;

            slotOpen[index] = false;
            if (slotFlips != null && index < slotFlips.Length && slotFlips[index] != null)
            {
                StopCoroutine(slotFlips[index]);
                slotFlips[index] = null;
            }

            if (slotLabels != null && index < slotLabels.Length && slotLabels[index] != null)
            {
                slotLabels[index].text = ClosedLabel;
                slotLabels[index].fontSize = 28f;
                slotLabels[index].color = Color.white;
            }

            if (slotFaces == null || index >= slotFaces.Length || slotFaces[index] == null)
                return;

            slotFaces[index].enabled = false;
            Transform slotRoot = slotFaces[index].transform.parent;
            var frame = slotRoot.GetComponent<Image>();
            if (frame != null)
            {
                Sprite sprite = SlotSprite();
                if (sprite != null)
                {
                    frame.sprite = sprite;
                    frame.type = Image.Type.Simple;
                }

                frame.color = Color.white;
            }

            slotRoot.localScale = Vector3.one;
            var slotButton = slotRoot.GetComponent<Button>();
            if (slotButton != null)
                slotButton.interactable = true;
        }

        private void ShowOpenedSlot(int index, int number)
        {
            if (slotFaces != null && index < slotFaces.Length && slotFaces[index] != null)
                slotFaces[index].enabled = false;

            Transform slotRoot = slotFaces != null && index < slotFaces.Length && slotFaces[index] != null
                ? slotFaces[index].transform.parent
                : null;
            if (slotRoot == null) return;

            var frame = slotRoot.GetComponent<Image>();
            if (frame != null)
            {
                Sprite opened = OpenSlotSprite();
                if (opened != null)
                {
                    frame.sprite = opened;
                    frame.type = Image.Type.Simple;
                }

                frame.color = NumberIsInHeader(number) ? openColor : missColor;
                frame.enabled = true;
            }

            if (slotLabels != null && index < slotLabels.Length && slotLabels[index] != null)
            {
                slotLabels[index].text = number.ToString();
                slotLabels[index].fontSize = 54f;
                slotLabels[index].color = Color.white;
            }

            var slotButton = slotRoot.GetComponent<Button>();
            if (slotButton != null)
            {
                KeepSlotOpaque(slotButton);
                slotButton.interactable = false;
            }
        }

        private void PlayMatchConfetti()
        {
            if (!Application.isPlaying) return;

            if (matchConfetti == null)
            {
                Transform existing = transform.Find("LottoMatchConfetti");
                if (existing != null)
                    matchConfetti = existing.GetComponent<UIConfettiEffect>();

                if (matchConfetti == null)
                {
                    var go = new GameObject("LottoMatchConfetti", typeof(RectTransform));
                    go.SetActive(false);
                    go.transform.SetParent(transform, false);
                    var rect = go.GetComponent<RectTransform>();
                    rect.anchorMin = new Vector2(0.5f, 0.5f);
                    rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = new Vector2(760f, 980f);
                    matchConfetti = go.AddComponent<UIConfettiEffect>();
                }
            }

            matchConfetti.transform.SetAsLastSibling();
            if (!matchConfetti.gameObject.activeSelf)
                matchConfetti.gameObject.SetActive(true);
            else
                matchConfetti.Play();
        }

        private void LoadOrStartRound()
        {
            int slotCount = SlotCount();
            GameSaveData data = SaveDataService.HasInstance ? SaveDataService.Instance.Data : null;
            string today = LocalTodayKey();
            bool numbersMatch = data != null
                && data.lottoHeaderNumbers != null
                && data.lottoHeaderNumbers.Length == headerCount
                && data.lottoSlotNumbers != null
                && data.lottoSlotNumbers.Length == slotCount
                && data.lottoSlotsOpen != null
                && data.lottoSlotsOpen.Length == slotCount
                && data.lottoUsesShortTimer == refreshEveryThirtySeconds
                && data.lottoRoundEndsUtcTicks > System.DateTime.UtcNow.Ticks;
            bool sameLocalDay = numbersMatch && (refreshEveryThirtySeconds || data.lottoRoundLocalDate == today);
            bool legacyStillRunning = numbersMatch
                && !refreshEveryThirtySeconds
                && string.IsNullOrEmpty(data.lottoRoundLocalDate);

            if (!sameLocalDay && !legacyStillRunning)
            {
                BeginNewRound();
                return;
            }

            roundLocalDate = today;
            roundEndsUtcTicks = refreshEveryThirtySeconds
                ? data.lottoRoundEndsUtcTicks
                : NextLocalMidnightUtcTicks();
            headerNumbers = (int[])data.lottoHeaderNumbers.Clone();
            slotNumbers = (int[])data.lottoSlotNumbers.Clone();
            slotOpen = (bool[])data.lottoSlotsOpen.Clone();
            ApplyHeaderLabels();
            for (int i = 0; i < slotOpen.Length; i++)
            {
                if (slotOpen[i])
                    ShowOpenedSlot(i, slotNumbers[i]);
                else
                    CloseSlot(i);
            }

            SaveLottoState();
        }

        private void BeginNewRound()
        {
            RollHeaderNumbers();
            roundLocalDate = LocalTodayKey();
            roundEndsUtcTicks = refreshEveryThirtySeconds
                ? System.DateTime.UtcNow.AddSeconds(ShortRoundSeconds).Ticks
                : NextLocalMidnightUtcTicks();
            SaveLottoState();
        }

        private bool CatchRoundExpiry()
        {
            if (roundEndsUtcTicks <= 0)
                return false;

            if (refreshEveryThirtySeconds)
            {
                if (System.DateTime.UtcNow.Ticks < roundEndsUtcTicks)
                    return false;
            }
            else if (!string.IsNullOrEmpty(roundLocalDate)
                && roundLocalDate == LocalTodayKey()
                && System.DateTime.UtcNow.Ticks < roundEndsUtcTicks)
            {
                return false;
            }
            else if (string.IsNullOrEmpty(roundLocalDate))
            {
                return false;
            }

            BeginNewRound();
            return true;
        }

        private static string LocalTodayKey()
        {
            return System.DateTime.Now.ToString("yyyy-MM-dd");
        }

        private static long NextLocalMidnightUtcTicks()
        {
            return System.DateTime.Now.Date.AddDays(1).ToUniversalTime().Ticks;
        }

        private void SaveLottoState()
        {
            if (!Application.isPlaying) return;
            if (headerNumbers == null || slotNumbers == null || slotOpen == null) return;
            if (!SaveDataService.HasInstance || SaveDataService.Instance.Data == null) return;

            GameSaveData data = SaveDataService.Instance.Data;
            data.lottoRoundLocalDate = roundLocalDate;
            data.lottoUsesShortTimer = refreshEveryThirtySeconds;
            data.lottoRoundEndsUtcTicks = roundEndsUtcTicks;
            data.lottoHeaderNumbers = (int[])headerNumbers.Clone();
            data.lottoSlotNumbers = (int[])slotNumbers.Clone();
            data.lottoSlotsOpen = (bool[])slotOpen.Clone();
            SaveDataService.Instance.Save();
        }

        private void ApplyHeaderLabels()
        {
            if (headerNumbers == null || headerLabels == null) return;

            int count = Mathf.Min(headerNumbers.Length, headerLabels.Length);
            for (int i = 0; i < count; i++)
            {
                if (headerLabels[i] != null)
                    headerLabels[i].text = headerNumbers[i].ToString();
            }
        }

        private int SlotCount()
        {
            if (slotLabels != null && slotLabels.Length > 0) return slotLabels.Length;
            if (slotOpen != null && slotOpen.Length > 0) return slotOpen.Length;
            return Mathf.Max(1, columns * rows);
        }

        private void RefreshTimerLabel()
        {
            if (roundEndsUtcTicks > 0)
            {
                var end = new System.DateTime(roundEndsUtcTicks, System.DateTimeKind.Utc);
                timeLeft = Mathf.Max(0f, (float)(end - System.DateTime.UtcNow).TotalSeconds);
            }

            if (timerLabel == null) return;

            int total = Mathf.Max(0, Mathf.CeilToInt(timeLeft));
            int hours = total / 3600;
            int minutes = (total % 3600) / 60;
            int seconds = total % 60;
            timerLabel.text = hours > 0
                ? $"{hours}h {minutes:00}:{seconds:00}"
                : $"{minutes:00}:{seconds:00}";
        }

        private void BindLottoButton()
        {
            UnbindLottoButton();

            Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate == null || candidate.name != "LottoButton") continue;
                if (candidate.IsChildOf(transform)) continue;

                Button button = candidate.GetComponent<Button>();
                if (button == null) continue;

                lottoButton = button;
                lottoButton.onClick.AddListener(OnLottoButton);
                return;
            }
        }

        private void UnbindLottoButton()
        {
            if (lottoButton == null) return;
            lottoButton.onClick.RemoveListener(OnLottoButton);
            lottoButton = null;
        }

        public void EnsureBoardExists()
        {
            Transform existing = transform.Find("LottoVisual");
            if (existing != null)
            {
                visualRoot = existing.gameObject;
                ApplyProjectFontToBoard();
                ApplySlotSprites();
                EnsurePayPanel();
                if (Application.isPlaying)
                    RebindVisual();
                return;
            }

            BuildVisual();
#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
        }

        private void EnsureVisual()
        {
            EnsureBoardExists();
        }

        private void BuildVisual()
        {
            Sprite sprite = GetRoundedSprite();

            visualRoot = CreateUiObject("LottoVisual", transform);
            Stretch(visualRoot.GetComponent<RectTransform>());
            var backdrop = visualRoot.AddComponent<Image>();
            backdrop.color = new Color(0.04f, 0.06f, 0.18f, 0.94f);
            backdrop.raycastTarget = true;

            var board = CreateUiObject("Board", visualRoot.transform);
            RectTransform boardRect = board.GetComponent<RectTransform>();
            boardRect.anchorMin = new Vector2(0.5f, 0.5f);
            boardRect.anchorMax = new Vector2(0.5f, 0.5f);
            boardRect.pivot = new Vector2(0.5f, 0.5f);
            boardRect.sizeDelta = new Vector2(760f, 980f);

            var boardImage = board.AddComponent<Image>();
            boardImage.sprite = sprite;
            boardImage.type = Image.Type.Sliced;
            boardImage.color = new Color(0.10f, 0.34f, 0.82f, 1f);

            var layout = board.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(36, 36, 28, 28);
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            CreateCloseButton(visualRoot.transform, sprite);
            CreateTitle(board.transform);
            CreateHeaderRow(board.transform, sprite);
            CreateBulbRow(board.transform);
            CreateSlotGrid(board.transform, sprite);
            CreateTimer(board.transform);

            RollHeaderNumbers();
            EnsurePayPanel();
        }

        private void CreateTitle(Transform parent)
        {
            var titleGo = CreateUiObject("Title", parent);
            var title = titleGo.AddComponent<TextMeshProUGUI>();
            title.text = "LOTTO";
            title.fontSize = 72f;
            title.fontStyle = FontStyles.Bold;
            title.alignment = TextAlignmentOptions.Center;
            title.color = Color.white;
            ApplyProjectFont(title);
            var le = titleGo.AddComponent<LayoutElement>();
            le.preferredHeight = 90f;
        }

        private void CreateHeaderRow(Transform parent, Sprite sprite)
        {
            var row = CreateUiObject("HeaderNumbers", parent);
            var h = row.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 10f;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;
            var le = row.AddComponent<LayoutElement>();
            le.preferredHeight = 92f;

            headerLabels = new TMP_Text[headerCount];
            for (int i = 0; i < headerCount; i++)
            {
                var ticket = CreateUiObject($"Header_{i}", row.transform);
                var frame = ticket.AddComponent<Image>();
                frame.sprite = sprite;
                frame.type = Image.Type.Sliced;
                frame.color = headerColor;

                var inner = CreateUiObject("Face", ticket.transform);
                Stretch(inner.GetComponent<RectTransform>(), 8f);
                var face = inner.AddComponent<Image>();
                face.sprite = sprite;
                face.type = Image.Type.Sliced;
                face.color = headerInner;
                face.raycastTarget = false;

                var textGo = CreateUiObject("Number", inner.transform);
                Stretch(textGo.GetComponent<RectTransform>());
                var label = textGo.AddComponent<TextMeshProUGUI>();
                label.text = "00";
                label.fontSize = 32f;
                label.fontStyle = FontStyles.Bold;
                label.alignment = TextAlignmentOptions.Center;
                label.color = new Color(0.85f, 0.95f, 1f, 1f);
                label.raycastTarget = false;
                ApplyProjectFont(label);
                headerLabels[i] = label;
            }
        }

        private void CreateBulbRow(Transform parent)
        {
            var row = CreateUiObject("Bulbs", parent);
            var h = row.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 18f;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = false;
            h.childControlHeight = false;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            var le = row.AddComponent<LayoutElement>();
            le.preferredHeight = 28f;

            for (int i = 0; i < 7; i++)
            {
                var bulb = CreateUiObject($"Bulb_{i}", row.transform);
                var rect = bulb.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(22f, 22f);
                var image = bulb.AddComponent<Image>();
                image.sprite = GetRoundedSprite();
                image.type = Image.Type.Sliced;
                image.color = new Color(1f, 0.86f, 0.25f, 1f);
                image.raycastTarget = false;
            }
        }

        private void CreateSlotGrid(Transform parent, Sprite sprite)
        {
            int count = Mathf.Max(1, columns * rows);
            slotOpen = new bool[count];
            slotFaces = new Image[count];
            slotLabels = new TMP_Text[count];

            var gridGo = CreateUiObject("Slots", parent);
            var grid = gridGo.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Mathf.Max(1, columns);
            grid.cellSize = new Vector2(210f, 150f);
            grid.spacing = new Vector2(16f, 16f);
            grid.childAlignment = TextAnchor.MiddleCenter;
            var le = gridGo.AddComponent<LayoutElement>();
            le.preferredHeight = rows * 150f + (rows - 1) * 16f;

            for (int i = 0; i < count; i++)
            {
                var slot = CreateUiObject($"Slot_{i}", gridGo.transform);
                var frame = slot.AddComponent<Image>();
                Sprite buttonSprite = SlotSprite();
                frame.sprite = buttonSprite != null ? buttonSprite : sprite;
                frame.type = buttonSprite != null ? Image.Type.Simple : Image.Type.Sliced;
                frame.color = buttonSprite != null ? Color.white : closedColor;
                frame.raycastTarget = true;
                WireSlotClick(slot, frame, i);

                var inner = CreateUiObject("Face", slot.transform);
                Stretch(inner.GetComponent<RectTransform>(), 10f);
                var face = inner.AddComponent<Image>();
                face.enabled = false;
                face.raycastTarget = false;
                slotFaces[i] = face;

                var textGo = CreateUiObject("Label", inner.transform);
                Stretch(textGo.GetComponent<RectTransform>());
                var label = textGo.AddComponent<TextMeshProUGUI>();
                label.text = ClosedLabel;
                label.fontSize = 28f;
                label.fontStyle = FontStyles.Bold;
                label.alignment = TextAlignmentOptions.Center;
                label.color = Color.white;
                label.raycastTarget = false;
                ApplyProjectFont(label);
                slotLabels[i] = label;
            }
        }

        private void CreateTimer(Transform parent)
        {
            var timerGo = CreateUiObject("Timer", parent);
            var label = timerGo.AddComponent<TextMeshProUGUI>();
            label.text = "00:00";
            label.fontSize = 42f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            ApplyProjectFont(label);
            var le = timerGo.AddComponent<LayoutElement>();
            le.preferredHeight = 56f;
            timerLabel = label;
        }

        private void CreateCloseButton(Transform parent, Sprite sprite)
        {
            var closeGo = CreateUiObject("LottoCloseButton", parent);
            var rect = closeGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-24f, -24f);
            rect.sizeDelta = new Vector2(72f, 72f);

            var image = closeGo.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = new Color(0.20f, 0.55f, 0.95f, 1f);

            var button = closeGo.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(HideBoard);

            var textGo = CreateUiObject("X", closeGo.transform);
            Stretch(textGo.GetComponent<RectTransform>());
            var label = textGo.AddComponent<TextMeshProUGUI>();
            label.text = "X";
            label.fontSize = 36f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            ApplyProjectFont(label);
        }

        private static TMP_FontAsset gameFont;

        private static void ApplyProjectFont(TMP_Text text)
        {
            if (text == null) return;

            TMP_FontAsset font = GameFont();
            if (font == null || text.font == font) return;

            text.font = font;
        }

        private static TMP_FontAsset GameFont()
        {
            if (gameFont != null) return gameFont;

            gameFont = Resources.Load<TMP_FontAsset>("Fonts/rimouski sb SDF");
#if UNITY_EDITOR
            if (gameFont == null)
                gameFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/rimouski sb SDF.asset");
#endif
            return gameFont;
        }

        public bool ApplyProjectFontToBoard()
        {
            bool changed = false;
            TMP_FontAsset font = GameFont();
            if (font == null) return false;

            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] == null || texts[i].font == font) continue;
                texts[i].font = font;
                changed = true;
            }

            return changed;
        }

        public bool ApplySlotSprites()
        {
            Sprite sprite = SlotSprite();
            OpenSlotSprite();
            if (sprite == null || visualRoot == null) return false;

            Transform slots = visualRoot.transform.Find("Board/Slots");
            if (slots == null) return false;

            bool changed = false;
            for (int i = 0; i < slots.childCount; i++)
            {
                Transform slot = slots.GetChild(i);
                var frame = slot.GetComponent<Image>();
                if (frame != null && (frame.sprite != sprite || frame.type != Image.Type.Simple))
                {
                    frame.sprite = sprite;
                    frame.type = Image.Type.Simple;
                    changed = true;
                }

                Transform label = slot.Find("Face/Label");
                bool closed = label == null || label.GetComponent<TMP_Text>() == null || label.GetComponent<TMP_Text>().text == ClosedLabel;
                if (frame != null && closed && frame.color != Color.white)
                {
                    frame.color = Color.white;
                    changed = true;
                }

                Transform face = slot.Find("Face");
                var faceImage = face != null ? face.GetComponent<Image>() : null;
                if (faceImage != null && faceImage.enabled)
                {
                    faceImage.enabled = false;
                    changed = true;
                }
            }

#if UNITY_EDITOR
            if (changed && !Application.isPlaying)
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
            return changed;
        }

        private Sprite SlotSprite()
        {
            if (slotSprite != null) return slotSprite;
            slotSprite = LoadKitSprite("UI-pack_Sprite_1_37");
            return slotSprite;
        }

        private Sprite OpenSlotSprite()
        {
            if (openSlotSprite != null) return openSlotSprite;
            openSlotSprite = LoadKitSprite("UI-pack_Sprite_1_46");
            return openSlotSprite;
        }

        private static Sprite LoadKitSprite(string spriteName)
        {
            if (string.IsNullOrEmpty(spriteName)) return null;

#if UNITY_EDITOR
            UnityEngine.Object[] assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(
                "Assets/300Mind/2D Game UI Kit/Sprites/UI-pack_Sprite_1.png");
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite found && found.name == spriteName)
                    return found;
            }
#endif
            return null;
        }

        private void RebindVisual()
        {
            headerLabels = new TMP_Text[headerCount];
            Transform header = visualRoot.transform.Find("Board/HeaderNumbers");
            if (header != null)
            {
                int count = Mathf.Min(headerCount, header.childCount);
                headerLabels = new TMP_Text[headerCount];
                for (int i = 0; i < count; i++)
                {
                    Transform number = header.GetChild(i).Find("Face/Number");
                    if (number != null)
                        headerLabels[i] = number.GetComponent<TMP_Text>();
                }
            }

            Transform slots = visualRoot.transform.Find("Board/Slots");
            int slotCount = slots != null ? slots.childCount : columns * rows;
            slotFaces = new Image[slotCount];
            slotLabels = new TMP_Text[slotCount];
            if (slotOpen == null || slotOpen.Length != slotCount)
                slotOpen = new bool[slotCount];

            for (int i = 0; i < slotCount; i++)
            {
                Transform slot = slots.GetChild(i);
                Transform face = slot.Find("Face");
                Transform label = slot.Find("Face/Label");
                if (face != null) slotFaces[i] = face.GetComponent<Image>();
                if (label != null) slotLabels[i] = label.GetComponent<TMP_Text>();
                int parsedNumber;
                slotOpen[i] = slotLabels[i] != null && int.TryParse(slotLabels[i].text, out parsedNumber);
                if (!slotOpen[i] && slotLabels[i] != null)
                    slotLabels[i].text = ClosedLabel;

                var frame = slot.GetComponent<Image>();
                if (frame != null)
                {
                    frame.raycastTarget = true;
                    WireSlotClick(slot.gameObject, frame, i);
                    if (slotOpen[i])
                    {
                        var slotButton = slot.GetComponent<Button>();
                        if (slotButton != null)
                            slotButton.interactable = false;
                    }
                }
            }

            Transform timer = visualRoot.transform.Find("Board/Timer");
            if (timer != null)
                timerLabel = timer.GetComponent<TMP_Text>();

            Transform close = visualRoot.transform.Find("LottoCloseButton");
            if (close != null)
            {
                var button = close.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.RemoveListener(HideBoard);
                    button.onClick.AddListener(HideBoard);
                }
            }
        }

        private void WireSlotClick(GameObject slot, Image frame, int index)
        {
            var button = slot.GetComponent<Button>();
            if (button == null)
                button = slot.AddComponent<Button>();

            button.targetGraphic = frame;
            KeepSlotOpaque(button);
            button.onClick.RemoveAllListeners();
            int captured = index;
            button.onClick.AddListener(() => AskToOpenSlot(captured));
        }

        private void AskToOpenSlot(int index)
        {
            if (slotOpen == null || index < 0 || index >= slotOpen.Length || slotOpen[index])
                return;

            pendingSlot = index;
            EnsurePayPanel();
            if (payPanel == null)
            {
                RevealSlot(index);
                return;
            }

            if (payGemCostLabel != null)
                payGemCostLabel.text = slotGemCost.ToString();
            if (payGemIcon != null && payGemIcon.sprite == null)
                payGemIcon.sprite = GemSprite();
            if (payMessage != null)
                payMessage.text = "";

            payPanel.SetActive(true);
            payPanel.transform.SetAsLastSibling();
        }

        private void OnPayWithAd()
        {
            if (pendingSlot < 0) return;
            int index = pendingSlot;
            HidePayPanel();
            RevealSlot(index);
        }

        private void OnPayWithGems()
        {
            if (pendingSlot < 0) return;

            GemManager gems = GemManager.Instance;
            if (gems == null || !gems.SpendGems(slotGemCost))
            {
                OpenShopLikeBalance();
                return;
            }

            int index = pendingSlot;
            HidePayPanel();
            RevealSlot(index);
        }

        private void OnPayCancel()
        {
            HidePayPanel();
        }

        private void OpenShopLikeBalance()
        {
            if (MenuShopVideoPanel.IsShopOpen) return;

            HidePayPanel();
            HideBoard(true);

            if (GameSettingsManager.HasInstance)
            {
                GameSettingsManager.Instance.PlayClickSound();
                GameSettingsManager.Instance.TriggerHaptic();
            }

            MenuShopVideoPanel.OpenShop();
        }

        private void HidePayPanel()
        {
            pendingSlot = -1;
            if (payGemButtonRect != null)
                payGemButtonRect.anchoredPosition = new Vector2(0f, 28f);
            if (payPanel != null)
                payPanel.SetActive(false);
        }

        private bool EnsurePayPanel()
        {
            if (visualRoot == null) return false;

            Transform existing = visualRoot.transform.Find("LottoPayPanel");
            if (existing != null)
            {
                BindPayPanel(existing);
                return false;
            }

            Sprite sprite = GetRoundedSprite();
            var root = CreateUiObject("LottoPayPanel", visualRoot.transform);
            Stretch(root.GetComponent<RectTransform>());

            var dim = root.AddComponent<Image>();
            dim.color = new Color(0.02f, 0.04f, 0.12f, 0.72f);
            var dimButton = root.AddComponent<Button>();
            dimButton.targetGraphic = dim;
            dimButton.transition = Selectable.Transition.None;
            dimButton.onClick.AddListener(OnPayCancel);

            var card = CreateUiObject("Card", root.transform);
            var cardRect = card.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(560f, 420f);
            var cardImage = card.AddComponent<Image>();
            cardImage.sprite = sprite;
            cardImage.type = Image.Type.Sliced;
            cardImage.color = new Color(0.10f, 0.28f, 0.72f, 1f);

            var title = CreateLabel(card.transform, "Title", "OPEN SLOT", 40f, new Vector2(0f, 150f), new Vector2(480f, 56f));
            title.color = Color.white;

            var gemButton = CreateChoiceButton(card.transform, "GemButton", new Vector2(0f, 28f), new Vector2(460f, 92f), new Color(0.14f, 0.72f, 0.35f, 1f));
            payGemButtonRect = gemButton.GetComponent<RectTransform>();
            var gemIconGo = CreateUiObject("GemIcon", gemButton.transform);
            var gemIconRect = gemIconGo.GetComponent<RectTransform>();
            gemIconRect.anchorMin = new Vector2(0.5f, 0.5f);
            gemIconRect.anchorMax = new Vector2(0.5f, 0.5f);
            gemIconRect.pivot = new Vector2(1f, 0.5f);
            gemIconRect.anchoredPosition = new Vector2(-8f, 0f);
            gemIconRect.sizeDelta = new Vector2(58f, 58f);
            payGemIcon = gemIconGo.AddComponent<Image>();
            payGemIcon.sprite = GemSprite();
            payGemIcon.preserveAspect = true;
            payGemIcon.raycastTarget = false;

            var costGo = CreateUiObject("Cost", gemButton.transform);
            var costRect = costGo.GetComponent<RectTransform>();
            costRect.anchorMin = new Vector2(0.5f, 0.5f);
            costRect.anchorMax = new Vector2(0.5f, 0.5f);
            costRect.pivot = new Vector2(0f, 0.5f);
            costRect.anchoredPosition = new Vector2(8f, 0f);
            costRect.sizeDelta = new Vector2(140f, 70f);
            payGemCostLabel = costGo.AddComponent<TextMeshProUGUI>();
            payGemCostLabel.text = slotGemCost.ToString();
            payGemCostLabel.fontSize = 42f;
            payGemCostLabel.fontStyle = FontStyles.Bold;
            payGemCostLabel.alignment = TextAlignmentOptions.MidlineLeft;
            payGemCostLabel.color = Color.white;
            payGemCostLabel.raycastTarget = false;
            ApplyProjectFont(payGemCostLabel);
            gemButton.onClick.AddListener(OnPayWithGems);

            var adButton = CreateChoiceButton(card.transform, "AdButton", new Vector2(0f, -84f), new Vector2(460f, 84f), new Color(0.38f, 0.32f, 0.88f, 1f));
            var adLabel = CreateLabel(adButton.transform, "Label", "WATCH AD", 32f, Vector2.zero, new Vector2(420f, 70f));
            adLabel.color = Color.white;
            adButton.onClick.AddListener(OnPayWithAd);

            var closeGo = CreateUiObject("PayClose", card.transform);
            var closeRect = closeGo.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-16f, -16f);
            closeRect.sizeDelta = new Vector2(56f, 56f);
            var closeImage = closeGo.AddComponent<Image>();
            closeImage.sprite = sprite;
            closeImage.type = Image.Type.Sliced;
            closeImage.color = new Color(0.20f, 0.55f, 0.95f, 1f);
            var closeButton = closeGo.AddComponent<Button>();
            closeButton.targetGraphic = closeImage;
            closeButton.onClick.AddListener(OnPayCancel);
            var closeLabel = CreateLabel(closeGo.transform, "X", "X", 28f, Vector2.zero, new Vector2(56f, 56f));
            closeLabel.color = Color.white;

            payMessage = CreateLabel(card.transform, "Message", "", 24f, new Vector2(0f, -168f), new Vector2(480f, 40f));
            payMessage.color = new Color(1f, 0.35f, 0.35f, 1f);

            payPanel = root;
            payPanel.SetActive(false);

#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
            return true;
        }

        private void BindPayPanel(Transform root)
        {
            payPanel = root.gameObject;
            var dimButton = root.GetComponent<Button>();
            if (dimButton != null)
            {
                dimButton.onClick.RemoveListener(OnPayCancel);
                dimButton.onClick.AddListener(OnPayCancel);
            }

            Transform gem = root.Find("Card/GemButton");
            if (gem != null)
            {
                payGemButtonRect = gem as RectTransform;
                payGemIcon = gem.Find("GemIcon") != null ? gem.Find("GemIcon").GetComponent<Image>() : null;
                payGemCostLabel = gem.Find("Cost") != null ? gem.Find("Cost").GetComponent<TMP_Text>() : null;
                var gemButton = gem.GetComponent<Button>();
                if (gemButton != null)
                {
                    gemButton.onClick.RemoveListener(OnPayWithGems);
                    gemButton.onClick.AddListener(OnPayWithGems);
                }
            }

            Transform ad = root.Find("Card/AdButton");
            if (ad != null)
            {
                var adButton = ad.GetComponent<Button>();
                if (adButton != null)
                {
                    adButton.onClick.RemoveListener(OnPayWithAd);
                    adButton.onClick.AddListener(OnPayWithAd);
                }
            }

            Transform close = root.Find("Card/PayClose");
            if (close != null)
            {
                var closeButton = close.GetComponent<Button>();
                if (closeButton != null)
                {
                    closeButton.onClick.RemoveListener(OnPayCancel);
                    closeButton.onClick.AddListener(OnPayCancel);
                }
            }

            Transform message = root.Find("Card/Message");
            if (message != null)
                payMessage = message.GetComponent<TMP_Text>();

            Transform title = root.Find("Card/Title");
            if (title != null)
            {
                TMP_Text titleLabel = title.GetComponent<TMP_Text>();
                if (titleLabel != null)
                    titleLabel.text = "OPEN SLOT";
            }

            Transform adLabel = root.Find("Card/AdButton/Label");
            if (adLabel != null)
            {
                TMP_Text adText = adLabel.GetComponent<TMP_Text>();
                if (adText != null)
                    adText.text = "WATCH AD";
            }
        }

        private Button CreateChoiceButton(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            var buttonGo = CreateUiObject(name, parent);
            var rect = buttonGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = buttonGo.AddComponent<Image>();
            image.sprite = GetRoundedSprite();
            image.type = Image.Type.Sliced;
            image.color = color;
            var button = buttonGo.AddComponent<Button>();
            button.targetGraphic = image;
            return button;
        }

        private TMP_Text CreateLabel(Transform parent, string name, string text, float fontSize, Vector2 position, Vector2 size)
        {
            var labelGo = CreateUiObject(name, parent);
            var rect = labelGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            ApplyProjectFont(label);
            return label;
        }

        private static Sprite GemSprite()
        {
            if (gemSprite != null) return gemSprite;

            gemSprite = Resources.Load<Sprite>("Sprites/gem_icon");
#if UNITY_EDITOR
            if (gemSprite == null)
                gemSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Sprites/gem_icon.png");
#endif
            if (gemSprite != null) return gemSprite;

            Image[] images = FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != null && images[i].name == "GemIcon" && images[i].sprite != null)
                {
                    gemSprite = images[i].sprite;
                    return gemSprite;
                }
            }

            return null;
        }

        private static void KeepSlotOpaque(Button button)
        {
            button.transition = Selectable.Transition.None;
        }

        private static GameObject CreateUiObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static Sprite GetRoundedSprite()
        {
            if (roundedSprite != null) return roundedSprite;

            const int size = 64;
            const int radius = 18;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[size * size];
            var fill = new Color32(255, 255, 255, 255);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool inside = IsInsideRoundRect(x, y, size, radius);
                    pixels[y * size + x] = inside ? fill : new Color32(255, 255, 255, 0);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            roundedSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            roundedSprite.name = "LottoRounded";
            return roundedSprite;
        }

        private static bool IsInsideRoundRect(int x, int y, int size, int radius)
        {
            int left = radius;
            int right = size - 1 - radius;
            int bottom = radius;
            int top = size - 1 - radius;
            if (x >= left && x <= right) return true;
            if (y >= bottom && y <= top) return true;

            int cx = x < left ? left : right;
            int cy = y < bottom ? bottom : top;
            int dx = x - cx;
            int dy = y - cy;
            return dx * dx + dy * dy <= radius * radius;
        }
    }
}
