using System;
using System.Collections.Generic;
using UnityEngine;
using LegoPuzzle.Data;

namespace LegoPuzzle.Runtime
{
    /// <summary>
    /// Відповідає за валідацію руху деталей, перевірку меж поля, колізій з іншими деталями,
    /// перевірку збігу кольорів та напрямку для виходу у ворота (Exit Gates).
    /// </summary>
    public class LevelMovementValidator : MonoBehaviour
    {
        private LevelLoader levelLoader;

        public void Initialize(LevelLoader loader)
        {
            levelLoader = loader;
        }

        private LevelLoader Loader => levelLoader != null ? levelLoader : (levelLoader = GetComponent<LevelLoader>());

        public bool CanMovePieceTo(LegoPieceView movingPiece, Vector2Int newOrigin)
        {
            var loader = Loader;
            if (loader == null || loader.CurrentLevel == null || movingPiece == null || movingPiece.PieceData == null) return false;

            var offsets = movingPiece.PieceData.shape != null
                ? movingPiece.PieceData.shape.GetRotatedOffsets(movingPiece.PieceData.rotationSteps)
                : new List<Vector2Int> { Vector2Int.zero };

            Color pieceColor = movingPiece.PieceData.GetColor();
            BlockColorType pieceColorType = movingPiece.PieceData.colorType;
            MoveRestriction restriction = movingPiece.PieceData.moveRestriction;

            if (restriction == MoveRestriction.Locked) return false;
            if (movingPiece.IsFrozen) return false;

            LevelData currentLevel = loader.CurrentLevel;
            var activePieces = loader.ActivePieces;

            foreach (var offset in offsets)
            {
                Vector2Int cellPos = newOrigin + offset;

                // Перевірка меж сітки
                if (!currentLevel.IsInsideGrid(cellPos.x, cellPos.y))
                    return false;

                CellData cellData = currentLevel.GetCell(cellPos.x, cellPos.y);

                // Не можна ставати на порожні місця або перешкоди (повні, половинчасті та четвертинки)
                if (cellData.cellType == CellType.Empty || cellData.cellType == CellType.Obstacle || cellData.cellType == CellType.HalfObstacle || cellData.cellType == CellType.QuarterObstacle)
                    return false;

                // На ворота (повні та напів-ворота) дозволено ставати ТІЛЬКИ якщо збігається колір та дозволений напрямок руху
                if (cellData.cellType == CellType.ExitGate || cellData.cellType == CellType.HalfExitGate)
                {
                    bool colorMatches = (cellData.gateColorType == BlockColorType.Universal) ||
                                        (cellData.gateColorType == pieceColorType) ||
                                        ColorsMatch(pieceColor, cellData.GetEffectiveColor());

                    if (!colorMatches)
                        return false;

                    if (restriction == MoveRestriction.HorizontalOnly &&
                        (cellData.exitDirection == ExitDirection.Up || cellData.exitDirection == ExitDirection.Down))
                    {
                        return false;
                    }

                    if (restriction == MoveRestriction.VerticalOnly &&
                        (cellData.exitDirection == ExitDirection.Left || cellData.exitDirection == ExitDirection.Right))
                    {
                        return false;
                    }
                }

                // Перевірка колізій з іншими блоками LEGO
                if (activePieces != null)
                {
                    int pieceCount = activePieces.Count;
                    for (int i = 0; i < pieceCount; i++)
                    {
                        var otherPiece = activePieces[i];
                        if (otherPiece == movingPiece || otherPiece == null || !otherPiece.gameObject.activeSelf) continue;

                        var otherCells = otherPiece.GetCurrentOccupiedCells();
                        if (otherCells != null && otherCells.Contains(cellPos))
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        public void GetSlideRangeHorizontal(LegoPieceView piece, Vector2Int currentOrigin, out int minStepX, out int maxStepX)
        {
            minStepX = 0;
            maxStepX = 0;
            var loader = Loader;
            if (loader == null || loader.CurrentLevel == null) return;

            int gridW = loader.CurrentLevel.gridWidth;
            for (int step = -1; step >= -gridW; step--)
            {
                if (CanMovePieceTo(piece, currentOrigin + new Vector2Int(step, 0)))
                    minStepX = step;
                else
                    break;
            }

            for (int step = 1; step <= gridW; step++)
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
            var loader = Loader;
            if (loader == null || loader.CurrentLevel == null) return;

            int gridH = loader.CurrentLevel.gridHeight;
            for (int step = -1; step >= -gridH; step--)
            {
                if (CanMovePieceTo(piece, currentOrigin + new Vector2Int(0, step)))
                    minStepY = step;
                else
                    break;
            }

            for (int step = 1; step <= gridH; step++)
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
            if (piece == null || piece.PieceData == null) return false;
            if (piece.PieceData.moveRestriction == MoveRestriction.Locked) return false;
            if (piece.IsFrozen) return false;

            var offsets = piece.PieceData.shape != null
                ? piece.PieceData.shape.GetRotatedOffsets(piece.PieceData.rotationSteps)
                : new List<Vector2Int> { Vector2Int.zero };

            List<Vector2Int> occupied = new List<Vector2Int>(offsets.Count);
            foreach (var offset in offsets)
            {
                occupied.Add(currentOrigin + offset);
            }

            Color pieceColor = piece.PieceData.GetColor();
            BlockColorType pieceColorType = piece.PieceData.colorType;

            ExitDirection[] directions = new ExitDirection[]
            {
                ExitDirection.Up,
                ExitDirection.Down,
                ExitDirection.Left,
                ExitDirection.Right
            };

            foreach (var dir in directions)
            {
                if (piece.PieceData.moveRestriction == MoveRestriction.HorizontalOnly && (dir == ExitDirection.Up || dir == ExitDirection.Down))
                    continue;

                if (piece.PieceData.moveRestriction == MoveRestriction.VerticalOnly && (dir == ExitDirection.Left || dir == ExitDirection.Right))
                    continue;

                if (CanPieceExitInDirection(piece, currentOrigin, occupied, pieceColorType, pieceColor, dir))
                {
                    exitDirection = dir;
                    return true;
                }
            }

            return false;
        }

        public bool CanPieceExitInDirection(LegoPieceView piece, Vector2Int origin, List<Vector2Int> occupied, BlockColorType pieceColorType, Color pieceColor, ExitDirection dir)
        {
            if (occupied == null || occupied.Count == 0) return false;

            switch (dir)
            {
                case ExitDirection.Up:
                {
                    int maxY = int.MinValue;
                    HashSet<int> occupiedCols = new HashSet<int>();
                    foreach (var cell in occupied)
                    {
                        if (cell.y > maxY) maxY = cell.y;
                        occupiedCols.Add(cell.x);
                    }

                    bool currentMatch = true;
                    foreach (int x in occupiedCols)
                    {
                        Vector2Int gatePos = new Vector2Int(x, maxY);
                        if (!IsMatchingExitGate(gatePos, dir, pieceColorType, pieceColor))
                        {
                            currentMatch = false;
                            break;
                        }
                    }
                    if (currentMatch) return true;

                    bool adjacentMatch = true;
                    foreach (int x in occupiedCols)
                    {
                        Vector2Int gatePos = new Vector2Int(x, maxY + 1);
                        if (!IsMatchingExitGate(gatePos, dir, pieceColorType, pieceColor))
                        {
                            adjacentMatch = false;
                            break;
                        }
                    }
                    if (adjacentMatch)
                    {
                        if (CanMovePieceTo(piece, origin + Vector2Int.up))
                        {
                            return true;
                        }
                    }

                    return false;
                }

                case ExitDirection.Down:
                {
                    int minY = int.MaxValue;
                    HashSet<int> occupiedCols = new HashSet<int>();
                    foreach (var cell in occupied)
                    {
                        if (cell.y < minY) minY = cell.y;
                        occupiedCols.Add(cell.x);
                    }

                    bool currentMatch = true;
                    foreach (int x in occupiedCols)
                    {
                        Vector2Int gatePos = new Vector2Int(x, minY);
                        if (!IsMatchingExitGate(gatePos, dir, pieceColorType, pieceColor))
                        {
                            currentMatch = false;
                            break;
                        }
                    }
                    if (currentMatch) return true;

                    bool adjacentMatch = true;
                    foreach (int x in occupiedCols)
                    {
                        Vector2Int gatePos = new Vector2Int(x, minY - 1);
                        if (!IsMatchingExitGate(gatePos, dir, pieceColorType, pieceColor))
                        {
                            adjacentMatch = false;
                            break;
                        }
                    }
                    if (adjacentMatch)
                    {
                        if (CanMovePieceTo(piece, origin + Vector2Int.down))
                        {
                            return true;
                        }
                    }

                    return false;
                }

                case ExitDirection.Right:
                {
                    int maxX = int.MinValue;
                    HashSet<int> occupiedRows = new HashSet<int>();
                    foreach (var cell in occupied)
                    {
                        if (cell.x > maxX) maxX = cell.x;
                        occupiedRows.Add(cell.y);
                    }

                    bool currentMatch = true;
                    foreach (int y in occupiedRows)
                    {
                        Vector2Int gatePos = new Vector2Int(maxX, y);
                        if (!IsMatchingExitGate(gatePos, dir, pieceColorType, pieceColor))
                        {
                            currentMatch = false;
                            break;
                        }
                    }
                    if (currentMatch) return true;

                    bool adjacentMatch = true;
                    foreach (int y in occupiedRows)
                    {
                        Vector2Int gatePos = new Vector2Int(maxX + 1, y);
                        if (!IsMatchingExitGate(gatePos, dir, pieceColorType, pieceColor))
                        {
                            adjacentMatch = false;
                            break;
                        }
                    }
                    if (adjacentMatch)
                    {
                        if (CanMovePieceTo(piece, origin + Vector2Int.right))
                        {
                            return true;
                        }
                    }

                    return false;
                }

                case ExitDirection.Left:
                {
                    int minX = int.MaxValue;
                    HashSet<int> occupiedRows = new HashSet<int>();
                    foreach (var cell in occupied)
                    {
                        if (cell.x < minX) minX = cell.x;
                        occupiedRows.Add(cell.y);
                    }

                    bool currentMatch = true;
                    foreach (int y in occupiedRows)
                    {
                        Vector2Int gatePos = new Vector2Int(minX, y);
                        if (!IsMatchingExitGate(gatePos, dir, pieceColorType, pieceColor))
                        {
                            currentMatch = false;
                            break;
                        }
                    }
                    if (currentMatch) return true;

                    bool adjacentMatch = true;
                    foreach (int y in occupiedRows)
                    {
                        Vector2Int gatePos = new Vector2Int(minX - 1, y);
                        if (!IsMatchingExitGate(gatePos, dir, pieceColorType, pieceColor))
                        {
                            adjacentMatch = false;
                            break;
                        }
                    }
                    if (adjacentMatch)
                    {
                        if (CanMovePieceTo(piece, origin + Vector2Int.left))
                        {
                            return true;
                        }
                    }

                    return false;
                }
            }

            return false;
        }

        public bool IsMatchingExitGate(Vector2Int pos, ExitDirection expectedDir, BlockColorType pieceColorType, Color pieceColor)
        {
            var loader = Loader;
            if (loader == null || loader.SpawnedCells == null) return false;

            if (loader.SpawnedCells.TryGetValue(pos, out GridCellView cellView))
            {
                if ((cellView.CellType == CellType.ExitGate || cellView.CellType == CellType.HalfExitGate) && cellView.ExitDirection == expectedDir)
                {
                    if (cellView.GateColorType == BlockColorType.Universal)
                        return true;

                    if (cellView.GateColorType == pieceColorType)
                        return true;

                    if (ColorsMatch(pieceColor, cellView.GateColor))
                        return true;
                }
            }

            return false;
        }

        public bool ColorsMatch(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.2f &&
                   Mathf.Abs(a.g - b.g) < 0.2f &&
                   Mathf.Abs(a.b - b.b) < 0.2f;
        }
    }
}
