using System;
using UnityEngine;

namespace LegoPuzzle.Data
{
    public enum CellType
    {
        Empty = 0,       // Поза полем / порожня ділянка
        Walkable = 1,    // Звичайний ігровий тайл (жовтий)
        Obstacle = 2,    // Дерев'яна нерухома перешкода
        ExitGate = 3     // Ворота виходу для блоків
    }

    public enum ExitDirection
    {
        Up = 0,
        Down = 1,
        Left = 2,
        Right = 3
    }

    public enum MoveRestriction
    {
        Free = 0,           // Рух у всі доступні боки
        HorizontalOnly = 1, // Тільки горизонтально (<->)
        VerticalOnly = 2,   // Тільки вертикально
        Locked = 3          // Заблокований до виконання умови / бустера
    }

    public enum BlockColorType
    {
        Red = 0,
        Yellow = 1,
        Blue = 2,
        Green = 3,
        Purple = 4,
        Pink = 5,
        Cyan = 6,
        Orange = 7,
        Custom = 8,
        Universal = 9 // Універсальні ворота: вихід для будь-яких блоків
    }

    [Serializable]
    public struct CellData
    {
        public Vector2Int position;
        public CellType cellType;
        public ExitDirection exitDirection;
        public BlockColorType gateColorType;
        public Color customGateColor;

        public CellData(Vector2Int position, CellType cellType)
        {
            this.position = position;
            this.cellType = cellType;
            this.exitDirection = ExitDirection.Up;
            this.gateColorType = BlockColorType.Universal;
            this.customGateColor = Color.white;
        }

        public Color GetEffectiveColor()
        {
            return gateColorType switch
            {
                BlockColorType.Red => new Color(0.95f, 0.2f, 0.2f),
                BlockColorType.Yellow => new Color(0.98f, 0.88f, 0.15f),
                BlockColorType.Blue => new Color(0.15f, 0.45f, 0.95f),
                BlockColorType.Green => new Color(0.25f, 0.85f, 0.25f),
                BlockColorType.Purple => new Color(0.65f, 0.15f, 0.85f),
                BlockColorType.Pink => new Color(0.98f, 0.45f, 0.75f),
                BlockColorType.Cyan => new Color(0.2f, 0.85f, 0.95f),
                BlockColorType.Orange => new Color(0.98f, 0.55f, 0.15f),
                BlockColorType.Universal => new Color(0.95f, 0.95f, 0.95f),
                _ => customGateColor
            };
        }
    }
}
