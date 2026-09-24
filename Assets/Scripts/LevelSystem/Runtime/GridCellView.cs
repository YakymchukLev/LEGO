using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LegoPuzzle.Data;

namespace LegoPuzzle.Runtime
{
    public class GridCellView : MonoBehaviour
    {
        public Vector2Int GridPosition { get; private set; }
        public CellType CellType { get; private set; }
        public ExitDirection ExitDirection { get; private set; }
        public BlockColorType GateColorType { get; private set; }
        public Color GateColor { get; private set; }

        [Header("Компоненти відображення")]
        [SerializeField] private MeshRenderer mainRenderer;
        [SerializeField] private SpriteRenderer arrowRenderer;
        [SerializeField] private Transform arrowTransform;

        public void Initialize(CellData data, BlockPalette palette, float cellSize = 1f)
        {
            GridPosition = data.position;
            CellType = data.cellType;
            ExitDirection = data.exitDirection;
            GateColorType = data.gateColorType;
            GateColor = data.GetEffectiveColor();

            if (mainRenderer == null)
            {
                mainRenderer = GetComponent<MeshRenderer>();
                if (mainRenderer == null) mainRenderer = GetComponentInChildren<MeshRenderer>(true);
            }

            if (arrowRenderer == null)
            {
                arrowRenderer = GetComponentInChildren<SpriteRenderer>(true);
            }
            if (arrowTransform == null && arrowRenderer != null)
            {
                arrowTransform = arrowRenderer.transform;
            }

            transform.localPosition = new Vector3(data.position.x * cellSize, -0.01f, data.position.y * cellSize);

            // Автоматичне вирівнювання орієнтації та масштабу залежно від типу мешу (Quad, Plane, Cube)
            AlignAndScaleCell(data, cellSize);

            // Застосовуємо матеріал з текстурою
            if (mainRenderer != null && palette != null)
            {
                switch (CellType)
                {
                    case CellType.Walkable:
                        if (palette.walkableTileMaterial != null)
                        {
                            mainRenderer.sharedMaterial = palette.walkableTileMaterial;
                        }
                        break;

                    case CellType.Obstacle:
                    case CellType.HalfObstacle:
                    case CellType.QuarterObstacle:
                        if (palette.obstacleTileMaterial != null)
                        {
                            mainRenderer.sharedMaterial = palette.obstacleTileMaterial;
                        }
                        break;

                    case CellType.ExitGate:
                    case CellType.HalfExitGate:
                        if (palette.exitGateMaterial != null)
                        {
                            mainRenderer.sharedMaterial = palette.exitGateMaterial;
                        }
                        else if (palette.walkableTileMaterial != null)
                        {
                            mainRenderer.sharedMaterial = palette.walkableTileMaterial;
                        }
                        break;
                }
            }

            switch (CellType)
            {
                case CellType.Empty:
                    gameObject.SetActive(false);
                    break;

                case CellType.Walkable:
                    gameObject.SetActive(true);
                    if (arrowRenderer != null) arrowRenderer.gameObject.SetActive(false);
                    break;

                case CellType.Obstacle:
                case CellType.HalfObstacle:
                case CellType.QuarterObstacle:
                    gameObject.SetActive(true);
                    if (arrowRenderer != null) arrowRenderer.gameObject.SetActive(false);
                    break;

                case CellType.ExitGate:
                case CellType.HalfExitGate:
                    gameObject.SetActive(true);
                    SetupExitGate(data, palette, cellSize);
                    break;
            }

            baseGateLocalScale = transform.localScale;
            cachedCellSize = cellSize;
        }

        /// <summary>
        /// Refreshes the gate's visual color (useful when toggling Colorblind Mode dynamically).
        /// </summary>
        public void RefreshGateColor()
        {
            if (CellType != CellType.ExitGate && CellType != CellType.HalfExitGate) return;
            GateColor = LegoPuzzle.Data.ColorblindPalette.GetGateColor(GateColorType);
            if (mainRenderer != null)
            {
                MaterialPropertyBlock mpb = new MaterialPropertyBlock();
                mainRenderer.GetPropertyBlock(mpb);
                mpb.SetColor("_Color", GateColor);
                mpb.SetColor("_BaseColor", GateColor);
                mainRenderer.SetPropertyBlock(mpb);
            }
        }

        private void AlignAndScaleCell(CellData data, float cellSize)
        {
            MeshFilter mf = GetComponent<MeshFilter>();
            if (mf == null) mf = GetComponentInChildren<MeshFilter>(true);

            bool is2DQuad = false;
            Bounds b = default;
            float rawSizeX = 1f;
            float rawSizeY = 1f;
            float rawSizeZ = 1f;

            if (mf != null && mf.sharedMesh != null)
            {
                b = mf.sharedMesh.bounds;
                rawSizeX = Mathf.Max(b.size.x, 0.001f);
                rawSizeY = Mathf.Max(b.size.y, 0.001f);
                rawSizeZ = Mathf.Max(b.size.z, 0.001f);
                // 2D Quad/Sprite у площині XY
                is2DQuad = (b.size.z < 0.01f && b.size.y > 0.01f);
            }

            if (CellType == CellType.QuarterObstacle)
            {
                float quarterSize = cellSize * 0.5f;
                Vector3 offset = Vector3.zero;

                switch (ExitDirection)
                {
                    case ExitDirection.Left:  // Top-Left (Вгору-Вліво)
                        offset = new Vector3(-cellSize * 0.25f, 0f, cellSize * 0.25f);
                        break;
                    case ExitDirection.Up:    // Top-Right (Вгору-Вправо)
                        offset = new Vector3(cellSize * 0.25f, 0f, cellSize * 0.25f);
                        break;
                    case ExitDirection.Right: // Bottom-Right (Вниз-Вправо)
                        offset = new Vector3(cellSize * 0.25f, 0f, -cellSize * 0.25f);
                        break;
                    case ExitDirection.Down:  // Bottom-Left (Вниз-Вліво)
                        offset = new Vector3(-cellSize * 0.25f, 0f, -cellSize * 0.25f);
                        break;
                }

                transform.localPosition = new Vector3(GridPosition.x * cellSize + offset.x, -0.01f, GridPosition.y * cellSize + offset.z);

                if (is2DQuad)
                {
                    transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    transform.localScale = new Vector3(quarterSize / rawSizeX, quarterSize / rawSizeY, 1f);
                }
                else
                {
                    transform.localRotation = Quaternion.identity;
                    float scaleY = (b.size.y > 0.05f) ? (cellSize / Mathf.Max(rawSizeX, rawSizeZ)) : 1f;
                    transform.localScale = new Vector3(quarterSize / rawSizeX, scaleY, quarterSize / rawSizeZ);
                }
                return;
            }

            if (CellType == CellType.HalfExitGate)
            {
                float halfDim = cellSize * 0.5f;
                float fullDim = cellSize;
                Vector3 offset = Vector3.zero;
                float scaleTargetX = fullDim;
                float scaleTargetZ = fullDim;

                switch (data.gateAlignment)
                {
                    case 0: // Horizontal Top (Верхня половина ▀)
                        scaleTargetX = fullDim;
                        scaleTargetZ = halfDim;
                        offset = new Vector3(0f, 0f, cellSize * 0.25f);
                        break;

                    case 1: // Horizontal Bottom (Нижня половина ▄)
                        scaleTargetX = fullDim;
                        scaleTargetZ = halfDim;
                        offset = new Vector3(0f, 0f, -cellSize * 0.25f);
                        break;

                    case 2: // Vertical Left (Ліва половина ▌)
                        scaleTargetX = halfDim;
                        scaleTargetZ = fullDim;
                        offset = new Vector3(-cellSize * 0.25f, 0f, 0f);
                        break;

                    case 3: // Vertical Right (Права половина ▐)
                        scaleTargetX = halfDim;
                        scaleTargetZ = fullDim;
                        offset = new Vector3(cellSize * 0.25f, 0f, 0f);
                        break;

                    default: // Fallback за напрямком виходу
                        if (ExitDirection == ExitDirection.Up || ExitDirection == ExitDirection.Down)
                        {
                            scaleTargetX = fullDim;
                            scaleTargetZ = halfDim;
                            offset = (ExitDirection == ExitDirection.Up) 
                                ? new Vector3(0f, 0f, cellSize * 0.25f) 
                                : new Vector3(0f, 0f, -cellSize * 0.25f);
                        }
                        else
                        {
                            scaleTargetX = halfDim;
                            scaleTargetZ = fullDim;
                            offset = (ExitDirection == ExitDirection.Right) 
                                ? new Vector3(cellSize * 0.25f, 0f, 0f) 
                                : new Vector3(-cellSize * 0.25f, 0f, 0f);
                        }
                        break;
                }

                transform.localPosition = new Vector3(GridPosition.x * cellSize + offset.x, -0.01f, GridPosition.y * cellSize + offset.z);

                if (is2DQuad)
                {
                    transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    transform.localScale = new Vector3(scaleTargetX / rawSizeX, scaleTargetZ / rawSizeY, 1f);
                }
                else
                {
                    transform.localRotation = Quaternion.identity;
                    float scaleY = (b.size.y > 0.05f) ? (cellSize / Mathf.Max(rawSizeX, rawSizeZ)) : 1f;
                    transform.localScale = new Vector3(scaleTargetX / rawSizeX, scaleY, scaleTargetZ / rawSizeZ);
                }
                return;
            }

            if (CellType == CellType.HalfObstacle)
            {
                float halfWidth = cellSize * 0.5f;
                Vector3 offset = Vector3.zero;
                float rotY = 0f;

                switch (ExitDirection)
                {
                    case ExitDirection.Up:
                        offset = new Vector3(0f, 0f, cellSize * 0.25f);
                        rotY = 0f;
                        break;
                    case ExitDirection.Right:
                        offset = new Vector3(cellSize * 0.25f, 0f, 0f);
                        rotY = 90f;
                        break;
                    case ExitDirection.Down:
                        offset = new Vector3(0f, 0f, -cellSize * 0.25f);
                        rotY = 180f;
                        break;
                    case ExitDirection.Left:
                        offset = new Vector3(-cellSize * 0.25f, 0f, 0f);
                        rotY = 270f;
                        break;
                }

                transform.localPosition = new Vector3(GridPosition.x * cellSize + offset.x, -0.01f, GridPosition.y * cellSize + offset.z);

                if (is2DQuad)
                {
                    transform.localRotation = Quaternion.Euler(90f, rotY, 0f);
                    transform.localScale = new Vector3(cellSize / rawSizeX, halfWidth / rawSizeY, 1f);
                }
                else
                {
                    transform.localRotation = Quaternion.Euler(0f, rotY, 0f);
                    float scaleY = (b.size.y > 0.05f) ? (cellSize / Mathf.Max(rawSizeX, rawSizeZ)) : 1f;
                    transform.localScale = new Vector3(cellSize / rawSizeX, scaleY, halfWidth / rawSizeZ);
                }
                return;
            }

            if (mf != null && mf.sharedMesh != null)
            {
                if (is2DQuad)
                {
                    transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    float scaleX = cellSize / rawSizeX;
                    float scaleY = cellSize / rawSizeY;
                    transform.localScale = new Vector3(scaleX, scaleY, 1f);
                }
                else
                {
                    transform.localRotation = Quaternion.identity;
                    float scaleX = cellSize / rawSizeX;
                    float scaleZ = cellSize / rawSizeZ;
                    float scaleY = (b.size.y > 0.05f) ? (cellSize / Mathf.Max(rawSizeX, rawSizeZ)) : 1f;

                    transform.localScale = new Vector3(scaleX, scaleY, scaleZ);
                }
            }
            else
            {
                transform.localRotation = Quaternion.identity;
                transform.localScale = Vector3.one * cellSize;
            }
        }

        private Vector3 baseGateLocalScale = Vector3.one;
        private Vector3 baseArrowLocalScale = Vector3.one;
        private Vector3 baseArrowLocalPos = Vector3.zero;
        private float cachedCellSize = 1f;
        private float pulseOffset = 0f;
        private Coroutine reactionCoroutine;
        private static Sprite cachedArrowSprite;
        private static Sprite cachedShockwaveSprite;

        private void Update()
        {
            if (arrowTransform != null && arrowRenderer != null && arrowRenderer.gameObject.activeSelf &&
                (CellType == CellType.ExitGate || CellType == CellType.HalfExitGate))
            {
                float t = (Time.time + pulseOffset) * 2.8f;
                // Комбінація 3: ритмічне дихання масштабу
                float scalePulse = 1f + Mathf.Sin(t) * 0.08f;
                arrowTransform.localScale = baseArrowLocalScale * scalePulse;

                // Комбінація 4: магнітне втягування (імпульс зміщення стрілки у напрямку виходу)
                float pullSin = Mathf.Max(0f, Mathf.Sin(t * 1.3f));
                float pullDist = pullSin * (cachedCellSize * 0.08f);

                Vector3 dirLocalOffset = ExitDirection switch
                {
                    ExitDirection.Up => Vector3.forward * pullDist,
                    ExitDirection.Down => Vector3.back * pullDist,
                    ExitDirection.Left => Vector3.left * pullDist,
                    ExitDirection.Right => Vector3.right * pullDist,
                    _ => Vector3.zero
                };

                arrowTransform.localPosition = baseArrowLocalPos + dirLocalOffset;
            }
        }

        private void SetupExitGate(CellData data, BlockPalette palette, float cellSize)
        {
            cachedCellSize = cellSize;

            if (mainRenderer != null)
            {
                MaterialPropertyBlock mpb = new MaterialPropertyBlock();
                mainRenderer.GetPropertyBlock(mpb);
                mpb.SetColor("_Color", GateColor);
                mpb.SetColor("_BaseColor", GateColor);
                mainRenderer.SetPropertyBlock(mpb);
            }

            // Ensure arrow GameObject and SpriteRenderer exist
            if (arrowRenderer == null)
            {
                Transform existing = transform.Find("GateArrow");
                GameObject arrowObj = (existing != null) ? existing.gameObject : new GameObject("GateArrow");
                if (existing == null)
                {
                    arrowObj.transform.SetParent(transform, false);
                }
                arrowRenderer = arrowObj.GetComponent<SpriteRenderer>();
                if (arrowRenderer == null)
                {
                    arrowRenderer = arrowObj.AddComponent<SpriteRenderer>();
                }
                arrowTransform = arrowObj.transform;
            }

            if (arrowRenderer != null)
            {
                arrowRenderer.gameObject.SetActive(true);

                Sprite spriteToUse = (palette != null && palette.exitGateArrowSprite != null)
                    ? palette.exitGateArrowSprite
                    : GetOrCreateProceduralArrowSprite();

                arrowRenderer.sprite = spriteToUse;
                arrowRenderer.color = Color.white;
                arrowRenderer.sortingOrder = 5;

                // Ensure sprite material in case default is unassigned
                if (arrowRenderer.sharedMaterial == null)
                {
                    Shader s = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                    if (s != null) arrowRenderer.sharedMaterial = new Material(s);
                }
            }

            if (arrowTransform != null)
            {
                // Rotation angle for exit direction:
                // Up -> 0 deg (faces World +Z)
                // Right -> -90 deg (faces World +X)
                // Down -> 180 deg (faces World -Z)
                // Left -> 90 deg (faces World -X)
                float angle = data.exitDirection switch
                {
                    ExitDirection.Up => 0f,
                    ExitDirection.Right => -90f,
                    ExitDirection.Down => 180f,
                    ExitDirection.Left => 90f,
                    _ => 0f
                };
                arrowTransform.localRotation = Quaternion.Euler(90f, 0f, angle);

                // Calculate surface height of the gate mesh
                MeshFilter mf = GetComponent<MeshFilter>();
                if (mf == null) mf = GetComponentInChildren<MeshFilter>(true);
                bool is2DQuad = false;
                float maxY = 0.5f;
                if (mf != null && mf.sharedMesh != null)
                {
                    Bounds b = mf.sharedMesh.bounds;
                    is2DQuad = (b.size.z < 0.01f && b.size.y > 0.01f);
                    maxY = b.max.y;
                }
                float topY = is2DQuad ? 0.02f : (maxY + 0.025f);

                // Exactly in the horizontal center of the gate
                baseArrowLocalPos = new Vector3(0f, topY, 0f);
                arrowTransform.localPosition = baseArrowLocalPos;

                // Adaptive scale: compensations for non-uniform parent scale
                float targetSize = (CellType == CellType.HalfExitGate) ? (cellSize * 0.32f) : (cellSize * 0.52f);
                Vector3 pScale = transform.localScale;
                float absSx = Mathf.Max(Mathf.Abs(pScale.x), 0.001f);
                float absSz = Mathf.Max(Mathf.Abs(pScale.z), 0.001f);

                bool isHorizontal = (data.exitDirection == ExitDirection.Left || data.exitDirection == ExitDirection.Right);
                float scaleX = isHorizontal ? (targetSize / absSz) : (targetSize / absSx);
                float scaleY = isHorizontal ? (targetSize / absSx) : (targetSize / absSz);

                baseArrowLocalScale = new Vector3(scaleX, scaleY, 1f);
                arrowTransform.localScale = baseArrowLocalScale;
                pulseOffset = (GridPosition.x * 0.7f + GridPosition.y * 1.3f);
            }
        }

        /// <summary>
        /// Запускає соковиту комбіновану реакцію воріт при проходженні блоку:
        /// желейне розтягування (Squash & Stretch), неоновий спалах та розширення кільця ударної хвилі (Shockwave).
        /// </summary>
        public void PlayGateExitReaction(ExitDirection dir, Color pieceColor)
        {
            if (!gameObject.activeInHierarchy) return;
            if (reactionCoroutine != null) StopCoroutine(reactionCoroutine);
            reactionCoroutine = StartCoroutine(GateExitReactionRoutine(dir, pieceColor));
        }

        private IEnumerator GateExitReactionRoutine(ExitDirection dir, Color pieceColor)
        {
            // 1. Спавн круглої ударної хвилі (Shockwave ring)
            Color waveColor = (pieceColor != Color.clear && pieceColor.a > 0.01f) ? pieceColor : GateColor;
            SpawnShockwave(waveColor);

            // 2. Неоновий спалах яскравості тайлу воріт
            if (mainRenderer != null)
            {
                MaterialPropertyBlock mpb = new MaterialPropertyBlock();
                Color flashColor = Color.Lerp(GateColor, Color.white, 0.75f);
                mainRenderer.GetPropertyBlock(mpb);
                mpb.SetColor("_Color", flashColor);
                mpb.SetColor("_BaseColor", flashColor);
                mainRenderer.SetPropertyBlock(mpb);
            }

            // 3. Желейний сплеск масштабу з розтягуванням вздовж осі виходу (Squash & Stretch)
            Vector3 baseScale = baseGateLocalScale;
            if (baseScale == Vector3.zero) baseScale = transform.localScale;

            Vector3 targetStretch = baseScale;
            if (dir == ExitDirection.Up || dir == ExitDirection.Down)
            {
                targetStretch.z *= 1.35f;
                targetStretch.x *= 1.15f;
            }
            else
            {
                targetStretch.x *= 1.35f;
                targetStretch.z *= 1.15f;
            }

            float duration = 0.35f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);

                // Пружна крива з легким овершутом
                float tScale = Mathf.Sin(progress * Mathf.PI * 1.3f);
                if (progress > 0.75f)
                {
                    tScale = -Mathf.Sin((progress - 0.75f) / 0.25f * Mathf.PI) * 0.12f;
                }

                transform.localScale = Vector3.LerpUnclamped(baseScale, targetStretch, Mathf.Max(0f, tScale));

                // Плавне повернення кольору після перших 0.08с
                if (mainRenderer != null && progress > 0.2f)
                {
                    float colorProgress = (progress - 0.2f) / 0.8f;
                    Color curCol = Color.Lerp(Color.Lerp(GateColor, Color.white, 0.75f), GateColor, colorProgress);
                    MaterialPropertyBlock mpb = new MaterialPropertyBlock();
                    mainRenderer.GetPropertyBlock(mpb);
                    mpb.SetColor("_Color", curCol);
                    mpb.SetColor("_BaseColor", curCol);
                    mainRenderer.SetPropertyBlock(mpb);
                }

                yield return null;
            }

            transform.localScale = baseScale;
            RefreshGateColor();
            reactionCoroutine = null;
        }

        private void SpawnShockwave(Color color)
        {
            GameObject waveObj = new GameObject("GateShockwave");
            waveObj.transform.position = transform.position + Vector3.up * 0.05f;
            waveObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            SpriteRenderer sr = waveObj.AddComponent<SpriteRenderer>();
            sr.sprite = GetOrCreateShockwaveSprite();
            sr.color = color;
            sr.sortingOrder = 10;

            Shader s = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (s != null) sr.sharedMaterial = new Material(s);

            StartCoroutine(ShockwaveExpandRoutine(waveObj, sr, color));
        }

        private IEnumerator ShockwaveExpandRoutine(GameObject waveObj, SpriteRenderer sr, Color color)
        {
            float duration = 0.28f;
            float elapsed = 0f;
            float cSize = cachedCellSize > 0.05f ? cachedCellSize : 1f;
            Vector3 startScale = Vector3.one * (cSize * 0.35f);
            Vector3 endScale = Vector3.one * (cSize * 2.2f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                if (waveObj != null)
                {
                    waveObj.transform.localScale = Vector3.Lerp(startScale, endScale, Mathf.SmoothStep(0f, 1f, t));
                }

                if (sr != null)
                {
                    float alpha = Mathf.Lerp(0.85f, 0f, t * t);
                    sr.color = new Color(color.r, color.g, color.b, alpha);
                }

                yield return null;
            }

            if (waveObj != null) Destroy(waveObj);
        }

        /// <summary>
        /// Генерує процедурний спрайт кільця ударної хвилі (Shockwave ring).
        /// </summary>
        public static Sprite GetOrCreateShockwaveSprite()
        {
            if (cachedShockwaveSprite != null) return cachedShockwaveSprite;

            const int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = "Procedural_ShockwaveRing";
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[size * size];
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float targetRadius = (size * 0.5f) * 0.76f;
            float halfThickness = 9f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float dRing = Mathf.Abs(dist - targetRadius);
                    float alpha = Mathf.Clamp01(1f - (dRing / halfThickness));
                    alpha = alpha * alpha * (3f - 2f * alpha); // Smoothstep curve
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, true);

            cachedShockwaveSprite = Sprite.Create(
                tex,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                size,
                0,
                SpriteMeshType.FullRect
            );

            return cachedShockwaveSprite;
        }

        /// <summary>
        /// Generates a procedural high-res anti-aliased bold arrow sprite with high-contrast outline.
        /// </summary>
        public static Sprite GetOrCreateProceduralArrowSprite()
        {
            if (cachedArrowSprite != null) return cachedArrowSprite;

            const int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = "Procedural_GateExitArrow";
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[size * size];
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);

            // Generate crisp, anti-aliased, bold arrow pointing UP (+Y)
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Normalized coordinates [-1, 1]
                    float nx = (x - center.x) / (size * 0.5f);
                    float ny = (y - center.y) / (size * 0.5f);

                    Vector2 p = new Vector2(Mathf.Abs(nx), ny);

                    // Head: segment from tip (0, 0.60) to barb (0.54, 0.05) with thickness 0.16
                    float distHead = DistanceToSegment(p, new Vector2(0f, 0.60f), new Vector2(0.54f, 0.05f)) - 0.16f;

                    // Stem: segment from bottom (0, -0.60) to join (0, 0.22) with thickness 0.14
                    float distStem = DistanceToSegment(p, new Vector2(0f, -0.60f), new Vector2(0f, 0.22f)) - 0.14f;

                    float d = Mathf.Min(distHead, distStem);

                    // Anti-aliasing thresholds (sub-pixel transitions)
                    float fillAlpha = Mathf.Clamp01(0.5f - d / 0.045f);
                    float outlineAlpha = Mathf.Clamp01(0.5f - (d - 0.065f) / 0.045f);

                    Color col = Color.clear;
                    if (fillAlpha > 0.005f)
                    {
                        // Clean solid white fill with smooth alpha edge
                        col = new Color(1f, 1f, 1f, fillAlpha);
                    }
                    else if (outlineAlpha > 0.005f)
                    {
                        // Subtle dark shadow outline (ensures visibility on light-colored gates)
                        col = new Color(0.05f, 0.05f, 0.05f, outlineAlpha * 0.65f);
                    }

                    pixels[y * size + x] = col;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, true);

            cachedArrowSprite = Sprite.Create(
                tex,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                size,
                0,
                SpriteMeshType.FullRect
            );

            return cachedArrowSprite;
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 pa = p - a;
            Vector2 ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude;
        }
    }
}
