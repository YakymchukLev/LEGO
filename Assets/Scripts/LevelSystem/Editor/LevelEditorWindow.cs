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
        private bool pieceIsDoubleBlock = false;
        private BlockColorType pieceInnerColorType = BlockColorType.Blue;
        private bool pieceIsMovableBarrier = false;

        // Виділена деталь для редагування
        private LegoPieceData selectedPiece = null;
        private bool showPiecesListFoldout = false;

        // Перетягування блоку в режимі SelectPiece
        private bool isDraggingSelectedPiece;
        private Vector2Int dragGrabOffset;
        private Vector2Int? dragHoverCell;
        private Vector2Int dragProposedOrigin;
        private bool dragPlacementValid;
        private bool dragHasHover;

        private const string LevelsFolder = "Assets/Resources/Levels";

        private Vector2 scrollPos;
        private float previewCellSize = LevelGridRenderer.DEFAULT_CELL_SIZE;

        private bool isFullscreen;
        private Rect windowedPosition = new Rect(80f, 60f, 980f, 820f);

        private static LevelData pendingLevel;
        private static bool pendingOpenFullscreen;

        [MenuItem("Tools/LEGO Level Editor", false, 1)]
        public static void OpenWindow()
        {
            OpenWindowInternal(fullscreen: false);
        }

        [MenuItem("Tools/LEGO Level Editor (Fullscreen)", false, 2)]
        public static void OpenWindowFullscreen()
        {
            OpenWindowInternal(fullscreen: true);
        }

        [MenuItem("LEGO/Level Editor", false, 1)]
        public static void OpenWindowFromLegoMenu()
        {
            OpenWindowInternal(fullscreen: false);
        }

        [MenuItem("LEGO/Level Editor (Fullscreen)", false, 2)]
        public static void OpenWindowFullscreenFromLegoMenu()
        {
            OpenWindowInternal(fullscreen: true);
        }

        private static void OpenWindowInternal(bool fullscreen)
        {
            // Close a stuck/off-screen docked instance, then show a floating window on screen.
            LevelEditorWindow[] open = Resources.FindObjectsOfTypeAll<LevelEditorWindow>();
            for (int i = 0; i < open.Length; i++)
            {
                if (open[i] != null)
                    open[i].Close();
            }

            pendingOpenFullscreen = fullscreen;

            var window = CreateInstance<LevelEditorWindow>();
            window.titleContent = new GUIContent("LEGO Editor");
            window.minSize = new Vector2(640f, 720f);
            window.windowedPosition = new Rect(80f, 60f, 980f, 820f);
            window.position = window.windowedPosition;
            window.ShowUtility();
            window.Focus();

            if (fullscreen)
                window.ApplyFullscreen(true);
        }

        [UnityEditor.Callbacks.OnOpenAsset(1)]
        public static bool OnOpenLevelAsset(int instanceID, int line)
        {
            var level = EditorUtility.InstanceIDToObject(instanceID) as LevelData;
            if (level == null) return false;

            pendingLevel = level;
            OpenWindow();
            return true;
        }

        private void OnEnable()
        {
            if (pendingLevel != null && IsValidLevelAsset(pendingLevel))
                targetLevel = pendingLevel;
            pendingLevel = null;
            ResolveTargetLevel();

            if (pendingOpenFullscreen)
            {
                pendingOpenFullscreen = false;
                EditorApplication.delayCall += () =>
                {
                    if (this != null)
                        ApplyFullscreen(true);
                };
            }
        }

        private void ResolveTargetLevel()
        {
            if (!IsValidLevelAsset(targetLevel))
                targetLevel = null;

            if (targetLevel == null && Selection.activeObject is LevelData selected && IsValidLevelAsset(selected))
                targetLevel = selected;

            if (targetLevel == null)
            {
                LevelData fromResources = AssetDatabase.LoadAssetAtPath<LevelData>($"{LevelsFolder}/Level_001.asset");
                if (IsValidLevelAsset(fromResources))
                    targetLevel = fromResources;
            }

            selectedPiece = null;
            CancelPieceDrag();
        }

        private static bool IsValidLevelAsset(LevelData level)
        {
            if (level == null) return false;
            string path = AssetDatabase.GetAssetPath(level);
            return !string.IsNullOrEmpty(path) && AssetDatabase.LoadAssetAtPath<LevelData>(path) != null;
        }

        private void HandleFullscreenHotkeys()
        {
            Event e = Event.current;
            if (e == null || e.type != EventType.KeyDown) return;

            if (e.keyCode == KeyCode.F11)
            {
                ToggleFullscreen();
                e.Use();
            }
            else if (isFullscreen && e.keyCode == KeyCode.Escape)
            {
                ApplyFullscreen(false);
                e.Use();
            }
        }

        private void ToggleFullscreen()
        {
            ApplyFullscreen(!isFullscreen);
        }

        private void ApplyFullscreen(bool fullscreen)
        {
            if (fullscreen == isFullscreen && fullscreen)
            {
                // Re-apply in case the main editor window moved/resized.
                position = GetFullscreenRect();
                return;
            }

            if (fullscreen)
            {
                windowedPosition = position;
                // Avoid saving a tiny/invalid rect if we were already expanding.
                if (windowedPosition.width < 200f || windowedPosition.height < 200f)
                    windowedPosition = new Rect(80f, 60f, 980f, 820f);

                position = GetFullscreenRect();
                isFullscreen = true;
            }
            else
            {
                position = windowedPosition;
                isFullscreen = false;
            }

            Repaint();
        }

        private static Rect GetFullscreenRect()
        {
            // Fill the Unity Editor main window — best "fullscreen" for a utility editor.
            Rect main = EditorGUIUtility.GetMainWindowPosition();
            if (main.width > 100f && main.height > 100f)
                return main;

            return new Rect(0f, 0f, Screen.currentResolution.width, Screen.currentResolution.height);
        }

        private void OnGUI()
        {
            HandleFullscreenHotkeys();

            if (!IsValidLevelAsset(targetLevel))
                targetLevel = null;

            EditorGUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("LEGO Puzzle Level Editor", EditorStyles.boldLabel);

            string fsLabel = isFullscreen ? "⤓ Вікно" : "⛶ Повний екран";
            if (GUILayout.Button(new GUIContent(fsLabel, "F11 — повний екран / вікно"), GUILayout.Width(130), GUILayout.Height(22)))
                ToggleFullscreen();
            EditorGUILayout.EndHorizontal();

            DrawLevelHeader();

            if (targetLevel == null)
            {
                EditorGUILayout.HelpBox(
                    "Оберіть LevelData з Assets/Resources/Levels або натисни «Новий рівень».",
                    MessageType.Info);
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

            List<LevelData> levels = GetSortedLevelAssets();
            int currentIdx = targetLevel != null ? levels.FindIndex(l => l == targetLevel) : -1;
            bool canPrev = currentIdx > 0 || (currentIdx < 0 && levels.Count > 0);
            bool canNext = (currentIdx >= 0 && currentIdx < levels.Count - 1)
                           || (currentIdx < 0 && levels.Count > 0);

            EditorGUI.BeginDisabledGroup(!canPrev);
            if (GUILayout.Button(new GUIContent("◀", "Попередній рівень"), GUILayout.Width(28), GUILayout.Height(20)))
                NavigateAdjacentLevel(-1, levels);
            EditorGUI.EndDisabledGroup();

            LevelData picked = (LevelData)EditorGUILayout.ObjectField("Рівень (Asset)", targetLevel, typeof(LevelData), false);
            if (picked != targetLevel)
            {
                targetLevel = IsValidLevelAsset(picked) ? picked : null;
                selectedPiece = null;
                CancelPieceDrag();
            }

            EditorGUI.BeginDisabledGroup(!canNext);
            if (GUILayout.Button(new GUIContent("▶", "Наступний рівень"), GUILayout.Width(28), GUILayout.Height(20)))
                NavigateAdjacentLevel(+1, levels);
            EditorGUI.EndDisabledGroup();

            if (levels.Count > 0)
            {
                string posLabel = currentIdx >= 0
                    ? $"{currentIdx + 1} / {levels.Count}"
                    : $"— / {levels.Count}";
                EditorGUILayout.LabelField(posLabel, GUILayout.Width(64));
            }

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
                    if (Runtime.SaveDataService.HasInstance
                        && Runtime.SaveDataService.Instance != null
                        && Runtime.SaveDataService.Instance.Data != null)
                    {
                        Runtime.SaveDataService.Instance.Data.currentLevelIndex = 0;
                        Runtime.SaveDataService.Instance.Save();
                    }
                    PlayerPrefs.DeleteKey("LEGO_CurrentLevelIndex");
                    PlayerPrefs.SetInt("LEGO_CurrentLevelIndex", 0);
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

                EditorGUILayout.Space(4);
                bool wasBarrier = pieceIsMovableBarrier;
                pieceIsMovableBarrier = EditorGUILayout.ToggleLeft("🧱 Рухомий бар'єр (колір стін, не виводиться)", pieceIsMovableBarrier);
                if (pieceIsMovableBarrier)
                {
                    pieceIsDoubleBlock = false;
                    if (pieceRestriction == MoveRestriction.Locked)
                        pieceRestriction = MoveRestriction.Free;
                    if (!wasBarrier || selectedShape == null)
                        TryAssignDefaultBarrierShape();

                    pieceRestriction = (MoveRestriction)EditorGUILayout.EnumPopup("Обмеження руху", pieceRestriction);
                    if (pieceRestriction == MoveRestriction.Locked)
                        pieceRestriction = MoveRestriction.Free;

                    pieceRotation = EditorGUILayout.IntSlider($"Поворот ({pieceRotation * 90}°)", pieceRotation, 0, 3);
                    pieceFlipHorizontal = EditorGUILayout.Toggle("Дзеркало по X (Flip X)", pieceFlipHorizontal);

                    EditorGUILayout.Space(4);
                    EditorGUILayout.BeginHorizontal();
                    pieceFreezeCount = EditorGUILayout.IntField("❄ Лічильник заморозки", Mathf.Max(0, pieceFreezeCount));
                    if (pieceFreezeCount > 0 && GUILayout.Button("Скинути на 0", GUILayout.Width(100)))
                        pieceFreezeCount = 0;
                    EditorGUILayout.EndHorizontal();

                    if (pieceFreezeCount > 0)
                        EditorGUILayout.HelpBox($"Бар'єр буде замороженим! Стане рухомим після знищення {pieceFreezeCount} інш. блоків.", MessageType.Info);

                    EditorGUILayout.HelpBox("Бар'єр блокує шлях, не виходить у ворота і не потрібен для перемоги. Може бути ❄, слайдером ↔/↕ і обертатися як звичайний блок.", MessageType.Info);
                }
                else
                {
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

                    EditorGUILayout.Space(4);
                    pieceIsDoubleBlock = EditorGUILayout.ToggleLeft("▣ Подвійний блок (2 шари)", pieceIsDoubleBlock);
                    if (pieceIsDoubleBlock)
                    {
                        pieceInnerColorType = (BlockColorType)EditorGUILayout.EnumPopup("Колір нижнього шару", pieceInnerColorType);
                        EditorGUILayout.HelpBox("У вирізі видно колір нижнього шару. Перше знищення знімає лише верх — низ стає звичайним блоком.", MessageType.Info);
                    }
                }
            }
            else if (currentMode == PaintMode.SelectPiece)
            {
                if (selectedPiece == null)
                {
                    EditorGUILayout.HelpBox(
                        "Клікніть на блок, щоб обрати його. Перетягніть блок на іншу клітинку, щоб змінити позицію.",
                        MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        isDraggingSelectedPiece
                            ? "Відпустіть кнопку миші на цільовій клітинці, щоб перемістити блок. Esc — скасувати."
                            : "Перетягніть обраний блок на сітці, щоб змінити його origin-позицію.",
                        MessageType.None);
                    EditorGUILayout.LabelField($"Налаштування обраного блоку: {selectedPiece.pieceId}", EditorStyles.boldLabel);
                    EditorGUI.BeginChangeCheck();

                    selectedPiece.pieceId = EditorGUILayout.TextField("ID блоку", selectedPiece.pieceId);
                    selectedPiece.shape = (LegoShapeDefinition)EditorGUILayout.ObjectField("Форма", selectedPiece.shape, typeof(LegoShapeDefinition), false);

                    selectedPiece.isMovableBarrier = EditorGUILayout.ToggleLeft("🧱 Рухомий бар'єр", selectedPiece.isMovableBarrier);
                    if (selectedPiece.isMovableBarrier)
                    {
                        selectedPiece.isRequiredForWin = false;
                        selectedPiece.isDoubleBlock = false;
                        if (selectedPiece.moveRestriction == MoveRestriction.Locked)
                            selectedPiece.moveRestriction = MoveRestriction.Free;

                        selectedPiece.moveRestriction = (MoveRestriction)EditorGUILayout.EnumPopup("Обмеження руху", selectedPiece.moveRestriction);
                        if (selectedPiece.moveRestriction == MoveRestriction.Locked)
                            selectedPiece.moveRestriction = MoveRestriction.Free;

                        selectedPiece.rotationSteps = EditorGUILayout.IntSlider($"Поворот ({selectedPiece.rotationSteps * 90}°)", selectedPiece.rotationSteps, 0, 3);
                        selectedPiece.flipHorizontal = EditorGUILayout.Toggle("Дзеркало по X (Flip X)", selectedPiece.flipHorizontal);

                        EditorGUILayout.Space(4);
                        EditorGUILayout.BeginHorizontal();
                        selectedPiece.freezeCount = EditorGUILayout.IntField("❄ Лічильник заморозки", Mathf.Max(0, selectedPiece.freezeCount));
                        if (GUILayout.Button("0 (Розморозити)", GUILayout.Width(120)))
                            selectedPiece.freezeCount = 0;
                        EditorGUILayout.EndHorizontal();

                        if (selectedPiece.freezeCount > 0)
                            EditorGUILayout.HelpBox($"Бар'єр заморожений! Стане рухомим після знищення {selectedPiece.freezeCount} інших блоків.", MessageType.Warning);

                        EditorGUILayout.HelpBox("Колір як у стін. Не виводиться у ворота, не потрібен для перемоги. Підтримує ❄, слайдер ↔/↕, поворот і дзеркало.", MessageType.Info);
                    }
                    else
                    {
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

                        EditorGUILayout.Space(4);
                        selectedPiece.isDoubleBlock = EditorGUILayout.ToggleLeft("▣ Подвійний блок (2 шари)", selectedPiece.isDoubleBlock);
                        if (selectedPiece.isDoubleBlock)
                        {
                            selectedPiece.innerColorType = (BlockColorType)EditorGUILayout.EnumPopup("Колір нижнього шару", selectedPiece.innerColorType);
                            EditorGUILayout.HelpBox("Перше знищення (вихід / молоток) знімає верхній шар; низ лишається як звичайний блок.", MessageType.Info);
                        }

                        selectedPiece.isRequiredForWin = EditorGUILayout.Toggle("Потрібен для перемоги", selectedPiece.isRequiredForWin);
                    }

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
                        CancelPieceDrag();
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

            if (currentMode != PaintMode.SelectPiece && isDraggingSelectedPiece)
                CancelPieceDrag();

            HandleSelectPieceDragHotkeys();

            EditorGUILayout.BeginHorizontal();
            string gridHint = currentMode == PaintMode.SelectPiece
                ? "Інтерактивне поле (перетягніть блок, щоб змінити позицію):"
                : "Інтерактивне поле (клікніть для редагування):";
            EditorGUILayout.LabelField(gridHint, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("Масштаб (Zoom):", GUILayout.Width(110));
            previewCellSize = EditorGUILayout.Slider(previewCellSize, LevelGridRenderer.MIN_CELL_SIZE, LevelGridRenderer.MAX_CELL_SIZE, GUILayout.Width(140));
            EditorGUILayout.EndHorizontal();

            int gridW = Mathf.Clamp(targetLevel.gridWidth, 1, 24);
            int gridH = Mathf.Clamp(targetLevel.gridHeight, 1, 24);
            float viewHeight = Mathf.Min(430f, (gridH + 1) * (previewCellSize + 3f) + 24f);
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(viewHeight));

            Event e = Event.current;
            int dragControlId = GUIUtility.GetControlID(FocusType.Passive);
            var cellRects = currentMode == PaintMode.SelectPiece
                ? new List<(Rect rect, int x, int y, LegoPieceData piece)>(targetLevel.gridWidth * targetLevel.gridHeight)
                : null;

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

                    if (cellRects != null)
                        cellRects.Add((cellRect, x, y, pieceOnCell));
                    else if (GUI.Button(cellRect, GUIContent.none, GUIStyle.none))
                        OnCellClicked(x, y);
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            if (currentMode == PaintMode.SelectPiece && cellRects != null)
            {
                UpdateSelectPieceDragHover(e, cellRects);
                DrawSelectPieceDragPreview(cellRects);
                HandleSelectPieceDragInput(e, dragControlId, cellRects);
            }

            EditorGUILayout.EndScrollView();

            LevelGridRenderer.DrawLegend();

            DrawPiecesList();
        }

        private void HandleSelectPieceDragHotkeys()
        {
            Event e = Event.current;
            if (e == null || e.type != EventType.KeyDown) return;
            if (e.keyCode == KeyCode.Escape && isDraggingSelectedPiece)
            {
                CancelPieceDrag();
                e.Use();
                Repaint();
            }
        }

        private void UpdateSelectPieceDragHover(Event e, List<(Rect rect, int x, int y, LegoPieceData piece)> cellRects)
        {
            dragHasHover = false;
            if (e == null) return;

            for (int i = 0; i < cellRects.Count; i++)
            {
                if (!cellRects[i].rect.Contains(e.mousePosition))
                    continue;

                dragHoverCell = new Vector2Int(cellRects[i].x, cellRects[i].y);
                dragHasHover = true;
                break;
            }

            if (isDraggingSelectedPiece && selectedPiece != null && dragHasHover && dragHoverCell.HasValue)
            {
                dragProposedOrigin = dragHoverCell.Value - dragGrabOffset;
                dragPlacementValid = CanPlacePieceAt(selectedPiece, dragProposedOrigin);
            }
            else
            {
                dragPlacementValid = false;
            }
        }

        private void DrawSelectPieceDragPreview(List<(Rect rect, int x, int y, LegoPieceData piece)> cellRects)
        {
            if (!isDraggingSelectedPiece || selectedPiece == null || !dragHasHover)
                return;

            HashSet<Vector2Int> previewCells = new HashSet<Vector2Int>(GetOccupiedAt(selectedPiece, dragProposedOrigin));
            Color preview = dragPlacementValid
                ? new Color(0.2f, 1f, 0.35f, 0.35f)
                : new Color(1f, 0.2f, 0.2f, 0.4f);

            for (int i = 0; i < cellRects.Count; i++)
            {
                if (previewCells.Contains(new Vector2Int(cellRects[i].x, cellRects[i].y)))
                    EditorGUI.DrawRect(cellRects[i].rect, preview);
            }
        }

        private void HandleSelectPieceDragInput(
            Event e,
            int dragControlId,
            List<(Rect rect, int x, int y, LegoPieceData piece)> cellRects)
        {
            if (e == null) return;

            if (e.type == EventType.MouseDown && e.button == 0 && dragHasHover && dragHoverCell.HasValue)
            {
                LegoPieceData pieceOnCell = null;
                for (int i = 0; i < cellRects.Count; i++)
                {
                    if (cellRects[i].x == dragHoverCell.Value.x && cellRects[i].y == dragHoverCell.Value.y)
                    {
                        pieceOnCell = cellRects[i].piece;
                        break;
                    }
                }

                if (pieceOnCell != null)
                {
                    selectedPiece = pieceOnCell;
                    isDraggingSelectedPiece = true;
                    dragGrabOffset = dragHoverCell.Value - selectedPiece.originPosition;
                    dragProposedOrigin = selectedPiece.originPosition;
                    dragPlacementValid = true;
                    GUIUtility.hotControl = dragControlId;
                    e.Use();
                    Repaint();
                }
                else
                {
                    selectedPiece = null;
                    CancelPieceDrag();
                    e.Use();
                    Repaint();
                }

                return;
            }

            if (isDraggingSelectedPiece && (e.type == EventType.MouseDrag || e.type == EventType.MouseMove))
            {
                Repaint();
                return;
            }

            if (e.type != EventType.MouseUp || e.button != 0) return;
            if (GUIUtility.hotControl != dragControlId && !isDraggingSelectedPiece) return;

            if (isDraggingSelectedPiece && selectedPiece != null && dragHasHover && dragHoverCell.HasValue)
            {
                Vector2Int newOrigin = dragHoverCell.Value - dragGrabOffset;
                if (newOrigin != selectedPiece.originPosition && CanPlacePieceAt(selectedPiece, newOrigin))
                {
                    Undo.RecordObject(targetLevel, "Move Piece");
                    selectedPiece.originPosition = newOrigin;
                    EditorUtility.SetDirty(targetLevel);
                    AssetDatabase.SaveAssets();
                }
            }

            if (GUIUtility.hotControl == dragControlId)
                GUIUtility.hotControl = 0;

            CancelPieceDrag();
            e.Use();
            Repaint();
        }

        private void CancelPieceDrag()
        {
            isDraggingSelectedPiece = false;
            dragHoverCell = null;
            dragHasHover = false;
            dragPlacementValid = false;
        }

        private static List<Vector2Int> GetOccupiedAt(LegoPieceData piece, Vector2Int origin)
        {
            var occupied = new List<Vector2Int>();
            if (piece == null)
                return occupied;

            var offsets = piece.GetOrientedOffsets();
            for (int i = 0; i < offsets.Count; i++)
                occupied.Add(origin + offsets[i]);

            return occupied;
        }

        private bool CanPlacePieceAt(LegoPieceData piece, Vector2Int newOrigin)
        {
            if (piece == null || targetLevel == null) return false;

            List<Vector2Int> occupied = GetOccupiedAt(piece, newOrigin);
            for (int i = 0; i < occupied.Count; i++)
            {
                Vector2Int cell = occupied[i];
                if (cell.x < 0 || cell.y < 0
                    || cell.x >= targetLevel.gridWidth
                    || cell.y >= targetLevel.gridHeight)
                {
                    return false;
                }

                // Блоки можна ставити лише на Walkable — не на стіни / Obstacle / Empty / ворота.
                if (!IsCellAllowedForPiece(targetLevel.GetCell(cell.x, cell.y).cellType))
                    return false;
            }

            if (targetLevel.pieces == null) return true;

            for (int i = 0; i < targetLevel.pieces.Count; i++)
            {
                LegoPieceData other = targetLevel.pieces[i];
                if (other == null || other == piece) continue;

                List<Vector2Int> otherCells = other.GetOccupiedGridCells();
                for (int c = 0; c < occupied.Count; c++)
                {
                    if (otherCells.Contains(occupied[c]))
                        return false;
                }
            }

            return true;
        }

        private static bool IsCellAllowedForPiece(CellType cellType)
        {
            return cellType == CellType.Walkable;
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

                    EditorGUILayout.LabelField(p.isMovableBarrier ? "🧱 Бар'єр" : p.colorType.ToString(), GUILayout.Width(75));
                    string restrName = p.moveRestriction switch
                    {
                        MoveRestriction.HorizontalOnly => "↔ Гориз.",
                        MoveRestriction.VerticalOnly => "↕ Верт.",
                        MoveRestriction.Locked => "🔒 Заблок.",
                        _ => p.isMovableBarrier ? "🧱 Вільний" : "Вільний"
                    };
                    EditorGUILayout.LabelField(restrName, GUILayout.Width(80));

                    EditorGUI.BeginChangeCheck();
                    int newFreeze = EditorGUILayout.IntField(Mathf.Max(0, p.freezeCount), GUILayout.Width(50));
                    if (EditorGUI.EndChangeCheck())
                    {
                        p.freezeCount = newFreeze;
                        EditorUtility.SetDirty(targetLevel);
                    }

                    if (p.isMovableBarrier && p.freezeCount > 0)
                    {
                        EditorGUILayout.LabelField("🧱❄ Бар'єр", EditorStyles.miniLabel, GUILayout.Width(95));
                    }
                    else if (p.isMovableBarrier)
                    {
                        EditorGUILayout.LabelField("🧱 Не для win", EditorStyles.miniLabel, GUILayout.Width(95));
                    }
                    else if (p.freezeCount > 0)
                    {
                        EditorGUILayout.LabelField("❄ Заморожений", EditorStyles.miniLabel, GUILayout.Width(95));
                    }
                    else if (p.isDoubleBlock)
                    {
                        EditorGUILayout.LabelField($"▣→{p.innerColorType}", EditorStyles.miniLabel, GUILayout.Width(95));
                    }
                    else
                    {
                        EditorGUILayout.LabelField("", GUILayout.Width(95));
                    }

                    GUILayout.FlexibleSpace();

                    GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
                    if (GUILayout.Button("✕", GUILayout.Width(25)))
                    {
                        if (selectedPiece == p)
                        {
                            selectedPiece = null;
                            CancelPieceDrag();
                        }
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
                        pieceId = pieceIsMovableBarrier
                            ? $"Barrier_{targetLevel.pieces.Count + 1}"
                            : $"Piece_{targetLevel.pieces.Count + 1}",
                        shape = selectedShape,
                        originPosition = new Vector2Int(x, y),
                        rotationSteps = pieceRotation,
                        flipHorizontal = pieceFlipHorizontal,
                        colorType = pieceColorType,
                        moveRestriction = pieceIsMovableBarrier && pieceRestriction == MoveRestriction.Locked
                            ? MoveRestriction.Free
                            : pieceRestriction,
                        freezeCount = pieceFreezeCount,
                        isDoubleBlock = pieceIsMovableBarrier ? false : pieceIsDoubleBlock,
                        innerColorType = pieceInnerColorType,
                        isMovableBarrier = pieceIsMovableBarrier,
                        isRequiredForWin = !pieceIsMovableBarrier
                    };

                    if (!CanPlacePieceAt(newPiece, newPiece.originPosition))
                    {
                        Debug.LogWarning("LevelEditor: Не можна поставити блок на стіну, Obstacle або не-Walkable клітинку.");
                        return;
                    }

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

        private void TryAssignDefaultBarrierShape()
        {
            if (selectedShape != null)
            {
                string id = (string.IsNullOrEmpty(selectedShape.shapeId) ? selectedShape.name : selectedShape.shapeId).ToLowerInvariant();
                if (id.Contains("1x1") || id.Contains("1_1"))
                    return;
            }

            string[] guids = AssetDatabase.FindAssets("t:LegoShapeDefinition");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var shape = AssetDatabase.LoadAssetAtPath<LegoShapeDefinition>(path);
                if (shape == null) continue;
                string name = (string.IsNullOrEmpty(shape.shapeId) ? shape.name : shape.shapeId).ToLowerInvariant();
                if (name.Contains("1x1") || name.Contains("1_1"))
                {
                    selectedShape = shape;
                    return;
                }
            }
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

        private static List<LevelData> GetSortedLevelAssets()
        {
            string[] guids = AssetDatabase.FindAssets("t:LevelData", new[] { LevelsFolder });
            if (guids == null || guids.Length == 0)
                guids = AssetDatabase.FindAssets("t:LevelData");

            var list = new List<LevelData>();
            var seen = new HashSet<int>();

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                LevelData level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
                if (level == null || !IsValidLevelAsset(level) || seen.Contains(level.levelIndex))
                    continue;

                seen.Add(level.levelIndex);
                list.Add(level);
            }

            list.Sort((a, b) => a.levelIndex.CompareTo(b.levelIndex));
            return list;
        }

        private void NavigateAdjacentLevel(int direction, List<LevelData> levels = null)
        {
            levels ??= GetSortedLevelAssets();
            if (levels.Count == 0) return;

            int currentIdx = targetLevel != null ? levels.FindIndex(l => l == targetLevel) : -1;
            int nextIdx;

            if (currentIdx < 0)
                nextIdx = direction < 0 ? levels.Count - 1 : 0;
            else
                nextIdx = currentIdx + direction;

            if (nextIdx < 0 || nextIdx >= levels.Count) return;

            LevelData next = levels[nextIdx];
            if (!IsValidLevelAsset(next)) return;

            targetLevel = next;
            selectedPiece = null;
            CancelPieceDrag();
            Selection.activeObject = next;
            GUI.FocusControl(null);
            Repaint();
        }
    }
}
#endif
