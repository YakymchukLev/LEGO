using System;
using System.Collections;
using UnityEngine;
using LegoPuzzle.Data;

namespace LegoPuzzle.Runtime
{
    public struct LevelActiveBounds
    {
        public int minX;
        public int maxX;
        public int minY;
        public int maxY;
        public int activeWidth => (maxX >= minX) ? (maxX - minX + 1) : 1;
        public int activeHeight => (maxY >= minY) ? (maxY - minY + 1) : 1;
        public float centerCellX => (minX + maxX) * 0.5f;
        public float centerCellY => (minY + maxY) * 0.5f;
        public bool hasValidCells;
    }

    /// <summary>
    /// Відповідає за позиціонування камери, адаптивне масштабування під розмір рівня,
    /// плавні переходи (Smooth Transition) та ефект струшування (Camera Shake).
    /// </summary>
    public class LevelCameraController : MonoBehaviour
    {
        private LevelLoader levelLoader;
        private Coroutine cameraTransitionCoroutine;
        private Coroutine cameraShakeCoroutine;

        public void Initialize(LevelLoader loader)
        {
            levelLoader = loader;
        }

        private LevelLoader Loader => levelLoader != null ? levelLoader : (levelLoader = GetComponent<LevelLoader>());

        public Camera EnsureCamera()
        {
            var loader = Loader;
            Camera cam = loader != null ? loader.GameCamera : null;
            if (cam == null)
            {
                cam = Camera.main;
                if (cam == null)
                {
                    cam = FindAnyObjectByType<Camera>();
                }
                if (loader != null)
                {
                    loader.GameCamera = cam;
                }
            }
            return cam;
        }

        /// <summary>
        /// Автоматично розраховує фактичні межі ігрового поля (ігнорує порожні тайли сітки)
        /// </summary>
        public LevelActiveBounds GetLevelActiveBounds(LevelData levelData)
        {
            var loader = Loader;
            bool autoDetect = loader != null ? loader.AutoDetectLevelBounds : true;

            LevelActiveBounds bounds = new LevelActiveBounds
            {
                minX = int.MaxValue,
                maxX = int.MinValue,
                minY = int.MaxValue,
                maxY = int.MinValue,
                hasValidCells = false
            };

            if (autoDetect && levelData != null)
            {
                if (levelData.cells != null && levelData.cells.Count > 0)
                {
                    for (int i = 0; i < levelData.cells.Count; i++)
                    {
                        var cell = levelData.cells[i];
                        if (cell.cellType != CellType.Empty)
                        {
                            if (cell.position.x < bounds.minX) bounds.minX = cell.position.x;
                            if (cell.position.x > bounds.maxX) bounds.maxX = cell.position.x;
                            if (cell.position.y < bounds.minY) bounds.minY = cell.position.y;
                            if (cell.position.y > bounds.maxY) bounds.maxY = cell.position.y;
                            bounds.hasValidCells = true;
                        }
                    }
                }

                if (levelData.pieces != null && levelData.pieces.Count > 0)
                {
                    for (int i = 0; i < levelData.pieces.Count; i++)
                    {
                        var piece = levelData.pieces[i];
                        if (piece == null) continue;

                        var occupiedCells = piece.GetOccupiedGridCells();
                        if (occupiedCells != null)
                        {
                            for (int j = 0; j < occupiedCells.Count; j++)
                            {
                                Vector2Int p = occupiedCells[j];
                                if (p.x < bounds.minX) bounds.minX = p.x;
                                if (p.x > bounds.maxX) bounds.maxX = p.x;
                                if (p.y < bounds.minY) bounds.minY = p.y;
                                if (p.y > bounds.maxY) bounds.maxY = p.y;
                                bounds.hasValidCells = true;
                            }
                        }
                    }
                }
            }

            if (!bounds.hasValidCells)
            {
                bounds.minX = 0;
                bounds.maxX = Mathf.Max(0, (levelData != null ? levelData.gridWidth : 1) - 1);
                bounds.minY = 0;
                bounds.maxY = Mathf.Max(0, (levelData != null ? levelData.gridHeight : 1) - 1);
                bounds.hasValidCells = true;
            }

            return bounds;
        }

        public void CenterCamera(LevelData levelData, bool forceInstant = false)
        {
            var loader = Loader;
            if (loader != null && !loader.AutoCenterCamera) return;
            if (levelData == null) return;

            Camera gameCamera = EnsureCamera();
            if (gameCamera == null) return;

            LevelActiveBounds bounds = GetLevelActiveBounds(levelData);

            Vector3 rootPos = (loader != null && loader.BoardContainer != null) 
                ? loader.BoardContainer.position 
                : transform.position;

            float cellSize = loader != null ? loader.CellSize : 1f;
            float centerX = rootPos.x + bounds.centerCellX * cellSize;
            float centerZ = rootPos.z + bounds.centerCellY * cellSize;

            float aspect = (gameCamera.aspect > 0.01f) ? gameCamera.aspect : ((float)Screen.width / Mathf.Max(1, Screen.height));
            if (aspect <= 0.01f) aspect = 9f / 16f;

            float boardPaddingCells = loader != null ? loader.BoardPaddingCells : 0.05f;
            float pad = Mathf.Max(0f, boardPaddingCells) * cellSize;
            float boardWidth = bounds.activeWidth * cellSize + pad * 2f;
            float boardHeight = bounds.activeHeight * cellSize + pad * 2f;

            float safeWidthRatio;
            float safeHeightRatio;

            float smallW = loader != null ? loader.SmallLevelScreenWidthRatio : 0.94f;
            float largeW = loader != null ? loader.LargeLevelScreenWidthRatio : 0.93f;
            float smallH = loader != null ? loader.SmallLevelScreenHeightRatio : 0.82f;
            float largeH = loader != null ? loader.LargeLevelScreenHeightRatio : 0.74f;

            float effectiveSmallW = smallW < 0.5f ? 0.94f : smallW;
            float effectiveLargeW = largeW < 0.5f ? 0.93f : largeW;
            float effectiveSmallH = smallH < 0.5f ? 0.82f : smallH;
            float effectiveLargeH = largeH < 0.4f ? 0.74f : largeH;

            bool dynamicScale = loader != null ? loader.DynamicScaleByLevelSize : true;
            if (dynamicScale)
            {
                float smallThresh = loader != null ? loader.SmallLevelThreshold : 6f;
                float largeThresh = loader != null ? loader.LargeLevelThreshold : 12f;
                float activeDimension = Mathf.Max(bounds.activeWidth, bounds.activeHeight);
                float denom = Mathf.Max(0.01f, largeThresh - smallThresh);
                float t = Mathf.Clamp01((activeDimension - smallThresh) / denom);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                safeWidthRatio = Mathf.Lerp(effectiveSmallW, effectiveLargeW, smoothT);
                safeHeightRatio = Mathf.Lerp(effectiveSmallH, effectiveLargeH, smoothT);
            }
            else
            {
                float targetW = loader != null ? loader.TargetScreenWidthRatio : 0.94f;
                float targetH = loader != null ? loader.TargetScreenHeightRatio : 0.76f;
                safeWidthRatio = (targetW <= 0.85f) ? 0.94f : targetW;
                safeHeightRatio = (targetH <= 0.70f) ? 0.76f : targetH;
            }

            safeWidthRatio = Mathf.Clamp(safeWidthRatio, 0.2f, 0.98f);
            safeHeightRatio = Mathf.Clamp(safeHeightRatio, 0.2f, 0.98f);

            float pitch = loader != null ? loader.CameraPitchAngle : 70f;
            float yaw = loader != null ? loader.CameraYawAngle : 0f;
            Quaternion targetRotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 camForward = targetRotation * Vector3.forward;

            float pitchRad = Mathf.Clamp(pitch, 30f, 90f) * Mathf.Deg2Rad;
            float effectiveBoardHeight = boardHeight * Mathf.Sin(pitchRad) + (0.5f * cellSize) * Mathf.Cos(pitchRad);

            float visibleHeightByWidth = (boardWidth / safeWidthRatio) / aspect;
            float visibleHeightByHeight = effectiveBoardHeight / safeHeightRatio;
            float visibleHeight = Mathf.Max(visibleHeightByWidth, visibleHeightByHeight);

            float zoomMult = Mathf.Max(0.1f, loader != null ? loader.CameraZoomMultiplier : 1.0f);
            visibleHeight /= zoomMult;

            float vertOffset = loader != null ? loader.CameraVerticalOffset : 0f;
            Vector3 lookTarget = new Vector3(centerX, rootPos.y, centerZ + vertOffset);
            Vector3 targetPosition;
            float targetOrthoSize = visibleHeight * 0.5f;

            if (gameCamera.orthographic)
            {
                targetOrthoSize = Mathf.Clamp(targetOrthoSize, 1.5f, 50f);
                targetPosition = lookTarget - camForward * 30f;
            }
            else
            {
                float fovRad = Mathf.Clamp(gameCamera.fieldOfView, 10f, 120f) * Mathf.Deg2Rad;
                float halfFovTan = Mathf.Tan(fovRad * 0.5f);
                if (halfFovTan < 0.001f) halfFovTan = 0.57735f;

                float distance = (visibleHeight * 0.5f) / halfFovTan;
                float minCamDist = loader != null ? loader.MinCameraDistance : 3.5f;
                float maxCamDist = loader != null ? loader.MaxCameraDistance : 45f;
                distance = Mathf.Clamp(distance, minCamDist, maxCamDist);

                targetPosition = lookTarget - camForward * distance;
            }

            if (cameraTransitionCoroutine != null)
            {
                StopCoroutine(cameraTransitionCoroutine);
                cameraTransitionCoroutine = null;
            }

            bool smoothTransition = loader != null && loader.SmoothCameraTransition;
            if (!forceInstant && smoothTransition && Application.isPlaying && gameObject.activeInHierarchy)
            {
                float duration = loader != null ? loader.CameraTransitionDuration : 0.35f;
                cameraTransitionCoroutine = StartCoroutine(SmoothCameraTransitionRoutine(gameCamera, targetPosition, targetRotation, targetOrthoSize, duration));
            }
            else
            {
                gameCamera.transform.position = targetPosition;
                gameCamera.transform.rotation = targetRotation;
                if (gameCamera.orthographic)
                {
                    gameCamera.orthographicSize = targetOrthoSize;
                }

                if (loader != null && loader.VideoBackground != null)
                {
                    loader.VideoBackground.UpdateQuadTransform();
                }

#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    UnityEditor.EditorUtility.SetDirty(gameCamera.transform);
                }
#endif
            }
        }

        private IEnumerator SmoothCameraTransitionRoutine(Camera gameCamera, Vector3 targetPos, Quaternion targetRot, float targetOrthoSize, float duration)
        {
            if (gameCamera == null) yield break;

            Vector3 startPos = gameCamera.transform.position;
            Quaternion startRot = gameCamera.transform.rotation;
            float startOrtho = gameCamera.orthographicSize;
            float effectiveDuration = Mathf.Max(0.05f, duration);
            float elapsed = 0f;

            var loader = Loader;

            while (elapsed < effectiveDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / effectiveDuration);

                gameCamera.transform.position = Vector3.Lerp(startPos, targetPos, t);
                gameCamera.transform.rotation = Quaternion.Slerp(startRot, targetRot, t);

                if (gameCamera.orthographic)
                {
                    gameCamera.orthographicSize = Mathf.Lerp(startOrtho, targetOrthoSize, t);
                }

                if (loader != null && loader.VideoBackground != null)
                {
                    loader.VideoBackground.UpdateQuadTransform();
                }

                yield return null;
            }

            gameCamera.transform.position = targetPos;
            gameCamera.transform.rotation = targetRot;
            if (gameCamera.orthographic)
            {
                gameCamera.orthographicSize = targetOrthoSize;
            }

            if (loader != null && loader.VideoBackground != null)
            {
                loader.VideoBackground.UpdateQuadTransform();
            }

            cameraTransitionCoroutine = null;
        }

        public void EnsureVideoBackground()
        {
            var loader = Loader;
            if (loader == null) return;

            if (!loader.EnableVideoBackground)
            {
                if (loader.VideoBackground != null) loader.VideoBackground.gameObject.SetActive(false);
                return;
            }

            if (loader.VideoBackground == null)
            {
                loader.VideoBackground = FindAnyObjectByType<LevelVideoBackground>();
            }

            if (loader.VideoBackground == null)
            {
                GameObject bgObj = new GameObject("LevelVideoBackground");
                loader.VideoBackground = bgObj.AddComponent<LevelVideoBackground>();
            }

            if (loader.VideoBackground != null)
            {
                loader.VideoBackground.gameObject.SetActive(true);
                loader.VideoBackground.SetupVideoBackground();
            }
        }

        public void RefreshCameraFraming()
        {
            var loader = Loader;
            if (loader == null) return;

            if (loader.CurrentLevel != null)
            {
                CenterCamera(loader.CurrentLevel, true);
            }
            else if (loader.TestLevelData != null)
            {
                CenterCamera(loader.TestLevelData, true);
            }
            else if (loader.AllLevels != null && loader.AllLevels.Count > 0 && loader.AllLevels[0] != null)
            {
                CenterCamera(loader.AllLevels[0], true);
            }
        }

        public void ShakeCamera(float duration = 0.18f, float intensity = 0.35f)
        {
            Camera cam = EnsureCamera();
            if (cam == null) return;

            if (cameraShakeCoroutine != null)
            {
                StopCoroutine(cameraShakeCoroutine);
            }
            cameraShakeCoroutine = StartCoroutine(CameraShakeRoutine(cam, duration, intensity));
        }

        private IEnumerator CameraShakeRoutine(Camera cam, float duration, float intensity)
        {
            Vector3 originalCamPos = cam.transform.position;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float trauma = 1f - Mathf.Clamp01(elapsed / duration);
                float shakeAmount = intensity * trauma * trauma;

                Vector3 randomOffset = new Vector3(
                    UnityEngine.Random.Range(-1f, 1f) * shakeAmount,
                    UnityEngine.Random.Range(-0.3f, 0.3f) * shakeAmount,
                    UnityEngine.Random.Range(-1f, 1f) * shakeAmount
                );

                cam.transform.position = originalCamPos + randomOffset;
                yield return null;
            }

            cam.transform.position = originalCamPos;
            cameraShakeCoroutine = null;
        }
    }
}
