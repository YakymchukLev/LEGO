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

            [Header("Заморожені ворота (Frozen Gates)")]
            public bool allowFrozenGates = false;
            public int frozenGateCount = 0;     // скільки груп воріт заморозити
            public int maxGateFreezeCount = 2;  // макс. лічильник заморозки на групу воріт

            [Header("Подвійні блоки (Double Layer)")]
            public bool allowDoubleBlocks = false;
            public int doubleBlockCount = 0; // скільки деталей зробити подвійними

            [Header("Рухомі бар'єри (Movable Barriers)")]
            public bool allowMovableBarriers = false;
            public int movableBarrierCount = 0; // скільки 1x1 бар'єрів додати після розв'язку

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
                    s.allowFrozenGates = false;
                    s.frozenGateCount = 0;
                    s.maxGateFreezeCount = 0;
                    s.allowDoubleBlocks = false;
                    s.doubleBlockCount = 0;
                    s.allowMovableBarriers = false;
                    s.movableBarrierCount = 0;
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
                    s.allowFrozenGates = false;
                    s.frozenGateCount = 0;
                    s.maxGateFreezeCount = 0;
                    s.allowDoubleBlocks = false;
                    s.doubleBlockCount = 0;
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
                    s.allowFrozenGates = false;
                    s.frozenGateCount = 0;
                    s.maxGateFreezeCount = 0;
                    s.allowDoubleBlocks = true;
                    s.doubleBlockCount = 1;
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
                    s.allowFrozenGates = true;
                    s.frozenGateCount = 1;
                    s.maxGateFreezeCount = 1;
                    s.allowDoubleBlocks = true;
                    s.doubleBlockCount = 1;
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
                    s.allowFrozenGates = true;
                    s.frozenGateCount = 1;
                    s.maxGateFreezeCount = 2;
                    s.allowDoubleBlocks = true;
                    s.doubleBlockCount = 2;
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
                    s.allowFrozenGates = true;
                    s.frozenGateCount = 2;
                    s.maxGateFreezeCount = 2;
                    s.allowDoubleBlocks = true;
                    s.doubleBlockCount = 2;
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

            // Більше дозволів (1x1, Free, рідка сітка, post-hoc механіки) → легші базові розкладки.
            // Компенсуємо вищим порогом ходів / scramble, інакше "все увімкнено" = простіше.
            ComputeDifficultyCompensation(settings, out int targetMoves, out int minAcceptableMoves, out int scrambleBonus);
            int effectiveScrambleSteps = Mathf.Clamp(settings.scrambleSteps + scrambleBonus, settings.scrambleSteps, 450);

            int maxAttempts = 120;
            LevelData bestLevel = null;
            SolverResult bestSolver = default;
            int bestMoveCount = -1;

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

                bool ok = BuildLevelWithReverseSolver(
                    candidate, settings, shapePool, rng,
                    out Vector2Int[] targetGateOrigins, out List<string> reverseSolutionTrace,
                    scrambleStepsOverride: effectiveScrambleSteps);
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
                // Якщо увімкнені заморожені блоки/ворота, перевіряємо кілька конфігурацій заморозки для цієї розкладки
                int freezeTries = (settings.allowFrozenPieces || settings.allowFrozenGates) ? 3 : 1;
                bool candidateSolved = false;

                for (int fTry = 0; fTry < freezeTries; fTry++)
                {
                    if (settings.allowFrozenPieces)
                        ApplyFrozenPieces(candidate, settings, rng);
                    if (settings.allowFrozenGates)
                        ApplyFrozenGates(candidate, settings, rng);

                    // Double blocks are applied AFTER a solvable layout is accepted.
                    // Applying them before BFS often made the search fail → fallback without doubles.
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
                            // Doubles only when still solvable; otherwise keep the solvable base level.
                            return CommitLevelWithOptionalDoubles(candidate, settings, rng, targetGateOrigins, solver);
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
                return CommitLevelWithOptionalDoubles(bestLevel, settings, rng, null, bestSolver);
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
                    maxFreezeCount = Mathf.Clamp(settings.maxFreezeCount, 1, 2),
                    allowFrozenGates = settings.allowFrozenGates && (tryCount < 20),
                    frozenGateCount = Mathf.Clamp(settings.frozenGateCount, 1, 2),
                    maxGateFreezeCount = Mathf.Clamp(settings.maxGateFreezeCount, 1, 2),
                    allowDoubleBlocks = settings.allowDoubleBlocks,
                    doubleBlockCount = settings.doubleBlockCount,
                    allowMovableBarriers = settings.allowMovableBarriers,
                    movableBarrierCount = settings.movableBarrierCount,
                    allowedColors = settings.allowedColors
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
                        ApplyFrozenPieces(fallback, fallbackSettings, rng);
                    if (fallbackSettings.allowFrozenGates)
                        ApplyFrozenGates(fallback, fallbackSettings, rng);
                    var solver = SolveLevelBFS(fallback, 35000, fallbackTargets);
                    if (solver.isSolvable && solver.optimalMoves >= 3)
                    {
                        GenerationResult committed = CommitLevelWithOptionalDoubles(
                            fallback, fallbackSettings, rng, fallbackTargets, solver);
                        solverResult = committed.solverResult;
                        return committed.levelData;
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
                    var otherOffsets = other.GetOrientedOffsets();
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
        /// Accepts a solvable level, trying to add double blocks / movable barriers when requested.
        /// Never discards a solvable base layout — extras are optional icing.
        /// </summary>
        private static GenerationResult CommitLevelWithOptionalDoubles(
            LevelData level,
            GenerationSettings settings,
            System.Random rng,
            Vector2Int[] targetGateOrigins,
            SolverResult baseSolver)
        {
            var result = new GenerationResult { success = true, levelData = level };
            SolverResult currentSolver = baseSolver;

            if (TryApplySolvableDoubleBlocks(level, settings, rng, targetGateOrigins, currentSolver, out SolverResult withDoubles))
            {
                currentSolver = withDoubles;
                if (settings != null && settings.allowDoubleBlocks && HasAnyDoubleBlock(level))
                    Debug.Log($"LegoLevelGenerator: додано подвійні блоки (▣), розв'язок {withDoubles.optimalMoves} ходів.");
            }
            else
            {
                ClearDoubleBlocks(level);
                if (settings != null && settings.allowDoubleBlocks)
                {
                    Debug.LogWarning("LegoLevelGenerator: не вдалося додати розв'язувані подвійні блоки — залишено звичайний розв'язуваний рівень.");
                }
            }

            if (settings != null && settings.allowMovableBarriers)
            {
                if (TryApplyMovableBarriers(level, settings, rng, currentSolver, out SolverResult withBarriers))
                {
                    currentSolver = withBarriers;
                    if (HasAnyMovableBarrier(level))
                        Debug.Log($"LegoLevelGenerator: додано рухомі бар'єри (🧱), розв'язок {withBarriers.optimalMoves} ходів.");
                }
                else
                {
                    ClearMovableBarriers(level);
                    Debug.LogWarning("LegoLevelGenerator: не вдалося додати розв'язувані рухомі бар'єри — залишено рівень без них.");
                }
            }

            result.solverResult = currentSolver;
            return result;
        }

        private static bool HasAnyMovableBarrier(LevelData level)
        {
            if (level?.pieces == null) return false;
            for (int i = 0; i < level.pieces.Count; i++)
            {
                if (level.pieces[i] != null && level.pieces[i].isMovableBarrier)
                    return true;
            }
            return false;
        }

        private static void ClearMovableBarriers(LevelData level)
        {
            if (level?.pieces == null) return;
            for (int i = level.pieces.Count - 1; i >= 0; i--)
            {
                if (level.pieces[i] != null && level.pieces[i].isMovableBarrier)
                    level.pieces.RemoveAt(i);
            }
        }

        /// <summary>
        /// Places optional 1x1 movable barriers after a solvable layout.
        /// 1) Prefer edge/obstacle-adjacent cells + static validate (cheap).
        /// 2) Fall back to movable BFS (barrier can be pushed aside).
        /// 3) Reduce count until at least one barrier sticks.
        /// </summary>
        private static bool TryApplyMovableBarriers(
            LevelData level,
            GenerationSettings settings,
            System.Random rng,
            SolverResult baseSolver,
            out SolverResult finalSolver)
        {
            finalSolver = baseSolver;
            if (level == null || settings == null || !settings.allowMovableBarriers || level.pieces == null)
                return false;

            Debug.Log($"LegoLevelGenerator: 🧱 спроба додати бар'єри ×{Mathf.Max(1, settings.movableBarrierCount)} (деталей зараз: {level.pieces.Count}).");

            ClearMovableBarriers(level);

            int room = Mathf.Max(0, 16 - level.pieces.Count); // BFS hard cap
            if (room == 0)
            {
                Debug.LogWarning("LegoLevelGenerator: немає місця під бар'єри (ліміт 16 деталей у BFS).");
                return false;
            }

            int desired = Mathf.Clamp(settings.movableBarrierCount > 0 ? settings.movableBarrierCount : 1, 1, Mathf.Min(6, room));
            LegoShapeDefinition shape1x1 = FindShape1x1();
            if (shape1x1 == null)
            {
                Debug.LogWarning("LegoLevelGenerator: Shape_1x1 не знайдено — рухомі бар'єри пропущено.");
                return false;
            }

            List<Vector2Int> baseCandidates = CollectBarrierCandidateCells(level, shape1x1);
            if (baseCandidates.Count == 0)
            {
                Debug.LogWarning("LegoLevelGenerator: немає вільних клітинок для рухомих бар'єрів.");
                return false;
            }

            PreferPeripheralBarrierCells(level, baseCandidates);

            for (int count = desired; count >= 1; count--)
            {
                // Phase A: static blockers (fast) — barrier never needs to move.
                if (TryPlaceBarriersWithValidation(level, shape1x1, baseCandidates, count, rng, treatAsStatic: true, maxStates: 60000, out finalSolver))
                    return true;

                // Phase B: movable barriers — may need to be slid aside for the solution.
                if (TryPlaceBarriersWithValidation(level, shape1x1, baseCandidates, count, rng, treatAsStatic: false, maxStates: 150000, out finalSolver))
                    return true;
            }

            // Phase C: last resort — always place Free barriers on peripheral empty cells.
            // Base layout was solvable; barriers are optional (not required for win) and Free to move.
            PreferPeripheralBarrierCells(level, baseCandidates);
            ClearMovableBarriers(level);
            int forced = PlaceFreeBarriers(level, shape1x1, baseCandidates, Mathf.Min(desired, 2));
            if (forced > 0)
            {
                SolverResult staticSolve = SolveLevelBFS(level, 100000, null, treatBarriersAsStatic: true);
                if (staticSolve.isSolvable)
                {
                    finalSolver = staticSolve;
                    Debug.Log($"LegoLevelGenerator: 🧱 бар'єри ({forced}) додано (static pocket).");
                    return true;
                }

                SolverResult movable = SolveLevelBFS(level, 150000, null, treatBarriersAsStatic: false);
                if (movable.isSolvable)
                {
                    finalSolver = movable;
                    Debug.Log($"LegoLevelGenerator: 🧱 бар'єри ({forced}) додано (movable validate).");
                    return true;
                }

                // Keep barriers + original solve stats (BFS state space too large / blocked).
                finalSolver = baseSolver;
                Debug.LogWarning($"LegoLevelGenerator: 🧱 бар'єри ({forced}) додано без повторної BFS-перевірки. Переглянь рівень у прев'ю.");
                return true;
            }

            ClearMovableBarriers(level);
            Debug.LogWarning($"LegoLevelGenerator: не вдалося розмістити рухомі бар'єри (просили {desired}, вільних клітинок {baseCandidates.Count}).");
            return false;
        }

        private static bool TryPlaceBarriersWithValidation(
            LevelData level,
            LegoShapeDefinition shape1x1,
            List<Vector2Int> baseCandidates,
            int count,
            System.Random rng,
            bool treatAsStatic,
            int maxStates,
            out SolverResult finalSolver)
        {
            finalSolver = default;
            int attempts = Mathf.Clamp(14 + count * 6, 16, 40);
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                ClearMovableBarriers(level);

                List<Vector2Int> candidates = new List<Vector2Int>(baseCandidates);
                PreferPeripheralBarrierCells(level, candidates);
                // Rotate starting offset so attempts explore different peripheral cells.
                if (candidates.Count > 1)
                {
                    int rot = (rng != null ? rng.Next(candidates.Count) : attempt) % candidates.Count;
                    if (rot > 0)
                    {
                        List<Vector2Int> rotated = new List<Vector2Int>(candidates.Count);
                        for (int i = 0; i < candidates.Count; i++)
                            rotated.Add(candidates[(i + rot) % candidates.Count]);
                        candidates = rotated;
                    }
                }

                int placed = PlaceFreeBarriers(level, shape1x1, candidates, count);
                if (placed < count)
                    continue;

                SolverResult solved = SolveLevelBFS(level, maxStates, null, treatBarriersAsStatic: treatAsStatic);
                if (!solved.isSolvable)
                    continue;

                finalSolver = solved;
                return true;
            }

            ClearMovableBarriers(level);
            return false;
        }

        private static List<Vector2Int> CollectBarrierCandidateCells(LevelData level, LegoShapeDefinition shape1x1)
        {
            List<Vector2Int> candidates = new List<Vector2Int>();
            int W = level.gridWidth;
            int H = level.gridHeight;
            for (int y = 1; y < H - 1; y++)
            {
                for (int x = 1; x < W - 1; x++)
                {
                    if (level.GetCell(x, y).cellType != CellType.Walkable)
                        continue;
                    Vector2Int pos = new Vector2Int(x, y);
                    if (CanFitAt(level, shape1x1, 0, pos, level.pieces, null))
                        candidates.Add(pos);
                }
            }
            return candidates;
        }

        /// <summary>
        /// Stable-sort candidates so cells next to obstacles / border come first.
        /// </summary>
        private static void PreferPeripheralBarrierCells(LevelData level, List<Vector2Int> candidates)
        {
            if (candidates == null || candidates.Count <= 1) return;

            int W = level.gridWidth;
            int H = level.gridHeight;
            candidates.Sort((a, b) =>
            {
                int sa = BarrierCellScore(level, a, W, H);
                int sb = BarrierCellScore(level, b, W, H);
                return sb.CompareTo(sa); // higher score first
            });
        }

        private static int BarrierCellScore(LevelData level, Vector2Int pos, int W, int H)
        {
            int score = 0;
            // Prefer near inner edge of the playable ring.
            int edgeDist = Mathf.Min(Mathf.Min(pos.x, W - 1 - pos.x), Mathf.Min(pos.y, H - 1 - pos.y));
            score += Mathf.Clamp(4 - edgeDist, 0, 4) * 3;

            Vector2Int[] neigh = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            for (int i = 0; i < neigh.Length; i++)
            {
                Vector2Int n = pos + neigh[i];
                if (n.x < 0 || n.x >= W || n.y < 0 || n.y >= H) { score += 2; continue; }
                CellType t = level.GetCell(n.x, n.y).cellType;
                if (t == CellType.Obstacle || t == CellType.HalfObstacle || t == CellType.QuarterObstacle)
                    score += 4;
            }
            return score;
        }

        private static int PlaceFreeBarriers(
            LevelData level,
            LegoShapeDefinition shape1x1,
            List<Vector2Int> candidates,
            int count)
        {
            int placed = 0;
            for (int i = 0; i < candidates.Count && placed < count; i++)
            {
                Vector2Int origin = candidates[i];
                if (!CanFitAt(level, shape1x1, 0, origin, level.pieces, null))
                    continue;

                level.pieces.Add(new LegoPieceData
                {
                    pieceId = $"Barrier_{placed + 1}",
                    shape = shape1x1,
                    originPosition = origin,
                    rotationSteps = 0,
                    flipHorizontal = false,
                    colorType = BlockColorType.Red,
                    moveRestriction = MoveRestriction.Free,
                    freezeCount = 0,
                    isDoubleBlock = false,
                    isMovableBarrier = true,
                    isRequiredForWin = false
                });
                placed++;
            }
            return placed;
        }

        private static LegoShapeDefinition FindShape1x1()
        {
            const string knownPath = "Assets/Scripts/LevelSystem/Shape_1x1.asset";
            var direct = AssetDatabase.LoadAssetAtPath<LegoShapeDefinition>(knownPath);
            if (direct != null)
                return direct;

            LegoShapeDefinition fuzzy = null;
            string[] guids = AssetDatabase.FindAssets("t:LegoShapeDefinition");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var shape = AssetDatabase.LoadAssetAtPath<LegoShapeDefinition>(path);
                if (shape == null) continue;
                string name = (string.IsNullOrEmpty(shape.shapeId) ? shape.name : shape.shapeId).ToLowerInvariant();
                if (name == "shape_1x1" || name == "1x1")
                    return shape;
                if (fuzzy == null && (name.Contains("1x1") || name.Contains("1_1")))
                    fuzzy = shape;
            }
            return fuzzy;
        }

        private static bool PieceMustExitForSolve(LegoPieceData piece)
        {
            return piece != null && !piece.isMovableBarrier && piece.isRequiredForWin;
        }

        private static string RestrictionTag(MoveRestriction restriction)
        {
            return restriction switch
            {
                MoveRestriction.HorizontalOnly => "↔",
                MoveRestriction.VerticalOnly => "↕",
                MoveRestriction.Locked => "🔒",
                _ => ""
            };
        }

        /// <summary>
        /// Tries every piece × every valid inner gate color until BFS confirms solvability.
        /// </summary>
        private static bool TryApplySolvableDoubleBlocks(
            LevelData level,
            GenerationSettings settings,
            System.Random rng,
            Vector2Int[] targetGateOrigins,
            SolverResult baseSolver,
            out SolverResult finalSolver)
        {
            finalSolver = baseSolver;
            if (level == null || settings == null || !settings.allowDoubleBlocks)
                return true;
            if (level.pieces == null || level.pieces.Count == 0)
                return false;

            List<BlockColorType> gateColors = CollectGateColors(level);
            List<BlockColorType> innerPool = gateColors.Count > 0
                ? gateColors
                : (settings.allowedColors != null && settings.allowedColors.Count > 0
                    ? settings.allowedColors
                    : new List<BlockColorType> { BlockColorType.Red, BlockColorType.Yellow, BlockColorType.Blue, BlockColorType.Green });

            int desired = Mathf.Clamp(settings.doubleBlockCount > 0 ? settings.doubleBlockCount : 1, 1, level.pieces.Count);

            // Build piece order: free-move first, then others. Include piece 0. Skip movable barriers.
            List<int> pieceOrder = new List<int>(level.pieces.Count);
            for (int i = 0; i < level.pieces.Count; i++)
            {
                if (level.pieces[i] != null && !level.pieces[i].isMovableBarrier
                    && level.pieces[i].moveRestriction == MoveRestriction.Free)
                    pieceOrder.Add(i);
            }
            for (int i = 0; i < level.pieces.Count; i++)
            {
                if (level.pieces[i] == null || level.pieces[i].isMovableBarrier) continue;
                if (gateColors.Count > 0 && !gateColors.Contains(level.pieces[i].colorType))
                    continue;
                if (!pieceOrder.Contains(i))
                    pieceOrder.Add(i);
            }
            for (int i = 0; i < level.pieces.Count; i++)
            {
                if (level.pieces[i] != null && !level.pieces[i].isMovableBarrier && !pieceOrder.Contains(i))
                    pieceOrder.Add(i);
            }
            if (rng != null)
                ShuffleList(pieceOrder, rng);

            // desired == 1: try each single piece systematically (most common / most reliable).
            if (desired <= 1)
            {
                for (int p = 0; p < pieceOrder.Count; p++)
                {
                    int pieceIdx = pieceOrder[p];
                    var piece = level.pieces[pieceIdx];
                    if (piece == null || piece.isMovableBarrier) continue;

                    for (int g = 0; g < innerPool.Count; g++)
                    {
                        BlockColorType inner = innerPool[g];
                        if (inner == piece.colorType || inner == BlockColorType.Universal)
                            continue;

                        ClearDoubleBlocks(level);
                        piece.isDoubleBlock = true;
                        piece.innerColorType = inner;

                        SolverResult solved = SolveLevelBFS(level, 60000, targetGateOrigins);
                        if (solved.isSolvable)
                        {
                            finalSolver = solved;
                            return true;
                        }
                    }
                }

                ClearDoubleBlocks(level);
                return false;
            }

            // desired > 1: random multi-assign attempts, then fall back to single-double search.
            for (int attempt = 0; attempt < 12; attempt++)
            {
                ApplyDoubleBlocks(level, settings, rng, desired);
                if (!HasAnyDoubleBlock(level))
                    break;

                SolverResult solved = SolveLevelBFS(level, 60000, targetGateOrigins);
                if (solved.isSolvable)
                {
                    finalSolver = solved;
                    return true;
                }
            }

            // Fall back: search for at least one solvable double (same systematic loop as desired==1).
            for (int p = 0; p < pieceOrder.Count; p++)
            {
                int pieceIdx = pieceOrder[p];
                var piece = level.pieces[pieceIdx];
                if (piece == null) continue;

                for (int g = 0; g < innerPool.Count; g++)
                {
                    BlockColorType inner = innerPool[g];
                    if (inner == piece.colorType || inner == BlockColorType.Universal)
                        continue;

                    ClearDoubleBlocks(level);
                    piece.isDoubleBlock = true;
                    piece.innerColorType = inner;

                    SolverResult solved = SolveLevelBFS(level, 60000, targetGateOrigins);
                    if (solved.isSolvable)
                    {
                        finalSolver = solved;
                        return true;
                    }
                }
            }

            ClearDoubleBlocks(level);
            return false;
        }

        private static List<BlockColorType> CollectGateColors(LevelData level)
        {
            var gateColors = new List<BlockColorType>();
            if (level == null) return gateColors;
            for (int y = 0; y < level.gridHeight; y++)
            {
                for (int x = 0; x < level.gridWidth; x++)
                {
                    var cell = level.GetCell(x, y);
                    if (cell.cellType != CellType.ExitGate && cell.cellType != CellType.HalfExitGate) continue;
                    if (cell.gateColorType == BlockColorType.Universal) continue;
                    if (!gateColors.Contains(cell.gateColorType))
                        gateColors.Add(cell.gateColorType);
                }
            }
            return gateColors;
        }

        private static bool HasAnyDoubleBlock(LevelData level)
        {
            if (level?.pieces == null) return false;
            for (int i = 0; i < level.pieces.Count; i++)
            {
                if (level.pieces[i] != null && level.pieces[i].isDoubleBlock)
                    return true;
            }
            return false;
        }

        private static void ClearDoubleBlocks(LevelData level)
        {
            if (level?.pieces == null) return;
            for (int i = 0; i < level.pieces.Count; i++)
            {
                if (level.pieces[i] != null)
                    level.pieces[i].isDoubleBlock = false;
            }
        }

        /// <summary>
        /// Random multi-piece double assignment (used when doubleBlockCount &gt; 1).
        /// </summary>
        private static void ApplyDoubleBlocks(LevelData level, GenerationSettings settings, System.Random rng, int overrideCount = -1)
        {
            if (level == null || level.pieces == null || level.pieces.Count == 0) return;

            ClearDoubleBlocks(level);

            if (settings == null || !settings.allowDoubleBlocks) return;

            int total = level.pieces.Count;
            int desired = overrideCount > 0
                ? overrideCount
                : (settings.doubleBlockCount > 0 ? settings.doubleBlockCount : 1);
            desired = Mathf.Clamp(desired, 0, total);
            if (desired <= 0) return;

            List<BlockColorType> gateColors = CollectGateColors(level);
            List<BlockColorType> innerPool = gateColors.Count > 0
                ? gateColors
                : (settings.allowedColors != null && settings.allowedColors.Count > 0
                    ? settings.allowedColors
                    : new List<BlockColorType> { BlockColorType.Red, BlockColorType.Yellow, BlockColorType.Blue, BlockColorType.Green });

            List<int> candidates = new List<int>();
            for (int i = 0; i < total; i++)
            {
                var piece = level.pieces[i];
                if (piece == null || piece.isMovableBarrier) continue;
                if (gateColors.Count > 0 && !gateColors.Contains(piece.colorType))
                    continue;
                candidates.Add(i);
            }
            if (candidates.Count == 0)
            {
                for (int i = 0; i < total; i++)
                    if (level.pieces[i] != null && !level.pieces[i].isMovableBarrier) candidates.Add(i);
            }
            ShuffleList(candidates, rng);

            int assigned = 0;
            for (int k = 0; k < candidates.Count && assigned < desired; k++)
            {
                var piece = level.pieces[candidates[k]];
                BlockColorType inner = piece.colorType;

                for (int g = 0; g < innerPool.Count; g++)
                {
                    if (innerPool[g] != piece.colorType && innerPool[g] != BlockColorType.Universal)
                    {
                        inner = innerPool[g];
                        break;
                    }
                }
                if (inner == piece.colorType) continue;

                // Rotate through pool with rng for variety across attempts.
                if (innerPool.Count > 1)
                {
                    for (int attempt = 0; attempt < innerPool.Count; attempt++)
                    {
                        BlockColorType cand = innerPool[rng.Next(innerPool.Count)];
                        if (cand != piece.colorType && cand != BlockColorType.Universal)
                        {
                            inner = cand;
                            break;
                        }
                    }
                }

                piece.isDoubleBlock = true;
                piece.innerColorType = inner;
                assigned++;
            }
        }

        /// <summary>
        /// Призначає механіку заморожених воріт (Frozen Gates).
        /// Заморожує цілі групи суміжних воріт одного кольору/напрямку.
        /// За можливості лишає щонайменше одну групу розмороженою.
        /// </summary>
        private static void ApplyFrozenGates(LevelData level, GenerationSettings settings, System.Random rng)
        {
            if (level == null) return;

            ClearAllGateFreeze(level);
            if (settings == null || !settings.allowFrozenGates) return;

            List<List<Vector2Int>> groups = FindExitGateGroups(level);
            if (groups.Count == 0) return;

            int maxCanFreeze = groups.Count > 1 ? groups.Count - 1 : 1;
            int desired = settings.frozenGateCount > 0 ? settings.frozenGateCount : 1;
            int gatesToFreeze = Mathf.Clamp(desired, 1, maxCanFreeze);
            int maxFreezeLimit = Mathf.Clamp(settings.maxGateFreezeCount, 1, 5);

            List<int> order = new List<int>(groups.Count);
            for (int i = 0; i < groups.Count; i++)
                order.Add(i);
            ShuffleList(order, rng);

            int assigned = 0;
            for (int k = 0; k < order.Count && assigned < gatesToFreeze; k++)
            {
                List<Vector2Int> group = groups[order[k]];
                int count = (maxFreezeLimit > 1) ? rng.Next(1, maxFreezeLimit + 1) : 1;
                for (int c = 0; c < group.Count; c++)
                {
                    Vector2Int pos = group[c];
                    CellData cell = level.GetCell(pos.x, pos.y);
                    cell.freezeCount = count;
                    level.SetCell(pos.x, pos.y, cell);
                }
                assigned++;
            }
        }

        private static void ClearAllGateFreeze(LevelData level)
        {
            if (level == null) return;
            for (int y = 0; y < level.gridHeight; y++)
            {
                for (int x = 0; x < level.gridWidth; x++)
                {
                    CellData cell = level.GetCell(x, y);
                    if (cell.cellType != CellType.ExitGate && cell.cellType != CellType.HalfExitGate)
                        continue;
                    if (cell.freezeCount == 0) continue;
                    cell.freezeCount = 0;
                    level.SetCell(x, y, cell);
                }
            }
        }

        private static List<List<Vector2Int>> FindExitGateGroups(LevelData level)
        {
            var groups = new List<List<Vector2Int>>();
            var visited = new HashSet<Vector2Int>();

            for (int y = 0; y < level.gridHeight; y++)
            {
                for (int x = 0; x < level.gridWidth; x++)
                {
                    Vector2Int start = new Vector2Int(x, y);
                    if (visited.Contains(start)) continue;

                    CellData seed = level.GetCell(x, y);
                    if (seed.cellType != CellType.ExitGate && seed.cellType != CellType.HalfExitGate)
                        continue;

                    var group = new List<Vector2Int>();
                    var queue = new Queue<Vector2Int>();
                    queue.Enqueue(start);
                    visited.Add(start);

                    while (queue.Count > 0)
                    {
                        Vector2Int cur = queue.Dequeue();
                        group.Add(cur);

                        Vector2Int[] neighbors =
                        {
                            cur + Vector2Int.up,
                            cur + Vector2Int.down,
                            cur + Vector2Int.left,
                            cur + Vector2Int.right
                        };

                        for (int n = 0; n < neighbors.Length; n++)
                        {
                            Vector2Int nb = neighbors[n];
                            if (visited.Contains(nb) || !level.IsInsideGrid(nb.x, nb.y))
                                continue;

                            CellData other = level.GetCell(nb.x, nb.y);
                            if (other.cellType != seed.cellType) continue;
                            if (other.exitDirection != seed.exitDirection) continue;
                            if (other.gateColorType != seed.gateColorType) continue;

                            visited.Add(nb);
                            queue.Enqueue(nb);
                        }
                    }

                    if (group.Count > 0)
                        groups.Add(group);
                }
            }

            return groups;
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
            // Рухомі бар'єри теж можуть бути замороженими.
            List<int> candidateIndices = new List<int>();
            for (int i = 1; i < totalPieces; i++)
            {
                if (level.pieces[i] != null)
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
            out List<string> reverseSolutionTrace,
            int scrambleStepsOverride = -1)
        {
            reverseSolutionTrace = new List<string>();
            var stepTrace = new List<GeneratorStepTrace>();
            int W = settings.gridWidth;
            int H = settings.gridHeight;
            int count = Mathf.Clamp(settings.pieceCount, 1, 16);
            targetGateOrigins = new Vector2Int[count];
            int scrambleSteps = scrambleStepsOverride > 0 ? scrambleStepsOverride : settings.scrambleSteps;
            bool preferBulkierShapes = settings.useShape1x1
                || CountEnabledShapes(settings) >= 5
                || settings.targetMinMoves >= 10;

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
                // Custom: якщо увімкнені і Free, і слайдери — частіше слайдери (інакше рівні стають занадто легкими).
                var sliderRestrictions = new List<MoveRestriction>();
                if (settings.allowHorizontalOnly) sliderRestrictions.Add(MoveRestriction.HorizontalOnly);
                if (settings.allowVerticalOnly) sliderRestrictions.Add(MoveRestriction.VerticalOnly);

                bool hasFree = settings.allowFreeMove;
                if (!hasFree && sliderRestrictions.Count == 0)
                    sliderRestrictions.Add(MoveRestriction.Free);

                for (int i = 0; i < count; i++)
                {
                    if (hasFree && sliderRestrictions.Count > 0)
                    {
                        // ~65% слайдери, ~35% Free — тримає контроль без повної втрати Free.
                        if (rng.NextDouble() < 0.65)
                            restrictions.Add(sliderRestrictions[rng.Next(sliderRestrictions.Count)]);
                        else
                            restrictions.Add(MoveRestriction.Free);
                    }
                    else if (sliderRestrictions.Count > 0)
                    {
                        restrictions.Add(sliderRestrictions[rng.Next(sliderRestrictions.Count)]);
                    }
                    else
                    {
                        restrictions.Add(MoveRestriction.Free);
                    }
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
                    var shape = PickShapeWeighted(shapePool, rng, preferBulkierShapes);
                    int rot = rng.Next(4);
                    var offsets = shape.GetRotatedOffsets(rot);
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
                        if (!TryGetExitEdge(offsets, side, out int crossMin, out int edgeLen, out int edgeDepth))
                            continue;

                        bool isHorizontalGate = (side == ExitDirection.Down || side == ExitDirection.Up);
                        int gateLen = edgeLen;
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
                                ExitDirection.Down => new Vector2Int(g - crossMin, 1 - edgeDepth),
                                ExitDirection.Up => new Vector2Int(g - crossMin, (H - 2) - edgeDepth),
                                ExitDirection.Left => new Vector2Int(1 - edgeDepth, g - crossMin),
                                _ => new Vector2Int((W - 2) - edgeDepth, g - crossMin)
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
            bool scrambleOk = ExecuteDeepReverseScramble(level, scrambleSteps, rng, targetGateOrigins, stepTrace);
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

        private static bool CanPieceExitAnywhereImmediate(LevelData level, LegoPieceData piece, Vector2Int origin, Vector2Int[] origins = null, bool[] exited = null, BlockColorType? colorOverride = null, int destroyEvents = 0)
        {
            Vector2Int[] dirs = new Vector2Int[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            foreach (var dir in dirs)
            {
                if (piece.moveRestriction == MoveRestriction.HorizontalOnly && (dir == Vector2Int.up || dir == Vector2Int.down))
                    continue;
                if (piece.moveRestriction == MoveRestriction.VerticalOnly && (dir == Vector2Int.left || dir == Vector2Int.right))
                    continue;

                if (CanExitImmediately(level, piece, origin, dir, origins, exited, colorOverride, destroyEvents))
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
                if (p == null || p.isMovableBarrier) continue;
                if (CanPieceExitAnywhereImmediate(level, p))
                    count++;
            }
            return count;
        }

        private static bool CanExitImmediately(LevelData level, LegoPieceData piece, Vector2Int origin, Vector2Int dir, Vector2Int[] origins = null, bool[] exited = null, BlockColorType? colorOverride = null, int destroyEvents = 0)
        {
            if (piece == null || piece.shape == null || piece.isMovableBarrier) return false;

            int W = level.gridWidth;
            int H = level.gridHeight;
            var offsets = piece.GetOrientedOffsets();
            BlockColorType effectiveColor = colorOverride ?? piece.colorType;

            ExitDirection expectedExitDir = (dir == Vector2Int.up) ? ExitDirection.Up
                : (dir == Vector2Int.down) ? ExitDirection.Down
                : (dir == Vector2Int.left) ? ExitDirection.Left
                : ExitDirection.Right;

            HashSet<int> borderCoords = new HashSet<int>();
            int extremeCoord = (dir == Vector2Int.up || dir == Vector2Int.right) ? int.MinValue : int.MaxValue;

            for (int k = 0; k < offsets.Count; k++)
            {
                Vector2Int cell = origin + offsets[k];
                bool towardMin = dir == Vector2Int.down || dir == Vector2Int.left;
                int depth = (dir == Vector2Int.down || dir == Vector2Int.up) ? cell.y : cell.x;

                if (towardMin)
                {
                    if (depth < extremeCoord) extremeCoord = depth;
                }
                else if (depth > extremeCoord)
                {
                    extremeCoord = depth;
                }
            }

            for (int k = 0; k < offsets.Count; k++)
            {
                Vector2Int cell = origin + offsets[k];
                int depth = (dir == Vector2Int.down || dir == Vector2Int.up) ? cell.y : cell.x;
                if (depth != extremeCoord) continue;
                int cross = (dir == Vector2Int.down || dir == Vector2Int.up) ? cell.x : cell.y;
                borderCoords.Add(cross);
            }

            if (borderCoords.Count == 0)
                return false;

            if (dir == Vector2Int.down && extremeCoord != 1) return false;
            if (dir == Vector2Int.up && extremeCoord != H - 2) return false;
            if (dir == Vector2Int.left && extremeCoord != 1) return false;
            if (dir == Vector2Int.right && extremeCoord != W - 2) return false;

            CellData sampleGate = default;
            bool hasSample = false;
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
                if (gateCell.gateColorType != BlockColorType.Universal && gateCell.gateColorType != effectiveColor)
                    return false;
                if (gateCell.freezeCount > destroyEvents)
                    return false;

                if (!hasSample)
                {
                    sampleGate = gateCell;
                    hasSample = true;
                }
            }

            if (!hasSample) return false;

            // Уся ширина або висота блока має лежати всередині воріт. Отвір може бути довшим.
            bool alongX = dir == Vector2Int.down || dir == Vector2Int.up;
            int line = dir == Vector2Int.down ? 0 : dir == Vector2Int.up ? H - 1 : dir == Vector2Int.left ? 0 : W - 1;
            int sampleCross = 0;
            foreach (int coord in borderCoords)
            {
                sampleCross = coord;
                break;
            }

            int min = sampleCross;
            int max = sampleCross;
            while (IsSameGeneratorGate(level, alongX, line, min - 1, sampleGate)) min--;
            while (IsSameGeneratorGate(level, alongX, line, max + 1, sampleGate)) max++;

            for (int k = 0; k < offsets.Count; k++)
            {
                Vector2Int cell = origin + offsets[k];
                int cross = alongX ? cell.x : cell.y;
                if (cross < min || cross > max) return false;
            }

            return GeneratorExitCorridorClear(level, piece, origin, offsets, dir, expectedExitDir, origins, exited, effectiveColor);
        }

        /// <summary>
        /// Виріз фігури не виходить крізь інші блоки: кожен стовпчик має дійти до воріт по порожніх клітинках.
        /// </summary>
        private static bool GeneratorExitCorridorClear(LevelData level, LegoPieceData self, Vector2Int origin, List<Vector2Int> offsets, Vector2Int dir, ExitDirection expectedExitDir, Vector2Int[] origins, bool[] exited, BlockColorType? colorOverride = null)
        {
            if (level == null || offsets == null || offsets.Count == 0) return false;

            BlockColorType matchColor = colorOverride ?? (self != null ? self.colorType : BlockColorType.Red);

            var own = new HashSet<Vector2Int>();
            for (int i = 0; i < offsets.Count; i++)
                own.Add(origin + offsets[i]);

            var blocked = new HashSet<Vector2Int>();
            if (level.pieces != null)
            {
                for (int p = 0; p < level.pieces.Count; p++)
                {
                    LegoPieceData other = level.pieces[p];
                    if (other == null || other == self || other.shape == null) continue;
                    if (exited != null && p < exited.Length && exited[p]) continue;
                    Vector2Int otherOrigin = (origins != null && p < origins.Length) ? origins[p] : other.originPosition;
                    List<Vector2Int> otherOffsets = other.GetOrientedOffsets();
                    for (int c = 0; c < otherOffsets.Count; c++)
                        blocked.Add(otherOrigin + otherOffsets[c]);
                }
            }

            int limit = Math.Max(level.gridWidth, level.gridHeight) + 2;
            for (int i = 0; i < offsets.Count; i++)
            {
                Vector2Int pos = origin + offsets[i] + dir;
                for (int guard = 0; guard < limit; guard++)
                {
                    if (!level.IsInsideGrid(pos.x, pos.y)) return false;
                    if (own.Contains(pos))
                    {
                        pos += dir;
                        continue;
                    }

                    if (blocked.Contains(pos)) return false;

                    CellData cell = level.GetCell(pos.x, pos.y);
                    bool isGate = cell.cellType == CellType.ExitGate || cell.cellType == CellType.HalfExitGate;
                    if (isGate)
                    {
                        if (cell.exitDirection != expectedExitDir) return false;
                        if (cell.gateColorType != BlockColorType.Universal && cell.gateColorType != matchColor)
                            return false;
                        break;
                    }

                    if (cell.cellType != CellType.Walkable)
                        return false;

                    pos += dir;
                }
            }

            return true;
        }

        /// <summary>
        /// Грань фігури, якою вона входить у ворота. Довжина виходу дорівнює цій грані, а не bounding box.
        /// </summary>
        private static bool TryGetExitEdge(List<Vector2Int> offsets, ExitDirection side, out int crossMin, out int edgeLen, out int edgeDepth)
        {
            crossMin = 0;
            edgeLen = 0;
            edgeDepth = 0;
            if (offsets == null || offsets.Count == 0) return false;

            bool horizontal = side == ExitDirection.Down || side == ExitDirection.Up;
            int extreme = (side == ExitDirection.Down || side == ExitDirection.Left) ? int.MaxValue : int.MinValue;
            for (int i = 0; i < offsets.Count; i++)
            {
                int depth = horizontal ? offsets[i].y : offsets[i].x;
                if (side == ExitDirection.Down || side == ExitDirection.Left)
                {
                    if (depth < extreme) extreme = depth;
                }
                else if (depth > extreme)
                {
                    extreme = depth;
                }
            }

            int minCross = int.MaxValue;
            int maxCross = int.MinValue;
            int count = 0;
            for (int i = 0; i < offsets.Count; i++)
            {
                int depth = horizontal ? offsets[i].y : offsets[i].x;
                if (depth != extreme) continue;
                int cross = horizontal ? offsets[i].x : offsets[i].y;
                if (cross < minCross) minCross = cross;
                if (cross > maxCross) maxCross = cross;
                count++;
            }

            edgeLen = maxCross - minCross + 1;
            if (count != edgeLen) return false;

            int shapeMin = int.MaxValue;
            int shapeMax = int.MinValue;
            for (int i = 0; i < offsets.Count; i++)
            {
                int cross = horizontal ? offsets[i].x : offsets[i].y;
                if (cross < shapeMin) shapeMin = cross;
                if (cross > shapeMax) shapeMax = cross;
            }

            // Ворота не вужчі за блок: ніс має покривати всю ширину фігури.
            if (minCross != shapeMin || maxCross != shapeMax) return false;

            crossMin = minCross;
            edgeDepth = extreme;
            return true;
        }

        private static bool IsSameGeneratorGate(LevelData level, bool alongX, int line, int cross, CellData gateCell)
        {
            int x = alongX ? cross : line;
            int y = alongX ? line : cross;
            if (!level.IsInsideGrid(x, y)) return false;

            CellData other = level.GetCell(x, y);
            bool isExit = other.cellType == CellType.ExitGate || other.cellType == CellType.HalfExitGate;
            return isExit
                && other.exitDirection == gateCell.exitDirection
                && other.gateColorType == gateCell.gateColorType;
        }

        private static bool CanPieceFitAt(LevelData level, LegoPieceData piece, Vector2Int newOrigin)
        {
            int W = level.gridWidth;
            int H = level.gridHeight;
            var offsets = piece.GetOrientedOffsets();

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
                    var otherOffsets = other.GetOrientedOffsets();
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

        private static int CalculateHeuristic(int n, Vector2Int[] origins, bool[] exited, Vector2Int[] targetGates, bool[] mustExit = null)
        {
            int h = 0;
            for (int i = 0; i < n; i++)
            {
                if (mustExit != null && !mustExit[i]) continue;
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
            public ushort peeled; // bit i = double-block top layer already peeled

            public StateKey(ulong k0, ulong k1, ushort peeled = 0)
            {
                this.k0 = k0;
                this.k1 = k1;
                this.peeled = peeled;
            }

            public bool Equals(StateKey other) => k0 == other.k0 && k1 == other.k1 && peeled == other.peeled;
            public override bool Equals(object obj) => obj is StateKey other && Equals(other);
            public override int GetHashCode() => unchecked(
                (int)(k0 ^ (k0 >> 32)) * 397
                ^ (int)(k1 ^ (k1 >> 32))
                ^ (peeled * 486187739));
            public static bool operator ==(StateKey a, StateKey b) => a.k0 == b.k0 && a.k1 == b.k1 && a.peeled == b.peeled;
            public static bool operator !=(StateKey a, StateKey b) => !(a == b);
        }

        private struct BfsState
        {
            public StateKey key;
            public int moves;
            public int parentIndex;
            public string moveDesc;
        }

        private static StateKey PackBfsKey(int n, Vector2Int[] origins, bool[] exited, bool[] peeled = null)
        {
            ulong k0 = 0;
            ulong k1 = 0;
            ushort peeledMask = 0;
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
                if (peeled != null && i < peeled.Length && peeled[i])
                    peeledMask |= (ushort)(1 << i);
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
                if (peeled != null && i < peeled.Length && peeled[i])
                    peeledMask |= (ushort)(1 << i);
            }
            return new StateKey(k0, k1, peeledMask);
        }

        private static void UnpackBfsKey(StateKey key, int n, Vector2Int[] origins, bool[] exited, bool[] peeled = null)
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
                if (peeled != null && i < peeled.Length)
                    peeled[i] = (key.peeled & (1 << i)) != 0;
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
                if (peeled != null && i < peeled.Length)
                    peeled[i] = (key.peeled & (1 << i)) != 0;
            }
        }

        private static BlockColorType GetEffectivePieceColor(LegoPieceData piece, bool peeled)
        {
            if (piece == null) return BlockColorType.Red;
            if (peeled && piece.isDoubleBlock) return piece.innerColorType;
            return piece.colorType;
        }

        /// <param name="treatBarriersAsStatic">
        /// When true, movable barriers stay fixed as blockers (used to validate barrier placement cheaply).
        /// </param>
        public static SolverResult SolveLevelBFS(
            LevelData level,
            int maxStates = 60000,
            Vector2Int[] targetGateOrigins = null,
            bool treatBarriersAsStatic = false)
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
                    ? p.GetOrientedOffsets()
                    : new List<Vector2Int> { Vector2Int.zero };
            }

            if (targetGateOrigins == null || targetGateOrigins.Length != n)
            {
                targetGateOrigins = new Vector2Int[n];
                for (int i = 0; i < n; i++)
                {
                    var p = level.pieces[i];
                    Vector2Int pBounds = p.shape != null ? p.GetOrientedBounds() : Vector2Int.one;
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
            bool[] currentPeeled = new bool[n];
            bool[] mustExit = new bool[n];
            for (int i = 0; i < n; i++)
            {
                currentOrigins[i] = level.pieces[i].originPosition;
                currentExited[i] = false;
                currentPeeled[i] = false;
                mustExit[i] = PieceMustExitForSolve(level.pieces[i]);
            }

            StateKey initialKey = PackBfsKey(n, currentOrigins, currentExited, currentPeeled);

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
            int initH = CalculateHeuristic(n, currentOrigins, currentExited, targetGateOrigins, mustExit);
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
            bool[] tempPeeled = new bool[n];
            Vector2Int[] nextOrigins = new Vector2Int[n];
            bool[] nextExited = new bool[n];
            bool[] nextPeeled = new bool[n];

            while (heap.Count > 0)
            {
                int stateIdx = heap.Dequeue();
                var state = statePool[stateIdx];
                result.exploredStates++;

                UnpackBfsKey(state.key, n, tempOrigins, tempExited, tempPeeled);

                bool allExited = true;
                for (int i = 0; i < n; i++)
                {
                    if (mustExit[i] && !tempExited[i])
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

                // Destroy events unlock freeze: full exits + peeled top layers of double blocks.
                int destroyEvents = 0;
                for (int e = 0; e < n; e++)
                {
                    if (tempExited[e])
                    {
                        destroyEvents++;
                        if (level.pieces[e].isDoubleBlock)
                            destroyEvents++; // top layer was peeled before the final exit
                    }
                    else if (tempPeeled[e])
                    {
                        destroyEvents++;
                    }
                }

                for (int i = 0; i < n; i++)
                {
                    if (tempExited[i]) continue;

                    var pieceData = level.pieces[i];
                    bool isBarrier = pieceData != null && pieceData.isMovableBarrier;

                    // Placement validation: barriers stay put as fixed blockers.
                    if (treatBarriersAsStatic && isBarrier)
                        continue;

                    // Механіка замороженого блоку: якщо лічильник > кількості знищень, деталь нерухома!
                    if (pieceData.freezeCount > destroyEvents)
                        continue;

                    // Runtime: after peeling the top layer, move restriction is cleared.
                    var restriction = (!isBarrier && pieceData.isDoubleBlock && tempPeeled[i])
                        ? MoveRestriction.Free
                        : pieceData.moveRestriction;
                    string freezeTag = pieceData.freezeCount > 0 ? "❄ " : "";
                    BlockColorType effectiveColor = GetEffectivePieceColor(pieceData, tempPeeled[i]);
                    bool needsPeel = !isBarrier && pieceData.isDoubleBlock && !tempPeeled[i];
                    string pieceTag = isBarrier
                        ? $"🧱{freezeTag}{RestrictionTag(restriction)}"
                        : $"{freezeTag}{effectiveColor}";

                    // 1. Прямий вихід у ворота без слайду (бар'єри ніколи не виходять)
                    if (!isBarrier)
                    {
                        foreach (var dir in dirs)
                        {
                            if (restriction == MoveRestriction.HorizontalOnly && (dir == Vector2Int.up || dir == Vector2Int.down))
                                continue;
                            if (restriction == MoveRestriction.VerticalOnly && (dir == Vector2Int.left || dir == Vector2Int.right))
                                continue;

                            if (CanExitImmediately(level, pieceData, tempOrigins[i], dir, tempOrigins, tempExited, effectiveColor, destroyEvents))
                            {
                                Array.Copy(tempOrigins, nextOrigins, n);
                                Array.Copy(tempExited, nextExited, n);
                                Array.Copy(tempPeeled, nextPeeled, n);

                                string desc;
                                if (needsPeel)
                                {
                                    // Double block: peel top layer, stay on board with inner color.
                                    nextPeeled[i] = true;
                                    desc = $"{pieceData.pieceId} (▣ {pieceData.colorType}→{pieceData.innerColorType}) знято верхній шар {dir}";
                                }
                                else
                                {
                                    nextExited[i] = true;
                                    desc = $"{pieceData.pieceId} ({pieceTag}) вихід через ворота {dir}";
                                }

                                StateKey exitKey = PackBfsKey(n, nextOrigins, nextExited, nextPeeled);
                                if (visited.Add(exitKey))
                                {
                                    int nextIdx = statePool.Count;
                                    statePool.Add(new BfsState
                                    {
                                        key = exitKey,
                                        moves = state.moves + 1,
                                        parentIndex = stateIdx,
                                        moveDesc = desc
                                    });
                                    int f = (state.moves + 1) + CalculateHeuristic(n, nextOrigins, nextExited, targetGateOrigins, mustExit);
                                    heap.Enqueue(nextIdx, f);
                                }
                            }
                        }
                    }

                    // 2. Слайди всередині поля (бар'єри теж рухаються і блокують шлях)
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
                            Array.Copy(tempPeeled, nextPeeled, n);
                            nextOrigins[i] = newOrigin;

                            if (!isBarrier && CanPieceExitAnywhereImmediate(level, pieceData, newOrigin, tempOrigins, tempExited, effectiveColor, destroyEvents))
                            {
                                string desc;
                                if (needsPeel)
                                {
                                    nextPeeled[i] = true;
                                    desc = $"{pieceData.pieceId} (▣ {pieceData.colorType}→{pieceData.innerColorType}) слайд {dir} ({dist}) та знято верх";
                                }
                                else
                                {
                                    nextExited[i] = true;
                                    desc = $"{pieceData.pieceId} ({pieceTag}) слайд {dir} ({dist}) та вихід у ворота";
                                }

                                StateKey exitKey = PackBfsKey(n, nextOrigins, nextExited, nextPeeled);
                                if (visited.Add(exitKey))
                                {
                                    int exitIdx = statePool.Count;
                                    statePool.Add(new BfsState
                                    {
                                        key = exitKey,
                                        moves = state.moves + 1,
                                        parentIndex = stateIdx,
                                        moveDesc = desc
                                    });
                                    int f = (state.moves + 1) + CalculateHeuristic(n, nextOrigins, nextExited, targetGateOrigins, mustExit);
                                    heap.Enqueue(exitIdx, f);
                                }
                            }
                            else
                            {
                                StateKey slideKey = PackBfsKey(n, nextOrigins, nextExited, nextPeeled);
                                if (visited.Add(slideKey))
                                {
                                    int nextIdx = statePool.Count;
                                    statePool.Add(new BfsState
                                    {
                                        key = slideKey,
                                        moves = state.moves + 1,
                                        parentIndex = stateIdx,
                                        moveDesc = $"{pieceData.pieceId} ({pieceTag}) слайд {dir} на {dist} крок(ів)"
                                    });
                                    int f = (state.moves + 1) + CalculateHeuristic(n, nextOrigins, nextExited, targetGateOrigins, mustExit);
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

        /// <summary>
        /// More permissive settings make solvable layouts easier to find — raise move/scramble bars.
        /// </summary>
        private static void ComputeDifficultyCompensation(
            GenerationSettings settings,
            out int targetMoves,
            out int minAcceptableMoves,
            out int scrambleBonus)
        {
            int ease = 0;

            if (settings.useShape1x1)
                ease += 4; // 1x1 сильно спрощує маневри та виходи

            int shapeCount = CountEnabledShapes(settings);
            if (shapeCount >= 7) ease += 3;
            else if (shapeCount >= 5) ease += 2;
            else if (shapeCount >= 4) ease += 1;

            if (settings.allowFreeMove && (settings.allowHorizontalOnly || settings.allowVerticalOnly))
                ease += 2; // рівномірний Free розмиває складність слайдерів

            int innerCells = Mathf.Max(1, (settings.gridWidth - 2) * (settings.gridHeight - 2));
            float density = settings.pieceCount / (float)innerCells;
            if (density < 0.10f) ease += 4;
            else if (density < 0.14f) ease += 2;
            else if (density < 0.18f) ease += 1;

            // Post-hoc механіки не ускладнюють базовий BFS — вимагаємо жорсткішу базу.
            if (settings.allowFrozenPieces) ease += 1;
            if (settings.allowFrozenGates) ease += 1;
            if (settings.allowDoubleBlocks) ease += 1;
            if (settings.allowMovableBarriers) ease += 1;
            if (settings.allowObstacles && settings.obstacleCount > 0) ease += 0; // obstacles usually harden

            targetMoves = Mathf.Clamp(settings.targetMinMoves + ease, settings.targetMinMoves, 60);
            scrambleBonus = ease * 18;

            minAcceptableMoves = settings.difficultyTier switch
            {
                DifficultyTier.VeryEasy => 2,
                DifficultyTier.Easy => 3,
                DifficultyTier.Medium => 5,
                DifficultyTier.Hard => 7,
                DifficultyTier.VeryHard => 9,
                DifficultyTier.Expert => 11,
                _ => Mathf.Max(2, (int)(targetMoves * 0.7f))
            };

            // Підтягуємо мінімум ближче до компенсованої цілі (не даємо здати «майже простий» best).
            minAcceptableMoves = Mathf.Max(minAcceptableMoves, Mathf.Max(2, (int)(targetMoves * 0.72f)));

            if (settings.pieceCount <= 3)
                minAcceptableMoves = Mathf.Min(minAcceptableMoves, Mathf.Max(2, targetMoves - 4));
            else if (settings.pieceCount <= 5)
                minAcceptableMoves = Mathf.Min(minAcceptableMoves, Mathf.Max(3, targetMoves - 3));

            if (ease > 0)
            {
                Debug.Log($"LegoLevelGenerator: компенсація складності +{ease} ходів " +
                          $"(ціль {settings.targetMinMoves}→{targetMoves}, мін.прийнятне {minAcceptableMoves}, scramble +{scrambleBonus}). " +
                          "Більше дозволів форм/Free зазвичай дає легші розкладки.");
            }
        }

        private static int CountEnabledShapes(GenerationSettings settings)
        {
            int n = 0;
            if (settings.useShape1x1) n++;
            if (settings.useShape1x2) n++;
            if (settings.useShape1x3) n++;
            if (settings.useShape2x2) n++;
            if (settings.useShapeL) n++;
            if (settings.useShapeL2x2) n++;
            if (settings.useShapeT3x2) n++;
            if (settings.useShapeCross3x3) n++;
            return n;
        }

        private static LegoShapeDefinition PickShapeWeighted(
            List<LegoShapeDefinition> pool,
            System.Random rng,
            bool preferBulkier)
        {
            if (pool == null || pool.Count == 0) return null;
            if (pool.Count == 1 || rng == null) return pool[0];

            int total = 0;
            int[] weights = new int[pool.Count];
            for (int i = 0; i < pool.Count; i++)
            {
                int cells = pool[i].localOccupiedCells != null ? pool[i].localOccupiedCells.Count : 1;
                cells = Mathf.Max(1, cells);
                // 1x1 залишаємо рідкісним, коли є інші форми — інакше рівні «розсипаються».
                if (preferBulkier)
                    weights[i] = cells <= 1 ? 1 : cells * cells;
                else
                    weights[i] = cells;
                total += weights[i];
            }

            int roll = rng.Next(total);
            int acc = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                acc += weights[i];
                if (roll < acc)
                    return pool[i];
            }
            return pool[pool.Count - 1];
        }

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
