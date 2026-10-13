#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using LegoPuzzle.Data;

namespace LegoPuzzle.Editor
{
    /// <summary>
    /// Процедурний генератор рівнів для LEGO Puzzle (Версія 2.0).
    /// Включає:
    /// - Точні орієнтації бортиків всередину (Inward) без щілин і розривів.
    /// - Вимкнені універсальні ворота (100% строгі кольори деталей).
    /// - Різноманітні архетипи полів (Острови, перешийки, лабіринти, паркувальні кишені).
    /// - Глибокий Scramble з гарантією блокування воріт на 1-му кроці.
    /// - Жорсткий контроль складності через BFS-розв'язувач.
    /// </summary>
    public static class LegoLevelGenerator
    {
        public enum BoardArchetype
        {
            StandardArena = 0,       // Класична відкрита арена
            CenterPillar = 1,        // Центральний острів / моноліт (Donut/Ring)
            ChokepointChambers = 2,  // Дві кімнати з вузьким перешийком (Hourglass)
            CorridorMaze = 3,        // Лабіринтні повороти
            Alcoves = 4,             // Паркувальні кишені / тупики для роз'їзду (Klotski alcoves)
            FortressVault = 5,       // Фортеця з кутовими бастіонами (хрестоподібна арена)
            SwitchbackLabyrinth = 6, // Звивисті S-подібні перегородки (серпантин)
            AsymmetricIslands = 7    // Асиметричні острови перешкод
        }

        public enum DifficultyTier
        {
            Custom = 0,         // Власні налаштування
            VeryEasy = 1,       // Дуже легко (2-3 деталі, 3-5 ходів, без перешкод)
            Easy = 2,           // Легко (4 деталі, 6-9 ходів, чиста арена 7x7)
            Medium = 3,         // Середньо (6 деталей, 12-16 ходів, 8x8, 2 слайдери, перешкоди)
            Hard = 4,           // Складно (8 деталей, 18-24 ходи, 9x9, 3 слайдери, перешийок)
            VeryHard = 5,       // Дуже складно (10 деталей, 25-32 ходи, 10x10, 4 слайдери, лабіринти)
            Expert = 6          // Експерт (11 деталей, 32-45+ ходів, 10x12, 5 слайдерів, майстер-лабіринт 5-7 хв)
        }

        [Serializable]
        public class GenerationSettings
        {
            [Header("Параметри рівня")]
            public int levelIndex = 1;
            public string levelTitle = "Level 1";
            public float timeLimitSeconds = 60f;
            public int movesLimit = 0;
            public DifficultyTier difficultyTier = DifficultyTier.Easy;

            [Header("Розміри сітки")]
            public int gridWidth = 6;
            public int gridHeight = 6;
            public BoardArchetype archetype = BoardArchetype.StandardArena;

            [Header("Деталі LEGO")]
            public int pieceCount = 2;
            public bool allowFreeMove = true;
            public bool allowHorizontalOnly = false;
            public bool allowVerticalOnly = false;

            [Header("Перешкоди (Obstacles)")]
            public bool allowObstacles = false;
            public int obstacleCount = 0;

            [Header("Заморожені блоки (Frozen Pieces)")]
            public bool allowFrozenPieces = false;
            public int frozenPieceCount = 0; // 0 = авто відповідно до складності або бажана к-сть
            public int maxFreezeCount = 2;   // Максимальний лічильник заморозки (на скільки виходів)

            [Header("Ворота")]
            public bool allowUniversalGates = false; // За замовчуванням ВИМКНЕНО!

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
            public int scrambleSteps = 100;
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

        #region Difficulty Curve & Tiers

        public static GenerationSettings GetSettingsForDifficulty(DifficultyTier tier, int levelIndex = 1)
        {
            var s = new GenerationSettings();
            s.levelIndex = Mathf.Max(1, levelIndex);
            s.levelTitle = $"Level {s.levelIndex}";
            s.randomSeed = s.levelIndex * 1337 + 42;
            s.difficultyTier = tier;
            s.allowUniversalGates = false;

            switch (tier)
            {
                case DifficultyTier.VeryEasy:
                    s.gridWidth = 6;
                    s.gridHeight = 6;
                    s.archetype = BoardArchetype.StandardArena;
                    s.pieceCount = 2;
                    s.timeLimitSeconds = 60f;
                    s.allowFreeMove = true;
                    s.allowHorizontalOnly = false;
                    s.allowVerticalOnly = false;
                    s.allowObstacles = false;
                    s.obstacleCount = 0;
                    s.allowFrozenPieces = false;
                    s.frozenPieceCount = 0;
                    s.maxFreezeCount = 0;
                    s.useShape1x2 = true;
                    s.useShape2x2 = true;
                    s.useShape1x1 = s.useShape1x3 = s.useShapeL = s.useShapeL2x2 = s.useShapeT3x2 = s.useShapeCross3x3 = false;
                    s.allowedColors = new List<BlockColorType> { BlockColorType.Red, BlockColorType.Yellow };
                    s.targetMinMoves = 3;
                    s.scrambleSteps = 60;
                    break;

                case DifficultyTier.Easy:
                    s.gridWidth = 6;
                    s.gridHeight = 7;
                    s.archetype = BoardArchetype.StandardArena;
                    s.pieceCount = 3;
                    s.timeLimitSeconds = 75f;
                    s.allowFreeMove = true;
                    s.allowHorizontalOnly = false;
                    s.allowVerticalOnly = false;
                    s.allowObstacles = false;
                    s.obstacleCount = 0;
                    s.allowFrozenPieces = false;
                    s.frozenPieceCount = 0;
                    s.maxFreezeCount = 0;
                    s.useShape1x2 = true;
                    s.useShape2x2 = true;
                    s.useShapeL = true;
                    s.useShape1x1 = s.useShape1x3 = s.useShapeL2x2 = s.useShapeT3x2 = s.useShapeCross3x3 = false;
                    s.allowedColors = new List<BlockColorType> { BlockColorType.Red, BlockColorType.Yellow, BlockColorType.Blue };
                    s.targetMinMoves = 5;
                    s.scrambleSteps = 80;
                    break;

                case DifficultyTier.Medium:
                    s.gridWidth = 8;
                    s.gridHeight = 8;
                    s.archetype = BoardArchetype.CenterPillar;
                    s.pieceCount = 6;
                    s.timeLimitSeconds = 120f;
                    s.allowFreeMove = true;
                    s.allowHorizontalOnly = true;
                    s.allowVerticalOnly = true;
                    s.allowObstacles = true;
                    s.obstacleCount = 2;
                    s.allowFrozenPieces = true;
                    s.frozenPieceCount = 1;
                    s.maxFreezeCount = 1;
                    s.useShape1x2 = true;
                    s.useShape1x3 = true;
                    s.useShape2x2 = true;
                    s.useShapeL = true;
                    s.useShapeL2x2 = true;
                    s.useShapeT3x2 = false;
                    s.useShape1x1 = s.useShapeCross3x3 = false;
                    s.allowedColors = new List<BlockColorType> { BlockColorType.Red, BlockColorType.Yellow, BlockColorType.Blue, BlockColorType.Green, BlockColorType.Purple, BlockColorType.Orange };
                    s.targetMinMoves = 6;
                    s.scrambleSteps = 120;
                    break;

                case DifficultyTier.Hard:
                    s.gridWidth = 9;
                    s.gridHeight = 9;
                    s.archetype = BoardArchetype.ChokepointChambers;
                    s.pieceCount = 8;
                    s.timeLimitSeconds = 180f;
                    s.allowFreeMove = true;
                    s.allowHorizontalOnly = true;
                    s.allowVerticalOnly = true;
                    s.allowObstacles = true;
                    s.obstacleCount = 2;
                    s.allowFrozenPieces = true;
                    s.frozenPieceCount = 1;
                    s.maxFreezeCount = 2;
                    s.useShape1x2 = true;
                    s.useShape1x3 = true;
                    s.useShape2x2 = true;
                    s.useShapeL = true;
                    s.useShapeL2x2 = true;
                    s.useShapeT3x2 = true;
                    s.useShapeCross3x3 = false;
                    s.useShape1x1 = false;
                    s.allowedColors = new List<BlockColorType> { BlockColorType.Red, BlockColorType.Yellow, BlockColorType.Blue, BlockColorType.Green, BlockColorType.Purple, BlockColorType.Pink, BlockColorType.Orange };
                    s.targetMinMoves = 8;
                    s.scrambleSteps = 160;
                    break;

                case DifficultyTier.VeryHard:
                    s.gridWidth = 9;
                    s.gridHeight = 11;
                    s.archetype = BoardArchetype.CorridorMaze;
                    s.pieceCount = 9;
                    s.timeLimitSeconds = 240f;
                    s.allowFreeMove = true;
                    s.allowHorizontalOnly = true;
                    s.allowVerticalOnly = true;
                    s.allowObstacles = true;
                    s.obstacleCount = 3;
                    s.allowFrozenPieces = true;
                    s.frozenPieceCount = 2;
                    s.maxFreezeCount = 2;
                    s.useShape1x2 = true;
                    s.useShape1x3 = true;
                    s.useShape2x2 = true;
                    s.useShapeL = true;
                    s.useShapeL2x2 = true;
                    s.useShapeT3x2 = true;
                    s.useShapeCross3x3 = false;
                    s.useShape1x1 = false;
                    s.allowedColors = new List<BlockColorType> { BlockColorType.Red, BlockColorType.Yellow, BlockColorType.Blue, BlockColorType.Green, BlockColorType.Purple, BlockColorType.Pink, BlockColorType.Cyan, BlockColorType.Orange };
                    s.targetMinMoves = 10;
                    s.scrambleSteps = 200;
                    break;

                case DifficultyTier.Expert:
                    // Процедурна різноманітність геометрії поля та архітектури!
                    int variant = Math.Abs(levelIndex + s.randomSeed) % 6;
                    switch (variant)
                    {
                        case 0:
                            s.gridWidth = 10;
                            s.gridHeight = 11;
                            s.archetype = BoardArchetype.FortressVault;
                            s.pieceCount = 10;
                            break;
                        case 1:
                            s.gridWidth = 9;
                            s.gridHeight = 12;
                            s.archetype = BoardArchetype.ChokepointChambers;
                            s.pieceCount = 10;
                            break;
                        case 2:
                            s.gridWidth = 11;
                            s.gridHeight = 11;
                            s.archetype = BoardArchetype.CenterPillar;
                            s.pieceCount = 11;
                            break;
                        case 3:
                            s.gridWidth = 10;
                            s.gridHeight = 12;
                            s.archetype = BoardArchetype.Alcoves;
                            s.pieceCount = 10;
                            break;
                        case 4:
                            s.gridWidth = 10;
                            s.gridHeight = 13;
                            s.archetype = BoardArchetype.SwitchbackLabyrinth;
                            s.pieceCount = 11;
                            break;
                        default:
                            s.gridWidth = 11;
                            s.gridHeight = 12;
                            s.archetype = BoardArchetype.AsymmetricIslands;
                            s.pieceCount = 11;
                            break;
                    }

                    s.timeLimitSeconds = 360f; // 6 хвилин
                    s.allowFreeMove = true;
                    s.allowHorizontalOnly = true;
                    s.allowVerticalOnly = true;
                    s.allowObstacles = true;
                    s.obstacleCount = 4;
                    s.allowFrozenPieces = true;
                    s.frozenPieceCount = 2;
                    s.maxFreezeCount = 3;
                    s.useShape1x1 = false;
                    s.useShape1x2 = true;
                    s.useShape1x3 = true;
                    s.useShape2x2 = true;
                    s.useShapeL = true;
                    s.useShapeL2x2 = true;
                    s.useShapeT3x2 = true;
                    s.useShapeCross3x3 = false;
                    s.allowedColors = new List<BlockColorType> { BlockColorType.Red, BlockColorType.Yellow, BlockColorType.Blue, BlockColorType.Green, BlockColorType.Purple, BlockColorType.Pink, BlockColorType.Cyan, BlockColorType.Orange };
                    s.targetMinMoves = 14;
                    s.scrambleSteps = 280;
                    break;

                default: // Custom
                    s.difficultyTier = DifficultyTier.Custom;
                    break;
            }

            return s;
        }

        public static GenerationSettings GetCurveSettingsForLevel(int levelIndex)
        {
            DifficultyTier tier = levelIndex switch
            {
                <= 3 => DifficultyTier.VeryEasy,
                <= 6 => DifficultyTier.Easy,
                <= 12 => DifficultyTier.Medium,
                <= 25 => DifficultyTier.Hard,
                <= 45 => DifficultyTier.VeryHard,
                _ => DifficultyTier.Expert
            };

            return GetSettingsForDifficulty(tier, levelIndex);
        }

        public static string GetDifficultyDescription(DifficultyTier tier)
        {
            return tier switch
            {
                DifficultyTier.VeryEasy => "🟢 Дуже легко: 2 деталі, без перешкод (2–3 ходи)",
                DifficultyTier.Easy => "🟡 Легко: 3 деталі, сітка 6x7 (3–5 ходів)",
                DifficultyTier.Medium => "🟠 Середньо: 6 деталей, арена 8x8, слайдери, 2 перешкоди, ❄ 1 заморожений блок (5–7 ходів)",
                DifficultyTier.Hard => "🔴 Складно: 8 деталей, сітка 9x9, перешийок між кімнатами, ❄ 1 заморожений блок (7–9 ходів)",
                DifficultyTier.VeryHard => "🟣 Дуже складно: 9 деталей, сітка 9x11, коридори, 80% слайдери, ❄ 2 заморожені блоки (9–11 ходів)",
                DifficultyTier.Expert => "👑 Експерт [100% Solvable]: 10–11 деталей, унікальні архітектури (Фортеця, Зали, Острів, Серпантин, Кишені), збалансовані слайдери, ❄ 2 крижані блоки (12–16+ ходів, 5–7 хв)",
                _ => "⚙️ Власна складність: Ручні налаштування параметрів"
            };
        }

        #endregion

        #region Generator Engine

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
                result.errorMessage = "Не знайдено жодної форми LegoShapeDefinition під обрані прапорці!";
                return result;
            }

            int targetMoves = settings.targetMinMoves;
            int maxAttempts = 120;
            LevelData bestLevel = null;
            SolverResult bestSolver = default;
            int bestMoveCount = -1;

            int minAcceptableMoves = settings.difficultyTier switch
            {
                DifficultyTier.VeryEasy => 2,
                DifficultyTier.Easy => 3,
                DifficultyTier.Medium => 5,
                DifficultyTier.Hard => 7,
                DifficultyTier.VeryHard => 9,
                DifficultyTier.Expert => 11,
                _ => Mathf.Max(2, (int)(settings.targetMinMoves * 0.6f))
            };

            // Динамічна адаптація порогу ходів, якщо користувач зменшив кількість деталей
            if (settings.pieceCount <= 3)
            {
                minAcceptableMoves = Mathf.Min(minAcceptableMoves, 2);
            }
            else if (settings.pieceCount <= 5)
            {
                minAcceptableMoves = Mathf.Min(minAcceptableMoves, 4);
            }

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

                bool ok = BuildLevelWithReverseSolver(candidate, settings, shapePool, rng, out Vector2Int[] targetGateOrigins, out List<string> reverseSolutionTrace);
                if (!ok)
                {
                    UnityEngine.Object.DestroyImmediate(candidate);
                    continue;
                }

                // 1. Жодна деталь не має виходити у ворота за 1 хід на старті (0 immediate exits)!
                int immediateExits = CountImmediateExits(candidate);
                if (immediateExits > 0)
                {
                    UnityEngine.Object.DestroyImmediate(candidate);
                    continue;
                }

                // 2. BFS / A* валідація (СУВОРА ПЕРЕВІРКА НА 100% РОЗВ'ЯЗУВАНІСТЬ)
                // Якщо увімкнені заморожені блоки, перевіряємо кілька конфігурацій заморозки для цієї розкладки
                int freezeTries = settings.allowFrozenPieces ? 3 : 1;
                bool candidateSolved = false;

                for (int fTry = 0; fTry < freezeTries; fTry++)
                {
                    if (settings.allowFrozenPieces)
                    {
                        ApplyFrozenPieces(candidate, settings, rng);
                    }

                    SolverResult solver = SolveLevelBFS(candidate, 35000, targetGateOrigins);
                    if (solver.isSolvable)
                    {
                        // Якщо BFS знайшов шорткат нижче мінімального порогу — відкидаємо занадто простий рівень
                        if (solver.optimalMoves < minAcceptableMoves)
                        {
                            continue;
                        }

                        if (solver.optimalMoves >= targetMoves)
                        {
                            // Ідеальне попадання у цільову складність!
                            result.levelData = candidate;
                            result.solverResult = solver;
                            result.success = true;
                            return result;
                        }

                        if (solver.optimalMoves > bestMoveCount)
                        {
                            if (bestLevel != null) UnityEngine.Object.DestroyImmediate(bestLevel);
                            bestLevel = candidate;
                            bestSolver = solver;
                            bestMoveCount = solver.optimalMoves;
                            candidateSolved = true;
                            break;
                        }
                    }
                }

                if (!candidateSolved && bestLevel != candidate)
                {
                    UnityEngine.Object.DestroyImmediate(candidate);
                }
            }

            if (bestLevel != null)
            {
                result.levelData = bestLevel;
                result.solverResult = bestSolver;
                result.success = true;
                return result;
            }

            // Гарантований розв'язуваний Fallback із збереженням характеру складності
            result.levelData = GenerateSafeFallbackLevel(settings, shapePool, rng, out result.solverResult);
            result.success = result.levelData != null;
            if (!result.success)
            {
                result.errorMessage = "Не вдалося згенерувати розв'язуваний рівень. Спробуйте збільшити розміри сітки або зменшити кількість деталей.";
            }
            return result;
        }

        private static LevelData GenerateSafeFallbackLevel(
            GenerationSettings settings,
            List<LegoShapeDefinition> shapePool,
            System.Random rng,
            out SolverResult solverResult)
        {
            solverResult = default;

            for (int tryCount = 0; tryCount < 30; tryCount++)
            {
                // Насамперед намагаємося використати точну кількість деталей, задану користувачем
                int fallbackPieceCount = (tryCount < 15)
                    ? Mathf.Clamp(settings.pieceCount, 1, 16)
                    : Mathf.Clamp(settings.pieceCount - 1, 2, 16);

                GenerationSettings fallbackSettings = new GenerationSettings
                {
                    levelIndex = settings.levelIndex,
                    levelTitle = settings.levelTitle,
                    timeLimitSeconds = settings.timeLimitSeconds,
                    movesLimit = settings.movesLimit,
                    gridWidth = Mathf.Max(8, settings.gridWidth),
                    gridHeight = Mathf.Max(8, settings.gridHeight),
                    archetype = settings.archetype,
                    pieceCount = fallbackPieceCount,
                    allowFreeMove = settings.allowFreeMove,
                    allowHorizontalOnly = settings.allowHorizontalOnly,
                    allowVerticalOnly = settings.allowVerticalOnly,
                    allowObstacles = settings.allowObstacles,
                    obstacleCount = Mathf.Max(2, settings.obstacleCount - 1),
                    useShape1x2 = true,
                    useShape2x2 = true,
                    useShape1x3 = true,
                    useShapeL = true,
                    scrambleSteps = 160,
                    targetMinMoves = Mathf.Max(8, settings.targetMinMoves / 2),
                    allowFrozenPieces = settings.allowFrozenPieces && (tryCount < 20),
                    frozenPieceCount = Mathf.Clamp(settings.frozenPieceCount, 1, 2),
                    maxFreezeCount = Mathf.Clamp(settings.maxFreezeCount, 1, 2)
                };

                LevelData fallback = ScriptableObject.CreateInstance<LevelData>();
                fallback.levelIndex = fallbackSettings.levelIndex;
                fallback.levelTitle = fallbackSettings.levelTitle;
                fallback.timeLimitSeconds = fallbackSettings.timeLimitSeconds;
                fallback.movesLimit = fallbackSettings.movesLimit;
                fallback.gridWidth = fallbackSettings.gridWidth;
                fallback.gridHeight = fallbackSettings.gridHeight;
                fallback.EnsureGridCapacity();

                bool ok = BuildLevelWithReverseSolver(fallback, fallbackSettings, shapePool, rng, out Vector2Int[] fallbackTargets, out List<string> fallbackTrace);
                if (ok && CountImmediateExits(fallback) == 0)
                {
                    if (fallbackSettings.allowFrozenPieces)
                    {
                        ApplyFrozenPieces(fallback, fallbackSettings, rng);
                    }
                    var solver = SolveLevelBFS(fallback, 35000, fallbackTargets);
                    if (solver.isSolvable && solver.optimalMoves >= 3)
                    {
                        solverResult = solver;
                        return fallback;
                    }
                }
                UnityEngine.Object.DestroyImmediate(fallback);
            }

            return null;
        }

        private static bool CanFitAt(
            LevelData level,
            LegoShapeDefinition shape,
            int rot,
            Vector2Int origin,
            List<LegoPieceData> pieceList,
            LegoPieceData ignore = null)
        {
            int W = level.gridWidth;
            int H = level.gridHeight;
            var offsets = shape.GetRotatedOffsets(rot);

            for (int k = 0; k < offsets.Count; k++)
            {
                Vector2Int c = origin + offsets[k];
                if (c.x < 1 || c.x > W - 2 || c.y < 1 || c.y > H - 2) return false;

                CellData cell = level.GetCell(c.x, c.y);
                if (cell.cellType != CellType.Walkable) return false;

                for (int pIdx = 0; pIdx < pieceList.Count; pIdx++)
                {
                    var other = pieceList[pIdx];
                    if (other == ignore) continue;
                    var otherOffsets = other.shape.GetRotatedOffsets(other.rotationSteps);
                    for (int o = 0; o < otherOffsets.Count; o++)
                    {
                        if (c == other.originPosition + otherOffsets[o]) return false;
                    }
                }
            }

            return true;
        }

        private static bool CanPlaceSolvedPiece(
            LevelData level,
            LegoShapeDefinition shape,
            int rot,
            Vector2Int origin,
            List<LegoPieceData> pieceList)
        {
            return CanFitAt(level, shape, rot, origin, pieceList, null);
        }

        private struct GeneratorStepTrace
        {
            public int pieceIdx;
            public Vector2Int fromPos;
            public Vector2Int toPos;
            public bool isEntrance;
        }

        private static List<string> BuildReverseSolutionTrace(LevelData level, List<GeneratorStepTrace> trace)
        {
            var result = new List<string>();
            if (trace == null || trace.Count == 0 || level.pieces == null) return result;

            int n = level.pieces.Count;
            for (int s = trace.Count - 1; s >= 0; s--)
            {
                var t = trace[s];
                if (t.pieceIdx < 0 || t.pieceIdx >= n) continue;
                var p = level.pieces[t.pieceIdx];

                if (t.isEntrance)
                {
                    result.Add($"{p.pieceId} ({p.colorType}) вихід через ворота");
                }
                else
                {
                    Vector2Int moveDelta = t.fromPos - t.toPos;
                    if (moveDelta != Vector2Int.zero)
                    {
                        int steps = Mathf.Max(Mathf.Abs(moveDelta.x), Mathf.Abs(moveDelta.y));
                        string dirName = moveDelta.x > 0 ? "вправо" : (moveDelta.x < 0 ? "вліво" : (moveDelta.y > 0 ? "вгору" : "вниз"));
                        result.Add($"{p.pieceId} ({p.colorType}) слайд {dirName} на {steps} крок(ів)");
                    }
                }
            }

            var condensed = new List<string>();
            for (int i = 0; i < result.Count; i++)
            {
                if (condensed.Count > 0 && condensed[condensed.Count - 1] == result[i])
                    continue;
                condensed.Add(result[i]);
            }
            return condensed;
        }

        private static void ExecuteMiniScramble(
            LevelData level,
            List<LegoPieceData> pieces,
            int steps,
            System.Random rng,
            List<GeneratorStepTrace> trace = null)
        {
            int W = level.gridWidth;
            int H = level.gridHeight;
            int n = pieces.Count;
            if (n == 0) return;
            Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

            for (int s = 0; s < steps; s++)
            {
                int idx = rng.Next(n);
                var p = pieces[idx];

                var allowed = new List<Vector2Int>();
                if (p.moveRestriction == MoveRestriction.HorizontalOnly)
                {
                    allowed.Add(Vector2Int.left);
                    allowed.Add(Vector2Int.right);
                }
                else if (p.moveRestriction == MoveRestriction.VerticalOnly)
                {
                    allowed.Add(Vector2Int.up);
                    allowed.Add(Vector2Int.down);
                }
                else
                {
                    allowed.AddRange(dirs);
                }

                ShuffleList(allowed, rng);

                foreach (var dir in allowed)
                {
                    int maxD = (dir == Vector2Int.left || dir == Vector2Int.right) ? (W - 3) : (H - 3);
                    var valid = new List<Vector2Int>();
                    for (int d = 1; d <= maxD; d++)
                    {
                        Vector2Int cand = p.originPosition + dir * d;
                        if (CanFitAt(level, p.shape, p.rotationSteps, cand, pieces, p))
                            valid.Add(cand);
                        else
                            break;
                    }

                    if (valid.Count > 0)
                    {
                        Vector2Int chosen = valid[rng.Next(valid.Count)];
                        if (trace != null)
                        {
                            trace.Add(new GeneratorStepTrace { pieceIdx = idx, fromPos = p.originPosition, toPos = chosen, isEntrance = false });
                        }
                        p.originPosition = chosen;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Призначає механіку заморожених блоків (Frozen Pieces) на деталі рівня.
        /// Гарантує, що принаймні 2 деталі залишаються вільними для стартового ходу.
        /// </summary>
        private static void ApplyFrozenPieces(LevelData level, GenerationSettings settings, System.Random rng)
        {
            if (level == null || level.pieces == null || level.pieces.Count <= 2) return;

            // Скидаємо заморозку для всіх деталей
            for (int i = 0; i < level.pieces.Count; i++)
            {
                level.pieces[i].freezeCount = 0;
            }

            if (!settings.allowFrozenPieces) return;

            int totalPieces = level.pieces.Count;
            int maxCanFreeze = Mathf.Max(1, totalPieces - 2);
            int desiredFreeze = settings.frozenPieceCount > 0 ? settings.frozenPieceCount : 1;
            int piecesToFreeze = Mathf.Clamp(desiredFreeze, 1, maxCanFreeze);

            // Кандидати: обираємо серед деталей з індексами 1..(totalPieces - 1)
            // (Ніколи не заморожуємо деталь 0 для збереження початкового розв'язку)
            List<int> candidateIndices = new List<int>();
            for (int i = 1; i < totalPieces; i++)
            {
                candidateIndices.Add(i);
            }
            ShuffleList(candidateIndices, rng);

            int maxFreezeLimit = Mathf.Clamp(settings.maxFreezeCount, 1, totalPieces - 2);
            int frozenAssigned = 0;

            for (int k = 0; k < candidateIndices.Count && frozenAssigned < piecesToFreeze; k++)
            {
                int pIdx = candidateIndices[k];
                var piece = level.pieces[pIdx];

                int count;
                if (frozenAssigned == 0)
                {
                    count = (maxFreezeLimit > 1) ? rng.Next(1, maxFreezeLimit + 1) : 1;
                }
                else
                {
                    count = (maxFreezeLimit >= 2) ? rng.Next(2, maxFreezeLimit + 1) : 1;
                }

                piece.freezeCount = count;
                frozenAssigned++;
            }
        }

        private static bool BuildLevelWithReverseSolver(
            LevelData level,
            GenerationSettings settings,
            List<LegoShapeDefinition> shapePool,
            System.Random rng,
            out Vector2Int[] targetGateOrigins,
            out List<string> reverseSolutionTrace)
        {
            reverseSolutionTrace = new List<string>();
            var stepTrace = new List<GeneratorStepTrace>();
            int W = settings.gridWidth;
            int H = settings.gridHeight;
            int count = Mathf.Clamp(settings.pieceCount, 1, 16);
            targetGateOrigins = new Vector2Int[count];

            // КРОК 1: Створення периметра з ПРАВИЛЬНИМИ орієнтаціями Inward (до центру поля)
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    Vector2Int pos = new Vector2Int(x, y);

                    // 4 КУТКИ (QuarterObstacle):
                    if (x == 0 && y == 0) // Bottom-Left: зміщення (+X, +Z) => ExitDirection.Up
                    {
                        level.SetCell(x, y, new CellData(pos, CellType.QuarterObstacle) { exitDirection = ExitDirection.Up });
                    }
                    else if (x == W - 1 && y == 0) // Bottom-Right: зміщення (-X, +Z) => ExitDirection.Left
                    {
                        level.SetCell(x, y, new CellData(pos, CellType.QuarterObstacle) { exitDirection = ExitDirection.Left });
                    }
                    else if (x == 0 && y == H - 1) // Top-Left: зміщення (+X, -Z) => ExitDirection.Right
                    {
                        level.SetCell(x, y, new CellData(pos, CellType.QuarterObstacle) { exitDirection = ExitDirection.Right });
                    }
                    else if (x == W - 1 && y == H - 1) // Top-Right: зміщення (-X, -Z) => ExitDirection.Down
                    {
                        level.SetCell(x, y, new CellData(pos, CellType.QuarterObstacle) { exitDirection = ExitDirection.Down });
                    }
                    // 4 СТІНИ (HalfObstacle):
                    else if (y == 0) // Нижня стіна: зміщення вгору до поля (+Z) => ExitDirection.Up
                    {
                        level.SetCell(x, y, new CellData(pos, CellType.HalfObstacle) { exitDirection = ExitDirection.Up });
                    }
                    else if (y == H - 1) // Верхня стіна: зміщення вниз до поля (-Z) => ExitDirection.Down
                    {
                        level.SetCell(x, y, new CellData(pos, CellType.HalfObstacle) { exitDirection = ExitDirection.Down });
                    }
                    else if (x == 0) // Ліва стіна: зміщення вправо до поля (+X) => ExitDirection.Right
                    {
                        level.SetCell(x, y, new CellData(pos, CellType.HalfObstacle) { exitDirection = ExitDirection.Right });
                    }
                    else if (x == W - 1) // Права стіна: зміщення вліво до поля (-X) => ExitDirection.Left
                    {
                        level.SetCell(x, y, new CellData(pos, CellType.HalfObstacle) { exitDirection = ExitDirection.Left });
                    }
                    // ІГРОВЕ ПОЛЕ:
                    else
                    {
                        level.SetCell(x, y, new CellData(pos, CellType.Walkable));
                    }
                }
            }

            // КРОК 2: Застосування архетипу поля та додаткових перешкод (суворо у внутрішній зоні [2..W-3, 2..H-3])
            ApplyArchetypeToGrid(level, settings.archetype, rng);
            if (settings.allowObstacles && settings.obstacleCount > 0)
            {
                int currentObs = CountObstacles(level);
                int needed = Mathf.Max(0, settings.obstacleCount - currentObs);
                if (needed > 0)
                {
                    PlaceAdditionalObstacles(level, needed, rng);
                }
            }

            // КРОК 3: Підготовка деталей та обмежень руху (Sliders)
            List<MoveRestriction> restrictions = new List<MoveRestriction>();

            if (settings.difficultyTier == DifficultyTier.Expert && settings.allowHorizontalOnly && settings.allowVerticalOnly)
            {
                // Для Експерта: 60% слайдери (3↔, 3↕) + 40% вільні деталі (Omni ✦) як пересувні ключі
                int freeCount = Mathf.Max(3, count * 4 / 10);
                for (int i = 0; i < count; i++)
                {
                    if (i < freeCount && settings.allowFreeMove)
                        restrictions.Add(MoveRestriction.Free);
                    else
                        restrictions.Add(i % 2 == 0 ? MoveRestriction.HorizontalOnly : MoveRestriction.VerticalOnly);
                }
            }
            else if (settings.difficultyTier == DifficultyTier.VeryHard && settings.allowHorizontalOnly && settings.allowVerticalOnly)
            {
                // Для Дуже складно: 90% слайдери (максимум 1 Free або всі слайдери)
                for (int i = 0; i < count; i++)
                {
                    if (i == 0 && settings.allowFreeMove)
                        restrictions.Add(MoveRestriction.Free);
                    else
                        restrictions.Add(i % 2 == 0 ? MoveRestriction.HorizontalOnly : MoveRestriction.VerticalOnly);
                }
            }
            else if (settings.difficultyTier == DifficultyTier.Hard && settings.allowHorizontalOnly && settings.allowVerticalOnly)
            {
                // Для Складно: 75% слайдери
                for (int i = 0; i < count; i++)
                {
                    if (i < 2 && settings.allowFreeMove)
                        restrictions.Add(MoveRestriction.Free);
                    else
                        restrictions.Add(i % 2 == 0 ? MoveRestriction.HorizontalOnly : MoveRestriction.VerticalOnly);
                }
            }
            else if (settings.difficultyTier == DifficultyTier.Medium && settings.allowHorizontalOnly && settings.allowVerticalOnly)
            {
                // Для Середньо: 55% слайдери
                for (int i = 0; i < count; i++)
                {
                    if (i < 3 && settings.allowFreeMove)
                        restrictions.Add(MoveRestriction.Free);
                    else
                        restrictions.Add(i % 2 == 0 ? MoveRestriction.HorizontalOnly : MoveRestriction.VerticalOnly);
                }
            }
            else if (settings.difficultyTier == DifficultyTier.Custom)
            {
                // Для власної складності: чесний вибір серед увімкнених користувачем типів обмежень
                var allowedRestrictions = new List<MoveRestriction>();
                if (settings.allowFreeMove) allowedRestrictions.Add(MoveRestriction.Free);
                if (settings.allowHorizontalOnly) allowedRestrictions.Add(MoveRestriction.HorizontalOnly);
                if (settings.allowVerticalOnly) allowedRestrictions.Add(MoveRestriction.VerticalOnly);
                if (allowedRestrictions.Count == 0) allowedRestrictions.Add(MoveRestriction.Free);

                for (int i = 0; i < count; i++)
                {
                    restrictions.Add(allowedRestrictions[rng.Next(allowedRestrictions.Count)]);
                }
            }
            else
            {
                // Загальний fallback для нестандартних комбінацій
                double sliderProbability = !settings.allowFreeMove ? 1.0 : 0.6;
                for (int i = 0; i < count; i++)
                {
                    if (settings.allowFreeMove && rng.NextDouble() >= sliderProbability)
                    {
                        restrictions.Add(MoveRestriction.Free);
                    }
                    else
                    {
                        if (settings.allowHorizontalOnly && settings.allowVerticalOnly)
                            restrictions.Add(i % 2 == 0 ? MoveRestriction.HorizontalOnly : MoveRestriction.VerticalOnly);
                        else if (settings.allowHorizontalOnly)
                            restrictions.Add(MoveRestriction.HorizontalOnly);
                        else if (settings.allowVerticalOnly)
                            restrictions.Add(MoveRestriction.VerticalOnly);
                        else
                            restrictions.Add(MoveRestriction.Free);
                    }
                }
            }

            List<BlockColorType> colors = (settings.allowedColors != null && settings.allowedColors.Count > 0)
                ? new List<BlockColorType>(settings.allowedColors)
                : new List<BlockColorType> { BlockColorType.Red, BlockColorType.Yellow, BlockColorType.Blue, BlockColorType.Green, BlockColorType.Purple, BlockColorType.Orange };

            // КРОК 4: Послідовна реверсивна вставка деталей (Sequential Reverse Insertion)
            // Кожна деталь входить через свої ворота на периметрі, зсувається вглиб і робить міні-скрембл
            Dictionary<ExitDirection, HashSet<int>> borderUsed = new Dictionary<ExitDirection, HashSet<int>>
            {
                { ExitDirection.Down, new HashSet<int>() },
                { ExitDirection.Up, new HashSet<int>() },
                { ExitDirection.Left, new HashSet<int>() },
                { ExitDirection.Right, new HashSet<int>() }
            };

            List<LegoPieceData> pieceList = new List<LegoPieceData>();
            ExitDirection[] allSides = { ExitDirection.Down, ExitDirection.Up, ExitDirection.Left, ExitDirection.Right };

            for (int i = 0; i < count; i++)
            {
                var restriction = restrictions[i];
                bool placed = false;

                for (int shapeTry = 0; shapeTry < 60 && !placed; shapeTry++)
                {
                    var shape = shapePool[rng.Next(shapePool.Count)];
                    int rot = rng.Next(4);
                    Vector2Int bounds = shape.GetRotatedBounds(rot);

                    List<ExitDirection> candidateSides = new List<ExitDirection>();
                    if (restriction == MoveRestriction.VerticalOnly)
                    {
                        candidateSides.Add(ExitDirection.Down);
                        candidateSides.Add(ExitDirection.Up);
                    }
                    else if (restriction == MoveRestriction.HorizontalOnly)
                    {
                        candidateSides.Add(ExitDirection.Left);
                        candidateSides.Add(ExitDirection.Right);
                    }
                    else
                    {
                        candidateSides.AddRange(allSides);
                    }

                    ShuffleList(candidateSides, rng);

                    foreach (var side in candidateSides)
                    {
                        bool isHorizontalGate = (side == ExitDirection.Down || side == ExitDirection.Up);
                        int gateLen = isHorizontalGate ? bounds.x : bounds.y;
                        int borderLength = isHorizontalGate ? W : H;
                        int maxStart = (borderLength - 1) - gateLen;
                        if (maxStart < 1) continue;

                        List<int> validGateStarts = new List<int>();
                        for (int g = 1; g <= maxStart; g++)
                        {
                            bool borderOverlap = false;
                            for (int gx = g; gx < g + gateLen; gx++)
                            {
                                if (borderUsed[side].Contains(gx))
                                {
                                    borderOverlap = true;
                                    break;
                                }
                            }
                            if (!borderOverlap)
                            {
                                // Перевірка Runway: чи вільний прохід під деталь від воріт
                                int inwardDepth = isHorizontalGate ? bounds.y : bounds.x;
                                bool runwayBlocked = false;
                                for (int gx = g; gx < g + gateLen && !runwayBlocked; gx++)
                                {
                                    for (int step = 1; step <= inwardDepth + 1 && !runwayBlocked; step++)
                                    {
                                        Vector2Int checkPos = side switch
                                        {
                                            ExitDirection.Down => new Vector2Int(gx, step),
                                            ExitDirection.Up => new Vector2Int(gx, H - 1 - step),
                                            ExitDirection.Left => new Vector2Int(step, gx),
                                            _ => new Vector2Int(W - 1 - step, gx)
                                        };
                                        if (checkPos.x >= 0 && checkPos.x < W && checkPos.y >= 0 && checkPos.y < H)
                                        {
                                            if (level.GetCell(checkPos.x, checkPos.y).cellType == CellType.Obstacle)
                                            {
                                                runwayBlocked = true;
                                            }
                                        }
                                    }
                                }
                                if (!runwayBlocked) validGateStarts.Add(g);
                            }
                        }

                        if (validGateStarts.Count == 0) continue;
                        ShuffleList(validGateStarts, rng);

                        foreach (int g in validGateStarts)
                        {
                            Vector2Int entranceOrigin = side switch
                            {
                                ExitDirection.Down => new Vector2Int(g, 1),
                                ExitDirection.Up => new Vector2Int(g, H - 1 - bounds.y),
                                ExitDirection.Left => new Vector2Int(1, g),
                                _ => new Vector2Int(W - 1 - bounds.x, g)
                            };

                            if (CanFitAt(level, shape, rot, entranceOrigin, pieceList))
                            {
                                BlockColorType color = colors[i % colors.Count];
                                BlockColorType gateColor = (settings.allowUniversalGates && rng.NextDouble() < 0.15)
                                    ? BlockColorType.Universal
                                    : color;

                                for (int gx = g; gx < g + gateLen; gx++)
                                {
                                    borderUsed[side].Add(gx);
                                    Vector2Int gatePos = side switch
                                    {
                                        ExitDirection.Down => new Vector2Int(gx, 0),
                                        ExitDirection.Up => new Vector2Int(gx, H - 1),
                                        ExitDirection.Left => new Vector2Int(0, gx),
                                        _ => new Vector2Int(W - 1, gx)
                                    };

                                    int alignment = side switch
                                    {
                                        ExitDirection.Down => 0, // Top half (▀) -> offset +Z
                                        ExitDirection.Up => 1,   // Bottom half (▄) -> offset -Z
                                        ExitDirection.Right => 2,// Left half (▌) -> offset -X
                                        ExitDirection.Left => 3, // Right half (▐) -> offset +X
                                        _ => 0
                                    };

                                    var gateCell = new CellData(gatePos, CellType.HalfExitGate)
                                    {
                                        exitDirection = side,
                                        gateColorType = gateColor,
                                        gateAlignment = alignment
                                    };
                                    level.SetCell(gatePos.x, gatePos.y, gateCell);
                                }

                                var newPiece = new LegoPieceData
                                {
                                    pieceId = $"Piece_{i + 1}",
                                    shape = shape,
                                    rotationSteps = rot,
                                    originPosition = entranceOrigin,
                                    colorType = color,
                                    moveRestriction = restriction,
                                    isRequiredForWin = true
                                };
                                pieceList.Add(newPiece);
                                targetGateOrigins[i] = entranceOrigin;
                                stepTrace.Add(new GeneratorStepTrace { pieceIdx = i, fromPos = entranceOrigin, toPos = entranceOrigin, isEntrance = true });

                                // Зсуваємо щойно додану деталь вглиб поля від воріт
                                Vector2Int inwardDir = side switch
                                {
                                    ExitDirection.Down => Vector2Int.up,
                                    ExitDirection.Up => Vector2Int.down,
                                    ExitDirection.Left => Vector2Int.right,
                                    _ => Vector2Int.left
                                };

                                for (int d = 1; d <= 6; d++)
                                {
                                    Vector2Int testPos = newPiece.originPosition + inwardDir;
                                    if (CanFitAt(level, newPiece.shape, newPiece.rotationSteps, testPos, pieceList, newPiece))
                                    {
                                        stepTrace.Add(new GeneratorStepTrace { pieceIdx = i, fromPos = newPiece.originPosition, toPos = testPos, isEntrance = false });
                                        newPiece.originPosition = testPos;
                                    }
                                    else break;
                                }

                                // Міні-скрембл для звільнення проходу під наступну деталь
                                ExecuteMiniScramble(level, pieceList, 25, rng, stepTrace);

                                placed = true;
                                break;
                            }
                        }

                        if (placed) break;
                    }
                }

                // Якщо деталь не вдалося розмістити - перезапуск спроби (ніяких зменшень кількості деталей!)
                if (!placed)
                {
                    return false;
                }
            }

            level.pieces = pieceList;

            // КРОК 5: Глибокий Scramble та усунення негайних виходів
            bool scrambleOk = ExecuteDeepReverseScramble(level, settings.scrambleSteps, rng, targetGateOrigins, stepTrace);
            if (!scrambleOk) return false;

            reverseSolutionTrace = BuildReverseSolutionTrace(level, stepTrace);
            return true;
        }

        private static void ApplyArchetypeToGrid(LevelData level, BoardArchetype archetype, System.Random rng)
        {
            int W = level.gridWidth;
            int H = level.gridHeight;

            void SetObs(int x, int y)
            {
                if (x >= 2 && x <= W - 3 && y >= 2 && y <= H - 3)
                {
                    level.SetCell(x, y, new CellData(new Vector2Int(x, y), CellType.Obstacle));
                }
            }

            switch (archetype)
            {
                case BoardArchetype.CenterPillar:
                {
                    // Центральний моноліт (Donut/Ring): 2x2 або 2x3 у центрі
                    int cx = W / 2;
                    int cy = H / 2;
                    SetObs(cx - 1, cy);
                    SetObs(cx, cy);
                    SetObs(cx - 1, cy - 1);
                    SetObs(cx, cy - 1);
                    if (H >= 11)
                    {
                        SetObs(cx - 1, cy + 1);
                        SetObs(cx, cy + 1);
                    }
                    break;
                }

                case BoardArchetype.ChokepointChambers:
                {
                    // Дві зали з вузьким перешийком (Hourglass)
                    int midY = H / 2;
                    int door1 = Mathf.Clamp(W / 3, 2, W - 3);
                    int door2 = Mathf.Clamp(W - 1 - (W / 3), 2, W - 3);
                    for (int x = 2; x <= W - 3; x++)
                    {
                        if (x != door1 && x != door2)
                        {
                            SetObs(x, midY);
                        }
                    }
                    break;
                }

                case BoardArchetype.CorridorMaze:
                {
                    // S-подібний лабіринт: дві перегородки зі зміщеними проходами
                    int y1 = Mathf.Clamp(H / 3, 2, H - 3);
                    int y2 = Mathf.Clamp(H - 1 - (H / 3), 2, H - 3);
                    for (int x = 2; x <= W - 4; x++) SetObs(x, y1);
                    for (int x = 3; x <= W - 3; x++) SetObs(x, y2);
                    break;
                }

                case BoardArchetype.Alcoves:
                {
                    // Паркувальні кишені / тупики для роз'їзду (Klotski-style)
                    int midX = W / 2;
                    int midY = H / 2;
                    SetObs(3, 3);
                    SetObs(3, 4);
                    SetObs(W - 4, H - 4);
                    SetObs(W - 4, H - 5);
                    if (W >= 10 && H >= 11)
                    {
                        SetObs(midX, midY);
                    }
                    break;
                }

                case BoardArchetype.FortressVault:
                {
                    // Фортеця з кутовими бастіонами (хрестоподібна арена)
                    SetObs(2, 2); SetObs(3, 2); SetObs(2, 3);
                    SetObs(W - 3, 2); SetObs(W - 4, 2); SetObs(W - 3, 3);
                    SetObs(2, H - 3); SetObs(3, H - 3); SetObs(2, H - 4);
                    SetObs(W - 3, H - 3); SetObs(W - 4, H - 3); SetObs(W - 3, H - 4);
                    break;
                }

                case BoardArchetype.SwitchbackLabyrinth:
                {
                    // Серпантин з кутовими бар'єрами
                    int midX = W / 2;
                    int midY = H / 2;
                    SetObs(2, midY);
                    SetObs(3, midY);
                    SetObs(W - 3, midY);
                    SetObs(W - 4, midY);
                    SetObs(midX, 2);
                    SetObs(midX, H - 3);
                    break;
                }

                case BoardArchetype.AsymmetricIslands:
                {
                    // Асиметричні процедурні острови
                    int rx1 = rng.Next(2, Mathf.Max(3, W / 2 - 1));
                    int ry1 = rng.Next(2, Mathf.Max(3, H / 2 - 1));
                    int rx2 = rng.Next(Mathf.Min(W - 4, W / 2 + 1), W - 2);
                    int ry2 = rng.Next(Mathf.Min(H - 4, H / 2 + 1), H - 2);
                    SetObs(rx1, ry1);
                    SetObs(rx1 + 1, ry1);
                    SetObs(rx2, ry2);
                    SetObs(rx2, ry2 - 1);
                    SetObs(W / 2, H / 2);
                    break;
                }

                default:
                    // StandardArena: класична відкрита арена з парою розсікаючих блоків
                    SetObs(W / 3, H / 3);
                    SetObs(W - 1 - (W / 3), H - 1 - (H / 3));
                    break;
            }
        }

        private static bool ExecuteDeepReverseScramble(
            LevelData level,
            int totalSteps,
            System.Random rng,
            Vector2Int[] targetGateOrigins,
            List<GeneratorStepTrace> trace = null)
        {
            int W = level.gridWidth;
            int H = level.gridHeight;
            int piecesCount = level.pieces.Count;
            if (piecesCount == 0) return false;

            Vector2Int[] dirs = new Vector2Int[]
            {
                Vector2Int.up,
                Vector2Int.down,
                Vector2Int.left,
                Vector2Int.right
            };

            for (int step = 0; step < totalSteps; step++)
            {
                int pIndex = rng.Next(piecesCount);
                var p = level.pieces[pIndex];

                Vector2Int gateOrigin = (targetGateOrigins != null && pIndex < targetGateOrigins.Length)
                    ? targetGateOrigins[pIndex]
                    : p.originPosition;

                Vector2Int awayFromGate = Vector2Int.zero;
                if (gateOrigin.y == 1) awayFromGate = Vector2Int.up;
                else if (gateOrigin.y >= H - 3) awayFromGate = Vector2Int.down;
                else if (gateOrigin.x == 1) awayFromGate = Vector2Int.right;
                else if (gateOrigin.x >= W - 3) awayFromGate = Vector2Int.left;

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

                if (awayFromGate != Vector2Int.zero && validDirs.Contains(awayFromGate) && rng.NextDouble() < 0.6)
                {
                    validDirs.Remove(awayFromGate);
                    validDirs.Insert(0, awayFromGate);
                }
                else
                {
                    ShuffleList(validDirs, rng);
                }

                foreach (var dir in validDirs)
                {
                    int maxDistance = (dir == Vector2Int.left || dir == Vector2Int.right) ? (W - 3) : (H - 3);
                    List<Vector2Int> validSteps = new List<Vector2Int>();

                    for (int dist = 1; dist <= maxDistance; dist++)
                    {
                        Vector2Int candidateOrigin = p.originPosition + dir * dist;
                        if (CanPieceFitAt(level, p, candidateOrigin))
                        {
                            validSteps.Add(candidateOrigin);
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (validSteps.Count > 0)
                    {
                        Vector2Int chosen = (validSteps.Count >= 2 && rng.NextDouble() < 0.7)
                            ? validSteps[validSteps.Count - 1]
                            : validSteps[rng.Next(validSteps.Count)];

                        if (trace != null)
                        {
                            trace.Add(new GeneratorStepTrace { pieceIdx = pIndex, fromPos = p.originPosition, toPos = chosen, isEntrance = false });
                        }
                        p.originPosition = chosen;
                        break;
                    }
                }
            }

            // Відсування деталей від воріт, щоб жодна деталь не мала виходу за 1 хід
            for (int extra = 0; extra < 60 && CountImmediateExits(level) > 0; extra++)
            {
                for (int i = 0; i < piecesCount; i++)
                {
                    var p = level.pieces[i];
                    if (!CanPieceExitAnywhereImmediate(level, p)) continue;

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

                    Vector2Int gateOrigin = (targetGateOrigins != null && i < targetGateOrigins.Length)
                        ? targetGateOrigins[i]
                        : p.originPosition;
                    Vector2Int inward = Vector2Int.zero;
                    if (gateOrigin.y == 1) inward = Vector2Int.up;
                    else if (gateOrigin.y >= H - 3) inward = Vector2Int.down;
                    else if (gateOrigin.x == 1) inward = Vector2Int.right;
                    else if (gateOrigin.x >= W - 3) inward = Vector2Int.left;

                    if (inward != Vector2Int.zero && validDirs.Contains(inward))
                    {
                        validDirs.Remove(inward);
                        validDirs.Insert(0, inward);
                    }
                    else
                    {
                        ShuffleList(validDirs, rng);
                    }

                    bool pushed = false;
                    foreach (var dir in validDirs)
                    {
                        int maxDistance = (dir == Vector2Int.left || dir == Vector2Int.right) ? (W - 3) : (H - 3);
                        for (int dist = maxDistance; dist >= 1; dist--)
                        {
                            Vector2Int cand = p.originPosition + dir * dist;
                            if (CanPieceFitAt(level, p, cand))
                            {
                                Vector2Int old = p.originPosition;
                                p.originPosition = cand;
                                if (!CanPieceExitAnywhereImmediate(level, p))
                                {
                                    if (trace != null)
                                    {
                                        trace.Add(new GeneratorStepTrace { pieceIdx = i, fromPos = old, toPos = cand, isEntrance = false });
                                    }
                                    pushed = true;
                                    break;
                                }
                                p.originPosition = old;
                            }
                        }
                        if (pushed) break;
                    }
                }

                if (CountImmediateExits(level) > 0)
                {
                    ExecuteMiniScramble(level, level.pieces, 12, rng, trace);
                }
            }

            return CountImmediateExits(level) == 0;
        }

        private static bool CanPieceExitAnywhereImmediate(LevelData level, LegoPieceData piece, Vector2Int origin)
        {
            Vector2Int[] dirs = new Vector2Int[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            foreach (var dir in dirs)
            {
                if (piece.moveRestriction == MoveRestriction.HorizontalOnly && (dir == Vector2Int.up || dir == Vector2Int.down))
                    continue;
                if (piece.moveRestriction == MoveRestriction.VerticalOnly && (dir == Vector2Int.left || dir == Vector2Int.right))
                    continue;

                if (CanExitImmediately(level, piece, origin, dir))
                    return true;
            }
            return false;
        }

        private static bool CanPieceExitAnywhereImmediate(LevelData level, LegoPieceData piece)
        {
            return CanPieceExitAnywhereImmediate(level, piece, piece.originPosition);
        }

        private static int CountImmediateExits(LevelData level)
        {
            int count = 0;
            if (level.pieces == null) return 0;
            foreach (var p in level.pieces)
            {
                if (CanPieceExitAnywhereImmediate(level, p))
                    count++;
            }
            return count;
        }

        private static bool CanExitImmediately(LevelData level, LegoPieceData piece, Vector2Int origin, Vector2Int dir)
        {
            if (piece == null || piece.shape == null) return false;

            int W = level.gridWidth;
            int H = level.gridHeight;
            var offsets = piece.shape.GetRotatedOffsets(piece.rotationSteps);

            ExitDirection expectedExitDir = (dir == Vector2Int.up) ? ExitDirection.Up
                : (dir == Vector2Int.down) ? ExitDirection.Down
                : (dir == Vector2Int.left) ? ExitDirection.Left
                : ExitDirection.Right;

            HashSet<int> borderCoords = new HashSet<int>();
            int extremeCoord = (dir == Vector2Int.up || dir == Vector2Int.right) ? int.MinValue : int.MaxValue;

            for (int k = 0; k < offsets.Count; k++)
            {
                Vector2Int cell = origin + offsets[k];
                if (dir == Vector2Int.down)
                {
                    if (cell.y < extremeCoord) extremeCoord = cell.y;
                    borderCoords.Add(cell.x);
                }
                else if (dir == Vector2Int.up)
                {
                    if (cell.y > extremeCoord) extremeCoord = cell.y;
                    borderCoords.Add(cell.x);
                }
                else if (dir == Vector2Int.left)
                {
                    if (cell.x < extremeCoord) extremeCoord = cell.x;
                    borderCoords.Add(cell.y);
                }
                else if (dir == Vector2Int.right)
                {
                    if (cell.x > extremeCoord) extremeCoord = cell.x;
                    borderCoords.Add(cell.y);
                }
            }

            if (dir == Vector2Int.down && extremeCoord != 1) return false;
            if (dir == Vector2Int.up && extremeCoord != H - 2) return false;
            if (dir == Vector2Int.left && extremeCoord != 1) return false;
            if (dir == Vector2Int.right && extremeCoord != W - 2) return false;

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

        private static bool CanPieceFitAt(LevelData level, LegoPieceData piece, Vector2Int newOrigin)
        {
            int W = level.gridWidth;
            int H = level.gridHeight;
            var offsets = piece.shape.GetRotatedOffsets(piece.rotationSteps);

            for (int i = 0; i < offsets.Count; i++)
            {
                Vector2Int cell = newOrigin + offsets[i];
                if (cell.x < 1 || cell.x > W - 2 || cell.y < 1 || cell.y > H - 2)
                    return false;

                CellData cellData = level.GetCell(cell.x, cell.y);
                if (cellData.cellType != CellType.Walkable)
                    return false;

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

        private static int CountObstacles(LevelData level)
        {
            if (level == null) return 0;
            int count = 0;
            for (int y = 0; y < level.gridHeight; y++)
            {
                for (int x = 0; x < level.gridWidth; x++)
                {
                    if (level.GetCell(x, y).cellType == CellType.Obstacle)
                        count++;
                }
            }
            return count;
        }

        private static void PlaceAdditionalObstacles(LevelData level, int count, System.Random rng)
        {
            int W = level.gridWidth;
            int H = level.gridHeight;

            List<Vector2Int> candidates = new List<Vector2Int>();
            for (int y = 2; y <= H - 3; y++)
            {
                for (int x = 2; x <= W - 3; x++)
                {
                    if (level.GetCell(x, y).cellType == CellType.Walkable)
                    {
                        bool hasAdjacentObstacle =
                            level.GetCell(x + 1, y).cellType == CellType.Obstacle ||
                            level.GetCell(x - 1, y).cellType == CellType.Obstacle ||
                            level.GetCell(x, y + 1).cellType == CellType.Obstacle ||
                            level.GetCell(x, y - 1).cellType == CellType.Obstacle;

                        if (!hasAdjacentObstacle)
                        {
                            candidates.Add(new Vector2Int(x, y));
                        }
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

        #region Fast 64-Bit BFS / A* Solver

        public class FastMinHeap
        {
            private struct Element
            {
                public int stateIdx;
                public int priority;
            }
            private Element[] heap;
            public int Count { get; private set; }

            public FastMinHeap(int capacity)
            {
                heap = new Element[Mathf.Max(32, capacity)];
                Count = 0;
            }

            public void Enqueue(int stateIdx, int priority)
            {
                if (Count == heap.Length)
                {
                    Array.Resize(ref heap, heap.Length * 2);
                }
                int i = Count++;
                heap[i] = new Element { stateIdx = stateIdx, priority = priority };
                while (i > 0)
                {
                    int p = (i - 1) >> 1;
                    if (heap[i].priority < heap[p].priority)
                    {
                        var tmp = heap[i]; heap[i] = heap[p]; heap[p] = tmp;
                        i = p;
                    }
                    else break;
                }
            }

            public int Dequeue()
            {
                int result = heap[0].stateIdx;
                Count--;
                if (Count > 0)
                {
                    heap[0] = heap[Count];
                    int i = 0;
                    while (true)
                    {
                        int left = (i << 1) + 1;
                        int right = left + 1;
                        int smallest = i;

                        if (left < Count && heap[left].priority < heap[smallest].priority)
                            smallest = left;
                        if (right < Count && heap[right].priority < heap[smallest].priority)
                            smallest = right;

                        if (smallest != i)
                        {
                            var tmp = heap[i]; heap[i] = heap[smallest]; heap[smallest] = tmp;
                            i = smallest;
                        }
                        else break;
                    }
                }
                return result;
            }
        }

        private static int CalculateHeuristic(int n, Vector2Int[] origins, bool[] exited, Vector2Int[] targetGates)
        {
            int h = 0;
            for (int i = 0; i < n; i++)
            {
                if (!exited[i])
                {
                    int dist = Math.Abs(origins[i].x - targetGates[i].x) + Math.Abs(origins[i].y - targetGates[i].y);
                    h += 10 + dist;
                }
            }
            return h;
        }

        public struct StateKey : IEquatable<StateKey>
        {
            public ulong k0; // pieces 0..7
            public ulong k1; // pieces 8..15

            public StateKey(ulong k0, ulong k1)
            {
                this.k0 = k0;
                this.k1 = k1;
            }

            public bool Equals(StateKey other) => k0 == other.k0 && k1 == other.k1;
            public override bool Equals(object obj) => obj is StateKey other && Equals(other);
            public override int GetHashCode() => unchecked((int)((k0 ^ (k0 >> 32)) * 397 ^ (k1 ^ (k1 >> 32))));
            public static bool operator ==(StateKey a, StateKey b) => a.k0 == b.k0 && a.k1 == b.k1;
            public static bool operator !=(StateKey a, StateKey b) => !(a == b);
        }

        private struct BfsState
        {
            public StateKey key;
            public int moves;
            public int parentIndex;
            public string moveDesc;
        }

        private static StateKey PackBfsKey(int n, Vector2Int[] origins, bool[] exited)
        {
            ulong k0 = 0;
            ulong k1 = 0;
            for (int i = 0; i < n && i < 8; i++)
            {
                if (exited[i])
                {
                    k0 |= (0xFFUL << (i * 8));
                }
                else
                {
                    ulong bx = (ulong)(Mathf.Clamp(origins[i].x, 0, 15) & 0x0F);
                    ulong by = (ulong)(Mathf.Clamp(origins[i].y, 0, 15) & 0x0F);
                    k0 |= ((bx | (by << 4)) << (i * 8));
                }
            }
            for (int i = 8; i < n && i < 16; i++)
            {
                int shift = (i - 8) * 8;
                if (exited[i])
                {
                    k1 |= (0xFFUL << shift);
                }
                else
                {
                    ulong bx = (ulong)(Mathf.Clamp(origins[i].x, 0, 15) & 0x0F);
                    ulong by = (ulong)(Mathf.Clamp(origins[i].y, 0, 15) & 0x0F);
                    k1 |= ((bx | (by << 4)) << shift);
                }
            }
            return new StateKey(k0, k1);
        }

        private static void UnpackBfsKey(StateKey key, int n, Vector2Int[] origins, bool[] exited)
        {
            for (int i = 0; i < n && i < 8; i++)
            {
                byte b = (byte)((key.k0 >> (i * 8)) & 0xFF);
                if (b == 0xFF)
                {
                    exited[i] = true;
                    origins[i] = new Vector2Int(-1, -1);
                }
                else
                {
                    exited[i] = false;
                    origins[i] = new Vector2Int(b & 0x0F, (b >> 4) & 0x0F);
                }
            }
            for (int i = 8; i < n && i < 16; i++)
            {
                int shift = (i - 8) * 8;
                byte b = (byte)((key.k1 >> shift) & 0xFF);
                if (b == 0xFF)
                {
                    exited[i] = true;
                    origins[i] = new Vector2Int(-1, -1);
                }
                else
                {
                    exited[i] = false;
                    origins[i] = new Vector2Int(b & 0x0F, (b >> 4) & 0x0F);
                }
            }
        }

        public static SolverResult SolveLevelBFS(LevelData level, int maxStates = 60000, Vector2Int[] targetGateOrigins = null)
        {
            var result = new SolverResult
            {
                isSolvable = false,
                optimalMoves = 0,
                solutionTrace = new List<string>(),
                exploredStates = 0
            };

            int n = level.pieces != null ? level.pieces.Count : 0;
            if (n == 0)
            {
                result.isSolvable = true;
                return result;
            }
            if (n > 16)
            {
                return result;
            }

            int W = level.gridWidth;
            int H = level.gridHeight;

            bool[,] isWalkable = new bool[W, H];
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    isWalkable[x, y] = (level.GetCell(x, y).cellType == CellType.Walkable);
                }
            }

            List<Vector2Int>[] pieceOffsets = new List<Vector2Int>[n];
            for (int i = 0; i < n; i++)
            {
                var p = level.pieces[i];
                pieceOffsets[i] = p.shape != null
                    ? p.shape.GetRotatedOffsets(p.rotationSteps)
                    : new List<Vector2Int> { Vector2Int.zero };
            }

            if (targetGateOrigins == null || targetGateOrigins.Length != n)
            {
                targetGateOrigins = new Vector2Int[n];
                for (int i = 0; i < n; i++)
                {
                    var p = level.pieces[i];
                    Vector2Int pBounds = p.shape != null ? p.shape.GetRotatedBounds(p.rotationSteps) : Vector2Int.one;
                    Vector2Int bestOrigin = p.originPosition;
                    int bestDist = int.MaxValue;

                    for (int x = 0; x < W; x++)
                    {
                        for (int y = 0; y < H; y++)
                        {
                            var cell = level.GetCell(x, y);
                            if (cell.cellType == CellType.HalfExitGate || cell.cellType == CellType.ExitGate)
                            {
                                if (cell.gateColorType == BlockColorType.Universal || cell.gateColorType == p.colorType)
                                {
                                    Vector2Int candOrigin = cell.exitDirection switch
                                    {
                                        ExitDirection.Down => new Vector2Int(x, 1),
                                        ExitDirection.Up => new Vector2Int(x, H - 1 - pBounds.y),
                                        ExitDirection.Left => new Vector2Int(1, y),
                                        _ => new Vector2Int(W - 1 - pBounds.x, y)
                                    };
                                    int d = Math.Abs(p.originPosition.x - candOrigin.x) + Math.Abs(p.originPosition.y - candOrigin.y);
                                    if (d < bestDist)
                                    {
                                        bestDist = d;
                                        bestOrigin = candOrigin;
                                    }
                                }
                            }
                        }
                    }
                    targetGateOrigins[i] = bestOrigin;
                }
            }

            Vector2Int[] currentOrigins = new Vector2Int[n];
            bool[] currentExited = new bool[n];
            for (int i = 0; i < n; i++)
            {
                currentOrigins[i] = level.pieces[i].originPosition;
                currentExited[i] = false;
            }

            StateKey initialKey = PackBfsKey(n, currentOrigins, currentExited);

            List<BfsState> statePool = new List<BfsState>(Mathf.Min(maxStates, 25000));
            FastMinHeap heap = new FastMinHeap(Mathf.Min(maxStates, 25000));
            HashSet<StateKey> visited = new HashSet<StateKey>(Mathf.Min(maxStates, 25000));

            statePool.Add(new BfsState
            {
                key = initialKey,
                moves = 0,
                parentIndex = -1,
                moveDesc = "Початкове положення"
            });
            visited.Add(initialKey);
            int initH = CalculateHeuristic(n, currentOrigins, currentExited, targetGateOrigins);
            heap.Enqueue(0, initH);

            Vector2Int[] dirs = new Vector2Int[]
            {
                Vector2Int.up,
                Vector2Int.down,
                Vector2Int.left,
                Vector2Int.right
            };

            Vector2Int[] tempOrigins = new Vector2Int[n];
            bool[] tempExited = new bool[n];
            Vector2Int[] nextOrigins = new Vector2Int[n];
            bool[] nextExited = new bool[n];

            while (heap.Count > 0)
            {
                int stateIdx = heap.Dequeue();
                var state = statePool[stateIdx];
                result.exploredStates++;

                UnpackBfsKey(state.key, n, tempOrigins, tempExited);

                bool allExited = true;
                for (int i = 0; i < n; i++)
                {
                    if (!tempExited[i])
                    {
                        allExited = false;
                        break;
                    }
                }

                if (allExited)
                {
                    result.isSolvable = true;
                    result.optimalMoves = state.moves;

                    int cur = stateIdx;
                    while (cur > 0)
                    {
                        result.solutionTrace.Insert(0, statePool[cur].moveDesc);
                        cur = statePool[cur].parentIndex;
                    }
                    return result;
                }

                if (result.exploredStates >= maxStates)
                {
                    result.isSolvable = false;
                    return result;
                }

                // Підрахунок кількості деталей, що вже вийшли через ворота
                int exitedCount = 0;
                for (int e = 0; e < n; e++)
                {
                    if (tempExited[e]) exitedCount++;
                }

                for (int i = 0; i < n; i++)
                {
                    if (tempExited[i]) continue;

                    var pieceData = level.pieces[i];

                    // Механіка замороженого блоку: якщо лічильник > кількості деталей, що вже вийшли, деталь нерухома!
                    if (pieceData.freezeCount > exitedCount)
                        continue;

                    var restriction = pieceData.moveRestriction;
                    string freezeTag = pieceData.freezeCount > 0 ? "❄ " : "";

                    // 1. Прямий вихід у ворота без слайду (якщо деталь вже стоїть перед воротами)
                    foreach (var dir in dirs)
                    {
                        if (restriction == MoveRestriction.HorizontalOnly && (dir == Vector2Int.up || dir == Vector2Int.down))
                            continue;
                        if (restriction == MoveRestriction.VerticalOnly && (dir == Vector2Int.left || dir == Vector2Int.right))
                            continue;

                        if (CanExitImmediately(level, pieceData, tempOrigins[i], dir))
                        {
                            Array.Copy(tempOrigins, nextOrigins, n);
                            Array.Copy(tempExited, nextExited, n);
                            nextExited[i] = true;

                            StateKey exitKey = PackBfsKey(n, nextOrigins, nextExited);
                            if (visited.Add(exitKey))
                            {
                                int nextIdx = statePool.Count;
                                statePool.Add(new BfsState
                                {
                                    key = exitKey,
                                    moves = state.moves + 1,
                                    parentIndex = stateIdx,
                                    moveDesc = $"{pieceData.pieceId} ({freezeTag}{pieceData.colorType}) вихід через ворота {dir}"
                                });
                                int f = (state.moves + 1) + CalculateHeuristic(n, nextOrigins, nextExited, targetGateOrigins);
                                heap.Enqueue(nextIdx, f);
                            }
                        }
                    }

                    // 2. Слайди всередині поля
                    foreach (var dir in dirs)
                    {
                        if (restriction == MoveRestriction.HorizontalOnly && (dir == Vector2Int.up || dir == Vector2Int.down))
                            continue;
                        if (restriction == MoveRestriction.VerticalOnly && (dir == Vector2Int.left || dir == Vector2Int.right))
                            continue;

                        int maxDist = (dir == Vector2Int.left || dir == Vector2Int.right) ? (W - 3) : (H - 3);
                        for (int dist = 1; dist <= maxDist; dist++)
                        {
                            Vector2Int newOrigin = tempOrigins[i] + dir * dist;

                            if (!CanBfsPieceFit(level, pieceOffsets, i, newOrigin, tempOrigins, tempExited, isWalkable, W, H))
                                break;

                            Array.Copy(tempOrigins, nextOrigins, n);
                            Array.Copy(tempExited, nextExited, n);
                            nextOrigins[i] = newOrigin;

                            if (CanPieceExitAnywhereImmediate(level, pieceData, newOrigin))
                            {
                                nextExited[i] = true;
                                StateKey exitKey = PackBfsKey(n, nextOrigins, nextExited);
                                if (visited.Add(exitKey))
                                {
                                    int exitIdx = statePool.Count;
                                    statePool.Add(new BfsState
                                    {
                                        key = exitKey,
                                        moves = state.moves + 1,
                                        parentIndex = stateIdx,
                                        moveDesc = $"{pieceData.pieceId} ({freezeTag}{pieceData.colorType}) слайд {dir} ({dist}) та вихід у ворота"
                                    });
                                    int f = (state.moves + 1) + CalculateHeuristic(n, nextOrigins, nextExited, targetGateOrigins);
                                    heap.Enqueue(exitIdx, f);
                                }
                            }
                            else
                            {
                                StateKey slideKey = PackBfsKey(n, nextOrigins, nextExited);
                                if (visited.Add(slideKey))
                                {
                                    int nextIdx = statePool.Count;
                                    statePool.Add(new BfsState
                                    {
                                        key = slideKey,
                                        moves = state.moves + 1,
                                        parentIndex = stateIdx,
                                        moveDesc = $"{pieceData.pieceId} ({freezeTag}{pieceData.colorType}) слайд {dir} на {dist} крок(ів)"
                                    });
                                    int f = (state.moves + 1) + CalculateHeuristic(n, nextOrigins, nextExited, targetGateOrigins);
                                    heap.Enqueue(nextIdx, f);
                                }
                            }
                        }
                    }
                }
            }

            return result;
        }

        private static bool CanBfsPieceFit(
            LevelData level,
            List<Vector2Int>[] pieceOffsets,
            int pieceIdx,
            Vector2Int newOrigin,
            Vector2Int[] tempOrigins,
            bool[] tempExited,
            bool[,] isWalkable,
            int W,
            int H)
        {
            var offsets = pieceOffsets[pieceIdx];
            for (int k = 0; k < offsets.Count; k++)
            {
                int cx = newOrigin.x + offsets[k].x;
                int cy = newOrigin.y + offsets[k].y;
                if (cx < 1 || cx > W - 2 || cy < 1 || cy > H - 2)
                    return false;
                if (!isWalkable[cx, cy])
                    return false;
            }

            for (int otherIdx = 0; otherIdx < tempOrigins.Length; otherIdx++)
            {
                if (otherIdx == pieceIdx || tempExited[otherIdx]) continue;

                var otherOffsets = pieceOffsets[otherIdx];
                Vector2Int otherOrigin = tempOrigins[otherIdx];

                for (int k = 0; k < offsets.Count; k++)
                {
                    Vector2Int c = newOrigin + offsets[k];
                    for (int o = 0; o < otherOffsets.Count; o++)
                    {
                        if (c == otherOrigin + otherOffsets[o])
                            return false;
                    }
                }
            }

            return true;
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
