using System;
using System.Collections.Generic;
using UnityEngine;

namespace LegoPuzzle.Data
{
    [CreateAssetMenu(fileName = "Shape_", menuName = "LEGO Puzzle/Shape Definition")]
    public class LegoShapeDefinition : ScriptableObject
    {
        [Tooltip("Унікальний ідентифікатор форми (наприклад, Rect_2x4, T_Shape, L_Shape_3x3)")]
        public string shapeId = "Rect_2x2";

        [Tooltip("3D-префаб цієї форми з папки Pack_ColoredBlocks (наприклад, деталь L, деталь 2x4, деталь T)")]
        public GameObject prefab;

        [Tooltip("Розмір фігури в клітинках (Ширина, Висота)")]
        public Vector2Int bounds = new Vector2Int(2, 2);

        [Tooltip("Список локальних клітинок відносно точки прив'язки (0,0)")]
        public List<Vector2Int> localOccupiedCells = new List<Vector2Int>()
        {
            new Vector2Int(0, 0),
            new Vector2Int(1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(1, 1)
        };

        /// <summary>
        /// Повертає список зайнятих клітинок з урахуванням повороту (0 = 0°, 1 = 90°, 2 = 180°, 3 = 270° за годинниковою стрілкою)
        /// </summary>
        public List<Vector2Int> GetRotatedOffsets(int rotationSteps)
        {
            rotationSteps = (rotationSteps % 4 + 4) % 4;
            if (rotationSteps == 0)
                return new List<Vector2Int>(localOccupiedCells);

            List<Vector2Int> rotated = new List<Vector2Int>(localOccupiedCells.Count);

            foreach (var cell in localOccupiedCells)
            {
                Vector2Int newCell = cell;
                for (int i = 0; i < rotationSteps; i++)
                {
                    // Поворот на 90° за годинниковою: (x, y) -> (y, -x)
                    newCell = new Vector2Int(newCell.y, -newCell.x);
                }
                rotated.Add(newCell);
            }

            // Нормалізуємо, щоб мінімальні координати починалися з (0, 0)
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            foreach (var cell in rotated)
            {
                if (cell.x < minX) minX = cell.x;
                if (cell.y < minY) minY = cell.y;
            }

            for (int i = 0; i < rotated.Count; i++)
            {
                rotated[i] = new Vector2Int(rotated[i].x - minX, rotated[i].y - minY);
            }

            return rotated;
        }

        public Vector2Int GetRotatedBounds(int rotationSteps)
        {
            var offsets = GetRotatedOffsets(rotationSteps);
            int maxX = 0;
            int maxY = 0;
            foreach (var cell in offsets)
            {
                if (cell.x > maxX) maxX = cell.x;
                if (cell.y > maxY) maxY = cell.y;
            }
            return new Vector2Int(maxX + 1, maxY + 1);
        }
    }
}
