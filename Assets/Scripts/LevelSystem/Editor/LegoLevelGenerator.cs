#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using LegoPuzzle.Data;

namespace LegoPuzzle.Editor
{
    /// <summary>
    /// Процедурний генератор рівнів для LEGO Puzzle.
    /// Реалізує гібридний Reverse-Solver з гарантією 100% прохідності,
    /// автоматичну криву складності та швидкий BFS-валідатор ходів.
    /// </summary>
    public static class LegoLevelGenerator
    {
        [Serializable]
        public class GenerationSettings
        {
            [Header("Параметри рівня")]
            public int levelIndex = 1;
            public string levelTitle = "Level 1";
            public float timeLimitSeconds = 60f;
            public int movesLimit = 0;

            [Header("Розміри сітки")]
            public int gridWidth = 6;
            public int gridHeight = 6;

            [Header("Деталі LEGO")]
            public int pieceCount = 2;
            public bool allowFreeMove = true;
            public bool allowHorizontalOnly = false;
            public bool allowVerticalOnly = false;

            [Header("Перешкоди (Obstacles)")]
            public bool allowObstacles = false;
            public int obstacleCount = 0;

            [Header("Ворота")]
            public bool allowUniversalGates = true;
            public bool forceMatchingColorGates = false;

            [Header("Форми блоків")]
            public bool useShape1x1 = false;
            public bool useShape1x2 = true;
            public bool useShape1x3 = false;
            public bool useShape2x2 = true;
            public bool useShapeL = true;
            public bool useShapeL2x2 = false;
            public bool useShapeT3x2 = false;
            public bool useShapeCross3x3 = false;

            [Header("Палітра кольорів")]
            public List<BlockColorType> allowedColors = new List<BlockColorType>()
            {
                BlockColorType.Red,
                BlockColorType.Yellow,
                BlockColorType.Blue
            };

            [Header("Складність та Scramble")]
            public int targetMinMoves = 3;
            public int scrambleSteps = 60;
            public int randomSeed = 0; // 0 = випадковий час
        }

        public struct SolverResult
        {
            public bool isSolvable;
            public int optimalMoves;
            public List<string> solutionTrace;
            public int exploredStates;
        }

        public struct GenerationResult
        {
            public LevelData levelData;
            public SolverResult solverResult;
            public bool success;
            public string errorMessage;
        }

        #region Difficulty Curve

        /// <summary>
        /// Повертає збалансовані налаштування згідно з плавною кривою складності:
        /// Перші кілька рівнів прості -> поступово середні -> тактичні -> складні -> експертні.
        /// </summary>
        public static GenerationSettings GetCurveSettingsForLevel(int levelIndex)
        {
            var s = new GenerationSettings();
            s.levelIndex = Mathf.Max(1, levelIndex);
            s.levelTitle = $"Level {s.levelIndex}";
            s.randomSeed = s.levelIndex * 1337 + 42;

            if (s.levelIndex <= 3)
            {
                // Етап 1: Туторіал / Надпростий (2 деталі, вільний рух, без перешкод, універсальні ворота)
                s.gridWidth = 6;
                s.gridHeight = 6;
                s.pieceCount = 2;
                s.timeLimitSeconds = 60f;
                s.allowFreeMove = true;
                s.allowHorizontalOnly = false;
                s.allowVerticalOnly = false;
                s.allowObstacles = false;
                s.obstacleCount = 0;
                s.allowUniversalGates = true;
                s.forceMatchingColorGates = false;

                s.useShape1x1 = false;
                s.useShape1x2 = true;
                s.useShape1x3 = false;
                s.useShape2x2 = true;
                s.useShapeL = false;
                s.useShapeL2x2 = false;
                s.useShapeT3x2 = false;
                s.useShapeCross3x3 = false;

                s.allowedColors = new List<BlockColorType> { BlockColorType.Red, BlockColorType.Yellow };
                s.targetMinMoves = 2;
                s.scrambleSteps = 25;
            }
            else if (s.levelIndex <= 6)
            {
                // Етап 2: Простий (3 деталі, вільний рух, додається форма L)
                s.gridWidth = 6;
                s.gridHeight = 7;
                s.pieceCount = 3;
                s.timeLimitSeconds = 75f;
                s.allowFreeMove = true;
                s.allowHorizontalOnly = false;
                s.allowVerticalOnly = false;
                s.allowObstacles = false;
                s.obstacleCount = 0;
                s.allowUniversalGates = true;
                s.forceMatchingColorGates = false;

                s.useShape1x1 = false;
                s.useShape1x2 = true;
                s.useShape1x3 = false;
                s.useShape2x2 = true;
                s.useShapeL = true;
                s.useShapeL2x2 = false;
                s.useShapeT3x2 = false;
                s.useShapeCross3x3 = false;

                s.allowedColors = new List<BlockColorType> { BlockColorType.Red, BlockColorType.Yellow, BlockColorType.Blue };
                s.targetMinMoves = 4;
                s.scrambleSteps = 35;
            }
            else if (s.levelIndex <= 12)
            {
                // Етап 3: Легкий із першим знайомством зі слайдерами (3-4 деталі, 1 слайдер)
                s.gridWidth = 7;
                s.gridHeight = 7;
                s.pieceCount = (s.levelIndex >= 10) ? 4 : 3;
                s.timeLimitSeconds = 85f;
                s.allowFreeMove = true;
                s.allowHorizontalOnly = true;
                s.allowVerticalOnly = true;
                s.allowObstacles = false;
                s.obstacleCount = 0;
                s.allowUniversalGates = true;
                s.forceMatchingColorGates = false;

                s.useShape1x1 = false;
                s.useShape1x2 = true;
                s.useShape1x3 = true;
                s.useShape2x2 = true;
                s.useShapeL = true;
                s.useShapeL2x2 = false;
                s.useShapeT3x2 = false;
                s.useShapeCross3x3 = false;

                s.allowedColors = new List<BlockColorType> { BlockColorType.Red, BlockColorType.Yellow, BlockColorType.Blue, BlockColorType.Green };
                s.targetMinMoves = 5;
                s.scrambleSteps = 45;
            }
            else if (s.levelIndex <= 20)
            {
                // Етап 4: Середній / Казуальний (4 деталі, слайдери, перша дерев'яна перешкода)
                s.gridWidth = 7;
                s.gridHeight = 8;
                s.pieceCount = 4;
                s.timeLimitSeconds = 90f;
                s.allowFreeMove = true;
                s.allowHorizontalOnly = true;
                s.allowVerticalOnly = true;
                s.allowObstacles = true;
                s.obstacleCount = 1;
                s.allowUniversalGates = false;
                s.forceMatchingColorGates = true;

                s.useShape1x1 = false;
                s.useShape1x2 = true;
                s.useShape1x3 = true;
                s.useShape2x2 = true;
                s.useShapeL = true;
                s.useShapeL2x2 = true;
                s.useShapeT3x2 = false;
                s.useShapeCross3x3 = false;

                s.allowedColors = new List<BlockColorType> { BlockColorType.Red, BlockColorType.Yellow, BlockColorType.Blue, BlockColorType.Green, BlockColorType.Purple };
                s.targetMinMoves = 7;
                s.scrambleSteps = 60;
            }
            else if (s.levelIndex <= 35)
            {
                // Етап 5: Складний / Тактичний (5 деталей, T-подібні блоки, 2 перешкоди, вищі вимоги до логіки)
                s.gridWidth = 8;
                s.gridHeight = 9;
                s.pieceCount = 5;
                s.timeLimitSeconds = 105f;
                s.allowFreeMove = true;
                s.allowHorizontalOnly = true;
                s.allowVerticalOnly = true;
                s.allowObstacles = true;
                s.obstacleCount = 2;
                s.allowUniversalGates = false;
                s.forceMatchingColorGates = true;

                s.useShape1x1 = false;
                s.useShape1x2 = true;
                s.useShape1x3 = true;
                s.useShape2x2 = true;
                s.useShapeL = true;
                s.useShapeL2x2 = true;
                s.useShapeT3x2 = true;
                s.useShapeCross3x3 = false;

                s.allowedColors = new List<BlockColorType> { BlockColorType.Red, BlockColorType.Yellow, BlockColorType.Blue, BlockColorType.Green, BlockColorType.Purple, BlockColorType.Orange };
                s.targetMinMoves = 10;
                s.scrambleSteps = 80;
            }
            else if (s.levelIndex <= 50)
            {
                // Етап 6: Дуже складний (6 деталей, майже всі форми, 3 перешкоди, строгі коридори)
                s.gridWidth = 8;
                s.gridHeight = 10;
                s.pieceCount = 6;
                s.timeLimitSeconds = 120f;
                s.allowFreeMove = true;
                s.allowHorizontalOnly = true;
                s.allowVerticalOnly = true;
                s.allowObstacles = true;
                s.obstacleCount = 3;
                s.allowUniversalGates = false;
                s.forceMatchingColorGates = true;

                s.useShape1x1 = false;
                s.useShape1x2 = true;
                s.useShape1x3 = true;
                s.useShape2x2 = true;
                s.useShapeL = true;
                s.useShapeL2x2 = true;
                s.useShapeT3x2 = true;
                s.useShapeCross3x3 = true;

                s.allowedColors = new List<BlockColorType> { BlockColorType.Red, BlockColorType.Yellow, BlockColorType.Blue, BlockColorType.Green, BlockColorType.Purple, BlockColorType.Pink, BlockColorType.Orange };
                s.targetMinMoves = 14;
                s.scrambleSteps = 100;
            }
            else
            {
                // Етап 7: Експертний / Мастер (7 деталей, максимальний челендж)
                s.gridWidth = 8;
                s.gridHeight = 11;
                s.pieceCount = 7;
                s.timeLimitSeconds = 135f;
                s.allowFreeMove = true;
                s.allowHorizontalOnly = true;
                s.allowVerticalOnly = true;
                s.allowObstacles = true;
                s.obstacleCount = 4;
                s.allowUniversalGates = false;
                s.forceMatchingColorGates = true;

                s.useShape1x1 = false;
                s.useShape1x2 = true;
                s.useShape1x3 = true;
                s.useShape2x2 = true;
                s.useShapeL = true;
                s.useShapeL2x2 = true;
                s.useShapeT3x2 = true;
                s.useShapeCross3x3 = true;

                s.allowedColors = new List<BlockColorType> { BlockColorType.Red, BlockColorType.Yellow, BlockColorType.Blue, BlockColorType.Green, BlockColorType.Purple, BlockColorType.Pink, BlockColorType.Cyan, BlockColorType.Orange };
                s.targetMinMoves = 18;
                s.scrambleSteps = 120;
            }

            return s;
        }

        #endregion

        #region Reverse-Solver Generator

        public static GenerationResult GenerateLevel(GenerationSettings settings)
        {
            var result = new GenerationResult();
            System.Random rng = (settings.randomSeed != 0) 
                ? new System.Random(settings.randomSeed) 
                : new System.Random(Environment.TickCount);

            List<LegoShapeDefinition> shapePool = LoadShapesMatchingSettings(settings);
            if (shapePool.Count == 0)
            {
                result.success = false;
                result.errorMessage = "Не знайдено жодної форми LegoShapeDefinition, що відповідає вибраним фільтрам!";
                return result;
            }

            int maxAttempts = 15;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                LevelData candidate = ScriptableObject.CreateInstance<LevelData>();
                candidate.levelIndex = settings.levelIndex;
                candidate.levelTitle = settings.levelTitle;
                candidate.timeLimitSeconds = settings.timeLimitSeconds;
                candidate.movesLimit = settings.movesLimit;
                candidate.gridWidth = settings.gridWidth;
                candidate.gridHeight = settings.gridHeight;
                candidate.EnsureGridCapacity();

                // 1. Створюємо базове поле з воротами та стінами
                bool buildSuccess = BuildLevelWithReverseSolver(candidate, settings, shapePool, rng);
                if (!buildSuccess)
                {
                    UnityEngine.Object.DestroyImmediate(candidate);
                    continue;
                }

                // 2. BFS валідація
                SolverResult solverRes = SolveLevelBFS(candidate);
                if (solverRes.isSolvable && solverRes.optimalMoves >= Mathf.Min(settings.targetMinMoves, 3))
                {
                    result.levelData = candidate;
                    result.solverResult = solverRes;
                    result.success = true;
                    return result;
                }

                UnityEngine.Object.DestroyImmediate(candidate);
            }

            // Якщо після кількох спроб не досягнуто точної кількості ходів, робимо останню спробу з полегшеними критеріями
            LevelData fallback = ScriptableObject.CreateInstance<LevelData>();
            fallback.levelIndex = settings.levelIndex;
            fallback.levelTitle = settings.levelTitle;
            fallback.timeLimitSeconds = settings.timeLimitSeconds;
            fallback.movesLimit = settings.movesLimit;
            fallback.gridWidth = settings.gridWidth;
            fallback.gridHeight = settings.gridHeight;
            fallback.EnsureGridCapacity();

            BuildLevelWithReverseSolver(fallback, settings, shapePool, rng);
            SolverResult fallbackSolver = SolveLevelBFS(fallback);

            result.levelData = fallback;
            result.solverResult = fallbackSolver;
            result.success = fallbackSolver.isSolvable;
            if (!result.success)
            {
                result.errorMessage = "Не вдалося згенерувати розв'язувану конфігурацію. Спробуйте змінити розмір поля або кількість деталей.";
            }

            return result;
        }

        private static bool BuildLevelWithReverseSolver(
            LevelData level, 
            GenerationSettings settings, 
            List<LegoShapeDefinition> shapePool, 
            System.Random rng)
        {
            int W = settings.gridWidth;
            int H = settings.gridHeight;

            // Крок 1: Ініціалізація периметра (кутки QuarterObstacle, стіни HalfObstacle)
            // Та ігрового поля (Walkable)
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    Vector2Int pos = new Vector2Int(x, y);
                    if (x == 0 && y == 0)
                    {
                        var cell = new CellData(pos, CellType.QuarterObstacle) { exitDirection = ExitDirection.Down };
                        level.SetCell(x, y, cell);
                    }
                    else if (x == W - 1 && y == 0)
                    {
                        var cell = new CellData(pos, CellType.QuarterObstacle) { exitDirection = ExitDirection.Right };
                        level.SetCell(x, y, cell);
                    }
                    else if (x == 0 && y == H - 1)
                    {
                        var cell = new CellData(pos, CellType.QuarterObstacle) { exitDirection = ExitDirection.Left };
                        level.SetCell(x, y, cell);
                    }
                    else if (x == W - 1 && y == H - 1)
                    {
                        var cell = new CellData(pos, CellType.QuarterObstacle) { exitDirection = ExitDirection.Up };
                        level.SetCell(x, y, cell);
                    }
                    else if (y == 0)
                    {
                        var cell = new CellData(pos, CellType.HalfObstacle) { exitDirection = ExitDirection.Down };
                        level.SetCell(x, y, cell);
                    }
                    else if (y == H - 1)
                    {
                        var cell = new CellData(pos, CellType.HalfObstacle) { exitDirection = ExitDirection.Up };
                        level.SetCell(x, y, cell);
                    }
                    else if (x == 0)
                    {
                        var cell = new CellData(pos, CellType.HalfObstacle) { exitDirection = ExitDirection.Left };
                        level.SetCell(x, y, cell);
                    }
                    else if (x == W - 1)
                    {
                        var cell = new CellData(pos, CellType.HalfObstacle) { exitDirection = ExitDirection.Right };
                        level.SetCell(x, y, cell);
                    }
                    else
                    {
                        var cell = new CellData(pos, CellType.Walkable);
                        level.SetCell(x, y, cell);
                    }
                }
            }

            // Крок 2: Вибір та конфігурація деталей
            int count = Mathf.Clamp(settings.pieceCount, 1, 8);
            List<LegoPieceData> pieceList = new List<LegoPieceData>();

            // Розрахунок обмежень руху (Sliders)
            List<MoveRestriction> restrictions = new List<MoveRestriction>();
            for (int i = 0; i < count; i++)
            {
                if (i == 0 && settings.allowFreeMove)
                {
                    restrictions.Add(MoveRestriction.Free);
                }
                else if (settings.allowHorizontalOnly && rng.NextDouble() < 0.35)
                {
                    restrictions.Add(MoveRestriction.HorizontalOnly);
                }
                else if (settings.allowVerticalOnly && rng.NextDouble() < 0.35)
                {
                    restrictions.Add(MoveRestriction.VerticalOnly);
                }
                else
                {
                    restrictions.Add(MoveRestriction.Free);
                }
            }

            // Вибір кольорів
            List<BlockColorType> colors = (settings.allowedColors != null && settings.allowedColors.Count > 0)
                ? new List<BlockColorType>(settings.allowedColors)
                : new List<BlockColorType> { BlockColorType.Red, BlockColorType.Yellow };

            // Крок 3: Розміщення воріт та фінальних позицій виходу деталей
            List<ExitDirection> sides = new List<ExitDirection>
            {
                ExitDirection.Down,
                ExitDirection.Up,
                ExitDirection.Left,
                ExitDirection.Right
            };

            // Мапи зайнятих воріт на сторонах: side -> list of occupied range on border
            Dictionary<ExitDirection, HashSet<int>> borderUsedCoords = new Dictionary<ExitDirection, HashSet<int>>
            {
                { ExitDirection.Down, new HashSet<int>() },
                { ExitDirection.Up, new HashSet<int>() },
                { ExitDirection.Left, new HashSet<int>() },
                { ExitDirection.Right, new HashSet<int>() }
            };

            for (int i = 0; i < count; i++)
            {
                var shape = shapePool[rng.Next(shapePool.Count)];
                int rot = rng.Next(4);
                Vector2Int bounds = shape.GetRotatedBounds(rot);
                var restriction = restrictions[i];

                // Визначаємо валідні сторони для виходу з огляду на обмеження руху
                List<ExitDirection> validSides = new List<ExitDirection>();
                if (restriction == MoveRestriction.HorizontalOnly)
                {
                    validSides.Add(ExitDirection.Left);
                    validSides.Add(ExitDirection.Right);
                }
                else if (restriction == MoveRestriction.VerticalOnly)
                {
                    validSides.Add(ExitDirection.Down);
                    validSides.Add(ExitDirection.Up);
                }
                else
                {
                    validSides.AddRange(sides);
                }

                // Перемішуємо сторони
                ShuffleList(validSides, rng);

                bool placedGate = false;
                ExitDirection chosenSide = ExitDirection.Down;
                Vector2Int solvedOrigin = Vector2Int.zero;
                int gateStartCoord = -1;
                int gateLength = 0;

                foreach (var side in validSides)
                {
                    if (side == ExitDirection.Down || side == ExitDirection.Up)
                    {
                        gateLength = bounds.x;
                        int maxStart = (W - 1) - gateLength;
                        if (maxStart < 1) continue;

                        List<int> candidateCoords = new List<int>();
                        for (int x = 1; x <= maxStart; x++)
                        {
                            bool conflict = false;
                            for (int gx = x; gx < x + gateLength; gx++)
                            {
                                if (borderUsedCoords[side].Contains(gx))
                                {
                                    conflict = true;
                                    break;
                                }
                            }
                            if (!conflict) candidateCoords.Add(x);
                        }

                        if (candidateCoords.Count > 0)
                        {
                            gateStartCoord = candidateCoords[rng.Next(candidateCoords.Count)];
                            chosenSide = side;
                            placedGate = true;
                            solvedOrigin = (side == ExitDirection.Down)
                                ? new Vector2Int(gateStartCoord, 1)
                                : new Vector2Int(gateStartCoord, H - 1 - bounds.y);
                            break;
                        }
                    }
                    else // Left or Right
                    {
                        gateLength = bounds.y;
                        int maxStart = (H - 1) - gateLength;
                        if (maxStart < 1) continue;

                        List<int> candidateCoords = new List<int>();
                        for (int y = 1; y <= maxStart; y++)
                        {
                            bool conflict = false;
                            for (int gy = y; gy < y + gateLength; gy++)
                            {
                                if (borderUsedCoords[side].Contains(gy))
                                {
                                    conflict = true;
                                    break;
                                }
                            }
                            if (!conflict) candidateCoords.Add(y);
                        }

                        if (candidateCoords.Count > 0)
                        {
                            gateStartCoord = candidateCoords[rng.Next(candidateCoords.Count)];
                            chosenSide = side;
                            placedGate = true;
                            solvedOrigin = (side == ExitDirection.Left)
                                ? new Vector2Int(1, gateStartCoord)
                                : new Vector2Int(W - 1 - bounds.x, gateStartCoord);
                            break;
                        }
                    }
                }

                if (!placedGate)
                {
                    // Не вмістилися ворота — невдача спроби, спробуємо з іншим seed/розміром
                    return false;
                }

                // Вибираємо колір деталі та воріт
                BlockColorType color = colors[i % colors.Count];
                BlockColorType gateColor = (settings.allowUniversalGates && rng.NextDouble() < 0.25)
                    ? BlockColorType.Universal
                    : color;

                // Займаємо ворота на периметрі
                for (int g = gateStartCoord; g < gateStartCoord + gateLength; g++)
                {
                    borderUsedCoords[chosenSide].Add(g);

                    Vector2Int gatePos = chosenSide switch
                    {
                        ExitDirection.Down => new Vector2Int(g, 0),
                        ExitDirection.Up => new Vector2Int(g, H - 1),
                        ExitDirection.Left => new Vector2Int(0, g),
                        ExitDirection.Right => new Vector2Int(W - 1, g),
                        _ => Vector2Int.zero
                    };

                    int alignment = chosenSide switch
                    {
                        ExitDirection.Up => 0,
                        ExitDirection.Down => 1,
                        ExitDirection.Left => 2,
                        ExitDirection.Right => 3,
                        _ => 1
                    };

                    var gateCell = new CellData(gatePos, CellType.HalfExitGate)
                    {
                        exitDirection = chosenSide,
                        gateColorType = gateColor,
                        gateAlignment = alignment
                    };
                    level.SetCell(gatePos.x, gatePos.y, gateCell);
                }

                var piece = new LegoPieceData
                {
                    pieceId = $"Piece_{i + 1}",
                    shape = shape,
                    rotationSteps = rot,
                    originPosition = solvedOrigin,
                    colorType = color,
                    moveRestriction = restriction,
                    isRequiredForWin = true
                };

                pieceList.Add(piece);
            }

            level.pieces = pieceList;

            // Крок 4: Зворотне перемішування (Reverse Scramble)
            // Починаємо з вирішеного стану і рухаємо деталі вглиб сітки
            bool scrambleOk = ExecuteReverseScramble(level, settings.scrambleSteps, rng);
            if (!scrambleOk) return false;

            // Крок 5: Розміщення внутрішніх дерев'яних перешкод (Obstacles)
            if (settings.allowObstacles && settings.obstacleCount > 0)
            {
                PlaceInteriorObstacles(level, settings.obstacleCount, rng);
            }

            return true;
        }

        private static bool ExecuteReverseScramble(LevelData level, int totalSteps, System.Random rng)
        {
            int W = level.gridWidth;
            int H = level.gridHeight;
            int piecesCount = level.pieces.Count;

            Vector2Int[] dirs = new Vector2Int[]
            {
                Vector2Int.up,
                Vector2Int.down,
                Vector2Int.left,
                Vector2Int.right
            };

            // Мапа зайнятих клітинок
            HashSet<Vector2Int> occupiedCells = new HashSet<Vector2Int>();
            foreach (var p in level.pieces)
            {
                foreach (var cell in p.GetOccupiedGridCells())
                {
                    if (occupiedCells.Contains(cell))
                    {
                        // Початковий перетин (конфлікт позицій)
                        return false;
                    }
                    occupiedCells.Add(cell);
                }
            }

            for (int step = 0; step < totalSteps; step++)
            {
                int pIndex = rng.Next(piecesCount);
                var p = level.pieces[pIndex];

                // Валідні напрямки для цієї деталі
                List<Vector2Int> validDirs = new List<Vector2Int>();
                if (p.moveRestriction == MoveRestriction.HorizontalOnly)
                {
                    validDirs.Add(Vector2Int.left);
                    validDirs.Add(Vector2Int.right);
                }
                else if (p.moveRestriction == MoveRestriction.VerticalOnly)
                {
                    validDirs.Add(Vector2Int.up);
                    validDirs.Add(Vector2Int.down);
                }
                else
                {
                    validDirs.AddRange(dirs);
                }

                ShuffleList(validDirs, rng);

                foreach (var dir in validDirs)
                {
                    // Спробуємо зсунути на 1 або 2 клітинки
                    int distance = rng.Next(1, 3);
                    Vector2Int targetOrigin = p.originPosition + dir * distance;

                    if (CanPieceFitAt(level, p, targetOrigin))
                    {
                        p.originPosition = targetOrigin;
                        break;
                    }
                    else if (distance > 1)
                    {
                        targetOrigin = p.originPosition + dir;
                        if (CanPieceFitAt(level, p, targetOrigin))
                        {
                            p.originPosition = targetOrigin;
                            break;
                        }
                    }
                }
            }

            return true;
        }

        private static bool CanPieceFitAt(LevelData level, LegoPieceData piece, Vector2Int newOrigin)
        {
            int W = level.gridWidth;
            int H = level.gridHeight;

            var offsets = piece.shape.GetRotatedOffsets(piece.rotationSteps);
            for (int i = 0; i < offsets.Count; i++)
            {
                Vector2Int cell = newOrigin + offsets[i];
                // Повинна бути в межах ігрового поля (не на стінах)
                if (cell.x < 1 || cell.x > W - 2 || cell.y < 1 || cell.y > H - 2)
                    return false;

                CellData cellData = level.GetCell(cell.x, cell.y);
                if (cellData.cellType != CellType.Walkable)
                    return false;

                // Перевірка перетину з іншими блоками
                foreach (var other in level.pieces)
                {
                    if (other == piece) continue;
                    var otherOffsets = other.shape.GetRotatedOffsets(other.rotationSteps);
                    for (int j = 0; j < otherOffsets.Count; j++)
                    {
                        if (cell == other.originPosition + otherOffsets[j])
                            return false;
                    }
                }
            }

            return true;
        }

        private static void PlaceInteriorObstacles(LevelData level, int count, System.Random rng)
        {
            int W = level.gridWidth;
            int H = level.gridHeight;

            HashSet<Vector2Int> occupied = new HashSet<Vector2Int>();
            foreach (var piece in level.pieces)
            {
                foreach (var c in piece.GetOccupiedGridCells())
                    occupied.Add(c);
            }

            List<Vector2Int> candidates = new List<Vector2Int>();
            for (int y = 2; y <= H - 3; y++)
            {
                for (int x = 2; x <= W - 3; x++)
                {
                    Vector2Int pos = new Vector2Int(x, y);
                    if (!occupied.Contains(pos))
                    {
                        candidates.Add(pos);
                    }
                }
            }

            ShuffleList(candidates, rng);
            int placed = 0;
            for (int i = 0; i < candidates.Count && placed < count; i++)
            {
                Vector2Int pos = candidates[i];
                level.SetCell(pos.x, pos.y, new CellData(pos, CellType.Obstacle));
                placed++;
            }
        }

        #endregion

        #region Fast BFS Solver

        private class BoardState
        {
            public Vector2Int[] pieceOrigins;
            public bool[] pieceExited;
            public int moves;
            public string lastMoveDescription;
            public BoardState previousState;

            public string GetKey()
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder(pieceOrigins.Length * 6);
                for (int i = 0; i < pieceOrigins.Length; i++)
                {
                    if (pieceExited[i])
                    {
                        sb.Append("X,");
                    }
                    else
                    {
                        sb.Append(pieceOrigins[i].x);
                        sb.Append(':');
                        sb.Append(pieceOrigins[i].y);
                        sb.Append(',');
                    }
                }
                return sb.ToString();
            }
        }

        public static SolverResult SolveLevelBFS(LevelData level, int maxStates = 30000)
        {
            var result = new SolverResult
            {
                isSolvable = false,
                optimalMoves = 0,
                solutionTrace = new List<string>(),
                exploredStates = 0
            };

            int n = level.pieces.Count;
            if (n == 0)
            {
                result.isSolvable = true;
                return result;
            }

            var initialState = new BoardState
            {
                pieceOrigins = new Vector2Int[n],
                pieceExited = new bool[n],
                moves = 0,
                lastMoveDescription = "Start"
            };

            for (int i = 0; i < n; i++)
            {
                initialState.pieceOrigins[i] = level.pieces[i].originPosition;
                initialState.pieceExited[i] = false;
            }

            Queue<BoardState> queue = new Queue<BoardState>();
            HashSet<string> visited = new HashSet<string>();

            queue.Enqueue(initialState);
            visited.Add(initialState.GetKey());

            Vector2Int[] dirs = new Vector2Int[]
            {
                Vector2Int.up,
                Vector2Int.down,
                Vector2Int.left,
                Vector2Int.right
            };

            while (queue.Count > 0)
            {
                var state = queue.Dequeue();
                result.exploredStates++;

                // Перевірка чи всі деталі вийшли
                bool allExited = true;
                for (int i = 0; i < n; i++)
                {
                    if (!state.pieceExited[i])
                    {
                        allExited = false;
                        break;
                    }
                }

                if (allExited)
                {
                    result.isSolvable = true;
                    result.optimalMoves = state.moves;

                    // Відновлення ланцюжка розв'язку
                    var cur = state;
                    while (cur != null && cur.previousState != null)
                    {
                        result.solutionTrace.Insert(0, cur.lastMoveDescription);
                        cur = cur.previousState;
                    }
                    return result;
                }

                if (result.exploredStates >= maxStates)
                {
                    break;
                }

                // Перебираємо всі активні деталі
                for (int i = 0; i < n; i++)
                {
                    if (state.pieceExited[i]) continue;

                    var pieceData = level.pieces[i];
                    var restriction = pieceData.moveRestriction;

                    foreach (var dir in dirs)
                    {
                        if (restriction == MoveRestriction.HorizontalOnly && (dir == Vector2Int.up || dir == Vector2Int.down))
                            continue;
                        if (restriction == MoveRestriction.VerticalOnly && (dir == Vector2Int.left || dir == Vector2Int.right))
                            continue;

                        // Перевіряємо чи можна вийти через ворота в цьому напрямку
                        if (CanExitThroughGate(level, pieceData, state.pieceOrigins[i], dir, state, i))
                        {
                            // Стан з виходом деталі
                            var nextState = CloneState(state);
                            nextState.pieceExited[i] = true;
                            nextState.moves = state.moves + 1;
                            nextState.lastMoveDescription = $"{pieceData.pieceId} виходить через {dir} ворота";
                            nextState.previousState = state;

                            string key = nextState.GetKey();
                            if (!visited.Contains(key))
                            {
                                visited.Add(key);
                                queue.Enqueue(nextState);
                            }
                            continue;
                        }

                        // Звичайний рух на 1 крок усередині поля
                        Vector2Int newOrigin = state.pieceOrigins[i] + dir;
                        if (CanMoveTo(level, pieceData, newOrigin, state, i))
                        {
                            var nextState = CloneState(state);
                            nextState.pieceOrigins[i] = newOrigin;
                            nextState.moves = state.moves + 1;
                            nextState.lastMoveDescription = $"{pieceData.pieceId} посунуто {dir}";
                            nextState.previousState = state;

                            string key = nextState.GetKey();
                            if (!visited.Contains(key))
                            {
                                visited.Add(key);
                                queue.Enqueue(nextState);
                            }
                        }
                    }
                }
            }

            return result;
        }

        private static bool CanMoveTo(LevelData level, LegoPieceData piece, Vector2Int origin, BoardState state, int pieceIdx)
        {
            int W = level.gridWidth;
            int H = level.gridHeight;
            var offsets = piece.shape.GetRotatedOffsets(piece.rotationSteps);

            for (int k = 0; k < offsets.Count; k++)
            {
                Vector2Int c = origin + offsets[k];
                if (c.x < 1 || c.x > W - 2 || c.y < 1 || c.y > H - 2)
                    return false;

                CellData cellData = level.GetCell(c.x, c.y);
                if (cellData.cellType != CellType.Walkable)
                    return false;

                // Перевірка колізій з іншими деталями
                for (int otherIdx = 0; otherIdx < state.pieceOrigins.Length; otherIdx++)
                {
                    if (otherIdx == pieceIdx || state.pieceExited[otherIdx]) continue;

                    var otherPiece = level.pieces[otherIdx];
                    var otherOffsets = otherPiece.shape.GetRotatedOffsets(otherPiece.rotationSteps);
                    Vector2Int otherOrigin = state.pieceOrigins[otherIdx];

                    for (int o = 0; o < otherOffsets.Count; o++)
                    {
                        if (c == otherOrigin + otherOffsets[o])
                            return false;
                    }
                }
            }

            return true;
        }

        private static bool CanExitThroughGate(
            LevelData level, 
            LegoPieceData piece, 
            Vector2Int origin, 
            Vector2Int dir, 
            BoardState state, 
            int pieceIdx)
        {
            int W = level.gridWidth;
            int H = level.gridHeight;
            var offsets = piece.shape.GetRotatedOffsets(piece.rotationSteps);

            ExitDirection expectedExitDir = (dir == Vector2Int.up) ? ExitDirection.Up
                : (dir == Vector2Int.down) ? ExitDirection.Down
                : (dir == Vector2Int.left) ? ExitDirection.Left
                : ExitDirection.Right;

            // Збираємо крайні клітинки деталі у напрямку dir
            HashSet<int> borderCoords = new HashSet<int>();
            int extremeCoord = (dir == Vector2Int.up || dir == Vector2Int.right) ? int.MinValue : int.MaxValue;

            for (int k = 0; k < offsets.Count; k++)
            {
                Vector2Int cell = origin + offsets[k];
                if (dir == Vector2Int.down)
                {
                    if (cell.y < extremeCoord) { extremeCoord = cell.y; }
                    borderCoords.Add(cell.x);
                }
                else if (dir == Vector2Int.up)
                {
                    if (cell.y > extremeCoord) { extremeCoord = cell.y; }
                    borderCoords.Add(cell.x);
                }
                else if (dir == Vector2Int.left)
                {
                    if (cell.x < extremeCoord) { extremeCoord = cell.x; }
                    borderCoords.Add(cell.y);
                }
                else if (dir == Vector2Int.right)
                {
                    if (cell.x > extremeCoord) { extremeCoord = cell.x; }
                    borderCoords.Add(cell.y);
                }
            }

            // Перевіряємо чи стоїть деталь впритик до воріт
            if (dir == Vector2Int.down && extremeCoord != 1) return false;
            if (dir == Vector2Int.up && extremeCoord != H - 2) return false;
            if (dir == Vector2Int.left && extremeCoord != 1) return false;
            if (dir == Vector2Int.right && extremeCoord != W - 2) return false;

            // Перевіряємо чи всі комірки на цій стороні воріт відповідають виходу
            foreach (int coord in borderCoords)
            {
                Vector2Int gatePos = dir == Vector2Int.down ? new Vector2Int(coord, 0)
                    : dir == Vector2Int.up ? new Vector2Int(coord, H - 1)
                    : dir == Vector2Int.left ? new Vector2Int(0, coord)
                    : new Vector2Int(W - 1, coord);

                CellData gateCell = level.GetCell(gatePos.x, gatePos.y);
                if (gateCell.cellType != CellType.HalfExitGate && gateCell.cellType != CellType.ExitGate)
                    return false;

                if (gateCell.exitDirection != expectedExitDir)
                    return false;

                if (gateCell.gateColorType != BlockColorType.Universal && gateCell.gateColorType != piece.colorType)
                    return false;
            }

            return true;
        }

        private static BoardState CloneState(BoardState src)
        {
            int n = src.pieceOrigins.Length;
            var dest = new BoardState
            {
                pieceOrigins = new Vector2Int[n],
                pieceExited = new bool[n],
                moves = src.moves
            };
            Array.Copy(src.pieceOrigins, dest.pieceOrigins, n);
            Array.Copy(src.pieceExited, dest.pieceExited, n);
            return dest;
        }

        #endregion

        #region Helpers

        private static List<LegoShapeDefinition> LoadShapesMatchingSettings(GenerationSettings settings)
        {
            List<LegoShapeDefinition> result = new List<LegoShapeDefinition>();
            string[] guids = AssetDatabase.FindAssets("t:LegoShapeDefinition");

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var shape = AssetDatabase.LoadAssetAtPath<LegoShapeDefinition>(path);
                if (shape == null) continue;

                string name = (string.IsNullOrEmpty(shape.shapeId) ? shape.name : shape.shapeId).ToLowerInvariant();

                if (settings.useShape1x1 && (name.Contains("1x1") || shape.localOccupiedCells.Count == 1))
                    result.Add(shape);
                else if (settings.useShape1x2 && (name.Contains("1x2") || (shape.bounds.x == 2 && shape.bounds.y == 1 && shape.localOccupiedCells.Count == 2)))
                    result.Add(shape);
                else if (settings.useShape1x3 && (name.Contains("1x3") || (shape.bounds.x == 3 && shape.bounds.y == 1 && shape.localOccupiedCells.Count == 3)))
                    result.Add(shape);
                else if (settings.useShape2x2 && (name.Contains("2x2") && !name.Contains("l") && shape.localOccupiedCells.Count == 4))
                    result.Add(shape);
                else if (settings.useShapeL && (name.Contains("shape_l") && !name.Contains("2x2")))
                    result.Add(shape);
                else if (settings.useShapeL2x2 && name.Contains("l_2x2"))
                    result.Add(shape);
                else if (settings.useShapeT3x2 && name.Contains("t_3x2"))
                    result.Add(shape);
                else if (settings.useShapeCross3x3 && name.Contains("cross"))
                    result.Add(shape);
            }

            // Fallback: якщо за фільтрами нічого не підійшло, додаємо всі знайдені
            if (result.Count == 0)
            {
                foreach (var guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var shape = AssetDatabase.LoadAssetAtPath<LegoShapeDefinition>(path);
                    if (shape != null) result.Add(shape);
                }
            }

            return result;
        }

        private static void ShuffleList<T>(IList<T> list, System.Random rng)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        #endregion
    }
}
#endif
