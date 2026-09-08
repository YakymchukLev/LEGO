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
            AlignAndScaleCell(cellSize);

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
                        if (palette.obstacleTileMaterial != null)
                        {
                            mainRenderer.sharedMaterial = palette.obstacleTileMaterial;
                        }
                        break;

                    case CellType.ExitGate:
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
                    gameObject.SetActive(true);
                    if (arrowRenderer != null) arrowRenderer.gameObject.SetActive(false);
                    break;

                case CellType.ExitGate:
                    gameObject.SetActive(true);
                    SetupExitGate(data);
                    break;
            }
        }

        private void AlignAndScaleCell(float cellSize)
        {
            MeshFilter mf = GetComponent<MeshFilter>();
            if (mf == null) mf = GetComponentInChildren<MeshFilter>(true);

            if (mf != null && mf.sharedMesh != null)
            {
                Bounds b = mf.sharedMesh.bounds;

                // Якщо це 2D Quad/Sprite у площині XY (Z ~ 0, але Y > 0)
                if (b.size.z < 0.01f && b.size.y > 0.01f)
                {
                    transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    float scaleX = b.size.x > 0.001f ? (cellSize / b.size.x) : cellSize;
                    float scaleY = b.size.y > 0.001f ? (cellSize / b.size.y) : cellSize;
                    transform.localScale = new Vector3(scaleX, scaleY, 1f);
                }
                else
                {
                    // Якщо це Plane (10x10), Cube (1x1x1) або 3D-модель у площині XZ
                    transform.localRotation = Quaternion.identity;
                    float rawSizeX = Mathf.Max(b.size.x, 0.01f);
                    float rawSizeZ = Mathf.Max(b.size.z, 0.01f);

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
                arrowTransform.localPosition = new Vector3(0f, 0.05f, 0f);
            }
        }
    }
}
