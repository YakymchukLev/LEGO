#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using LegoPuzzle.Data;
using LegoPuzzle.Runtime;

namespace LegoPuzzle.Editor
{
    /// <summary>
    /// Вікно студії генерації рівнів для LEGO Puzzle.
    /// Меню: Tools > LEGO Level Generator.
    /// Дозволяє самостійно обирати бажаний рівень складності (від Дуже легко до Експерта)
    /// або налаштовувати механіки вручну.
    /// </summary>
    public class LevelGeneratorWindow : EditorWindow
    {
        private enum GeneratorTab
        {
            SingleLevelStudio = 0,
            BatchGenerator = 1,
            DifficultyCurveInspector = 2
        }

        public enum BatchDifficultyMode
        {
            FollowLevelIndexCurve = 0, // Плавне зростання складності за номерами рівнів
            FixedDifficulty = 1         // Фіксована обрана складність для всіх рівнів
        }

        private GeneratorTab currentTab = GeneratorTab.SingleLevelStudio;
        private LegoLevelGenerator.GenerationSettings currentSettings = new LegoLevelGenerator.GenerationSettings();
        private LegoLevelGenerator.GenerationResult currentResult;

        // Налаштування пакетної генерації
        private int batchFromLevel = 1;
        private int batchToLevel = 20;
        private BatchDifficultyMode batchDiffMode = BatchDifficultyMode.FollowLevelIndexCurve;
        private LegoLevelGenerator.DifficultyTier batchFixedTier = LegoLevelGenerator.DifficultyTier.Medium;
        private bool batchOverwrite = true;
        private bool batchAutoPopulateLoader = true;
        private string batchSavePath = "Assets/Levels";

        // Фільтри механік для пакетної генерації
        private bool batchAllowSliders = true;
        private bool batchAllowObstacles = true;
        private bool batchAllowFrozenPieces = true;
        private bool batchAllowUniversalGates = false;
        private bool batchRandomizeArchetypes = true;
        private bool batchOverridePieceCount = false;
        private int batchCustomPieceCount = 6;
        private int batchPieceCountOffset = 0;

        // Інспектор кривої складності
        private int previewCurveLevel = 1;

        // Скрол та вигляд прев'ю
        private Vector2 scrollPos;
        private Vector2 gridScrollPos;
        private bool showMechanicsFoldout = true;
        private bool showBatchMechanicsFoldout = true;
        private bool showSolutionTrace = false;
        private float previewCellSize = LevelGridRenderer.DEFAULT_CELL_SIZE;

        [MenuItem("Tools/LEGO Level Generator/Відкрити вікно генератора", false, 10)]
        public static void ShowWindow()
        {
            var window = GetWindow<LevelGeneratorWindow>("LEGO Generator");
            window.minSize = new Vector2(580, 700);
            window.Show();
        }

        [MenuItem("Tools/LEGO Level Generator/🧪 Тестувати всі складності", false, 20)]
        public static void TestAllDifficulties()
        {
            var tiers = new[]
            {
                LegoLevelGenerator.DifficultyTier.VeryEasy,
                LegoLevelGenerator.DifficultyTier.Easy,
                LegoLevelGenerator.DifficultyTier.Medium,
                LegoLevelGenerator.DifficultyTier.Hard,
                LegoLevelGenerator.DifficultyTier.VeryHard,
                LegoLevelGenerator.DifficultyTier.Expert
            };

            Debug.Log("<color=cyan>=== Початок тестування генератора рівнів для всіх складностей ===</color>");
            int successCount = 0;

            for (int i = 0; i < tiers.Length; i++)
            {
                var tier = tiers[i];
                var settings = LegoLevelGenerator.GetSettingsForDifficulty(tier, i + 1);
                var res = LegoLevelGenerator.GenerateLevel(settings);
                if (res.success)
                {
                    successCount++;
                    int frozen = 0;
                    if (res.levelData.pieces != null)
                    {
                        foreach (var p in res.levelData.pieces)
                        {
                            if (p != null && p.IsFrozen) frozen++;
                        }
                    }
                    Debug.Log($"<color=green>✓ {tier}: Успішно згенеровано! (Ходів: {res.solverResult.optimalMoves}, Деталей: {res.levelData.pieces.Count}, ❄ Заморожених: {frozen})</color>");
                }
                else
                {
                    Debug.LogError($"✗ {tier}: Помилка: {res.errorMessage}");
                }
            }

            Debug.Log($"<color=cyan>=== Результат: {successCount}/{tiers.Length} складностей успішно згенеровано! ===</color>");
        }

        private const string PREFS_GEN_LEVEL_INDEX = "LEGO_Gen_SelectedLevelIndex";

        private void OnEnable()
        {
            if (currentSettings == null)
            {
                int savedIndex = EditorPrefs.GetInt(PREFS_GEN_LEVEL_INDEX, 1);
                currentSettings = LegoLevelGenerator.GetSettingsForDifficulty(LegoLevelGenerator.DifficultyTier.Easy, savedIndex);
                currentSettings.levelIndex = savedIndex;
                currentSettings.levelTitle = $"Level {savedIndex}";
            }
        }

        private void OnGUI()
        {
            DrawHeader();
            DrawTabBar();

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            switch (currentTab)
            {
                case GeneratorTab.SingleLevelStudio:
                    DrawSingleLevelStudio();
                    break;

                case GeneratorTab.BatchGenerator:
                    DrawBatchGenerator();
                    break;

                case GeneratorTab.DifficultyCurveInspector:
                    DrawDifficultyCurveInspector();
                    break;
            }

            EditorGUILayout.EndScrollView();
        }

        #region Header & Tabs

        private void DrawHeader()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🧩 LEGO Puzzle — Студія Генерації Рівнів", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Оберіть бажану складність або налаштуйте механіки власноруч — генератор побудує 100% розв'язуваний рівень!",
                EditorStyles.miniLabel
            );
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        private void DrawTabBar()
        {
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = currentTab == GeneratorTab.SingleLevelStudio ? new Color(0.4f, 0.8f, 1f) : Color.white;
            if (GUILayout.Button("🎲 Single Studio", GUILayout.Height(28)))
            {
                currentTab = GeneratorTab.SingleLevelStudio;
            }

            GUI.backgroundColor = currentTab == GeneratorTab.BatchGenerator ? new Color(0.4f, 0.8f, 1f) : Color.white;
            if (GUILayout.Button("📦 Batch Studio", GUILayout.Height(28)))
            {
                currentTab = GeneratorTab.BatchGenerator;
            }

            GUI.backgroundColor = currentTab == GeneratorTab.DifficultyCurveInspector ? new Color(0.4f, 0.8f, 1f) : Color.white;
            if (GUILayout.Button("📈 Крива складності", GUILayout.Height(28)))
            {
                currentTab = GeneratorTab.DifficultyCurveInspector;
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(8);
        }

        #endregion

        #region Tab 1: Single Level Studio

        private void DrawSingleLevelStudio()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("1. Базові параметри рівня", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            int enteredIndex = EditorGUILayout.IntField("Номер рівня (Level Index)", currentSettings.levelIndex);
            if (EditorGUI.EndChangeCheck())
            {
                currentSettings.levelIndex = Mathf.Max(1, enteredIndex);
                currentSettings.levelTitle = $"Level {currentSettings.levelIndex}";
                EditorPrefs.SetInt(PREFS_GEN_LEVEL_INDEX, currentSettings.levelIndex);

                if (currentResult.levelData != null)
                {
                    currentResult.levelData.levelIndex = currentSettings.levelIndex;
                    currentResult.levelData.levelTitle = currentSettings.levelTitle;
                }
            }

            EditorGUI.BeginChangeCheck();
            string enteredTitle = EditorGUILayout.TextField("Назва рівня", currentSettings.levelTitle);
            if (EditorGUI.EndChangeCheck())
            {
                currentSettings.levelTitle = enteredTitle;
                if (currentResult.levelData != null)
                {
                    currentResult.levelData.levelTitle = currentSettings.levelTitle;
                }
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // ВИБІР РІВНЯ СКЛАДНОСТІ (Difficulty Level)
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("2. Оберіть рівень складності (Difficulty Tier):", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            DrawDifficultyButton("🟢 Дуже легко", LegoLevelGenerator.DifficultyTier.VeryEasy, new Color(0.4f, 0.9f, 0.5f));
            DrawDifficultyButton("🟡 Легко", LegoLevelGenerator.DifficultyTier.Easy, new Color(0.95f, 0.9f, 0.4f));
            DrawDifficultyButton("🟠 Середньо", LegoLevelGenerator.DifficultyTier.Medium, new Color(1f, 0.75f, 0.3f));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            DrawDifficultyButton("🔴 Складно", LegoLevelGenerator.DifficultyTier.Hard, new Color(1f, 0.55f, 0.55f));
            DrawDifficultyButton("🟣 Дуже складно", LegoLevelGenerator.DifficultyTier.VeryHard, new Color(0.85f, 0.55f, 1f));
            DrawDifficultyButton("👑 Експерт", LegoLevelGenerator.DifficultyTier.Expert, new Color(0.95f, 0.4f, 0.4f));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            string diffDesc = LegoLevelGenerator.GetDifficultyDescription(currentSettings.difficultyTier);
            EditorGUILayout.HelpBox(diffDesc, MessageType.Info);

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // 3. Кількість деталей / блоків на рівні
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("3. Кількість деталей / блоків на рівні:", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            int newPieceCount = EditorGUILayout.IntSlider("Кількість блоків", currentSettings.pieceCount, 1, 16);
            if (EditorGUI.EndChangeCheck())
            {
                currentSettings.pieceCount = newPieceCount;
                currentSettings.difficultyTier = LegoLevelGenerator.DifficultyTier.Custom;
            }

            if (GUILayout.Button("-", GUILayout.Width(28), GUILayout.Height(18)))
            {
                currentSettings.pieceCount = Mathf.Clamp(currentSettings.pieceCount - 1, 1, 16);
                currentSettings.difficultyTier = LegoLevelGenerator.DifficultyTier.Custom;
            }
            if (GUILayout.Button("+", GUILayout.Width(28), GUILayout.Height(18)))
            {
                currentSettings.pieceCount = Mathf.Clamp(currentSettings.pieceCount + 1, 1, 16);
                currentSettings.difficultyTier = LegoLevelGenerator.DifficultyTier.Custom;
            }
            EditorGUILayout.EndHorizontal();

            // Швидкі кнопки вибору кількості блоків
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Швидкий вибір:", EditorStyles.miniLabel, GUILayout.Width(90));
            int[] quickCounts = new int[] { 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };
            foreach (int qc in quickCounts)
            {
                bool isCur = currentSettings.pieceCount == qc;
                Color prevC = GUI.backgroundColor;
                if (isCur) GUI.backgroundColor = new Color(0.4f, 0.85f, 1f);
                if (GUILayout.Button(qc.ToString(), EditorStyles.miniButton, GUILayout.Width(26)))
                {
                    currentSettings.pieceCount = qc;
                    currentSettings.difficultyTier = LegoLevelGenerator.DifficultyTier.Custom;
                }
                GUI.backgroundColor = prevC;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // 4. Механіки та правила гри
            showMechanicsFoldout = EditorGUILayout.Foldout(showMechanicsFoldout, "⚙️ Додаткові параметри поля та механіки (Повний контроль)", true);
            if (showMechanicsFoldout)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // Розміри та архетип
                EditorGUILayout.LabelField("Розміри сітки та архетип поля:", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.BeginHorizontal();
                currentSettings.gridWidth = EditorGUILayout.IntSlider("Ширина (Grid X)", currentSettings.gridWidth, 5, 14);
                currentSettings.gridHeight = EditorGUILayout.IntSlider("Висота (Grid Y)", currentSettings.gridHeight, 5, 16);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                currentSettings.archetype = (LegoLevelGenerator.BoardArchetype)EditorGUILayout.EnumPopup("Архетип поля", currentSettings.archetype);
                currentSettings.pieceCount = EditorGUILayout.IntSlider("К-сть деталей", currentSettings.pieceCount, 1, 16);
                EditorGUILayout.EndHorizontal();
                if (EditorGUI.EndChangeCheck())
                {
                    currentSettings.difficultyTier = LegoLevelGenerator.DifficultyTier.Custom;
                }

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Обмеження руху деталей (Sliders):", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                currentSettings.allowFreeMove = EditorGUILayout.ToggleLeft("Free (●)", currentSettings.allowFreeMove, GUILayout.Width(90));
                currentSettings.allowHorizontalOnly = EditorGUILayout.ToggleLeft("Horizontal (↔)", currentSettings.allowHorizontalOnly, GUILayout.Width(120));
                currentSettings.allowVerticalOnly = EditorGUILayout.ToggleLeft("Vertical (↕)", currentSettings.allowVerticalOnly, GUILayout.Width(110));
                if (EditorGUI.EndChangeCheck())
                {
                    currentSettings.difficultyTier = LegoLevelGenerator.DifficultyTier.Custom;
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Перешкоди (Obstacles / Wood Blocks):", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                currentSettings.allowObstacles = EditorGUILayout.ToggleLeft("Дозволити перешкоди", currentSettings.allowObstacles, GUILayout.Width(160));
                if (currentSettings.allowObstacles)
                {
                    currentSettings.obstacleCount = EditorGUILayout.IntSlider(currentSettings.obstacleCount, 1, 6);
                }
                if (EditorGUI.EndChangeCheck())
                {
                    currentSettings.difficultyTier = LegoLevelGenerator.DifficultyTier.Custom;
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Заморожені блоки (Frozen Pieces):", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                currentSettings.allowFrozenPieces = EditorGUILayout.ToggleLeft("Дозволити лід (❄)", currentSettings.allowFrozenPieces, GUILayout.Width(140));
                if (currentSettings.allowFrozenPieces)
                {
                    EditorGUILayout.LabelField("Блоків:", GUILayout.Width(50));
                    currentSettings.frozenPieceCount = EditorGUILayout.IntSlider(currentSettings.frozenPieceCount, 1, 4, GUILayout.Width(110));
                    EditorGUILayout.LabelField("Макс лічильник:", GUILayout.Width(100));
                    currentSettings.maxFreezeCount = EditorGUILayout.IntSlider(currentSettings.maxFreezeCount, 1, 5, GUILayout.Width(110));
                }
                if (EditorGUI.EndChangeCheck())
                {
                    currentSettings.difficultyTier = LegoLevelGenerator.DifficultyTier.Custom;
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Ворота виходу:", EditorStyles.boldLabel);
                currentSettings.allowUniversalGates = EditorGUILayout.ToggleLeft("Дозволити універсальні ворота (★) [УВАГА: полегшує гру]", currentSettings.allowUniversalGates);

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Дозволені форми блоків:", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.BeginHorizontal();
                currentSettings.useShape1x1 = EditorGUILayout.ToggleLeft("1x1", currentSettings.useShape1x1, GUILayout.Width(55));
                currentSettings.useShape1x2 = EditorGUILayout.ToggleLeft("1x2", currentSettings.useShape1x2, GUILayout.Width(55));
                currentSettings.useShape1x3 = EditorGUILayout.ToggleLeft("1x3", currentSettings.useShape1x3, GUILayout.Width(55));
                currentSettings.useShape2x2 = EditorGUILayout.ToggleLeft("2x2", currentSettings.useShape2x2, GUILayout.Width(55));
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                currentSettings.useShapeL = EditorGUILayout.ToggleLeft("L", currentSettings.useShapeL, GUILayout.Width(55));
                currentSettings.useShapeL2x2 = EditorGUILayout.ToggleLeft("L 2x2", currentSettings.useShapeL2x2, GUILayout.Width(55));
                currentSettings.useShapeT3x2 = EditorGUILayout.ToggleLeft("T 3x2", currentSettings.useShapeT3x2, GUILayout.Width(55));
                currentSettings.useShapeCross3x3 = EditorGUILayout.ToggleLeft("Cross 3x3", currentSettings.useShapeCross3x3, GUILayout.Width(80));
                EditorGUILayout.EndHorizontal();
                if (EditorGUI.EndChangeCheck())
                {
                    currentSettings.difficultyTier = LegoLevelGenerator.DifficultyTier.Custom;
                }

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Умови гри:", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                currentSettings.timeLimitSeconds = EditorGUILayout.FloatField("Час (сек)", currentSettings.timeLimitSeconds);
                currentSettings.movesLimit = EditorGUILayout.IntField("Ліміт ходів (0=off)", currentSettings.movesLimit);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                currentSettings.targetMinMoves = EditorGUILayout.IntSlider("Цільова мін. к-сть ходів", currentSettings.targetMinMoves, 2, 50);
                currentSettings.randomSeed = EditorGUILayout.IntField("Seed (0=rnd)", currentSettings.randomSeed);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(8);

            // Кнопка генерації — СУВОРО використовує обрану складність та прапорці
            GUI.backgroundColor = new Color(0.2f, 0.9f, 0.4f);
            string genButtonText = (currentSettings.difficultyTier != LegoLevelGenerator.DifficultyTier.Custom)
                ? $"🎲 ЗГЕНЕРУВАТИ РІВЕНЬ [{currentSettings.difficultyTier}]"
                : "🎲 ЗГЕНЕРУВАТИ РІВЕНЬ [Власна складність]";

            if (GUILayout.Button(genButtonText, GUILayout.Height(36)))
            {
                GenerateSingleLevel();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(8);

            // Відображення результату
            if (currentResult.levelData != null)
            {
                DrawResultStats();
                DrawGridVisualizer(currentResult.levelData);
                DrawActionButtons();
            }
            else if (!string.IsNullOrEmpty(currentResult.errorMessage))
            {
                EditorGUILayout.HelpBox($"Помилка генерації: {currentResult.errorMessage}", MessageType.Error);
            }
        }

        private void DrawDifficultyButton(string label, LegoLevelGenerator.DifficultyTier tier, Color activeColor)
        {
            bool isSelected = currentSettings.difficultyTier == tier;
            Color prev = GUI.backgroundColor;
            GUI.backgroundColor = isSelected ? activeColor : new Color(0.9f, 0.9f, 0.9f, 0.7f);

            if (GUILayout.Button(label, GUILayout.Height(28)))
            {
                GUI.FocusControl(null);
                int lvl = currentSettings.levelIndex;
                string title = currentSettings.levelTitle;
                currentSettings = LegoLevelGenerator.GetSettingsForDifficulty(tier, lvl);
                currentSettings.levelIndex = lvl;
                currentSettings.levelTitle = title;
                EditorPrefs.SetInt(PREFS_GEN_LEVEL_INDEX, lvl);

                if (currentResult.levelData != null)
                {
                    currentResult.levelData.levelIndex = lvl;
                    currentResult.levelData.levelTitle = title;
                }
            }

            GUI.backgroundColor = prev;
        }

        private void GenerateSingleLevel()
        {
            GUI.FocusControl(null);
            if (currentSettings.difficultyTier != LegoLevelGenerator.DifficultyTier.Custom && currentSettings.randomSeed == 0)
            {
                // Для кожного кліку генеруємо нову унікальну архітектуру на базі нового сід
                int lvl = currentSettings.levelIndex;
                string title = currentSettings.levelTitle;
                int chosenPieceCount = currentSettings.pieceCount;
                int freshSeed = System.Environment.TickCount;
                var freshSettings = LegoLevelGenerator.GetSettingsForDifficulty(currentSettings.difficultyTier, lvl + freshSeed);
                freshSettings.levelIndex = lvl;
                freshSettings.levelTitle = title;
                freshSettings.pieceCount = chosenPieceCount;
                freshSettings.randomSeed = 0; // залишаємо 0 для подальших кліків
                currentSettings = freshSettings;
            }

            currentResult = LegoLevelGenerator.GenerateLevel(currentSettings);
            if (currentResult.success)
            {
                currentResult.levelData.levelIndex = currentSettings.levelIndex;
                currentResult.levelData.levelTitle = currentSettings.levelTitle;
                Debug.Log($"<color=green>LegoLevelGenerator: Рівень {currentSettings.levelIndex} успішно згенеровано! Складність: {currentSettings.difficultyTier}, Архетип: {currentSettings.archetype}, Оптимально ходів: {currentResult.solverResult.optimalMoves}</color>");
            }
            else
            {
                Debug.LogWarning($"LegoLevelGenerator: Помилка генерації рівня {currentSettings.levelIndex}: {currentResult.errorMessage}");
            }
        }

        private void DrawResultStats()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            GUI.color = Color.green;
            EditorGUILayout.LabelField("✅ 100% Solvable", EditorStyles.boldLabel, GUILayout.Width(130));
            GUI.color = Color.white;

            EditorGUILayout.LabelField($"🎯 Хід розв'язку: {currentResult.solverResult.optimalMoves} кроків", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"🏛 Архетип: {currentSettings.archetype}");
            EditorGUILayout.LabelField($"🧩 Деталей: {currentResult.levelData.pieces.Count}");

            int frozenCount = 0;
            if (currentResult.levelData.pieces != null)
            {
                foreach (var p in currentResult.levelData.pieces)
                {
                    if (p != null && p.IsFrozen) frozenCount++;
                }
            }
            if (frozenCount > 0)
            {
                GUI.color = new Color(0.4f, 0.85f, 1f);
                EditorGUILayout.LabelField($"❄ Заморожених: {frozenCount}", EditorStyles.boldLabel);
                GUI.color = Color.white;
            }
            EditorGUILayout.EndHorizontal();

            showSolutionTrace = EditorGUILayout.Foldout(showSolutionTrace, $"📋 Покроковий розв'язок ({currentResult.solverResult.solutionTrace.Count} кроків)");
            if (showSolutionTrace)
            {
                EditorGUI.indentLevel++;
                for (int i = 0; i < currentResult.solverResult.solutionTrace.Count; i++)
                {
                    EditorGUILayout.LabelField($"{i + 1}. {currentResult.solverResult.solutionTrace[i]}", EditorStyles.miniLabel);
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawGridVisualizer(LevelData level)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Інтерактивне прев'ю ігрового поля:", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("Масштаб (Zoom):", GUILayout.Width(110));
            previewCellSize = EditorGUILayout.Slider(previewCellSize, LevelGridRenderer.MIN_CELL_SIZE, LevelGridRenderer.MAX_CELL_SIZE, GUILayout.Width(140));
            EditorGUILayout.EndHorizontal();

            float viewHeight = Mathf.Min(430f, (level.gridHeight + 1) * (previewCellSize + 3f) + 24f);
            gridScrollPos = EditorGUILayout.BeginScrollView(gridScrollPos, GUILayout.Height(viewHeight));

            for (int y = level.gridHeight - 1; y >= 0; y--)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                for (int x = 0; x < level.gridWidth; x++)
                {
                    CellData cell = level.GetCell(x, y);
                    LegoPieceData piece = GetPieceAt(level, x, y);

                    Rect cellRect = GUILayoutUtility.GetRect(previewCellSize, previewCellSize, GUILayout.Width(previewCellSize), GUILayout.Height(previewCellSize));
                    LevelGridRenderer.DrawCell(cellRect, cell, piece, x, y, previewCellSize);
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            LevelGridRenderer.DrawLegend();
        }

        private LegoPieceData GetPieceAt(LevelData level, int x, int y)
        {
            if (level.pieces == null) return null;
            Vector2Int pos = new Vector2Int(x, y);

            foreach (var piece in level.pieces)
            {
                if (piece == null) continue;
                var occupied = piece.GetOccupiedGridCells();
                if (occupied.Contains(pos)) return piece;
            }
            return null;
        }

        private void DrawActionButtons()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.3f, 0.7f, 1f);
            if (GUILayout.Button("▶ ТЕСТУВАТИ У ГРІ", GUILayout.Height(32)))
            {
                TestCurrentLevelInGame();
            }

            GUI.backgroundColor = new Color(0.9f, 0.75f, 0.2f);
            if (GUILayout.Button("💾 ЗБЕРЕГТИ ЯК ASSET", GUILayout.Height(32)))
            {
                SaveCurrentLevelAsAsset();
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(8);
        }

        private void TestCurrentLevelInGame()
        {
            if (currentResult.levelData == null) return;

            GUI.FocusControl(null);
            currentResult.levelData.levelIndex = currentSettings.levelIndex;
            currentResult.levelData.levelTitle = currentSettings.levelTitle;
            SaveCurrentLevelAsAsset();

            LevelLoader loader = FindFirstObjectByType<LevelLoader>();
            if (loader != null)
            {
                SerializedObject so = new SerializedObject(loader);
                so.FindProperty("testLevelData").objectReferenceValue = currentResult.levelData;
                so.FindProperty("overrideWithTestLevel").boolValue = true;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(loader);

                if (!EditorApplication.isPlaying)
                {
                    EditorApplication.isPlaying = true;
                }
            }
            else
            {
                EditorUtility.DisplayDialog("Тестування рівня", $"Рівень {currentSettings.levelIndex} збережено в Assets/Levels. Відкрийте сцену Game і запустіть Play Mode.", "ОК");
            }
        }

        private void SaveCurrentLevelAsAsset()
        {
            if (currentResult.levelData == null) return;

            GUI.FocusControl(null);
            int targetIndex = currentSettings.levelIndex;
            currentResult.levelData.levelIndex = targetIndex;
            currentResult.levelData.levelTitle = currentSettings.levelTitle;

            if (!Directory.Exists("Assets/Levels"))
            {
                Directory.CreateDirectory("Assets/Levels");
            }

            // Шукаємо, чи існує вже файл під цей номер: Level_014.asset (3 знаки) або Level_14.asset (2 знаки)
            string path3 = Path.Combine("Assets/Levels", $"Level_{targetIndex:D3}.asset").Replace('\\', '/');
            string pathRaw = Path.Combine("Assets/Levels", $"Level_{targetIndex}.asset").Replace('\\', '/');

            string path = path3;
            if (File.Exists(pathRaw) && !File.Exists(path3))
            {
                path = pathRaw;
            }

            var existing = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            if (existing != null)
            {
                // Захист від випадкового перезапису Level_001, якщо користувач обрав інший номер!
                if (targetIndex != 1 && (existing.name == "Level_001" || path.EndsWith("Level_001.asset")))
                {
                    Debug.LogError($"[LevelGenerator] Критична зупинка: Спроба перезаписати Level_001 при обраному Рівні {targetIndex}!");
                    path = pathRaw;
                    existing = AssetDatabase.LoadAssetAtPath<LevelData>(path);
                }
            }

            if (existing != null)
            {
                EditorUtility.CopySerialized(currentResult.levelData, existing);
                existing.levelIndex = targetIndex;
                existing.levelTitle = currentSettings.levelTitle;
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssets();
                EditorGUIUtility.PingObject(existing);
                Debug.Log($"<color=green>LevelGenerator: Оновлено існуючий асет: {path} (Рівень {targetIndex})</color>");
            }
            else
            {
                AssetDatabase.CreateAsset(currentResult.levelData, path);
                AssetDatabase.SaveAssets();
                EditorGUIUtility.PingObject(currentResult.levelData);
                Debug.Log($"<color=green>LevelGenerator: Створено новий асет: {path} (Рівень {targetIndex})</color>");
            }

            PopulateSceneLevelLoader();
        }

        #endregion

        #region Tab 2: Batch Generator

        private void DrawBatchGenerator()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Пакетна генерація серії рівнів", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Генерує великий пакет розв'язуваних рівнів із контролем складності та зберігає в Assets/Levels.",
                EditorStyles.miniLabel
            );
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Діапазон рівнів:", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            batchFromLevel = EditorGUILayout.IntField("Від рівня", batchFromLevel);
            batchToLevel = EditorGUILayout.IntField("До рівня", batchToLevel);
            EditorGUILayout.EndHorizontal();

            if (batchFromLevel > batchToLevel)
            {
                batchFromLevel = batchToLevel;
            }

            int count = (batchToLevel - batchFromLevel) + 1;
            EditorGUILayout.LabelField($"Всього буде згенеровано: {count} рівнів", EditorStyles.boldLabel);

            EditorGUILayout.Space(6);

            // ВИБІР РЕЖИМУ СКЛАДНОСТІ ДЛЯ ПАКЕТУ
            EditorGUILayout.LabelField("Режим складності пакету:", EditorStyles.boldLabel);
            batchDiffMode = (BatchDifficultyMode)EditorGUILayout.EnumPopup("Режим", batchDiffMode);

            if (batchDiffMode == BatchDifficultyMode.FixedDifficulty)
            {
                batchFixedTier = (LegoLevelGenerator.DifficultyTier)EditorGUILayout.EnumPopup("Фіксована складність", batchFixedTier);
                string desc = LegoLevelGenerator.GetDifficultyDescription(batchFixedTier);
                EditorGUILayout.HelpBox($"Усі {count} рівнів будуть згенеровані строго цієї складності:\n{desc}", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("Складність зростатиме поступово за номерами рівнів (від дуже простих до експертних).", MessageType.Info);
            }

            EditorGUILayout.Space(4);
            batchOverwrite = EditorGUILayout.ToggleLeft("⚠️ Перезаписувати існуючі файли .asset", batchOverwrite);
            batchAutoPopulateLoader = EditorGUILayout.ToggleLeft("🔄 Автоматично оновити список All Levels у LevelLoader", batchAutoPopulateLoader);
            batchSavePath = EditorGUILayout.TextField("Папка для збереження", batchSavePath);

            EditorGUILayout.Space(6);

            // Фільтри механік для пакетної генерації
            showBatchMechanicsFoldout = EditorGUILayout.Foldout(showBatchMechanicsFoldout, "⚙️ Глобальні фільтри механік для всього пакету", true);
            if (showBatchMechanicsFoldout)
            {
                EditorGUI.indentLevel++;
                batchAllowSliders = EditorGUILayout.ToggleLeft("Дозволити слайдери (↔, ↕)", batchAllowSliders);
                batchAllowObstacles = EditorGUILayout.ToggleLeft("Дозволити перешкоди (Дерев'яні блоки)", batchAllowObstacles);
                batchAllowFrozenPieces = EditorGUILayout.ToggleLeft("Дозволити заморожені блоки (❄ Заморозка)", batchAllowFrozenPieces);
                batchAllowUniversalGates = EditorGUILayout.ToggleLeft("Дозволити універсальні ворота (★) [За замовчуванням ВИМКНЕНО]", batchAllowUniversalGates);
                batchRandomizeArchetypes = EditorGUILayout.ToggleLeft("Урізноманітнювати архетипи полів (Острови, перешийки, лабіринти)", batchRandomizeArchetypes);
                EditorGUI.indentLevel--;

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("Коригування кількості блоків у пакеті:", EditorStyles.boldLabel);
                batchOverridePieceCount = EditorGUILayout.ToggleLeft("Фіксована кількість блоків для всіх рівнів пакету", batchOverridePieceCount);
                if (batchOverridePieceCount)
                {
                    EditorGUI.indentLevel++;
                    batchCustomPieceCount = EditorGUILayout.IntSlider("Кількість блоків", batchCustomPieceCount, 1, 16);
                    EditorGUI.indentLevel--;
                }
                else
                {
                    EditorGUI.indentLevel++;
                    batchPieceCountOffset = EditorGUILayout.IntSlider("Зсув кількості деталей (+/-)", batchPieceCountOffset, -5, 5);
                    if (batchPieceCountOffset != 0)
                    {
                        string sign = batchPieceCountOffset > 0 ? $"+{batchPieceCountOffset}" : $"{batchPieceCountOffset}";
                        EditorGUILayout.HelpBox($"До кількості деталей за шкалою складності буде додано: {sign} шт.", MessageType.None);
                    }
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            GUI.backgroundColor = new Color(0.2f, 0.85f, 0.4f);
            if (GUILayout.Button($"🚀 ЗГЕНЕРУВАТИ ВСІ {count} РІВНІВ У {batchSavePath}", GUILayout.Height(38)))
            {
                ExecuteBatchGeneration();
            }
            GUI.backgroundColor = Color.white;
        }

        private void ExecuteBatchGeneration()
        {
            if (!Directory.Exists(batchSavePath))
            {
                Directory.CreateDirectory(batchSavePath);
            }

            int total = (batchToLevel - batchFromLevel) + 1;
            int generatedCount = 0;
            int failedCount = 0;

            try
            {
                for (int lvl = batchFromLevel; lvl <= batchToLevel; lvl++)
                {
                    float progress = (float)(lvl - batchFromLevel) / total;
                    EditorUtility.DisplayProgressBar("Пакетна генерація рівнів LEGO", $"Генерація Level {lvl:D3} ({lvl}/{batchToLevel})...", progress);

                    LegoLevelGenerator.GenerationSettings s = (batchDiffMode == BatchDifficultyMode.FixedDifficulty)
                        ? LegoLevelGenerator.GetSettingsForDifficulty(batchFixedTier, lvl)
                        : LegoLevelGenerator.GetCurveSettingsForLevel(lvl);

                    // Коригування кількості блоків у пакеті!
                    if (batchOverridePieceCount)
                    {
                        s.pieceCount = Mathf.Clamp(batchCustomPieceCount, 1, 16);
                    }
                    else if (batchPieceCountOffset != 0)
                    {
                        s.pieceCount = Mathf.Clamp(s.pieceCount + batchPieceCountOffset, 1, 16);
                    }

                    // Застосовуємо глобальні фільтри користувача!
                    if (!batchAllowSliders)
                    {
                        s.allowHorizontalOnly = false;
                        s.allowVerticalOnly = false;
                    }
                    if (!batchAllowObstacles)
                    {
                        s.allowObstacles = false;
                        s.obstacleCount = 0;
                        if (s.archetype == LegoLevelGenerator.BoardArchetype.CenterPillar || s.archetype == LegoLevelGenerator.BoardArchetype.ChokepointChambers)
                        {
                            s.archetype = LegoLevelGenerator.BoardArchetype.StandardArena;
                        }
                    }
                    if (!batchAllowFrozenPieces)
                    {
                        s.allowFrozenPieces = false;
                        s.frozenPieceCount = 0;
                    }
                    s.allowUniversalGates = batchAllowUniversalGates;

                    if (batchRandomizeArchetypes && s.allowObstacles && lvl >= 7)
                    {
                        s.archetype = (LegoLevelGenerator.BoardArchetype)((lvl % 4) + 1);
                    }

                    string filename = $"Level_{lvl:D3}.asset";
                    string filePath = Path.Combine(batchSavePath, filename).Replace('\\', '/');

                    if (!batchOverwrite && File.Exists(filePath))
                    {
                        continue;
                    }

                    var genResult = LegoLevelGenerator.GenerateLevel(s);
                    if (genResult.success && genResult.levelData != null)
                    {
                        var existing = AssetDatabase.LoadAssetAtPath<LevelData>(filePath);
                        if (existing != null)
                        {
                            EditorUtility.CopySerialized(genResult.levelData, existing);
                            EditorUtility.SetDirty(existing);
                        }
                        else
                        {
                            AssetDatabase.CreateAsset(genResult.levelData, filePath);
                        }
                        generatedCount++;
                    }
                    else
                    {
                        failedCount++;
                        Debug.LogWarning($"LevelGenerator: Не вдалося згенерувати рівень {lvl}");
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            if (batchAutoPopulateLoader)
            {
                PopulateSceneLevelLoader();
            }

            EditorUtility.DisplayDialog(
                "Генерація завершена!",
                $"Успішно створено/оновлено: {generatedCount} рівнів.\nПомилок: {failedCount}.",
                "Чудово"
            );
        }

        private void PopulateSceneLevelLoader()
        {
            LevelLoader loader = FindFirstObjectByType<LevelLoader>();
            if (loader == null) return;

            string[] guids = AssetDatabase.FindAssets("t:LevelData");
            var list = new List<LevelData>();

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
                if (level != null)
                {
                    list.Add(level);
                }
            }

            list.Sort((a, b) => a.levelIndex.CompareTo(b.levelIndex));

            SerializedObject so = new SerializedObject(loader);
            var prop = so.FindProperty("allLevels");
            if (prop != null)
            {
                prop.ClearArray();
                for (int i = 0; i < list.Count; i++)
                {
                    prop.InsertArrayElementAtIndex(i);
                    prop.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
                }
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(loader);
            Debug.Log($"<color=green>LevelLoader: Автоматично додано {list.Count} рівнів у список All Levels!</color>");
        }

        #endregion

        #region Tab 3: Difficulty Curve Inspector

        private void DrawDifficultyCurveInspector()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Огляд рівнів складності гри (Difficulty Tiers)", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Ви можете як обирати фіксовану складність, так і використовувати автоматичну криву зростання.",
                EditorStyles.miniLabel
            );
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(8);

            previewCurveLevel = EditorGUILayout.IntSlider("Перегляд параметрів для рівня", previewCurveLevel, 1, 60);
            var curve = LegoLevelGenerator.GetCurveSettingsForLevel(previewCurveLevel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Параметри Level {previewCurveLevel} (Складність: {curve.difficultyTier}):", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"• Опис: {LegoLevelGenerator.GetDifficultyDescription(curve.difficultyTier)}");
            EditorGUILayout.LabelField($"• Розмір сітки: {curve.gridWidth} x {curve.gridHeight}");
            EditorGUILayout.LabelField($"• Архетип: {curve.archetype}");
            EditorGUILayout.LabelField($"• Кількість деталей: {curve.pieceCount}");
            EditorGUILayout.LabelField($"• Слайдери (Sliders): Horizontal={curve.allowHorizontalOnly}, Vertical={curve.allowVerticalOnly}");
            EditorGUILayout.LabelField($"• Перешкоди (Obstacles): {curve.obstacleCount}");
            EditorGUILayout.LabelField($"• Заморожені блоки (❄): {(curve.allowFrozenPieces ? $"{curve.frozenPieceCount} шт (лічильник до {curve.maxFreezeCount})" : "Вимкнено")}");
            EditorGUILayout.LabelField($"• Ворота: Строго кольорові");
            EditorGUILayout.LabelField($"• Цільова мін. к-сть ходів: {curve.targetMinMoves} кроків");
            EditorGUILayout.LabelField($"• Ліміт часу: {curve.timeLimitSeconds} сек");
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Зведена таблиця рівнів складності:", EditorStyles.boldLabel);

            DrawStageRow("Дуже легко", "6x6", "2 деталі", "100% Free", "0", "Без ❄", "3–4 ходи");
            DrawStageRow("Легко", "6x7", "3 деталі", "100% Free", "0", "Без ❄", "4–6 ходів");
            DrawStageRow("Середньо", "7x7", "4 деталі", "1 слайдер", "1 острів", "1 ❄ (к:1)", "7–10 ходів");
            DrawStageRow("Складно", "8x9", "5 деталей", "2 слайдери", "2 перешийки", "1 ❄ (к:2)", "11–15 ходів");
            DrawStageRow("Дуже складно", "8x10", "6 деталей", "3 слайдери", "3 лабіринти", "2 ❄ (к:2)", "16–22 ходи");
            DrawStageRow("Експерт", "8x11+", "7 деталей", "50%+ слайдери", "4+ перешкоди", "2 ❄ (к:3)", "22+ ходів");
        }

        private void DrawStageRow(string tier, string grid, string pieces, string sliders, string obs, string frozen, string moves)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField(tier, EditorStyles.boldLabel, GUILayout.Width(90));
            EditorGUILayout.LabelField(grid, GUILayout.Width(50));
            EditorGUILayout.LabelField(pieces, GUILayout.Width(70));
            EditorGUILayout.LabelField(sliders, GUILayout.Width(100));
            EditorGUILayout.LabelField(obs, GUILayout.Width(90));
            EditorGUILayout.LabelField(frozen, GUILayout.Width(75));
            EditorGUILayout.LabelField(moves);
            EditorGUILayout.EndHorizontal();
        }

        #endregion
    }
}
#endif
