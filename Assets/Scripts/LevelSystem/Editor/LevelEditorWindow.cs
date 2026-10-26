#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using LegoPuzzle.Data;
using LegoPuzzle.Runtime;

namespace LegoPuzzle.Editor
{
    public class LevelEditorWindow : EditorWindow
    {
        private enum PaintMode
        {
            CellBrush,
            PlacePiece,
            SelectPiece,
            ErasePiece
        }

        private LevelData targetLevel;
        private PaintMode currentMode = PaintMode.CellBrush;
        private CellType brushCellType = CellType.Walkable;
        private ExitDirection brushExitDir = ExitDirection.Up;
        private BlockColorType brushColorType = BlockColorType.Yellow;
        private int brushGateAlignment = 1; // 1 = Ліва/Верхня, 2 = Права/Нижня, 0 = По центру

        // Налаштування для розміщення фігури LEGO
        private LegoShapeDefinition selectedShape;
        private BlockColorType pieceColorType = BlockColorType.Red;
        private MoveRestriction pieceRestriction = MoveRestriction.Free;
        private int pieceRotation = 0;
        private bool pieceFlipHorizontal = false;
        private int pieceFreezeCount = 0;

        // Виділена деталь для редагування
        private LegoPieceData selectedPiece = null;
        private bool showPiecesListFoldout = false;

        private const string LevelsFolder = "Assets/Resources/Levels";

        private Vector2 scrollPos;
        private float previewCellSize = LevelGridRenderer.DEFAULT_CELL_SIZE;

        [MenuItem("Tools/LEGO Level Editor")]
        public static void OpenWindow()
        {
            var window = GetWindow<LevelEditorWindow>("LEGO Editor");
            window.minSize = new Vector2(500, 700);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("LEGO Puzzle Level Editor", EditorStyles.boldLabel);

            // Вибір / Створення рівня
            DrawLevelHeader();

            if (targetLevel == null)
            {
                EditorGUILayout.HelpBox("Оберіть існуючий LevelData або створіть новий для початку редагування.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(10);
            DrawSettingsPanel();

            EditorGUILayout.Space(10);
            DrawToolbarPanel();

            EditorGUILayout.Space(10);
            DrawInteractiveGrid();
        }

        private void DrawLevelHeader()
        {
            EditorGUILayout.BeginHorizontal();
            targetLevel = (LevelData)EditorGUILayout.ObjectField("Рівень (Asset)", targetLevel, typeof(LevelData), false);

            if (GUILayout.Button("Новий рівень", GUILayout.Width(100)))
            {
                CreateNewLevelAsset();
            }

            GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
            if (GUILayout.Button("Зберегти", GUILayout.Width(90)))
            {
                SaveCurrentLevel();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            if (targetLevel != null)
            {
                EditorGUILayout.BeginHorizontal();

                GUI.backgroundColor = new Color(0.4f, 0.8f, 1f);
                if (GUILayout.Button("Тестувати цей рівень у грі", GUILayout.Height(24)))
                {
                    TestCurrentLevelInGame();
                }

                GUI.backgroundColor = new Color(1f, 0.85f, 0.4f);
                if (GUILayout.Button("Скинути прогрес (на Рівень 1)", GUILayout.Height(24)))
                {
                    if (Runtime.SaveDataService.Instance != null && Runtime.SaveDataService.Instance.Data != null)
                    {
                        Runtime.SaveDataService.Instance.Data.currentLevelIndex = 0;
                        Runtime.SaveDataService.Instance.Save();
                    }
                    PlayerPrefs.DeleteKey("LEGO_CurrentLevelIndex");
                    PlayerPrefs.Save();
                    Debug.Log("<color=yellow>LevelEditor: Прогрес гри успішно скинуто на Рівень 1!</color>");
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.EndHorizontal();
            }
        }

        private void TestCurrentLevelInGame()
        {
            if (targetLevel == null) return;

            SaveCurrentLevel();

            var loader = FindAnyObjectByType<LevelLoader>();
            if (loader != null)
            {
                if (Application.isPlaying)
                {
                    loader.LoadLevel(targetLevel);
                }
                else
                {
                    UnityEditor.Undo.RecordObject(loader, "Set Test Level");
                    var so = new UnityEditor.SerializedObject(loader);
                    so.FindProperty("testLevelData").objectReferenceValue = targetLevel;
                    so.FindProperty("overrideWithTestLevel").boolValue = true;
                    so.ApplyModifiedProperties();
                    UnityEditor.EditorUtility.SetDirty(loader);
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(loader.gameObject.scene);
                }
                Debug.Log($"<color=green>LevelEditor: Рівень '{targetLevel.name}' призначено як активний тестовий у LevelLoader!</color>");
            }
            else
            {
                Debug.LogWarning("LevelEditor: На поточній сцені не знайдено LevelLoader.");
            }
        }

        private void DrawSettingsPanel()
        {
            EditorGUILayout.LabelField("Параметри рівня", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();

            targetLevel.levelIndex = EditorGUILayout.IntField("Номер рівня", targetLevel.levelIndex);
            targetLevel.levelTitle = EditorGUILayout.TextField("Назва", targetLevel.levelTitle);
            targetLevel.timeLimitSeconds = EditorGUILayout.FloatField("Час (сек)", targetLevel.timeLimitSeconds);

            int newWidth = EditorGUILayout.IntSlider("Ширина сітки", targetLevel.gridWidth, 4, 16);
            int newHeight = EditorGUILayout.IntSlider("Висота сітки", targetLevel.gridHeight, 4, 20);

            if (newWidth != targetLevel.gridWidth || newHeight != targetLevel.gridHeight)
            {
                targetLevel.gridWidth = newWidth;
                targetLevel.gridHeight = newHeight;
                targetLevel.EnsureGridCapacity();
            }

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(targetLevel);
                AssetDatabase.SaveAssets();
            }
        }

        private void DrawToolbarPanel()
        {
            EditorGUILayout.LabelField("Інструменти редагування", EditorStyles.boldLabel);
            currentMode = (PaintMode)GUILayout.Toolbar((int)currentMode, new string[] { "Пензель клітинок", "Поставити блок", "Вибір / Редагування блоку", "Видалити блок" });

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (currentMode == PaintMode.CellBrush)
            {
                brushCellType = (CellType)EditorGUILayout.EnumPopup("Тип клітинки", brushCellType);
                if (brushCellType == CellType.ExitGate)
                {
                    brushExitDir = (ExitDirection)EditorGUILayout.EnumPopup("Напрямок виходу", brushExitDir);
                    brushColorType = (BlockColorType)EditorGUILayout.EnumPopup("Колір воріт", brushColorType);
                }
                else if (brushCellType == CellType.HalfExitGate)
                {
                    brushExitDir = (ExitDirection)EditorGUILayout.EnumPopup("Напрямок виходу (Стрілка)", brushExitDir);
                    brushColorType = (BlockColorType)EditorGUILayout.EnumPopup("Колір воріт", brushColorType);

                    string[] halfOptions = new string[]
                    {
                        "Нижня половина (Horizontal Bottom  ▄) — для верхньої стіни",
                        "Верхня половина (Horizontal Top  ▀) — для нижньої стіни",
                        "Ліва половина (Vertical Left  ▌) — для правої стіни",
                        "Права половина (Vertical Right  ▐) — для лівої стіни"
                    };

                    int currentIdx = brushGateAlignment switch
                    {
                        1 => 0, // Bottom ▄
                        0 => 1, // Top ▀
                        2 => 2, // Left ▌
                        3 => 3, // Right ▐
                        _ => 0
                    };

                    int newIdx = EditorGUILayout.Popup("Положення половинки у клітинці", currentIdx, halfOptions);
                    brushGateAlignment = newIdx switch
                    {
                        0 => 1, // Bottom ▄
                        1 => 0, // Top ▀
                        2 => 2, // Left ▌
                        3 => 3, // Right ▐
                        _ => 1
                    };
                }
                else if (brushCellType == CellType.HalfObstacle)
                {
                    brushExitDir = (ExitDirection)EditorGUILayout.EnumPopup("Напрямок половинки", brushExitDir);
                }
                else if (brushCellType == CellType.QuarterObstacle)
                {
                    string[] cornerOptions = new string[]
                    {
                        "Низ-Право (Top-Left)  ▘",
                        "Низ-Ліво (Top-Right)  ▝",
                        "Верх-Право (Bottom-Left)  ▖",
                        "Верх-Ліво (Bottom-Right)  ▗"
                    };
                    int currentCornerIndex = brushExitDir switch
                    {
                        ExitDirection.Left => 0,
                        ExitDirection.Up => 1,
                        ExitDirection.Down => 2,
                        ExitDirection.Right => 3,
                        _ => 0
                    };
                    int newCornerIndex = EditorGUILayout.Popup("Куток для заповнення", currentCornerIndex, cornerOptions);
                    brushExitDir = newCornerIndex switch
                    {
                        0 => ExitDirection.Left,
                        1 => ExitDirection.Up,
                        2 => ExitDirection.Down,
                        3 => ExitDirection.Right,
                        _ => ExitDirection.Left
                    };
                }
            }
            else if (currentMode == PaintMode.PlacePiece)
            {
                // Швидкий вибір зі знайдених у проекті форм
                DrawShapeQuickSelection();

                selectedShape = (LegoShapeDefinition)EditorGUILayout.ObjectField("Обрана форма", selectedShape, typeof(LegoShapeDefinition), false);
                pieceColorType = (BlockColorType)EditorGUILayout.EnumPopup("Колір деталі", pieceColorType);
                pieceRestriction = (MoveRestriction)EditorGUILayout.EnumPopup("Обмеження руху", pieceRestriction);
                pieceRotation = EditorGUILayout.IntSlider($"Поворот ({pieceRotation * 90}°)", pieceRotation, 0, 3);
                pieceFlipHorizontal = EditorGUILayout.Toggle("Дзеркало по X (Flip X)", pieceFlipHorizontal);

                EditorGUILayout.Space(4);
                EditorGUILayout.BeginHorizontal();
                pieceFreezeCount = EditorGUILayout.IntField("❄ Лічильник заморозки", Mathf.Max(0, pieceFreezeCount));
                if (pieceFreezeCount > 0 && GUILayout.Button("Скинути на 0", GUILayout.Width(100)))
                {
                    pieceFreezeCount = 0;
                }
                EditorGUILayout.EndHorizontal();

                if (pieceFreezeCount > 0)
                {
                    EditorGUILayout.HelpBox($"Блок буде замороженим! Для розморозки потрібно знищити {pieceFreezeCount} інш. блоків.", MessageType.Info);
                }
            }
            else if (currentMode == PaintMode.SelectPiece)
            {
                if (selectedPiece == null)
                {
                    EditorGUILayout.HelpBox("Клікніть на будь-який блок на сітці нижче, щоб обрати та налаштувати його параметри.", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.LabelField($"Налаштування обраного блоку: {selectedPiece.pieceId}", EditorStyles.boldLabel);
                    EditorGUI.BeginChangeCheck();

                    selectedPiece.pieceId = EditorGUILayout.TextField("ID блоку", selectedPiece.pieceId);
                    selectedPiece.shape = (LegoShapeDefinition)EditorGUILayout.ObjectField("Форма", selectedPiece.shape, typeof(LegoShapeDefinition), false);
                    selectedPiece.colorType = (BlockColorType)EditorGUILayout.EnumPopup("Колір деталі", selectedPiece.colorType);
                    selectedPiece.moveRestriction = (MoveRestriction)EditorGUILayout.EnumPopup("Обмеження руху", selectedPiece.moveRestriction);
                    selectedPiece.rotationSteps = EditorGUILayout.IntSlider($"Поворот ({selectedPiece.rotationSteps * 90}°)", selectedPiece.rotationSteps, 0, 3);
                    selectedPiece.flipHorizontal = EditorGUILayout.Toggle("Дзеркало по X (Flip X)", selectedPiece.flipHorizontal);

                    EditorGUILayout.Space(4);
                    EditorGUILayout.BeginHorizontal();
                    selectedPiece.freezeCount = EditorGUILayout.IntField("❄ Лічильник заморозки", Mathf.Max(0, selectedPiece.freezeCount));
                    if (GUILayout.Button("0 (Розморозити)", GUILayout.Width(120)))
                    {
                        selectedPiece.freezeCount = 0;
                    }
                    EditorGUILayout.EndHorizontal();

                    if (selectedPiece.freezeCount > 0)
                    {
                        EditorGUILayout.HelpBox($"Блок заморожений! Він стане рухомим тільки після знищення {selectedPiece.freezeCount} інших блоків.", MessageType.Warning);
                    }

                    selectedPiece.isRequiredForWin = EditorGUILayout.Toggle("Потрібен для перемоги", selectedPiece.isRequiredForWin);

                    if (EditorGUI.EndChangeCheck())
                    {
                        EditorUtility.SetDirty(targetLevel);
                        AssetDatabase.SaveAssets();
                    }

                    EditorGUILayout.Space(6);
                    GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
                    if (GUILayout.Button("Видалити цей блок"))
                    {
                        targetLevel.pieces.Remove(selectedPiece);
                        selectedPiece = null;
                        EditorUtility.SetDirty(targetLevel);
                        AssetDatabase.SaveAssets();
                    }
                    GUI.backgroundColor = Color.white;
                }
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Заповнити все Walkable"))
            {
                FillAllCells(CellType.Walkable);
            }
            if (GUILayout.Button("Очистити сітку"))
            {
                FillAllCells(CellType.Empty);
            }
            if (GUILayout.Button("Видалити всі блоки"))
            {
                targetLevel.pieces.Clear();
                EditorUtility.SetDirty(targetLevel);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawShapeQuickSelection()
        {
            string[] guids = AssetDatabase.FindAssets("t:LegoShapeDefinition");
            if (guids.Length == 0) return;

            List<LegoShapeDefinition> shapes = new List<LegoShapeDefinition>();
            List<string> shapeNames = new List<string>();

            int selectedIndex = 0;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var shape = AssetDatabase.LoadAssetAtPath<LegoShapeDefinition>(path);
                if (shape != null)
                {
                    shapes.Add(shape);
                    shapeNames.Add(string.IsNullOrEmpty(shape.shapeId) ? shape.name : shape.shapeId);
                    if (shape == selectedShape)
                    {
                        selectedIndex = shapes.Count - 1;
                    }
                }
            }

            if (selectedShape == null && shapes.Count > 0)
            {
                selectedShape = shapes[0];
                selectedIndex = 0;
            }

            if (shapes.Count > 0)
            {
                int newIndex = EditorGUILayout.Popup("Швидкий вибір форми", selectedIndex, shapeNames.ToArray());
                if (newIndex >= 0 && newIndex < shapes.Count)
                {
                    selectedShape = shapes[newIndex];
                }
            }
        }

        private void DrawInteractiveGrid()
        {
            targetLevel.EnsureGridCapacity();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Інтерактивне поле (клікніть для редагування):", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("Масштаб (Zoom):", GUILayout.Width(110));
            previewCellSize = EditorGUILayout.Slider(previewCellSize, LevelGridRenderer.MIN_CELL_SIZE, LevelGridRenderer.MAX_CELL_SIZE, GUILayout.Width(140));
            EditorGUILayout.EndHorizontal();

            float viewHeight = Mathf.Min(430f, (targetLevel.gridHeight + 1) * (previewCellSize + 3f) + 24f);
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(viewHeight));

            // Малюємо сітку знизу вгору (як у системі координат Y)
            for (int y = targetLevel.gridHeight - 1; y >= 0; y--)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                for (int x = 0; x < targetLevel.gridWidth; x++)
                {
                    CellData cell = targetLevel.GetCell(x, y);
                    LegoPieceData pieceOnCell = GetPieceAt(x, y);

                    Rect cellRect = GUILayoutUtility.GetRect(previewCellSize, previewCellSize, GUILayout.Width(previewCellSize), GUILayout.Height(previewCellSize));
                    LevelGridRenderer.DrawCell(cellRect, cell, pieceOnCell, x, y, previewCellSize);

                    if (pieceOnCell != null && pieceOnCell == selectedPiece)
                    {
                        Color selBorder = new Color(1f, 0.9f, 0.1f, 1f);
                        EditorGUI.DrawRect(new Rect(cellRect.x, cellRect.y, cellRect.width, 2.5f), selBorder);
                        EditorGUI.DrawRect(new Rect(cellRect.x, cellRect.y, 2.5f, cellRect.height), selBorder);
                        EditorGUI.DrawRect(new Rect(cellRect.x, cellRect.yMax - 2.5f, cellRect.width, 2.5f), selBorder);
                        EditorGUI.DrawRect(new Rect(cellRect.xMax - 2.5f, cellRect.y, 2.5f, cellRect.height), selBorder);
                    }

                    if (GUI.Button(cellRect, GUIContent.none, GUIStyle.none))
                    {
                        OnCellClicked(x, y);
                    }
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            LevelGridRenderer.DrawLegend();

            DrawPiecesList();
        }

        private void DrawPiecesList()
        {
            if (targetLevel == null || targetLevel.pieces == null) return;

            EditorGUILayout.Space(6);
            showPiecesListFoldout = EditorGUILayout.Foldout(showPiecesListFoldout, $"📋 Список усіх блоків на рівні ({targetLevel.pieces.Count})", true, EditorStyles.foldoutHeader);
            if (!showPiecesListFoldout) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (targetLevel.pieces.Count == 0)
            {
                EditorGUILayout.LabelField("На рівні ще немає жодного блоку.", EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Блок / ID", EditorStyles.boldLabel, GUILayout.Width(100));
                EditorGUILayout.LabelField("Колір", EditorStyles.boldLabel, GUILayout.Width(75));
                EditorGUILayout.LabelField("Рух", EditorStyles.boldLabel, GUILayout.Width(80));
                EditorGUILayout.LabelField("❄ Заморозка", EditorStyles.boldLabel, GUILayout.Width(110));
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                for (int i = 0; i < targetLevel.pieces.Count; i++)
                {
                    var p = targetLevel.pieces[i];
                    if (p == null) continue;

                    EditorGUILayout.BeginHorizontal();
                    bool isSel = (p == selectedPiece);
                    if (isSel) GUI.backgroundColor = new Color(1f, 1f, 0.4f);

                    if (GUILayout.Button(isSel ? $"▶ {p.pieceId}" : p.pieceId, GUILayout.Width(100)))
                    {
                        selectedPiece = p;
                        currentMode = PaintMode.SelectPiece;
                    }
                    GUI.backgroundColor = Color.white;

                    EditorGUILayout.LabelField(p.colorType.ToString(), GUILayout.Width(75));
                    string restrName = p.moveRestriction switch
                    {
                        MoveRestriction.HorizontalOnly => "↔ Гориз.",
                        MoveRestriction.VerticalOnly => "↕ Верт.",
                        MoveRestriction.Locked => "🔒 Заблок.",
                        _ => "Вільний"
                    };
                    EditorGUILayout.LabelField(restrName, GUILayout.Width(80));

                    EditorGUI.BeginChangeCheck();
                    int newFreeze = EditorGUILayout.IntField(Mathf.Max(0, p.freezeCount), GUILayout.Width(50));
                    if (EditorGUI.EndChangeCheck())
                    {
                        p.freezeCount = newFreeze;
                        EditorUtility.SetDirty(targetLevel);
                    }

                    if (p.freezeCount > 0)
                    {
                        EditorGUILayout.LabelField("❄ Заморожений", EditorStyles.miniLabel, GUILayout.Width(95));
                    }
                    else
                    {
                        EditorGUILayout.LabelField("", GUILayout.Width(95));
                    }

                    GUILayout.FlexibleSpace();

                    GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
                    if (GUILayout.Button("✕", GUILayout.Width(25)))
                    {
                        if (selectedPiece == p) selectedPiece = null;
                        targetLevel.pieces.RemoveAt(i);
                        EditorUtility.SetDirty(targetLevel);
                        GUI.backgroundColor = Color.white;
                        break;
                    }
                    GUI.backgroundColor = Color.white;

                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void OnCellClicked(int x, int y)
        {
            Undo.RecordObject(targetLevel, "Modify Level Grid");

            if (currentMode == PaintMode.CellBrush)
            {
                CellData newCell = new CellData(new Vector2Int(x, y), brushCellType)
                {
                    exitDirection = brushExitDir,
                    gateColorType = brushColorType,
                    gateAlignment = brushGateAlignment
                };
                targetLevel.SetCell(x, y, newCell);
            }
            else if (currentMode == PaintMode.SelectPiece)
            {
                selectedPiece = GetPieceAt(x, y);
            }
            else if (currentMode == PaintMode.PlacePiece)
            {
                LegoPieceData existing = GetPieceAt(x, y);
                if (existing != null)
                {
                    selectedPiece = existing;
                }
                else
                {
                    // Створюємо нову деталь
                    LegoPieceData newPiece = new LegoPieceData
                    {
                        pieceId = $"Piece_{targetLevel.pieces.Count + 1}",
                        shape = selectedShape,
                        originPosition = new Vector2Int(x, y),
                        rotationSteps = pieceRotation,
                        flipHorizontal = pieceFlipHorizontal,
                        colorType = pieceColorType,
                        moveRestriction = pieceRestriction,
                        freezeCount = pieceFreezeCount
                    };

                    targetLevel.pieces.Add(newPiece);
                    selectedPiece = newPiece;
                }
            }
            else if (currentMode == PaintMode.ErasePiece)
            {
                LegoPieceData piece = GetPieceAt(x, y);
                if (piece != null)
                {
                    if (selectedPiece == piece) selectedPiece = null;
                    targetLevel.pieces.Remove(piece);
                }
            }

            EditorUtility.SetDirty(targetLevel);
            AssetDatabase.SaveAssets();
        }

        private LegoPieceData GetPieceAt(int x, int y)
        {
            Vector2Int pos = new Vector2Int(x, y);
            foreach (var piece in targetLevel.pieces)
            {
                if (piece.GetOccupiedGridCells().Contains(pos))
                {
                    return piece;
                }
            }
            return null;
        }

        private void FillAllCells(CellType type)
        {
            Undo.RecordObject(targetLevel, "Fill Cells");
            for (int y = 0; y < targetLevel.gridHeight; y++)
            {
                for (int x = 0; x < targetLevel.gridWidth; x++)
                {
                    targetLevel.SetCell(x, y, new CellData(new Vector2Int(x, y), type));
                }
            }
            EditorUtility.SetDirty(targetLevel);
            AssetDatabase.SaveAssets();
        }

        private void SaveCurrentLevel()
        {
            if (targetLevel != null)
            {
                GUI.FocusControl(null);

                string expectedName3 = $"Level_{targetLevel.levelIndex:D3}";
                string expectedNameRaw = $"Level_{targetLevel.levelIndex}";

                // Якщо номер рівня не відповідає назві файлу (наприклад, відкритий Level_001, а номер змінено на 14)
                if (targetLevel.name != expectedName3 && targetLevel.name != expectedNameRaw)
                {
                    bool saveAsNew = EditorUtility.DisplayDialog(
                        "Збереження рівня",
                        $"Поточний файл має назву '{targetLevel.name}', але номер рівня встановлено як {targetLevel.levelIndex}.\n\nСтворити новий файл '{expectedName3}.asset' під цей номер рівня?",
                        "Створити новий асет (Save As)",
                        "Перезаписати поточний"
                    );

                    if (saveAsNew)
                    {
                        EnsureLevelsFolderExists();
                        string newPath = $"{LevelsFolder}/{expectedName3}.asset";
                        if (AssetDatabase.LoadAssetAtPath<LevelData>(newPath) != null)
                        {
                            newPath = AssetDatabase.GenerateUniqueAssetPath(newPath);
                        }

                        LevelData copy = Instantiate(targetLevel);
                        copy.levelIndex = targetLevel.levelIndex;
                        copy.levelTitle = string.IsNullOrEmpty(targetLevel.levelTitle) ? $"Level {targetLevel.levelIndex}" : targetLevel.levelTitle;
                        AssetDatabase.CreateAsset(copy, newPath);
                        AssetDatabase.SaveAssets();
                        targetLevel = copy;
                        Selection.activeObject = copy;
                        Debug.Log($"<color=green>Рівень збережено як новий асет: {newPath}</color>");
                        return;
                    }
                }

                EditorUtility.SetDirty(targetLevel);
                AssetDatabase.SaveAssets();
                Debug.Log($"<color=green>Рівень '{targetLevel.name}' успішно збережено на диск!</color>");
            }
        }

        private void CreateNewLevelAsset()
        {
            EnsureLevelsFolderExists();

            int count = GetNextLevelNumber();
            string assetPath = $"{LevelsFolder}/Level_{count:D3}.asset";
            if (AssetDatabase.LoadAssetAtPath<LevelData>(assetPath) != null)
            {
                assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
            }

            LevelData newLevel = ScriptableObject.CreateInstance<LevelData>();
            newLevel.levelIndex = count;
            newLevel.levelTitle = $"Level {count}";
            newLevel.EnsureGridCapacity();

            AssetDatabase.CreateAsset(newLevel, assetPath);
            AssetDatabase.SaveAssets();

            targetLevel = newLevel;
            Selection.activeObject = newLevel;
            Debug.Log($"Створено новий рівень: {assetPath}");
        }

        private static void EnsureLevelsFolderExists()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            if (!AssetDatabase.IsValidFolder(LevelsFolder))
            {
                AssetDatabase.CreateFolder("Assets/Resources", "Levels");
            }
        }

        private static int GetNextLevelNumber()
        {
            int max = 0;
            string[] guids = AssetDatabase.FindAssets("t:LevelData");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                LevelData level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
                if (level != null && level.levelIndex > max)
                {
                    max = level.levelIndex;
                }
            }

            return max + 1;
        }
    }
}
#endif
