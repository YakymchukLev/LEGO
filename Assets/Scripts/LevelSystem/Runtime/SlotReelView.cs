using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LegoPuzzle.Runtime
{
    /// <summary>
    /// Represents an individual vertical slot reel containing endlessly cycling symbols.
    /// Uses continuous periodic modulo positioning and guaranteed sprite caching to prevent
    /// disappearing sprites, blank cells, and visual popping.
    /// </summary>
    public class SlotReelView : MonoBehaviour
    {
        [Header("Reel Layout Dimensions")]
        [Tooltip("Height of each item symbol slot in pixels")]
        [SerializeField] private float itemHeight = 52f;

        [Tooltip("Total number of physical UI cells recycled in the reel strip")]
        [SerializeField] private int cellCount = 7;

        [Header("Spin Motion Parameters")]
        [Tooltip("Max linear downward scrolling speed in pixels per second")]
        [SerializeField] private float maxSpinSpeed = 1900f;

        [Tooltip("Anticipation pull distance in pixels before spinning down")]
        [SerializeField] private float anticipationDistance = 14f;

        [Tooltip("Bounce overshoot distance in pixels when landing on payline")]
        [SerializeField] private float overshootDistance = 15f;

        [Header("References")]
        [SerializeField] private RectTransform stripContainer;
        [SerializeField] private List<ReelCell> activeCells = new List<ReelCell>();

        public bool IsSpinning { get; private set; } = false;
        public SlotItemData PaylineItem { get; private set; }
        public float ItemHeight => itemHeight;

        public event Action OnPaylineTick;
        public event Action<SlotReelView> OnReelStopped;

        [Serializable]
        public class ReelCell
        {
            public RectTransform rect;
            public Image iconImage;
            public SlotItemData currentData;
            public int lastCycle = int.MinValue;
        }

        private Coroutine spinCoroutine;
        private List<SlotItemData> itemPool = new List<SlotItemData>();
        private float currentScrollPos = 0f;

        private void Awake()
        {
            EnsureHierarchy();
        }

        public void SetItemHeight(float height)
        {
            itemHeight = height;
            EnsureHierarchy();
            UpdateAllCellPositions(currentScrollPos, false);
        }

        /// <summary>
        /// Ensures the internal strip container and cell images are created and configured.
        /// </summary>
        public void EnsureHierarchy()
        {
            if (stripContainer == null)
            {
                var existing = transform.Find("StripContainer");
                if (existing != null)
                {
                    stripContainer = existing as RectTransform;
                }
                else
                {
                    GameObject stripObj = new GameObject("StripContainer", typeof(RectTransform));
                    stripObj.transform.SetParent(transform, false);
                    stripContainer = stripObj.GetComponent<RectTransform>();
                    stripContainer.anchorMin = new Vector2(0.5f, 0.5f);
                    stripContainer.anchorMax = new Vector2(0.5f, 0.5f);
                    stripContainer.pivot = new Vector2(0.5f, 0.5f);
                    stripContainer.sizeDelta = new Vector2(itemHeight * 0.95f, itemHeight * cellCount);
                    stripContainer.anchoredPosition = Vector2.zero;
                }
            }
            else
            {
                stripContainer.sizeDelta = new Vector2(itemHeight * 0.95f, itemHeight * cellCount);
            }

            if (activeCells == null) activeCells = new List<ReelCell>();

            if (activeCells.Count < cellCount)
            {
                activeCells.Clear();
                int existingChildren = stripContainer.childCount;
                for (int i = 0; i < existingChildren; i++)
                {
                    var child = stripContainer.GetChild(i) as RectTransform;
                    var img = child.GetComponentInChildren<Image>();
                    child.sizeDelta = new Vector2(itemHeight * 0.88f, itemHeight * 0.88f);
                    activeCells.Add(new ReelCell
                    {
                        rect = child,
                        iconImage = img,
                        currentData = null,
                        lastCycle = int.MinValue
                    });
                }

                while (activeCells.Count < cellCount)
                {
                    int index = activeCells.Count;
                    GameObject cellObj = new GameObject($"Cell_{index}", typeof(RectTransform));
                    cellObj.transform.SetParent(stripContainer, false);
                    RectTransform cellRect = cellObj.GetComponent<RectTransform>();
                    cellRect.anchorMin = new Vector2(0.5f, 0.5f);
                    cellRect.anchorMax = new Vector2(0.5f, 0.5f);
                    cellRect.pivot = new Vector2(0.5f, 0.5f);
                    cellRect.sizeDelta = new Vector2(itemHeight * 0.88f, itemHeight * 0.88f);

                    GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                    iconObj.transform.SetParent(cellObj.transform, false);
                    RectTransform iconRect = iconObj.GetComponent<RectTransform>();
                    iconRect.anchorMin = Vector2.zero;
                    iconRect.anchorMax = Vector2.one;
                    iconRect.sizeDelta = Vector2.zero;

                    Image img = iconObj.GetComponent<Image>();
                    img.preserveAspect = true;

                    activeCells.Add(new ReelCell
                    {
                        rect = cellRect,
                        iconImage = img,
                        currentData = null,
                        lastCycle = int.MinValue
                    });
                }
            }
            else
            {
                for (int i = 0; i < activeCells.Count; i++)
                {
                    if (activeCells[i].rect != null)
                    {
                        activeCells[i].rect.sizeDelta = new Vector2(itemHeight * 0.88f, itemHeight * 0.88f);
                    }
                }
            }

            UpdateAllCellPositions(currentScrollPos);
        }

        /// <summary>
        /// Initializes the reel with specific visible items (top, center payline, bottom)
        /// or picks random items from the provided pool.
        /// </summary>
        public void Initialize(List<SlotItemData> availableItems, SlotItemData centerDefault = null)
        {
            EnsureHierarchy();
            itemPool = availableItems != null ? availableItems : new List<SlotItemData>();
            if (itemPool.Count == 0) return;

            int centerIndex = cellCount / 2;
            currentScrollPos = 0f;

            for (int i = 0; i < activeCells.Count; i++)
            {
                SlotItemData data = (i == centerIndex && centerDefault != null)
                    ? centerDefault
                    : GetRandomPoolItem();

                SetCellData(activeCells[i], data);
                activeCells[i].lastCycle = 0;
            }

            PaylineItem = activeCells[centerIndex].currentData;
            UpdateAllCellPositions(0f);
        }

        public void SetCellData(ReelCell cell, SlotItemData data)
        {
            if (data == null) data = GetRandomPoolItem();
            cell.currentData = data;

            if (cell.iconImage != null)
            {
                if (data != null && data.icon != null)
                {
                    cell.iconImage.sprite = data.icon;
                    cell.iconImage.enabled = true;
                    cell.iconImage.color = Color.white;
                }
                else
                {
                    // Fallback to any valid pool sprite so it never displays a blank square
                    Sprite fallback = GetAnyValidSprite();
                    if (fallback != null)
                    {
                        cell.iconImage.sprite = fallback;
                        cell.iconImage.enabled = true;
                        cell.iconImage.color = Color.white;
                    }
                    else
                    {
                        cell.iconImage.enabled = false;
                    }
                }
            }
        }

        private Sprite GetAnyValidSprite()
        {
            if (itemPool != null)
            {
                foreach (var item in itemPool)
                {
                    if (item != null && item.icon != null) return item.icon;
                }
            }
            return null;
        }

        private SlotItemData GetRandomPoolItem()
        {
            if (itemPool == null || itemPool.Count == 0) return null;
            return itemPool[UnityEngine.Random.Range(0, itemPool.Count)];
        }

        /// <summary>
        /// Continuously positions and wraps all cells based on scrollPos.
        /// When a cell wraps around the edge, assigns a new symbol if allowed.
        /// </summary>
        private void UpdateAllCellPositions(float scrollPos, bool assignNewOnWrap = true)
        {
            int centerIndex = cellCount / 2;
            float stripHeight = cellCount * itemHeight;
            float halfStrip = stripHeight / 2f;

            for (int i = 0; i < activeCells.Count; i++)
            {
                ReelCell cell = activeCells[i];
                float baseY = (i - centerIndex) * itemHeight;
                float rawY = baseY - scrollPos;

                // Periodic continuous wrap within [-halfStrip, +halfStrip]
                float wrappedY = Mathf.Repeat(rawY + halfStrip, stripHeight) - halfStrip;
                cell.rect.anchoredPosition = new Vector2(0f, wrappedY);

                // Cycle calculation to detect when a cell has wrapped from bottom to top
                int cycle = Mathf.FloorToInt((rawY + halfStrip) / stripHeight);
                if (assignNewOnWrap && cycle != cell.lastCycle)
                {
                    cell.lastCycle = cycle;
                    SetCellData(cell, GetRandomPoolItem());
                }
            }
        }

        /// <summary>
        /// Starts spinning this reel, smoothly decelerates after spinDuration,
        /// and lands targetOutcome precisely on the center payline.
        /// </summary>
        public void StartSpin(List<SlotItemData> pool, SlotItemData targetOutcome, float spinDuration, Action onFinished = null)
        {
            if (spinCoroutine != null) StopCoroutine(spinCoroutine);
            itemPool = pool;
            spinCoroutine = StartCoroutine(SpinRoutine(targetOutcome, spinDuration, onFinished));
        }

        private IEnumerator SpinRoutine(SlotItemData targetOutcome, float duration, Action onFinished)
        {
            IsSpinning = true;
            EnsureHierarchy();

            int centerIndex = cellCount / 2;
            float stripHeight = cellCount * itemHeight;
            float halfStrip = stripHeight / 2f;

            // Phase 1: Anticipation pull upwards
            float pullDuration = 0.12f;
            float elapsedPull = 0f;
            float startScroll = currentScrollPos;

            while (elapsedPull < pullDuration)
            {
                elapsedPull += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(elapsedPull / pullDuration);
                float pull = -Mathf.Sin(p * Mathf.PI * 0.5f) * anticipationDistance;
                currentScrollPos = startScroll + pull;
                UpdateAllCellPositions(currentScrollPos, false);
                yield return null;
            }

            // Phase 2: Full speed spin loop
            float speed = 0f;
            float accelTime = 0.20f;
            float elapsedSpin = 0f;
            float spinTime = Mathf.Max(duration, 0.6f);
            float lastTickScroll = currentScrollPos;

            while (elapsedSpin < spinTime)
            {
                float dt = Time.unscaledDeltaTime;
                elapsedSpin += dt;

                if (elapsedSpin < accelTime)
                {
                    speed = Mathf.Lerp(0f, maxSpinSpeed, elapsedSpin / accelTime);
                }
                else
                {
                    speed = maxSpinSpeed;
                }

                currentScrollPos += speed * dt;
                UpdateAllCellPositions(currentScrollPos, true);

                if (Mathf.Abs(currentScrollPos - lastTickScroll) >= itemHeight)
                {
                    lastTickScroll = currentScrollPos;
                    OnPaylineTick?.Invoke();
                }

                yield return null;
            }

            // Phase 3: Deceleration & exact target outcome alignment
            // Calculate total target scroll position so targetOutcome arrives at center payline (wrappedY = 0)
            float minDecelDistance = itemHeight * 12f; // ~12 symbols deceleration distance
            float targetScroll = Mathf.Ceil((currentScrollPos + minDecelDistance) / itemHeight) * itemHeight;

            // Find which physical cell will land on the center payline at targetScroll
            ReelCell landingCell = null;
            int landingCellIndex = -1;

            for (int i = 0; i < activeCells.Count; i++)
            {
                float baseY = (i - centerIndex) * itemHeight;
                float rawY = baseY - targetScroll;
                float finalWrappedY = Mathf.Repeat(rawY + halfStrip, stripHeight) - halfStrip;

                if (Mathf.Abs(finalWrappedY) < 1f)
                {
                    landingCell = activeCells[i];
                    landingCellIndex = i;
                    break;
                }
            }

            // Fallback just in case
            if (landingCell == null)
            {
                landingCell = activeCells[centerIndex];
                landingCellIndex = centerIndex;
            }

            // Assign the targetOutcome to the landing cell
            SetCellData(landingCell, targetOutcome);

            // Populate all other cells with random valid items
            for (int i = 0; i < activeCells.Count; i++)
            {
                if (i != landingCellIndex)
                {
                    SetCellData(activeCells[i], GetRandomPoolItem());
                }
            }

            // Animate deceleration with smooth ease-out curve + spring bounce overshoot
            float decelStartScroll = currentScrollPos;
            float decelDuration = 0.75f;
            float elapsedDecel = 0f;

            while (elapsedDecel < decelDuration)
            {
                elapsedDecel += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(elapsedDecel / decelDuration);

                // Smooth quintic ease-out curve
                float easeOut = 1f - Mathf.Pow(1f - p, 3.5f);

                // Spring overshoot bounce
                float bounce = Mathf.Sin(p * Mathf.PI) * (1f - p) * overshootDistance;

                currentScrollPos = Mathf.Lerp(decelStartScroll, targetScroll, easeOut) + bounce;
                UpdateAllCellPositions(currentScrollPos, false);

                if (Mathf.Abs(currentScrollPos - lastTickScroll) >= itemHeight)
                {
                    lastTickScroll = currentScrollPos;
                    OnPaylineTick?.Invoke();
                }

                yield return null;
            }

            // Final exact snap to payline
            currentScrollPos = targetScroll;
            UpdateAllCellPositions(targetScroll, false);

            PaylineItem = targetOutcome;
            IsSpinning = false;

            OnReelStopped?.Invoke(this);
            onFinished?.Invoke();
            spinCoroutine = null;
        }
    }
}
