using System;
using System.Collections.Generic;
using UnityEngine;
using LegoPuzzle.Data;

namespace LegoPuzzle.Runtime
{
    public class LevelLoader : MonoBehaviour
    {
        [Header("Конфігурація")]
        [SerializeField] private BlockPalette palette;
        [SerializeField] private LevelData testLevelData;
        [Tooltip("Розмір однієї клітинки сітки (підганяйте під масштаб ваших 3D-моделей)")]
        [SerializeField] private float cellSize = 1f;

        [Tooltip("Поворот 3D-моделей блоків (якщо вони лежать горизонтально, ставте -90 по X або 0)")]
        [SerializeField] private Vector3 blockModelRotation = new Vector3(0f, 0f, 0f);

        [Header("Камера")]
        [Tooltip("Автоматично центрувати та підганяти зум камери під розмір поля (вимкніть, якщо позиціонуєте камеру вручну)")]
        [SerializeField] private bool autoCenterCamera = true;
        [SerializeField] private Camera gameCamera;

        [Header("Батьківські контейнери (Опціонально)")]
        [SerializeField] private Transform boardContainer;
        [SerializeField] private Transform piecesContainer;

        public LevelData CurrentLevel { get; private set; }
        public bool IsGameplayActive { get; private set; } = false;
        public float RemainingTime { get; private set; }

        private readonly Dictionary<Vector2Int, GridCellView> spawnedCells = new Dictionary<Vector2Int, GridCellView>();
        private readonly List<LegoPieceView> activePieces = new List<LegoPieceView>();
        private int requiredPiecesToWin = 0;
        private int piecesExited = 0;

        public event Action<int> OnLevelLoaded;
        public event Action<float> OnTimerUpdated;
        public event Action OnLevelWon;
        public event Action OnLevelLost;

        private void Awake()
        {
            if (gameCamera == null)
            {
                gameCamera = Camera.main;
            }

            EnsurePalette();
            EnsureEventSystem();
        }

        private void OnValidate()
        {
            EnsurePalette();
        }

        private void EnsurePalette()
        {
            if (palette == null)
            {
                palette = Resources.Load<BlockPalette>("MainBlockPalette");
                if (palette == null)
                {
                    palette = Resources.Load<BlockPalette>("BlockPalette");
                }

                if (palette == null)
                {
                    var palettes = Resources.FindObjectsOfTypeAll<BlockPalette>();
                    if (palettes != null && palettes.Length > 0)
                    {
                        palette = palettes[0];
                    }
                }

#if UNITY_EDITOR
                if (palette == null)
                {
                    string[] guids = UnityEditor.AssetDatabase.FindAssets("t:BlockPalette");
                    if (guids.Length > 0)
                    {
                        string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                        palette = UnityEditor.AssetDatabase.LoadAssetAtPath<BlockPalette>(path);
                    }
                }
#endif
            }
        }

        private void EnsureEventSystem()
        {
            // Перевіряємо наявність PhysicsRaycaster на камері
            if (gameCamera != null && gameCamera.GetComponent<UnityEngine.EventSystems.PhysicsRaycaster>() == null)
            {
                gameCamera.gameObject.AddComponent<UnityEngine.EventSystems.PhysicsRaycaster>();
            }

            // Перевіряємо наявність EventSystem на сцені
            if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject eventSystemObj = new GameObject("EventSystem");
                eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystemObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
        }

        private void Start()
        {
            EnsurePalette();
            if (testLevelData != null)
            {
                LoadLevel(testLevelData);
            }
        }

        private void Update()
        {
            if (!IsGameplayActive) return;

            if (CurrentLevel != null && CurrentLevel.timeLimitSeconds > 0)
            {
                RemainingTime -= Time.deltaTime;
                OnTimerUpdated?.Invoke(Mathf.Max(0f, RemainingTime));

                if (RemainingTime <= 0f)
                {
                    IsGameplayActive = false;
                    OnLevelLost?.Invoke();
                    Debug.Log("<color=red>Час вичерпано! Поразка.</color>");
                }
            }
        }

        public void LoadLevel(LevelData levelData)
        {
            if (levelData == null)
            {
                Debug.LogError("LevelData is null!");
                return;
            }

            EnsurePalette();
            CurrentLevel = levelData;
            ClearBoard();

            RemainingTime = levelData.timeLimitSeconds;
            piecesExited = 0;
            requiredPiecesToWin = 0;

            BuildGrid(levelData);
            SpawnPieces(levelData);
            CenterCamera(levelData);

            IsGameplayActive = true;
            OnLevelLoaded?.Invoke(levelData.levelIndex);
        }

        public void RestartCurrentLevel()
        {
            if (CurrentLevel != null)
            {
                LoadLevel(CurrentLevel);
            }
        }

        private void ClearBoard()
        {
            foreach (var cell in spawnedCells.Values)
            {
                if (cell != null) Destroy(cell.gameObject);
            }
            spawnedCells.Clear();

            foreach (var piece in activePieces)
            {
                if (piece != null) Destroy(piece.gameObject);
            }
            activePieces.Clear();
        }

        private void BuildGrid(LevelData levelData)
        {
            EnsurePalette();
            levelData.EnsureGridCapacity();

            Transform parent = boardContainer != null ? boardContainer : transform;

            for (int y = 0; y < levelData.gridHeight; y++)
            {
                for (int x = 0; x < levelData.gridWidth; x++)
                {
                    CellData cellData = levelData.GetCell(x, y);
                    if (cellData.cellType == CellType.Empty) continue;

                    GameObject prefab = cellData.cellType switch
                    {
                        CellType.Obstacle => palette?.obstacleTilePrefab,
                        CellType.ExitGate => palette?.exitGatePrefab,
                        _ => palette?.walkableTilePrefab
                    };

                    GameObject cellObj;
                    if (prefab != null)
                    {
                        cellObj = Instantiate(prefab, parent);
                    }
                    else
                    {
                        cellObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
                        cellObj.transform.SetParent(parent);
                        Collider col = cellObj.GetComponent<Collider>();
                        if (col != null)
                        {
                            Destroy(col);
                        }
                    }

                    cellObj.name = $"Cell_{x}_{y}_{cellData.cellType}";
                    cellObj.transform.localPosition = new Vector3(x * cellSize, -0.01f, y * cellSize);
                    cellObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    cellObj.transform.localScale = Vector3.one * cellSize;

                    GridCellView cellView = cellObj.GetComponent<GridCellView>();
                    if (cellView == null)
                    {
                        cellView = cellObj.AddComponent<GridCellView>();
                    }

                    cellView.Initialize(cellData, palette, cellSize);
                    spawnedCells[new Vector2Int(x, y)] = cellView;
                }
            }
        }

        private void SpawnPieces(LevelData levelData)
        {
            Transform parent = piecesContainer != null ? piecesContainer : transform;

            foreach (var pieceData in levelData.pieces)
            {
                // Обираємо 3D-префаб: з форми або дефолтний з палітри
                GameObject prefabToSpawn = null;
                if (pieceData.shape != null && pieceData.shape.prefab != null)
                {
                    prefabToSpawn = pieceData.shape.prefab;
                }
                else if (palette != null && palette.defaultLegoPiecePrefab != null)
                {
                    prefabToSpawn = palette.defaultLegoPiecePrefab;
                }

                // Створюємо чистий корінь деталі на сітці
                GameObject pieceObj = new GameObject($"Piece_{pieceData.pieceId}");
                pieceObj.transform.SetParent(parent);

                // Якщо є 3D-префаб, спавнимо його як дочірній об'єкт Model
                GameObject visualChild = null;
                if (prefabToSpawn != null)
                {
                    visualChild = Instantiate(prefabToSpawn, pieceObj.transform);
                    visualChild.name = "Model";

                    // Очищуємо від фізичних тіл Rigidbody та сторонніх скриптів префабу, щоб уникнути дрейфу
                    var rbs = visualChild.GetComponentsInChildren<Rigidbody>(true);
                    foreach (var rb in rbs)
                    {
                        rb.isKinematic = true;
                        rb.detectCollisions = false;
                        Destroy(rb);
                    }

                    var oldCols = visualChild.GetComponentsInChildren<Collider>(true);
                    foreach (var c in oldCols)
                    {
                        c.enabled = false;
                        Destroy(c);
                    }

                    var oldMono = visualChild.GetComponentsInChildren<MonoBehaviour>(true);
                    foreach (var m in oldMono)
                    {
                        m.enabled = false;
                        Destroy(m);
                    }
                }

                LegoPieceView pieceView = pieceObj.AddComponent<LegoPieceView>();
                pieceView.Initialize(pieceData, this, palette, cellSize, visualChild);
                pieceView.OnExited += HandlePieceExited;
                activePieces.Add(pieceView);

                if (pieceData.isRequiredForWin)
                {
                    requiredPiecesToWin++;
                }
            }
        }

        private void CenterCamera(LevelData levelData)
        {
            if (!autoCenterCamera || gameCamera == null) return;

            // Враховуємо позицію батьківського об'єкта / менеджера у просторі
            Vector3 rootPos = (boardContainer != null) ? boardContainer.position : transform.position;
            float centerX = rootPos.x + (levelData.gridWidth - 1) * cellSize * 0.5f;
            float centerZ = rootPos.z + (levelData.gridHeight - 1) * cellSize * 0.5f;

            if (gameCamera.orthographic)
            {
                float aspect = (gameCamera.aspect > 0.01f) ? gameCamera.aspect : (9f / 16f);
                float verticalSize = (levelData.gridHeight * cellSize * 0.5f) + 1.5f;
                float horizontalSize = ((levelData.gridWidth * cellSize * 0.5f) + 1.5f) / aspect;

                gameCamera.orthographicSize = Mathf.Max(verticalSize, horizontalSize);
                gameCamera.transform.position = new Vector3(centerX, rootPos.y + 20f, centerZ);
                gameCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            }
            else
            {
                // Для перспективної камери розраховуємо точну дистанцію за кутом огляду (FOV)
                float aspect = (gameCamera.aspect > 0.01f) ? gameCamera.aspect : (9f / 16f);
                float fovRad = gameCamera.fieldOfView * Mathf.Deg2Rad;

                float requiredHeight = (levelData.gridHeight * cellSize) + 2.5f;
                float requiredWidth = ((levelData.gridWidth * cellSize) + 2.5f) / aspect;
                float maxDim = Mathf.Max(requiredHeight, requiredWidth);
                float distance = (maxDim * 0.5f) / Mathf.Tan(fovRad * 0.5f);

                gameCamera.transform.position = new Vector3(centerX, rootPos.y + distance, centerZ);
                gameCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            }
        }

        public bool CanMovePieceTo(LegoPieceView movingPiece, Vector2Int newOrigin)
        {
            if (CurrentLevel == null) return false;

            var offsets = movingPiece.PieceData.shape != null
                ? movingPiece.PieceData.shape.GetRotatedOffsets(movingPiece.PieceData.rotationSteps)
                : new List<Vector2Int> { Vector2Int.zero };

            foreach (var offset in offsets)
            {
                Vector2Int cellPos = newOrigin + offset;

                // Перевірка меж сітки
                if (!CurrentLevel.IsInsideGrid(cellPos.x, cellPos.y))
                    return false;

                CellData cellData = CurrentLevel.GetCell(cellPos.x, cellPos.y);

                // Не можна ставати на порожні місця або перешкоди
                if (cellData.cellType == CellType.Empty || cellData.cellType == CellType.Obstacle)
                    return false;

                // Перевірка колізій з іншими блоками LEGO
                foreach (var otherPiece in activePieces)
                {
                    if (otherPiece == movingPiece || !otherPiece.gameObject.activeSelf) continue;

                    var otherCells = otherPiece.GetCurrentOccupiedCells();
                    if (otherCells.Contains(cellPos))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public void GetSlideRangeHorizontal(LegoPieceView piece, Vector2Int currentOrigin, out int minStepX, out int maxStepX)
        {
            minStepX = 0;
            maxStepX = 0;
            if (CurrentLevel == null) return;

            for (int step = -1; step >= -CurrentLevel.gridWidth; step--)
            {
                if (CanMovePieceTo(piece, currentOrigin + new Vector2Int(step, 0)))
                    minStepX = step;
                else
                    break;
            }

            for (int step = 1; step <= CurrentLevel.gridWidth; step++)
            {
                if (CanMovePieceTo(piece, currentOrigin + new Vector2Int(step, 0)))
                    maxStepX = step;
                else
                    break;
            }
        }

        public void GetSlideRangeVertical(LegoPieceView piece, Vector2Int currentOrigin, out int minStepY, out int maxStepY)
        {
            minStepY = 0;
            maxStepY = 0;
            if (CurrentLevel == null) return;

            for (int step = -1; step >= -CurrentLevel.gridHeight; step--)
            {
                if (CanMovePieceTo(piece, currentOrigin + new Vector2Int(0, step)))
                    minStepY = step;
                else
                    break;
            }

            for (int step = 1; step <= CurrentLevel.gridHeight; step++)
            {
                if (CanMovePieceTo(piece, currentOrigin + new Vector2Int(0, step)))
                    maxStepY = step;
                else
                    break;
            }
        }

        public void GetSlideRange(LegoPieceView piece, Vector2Int currentOrigin, out int minStepX, out int maxStepX, out int minStepY, out int maxStepY)
        {
            GetSlideRangeHorizontal(piece, currentOrigin, out minStepX, out maxStepX);
            GetSlideRangeVertical(piece, currentOrigin, out minStepY, out maxStepY);
        }

        public bool CheckIfPieceExits(LegoPieceView piece, Vector2Int currentOrigin, out ExitDirection exitDirection)
        {
            exitDirection = ExitDirection.Up;
            var occupied = piece.GetCurrentOccupiedCells();

            // Перевіряємо, чи клітинки деталі потрапили у відповідні ворота
            foreach (var cellPos in occupied)
            {
                if (spawnedCells.TryGetValue(cellPos, out GridCellView cellView))
                {
                    if (cellView.CellType == CellType.ExitGate)
                    {
                        // 1. Універсальні ворота (Universal) — приймають будь-які блоки незалежно від кольору
                        if (cellView.GateColorType == BlockColorType.Universal)
                        {
                            exitDirection = cellView.ExitDirection;
                            return true;
                        }

                        // 2. Кольорові ворота — перевіряють збіг кольору деталі
                        Color pieceColor = piece.PieceData.GetColor();
                        if (ColorsMatch(pieceColor, cellView.GateColor))
                        {
                            exitDirection = cellView.ExitDirection;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private bool ColorsMatch(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.15f &&
                   Mathf.Abs(a.g - b.g) < 0.15f &&
                   Mathf.Abs(a.b - b.b) < 0.15f;
        }

        private void HandlePieceExited(LegoPieceView piece)
        {
            piecesExited++;

            if (piecesExited >= requiredPiecesToWin)
            {
                IsGameplayActive = false;
                OnLevelWon?.Invoke();
                Debug.Log("<color=green>Рівень пройдено! Перемога!</color>");
            }
        }
    }
}
