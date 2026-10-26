using System;
using System.Collections.Generic;
using UnityEngine;

namespace LegoPuzzle.Data
{
    [Serializable]
    public class LegoPieceData
    {
        [Tooltip("Унікальний ідентифікатор деталі на рівні")]
        public string pieceId = "Piece_1";

        [Tooltip("Визначення форми фігури (ScriptableObject)")]
        public LegoShapeDefinition shape;

        [Tooltip("Початкова координата кореневого лівого нижнього кута на сітці (X, Y)")]
        public Vector2Int originPosition;

        [Tooltip("Кількість кроків повороту на 90° (0 = 0°, 1 = 90°, 2 = 180°, 3 = 270°)")]
        [Range(0, 3)]
        public int rotationSteps = 0;

        [Tooltip("Горизонтальне дзеркало базової форми (для L дає J: поворот 0°, але навпаки)")]
        public bool flipHorizontal = false;

        [Tooltip("Колір деталі")]
        public BlockColorType colorType = BlockColorType.Red;

        [Tooltip("Користувацький колір (якщо вибрано Custom)")]
        public Color customColor = Color.red;

        [Tooltip("Обмеження пересування")]
        public MoveRestriction moveRestriction = MoveRestriction.Free;

        [Tooltip("Чи обов'язково вивести цей блок для перемоги")]
        public bool isRequiredForWin = true;

        [Tooltip("Початковий лічильник заморозки (0 = розморожений блок, >0 = заморожений блок, не може рухатись поки не знищено вказану кількість інших блоків)")]
        public int freezeCount = 0;

        public bool IsFrozen => freezeCount > 0;

        /// <summary>
        /// Повертає список абсолютних координат сітки, які займає цей блок
        /// </summary>
        public List<Vector2Int> GetOccupiedGridCells()
        {
            List<Vector2Int> occupied = new List<Vector2Int>();
            if (shape == null)
            {
                occupied.Add(originPosition);
                return occupied;
            }

            var offsets = GetOrientedOffsets();
            foreach (var offset in offsets)
            {
                occupied.Add(originPosition + offset);
            }

            return occupied;
        }

        public List<Vector2Int> GetOrientedOffsets()
        {
            if (shape == null)
            {
                return new List<Vector2Int> { Vector2Int.zero };
            }

            return shape.GetRotatedOffsets(rotationSteps, flipHorizontal);
        }

        public Vector2Int GetOrientedBounds()
        {
            if (shape == null) return Vector2Int.one;
            return shape.GetRotatedBounds(rotationSteps, flipHorizontal);
        }

        public Color GetColor()
        {
            return ColorblindPalette.GetPieceColor(colorType, customColor);
        }

        public Color GetColor(bool forceColorblind)
        {
            return ColorblindPalette.GetPieceColor(colorType, customColor, forceColorblind);
        }
    }
}
