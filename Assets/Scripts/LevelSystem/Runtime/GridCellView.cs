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
            transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            transform.localScale = Vector3.one * cellSize;

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
                arrowTransform.localRotation = Quaternion.Euler(0, 0, angle);
            }
        }
    }
}
