using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using LegoPuzzle.Data;

namespace LegoPuzzle.Runtime
{
    public class LegoPieceView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("Дані деталі")]
        public LegoPieceData PieceData { get; private set; }
        public Vector2Int CurrentOrigin { get; private set; }
        public void SetCurrentOriginDirect(Vector2Int origin) => CurrentOrigin = origin;
        public bool IsExiting => isExiting;
        public int CurrentFreezeCount { get; private set; }
        public bool IsFrozen => CurrentFreezeCount > 0;

        [Header("Посилання")]
        [SerializeField] private GameObject modelInstance;
        [SerializeField] private SpriteRenderer restrictionIconRenderer;

        [Header("Параметри анімації")]
        [SerializeField] private float snapDuration = 0.1f;

        [Header("Параметри підйому при перетягуванні")]
        [SerializeField] private float liftHeight = 0.14f;
        [SerializeField] private float liftScaleMultiplier = 1.025f;
        [SerializeField] private float liftSpeed = 22f;
        [SerializeField] private float dropSpeed = 24f;
        [SerializeField] private bool enableLiftShadow = true;

        private LevelLoader levelLoader;
        private BlockPalette cachedPalette;
        private float cellSize = 1f;
        private bool isDragging = false;
        private bool isSnapping = false;
        private bool isExiting = false;

        private GameObject visualRoot;
        private GameObject shadowRoot;
        private List<MeshRenderer> shadowRenderers;
        private Vector3 visualCenter;
        private float currentLiftY = 0f;
        private float targetLiftY = 0f;
        private bool isLifted = false;
        private bool wasLifted = false;
        private float landingBounceTimer = 0f;
        private const float LandingBounceDuration = 0.09f;

        private GameObject freezeBadgeObj;
        private SpriteRenderer freezeBadgeBgRenderer;
        private TextMeshPro freezeBadgeText;
        private GameObject iceShellObj;
        private MeshFilter iceShellMeshFilter;
        private MeshRenderer iceShellMeshRenderer;
        private static Material sharedIceMaterial;
        private MaterialPropertyBlock iceMpb;
        private Coroutine crackPulseCoroutine;
        private const float FreezeBadgeScale = 0.44f;
        private Coroutine frozenShakeCoroutine;
        private Coroutine badgePulseCoroutine;

        private Vector3 dragStartPointerWorld;
        private Vector3 dragStartLocalPosition;
        private Vector2Int dragStartGridPos;
        private float minWorldX, maxWorldX;
        private float minWorldZ, maxWorldZ;
        private List<Vector2Int> currentOffsets;

        public event Action<LegoPieceView> OnExited;

        public void TriggerAutoExit(ExitDirection dir)
        {
            if (isExiting || IsFrozen) return;
            isDragging = false;
            isSnapping = false;
            isLifted = false;
            targetLiftY = 0f;
            SetOutlineActive(false);
            StartCoroutine(PlayExitAnimation(dir));
        }

        public void Initialize(LegoPieceData data, LevelLoader loader, BlockPalette palette, float size, GameObject visualChild = null)
        {
            PieceData = data;
            levelLoader = loader;
            cachedPalette = palette;
            cellSize = size;
            CurrentOrigin = data.originPosition;
            CurrentFreezeCount = data != null ? Mathf.Max(0, data.freezeCount) : 0;
            modelInstance = visualChild;
            currentOffsets = data.shape != null ? data.shape.GetRotatedOffsets(data.rotationSteps) : new List<Vector2Int> { Vector2Int.zero };

            // Розраховуємо геометричний центр форми фігури для симетричного масштабування та підйому
            Vector2Int shapeBounds = PieceData.shape != null ? PieceData.shape.GetRotatedBounds(PieceData.rotationSteps) : Vector2Int.one;
            visualCenter = new Vector3((shapeBounds.x - 1) * cellSize * 0.5f, 0f, (shapeBounds.y - 1) * cellSize * 0.5f);

            if (visualRoot == null)
            {
                visualRoot = new GameObject("VisualRoot");
                visualRoot.transform.SetParent(transform, false);
            }
            visualRoot.transform.localPosition = visualCenter;
            visualRoot.transform.localRotation = Quaternion.identity;
            visualRoot.transform.localScale = Vector3.one;

            if (modelInstance != null)
            {
                modelInstance.transform.SetParent(visualRoot.transform, false);
            }

            // Повністю знешкоджуємо фізику та старі скрипти на дочірній 3D-моделі
            if (modelInstance != null)
            {
                var rbs = modelInstance.GetComponentsInChildren<Rigidbody>(true);
                foreach (var rb in rbs)
                {
                    rb.isKinematic = true;
                    rb.detectCollisions = false;
                    Destroy(rb);
                }

                var oldMono = modelInstance.GetComponentsInChildren<MonoBehaviour>(true);
                foreach (var m in oldMono)
                {
                    if (m != this)
                    {
                        m.enabled = false;
                        Destroy(m);
                    }
                }

                var oldCols = modelInstance.GetComponentsInChildren<Collider>(true);
                foreach (var c in oldCols)
                {
                    c.enabled = false;
                    Destroy(c);
                }
            }

            UpdateWorldPosition();
            SetupVisualModel(palette);
            SetupRestrictionIcon(palette);
            SetupCollider();
            SetupOutline();
            SetupShadow();
            SetupIceShell();
            SetupFreezeBadge();
        }

        private void SetupCollider()
        {
            var existingCols = GetComponents<Collider>();
            foreach (var c in existingCols)
            {
                Destroy(c);
            }

            // Вимірюємо точну висоту моделі
            float modelHeight = 0.5f;
            if (modelInstance != null)
            {
                var renderers = modelInstance.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    modelHeight = renderers[0].bounds.size.y;
                }
            }

            // Створюємо окремий BoxCollider для кожної зайнятої клітинки фігури
            // Це виключає помилкові колізії на порожніх кутах L/T/Хрест форм
            foreach (var offset in currentOffsets)
            {
                var box = gameObject.AddComponent<BoxCollider>();
                box.center = new Vector3(offset.x * cellSize, modelHeight * 0.5f + 0.02f, offset.y * cellSize);
                box.size = new Vector3(cellSize * 0.95f, Mathf.Max(modelHeight, 0.4f), cellSize * 0.95f);
            }
        }

        private void SetupVisualModel(BlockPalette palette)
        {
            Color blockColor = PieceData.GetColor();
            if (IsFrozen)
            {
                blockColor = Color.Lerp(blockColor, new Color(0.55f, 0.85f, 1f, 1f), 0.32f);
            }

            if (modelInstance != null)
            {
                ApplyColor(modelInstance, blockColor);
                AlignAndScaleModel(modelInstance);
            }
            else if (palette != null && palette.legoStudUnitPrefab != null)
            {
                // Якщо немає 3D-моделі, процедурно збираємо блоки з одиничних секцій
                GameObject unitContainer = new GameObject("ProceduralVisuals");
                Transform parentT = visualRoot != null ? visualRoot.transform : transform;
                unitContainer.transform.SetParent(parentT, false);
                unitContainer.transform.localPosition = visualRoot != null ? -visualCenter : Vector3.zero;

                foreach (var offset in currentOffsets)
                {
                    GameObject unit = Instantiate(palette.legoStudUnitPrefab, unitContainer.transform);
                    unit.transform.localPosition = new Vector3(offset.x * cellSize, 0.02f, offset.y * cellSize);
                    ApplyColor(unit, blockColor);
                }

                modelInstance = unitContainer;
            }
        }

        private void AlignAndScaleModel(GameObject model)
        {
            if (model == null || PieceData.shape == null) return;

            Vector2Int shapeBounds = PieceData.shape.GetRotatedBounds(PieceData.rotationSteps);
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            // 1. Поворот моделі відповідно до rotationSteps (за годинниковою стрілкою +90° за крок)
            float angle = PieceData.rotationSteps * 90f;
            model.transform.localRotation = Quaternion.Euler(0f, angle, 0f);
            model.transform.localPosition = Vector3.zero;

            // 2. Вимірюємо габарити моделі
            Bounds bounds = renderers[0].bounds;
            foreach (var r in renderers)
            {
                if (r == restrictionIconRenderer) continue;
                bounds.Encapsulate(r.bounds);
            }

            float meshSizeX = bounds.size.x;
            float meshSizeZ = bounds.size.z;
            if (meshSizeZ < 0.05f && bounds.size.y > 0.05f)
            {
                meshSizeZ = bounds.size.y;
            }

            float targetSizeX = shapeBounds.x * cellSize;
            float targetSizeZ = shapeBounds.y * cellSize;

            // 3. Масштабуємо модель під клітинки сітки
            if (meshSizeX > 0.05f && targetSizeX > 0.05f)
            {
                float factorX = targetSizeX / meshSizeX;
                float factorZ = targetSizeZ / meshSizeZ;
                float scale = Mathf.Min(factorX, factorZ);

                model.transform.localScale = Vector3.one * scale;
            }

            // 4. Перераховуємо межі після масштабування
            bounds = renderers[0].bounds;
            foreach (var r in renderers)
            {
                if (r == restrictionIconRenderer) continue;
                bounds.Encapsulate(r.bounds);
            }

            // 5. Вирівнювання по X та Z (центр сітки)
            Vector3 targetCenter = new Vector3((shapeBounds.x - 1) * cellSize * 0.5f, 0f, (shapeBounds.y - 1) * cellSize * 0.5f);
            Vector3 currentCenterOffset = bounds.center - transform.position;
            float shiftX = targetCenter.x - currentCenterOffset.x;
            float shiftZ = targetCenter.z - currentCenterOffset.z;

            // 6. Вирівнювання по Y: піднімаємо низ моделі строго на поверхню столу (Y = 0.02f)
            float currentBottomY = bounds.min.y - transform.position.y;
            float shiftY = 0.02f - currentBottomY;

            model.transform.localPosition = new Vector3(shiftX, shiftY, shiftZ);
        }

        private void ApplyColor(GameObject target, Color color)
        {
            var renderers = target.GetComponentsInChildren<Renderer>();
            MaterialPropertyBlock mpb = new MaterialPropertyBlock();
            foreach (var r in renderers)
            {
                if (r == restrictionIconRenderer) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetColor("_Color", color);
                mpb.SetColor("_BaseColor", color);
                r.SetPropertyBlock(mpb);
            }
        }

        /// <summary>
        /// Refreshes the piece's visual material color (useful when toggling Colorblind Mode dynamically).
        /// </summary>
        public void RefreshColor()
        {
            if (PieceData == null) return;
            Color blockColor = PieceData.GetColor();
            if (IsFrozen)
            {
                blockColor = Color.Lerp(blockColor, new Color(0.55f, 0.85f, 1f, 1f), 0.32f);
            }
            if (modelInstance != null)
            {
                ApplyColor(modelInstance, blockColor);
            }
        }

        private void SetupRestrictionIcon(BlockPalette palette)
        {
            if (IsFrozen || PieceData.moveRestriction == MoveRestriction.Free || PieceData.moveRestriction == MoveRestriction.Locked)
            {
                if (restrictionIconRenderer != null) restrictionIconRenderer.gameObject.SetActive(false);
                return;
            }

            if (restrictionIconRenderer == null)
            {
                GameObject iconObj = new GameObject("RestrictionIcon");
                Transform parentT = visualRoot != null ? visualRoot.transform : transform;
                iconObj.transform.SetParent(parentT, false);
                restrictionIconRenderer = iconObj.AddComponent<SpriteRenderer>();
            }
            else if (visualRoot != null)
            {
                restrictionIconRenderer.transform.SetParent(visualRoot.transform, false);
            }

            restrictionIconRenderer.gameObject.SetActive(true);

            Sprite arrowSprite = null;
            if (PieceData.moveRestriction == MoveRestriction.HorizontalOnly && palette != null && palette.horizontalArrowSprite != null)
            {
                arrowSprite = palette.horizontalArrowSprite;
            }
            else if (PieceData.moveRestriction == MoveRestriction.VerticalOnly && palette != null && palette.verticalArrowSprite != null)
            {
                arrowSprite = palette.verticalArrowSprite;
            }

            if (arrowSprite == null)
            {
                arrowSprite = GetOrCreateProceduralRestrictionArrowSprite();
            }

            restrictionIconRenderer.sprite = arrowSprite;
            restrictionIconRenderer.color = Color.white;
            restrictionIconRenderer.sortingOrder = 25;

            if (restrictionIconRenderer.sharedMaterial == null)
            {
                Shader s = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (s != null) restrictionIconRenderer.sharedMaterial = new Material(s);
            }

            // Орієнтація: процедурний спрайт орієнтований вертикально (Вгору/Вниз)
            // Для VerticalOnly: поворот 0° -> стрілка вздовж World Z (Вгору/Вниз)
            // Для HorizontalOnly: поворот 90° -> стрілка вздовж World X (Вліво/Вправо)
            float rotAngle = (PieceData.moveRestriction == MoveRestriction.HorizontalOnly) ? 90f : 0f;
            restrictionIconRenderer.transform.localRotation = Quaternion.Euler(90f, 0f, rotAngle);

            // Масштаб значка на поверхні деталі (великий, виразний та адаптований до розміру фігури)
            Vector2Int shapeBounds = PieceData.shape != null ? PieceData.shape.GetRotatedBounds(PieceData.rotationSteps) : Vector2Int.one;
            int minDim = Mathf.Min(shapeBounds.x, shapeBounds.y);
            int maxDim = Mathf.Max(shapeBounds.x, shapeBounds.y);

            float baseScale;
            if (minDim >= 3)
            {
                baseScale = 1.45f;
            }
            else if (minDim >= 2)
            {
                baseScale = 1.30f;
            }
            else
            {
                baseScale = (maxDim >= 3) ? 1.10f : 0.95f;
            }

            float iconSize = cellSize * baseScale;
            restrictionIconRenderer.transform.localScale = new Vector3(iconSize, iconSize, 1f);

            // Визначаємо точну висоту поверхні деталі (рівень верху шипів моделі)
            float topY = 0.52f;
            if (modelInstance != null)
            {
                var rends = modelInstance.GetComponentsInChildren<Renderer>(true);
                foreach (var r in rends)
                {
                    if (r == restrictionIconRenderer || r.gameObject.name.StartsWith("Outline_")) continue;
                    float modelTop = r.bounds.max.y - transform.position.y;
                    if (modelTop > 0.1f && modelTop < 1.5f)
                    {
                        topY = Mathf.Max(topY, modelTop);
                    }
                }
            }

            // Розраховуємо геометричний центроїд деталі за зайнятими клітинками сітки
            float avgX = 0f, avgY = 0f;
            if (currentOffsets != null && currentOffsets.Count > 0)
            {
                foreach (var o in currentOffsets) { avgX += o.x; avgY += o.y; }
                avgX /= currentOffsets.Count;
                avgY /= currentOffsets.Count;
            }

            // Розміщення строго по центру верхньої поверхні деталі (між рядами шипів).
            // Оскільки стрілка лежить на площині Y = topY, у проекції камери під кутом 70°
            // 3D-центр між шипами строго проєктується у 2D оптичний центр між рядами шипів на екрані.
            Vector3 localPos = visualRoot != null
                ? new Vector3(avgX * cellSize - visualCenter.x, topY + 0.02f, avgY * cellSize - visualCenter.z)
                : new Vector3(avgX * cellSize, topY + 0.02f, avgY * cellSize);

            restrictionIconRenderer.transform.localPosition = localPos;
        }

        private static Sprite cachedRestrictionArrowSprite;

        /// <summary>
        /// Генерує високоякісний процедурний спрайт двонаправленої стрілки (↕) з білою заливкою та контрастним темним контуром.
        /// </summary>
        public static Sprite GetOrCreateProceduralRestrictionArrowSprite()
        {
            if (cachedRestrictionArrowSprite != null &&
                cachedRestrictionArrowSprite.texture != null &&
                cachedRestrictionArrowSprite.texture.name == "Procedural_DoubleEndedRestrictionArrow_V2")
            {
                return cachedRestrictionArrowSprite;
            }

            const int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = "Procedural_DoubleEndedRestrictionArrow_V2";
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[size * size];
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);

            // Геометрія двонаправленої стрілки з 4-осьовою симетрією (в позитивному квадранті X >= 0, Y >= 0):
            // Вершина вістря: (0, 0.88)
            // Зовнішній кут бородки вістря: (0.48, 0.34)
            // Внутрішня виїмка бородки: (0.22, 0.34)
            // Стрижень: від (0.22, 0) до (0.22, 0.34)
            Vector2 tip = new Vector2(0f, 0.88f);
            Vector2 barbCorner = new Vector2(0.48f, 0.34f);
            Vector2 barbNotch = new Vector2(0.22f, 0.34f);
            Vector2 shaftCenter = new Vector2(0.22f, 0f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x - center.x) / (size * 0.5f);
                    float ny = (y - center.y) / (size * 0.5f);

                    Vector2 p = new Vector2(Mathf.Abs(nx), Mathf.Abs(ny));

                    float dShaft = DistanceToSegment2D(p, shaftCenter, barbNotch);
                    float dShelf = DistanceToSegment2D(p, barbNotch, barbCorner);
                    float dSlant = DistanceToSegment2D(p, barbCorner, tip);

                    float dist = Mathf.Min(dShaft, Mathf.Min(dShelf, dSlant));

                    bool inside = false;
                    if (p.y <= 0.34f)
                    {
                        inside = (p.x <= 0.22f);
                    }
                    else if (p.y <= 0.88f)
                    {
                        inside = (p.x <= 0.48f * (0.88f - p.y) / (0.88f - 0.34f));
                    }

                    float sd = inside ? -dist : dist;

                    float aaWidth = 0.024f;
                    float fillAlpha = Mathf.Clamp01(0.5f - sd / aaWidth);
                    float outlineThickness = 0.075f;
                    float outlineAlpha = Mathf.Clamp01(0.5f - (sd - outlineThickness) / aaWidth);

                    Color col = Color.clear;
                    if (fillAlpha > 0.005f)
                    {
                        col = Color.Lerp(new Color(0.10f, 0.10f, 0.12f, 1f), Color.white, fillAlpha);
                    }
                    else if (outlineAlpha > 0.005f)
                    {
                        col = new Color(0.08f, 0.08f, 0.10f, outlineAlpha * 0.95f);
                    }

                    pixels[y * size + x] = col;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, true);

            cachedRestrictionArrowSprite = Sprite.Create(
                tex,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                size,
                0,
                SpriteMeshType.FullRect
            );

            return cachedRestrictionArrowSprite;
        }

        private static float DistanceToSegment2D(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 pa = p - a;
            Vector2 ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude;
        }

        public List<Vector2Int> GetCurrentOccupiedCells()
        {
            List<Vector2Int> cells = new List<Vector2Int>(currentOffsets.Count);
            foreach (var offset in currentOffsets)
            {
                cells.Add(CurrentOrigin + offset);
            }
            return cells;
        }

        public void UpdateWorldPosition()
        {
            transform.localPosition = new Vector3(CurrentOrigin.x * cellSize, 0.1f, CurrentOrigin.y * cellSize);
        }

        private Vector3 pointerOffset;
        private Vector2Int lastValidGridPos;

        // ==========================================
        // EventSystem Touch & Drag Реалізація
        // ==========================================

        public void OnPointerDown(PointerEventData eventData)
        {
            if (isExiting || isSnapping || levelLoader == null || !levelLoader.IsGameplayActive) return;

            // Приховуємо активну підказку, щойно гравець торкається деталі
            HintIndicatorEffect.Dismiss();

            // Перехоплення та блокування кліку при активному бустері "Молоток"
            if (HammerBooster.IsHammerActive)
            {
                if (HammerBooster.IsTargeting)
                {
                    HammerBooster.SelectTarget(this);
                }
                return;
            }

            if (IsFrozen)
            {
                PlayFrozenShake();
                return;
            }

            if (BoosterTutorialManager.IsBoosterTutorialActive) return;

            if (PieceData.moveRestriction == MoveRestriction.Locked) return;

            // Підйом блоку при торканні + вібровідгук
            isLifted = true;
            wasLifted = true;
            targetLiftY = liftHeight;
            if (GameSettingsManager.HasInstance)
            {
                GameSettingsManager.Instance.TriggerHaptic();
            }

            Vector3 touchWorld = GetWorldPointerPosition(eventData);
            Vector3 touchLocal = transform.parent != null ? transform.parent.InverseTransformPoint(touchWorld) : touchWorld;
            pointerOffset = transform.localPosition - touchLocal;
            lastValidGridPos = CurrentOrigin;
            dragStartGridPos = CurrentOrigin;
            dragStartLocalPosition = transform.localPosition;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (HammerBooster.IsHammerActive)
            {
                isLifted = false;
                targetLiftY = 0f;
                SetOutlineActive(false);
                return;
            }

            if (!isDragging)
            {
                isLifted = false;
                targetLiftY = 0f;
                SetOutlineActive(false);
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (HammerBooster.IsHammerActive) return;
            if (isExiting || isSnapping || levelLoader == null || !levelLoader.IsGameplayActive) return;
            if (PieceData.moveRestriction == MoveRestriction.Locked) return;
            if (IsFrozen)
            {
                PlayFrozenShake();
                return;
            }
            if (BoosterTutorialManager.IsBoosterTutorialActive) return;

            isDragging = true;
            isLifted = true;
            wasLifted = true;
            targetLiftY = liftHeight;
            SetOutlineActive(true);
            Vector3 touchWorld = GetWorldPointerPosition(eventData);
            Vector3 touchLocal = transform.parent != null ? transform.parent.InverseTransformPoint(touchWorld) : touchWorld;
            pointerOffset = transform.localPosition - touchLocal;
            lastValidGridPos = CurrentOrigin;
            dragStartGridPos = CurrentOrigin;
            dragStartLocalPosition = transform.localPosition;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (IsFrozen) return;
            if (BoosterTutorialManager.IsBoosterTutorialActive || HammerBooster.IsHammerActive)
            {
                if (isDragging)
                {
                    isDragging = false;
                    isLifted = false;
                    targetLiftY = 0f;
                    StartCoroutine(SnapToGridRoutine(CurrentOrigin));
                }
                return;
            }

            if (levelLoader != null && !levelLoader.IsGameplayActive)
            {
                if (isDragging)
                {
                    isDragging = false;
                    isLifted = false;
                    targetLiftY = 0f;
                    StartCoroutine(SnapToGridRoutine(CurrentOrigin));
                }
                return;
            }

            if (!isDragging && !isSnapping && !isExiting)
            {
                isDragging = true;
                isLifted = true;
                wasLifted = true;
                targetLiftY = liftHeight;
                Vector3 touchWorld = GetWorldPointerPosition(eventData);
                Vector3 touchLocal = transform.parent != null ? transform.parent.InverseTransformPoint(touchWorld) : touchWorld;
                pointerOffset = transform.localPosition - touchLocal;
                lastValidGridPos = CurrentOrigin;
                dragStartGridPos = CurrentOrigin;
                dragStartLocalPosition = transform.localPosition;
            }

            if (!isDragging || isExiting) return;

            Vector3 touchWorldPos = GetWorldPointerPosition(eventData);
            Vector3 touchLocalPos = transform.parent != null ? transform.parent.InverseTransformPoint(touchWorldPos) : touchWorldPos;
            Vector3 desiredPos = touchLocalPos + pointerOffset;

            // 1. Отримуємо актуальний діапазон вільних клітинок від поточної зафіксованої позиції
            int minX = 0, maxX = 0, minZ = 0, maxZ = 0;

            if (PieceData.moveRestriction != MoveRestriction.VerticalOnly && PieceData.moveRestriction != MoveRestriction.Locked)
            {
                levelLoader.GetSlideRangeHorizontal(this, lastValidGridPos, out minX, out maxX);
            }
            if (PieceData.moveRestriction != MoveRestriction.HorizontalOnly && PieceData.moveRestriction != MoveRestriction.Locked)
            {
                levelLoader.GetSlideRangeVertical(this, lastValidGridPos, out minZ, out maxZ);
            }

            float minWorldX = (lastValidGridPos.x + minX) * cellSize;
            float maxWorldX = (lastValidGridPos.x + maxX) * cellSize;
            float minWorldZ = (lastValidGridPos.y + minZ) * cellSize;
            float maxWorldZ = (lastValidGridPos.y + maxZ) * cellSize;

            // 2. Безперервне плавне обмеження руху
            float clampedX = Mathf.Clamp(desiredPos.x, minWorldX, maxWorldX);
            float clampedZ = Mathf.Clamp(desiredPos.z, minWorldZ, maxWorldZ);

            if (PieceData.moveRestriction == MoveRestriction.HorizontalOnly)
            {
                clampedZ = lastValidGridPos.y * cellSize;
            }
            else if (PieceData.moveRestriction == MoveRestriction.VerticalOnly)
            {
                clampedX = lastValidGridPos.x * cellSize;
            }

            // Плавне переміщення строго за пальцем без ривків
            transform.localPosition = new Vector3(clampedX, 0.1f, clampedZ);

            // 3. Оновлення поточної зайнятої клітинки на сітці з роздільною перевіркою осей
            int targetX = Mathf.RoundToInt(clampedX / cellSize);
            int targetY = Mathf.RoundToInt(clampedZ / cellSize);

            float deltaX = Mathf.Abs(clampedX - lastValidGridPos.x * cellSize);
            float deltaZ = Mathf.Abs(clampedZ - lastValidGridPos.y * cellSize);

            if (deltaX >= deltaZ)
            {
                // Перевіряємо крок по головній осі X
                Vector2Int candX = new Vector2Int(targetX, lastValidGridPos.y);
                if (candX != lastValidGridPos && levelLoader.CanMovePieceTo(this, candX))
                {
                    lastValidGridPos = candX;
                    CurrentOrigin = candX;
                    if (levelLoader != null) levelLoader.PlayPieceMoveSound();
                    if (GameSettingsManager.HasInstance) GameSettingsManager.Instance.TriggerHapticMove();
                }

                // Перевіряємо крок по осі Z
                Vector2Int candZ = new Vector2Int(lastValidGridPos.x, targetY);
                if (candZ != lastValidGridPos && levelLoader.CanMovePieceTo(this, candZ))
                {
                    lastValidGridPos = candZ;
                    CurrentOrigin = candZ;
                    if (levelLoader != null) levelLoader.PlayPieceMoveSound();
                    if (GameSettingsManager.HasInstance) GameSettingsManager.Instance.TriggerHapticMove();
                }
            }
            else
            {
                // Перевіряємо крок по головній осі Z
                Vector2Int candZ = new Vector2Int(lastValidGridPos.x, targetY);
                if (candZ != lastValidGridPos && levelLoader.CanMovePieceTo(this, candZ))
                {
                    lastValidGridPos = candZ;
                    CurrentOrigin = candZ;
                    if (levelLoader != null) levelLoader.PlayPieceMoveSound();
                    if (GameSettingsManager.HasInstance) GameSettingsManager.Instance.TriggerHapticMove();
                }

                // Перевіряємо крок по осі X
                Vector2Int candX = new Vector2Int(targetX, lastValidGridPos.y);
                if (candX != lastValidGridPos && levelLoader.CanMovePieceTo(this, candX))
                {
                    lastValidGridPos = candX;
                    CurrentOrigin = candX;
                    if (levelLoader != null) levelLoader.PlayPieceMoveSound();
                    if (GameSettingsManager.HasInstance) GameSettingsManager.Instance.TriggerHapticMove();
                }
            }

            // Початок відліку таймера на рівні та вимикання руки-підказки при першому ДІЙСНОМУ переході на нову клітинку
            if (levelLoader != null && !levelLoader.HasFirstMoveOccurred)
            {
                if (CurrentOrigin != dragStartGridPos)
                {
                    levelLoader.NotifyBlockMoved();
                }
            }

            // Миттєвий вихід у ворота під час перетягування (навіть якщо палець ще не відпущено)
            if (!isExiting && levelLoader.CheckIfPieceExits(this, CurrentOrigin, out ExitDirection exitDir))
            {
                isDragging = false;
                StartCoroutine(PlayExitAnimation(exitDir));
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            SetOutlineActive(false);
            if (IsFrozen) return;

            if (BoosterTutorialManager.IsBoosterTutorialActive || HammerBooster.IsHammerActive)
            {
                if (isDragging)
                {
                    isDragging = false;
                    isLifted = false;
                    targetLiftY = 0f;
                    StartCoroutine(SnapToGridRoutine(CurrentOrigin));
                }
                return;
            }

            if (!isDragging || isExiting) return;
            isDragging = false;
            isLifted = false;
            targetLiftY = 0f;

            int targetGridX = Mathf.RoundToInt(transform.localPosition.x / cellSize);
            int targetGridY = Mathf.RoundToInt(transform.localPosition.z / cellSize);

            Vector2Int targetOrigin = new Vector2Int(targetGridX, targetGridY);

            bool actuallyMoved = (CurrentOrigin != dragStartGridPos) || (targetOrigin != dragStartGridPos);

            if (actuallyMoved)
            {
                if (levelLoader != null && !levelLoader.HasFirstMoveOccurred)
                {
                    levelLoader.NotifyBlockMoved();
                }
            }
            else
            {
                // Гравець не змінив клітинку (відпустив деталь назад) — повертаємо/гарантуємо руку-підказку
                if (levelLoader != null && !levelLoader.HasFirstMoveOccurred)
                {
                    levelLoader.EnsureFirstLevelTutorialHand();
                }
            }

            if (levelLoader.CanMovePieceTo(this, targetOrigin))
            {
                CurrentOrigin = targetOrigin;
            }

            if (levelLoader != null)
            {
                levelLoader.PlayPieceStepSound();
            }

            StartCoroutine(SnapToGridRoutine(CurrentOrigin));
        }

        private void OnDisable()
        {
            SetOutlineActive(false);
            isLifted = false;
            targetLiftY = 0f;
            currentLiftY = 0f;
        }

        private void Update()
        {
            if (isExiting) return;

            // 1. Плавна інтерполяція висоти підйому (Spring decay)
            float speed = targetLiftY > currentLiftY ? liftSpeed : dropSpeed;
            if (Mathf.Abs(currentLiftY - targetLiftY) > 0.0005f)
            {
                currentLiftY = Mathf.Lerp(currentLiftY, targetLiftY, 1f - Mathf.Exp(-speed * Time.deltaTime));
            }
            else
            {
                currentLiftY = targetLiftY;
            }

            // 2. Детекція моменту приземлення для мікро-амортизації (Juice Bounce)
            if (wasLifted && !isLifted && currentLiftY < 0.015f)
            {
                wasLifted = false;
                landingBounceTimer = LandingBounceDuration;
            }

            float landingScaleFactor = 1f;
            float landingYOffset = 0f;
            if (landingBounceTimer > 0f)
            {
                landingBounceTimer -= Time.deltaTime;
                float t = Mathf.Clamp01(landingBounceTimer / LandingBounceDuration);
                float bounce = Mathf.Sin(t * Mathf.PI);
                landingYOffset = -bounce * 0.018f; // легке пружне просідання при ударі об основу
                landingScaleFactor = 1f - (bounce * 0.012f);
            }

            // 3. Застосування трансформацій до VisualRoot
            if (visualRoot != null)
            {
                float totalY = currentLiftY + landingYOffset;
                visualRoot.transform.localPosition = visualCenter + new Vector3(0f, totalY, 0f);

                float liftProgress = liftHeight > 0.001f ? Mathf.Clamp01(currentLiftY / liftHeight) : 0f;
                float baseScale = Mathf.Lerp(1f, liftScaleMultiplier, liftProgress);
                visualRoot.transform.localScale = Vector3.one * (baseScale * landingScaleFactor);

                // 4. Оновлення динамічної тіні на підлозі під блоком
                if (enableLiftShadow && shadowRoot != null)
                {
                    UpdateShadowVisuals(liftProgress);
                }
            }
        }

        private IEnumerator SnapToGridRoutine(Vector2Int targetOrigin)
        {
            if (isExiting) yield break;
            isSnapping = true;
            isLifted = false;
            targetLiftY = 0f;

            Vector3 startPos = transform.localPosition;
            Vector3 endPos = new Vector3(targetOrigin.x * cellSize, 0.1f, targetOrigin.y * cellSize);

            float elapsed = 0f;
            while (elapsed < snapDuration)
            {
                if (isExiting) yield break;
                float dt = Time.timeScale > 0.001f ? Time.deltaTime : Time.unscaledDeltaTime;
                elapsed += dt;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / snapDuration);
                transform.localPosition = Vector3.Lerp(startPos, endPos, t);
                yield return null;
            }

            transform.localPosition = endPos;
            CurrentOrigin = targetOrigin;
            isSnapping = false;

            if (!isExiting && levelLoader.CheckIfPieceExits(this, CurrentOrigin, out ExitDirection exitDir))
            {
                StartCoroutine(PlayExitAnimation(exitDir));
            }
        }

        private IEnumerator PlayExitAnimation(ExitDirection direction)
        {
            isExiting = true;
            isDragging = false;
            isSnapping = false;
            isLifted = false;
            targetLiftY = 0f;
            currentLiftY = 0f;
            if (shadowRoot != null) shadowRoot.SetActive(false);
            if (visualRoot != null)
            {
                visualRoot.transform.localPosition = visualCenter;
                visualRoot.transform.localScale = Vector3.one;
            }
            SetOutlineActive(false);

            if (levelLoader != null)
            {
                levelLoader.NotifyBlockMoved();
                levelLoader.NotifyGateReaction(this, direction);
                levelLoader.PlayPieceExitSound();
            }

            if (GameSettingsManager.HasInstance)
            {
                GameSettingsManager.Instance.TriggerHapticExit();
            }

            // 1. Увімкнення режиму напівпрозорості на матеріалах блоку (URP Transparent Surface)
            List<Renderer> activeRenderers = new List<Renderer>();
            Color basePieceColor = PieceData != null ? PieceData.GetColor() : Color.white;

            if (modelInstance != null)
            {
                var rends = modelInstance.GetComponentsInChildren<Renderer>(true);
                foreach (var r in rends)
                {
                    if (r.gameObject.name.StartsWith("Outline_")) continue;
                    if (r == restrictionIconRenderer) continue;
                    activeRenderers.Add(r);

                    foreach (var mat in r.materials)
                    {
                        mat.SetFloat("_Surface", 1f); // 1 = Transparent
                        mat.SetOverrideTag("RenderType", "Transparent");
                        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        mat.SetInt("_ZWrite", 0);
                        mat.DisableKeyword("_ALPHATEST_ON");
                        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    }
                }
            }

            Vector3 exitOffset = direction switch
            {
                ExitDirection.Up => Vector3.forward * 12f,
                ExitDirection.Down => Vector3.back * 12f,
                ExitDirection.Left => Vector3.left * 12f,
                ExitDirection.Right => Vector3.right * 12f,
                _ => Vector3.forward * 12f
            };

            Vector3 startPos = transform.localPosition;
            Vector3 endPos = startPos + exitOffset;
            Vector3 baseScale = transform.localScale;

            // Комбінація 4 (Warp Funnel): розтягування по осі руху та сплющення по боках
            Vector3 funnelScale = baseScale;
            if (direction == ExitDirection.Up || direction == ExitDirection.Down)
            {
                funnelScale.z *= 1.32f;
                funnelScale.x *= 0.84f;
            }
            else
            {
                funnelScale.x *= 1.32f;
                funnelScale.z *= 0.84f;
            }

            MaterialPropertyBlock fadeMpb = new MaterialPropertyBlock();
            float duration = 0.36f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);

                // Прискорення вильоту у ворота (Warp acceleration)
                float tPos = Mathf.Pow(progress, 1.7f);
                transform.localPosition = Vector3.Lerp(startPos, endPos, tPos);

                // Пружне розтягування блоку при вході у ворота
                float tScale = Mathf.Sin(progress * Mathf.PI);
                transform.localScale = Vector3.Lerp(baseScale, funnelScale, tScale);

                // Напівпрозорість: альфа плавно падає з 1.0f до 0.25f, потім у нуль наприкінці
                float alpha = Mathf.Lerp(1.0f, 0.25f, progress / 0.7f);
                if (progress > 0.7f)
                {
                    alpha = Mathf.Lerp(0.25f, 0f, (progress - 0.7f) / 0.3f);
                }

                Color fadedColor = new Color(basePieceColor.r, basePieceColor.g, basePieceColor.b, alpha);
                foreach (var r in activeRenderers)
                {
                    if (r != null)
                    {
                        r.GetPropertyBlock(fadeMpb);
                        fadeMpb.SetColor("_Color", fadedColor);
                        fadeMpb.SetColor("_BaseColor", fadedColor);
                        r.SetPropertyBlock(fadeMpb);
                    }
                }

                if (restrictionIconRenderer != null)
                {
                    Color iconCol = restrictionIconRenderer.color;
                    restrictionIconRenderer.color = new Color(iconCol.r, iconCol.g, iconCol.b, alpha);
                }

                yield return null;
            }

            transform.localScale = baseScale;
            OnExited?.Invoke(this);
            gameObject.SetActive(false);
        }

        // ==========================================
        // 3D Outline (Обводка блоку при русі)
        // ==========================================

        private List<GameObject> outlineObjects = new List<GameObject>();
        private static Material sharedOutlineMaterial;

        public void SetOutlineActive(bool active)
        {
            if (outlineObjects == null) return;
            for (int i = 0; i < outlineObjects.Count; i++)
            {
                if (outlineObjects[i] != null && outlineObjects[i].activeSelf != active)
                {
                    outlineObjects[i].SetActive(active);
                }
            }
        }

        private void SetupOutline()
        {
            if (modelInstance == null) return;

            if (outlineObjects != null)
            {
                foreach (var obj in outlineObjects)
                {
                    if (obj != null) Destroy(obj);
                }
                outlineObjects.Clear();
            }
            else
            {
                outlineObjects = new List<GameObject>();
            }

            Material outlineMat = GetOrCreateOutlineMaterial();

            // 1. SkinnedMeshRenderer (3D-моделі деталей з BlendShapes морфінгом)
            var smrs = modelInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var smr in smrs)
            {
                if (smr == null || smr.sharedMesh == null) continue;
                if (smr.gameObject.name.StartsWith("Outline_")) continue;

                GameObject outlineChild = new GameObject("Outline_Skinned");
                outlineChild.transform.SetParent(smr.transform, false);
                outlineChild.transform.localPosition = Vector3.zero;
                outlineChild.transform.localRotation = Quaternion.identity;
                outlineChild.transform.localScale = Vector3.one;

                Mesh bakedMesh = new Mesh();
                bakedMesh.name = smr.name + "_BakedOutline";
                smr.BakeMesh(bakedMesh);

                BakeSmoothedNormalsToTangents(bakedMesh);

                MeshFilter outMf = outlineChild.AddComponent<MeshFilter>();
                outMf.sharedMesh = bakedMesh;

                MeshRenderer outMr = outlineChild.AddComponent<MeshRenderer>();
                outMr.sharedMaterial = outlineMat;
                outMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                outMr.receiveShadows = false;

                outlineChild.SetActive(false);
                outlineObjects.Add(outlineChild);
            }

            // 2. MeshFilter (процедурні або стандартні меші без скінінгу)
            var meshFilters = modelInstance.GetComponentsInChildren<MeshFilter>(true);
            foreach (var mf in meshFilters)
            {
                if (mf == null || mf.sharedMesh == null) continue;
                if (mf.gameObject.name.StartsWith("Outline_")) continue;
                if (mf.GetComponent<SkinnedMeshRenderer>() != null) continue;

                GameObject outlineChild = new GameObject("Outline_Mesh");
                outlineChild.transform.SetParent(mf.transform, false);
                outlineChild.transform.localPosition = Vector3.zero;
                outlineChild.transform.localRotation = Quaternion.identity;
                outlineChild.transform.localScale = Vector3.one;

                Mesh outlineMesh = Instantiate(mf.sharedMesh);
                outlineMesh.name = mf.sharedMesh.name + "_Outline";
                BakeSmoothedNormalsToTangents(outlineMesh);

                MeshFilter outMf = outlineChild.AddComponent<MeshFilter>();
                outMf.sharedMesh = outlineMesh;

                MeshRenderer outMr = outlineChild.AddComponent<MeshRenderer>();
                outMr.sharedMaterial = outlineMat;
                outMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                outMr.receiveShadows = false;

                outlineChild.SetActive(false);
                outlineObjects.Add(outlineChild);
            }
        }

        private static Material GetOrCreateOutlineMaterial()
        {
            if (sharedOutlineMaterial != null) return sharedOutlineMaterial;

            Material loadedMat = Resources.Load<Material>("Materials/BlockOutlineMaterial")
                              ?? Resources.Load<Material>("BlockOutlineMaterial");
            if (loadedMat != null)
            {
                sharedOutlineMaterial = new Material(loadedMat);
                sharedOutlineMaterial.name = "M_BlockOutline";
                return sharedOutlineMaterial;
            }

            Shader s = Shader.Find("Custom/BlockOutline")
                    ?? Shader.Find("Universal Render Pipeline/Unlit")
                    ?? Shader.Find("Sprites/Default");

            sharedOutlineMaterial = new Material(s);
            sharedOutlineMaterial.name = "M_BlockOutline";
            sharedOutlineMaterial.SetColor("_OutlineColor", new Color(1f, 1f, 1f, 0.95f));
            sharedOutlineMaterial.SetFloat("_OutlineWidth", 0.045f);
            return sharedOutlineMaterial;
        }

        private static void BakeSmoothedNormalsToTangents(Mesh mesh)
        {
            if (mesh == null) return;
            try
            {
                Vector3[] vertices = mesh.vertices;
                Vector3[] normals = mesh.normals;

                if (normals != null && normals.Length == vertices.Length)
                {
                    Dictionary<Vector3, Vector3> averageNormals = new Dictionary<Vector3, Vector3>(vertices.Length);
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        Vector3 v = vertices[i];
                        Vector3 key = new Vector3(Mathf.Round(v.x * 1000f) / 1000f, Mathf.Round(v.y * 1000f) / 1000f, Mathf.Round(v.z * 1000f) / 1000f);
                        if (averageNormals.TryGetValue(key, out Vector3 accumulated))
                        {
                            averageNormals[key] = accumulated + normals[i];
                        }
                        else
                        {
                            averageNormals[key] = normals[i];
                        }
                    }

                    Vector4[] tangents = new Vector4[vertices.Length];
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        Vector3 v = vertices[i];
                        Vector3 key = new Vector3(Mathf.Round(v.x * 1000f) / 1000f, Mathf.Round(v.y * 1000f) / 1000f, Mathf.Round(v.z * 1000f) / 1000f);
                        Vector3 avg = averageNormals[key].normalized;
                        tangents[i] = new Vector4(avg.x, avg.y, avg.z, 1f);
                    }

                    mesh.tangents = tangents;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LegoPieceView] Error baking smoothed tangents: {ex.Message}");
            }
        }

        private void OnDestroy()
        {
            if (outlineObjects != null)
            {
                foreach (var obj in outlineObjects)
                {
                    if (obj != null) Destroy(obj);
                }
                outlineObjects.Clear();
            }

            if (shadowRoot != null)
            {
                Destroy(shadowRoot);
                shadowRoot = null;
            }

            if (iceShellObj != null)
            {
                Destroy(iceShellObj);
                iceShellObj = null;
            }

            if (freezeBadgeObj != null)
            {
                Destroy(freezeBadgeObj);
                freezeBadgeObj = null;
            }
        }

        // ==========================================
        // Динамічна Тінь під Блоком (Dynamic Contact Shadow)
        // ==========================================

        private static Mesh sharedQuadMesh;
        private static Texture2D sharedShadowTexture;
        private static Material sharedShadowMaterial;
        private static MaterialPropertyBlock shadowMpb;

        private void SetupShadow()
        {
            if (!enableLiftShadow || currentOffsets == null || currentOffsets.Count == 0) return;

            if (shadowRoot != null)
            {
                Destroy(shadowRoot);
            }

            shadowRoot = new GameObject("ShadowRoot");
            shadowRoot.transform.SetParent(transform, false);
            // Плитки поля лежать на y = -0.01f в parent space. transform лежить на y = 0.1f.
            // Тому -0.088f у локальних координатах дає y = 0.012f (на 0.022f вище плиток без z-fighting)
            shadowRoot.transform.localPosition = new Vector3(0f, -0.088f, 0f);
            shadowRoot.transform.localRotation = Quaternion.identity;
            shadowRoot.transform.localScale = Vector3.one;

            Mesh quadMesh = GetOrCreateQuadMesh();
            Material shadowMat = GetOrCreateShadowMaterial();

            shadowRenderers = new List<MeshRenderer>(currentOffsets.Count);
            foreach (var offset in currentOffsets)
            {
                GameObject shadowCell = new GameObject($"ShadowCell_{offset.x}_{offset.y}");
                shadowCell.transform.SetParent(shadowRoot.transform, false);
                shadowCell.transform.localPosition = new Vector3(offset.x * cellSize, 0f, offset.y * cellSize);
                shadowCell.transform.localRotation = Quaternion.identity;
                shadowCell.transform.localScale = new Vector3(cellSize * 1.12f, 1f, cellSize * 1.12f);

                MeshFilter mf = shadowCell.AddComponent<MeshFilter>();
                mf.sharedMesh = quadMesh;

                MeshRenderer mr = shadowCell.AddComponent<MeshRenderer>();
                mr.sharedMaterial = shadowMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

                shadowRenderers.Add(mr);
            }

            UpdateShadowVisuals(0f);
        }

        private void UpdateShadowVisuals(float liftProgress)
        {
            if (shadowRoot == null || shadowRenderers == null) return;

            // При підйомі тінь плавно розширюється (розсіювання світла)
            float shadowScale = Mathf.Lerp(1.0f, 1.07f, liftProgress);
            shadowRoot.transform.localScale = new Vector3(shadowScale, 1f, shadowScale);

            // Альфа стає м'якшою на висоті
            float baseAlpha = 0.44f;
            float liftedAlpha = 0.28f;
            float currentAlpha = Mathf.Lerp(baseAlpha, liftedAlpha, liftProgress);

            if (shadowMpb == null) shadowMpb = new MaterialPropertyBlock();
            Color shadowColor = new Color(0f, 0f, 0f, currentAlpha);

            for (int i = 0; i < shadowRenderers.Count; i++)
            {
                if (shadowRenderers[i] != null)
                {
                    shadowRenderers[i].GetPropertyBlock(shadowMpb);
                    shadowMpb.SetColor("_BaseColor", shadowColor);
                    shadowMpb.SetColor("_Color", shadowColor);
                    shadowRenderers[i].SetPropertyBlock(shadowMpb);
                }
            }
        }

        private static Mesh GetOrCreateQuadMesh()
        {
            if (sharedQuadMesh != null) return sharedQuadMesh;

            sharedQuadMesh = new Mesh();
            sharedQuadMesh.name = "PieceShadowQuad";
            sharedQuadMesh.vertices = new Vector3[]
            {
                new Vector3(-0.5f, 0f, -0.5f),
                new Vector3(0.5f, 0f, -0.5f),
                new Vector3(-0.5f, 0f, 0.5f),
                new Vector3(0.5f, 0f, 0.5f)
            };
            sharedQuadMesh.uv = new Vector2[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            };
            sharedQuadMesh.triangles = new int[] { 0, 2, 1, 2, 3, 1 };
            sharedQuadMesh.RecalculateNormals();
            return sharedQuadMesh;
        }

        private static Texture2D GetOrCreateShadowTexture()
        {
            if (sharedShadowTexture != null) return sharedShadowTexture;

            int size = 64;
            sharedShadowTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            sharedShadowTexture.name = "T_PieceBlobShadow";
            sharedShadowTexture.wrapMode = TextureWrapMode.Clamp;
            sharedShadowTexture.filterMode = FilterMode.Bilinear;

            float center = (size - 1) * 0.5f;
            float radius = center * 0.95f;

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = Mathf.Abs((x - center) / radius);
                    float ny = Mathf.Abs((y - center) / radius);

                    // Супер-еліпс / закруглений прямокутник для м'якого контуру деталі
                    float p = 3.2f;
                    float dist = Mathf.Pow(Mathf.Pow(nx, p) + Mathf.Pow(ny, p), 1f / p);

                    float alpha = Mathf.Clamp01(1f - dist);
                    alpha = Mathf.SmoothStep(0f, 1f, alpha);
                    alpha = Mathf.Pow(alpha, 1.2f);

                    pixels[y * size + x] = new Color(0f, 0f, 0f, alpha);
                }
            }
            sharedShadowTexture.SetPixels(pixels);
            sharedShadowTexture.Apply(false, true);
            return sharedShadowTexture;
        }

        private static Material GetOrCreateShadowMaterial()
        {
            if (sharedShadowMaterial != null) return sharedShadowMaterial;

            Shader s = Shader.Find("Universal Render Pipeline/Unlit");
            if (s == null) s = Shader.Find("Sprites/Default");
            if (s == null) s = Shader.Find("Unlit/Transparent");

            sharedShadowMaterial = new Material(s);
            sharedShadowMaterial.name = "M_PieceBlobShadow";

            Texture2D tex = GetOrCreateShadowTexture();
            sharedShadowMaterial.mainTexture = tex;
            if (sharedShadowMaterial.HasProperty("_BaseMap"))
            {
                sharedShadowMaterial.SetTexture("_BaseMap", tex);
            }
            if (sharedShadowMaterial.HasProperty("_MainTex"))
            {
                sharedShadowMaterial.SetTexture("_MainTex", tex);
            }

            if (sharedShadowMaterial.HasProperty("_Surface"))
            {
                sharedShadowMaterial.SetFloat("_Surface", 1f); // Transparent
            }
            sharedShadowMaterial.SetOverrideTag("RenderType", "Transparent");
            sharedShadowMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            sharedShadowMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            sharedShadowMaterial.SetInt("_ZWrite", 0);
            sharedShadowMaterial.DisableKeyword("_ALPHATEST_ON");
            sharedShadowMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            sharedShadowMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent - 20;

            return sharedShadowMaterial;
        }

        private Vector3 GetWorldPointerPosition(PointerEventData eventData)
        {
            Camera cam = eventData.pressEventCamera != null ? eventData.pressEventCamera : Camera.main;
            if (cam == null) return transform.position;

            Ray ray = cam.ScreenPointToRay(eventData.position);
            Plane plane = new Plane(Vector3.up, new Vector3(0f, 0.1f, 0f));

            if (plane.Raycast(ray, out float enter))
            {
                return ray.GetPoint(enter);
            }

            return transform.position;
        }

        /// <summary>
        /// Створює соковитий 3D-вибух уламків цеглинок LEGO при ударі молотком.
        /// </summary>
        public void SpawnShatterDebris()
        {
            Color pieceColor = PieceData != null ? PieceData.GetColor() : Color.red;
            Vector3 center = transform.position;

            GameObject debrisRoot = new GameObject("LegoShatterDebris");
            debrisRoot.transform.position = center;

            int chunkCount = Mathf.Clamp(currentOffsets != null ? currentOffsets.Count * 4 : 8, 8, 18);
            for (int i = 0; i < chunkCount; i++)
            {
                GameObject chunk = GameObject.CreatePrimitive(PrimitiveType.Cube);
                chunk.transform.SetParent(debrisRoot.transform);

                Vector3 spawnOffset = new Vector3(
                    UnityEngine.Random.Range(-0.4f, 0.4f) * cellSize,
                    UnityEngine.Random.Range(0.05f, 0.45f),
                    UnityEngine.Random.Range(-0.4f, 0.4f) * cellSize
                );
                chunk.transform.position = center + spawnOffset;

                float size = UnityEngine.Random.Range(0.14f, 0.26f) * cellSize;
                chunk.transform.localScale = new Vector3(size, size * 0.75f, size);

                var r = chunk.GetComponent<Renderer>();
                if (r != null)
                {
                    r.material.color = Color.Lerp(pieceColor, Color.white, UnityEngine.Random.Range(0f, 0.3f));
                }

                var col = chunk.GetComponent<Collider>();
                if (col != null) Destroy(col);

                Vector3 blastDir = (spawnOffset.normalized + Vector3.up * 1.8f).normalized;
                Vector3 initialVel = blastDir * UnityEngine.Random.Range(4f, 7.5f) + UnityEngine.Random.insideUnitSphere * 1.5f;
                Vector3 rotSpeed = UnityEngine.Random.insideUnitSphere * 360f;

                debrisRoot.AddComponent<PieceDebrisMotion>().Launch(chunk, initialVel, rotSpeed, levelLoader != null ? levelLoader.BoardBaseY : 0f);
            }

            Destroy(debrisRoot, 1.4f);
        }

        // ==========================================
        // Механіка Замороженого Блоку (Frozen Block)
        // ==========================================

        private float GetModelHeight()
        {
            float modelHeight = 0.52f;
            if (modelInstance != null)
            {
                var rends = modelInstance.GetComponentsInChildren<Renderer>(true);
                foreach (var r in rends)
                {
                    if (r == restrictionIconRenderer || r.gameObject.name.StartsWith("Outline_")) continue;
                    float modelTop = r.bounds.max.y - transform.position.y;
                    if (modelTop > 0.1f && modelTop < 2.5f)
                    {
                        modelHeight = Mathf.Max(modelHeight, modelTop);
                    }
                }
            }
            return modelHeight;
        }

        private void SetupIceShell()
        {
            if (!IsFrozen)
            {
                if (iceShellObj != null) iceShellObj.SetActive(false);
                return;
            }

            if (iceShellObj == null)
            {
                iceShellObj = new GameObject("IceShell");
                Transform parentT = visualRoot != null ? visualRoot.transform : transform;
                iceShellObj.transform.SetParent(parentT, false);
                iceShellObj.transform.localPosition = Vector3.zero;
                iceShellObj.transform.localRotation = Quaternion.identity;
                iceShellObj.transform.localScale = Vector3.one;

                iceShellMeshFilter = iceShellObj.AddComponent<MeshFilter>();
                iceShellMeshRenderer = iceShellObj.AddComponent<MeshRenderer>();
            }
            else if (visualRoot != null)
            {
                iceShellObj.transform.SetParent(visualRoot.transform, false);
            }

            iceShellObj.SetActive(true);

            float modelHeight = GetModelHeight();
            Mesh iceMesh = GenerateIceShellMesh(currentOffsets, cellSize, visualCenter, modelHeight);
            iceShellMeshFilter.sharedMesh = iceMesh;
            iceShellMeshRenderer.sharedMaterial = GetOrCreateIceMaterial();
            iceShellMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            iceShellMeshRenderer.receiveShadows = false;

            if (iceMpb == null) iceMpb = new MaterialPropertyBlock();
            iceShellMeshRenderer.GetPropertyBlock(iceMpb);
            iceMpb.SetFloat("_CrackIntensity", GetTargetCrackIntensity());
            iceShellMeshRenderer.SetPropertyBlock(iceMpb);
        }

        private float GetTargetCrackIntensity()
        {
            if (!IsFrozen) return 0f;
            int maxCount = PieceData != null ? Mathf.Max(PieceData.freezeCount, 1) : 1;
            if (maxCount <= 1 || CurrentFreezeCount <= 1) return 0.95f;
            float progress = 1f - (float)(CurrentFreezeCount - 1) / (maxCount - 1);
            return Mathf.Lerp(0.18f, 0.95f, progress);
        }

        private void TriggerCrackPulse(float targetIntensity)
        {
            if (crackPulseCoroutine != null) StopCoroutine(crackPulseCoroutine);
            crackPulseCoroutine = StartCoroutine(CrackPulseRoutine(targetIntensity));
        }

        private IEnumerator CrackPulseRoutine(float targetIntensity)
        {
            if (iceShellMeshRenderer == null) yield break;
            if (iceMpb == null) iceMpb = new MaterialPropertyBlock();

            float flashIntensity = Mathf.Min(1.0f, targetIntensity + 0.22f);
            float duration = 0.22f;
            float elapsed = 0f;

            while (elapsed < duration && iceShellMeshRenderer != null)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float cur = Mathf.Lerp(flashIntensity, targetIntensity, t * t);
                iceShellMeshRenderer.GetPropertyBlock(iceMpb);
                iceMpb.SetFloat("_CrackIntensity", cur);
                iceShellMeshRenderer.SetPropertyBlock(iceMpb);
                yield return null;
            }

            if (iceShellMeshRenderer != null)
            {
                iceShellMeshRenderer.GetPropertyBlock(iceMpb);
                iceMpb.SetFloat("_CrackIntensity", targetIntensity);
                iceShellMeshRenderer.SetPropertyBlock(iceMpb);
            }

            crackPulseCoroutine = null;
        }

        private static Material GetOrCreateIceMaterial()
        {
            if (sharedIceMaterial != null) return sharedIceMaterial;

            // 1. Пріоритет: завантаження серіалізованого асету з Resources (завжди включений у збірку на телефоні)
            Material loadedMat = Resources.Load<Material>("Materials/IceBlockMaterial")
                              ?? Resources.Load<Material>("IceBlockMaterial");
            if (loadedMat != null)
            {
                sharedIceMaterial = new Material(loadedMat);
                sharedIceMaterial.name = "M_IceBlockEncasement";
                return sharedIceMaterial;
            }

            // 2. Спроба знайти шейдер безпосередньо
            Shader s = Shader.Find("Custom/IceBlock");
            if (s != null)
            {
                sharedIceMaterial = new Material(s);
                sharedIceMaterial.name = "M_IceBlockEncasement";
                sharedIceMaterial.SetColor("_IceColor", new Color(0.55f, 0.88f, 1.0f, 0.18f));
                sharedIceMaterial.SetColor("_BaseColor", new Color(0.55f, 0.88f, 1.0f, 0.18f));
                sharedIceMaterial.SetColor("_Color", new Color(0.55f, 0.88f, 1.0f, 0.18f));
                sharedIceMaterial.SetColor("_RimColor", new Color(0.92f, 0.98f, 1.0f, 0.85f));
                sharedIceMaterial.SetFloat("_RimPower", 3.5f);
                sharedIceMaterial.SetFloat("_FrostGlow", 0.25f);
                sharedIceMaterial.SetFloat("_CrackIntensity", 0.0f);
                return sharedIceMaterial;
            }

            // 3. Надійний fallback (URP Unlit або Lit з налаштованою прозорістю, без білого кольору!)
            Shader fallbackShader = Shader.Find("Universal Render Pipeline/Unlit")
                                 ?? Shader.Find("Universal Render Pipeline/Lit")
                                 ?? Shader.Find("Sprites/Default")
                                 ?? Shader.Find("Standard");

            if (fallbackShader != null)
            {
                sharedIceMaterial = new Material(fallbackShader);
                sharedIceMaterial.name = "M_IceBlockFallback";
                Color iceTint = new Color(0.55f, 0.88f, 1.0f, 0.22f);
                sharedIceMaterial.SetColor("_BaseColor", iceTint);
                sharedIceMaterial.SetColor("_Color", iceTint);
                sharedIceMaterial.SetFloat("_Surface", 1f);
                sharedIceMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                sharedIceMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                sharedIceMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                sharedIceMaterial.SetInt("_ZWrite", 0);
                sharedIceMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 60;
                return sharedIceMaterial;
            }

            return null;
        }

        private static Mesh GenerateIceShellMesh(List<Vector2Int> offsets, float cellSize, Vector3 visualCenter, float modelHeight)
        {
            Mesh mesh = new Mesh();
            mesh.name = "IceShellMesh";

            if (offsets == null || offsets.Count == 0) return mesh;

            HashSet<Vector2Int> cellSet = new HashSet<Vector2Int>(offsets);
            float half = cellSize * 0.5f;
            float margin = 0.055f * cellSize;
            float iceBottomY = 0.005f;
            float iceTopY = modelHeight + 0.045f * cellSize;

            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();

            void AddQuad(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, Vector3 norm)
            {
                int baseIdx = vertices.Count;
                vertices.Add(v0);
                vertices.Add(v1);
                vertices.Add(v2);
                vertices.Add(v3);

                normals.Add(norm);
                normals.Add(norm);
                normals.Add(norm);
                normals.Add(norm);

                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(0f, 1f));
                uvs.Add(new Vector2(1f, 1f));
                uvs.Add(new Vector2(1f, 0f));

                triangles.Add(baseIdx + 0);
                triangles.Add(baseIdx + 1);
                triangles.Add(baseIdx + 2);
                triangles.Add(baseIdx + 0);
                triangles.Add(baseIdx + 2);
                triangles.Add(baseIdx + 3);
            }

            foreach (var cell in offsets)
            {
                bool hasLeft = cellSet.Contains(new Vector2Int(cell.x - 1, cell.y));
                bool hasRight = cellSet.Contains(new Vector2Int(cell.x + 1, cell.y));
                bool hasBottom = cellSet.Contains(new Vector2Int(cell.x, cell.y - 1));
                bool hasTop = cellSet.Contains(new Vector2Int(cell.x, cell.y + 1));

                float cx = cell.x * cellSize - visualCenter.x;
                float cz = cell.y * cellSize - visualCenter.z;

                float x0 = cx - half - (hasLeft ? 0f : margin);
                float x1 = cx + half + (hasRight ? 0f : margin);
                float z0 = cz - half - (hasBottom ? 0f : margin);
                float z1 = cz + half + (hasTop ? 0f : margin);

                // Top Face (+Y)
                AddQuad(
                    new Vector3(x0, iceTopY, z0),
                    new Vector3(x0, iceTopY, z1),
                    new Vector3(x1, iceTopY, z1),
                    new Vector3(x1, iceTopY, z0),
                    Vector3.up
                );

                // Bottom Face (-Y)
                AddQuad(
                    new Vector3(x0, iceBottomY, z0),
                    new Vector3(x1, iceBottomY, z0),
                    new Vector3(x1, iceBottomY, z1),
                    new Vector3(x0, iceBottomY, z1),
                    Vector3.down
                );

                // Left Wall (-X)
                if (!hasLeft)
                {
                    float leftZ0 = z0;
                    float leftZ1 = z1;
                    if (hasBottom && cellSet.Contains(new Vector2Int(cell.x - 1, cell.y - 1))) leftZ0 += margin;
                    if (hasTop && cellSet.Contains(new Vector2Int(cell.x - 1, cell.y + 1))) leftZ1 -= margin;

                    AddQuad(
                        new Vector3(x0, iceBottomY, leftZ1),
                        new Vector3(x0, iceTopY, leftZ1),
                        new Vector3(x0, iceTopY, leftZ0),
                        new Vector3(x0, iceBottomY, leftZ0),
                        Vector3.left
                    );
                }

                // Right Wall (+X)
                if (!hasRight)
                {
                    float rightZ0 = z0;
                    float rightZ1 = z1;
                    if (hasBottom && cellSet.Contains(new Vector2Int(cell.x + 1, cell.y - 1))) rightZ0 += margin;
                    if (hasTop && cellSet.Contains(new Vector2Int(cell.x + 1, cell.y + 1))) rightZ1 -= margin;

                    AddQuad(
                        new Vector3(x1, iceBottomY, rightZ0),
                        new Vector3(x1, iceTopY, rightZ0),
                        new Vector3(x1, iceTopY, rightZ1),
                        new Vector3(x1, iceBottomY, rightZ1),
                        Vector3.right
                    );
                }

                // Front Wall (-Z)
                if (!hasBottom)
                {
                    float frontX0 = x0;
                    float frontX1 = x1;
                    if (hasLeft && cellSet.Contains(new Vector2Int(cell.x - 1, cell.y - 1))) frontX0 += margin;
                    if (hasRight && cellSet.Contains(new Vector2Int(cell.x + 1, cell.y - 1))) frontX1 -= margin;

                    AddQuad(
                        new Vector3(frontX0, iceBottomY, z0),
                        new Vector3(frontX0, iceTopY, z0),
                        new Vector3(frontX1, iceTopY, z0),
                        new Vector3(frontX1, iceBottomY, z0),
                        Vector3.back
                    );
                }

                // Back Wall (+Z)
                if (!hasTop)
                {
                    float backX0 = x0;
                    float backX1 = x1;
                    if (hasLeft && cellSet.Contains(new Vector2Int(cell.x - 1, cell.y + 1))) backX0 += margin;
                    if (hasRight && cellSet.Contains(new Vector2Int(cell.x + 1, cell.y + 1))) backX1 -= margin;

                    AddQuad(
                        new Vector3(backX1, iceBottomY, z1),
                        new Vector3(backX1, iceTopY, z1),
                        new Vector3(backX0, iceTopY, z1),
                        new Vector3(backX0, iceBottomY, z1),
                        Vector3.forward
                    );
                }
            }

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            return mesh;
        }

        private void SetupFreezeBadge()
        {
            if (!IsFrozen)
            {
                if (freezeBadgeObj != null) freezeBadgeObj.SetActive(false);
                return;
            }

            if (freezeBadgeObj == null)
            {
                freezeBadgeObj = new GameObject("FrozenBadge");
                Transform parentT = visualRoot != null ? visualRoot.transform : transform;
                freezeBadgeObj.transform.SetParent(parentT, false);
            }
            else if (visualRoot != null)
            {
                freezeBadgeObj.transform.SetParent(visualRoot.transform, false);
            }

            freezeBadgeObj.SetActive(true);

            if (freezeBadgeBgRenderer == null)
            {
                freezeBadgeBgRenderer = freezeBadgeObj.GetComponent<SpriteRenderer>();
                if (freezeBadgeBgRenderer == null)
                {
                    freezeBadgeBgRenderer = freezeBadgeObj.AddComponent<SpriteRenderer>();
                }
            }

            freezeBadgeBgRenderer.sprite = GetOrCreateProceduralIceBadgeSprite();
            freezeBadgeBgRenderer.color = Color.white;
            freezeBadgeBgRenderer.sortingOrder = 30;

            if (freezeBadgeBgRenderer.sharedMaterial == null)
            {
                Shader s = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (s != null) freezeBadgeBgRenderer.sharedMaterial = new Material(s);
            }

            float badgeSize = cellSize * FreezeBadgeScale;
            freezeBadgeObj.transform.localScale = new Vector3(badgeSize, badgeSize, 1f);

            float pitch = levelLoader != null ? levelLoader.CameraPitchAngle : 70f;
            freezeBadgeObj.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            float topY = GetModelHeight();
            float iceTopY = topY + 0.045f * cellSize;

            float avgX = 0f, avgY = 0f;
            if (currentOffsets != null && currentOffsets.Count > 0)
            {
                foreach (var o in currentOffsets) { avgX += o.x; avgY += o.y; }
                avgX /= currentOffsets.Count;
                avgY /= currentOffsets.Count;
            }

            Vector3 localPos = visualRoot != null
                ? new Vector3(avgX * cellSize - visualCenter.x, iceTopY + 0.035f * cellSize, avgY * cellSize - visualCenter.z)
                : new Vector3(avgX * cellSize, iceTopY + 0.035f * cellSize, avgY * cellSize);

            freezeBadgeObj.transform.localPosition = localPos;

            Transform textTr = freezeBadgeObj.transform.Find("BadgeText");
            GameObject textObj = textTr != null ? textTr.gameObject : new GameObject("BadgeText");
            textObj.transform.SetParent(freezeBadgeObj.transform, false);
            textObj.transform.localPosition = new Vector3(0f, 0f, -0.015f);
            textObj.transform.localRotation = Quaternion.identity;

            freezeBadgeText = textObj.GetComponent<TextMeshPro>();
            if (freezeBadgeText == null)
            {
                freezeBadgeText = textObj.AddComponent<TextMeshPro>();
            }

            TMP_FontAsset font = BoosterBadgeUI.GetDefaultTMPFont();
            if (font != null) freezeBadgeText.font = font;

            freezeBadgeText.text = CurrentFreezeCount.ToString();
            freezeBadgeText.fontSize = 4.0f * cellSize;
            freezeBadgeText.alignment = TextAlignmentOptions.Center;
            freezeBadgeText.fontStyle = FontStyles.Bold;
            freezeBadgeText.color = Color.white;
            freezeBadgeText.sortingOrder = 35;
        }

        private static Sprite cachedIceBadgeSprite;

        public static Sprite GetOrCreateProceduralIceBadgeSprite()
        {
            if (cachedIceBadgeSprite != null && cachedIceBadgeSprite.texture != null)
            {
                return cachedIceBadgeSprite;
            }

            const int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = "Procedural_IceBadgeSprite";
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[size * size];
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = (size - 1) * 0.46f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x - center.x) / radius;
                    float ny = (y - center.y) / radius;

                    float ax = Mathf.Abs(nx);
                    float ay = Mathf.Abs(ny);

                    // Hexagonal distance (vertical hexagon)
                    float hexDist = Mathf.Max(ax * 0.866025f + ay * 0.5f, ay);

                    // Corner rounding via superellipse blend
                    float cornerDist = Mathf.Pow(ax, 3f) + Mathf.Pow(ay, 3f);
                    float dist = Mathf.Lerp(hexDist, Mathf.Pow(cornerDist, 1f / 3f), 0.35f);

                    if (dist > 1.05f)
                    {
                        pixels[y * size + x] = Color.clear;
                    }
                    else
                    {
                        float aa = Mathf.Clamp01((1.05f - dist) / 0.08f);

                        Color col;
                        if (dist > 0.80f)
                        {
                            // Outer icy glowing rim: frosty cyan to brilliant white
                            float rimT = (dist - 0.80f) / 0.25f;
                            col = Color.Lerp(new Color(0.35f, 0.90f, 1f, 0.95f), Color.white, rimT * 0.85f);
                        }
                        else if (dist > 0.68f)
                        {
                            // Inner rim highlight: pure frost
                            col = new Color(0.90f, 0.98f, 1f, 0.92f);
                        }
                        else
                        {
                            // Translucent frosted glass core: allow Lego brick and studs below to be seen
                            float grad = Mathf.Clamp01((ny + 0.8f) * 0.6f);
                            Color deepIce = new Color(0.10f, 0.45f, 0.80f, 0.52f);
                            Color lightIce = new Color(0.30f, 0.72f, 0.98f, 0.62f);
                            col = Color.Lerp(deepIce, lightIce, grad);

                            // Subtle top crystal glint
                            if (ny > 0.25f && ax < 0.40f)
                            {
                                col = Color.Lerp(col, new Color(1f, 1f, 1f, 0.80f), (ny - 0.25f) * 0.55f);
                            }
                        }

                        col.a *= aa;
                        pixels[y * size + x] = col;
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            cachedIceBadgeSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return cachedIceBadgeSprite;
        }

        /// <summary>
        /// Зменшує лічильник заморозки блоку при знищенні іншого блоку.
        /// Коли лічильник досягає 0, блок розморожується і стає повноцінно ігровим.
        /// </summary>
        public void DecrementFreeze()
        {
            if (!IsFrozen || isExiting) return;

            CurrentFreezeCount--;

            if (CurrentFreezeCount > 0)
            {
                if (freezeBadgeText != null)
                {
                    freezeBadgeText.text = CurrentFreezeCount.ToString();
                }

                if (badgePulseCoroutine != null) StopCoroutine(badgePulseCoroutine);
                badgePulseCoroutine = StartCoroutine(BadgePulseRoutine());

                SpawnIceChipDebris();

                float targetCrack = GetTargetCrackIntensity();
                TriggerCrackPulse(targetCrack);

                if (levelLoader != null)
                {
                    levelLoader.PlayBlockCrackSound();
                }
                if (GameSettingsManager.HasInstance)
                {
                    GameSettingsManager.Instance.TriggerHaptic();
                }
            }
            else
            {
                CurrentFreezeCount = 0;
                StartCoroutine(UnfreezeRoutine());
            }
        }

        private void SpawnIceChipDebris()
        {
            if (visualRoot == null) return;
            Vector3 center = visualRoot.transform.position + Vector3.up * 0.25f;
            int chipCount = UnityEngine.Random.Range(3, 5);

            for (int i = 0; i < chipCount; i++)
            {
                GameObject chip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                chip.name = "IceChip";
                chip.transform.position = center + UnityEngine.Random.insideUnitSphere * 0.2f * cellSize;
                float s = UnityEngine.Random.Range(0.06f, 0.12f) * cellSize;
                chip.transform.localScale = new Vector3(s, s * 0.7f, s * 1.3f);

                var r = chip.GetComponent<Renderer>();
                if (r != null)
                {
                    r.sharedMaterial = GetOrCreateIceMaterial();
                }

                var col = chip.GetComponent<Collider>();
                if (col != null) Destroy(col);

                Vector3 blastDir = (UnityEngine.Random.insideUnitSphere + Vector3.up * 1.4f).normalized;
                Vector3 vel = blastDir * UnityEngine.Random.Range(2.0f, 4.0f);
                Vector3 rot = UnityEngine.Random.insideUnitSphere * 350f;

                chip.AddComponent<PieceDebrisMotion>().Launch(chip, vel, rot, levelLoader != null ? levelLoader.BoardBaseY : 0f);
                Destroy(chip, 0.8f);
            }
        }

        private IEnumerator BadgePulseRoutine()
        {
            if (freezeBadgeObj == null) yield break;

            Transform badgeT = freezeBadgeObj.transform;
            float badgeSize = cellSize * FreezeBadgeScale;
            Vector3 baseScale = new Vector3(badgeSize, badgeSize, 1f);
            float duration = 0.20f;
            float elapsed = 0f;

            while (elapsed < duration && freezeBadgeObj != null)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float scaleMul = 1f + Mathf.Sin(t * Mathf.PI) * 0.38f;
                badgeT.localScale = baseScale * scaleMul;
                yield return null;
            }

            if (freezeBadgeObj != null)
            {
                badgeT.localScale = baseScale;
            }
            badgePulseCoroutine = null;
        }

        public void PlayFrozenShake()
        {
            if (frozenShakeCoroutine != null) StopCoroutine(frozenShakeCoroutine);
            frozenShakeCoroutine = StartCoroutine(FrozenShakeRoutine());

            if (levelLoader != null)
            {
                levelLoader.PlayBlockCrackSound();
            }
            if (GameSettingsManager.HasInstance)
            {
                GameSettingsManager.Instance.TriggerHaptic();
            }
        }

        private IEnumerator FrozenShakeRoutine()
        {
            Transform t = visualRoot != null ? visualRoot.transform : transform;
            Vector3 originalPos = visualCenter;
            float duration = 0.16f;
            float elapsed = 0f;
            float magnitude = 0.045f * cellSize;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / duration;
                float offset = Mathf.Sin(progress * Mathf.PI * 6f) * (1f - progress) * magnitude;
                t.localPosition = originalPos + new Vector3(offset, 0f, 0f);
                yield return null;
            }

            t.localPosition = originalPos;
            frozenShakeCoroutine = null;
        }

        private IEnumerator UnfreezeRoutine()
        {
            // 0. Destroy ice shell encasement
            if (iceShellObj != null)
            {
                Destroy(iceShellObj);
                iceShellObj = null;
            }

            // 1. Ice shatter debris & sounds
            SpawnIceShatterDebris();

            if (levelLoader != null)
            {
                levelLoader.PlayBlockUnfreezeSound();
            }
            if (GameSettingsManager.HasInstance)
            {
                GameSettingsManager.Instance.TriggerHapticExit();
            }

            // 2. Shrink and destroy freeze badge
            if (freezeBadgeObj != null)
            {
                float fadeDuration = 0.18f;
                float elapsed = 0f;
                Vector3 origScale = freezeBadgeObj.transform.localScale;
                while (elapsed < fadeDuration && freezeBadgeObj != null)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / fadeDuration;
                    freezeBadgeObj.transform.localScale = Vector3.Lerp(origScale, Vector3.zero, t * t);
                    yield return null;
                }
                if (freezeBadgeObj != null)
                {
                    Destroy(freezeBadgeObj);
                    freezeBadgeObj = null;
                }
            }

            // 3. Restore normal piece color
            RefreshColor();

            // 4. Restore restriction icon if needed
            if (PieceData.moveRestriction == MoveRestriction.HorizontalOnly || PieceData.moveRestriction == MoveRestriction.VerticalOnly)
            {
                SetupRestrictionIcon(cachedPalette);
                if (restrictionIconRenderer != null)
                {
                    restrictionIconRenderer.gameObject.SetActive(true);
                }
            }

            // 5. Check if newly unfrozen block can auto-exit
            if (levelLoader != null)
            {
                levelLoader.CheckAndTriggerAutoExits();
            }
        }

        private void SpawnIceShatterDebris()
        {
            GameObject debrisRoot = new GameObject("IceShatterDebris");
            debrisRoot.transform.position = transform.position;

            int shardCount = 14;
            Vector3 center = visualRoot != null ? visualRoot.transform.position : transform.position;
            Material iceMat = GetOrCreateIceMaterial();

            for (int i = 0; i < shardCount; i++)
            {
                GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shard.transform.SetParent(debrisRoot.transform, false);
                shard.transform.position = center + UnityEngine.Random.insideUnitSphere * 0.28f * cellSize;

                float size = UnityEngine.Random.Range(0.10f, 0.22f) * cellSize;
                shard.transform.localScale = new Vector3(size, size * 0.85f, size * 1.25f);

                var r = shard.GetComponent<Renderer>();
                if (r != null)
                {
                    r.sharedMaterial = iceMat;
                }

                var col = shard.GetComponent<Collider>();
                if (col != null) Destroy(col);

                Vector3 blastDir = (UnityEngine.Random.insideUnitSphere + Vector3.up * 1.6f).normalized;
                Vector3 initialVel = blastDir * UnityEngine.Random.Range(3.8f, 7.0f);
                Vector3 rotSpeed = UnityEngine.Random.insideUnitSphere * 450f;

                debrisRoot.AddComponent<PieceDebrisMotion>().Launch(shard, initialVel, rotSpeed, levelLoader != null ? levelLoader.BoardBaseY : 0f);
            }

            Destroy(debrisRoot, 1.4f);
        }

        private class PieceDebrisMotion : MonoBehaviour
        {
            public void Launch(GameObject target, Vector3 velocity, Vector3 rotSpeed, float floorY)
            {
                StartCoroutine(MotionRoutine(target, velocity, rotSpeed, floorY));
            }

            private IEnumerator MotionRoutine(GameObject target, Vector3 velocity, Vector3 rotSpeed, float floorY)
            {
                float lifetime = 1.2f;
                float elapsed = 0f;
                Vector3 pos = target != null ? target.transform.position : Vector3.zero;

                while (elapsed < lifetime && target != null)
                {
                    elapsed += Time.deltaTime;
                    velocity.y -= 16f * Time.deltaTime;
                    pos += velocity * Time.deltaTime;

                    if (pos.y < floorY + 0.05f)
                    {
                        pos.y = floorY + 0.05f;
                        velocity.y = -velocity.y * 0.35f;
                        velocity.x *= 0.6f;
                        velocity.z *= 0.6f;
                    }

                    target.transform.position = pos;
                    target.transform.Rotate(rotSpeed * Time.deltaTime);

                    if (elapsed > 0.8f)
                    {
                        float shrink = 1f - ((elapsed - 0.8f) / 0.4f);
                        target.transform.localScale = target.transform.localScale * Mathf.Clamp01(shrink);
                    }

                    yield return null;
                }
            }
        }
    }
}
