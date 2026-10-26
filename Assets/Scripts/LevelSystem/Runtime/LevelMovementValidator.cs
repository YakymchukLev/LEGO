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
                ? movingPiece.PieceData.GetOrientedOffsets()
                : new List<Vector2Int> { Vector2Int.zero };

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

                // Ворота — суцільна колізія, як стіна. Блок ніколи не займає їхню клітинку.
                // Вихід спрацьовує з сусідньої клітинки, коли весь блок вміщається в отвір.
                if (cellData.cellType == CellType.Empty
                    || cellData.cellType == CellType.Obstacle
                    || cellData.cellType == CellType.HalfObstacle
                    || cellData.cellType == CellType.QuarterObstacle
                    || cellData.cellType == CellType.ExitGate
                    || cellData.cellType == CellType.HalfExitGate)
                    return false;

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

        /// <summary>
        /// Уся ширина блока (вихід вгору/вниз) або висота (вліво/вправо) має лежати
        /// всередині одного отвору. Ворота можуть бути довшими за блок.
        /// Вужча сторона дотику не дозволяє вийти, якщо решта блока ширша за отвір.
        /// </summary>
        private static bool PieceFitsThroughGate(LevelData level, List<Vector2Int> pieceCells, List<Vector2Int> contactCells)
        {
            if (level == null || pieceCells == null || pieceCells.Count == 0 || contactCells == null || contactCells.Count == 0)
                return false;

            Vector2Int pos = contactCells[0];
            if (!level.IsInsideGrid(pos.x, pos.y)) return false;

            CellData gateCell = level.GetCell(pos.x, pos.y);
            if (!IsExitCell(gateCell.cellType)) return false;

            bool alongX = gateCell.exitDirection == ExitDirection.Up || gateCell.exitDirection == ExitDirection.Down;
            int line = alongX ? pos.y : pos.x;
            int cross = alongX ? pos.x : pos.y;

            int min = cross;
            int max = cross;
            while (IsSameGate(level, alongX, line, min - 1, gateCell)) min--;
            while (IsSameGate(level, alongX, line, max + 1, gateCell)) max++;

            int pieceMin = int.MaxValue;
            int pieceMax = int.MinValue;
            for (int i = 0; i < pieceCells.Count; i++)
            {
                int pieceCross = alongX ? pieceCells[i].x : pieceCells[i].y;
                if (pieceCross < pieceMin) pieceMin = pieceCross;
                if (pieceCross > pieceMax) pieceMax = pieceCross;
            }

            return pieceMin >= min && pieceMax <= max;
        }

        private static bool IsSameGate(LevelData level, bool alongX, int line, int cross, CellData gateCell)
        {
            int x = alongX ? cross : line;
            int y = alongX ? line : cross;
            if (!level.IsInsideGrid(x, y)) return false;

            CellData other = level.GetCell(x, y);
            return IsExitCell(other.cellType)
                && other.exitDirection == gateCell.exitDirection
                && other.gateColorType == gateCell.gateColorType;
        }

        private static bool IsExitCell(CellType cellType)
        {
            return cellType == CellType.ExitGate || cellType == CellType.HalfExitGate;
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
                ? piece.PieceData.GetOrientedOffsets()
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

            List<Vector2Int> edge = GetLeadingEdge(occupied, dir);
            if (edge.Count == 0) return false;

            LevelData level = Loader != null ? Loader.CurrentLevel : null;

            // Блок уже стоїть на воротах (старі розкладки).
            if (EdgeIsMatchingGate(edge, dir, pieceColorType, pieceColor)
                && PieceFitsThroughGate(level, occupied, edge)
                && ExitCorridorClear(piece, occupied, dir, pieceColorType, pieceColor))
                return true;

            Vector2Int step = dir switch
            {
                ExitDirection.Up => Vector2Int.up,
                ExitDirection.Down => Vector2Int.down,
                ExitDirection.Left => Vector2Int.left,
                ExitDirection.Right => Vector2Int.right,
                _ => Vector2Int.zero
            };

            var inFront = new List<Vector2Int>(edge.Count);
            for (int i = 0; i < edge.Count; i++)
            {
                inFront.Add(edge[i] + step);
            }

            // Вихід з клітинки перед воротами. На самі ворота блок не заходить.
            // Виріз фігури не може проходити крізь інший блок.
            return EdgeIsMatchingGate(inFront, dir, pieceColorType, pieceColor)
                && PieceFitsThroughGate(level, occupied, inFront)
                && ExitCorridorClear(piece, occupied, dir, pieceColorType, pieceColor);
        }

        /// <summary>
        /// Кожна клітинка блока має дійти до своїх воріт порожнім коридором.
        /// Інший блок у вирізі, як у L, вихід блокує.
        /// </summary>
        private bool ExitCorridorClear(LegoPieceView piece, List<Vector2Int> occupied, ExitDirection dir, BlockColorType pieceColorType, Color pieceColor)
        {
            var loader = Loader;
            LevelData level = loader != null ? loader.CurrentLevel : null;
            if (level == null || occupied == null || occupied.Count == 0) return false;

            Vector2Int step = dir switch
            {
                ExitDirection.Up => Vector2Int.up,
                ExitDirection.Down => Vector2Int.down,
                ExitDirection.Left => Vector2Int.left,
                ExitDirection.Right => Vector2Int.right,
                _ => Vector2Int.zero
            };
            if (step == Vector2Int.zero) return false;

            var own = new HashSet<Vector2Int>(occupied);
            var blocked = new HashSet<Vector2Int>();
            var activePieces = loader.ActivePieces;
            if (activePieces != null)
            {
                for (int i = 0; i < activePieces.Count; i++)
                {
                    LegoPieceView other = activePieces[i];
                    if (other == null || other == piece || !other.gameObject.activeSelf) continue;
                    List<Vector2Int> otherCells = other.GetCurrentOccupiedCells();
                    if (otherCells == null) continue;
                    for (int c = 0; c < otherCells.Count; c++)
                        blocked.Add(otherCells[c]);
                }
            }

            int limit = Mathf.Max(level.gridWidth, level.gridHeight) + 2;
            for (int i = 0; i < occupied.Count; i++)
            {
                Vector2Int pos = occupied[i] + step;
                for (int guard = 0; guard < limit; guard++)
                {
                    if (!level.IsInsideGrid(pos.x, pos.y)) return false;
                    if (own.Contains(pos))
                    {
                        pos += step;
                        continue;
                    }

                    if (blocked.Contains(pos)) return false;

                    CellData cell = level.GetCell(pos.x, pos.y);
                    if (IsExitCell(cell.cellType))
                    {
                        if (!IsMatchingExitGate(pos, dir, pieceColorType, pieceColor))
                            return false;
                        break;
                    }

                    if (cell.cellType != CellType.Walkable)
                        return false;

                    pos += step;
                }
            }

            return true;
        }

        private static List<Vector2Int> GetLeadingEdge(List<Vector2Int> occupied, ExitDirection dir)
        {
            var edge = new List<Vector2Int>();
            int extreme = (dir == ExitDirection.Down || dir == ExitDirection.Left) ? int.MaxValue : int.MinValue;

            for (int i = 0; i < occupied.Count; i++)
            {
                int depth = (dir == ExitDirection.Up || dir == ExitDirection.Down) ? occupied[i].y : occupied[i].x;
                if (dir == ExitDirection.Down || dir == ExitDirection.Left)
                {
                    if (depth < extreme) extreme = depth;
                }
                else if (depth > extreme)
                {
                    extreme = depth;
                }
            }

            for (int i = 0; i < occupied.Count; i++)
            {
                int depth = (dir == ExitDirection.Up || dir == ExitDirection.Down) ? occupied[i].y : occupied[i].x;
                if (depth == extreme) edge.Add(occupied[i]);
            }

            return edge;
        }

        private bool EdgeIsMatchingGate(List<Vector2Int> cells, ExitDirection dir, BlockColorType pieceColorType, Color pieceColor)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                if (!IsMatchingExitGate(cells[i], dir, pieceColorType, pieceColor))
                    return false;
            }

            return cells.Count > 0;
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
