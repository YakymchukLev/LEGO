using System;
using System.Collections.Generic;
using UnityEngine;
using LegoPuzzle.Data;

namespace LegoPuzzle.Runtime
{
    /// <summary>
    /// Відповідає за логіку бустерів: розрахунок підказки ходу (Hint solver),
    /// ефект та знищення деталі молотком (Hammer booster), аудіо-відгук бустерів.
    /// </summary>
    public class LevelBoosterService : MonoBehaviour
    {
        private LevelLoader levelLoader;

        public void Initialize(LevelLoader loader)
        {
            levelLoader = loader;
        }

        private LevelLoader Loader => levelLoader != null ? levelLoader : (levelLoader = GetComponent<LevelLoader>());

        /// <summary>
        /// Знищує деталь бустером "Молоток", звільняє зайняті клітинки на сітці
        /// та зараховує прогрес, якщо деталь була необхідна для перемоги.
        /// </summary>
        public void DestroyPieceWithHammer(LegoPieceView piece)
        {
            if (piece == null) return;
            var loader = Loader;
            if (loader == null) return;

            loader.NotifyBlockMoved();

            bool wasRequired = piece.PieceData != null && piece.PieceData.isRequiredForWin;

            loader.RemoveActivePiece(piece);

            piece.gameObject.SetActive(false);
            Destroy(piece.gameObject, 0.5f);

            loader.NotifyPieceDestroyed(piece);

            if (wasRequired)
            {
                loader.HandleRequiredPieceVictoryCheck(fromHammer: true);
            }
            else
            {
                Debug.Log("<color=orange>🔨 Блок-перешкоду знищено молотком! Сітка звільнилася.</color>");
            }
        }

        /// <summary>
        /// Відтворює звук удару молотка зі зниженим пітчем для глухого важкого звуку.
        /// </summary>
        public void PlaySmashSound(AudioClip customClip = null)
        {
            var loader = Loader;
            if (loader == null) return;

            AudioClip clipToPlay = customClip;
            if (clipToPlay == null)
            {
                clipToPlay = loader.PieceExitSound != null ? loader.PieceExitSound : (loader.Palette != null ? loader.Palette.pieceExitSound : null);
            }

            if (clipToPlay != null && loader.AudioSource != null)
            {
                loader.AudioSource.pitch = UnityEngine.Random.Range(0.70f, 0.82f);
                loader.AudioSource.PlayOneShot(clipToPlay, 1.25f);
            }
        }

        /// <summary>
        /// Показує візуальну підказку найкращого наступного ходу.
        /// Повертає true, якщо підказку знайдено і показано.
        /// </summary>
        public bool ShowMoveHint(AudioClip customChime = null)
        {
            var loader = Loader;
            if (loader == null) return false;

            if (FindBestHintMove(out LegoPieceView piece, out Vector2Int delta, out bool leadsToExit))
            {
                HintIndicatorEffect.Show(piece, delta, loader.CellSize);
                PlayHintChime(customChime);
                Debug.Log($"<color=gold>💡 Підказка: Рухайте деталь '{piece.name}' на {delta} {(leadsToExit ? "(Вихід у ворота!)" : "(Розчищення шляху)")}</color>");
                return true;
            }
            else
            {
                Debug.LogWarning("LevelLoader: Не знайдено доступного ходу для підказки.");
                return false;
            }
        }

        public void DismissMoveHint()
        {
            HintIndicatorEffect.Dismiss();
        }

        public void PlayHintChime(AudioClip customClip = null)
        {
            var loader = Loader;
            if (loader == null) return;

            AudioClip clip = customClip;
            if (clip == null)
            {
                clip = loader.PieceExitSound != null ? loader.PieceExitSound : (loader.Palette != null ? loader.Palette.pieceExitSound : null);
            }

            if (clip != null && loader.AudioSource != null)
            {
                loader.AudioSource.pitch = 1.35f; // Підвищений кришталевий тон
                loader.AudioSource.PlayOneShot(clip, 1.15f);
            }
        }

        /// <summary>
        /// Знаходить найбільш вигідний наступний хід для гравця (пріоритет: прямий вихід,
        /// розблокування виходу для іншого блоку або рух у напрямку воріт).
        /// </summary>
        public bool FindBestHintMove(out LegoPieceView bestPiece, out Vector2Int bestDelta, out bool leadsToExit)
        {
            bestPiece = null;
            bestDelta = Vector2Int.zero;
            leadsToExit = false;

            var loader = Loader;
            if (loader == null || !loader.IsGameplayActive || loader.CurrentLevel == null || loader.ActivePieces == null || loader.ActivePieces.Count == 0)
                return false;

            var validator = loader.MovementValidator;
            if (validator == null) return false;

            List<LegoPieceView> movablePieces = new List<LegoPieceView>();
            foreach (var p in loader.ActivePieces)
            {
                if (p != null && p.gameObject.activeInHierarchy && p.PieceData != null && p.PieceData.moveRestriction != MoveRestriction.Locked && !p.IsFrozen)
                {
                    movablePieces.Add(p);
                }
            }

            if (movablePieces.Count == 0) return false;

            // ==============================================================
            // ПРІОРИТЕТ 1: Прямий вихід у ворота вже за цей хід!
            // ==============================================================
            foreach (var piece in movablePieces)
            {
                validator.GetSlideRangeHorizontal(piece, piece.CurrentOrigin, out int minX, out int maxX);
                for (int x = 1; x <= maxX; x++)
                {
                    Vector2Int testPos = piece.CurrentOrigin + new Vector2Int(x, 0);
                    if (validator.CheckIfPieceExits(piece, testPos, out _))
                    {
                        bestPiece = piece;
                        bestDelta = new Vector2Int(x, 0);
                        leadsToExit = true;
                        return true;
                    }
                }
                for (int x = -1; x >= minX; x--)
                {
                    Vector2Int testPos = piece.CurrentOrigin + new Vector2Int(x, 0);
                    if (validator.CheckIfPieceExits(piece, testPos, out _))
                    {
                        bestPiece = piece;
                        bestDelta = new Vector2Int(x, 0);
                        leadsToExit = true;
                        return true;
                    }
                }

                validator.GetSlideRangeVertical(piece, piece.CurrentOrigin, out int minY, out int maxY);
                for (int y = 1; y <= maxY; y++)
                {
                    Vector2Int testPos = piece.CurrentOrigin + new Vector2Int(0, y);
                    if (validator.CheckIfPieceExits(piece, testPos, out _))
                    {
                        bestPiece = piece;
                        bestDelta = new Vector2Int(0, y);
                        leadsToExit = true;
                        return true;
                    }
                }
                for (int y = -1; y >= minY; y--)
                {
                    Vector2Int testPos = piece.CurrentOrigin + new Vector2Int(0, y);
                    if (validator.CheckIfPieceExits(piece, testPos, out _))
                    {
                        bestPiece = piece;
                        bestDelta = new Vector2Int(0, y);
                        leadsToExit = true;
                        return true;
                    }
                }
            }

            // ==============================================================
            // ПРІОРИТЕТ 2: Хід деталі A, що дозволяє іншій деталі B вийти у ворота!
            // ==============================================================
            foreach (var pieceA in movablePieces)
            {
                List<Vector2Int> validMovesA = GetValidMovesForPiece(pieceA);
                Vector2Int origPosA = pieceA.CurrentOrigin;

                foreach (var deltaA in validMovesA)
                {
                    pieceA.SetCurrentOriginDirect(origPosA + deltaA);

                    bool unblockedAnother = false;
                    foreach (var pieceB in movablePieces)
                    {
                        if (pieceB == pieceA) continue;
                        if (CanPieceExitAnywhere(pieceB))
                        {
                            unblockedAnother = true;
                            break;
                        }
                    }

                    pieceA.SetCurrentOriginDirect(origPosA);

                    if (unblockedAnother)
                    {
                        bestPiece = pieceA;
                        bestDelta = deltaA;
                        leadsToExit = false;
                        return true;
                    }
                }
            }

            // ==============================================================
            // ПРІОРИТЕТ 3: Хід, що максимально скорочує відстань до відповідних воріт
            // ==============================================================
            float bestDistanceImprovement = 0f;
            foreach (var piece in movablePieces)
            {
                List<Vector2Int> matchingGates = GetMatchingExitGatePositions(piece);
                if (matchingGates.Count == 0) continue;

                float currentMinDist = GetMinDistanceToGates(piece.CurrentOrigin, matchingGates);
                List<Vector2Int> validMoves = GetValidMovesForPiece(piece);

                foreach (var delta in validMoves)
                {
                    Vector2Int newPos = piece.CurrentOrigin + delta;
                    float newDist = GetMinDistanceToGates(newPos, matchingGates);
                    float improvement = currentMinDist - newDist;

                    if (improvement > bestDistanceImprovement)
                    {
                        bestDistanceImprovement = improvement;
                        bestPiece = piece;
                        bestDelta = delta;
                    }
                }
            }

            if (bestPiece != null && bestDelta != Vector2Int.zero)
            {
                return true;
            }

            // ==============================================================
            // ПРІОРИТЕТ 4: Будь-який найбільший хід, що звільняє простір
            // ==============================================================
            int maxMoveDistance = 0;
            foreach (var piece in movablePieces)
            {
                List<Vector2Int> validMoves = GetValidMovesForPiece(piece);
                foreach (var delta in validMoves)
                {
                    int dist = Mathf.Abs(delta.x) + Mathf.Abs(delta.y);
                    if (dist > maxMoveDistance)
                    {
                        maxMoveDistance = dist;
                        bestPiece = piece;
                        bestDelta = delta;
                    }
                }
            }

            return bestPiece != null && bestDelta != Vector2Int.zero;
        }

        public List<Vector2Int> GetValidMovesForPiece(LegoPieceView piece)
        {
            List<Vector2Int> moves = new List<Vector2Int>();
            if (piece == null || piece.PieceData == null) return moves;

            var validator = Loader != null ? Loader.MovementValidator : null;
            if (validator == null) return moves;

            validator.GetSlideRangeHorizontal(piece, piece.CurrentOrigin, out int minX, out int maxX);
            for (int x = 1; x <= maxX; x++) moves.Add(new Vector2Int(x, 0));
            for (int x = -1; x >= minX; x--) moves.Add(new Vector2Int(x, 0));

            validator.GetSlideRangeVertical(piece, piece.CurrentOrigin, out int minY, out int maxY);
            for (int y = 1; y <= maxY; y++) moves.Add(new Vector2Int(0, y));
            for (int y = -1; y >= minY; y--) moves.Add(new Vector2Int(0, y));

            return moves;
        }

        public bool CanPieceExitAnywhere(LegoPieceView piece)
        {
            if (piece == null || piece.PieceData == null) return false;

            var validator = Loader != null ? Loader.MovementValidator : null;
            if (validator == null) return false;

            validator.GetSlideRangeHorizontal(piece, piece.CurrentOrigin, out int minX, out int maxX);
            for (int x = 1; x <= maxX; x++)
            {
                if (validator.CheckIfPieceExits(piece, piece.CurrentOrigin + new Vector2Int(x, 0), out _)) return true;
            }
            for (int x = -1; x >= minX; x--)
            {
                if (validator.CheckIfPieceExits(piece, piece.CurrentOrigin + new Vector2Int(x, 0), out _)) return true;
            }

            validator.GetSlideRangeVertical(piece, piece.CurrentOrigin, out int minY, out int maxY);
            for (int y = 1; y <= maxY; y++)
            {
                if (validator.CheckIfPieceExits(piece, piece.CurrentOrigin + new Vector2Int(0, y), out _)) return true;
            }
            for (int y = -1; y >= minY; y--)
            {
                if (validator.CheckIfPieceExits(piece, piece.CurrentOrigin + new Vector2Int(0, y), out _)) return true;
            }

            return false;
        }

        public List<Vector2Int> GetMatchingExitGatePositions(LegoPieceView piece)
        {
            List<Vector2Int> gates = new List<Vector2Int>();
            if (piece == null || piece.PieceData == null) return gates;

            var loader = Loader;
            if (loader == null || loader.SpawnedCells == null) return gates;

            var validator = loader.MovementValidator;
            if (validator == null) return gates;

            Color pieceColor = piece.PieceData.GetColor();
            BlockColorType pieceColorType = piece.PieceData.colorType;

            foreach (var kvp in loader.SpawnedCells)
            {
                var cell = kvp.Value;
                if (cell != null && (cell.CellType == CellType.ExitGate || cell.CellType == CellType.HalfExitGate))
                {
                    bool colorMatch = (cell.GateColorType == BlockColorType.Universal) ||
                                      (cell.GateColorType == pieceColorType) ||
                                      validator.ColorsMatch(pieceColor, cell.GateColor);

                    if (colorMatch)
                    {
                        gates.Add(kvp.Key);
                    }
                }
            }

            return gates;
        }

        private float GetMinDistanceToGates(Vector2Int pos, List<Vector2Int> gates)
        {
            float min = float.MaxValue;
            foreach (var g in gates)
            {
                float d = Mathf.Abs(pos.x - g.x) + Mathf.Abs(pos.y - g.y);
                if (d < min) min = d;
            }
            return min;
        }
    }
}
