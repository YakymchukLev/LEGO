#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using LegoPuzzle.Data;

namespace LegoPuzzle.Editor
{
    public class LevelEditorWindow : EditorWindow
    {
        private enum PaintMode
        {
            CellBrush,
            PlacePiece,
            ErasePiece
        }

        private LevelData targetLevel;
        private PaintMode currentMode = PaintMode.CellBrush;
        private CellType brushCellType = CellType.Walkable;
        private ExitDirection brushExitDir = ExitDirection.Up;
        private BlockColorType brushColorType = BlockColorType.Yellow;

        // Налаштування для розміщення фігури LEGO
        private LegoShapeDefinition selectedShape;
        private BlockColorType pieceColorType = BlockColorType.Red;
        private MoveRestriction pieceRestriction = MoveRestriction.Free;
        private int pieceRotation = 0;

        private Vector2 scrollPos;
        private const float CELL_GUI_SIZE = 36f;

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

            if (GUILayout.Button("Новий рівень", GUILayout.Width(110)))
            {
                CreateNewLevelAsset();
            }
            EditorGUILayout.EndHorizontal();
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
            }
        }

        private void DrawToolbarPanel()
        {
            EditorGUILayout.LabelField("Інструменти редагування", EditorStyles.boldLabel);
            currentMode = (PaintMode)GUILayout.Toolbar((int)currentMode, new string[] { "Пензель клітинок", "Поставити блок", "Видалити блок" });

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (currentMode == PaintMode.CellBrush)
            {
                brushCellType = (CellType)EditorGUILayout.EnumPopup("Тип клітинки", brushCellType);
                if (brushCellType == CellType.ExitGate)
                {
                    brushExitDir = (ExitDirection)EditorGUILayout.EnumPopup("Напрямок виходу", brushExitDir);
                    brushColorType = (BlockColorType)EditorGUILayout.EnumPopup("Колір воріт", brushColorType);
                }
            }
            else if (currentMode == PaintMode.PlacePiece)
            {
                // Швидкий вибір зі знайдених у проекті форм
                DrawShapeQuickSelection();

                selectedShape = (LegoShapeDefinition)EditorGUILayout.ObjectField("Обрана форма", selectedShape, typeof(LegoShapeDefinition), false);
                pieceColorType = (BlockColorType)EditorGUILayout.EnumPopup("Колір деталі", pieceColorType);
                pieceRestriction = (MoveRestriction)EditorGUILayout.EnumPopup("Обмеження руху", pieceRestriction);
                pieceRotation = EditorGUILayout.IntSlider("Поворот (0-3)", pieceRotation, 0, 3);
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

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            // Малюємо сітку знизу вгору (як у системі координат Y)
            for (int y = targetLevel.gridHeight - 1; y >= 0; y--)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                for (int x = 0; x < targetLevel.gridWidth; x++)
                {
                    CellData cell = targetLevel.GetCell(x, y);
                    LegoPieceData pieceOnCell = GetPieceAt(x, y);

                    Color originalBg = GUI.backgroundColor;

                    // Колір кнопки залежно від вмісту
                    if (pieceOnCell != null)
                    {
                        GUI.backgroundColor = pieceOnCell.GetColor();
                    }
                    else
                    {
                        GUI.backgroundColor = cell.cellType switch
                        {
                            CellType.Walkable => new Color(0.95f, 0.90f, 0.55f),
                            CellType.Obstacle => new Color(0.55f, 0.35f, 0.20f),
                            CellType.ExitGate => cell.GetEffectiveColor(),
                            _ => new Color(0.2f, 0.2f, 0.2f, 0.3f)
                        };
                    }

                    string label = GetCellLabel(cell, pieceOnCell);

                    if (GUILayout.Button(label, GUILayout.Width(CELL_GUI_SIZE), GUILayout.Height(CELL_GUI_SIZE)))
                    {
                        OnCellClicked(x, y);
                    }

                    GUI.backgroundColor = originalBg;
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        private string GetCellLabel(CellData cell, LegoPieceData piece)
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
                CellType.ExitGate => (cell.gateColorType == BlockColorType.Universal ? "★" : "") + cell.exitDirection switch
                {
                    ExitDirection.Up => "▲",
                    ExitDirection.Down => "▼",
                    ExitDirection.Left => "◀",
                    ExitDirection.Right => "▶",
                    _ => "O"
                },
                CellType.Walkable => "+",
                _ => ""
            };
        }

        private void OnCellClicked(int x, int y)
        {
            Undo.RecordObject(targetLevel, "Modify Level Grid");

            if (currentMode == PaintMode.CellBrush)
            {
                CellData newCell = new CellData(new Vector2Int(x, y), brushCellType)
                {
                    exitDirection = brushExitDir,
                    gateColorType = brushColorType
                };
                targetLevel.SetCell(x, y, newCell);
            }
            else if (currentMode == PaintMode.PlacePiece)
            {
                // Створюємо нову деталь
                LegoPieceData newPiece = new LegoPieceData
                {
                    pieceId = $"Piece_{targetLevel.pieces.Count + 1}",
                    shape = selectedShape,
                    originPosition = new Vector2Int(x, y),
                    rotationSteps = pieceRotation,
                    colorType = pieceColorType,
                    moveRestriction = pieceRestriction
                };

                targetLevel.pieces.Add(newPiece);
            }
            else if (currentMode == PaintMode.ErasePiece)
            {
                LegoPieceData piece = GetPieceAt(x, y);
                if (piece != null)
                {
                    targetLevel.pieces.Remove(piece);
                }
            }

            EditorUtility.SetDirty(targetLevel);
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
        }

        private void CreateNewLevelAsset()
        {
            string folderPath = "Assets/Levels";
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            int count = Directory.GetFiles(folderPath, "Level_*.asset").Length + 1;
            string assetPath = $"{folderPath}/Level_{count:D3}.asset";

            LevelData newLevel = ScriptableObject.CreateInstance<LevelData>();
            newLevel.levelIndex = count;
            newLevel.levelTitle = $"Рівень {count}";
            newLevel.EnsureGridCapacity();

            AssetDatabase.CreateAsset(newLevel, assetPath);
            AssetDatabase.SaveAssets();

            targetLevel = newLevel;
            Selection.activeObject = newLevel;
            Debug.Log($"Створено новий рівень: {assetPath}");
        }
    }
}
#endif
