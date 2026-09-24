using System;
using UnityEngine;

namespace LegoPuzzle.Data
{
    public enum CellType
    {
        Empty = 0,        // Поза полем / порожня ділянка
        Walkable = 1,     // Звичайний ігровий тайл (жовтий)
        Obstacle = 2,     // Дерев'яна нерухома перешкода (повна 1x1)
        ExitGate = 3,        // Ворота виходу для блоків (повні 1x1)
        HalfObstacle = 4,    // Перешкода в половину ширини (повертається у 4 напрямках: Up, Down, Left, Right)
        QuarterObstacle = 5, // Четвертина перешкоди для заповнення кутків (Top-Left, Top-Right, Bottom-Right, Bottom-Left)
        HalfExitGate = 6     // Напів-ворота виходу (половина ширини, повертаються у 4 напрямках)
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
        [Tooltip("0 = По центру, 1 = Ліва/Верхня половина, 2 = Права/Нижня половина")]
        public int gateAlignment;

        public CellData(Vector2Int position, CellType cellType)
        {
            this.position = position;
            this.cellType = cellType;
            this.exitDirection = ExitDirection.Up;
            this.gateColorType = BlockColorType.Universal;
            this.customGateColor = Color.white;
            this.gateAlignment = 1;
        }

        public Color GetEffectiveColor()
        {
            return ColorblindPalette.GetPieceColor(gateColorType, customGateColor);
        }

        public Color GetEffectiveColor(bool forceColorblind)
        {
            return ColorblindPalette.GetPieceColor(gateColorType, customGateColor, forceColorblind);
        }
    }
}
