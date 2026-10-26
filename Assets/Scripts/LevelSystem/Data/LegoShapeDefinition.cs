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
        /// та опційного горизонтального дзеркала базової форми (потрібно для L / J хіральності).
        /// </summary>
        public List<Vector2Int> GetRotatedOffsets(int rotationSteps, bool flipHorizontal = false)
        {
            rotationSteps = (rotationSteps % 4 + 4) % 4;

            List<Vector2Int> source = localOccupiedCells != null
                ? new List<Vector2Int>(localOccupiedCells)
                : new List<Vector2Int> { Vector2Int.zero };

            if (flipHorizontal)
            {
                source = FlipHorizontal(source);
            }

            if (rotationSteps == 0)
                return NormalizeToOrigin(source);

            List<Vector2Int> rotated = new List<Vector2Int>(source.Count);
            foreach (var cell in source)
            {
                Vector2Int newCell = cell;
                for (int i = 0; i < rotationSteps; i++)
                {
                    // Поворот на 90° за годинниковою: (x, y) -> (y, -x)
                    newCell = new Vector2Int(newCell.y, -newCell.x);
                }
                rotated.Add(newCell);
            }

            return NormalizeToOrigin(rotated);
        }

        private static List<Vector2Int> FlipHorizontal(List<Vector2Int> cells)
        {
            int maxX = 0;
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i].x > maxX) maxX = cells[i].x;
            }

            var flipped = new List<Vector2Int>(cells.Count);
            for (int i = 0; i < cells.Count; i++)
            {
                flipped.Add(new Vector2Int(maxX - cells[i].x, cells[i].y));
            }

            return flipped;
        }

        private static List<Vector2Int> NormalizeToOrigin(List<Vector2Int> cells)
        {
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i].x < minX) minX = cells[i].x;
                if (cells[i].y < minY) minY = cells[i].y;
            }

            for (int i = 0; i < cells.Count; i++)
            {
                cells[i] = new Vector2Int(cells[i].x - minX, cells[i].y - minY);
            }

            return cells;
        }

        public Vector2Int GetRotatedBounds(int rotationSteps, bool flipHorizontal = false)
        {
            var offsets = GetRotatedOffsets(rotationSteps, flipHorizontal);
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
