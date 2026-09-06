using System.Collections.Generic;
using UnityEngine;

namespace LegoPuzzle.Data
{
    [CreateAssetMenu(fileName = "Level_001", menuName = "LEGO Puzzle/Level Data")]
    public class LevelData : ScriptableObject
    {
        [Header("Основні параметри")]
        [Tooltip("Номер рівня для відображення в UI")]
        public int levelIndex = 1;

        [Tooltip("Назва або опис рівня")]
        public string levelTitle = "Рівень 1";

        [Header("Умови гри")]
        [Tooltip("Ліміт часу в секундах (0 = без ліміту)")]
        public float timeLimitSeconds = 95f;

        [Tooltip("Ліміт ходів (0 = без ліміту ходів, гра на час)")]
        public int movesLimit = 0;

        [Header("Розміри сітки")]
        [Range(3, 20)]
        public int gridWidth = 8;

        [Range(3, 25)]
        public int gridHeight = 12;

        [Header("Клітинки сітки (Форма поля, перешкоди та ворота)")]
        [SerializeField]
        public List<CellData> cells = new List<CellData>();

        [Header("Деталі LEGO на полі")]
        [SerializeField]
        public List<LegoPieceData> pieces = new List<LegoPieceData>();

        public bool IsInsideGrid(int x, int y)
        {
            return x >= 0 && x < gridWidth && y >= 0 && y < gridHeight;
        }

        public CellData GetCell(int x, int y)
        {
            int index = y * gridWidth + x;
            if (index >= 0 && index < cells.Count)
            {
                return cells[index];
            }
            return new CellData(new Vector2Int(x, y), CellType.Empty);
        }

        public void SetCell(int x, int y, CellData cellData)
        {
            EnsureGridCapacity();
            int index = y * gridWidth + x;
            if (index >= 0 && index < cells.Count)
            {
                cellData.position = new Vector2Int(x, y);
                cells[index] = cellData;
            }
        }

        public void EnsureGridCapacity()
        {
            int targetCount = gridWidth * gridHeight;
            if (cells == null)
            {
                cells = new List<CellData>(targetCount);
            }

            if (cells.Count != targetCount)
            {
                List<CellData> newCells = new List<CellData>(targetCount);
                for (int y = 0; y < gridHeight; y++)
                {
                    for (int x = 0; x < gridWidth; x++)
                    {
                        // Спробувати зберегти старе значення, якщо було
                        int oldIndex = y * gridWidth + x;
                        if (oldIndex < cells.Count && cells[oldIndex].position == new Vector2Int(x, y))
                        {
                            newCells.Add(cells[oldIndex]);
                        }
                        else
                        {
                            // За замовчуванням створюємо Walkable ігровий тайл
                            newCells.Add(new CellData(new Vector2Int(x, y), CellType.Walkable));
                        }
                    }
                }
                cells = newCells;
            }
        }
    }
}
