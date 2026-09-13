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
                    SetupExitGate(data);
                    break;
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

        private void SetupExitGate(CellData data)
        {
            if (mainRenderer != null)
            {
                MaterialPropertyBlock mpb = new MaterialPropertyBlock();
                mainRenderer.GetPropertyBlock(mpb);
                mpb.SetColor("_Color", GateColor);
                mpb.SetColor("_BaseColor", GateColor);
                mainRenderer.SetPropertyBlock(mpb);
            }

            if (arrowRenderer != null)
            {
                arrowRenderer.gameObject.SetActive(true);
                arrowRenderer.color = Color.white;
            }

            if (arrowTransform != null)
            {
                float angle = data.exitDirection switch
                {
                    ExitDirection.Up => 0f,
                    ExitDirection.Right => -90f,
                    ExitDirection.Down => 180f,
                    ExitDirection.Left => 90f,
                    _ => 0f
                };
                arrowTransform.localRotation = Quaternion.Euler(90f, 0f, angle);
                // Піднімаємо стрілочку на верхню грань 3D куба (Y > 0.5f)
                arrowTransform.localPosition = new Vector3(0f, 0.55f, 0f);

                if (CellType == CellType.HalfExitGate)
                {
                    Vector3 pScale = transform.localScale;
                    float invX = (Mathf.Abs(pScale.x) > 0.001f) ? (1f / Mathf.Abs(pScale.x)) : 1f;
                    float invZ = (Mathf.Abs(pScale.z) > 0.001f) ? (1f / Mathf.Abs(pScale.z)) : 1f;
                    float baseSize = 0.5f;
                    arrowTransform.localScale = new Vector3(invX * baseSize, invZ * baseSize, 1f);
                }
                else
                {
                    arrowTransform.localScale = Vector3.one;
                }
            }
        }
    }
}
