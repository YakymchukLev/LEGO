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
    /// </summary>
    public class LevelGeneratorWindow : EditorWindow
    {
        private enum GeneratorTab
        {
            SingleLevelStudio = 0,
            BatchGenerator = 1,
            DifficultyCurveInspector = 2
        }

        private GeneratorTab currentTab = GeneratorTab.SingleLevelStudio;
        private LegoLevelGenerator.GenerationSettings currentSettings = new LegoLevelGenerator.GenerationSettings();
        private LegoLevelGenerator.GenerationResult currentResult;
        private bool useAutoCurve = true;

        // Налаштування пакетної генерації
        private int batchFromLevel = 1;
        private int batchToLevel = 20;
        private bool batchUseCurve = true;
        private bool batchOverwrite = true;
        private bool batchAutoPopulateLoader = true;
        private string batchSavePath = "Assets/Levels";

        // Інспектор кривої складності
        private int previewCurveLevel = 1;

        // Скрол та вигляд прев'ю
        private Vector2 scrollPos;
        private Vector2 gridScrollPos;
        private bool showMechanicsFoldout = true;
        private bool showSolutionTrace = false;
        private const float CELL_GUI_SIZE = 34f;

        [MenuItem("Tools/LEGO Level Generator", false, 10)]
        public static void ShowWindow()
        {
            var window = GetWindow<LevelGeneratorWindow>("LEGO Generator");
            window.minSize = new Vector2(560, 650);
            window.Show();
        }

        private void OnEnable()
        {
            if (currentSettings == null)
            {
                currentSettings = LegoLevelGenerator.GetCurveSettingsForLevel(1);
            }
            else if (useAutoCurve)
            {
                SyncSettingsWithCurve();
            }
        }

        private void SyncSettingsWithCurve()
        {
            var curve = LegoLevelGenerator.GetCurveSettingsForLevel(currentSettings.levelIndex);
            currentSettings.gridWidth = curve.gridWidth;
            currentSettings.gridHeight = curve.gridHeight;
            currentSettings.pieceCount = curve.pieceCount;
            currentSettings.timeLimitSeconds = curve.timeLimitSeconds;
            currentSettings.allowFreeMove = curve.allowFreeMove;
            currentSettings.allowHorizontalOnly = curve.allowHorizontalOnly;
            currentSettings.allowVerticalOnly = curve.allowVerticalOnly;
            currentSettings.allowObstacles = curve.allowObstacles;
            currentSettings.obstacleCount = curve.obstacleCount;
            currentSettings.allowUniversalGates = curve.allowUniversalGates;
            currentSettings.forceMatchingColorGates = curve.forceMatchingColorGates;
            currentSettings.useShape1x1 = curve.useShape1x1;
            currentSettings.useShape1x2 = curve.useShape1x2;
            currentSettings.useShape1x3 = curve.useShape1x3;
            currentSettings.useShape2x2 = curve.useShape2x2;
            currentSettings.useShapeL = curve.useShapeL;
            currentSettings.useShapeL2x2 = curve.useShapeL2x2;
            currentSettings.useShapeT3x2 = curve.useShapeT3x2;
            currentSettings.useShapeCross3x3 = curve.useShapeCross3x3;
            currentSettings.allowedColors = new List<BlockColorType>(curve.allowedColors);
            currentSettings.targetMinMoves = curve.targetMinMoves;
            currentSettings.scrambleSteps = curve.scrambleSteps;
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
            EditorGUILayout.LabelField("🧩 LEGO Puzzle — Генератор Рівнів (Hybrid 1 + 3)", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Reverse-Solver з 100% гарантією розв'язку + пакетна генерація та плавна крива складності",
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
            EditorGUILayout.LabelField("1. Параметри та прогресія", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUILayout.IntField("Номер рівня (Level Index)", currentSettings.levelIndex);
            if (EditorGUI.EndChangeCheck())
            {
                currentSettings.levelIndex = Mathf.Max(1, newIndex);
                currentSettings.levelTitle = $"Level {currentSettings.levelIndex}";
                if (useAutoCurve)
                {
                    SyncSettingsWithCurve();
                }
            }

            currentSettings.levelTitle = EditorGUILayout.TextField("Назва рівня", currentSettings.levelTitle);

            EditorGUI.BeginChangeCheck();
            useAutoCurve = EditorGUILayout.ToggleLeft("✨ Використовувати плавну криву складності (Auto Curve)", useAutoCurve, EditorStyles.boldLabel);
            if (EditorGUI.EndChangeCheck() && useAutoCurve)
            {
                SyncSettingsWithCurve();
            }

            if (useAutoCurve)
            {
                string summary = GetLevelStageDescription(currentSettings.levelIndex);
                EditorGUILayout.HelpBox(summary, MessageType.Info);
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // 2. Механіки та правила гри
            showMechanicsFoldout = EditorGUILayout.Foldout(showMechanicsFoldout, "⚙️ Механіки та перемикачі на рівні", true);
            if (showMechanicsFoldout)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // Розміри
                EditorGUILayout.LabelField("Розміри сітки:", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                currentSettings.gridWidth = EditorGUILayout.IntSlider("Ширина (Grid X)", currentSettings.gridWidth, 5, 12);
                currentSettings.gridHeight = EditorGUILayout.IntSlider("Висота (Grid Y)", currentSettings.gridHeight, 5, 14);
                EditorGUILayout.EndHorizontal();

                currentSettings.pieceCount = EditorGUILayout.IntSlider("Кількість деталей", currentSettings.pieceCount, 1, 8);

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Обмеження руху (Sliders):", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                currentSettings.allowFreeMove = EditorGUILayout.ToggleLeft("Free (●)", currentSettings.allowFreeMove, GUILayout.Width(90));
                currentSettings.allowHorizontalOnly = EditorGUILayout.ToggleLeft("Horizontal (↔)", currentSettings.allowHorizontalOnly, GUILayout.Width(120));
                currentSettings.allowVerticalOnly = EditorGUILayout.ToggleLeft("Vertical (↕)", currentSettings.allowVerticalOnly, GUILayout.Width(110));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Перешкоди (Obstacles / Wood):", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                currentSettings.allowObstacles = EditorGUILayout.ToggleLeft("Дозволити перешкоди", currentSettings.allowObstacles, GUILayout.Width(160));
                if (currentSettings.allowObstacles)
                {
                    currentSettings.obstacleCount = EditorGUILayout.IntSlider(currentSettings.obstacleCount, 1, 6);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Ворота виходу:", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                currentSettings.allowUniversalGates = EditorGUILayout.ToggleLeft("Універсальні ворота (★)", currentSettings.allowUniversalGates);
                currentSettings.forceMatchingColorGates = EditorGUILayout.ToggleLeft("Строго під колір", currentSettings.forceMatchingColorGates);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Дозволені форми блоків:", EditorStyles.boldLabel);
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

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Умови гри:", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                currentSettings.timeLimitSeconds = EditorGUILayout.FloatField("Час (сек)", currentSettings.timeLimitSeconds);
                currentSettings.movesLimit = EditorGUILayout.IntField("Ліміт ходів (0=off)", currentSettings.movesLimit);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                currentSettings.targetMinMoves = EditorGUILayout.IntSlider("Мін. ходів розв'язку", currentSettings.targetMinMoves, 2, 25);
                currentSettings.randomSeed = EditorGUILayout.IntField("Seed (0=rnd)", currentSettings.randomSeed);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(8);

            // Кнопка генерації
            GUI.backgroundColor = new Color(0.2f, 0.9f, 0.4f);
            if (GUILayout.Button("🎲 ЗГЕНЕРУВАТИ РІВЕНЬ (Reverse-Solver)", GUILayout.Height(36)))
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

        private void GenerateSingleLevel()
        {
            if (useAutoCurve)
            {
                SyncSettingsWithCurve();
            }

            currentResult = LegoLevelGenerator.GenerateLevel(currentSettings);
            if (currentResult.success)
            {
                Debug.Log($"<color=green>LegoLevelGenerator: Рівень {currentSettings.levelIndex} успішно згенеровано! Оптимально ходів: {currentResult.solverResult.optimalMoves}</color>");
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
            EditorGUILayout.LabelField($"🧩 Деталей: {currentResult.levelData.pieces.Count}");
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
            EditorGUILayout.LabelField("Інтерактивне прев'ю ігрового поля:", EditorStyles.boldLabel);

            gridScrollPos = EditorGUILayout.BeginScrollView(gridScrollPos, GUILayout.Height(Mathf.Min(350, (level.gridHeight + 2) * (CELL_GUI_SIZE + 4))));

            for (int y = level.gridHeight - 1; y >= 0; y--)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                for (int x = 0; x < level.gridWidth; x++)
                {
                    CellData cell = level.GetCell(x, y);
                    LegoPieceData piece = GetPieceAt(level, x, y);

                    Color originalBg = GUI.backgroundColor;

                    if (piece != null)
                    {
                        GUI.backgroundColor = piece.GetColor();
                    }
                    else
                    {
                        GUI.backgroundColor = cell.cellType switch
                        {
                            CellType.Walkable => new Color(0.95f, 0.90f, 0.55f),
                            CellType.Obstacle => new Color(0.55f, 0.35f, 0.20f),
                            CellType.HalfObstacle => new Color(0.68f, 0.42f, 0.22f),
                            CellType.QuarterObstacle => new Color(0.72f, 0.45f, 0.24f),
                            CellType.ExitGate => cell.GetEffectiveColor(),
                            CellType.HalfExitGate => cell.GetEffectiveColor(),
                            _ => new Color(0.2f, 0.2f, 0.2f, 0.3f)
                        };
                    }

                    string label = GetCellGlyph(cell, piece);
                    GUILayout.Box(label, GUILayout.Width(CELL_GUI_SIZE), GUILayout.Height(CELL_GUI_SIZE));

                    GUI.backgroundColor = originalBg;
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        private string GetCellGlyph(CellData cell, LegoPieceData piece)
        {
            if (piece != null)
            {
                return piece.moveRestriction switch
                {
                    MoveRestriction.HorizontalOnly => "↔",
                    MoveRestriction.VerticalOnly => "↕",
                    _ => "●"
                };
            }

            return cell.cellType switch
            {
                CellType.Obstacle => "■",
                CellType.HalfObstacle => cell.exitDirection switch
                {
                    ExitDirection.Up => "▀",
                    ExitDirection.Down => "▄",
                    ExitDirection.Left => "▌",
                    ExitDirection.Right => "▐",
                    _ => "■"
                },
                CellType.QuarterObstacle => "▲",
                CellType.ExitGate => (cell.gateColorType == BlockColorType.Universal ? "★" : "") + (cell.exitDirection switch
                {
                    ExitDirection.Up => "▲",
                    ExitDirection.Down => "▼",
                    ExitDirection.Left => "◀",
                    ExitDirection.Right => "▶",
                    _ => "O"
                }),
                CellType.HalfExitGate => (cell.gateColorType == BlockColorType.Universal ? "★" : "") + (cell.exitDirection switch
                {
                    ExitDirection.Up => "▲",
                    ExitDirection.Down => "▼",
                    ExitDirection.Left => "◀",
                    ExitDirection.Right => "▶",
                    _ => "O"
                }),
                CellType.Walkable => "·",
                _ => ""
            };
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
                // Якщо loader не знайдено на поточній сцені, збережемо як тимчасовий асет
                SaveCurrentLevelAsAsset();
                EditorUtility.DisplayDialog("Тестування рівня", "Рівень збережено в Assets/Levels. Відкрийте сцену Game і запустіть Play Mode.", "ОК");
            }
        }

        private void SaveCurrentLevelAsAsset()
        {
            if (currentResult.levelData == null) return;

            if (!Directory.Exists("Assets/Levels"))
            {
                Directory.CreateDirectory("Assets/Levels");
            }

            string filename = $"Level_{currentResult.levelData.levelIndex:D3}.asset";
            string path = Path.Combine("Assets/Levels", filename).Replace('\\', '/');

            var existing = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(currentResult.levelData, existing);
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssets();
                EditorGUIUtility.PingObject(existing);
                Debug.Log($"<color=green>LevelGenerator: Оновлено існуючий асет: {path}</color>");
            }
            else
            {
                AssetDatabase.CreateAsset(currentResult.levelData, path);
                AssetDatabase.SaveAssets();
                EditorGUIUtility.PingObject(currentResult.levelData);
                Debug.Log($"<color=green>LevelGenerator: Створено новий асет: {path}</color>");
            }
        }

        #endregion

        #region Tab 2: Batch Generator

        private void DrawBatchGenerator()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Пакетна генерація серії рівнів", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Генерує великий пакет розв'язуваних рівнів із плавним зростанням складності та зберігає в Assets/Levels.",
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

            EditorGUILayout.Space(4);
            batchUseCurve = EditorGUILayout.ToggleLeft("✨ Застосувати криву складності для кожного рівня", batchUseCurve);
            batchOverwrite = EditorGUILayout.ToggleLeft("⚠️ Перезаписувати існуючі файли .asset", batchOverwrite);
            batchAutoPopulateLoader = EditorGUILayout.ToggleLeft("🔄 Автоматично оновити список All Levels у LevelLoader", batchAutoPopulateLoader);
            batchSavePath = EditorGUILayout.TextField("Папка для збереження", batchSavePath);

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

                    LegoLevelGenerator.GenerationSettings s = batchUseCurve 
                        ? LegoLevelGenerator.GetCurveSettingsForLevel(lvl)
                        : CloneSettings(currentSettings, lvl);

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

        private LegoLevelGenerator.GenerationSettings CloneSettings(LegoLevelGenerator.GenerationSettings src, int newLvl)
        {
            var s = new LegoLevelGenerator.GenerationSettings
            {
                levelIndex = newLvl,
                levelTitle = $"Level {newLvl}",
                timeLimitSeconds = src.timeLimitSeconds,
                movesLimit = src.movesLimit,
                gridWidth = src.gridWidth,
                gridHeight = src.gridHeight,
                pieceCount = src.pieceCount,
                allowFreeMove = src.allowFreeMove,
                allowHorizontalOnly = src.allowHorizontalOnly,
                allowVerticalOnly = src.allowVerticalOnly,
                allowObstacles = src.allowObstacles,
                obstacleCount = src.obstacleCount,
                allowUniversalGates = src.allowUniversalGates,
                forceMatchingColorGates = src.forceMatchingColorGates,
                useShape1x1 = src.useShape1x1,
                useShape1x2 = src.useShape1x2,
                useShape1x3 = src.useShape1x3,
                useShape2x2 = src.useShape2x2,
                useShapeL = src.useShapeL,
                useShapeL2x2 = src.useShapeL2x2,
                useShapeT3x2 = src.useShapeT3x2,
                useShapeCross3x3 = src.useShapeCross3x3,
                allowedColors = new List<BlockColorType>(src.allowedColors),
                targetMinMoves = src.targetMinMoves,
                scrambleSteps = src.scrambleSteps,
                randomSeed = newLvl * 1337 + 42
            };
            return s;
        }

        #endregion

        #region Tab 3: Difficulty Curve Inspector

        private void DrawDifficultyCurveInspector()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Огляд кривої складності гри (Gentle Progression Curve)", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Перші рівні максимально прості, щоб гравець не відчував тиску, далі поступово складнішає, а в кінці стає дуже складно.",
                EditorStyles.miniLabel
            );
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(8);

            previewCurveLevel = EditorGUILayout.IntSlider("Перегляд параметрів для рівня", previewCurveLevel, 1, 60);
            var curve = LegoLevelGenerator.GetCurveSettingsForLevel(previewCurveLevel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Параметри Level {previewCurveLevel}:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"• Етап: {GetLevelStageDescription(previewCurveLevel)}");
            EditorGUILayout.LabelField($"• Розмір сітки: {curve.gridWidth} x {curve.gridHeight}");
            EditorGUILayout.LabelField($"• Кількість деталей: {curve.pieceCount}");
            EditorGUILayout.LabelField($"• Слайдери (Sliders): Horizontal={curve.allowHorizontalOnly}, Vertical={curve.allowVerticalOnly}");
            EditorGUILayout.LabelField($"• Перешкоди (Obstacles): {curve.obstacleCount}");
            EditorGUILayout.LabelField($"• Ворота: Універсальні={curve.allowUniversalGates}, Строгі={curve.forceMatchingColorGates}");
            EditorGUILayout.LabelField($"• Орієнтовна мін. к-сть ходів: {curve.targetMinMoves} кроків");
            EditorGUILayout.LabelField($"• Ліміт часу: {curve.timeLimitSeconds} сек");
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Зведена таблиця прогресії:", EditorStyles.boldLabel);

            DrawStageRow("Рівні 1–3", "Початковий (Туторіал)", "6x6", "2 деталі", "100% Free", "0", "2–3 ходи");
            DrawStageRow("Рівні 4–6", "Дуже простий", "6x7", "3 деталі", "100% Free", "0", "3–5 ходів");
            DrawStageRow("Рівні 7–12", "Легкий (Знайомство зі слайдерами)", "7x7", "3–4 деталі", "1 слайдер", "0", "5–7 ходів");
            DrawStageRow("Рівні 13–20", "Казуальний / Середній", "7x8", "4 деталі", "1–2 слайдери", "1 перешкода", "7–10 ходів");
            DrawStageRow("Рівні 21–35", "Тактичний / Складний", "8x9", "5 деталей", "2–3 слайдери", "2 перешкоди", "10–14 ходів");
            DrawStageRow("Рівні 36–50", "Дуже складний / Pro", "8x10", "6 деталей", "40–50% слайдери", "3 перешкоди", "14–18 ходів");
            DrawStageRow("Рівні 51+", "Експерт / Master", "8x11+", "7 деталей", "50%+ слайдери", "4+ перешкоди", "18–25+ ходів");
        }

        private void DrawStageRow(string range, string stage, string grid, string pieces, string sliders, string obs, string moves)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField(range, EditorStyles.boldLabel, GUILayout.Width(80));
            EditorGUILayout.LabelField(stage, GUILayout.Width(170));
            EditorGUILayout.LabelField(grid, GUILayout.Width(50));
            EditorGUILayout.LabelField(pieces, GUILayout.Width(70));
            EditorGUILayout.LabelField(sliders, GUILayout.Width(100));
            EditorGUILayout.LabelField(obs, GUILayout.Width(80));
            EditorGUILayout.LabelField(moves);
            EditorGUILayout.EndHorizontal();
        }

        private string GetLevelStageDescription(int lvl)
        {
            if (lvl <= 3) return "Етап 1: Туторіал / Надпростий (без стресу, прості рухи, універсальні ворота)";
            if (lvl <= 6) return "Етап 2: Простий (легке розсіювання, форми L, вільний рух)";
            if (lvl <= 12) return "Етап 3: Легкий із першим знайомством зі слайдерами (↔ або ↕)";
            if (lvl <= 20) return "Етап 4: Казуальний / Середній (перша дерев'яна перешкода, суворіші кольори воріт)";
            if (lvl <= 35) return "Етап 5: Тактичний / Складний (T-форми, 2 перешкоди, комбіновані маневри)";
            if (lvl <= 50) return "Етап 6: Дуже складний (коридори, хрести, висока щільність слайдерів)";
            return "Етап 7: Експерт / Майстер (максимальний інтелектуальний виклик, 7+ блоків)";
        }

        #endregion
    }
}
#endif
