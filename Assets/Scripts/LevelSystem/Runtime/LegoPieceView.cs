using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using LegoPuzzle.Data;

namespace LegoPuzzle.Runtime
{
    public class LegoPieceView : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("Дані деталі")]
        public LegoPieceData PieceData { get; private set; }
        public Vector2Int CurrentOrigin { get; private set; }
        public void SetCurrentOriginDirect(Vector2Int origin) => CurrentOrigin = origin;

        [Header("Посилання")]
        [SerializeField] private GameObject modelInstance;
        [SerializeField] private SpriteRenderer restrictionIconRenderer;

        [Header("Параметри анімації")]
        [SerializeField] private float snapDuration = 0.1f;

        private LevelLoader levelLoader;
        private float cellSize = 1f;
        private bool isDragging = false;
        private bool isSnapping = false;
        private bool isExiting = false;

        private Vector3 dragStartPointerWorld;
        private Vector3 dragStartLocalPosition;
        private float minWorldX, maxWorldX;
        private float minWorldZ, maxWorldZ;
        private List<Vector2Int> currentOffsets;

        public event Action<LegoPieceView> OnExited;

        public void Initialize(LegoPieceData data, LevelLoader loader, BlockPalette palette, float size, GameObject visualChild = null)
        {
            PieceData = data;
            levelLoader = loader;
            cellSize = size;
            CurrentOrigin = data.originPosition;
            modelInstance = visualChild;
            currentOffsets = data.shape != null ? data.shape.GetRotatedOffsets(data.rotationSteps) : new List<Vector2Int> { Vector2Int.zero };

            // Повністю знешкоджуємо фізику та старі скрипти на дочірній 3D-моделі
            if (modelInstance != null)
            {
                var rbs = modelInstance.GetComponentsInChildren<Rigidbody>(true);
                foreach (var rb in rbs)
                {
                    rb.isKinematic = true;
                    rb.detectCollisions = false;
                    Destroy(rb);
                }

                var oldMono = modelInstance.GetComponentsInChildren<MonoBehaviour>(true);
                foreach (var m in oldMono)
                {
                    if (m != this)
                    {
                        m.enabled = false;
                        Destroy(m);
                    }
                }

                var oldCols = modelInstance.GetComponentsInChildren<Collider>(true);
                foreach (var c in oldCols)
                {
                    c.enabled = false;
                    Destroy(c);
                }
            }

            UpdateWorldPosition();
            SetupVisualModel(palette);
            SetupRestrictionIcon(palette);
            SetupCollider();
        }

        private void SetupCollider()
        {
            var existingCols = GetComponents<Collider>();
            foreach (var c in existingCols)
            {
                Destroy(c);
            }

            // Вимірюємо точну висоту моделі
            float modelHeight = 0.5f;
            if (modelInstance != null)
            {
                var renderers = modelInstance.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    modelHeight = renderers[0].bounds.size.y;
                }
            }

            // Створюємо окремий BoxCollider для кожної зайнятої клітинки фігури
            // Це виключає помилкові колізії на порожніх кутах L/T/Хрест форм
            foreach (var offset in currentOffsets)
            {
                var box = gameObject.AddComponent<BoxCollider>();
                box.center = new Vector3(offset.x * cellSize, modelHeight * 0.5f + 0.02f, offset.y * cellSize);
                box.size = new Vector3(cellSize * 0.95f, Mathf.Max(modelHeight, 0.4f), cellSize * 0.95f);
            }
        }

        private void SetupVisualModel(BlockPalette palette)
        {
            Color blockColor = PieceData.GetColor();

            if (modelInstance != null)
            {
                ApplyColor(modelInstance, blockColor);
                AlignAndScaleModel(modelInstance);
            }
            else if (palette != null && palette.legoStudUnitPrefab != null)
            {
                // Якщо немає 3D-моделі, процедурно збираємо блоки з одиничних секцій
                GameObject unitContainer = new GameObject("ProceduralVisuals");
                unitContainer.transform.SetParent(transform);
                unitContainer.transform.localPosition = Vector3.zero;

                foreach (var offset in currentOffsets)
                {
                    GameObject unit = Instantiate(palette.legoStudUnitPrefab, unitContainer.transform);
                    unit.transform.localPosition = new Vector3(offset.x * cellSize, 0.02f, offset.y * cellSize);
                    ApplyColor(unit, blockColor);
                }

                modelInstance = unitContainer;
            }
        }

        private void AlignAndScaleModel(GameObject model)
        {
            if (model == null || PieceData.shape == null) return;

            Vector2Int shapeBounds = PieceData.shape.GetRotatedBounds(PieceData.rotationSteps);
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            // 1. Поворот моделі відповідно до rotationSteps (за годинниковою стрілкою +90° за крок)
            float angle = PieceData.rotationSteps * 90f;
            model.transform.localRotation = Quaternion.Euler(0f, angle, 0f);
            model.transform.localPosition = Vector3.zero;

            // 2. Вимірюємо габарити моделі
            Bounds bounds = renderers[0].bounds;
            foreach (var r in renderers)
            {
                if (r == restrictionIconRenderer) continue;
                bounds.Encapsulate(r.bounds);
            }

            float meshSizeX = bounds.size.x;
            float meshSizeZ = bounds.size.z;
            if (meshSizeZ < 0.05f && bounds.size.y > 0.05f)
            {
                meshSizeZ = bounds.size.y;
            }

            float targetSizeX = shapeBounds.x * cellSize;
            float targetSizeZ = shapeBounds.y * cellSize;

            // 3. Масштабуємо модель під клітинки сітки
            if (meshSizeX > 0.05f && targetSizeX > 0.05f)
            {
                float factorX = targetSizeX / meshSizeX;
                float factorZ = targetSizeZ / meshSizeZ;
                float scale = Mathf.Min(factorX, factorZ);

                model.transform.localScale = Vector3.one * scale;
            }

            // 4. Перераховуємо межі після масштабування
            bounds = renderers[0].bounds;
            foreach (var r in renderers)
            {
                if (r == restrictionIconRenderer) continue;
                bounds.Encapsulate(r.bounds);
            }

            // 5. Вирівнювання по X та Z (центр сітки)
            Vector3 targetCenter = new Vector3((shapeBounds.x - 1) * cellSize * 0.5f, 0f, (shapeBounds.y - 1) * cellSize * 0.5f);
            Vector3 currentCenterOffset = bounds.center - transform.position;
            float shiftX = targetCenter.x - currentCenterOffset.x;
            float shiftZ = targetCenter.z - currentCenterOffset.z;

            // 6. Вирівнювання по Y: піднімаємо низ моделі строго на поверхню столу (Y = 0.02f)
            float currentBottomY = bounds.min.y - transform.position.y;
            float shiftY = 0.02f - currentBottomY;

            model.transform.localPosition = new Vector3(shiftX, shiftY, shiftZ);
        }

        private void ApplyColor(GameObject target, Color color)
        {
            var renderers = target.GetComponentsInChildren<Renderer>();
            MaterialPropertyBlock mpb = new MaterialPropertyBlock();
            foreach (var r in renderers)
            {
                if (r == restrictionIconRenderer) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetColor("_Color", color);
                mpb.SetColor("_BaseColor", color);
                r.SetPropertyBlock(mpb);
            }
        }

        private void SetupRestrictionIcon(BlockPalette palette)
        {
            if (PieceData.moveRestriction == MoveRestriction.Free || PieceData.moveRestriction == MoveRestriction.Locked)
            {
                if (restrictionIconRenderer != null) restrictionIconRenderer.gameObject.SetActive(false);
                return;
            }

            if (restrictionIconRenderer == null)
            {
                GameObject iconObj = new GameObject("RestrictionIcon");
                iconObj.transform.SetParent(transform);
                restrictionIconRenderer = iconObj.AddComponent<SpriteRenderer>();
            }

            restrictionIconRenderer.gameObject.SetActive(true);

            if (PieceData.moveRestriction == MoveRestriction.HorizontalOnly && palette != null)
            {
                restrictionIconRenderer.sprite = palette.horizontalArrowSprite;
            }
            else if (PieceData.moveRestriction == MoveRestriction.VerticalOnly && palette != null)
            {
                restrictionIconRenderer.sprite = palette.verticalArrowSprite;
            }

            if (currentOffsets.Count > 0)
            {
                float avgX = 0f, avgY = 0f;
                foreach (var o in currentOffsets) { avgX += o.x; avgY += o.y; }
                avgX /= currentOffsets.Count;
                avgY /= currentOffsets.Count;
                restrictionIconRenderer.transform.localPosition = new Vector3(avgX * cellSize, 0.8f, avgY * cellSize);
                restrictionIconRenderer.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
        }

        public List<Vector2Int> GetCurrentOccupiedCells()
        {
            List<Vector2Int> cells = new List<Vector2Int>(currentOffsets.Count);
            foreach (var offset in currentOffsets)
            {
                cells.Add(CurrentOrigin + offset);
            }
            return cells;
        }

        public void UpdateWorldPosition()
        {
            transform.localPosition = new Vector3(CurrentOrigin.x * cellSize, 0.1f, CurrentOrigin.y * cellSize);
        }

        private Vector3 pointerOffset;
        private Vector2Int lastValidGridPos;

        // ==========================================
        // EventSystem Touch & Drag Реалізація
        // ==========================================

        public void OnPointerDown(PointerEventData eventData)
        {
            if (isExiting || isSnapping || levelLoader == null || !levelLoader.IsGameplayActive) return;

            // Приховуємо активну підказку, щойно гравець торкається деталі
            HintIndicatorEffect.Dismiss();

            // Перехоплення кліку у режимі прицілювання бустера "Молоток"
            if (HammerBooster.IsTargeting)
            {
                HammerBooster.SelectTarget(this);
                return;
            }

            if (PieceData.moveRestriction == MoveRestriction.Locked) return;

            Vector3 touchWorld = GetWorldPointerPosition(eventData);
            Vector3 touchLocal = transform.parent != null ? transform.parent.InverseTransformPoint(touchWorld) : touchWorld;
            pointerOffset = transform.localPosition - touchLocal;
            lastValidGridPos = CurrentOrigin;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (HammerBooster.IsTargeting) return;
            if (isExiting || isSnapping || levelLoader == null || !levelLoader.IsGameplayActive) return;
            if (PieceData.moveRestriction == MoveRestriction.Locked) return;

            isDragging = true;
            Vector3 touchWorld = GetWorldPointerPosition(eventData);
            Vector3 touchLocal = transform.parent != null ? transform.parent.InverseTransformPoint(touchWorld) : touchWorld;
            pointerOffset = transform.localPosition - touchLocal;
            lastValidGridPos = CurrentOrigin;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (levelLoader != null && !levelLoader.IsGameplayActive)
            {
                if (isDragging)
                {
                    isDragging = false;
                    StartCoroutine(SnapToGridRoutine(CurrentOrigin));
                }
                return;
            }

            if (!isDragging && !isSnapping && !isExiting)
            {
                isDragging = true;
                Vector3 touchWorld = GetWorldPointerPosition(eventData);
                Vector3 touchLocal = transform.parent != null ? transform.parent.InverseTransformPoint(touchWorld) : touchWorld;
                pointerOffset = transform.localPosition - touchLocal;
                lastValidGridPos = CurrentOrigin;
            }

            if (!isDragging || isExiting) return;

            Vector3 touchWorldPos = GetWorldPointerPosition(eventData);
            Vector3 touchLocalPos = transform.parent != null ? transform.parent.InverseTransformPoint(touchWorldPos) : touchWorldPos;
            Vector3 desiredPos = touchLocalPos + pointerOffset;

            // 1. Отримуємо актуальний діапазон вільних клітинок від поточної зафіксованої позиції
            int minX = 0, maxX = 0, minZ = 0, maxZ = 0;

            if (PieceData.moveRestriction != MoveRestriction.VerticalOnly && PieceData.moveRestriction != MoveRestriction.Locked)
            {
                levelLoader.GetSlideRangeHorizontal(this, lastValidGridPos, out minX, out maxX);
            }
            if (PieceData.moveRestriction != MoveRestriction.HorizontalOnly && PieceData.moveRestriction != MoveRestriction.Locked)
            {
                levelLoader.GetSlideRangeVertical(this, lastValidGridPos, out minZ, out maxZ);
            }

            float minWorldX = (lastValidGridPos.x + minX) * cellSize;
            float maxWorldX = (lastValidGridPos.x + maxX) * cellSize;
            float minWorldZ = (lastValidGridPos.y + minZ) * cellSize;
            float maxWorldZ = (lastValidGridPos.y + maxZ) * cellSize;

            // 2. Безперервне плавне обмеження руху
            float clampedX = Mathf.Clamp(desiredPos.x, minWorldX, maxWorldX);
            float clampedZ = Mathf.Clamp(desiredPos.z, minWorldZ, maxWorldZ);

            if (PieceData.moveRestriction == MoveRestriction.HorizontalOnly)
            {
                clampedZ = lastValidGridPos.y * cellSize;
            }
            else if (PieceData.moveRestriction == MoveRestriction.VerticalOnly)
            {
                clampedX = lastValidGridPos.x * cellSize;
            }

            // Плавне переміщення строго за пальцем без ривків
            transform.localPosition = new Vector3(clampedX, 0.1f, clampedZ);

            // 3. Оновлення поточної зайнятої клітинки на сітці з роздільною перевіркою осей
            int targetX = Mathf.RoundToInt(clampedX / cellSize);
            int targetY = Mathf.RoundToInt(clampedZ / cellSize);

            float deltaX = Mathf.Abs(clampedX - lastValidGridPos.x * cellSize);
            float deltaZ = Mathf.Abs(clampedZ - lastValidGridPos.y * cellSize);

            if (deltaX >= deltaZ)
            {
                // Перевіряємо крок по головній осі X
                Vector2Int candX = new Vector2Int(targetX, lastValidGridPos.y);
                if (candX != lastValidGridPos && levelLoader.CanMovePieceTo(this, candX))
                {
                    lastValidGridPos = candX;
                    CurrentOrigin = candX;
                    if (levelLoader != null) levelLoader.PlayPieceMoveSound();
                }

                // Перевіряємо крок по осі Z
                Vector2Int candZ = new Vector2Int(lastValidGridPos.x, targetY);
                if (candZ != lastValidGridPos && levelLoader.CanMovePieceTo(this, candZ))
                {
                    lastValidGridPos = candZ;
                    CurrentOrigin = candZ;
                    if (levelLoader != null) levelLoader.PlayPieceMoveSound();
                }
            }
            else
            {
                // Перевіряємо крок по головній осі Z
                Vector2Int candZ = new Vector2Int(lastValidGridPos.x, targetY);
                if (candZ != lastValidGridPos && levelLoader.CanMovePieceTo(this, candZ))
                {
                    lastValidGridPos = candZ;
                    CurrentOrigin = candZ;
                    if (levelLoader != null) levelLoader.PlayPieceMoveSound();
                }

                // Перевіряємо крок по осі X
                Vector2Int candX = new Vector2Int(targetX, lastValidGridPos.y);
                if (candX != lastValidGridPos && levelLoader.CanMovePieceTo(this, candX))
                {
                    lastValidGridPos = candX;
                    CurrentOrigin = candX;
                    if (levelLoader != null) levelLoader.PlayPieceMoveSound();
                }
            }

            // Миттєвий вихід у ворота під час перетягування (навіть якщо палець ще не відпущено)
            if (!isExiting && levelLoader.CheckIfPieceExits(this, CurrentOrigin, out ExitDirection exitDir))
            {
                isDragging = false;
                StartCoroutine(PlayExitAnimation(exitDir));
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!isDragging || isExiting) return;
            isDragging = false;

            int targetGridX = Mathf.RoundToInt(transform.localPosition.x / cellSize);
            int targetGridY = Mathf.RoundToInt(transform.localPosition.z / cellSize);

            Vector2Int targetOrigin = new Vector2Int(targetGridX, targetGridY);

            if (levelLoader.CanMovePieceTo(this, targetOrigin))
            {
                CurrentOrigin = targetOrigin;
            }

            if (levelLoader != null)
            {
                levelLoader.PlayPieceStepSound();
            }

            StartCoroutine(SnapToGridRoutine(CurrentOrigin));
        }

        private IEnumerator SnapToGridRoutine(Vector2Int targetOrigin)
        {
            if (isExiting) yield break;
            isSnapping = true;

            Vector3 startPos = transform.localPosition;
            Vector3 endPos = new Vector3(targetOrigin.x * cellSize, 0.1f, targetOrigin.y * cellSize);

            float elapsed = 0f;
            while (elapsed < snapDuration)
            {
                if (isExiting) yield break;
                float dt = Time.timeScale > 0.001f ? Time.deltaTime : Time.unscaledDeltaTime;
                elapsed += dt;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / snapDuration);
                transform.localPosition = Vector3.Lerp(startPos, endPos, t);
                yield return null;
            }

            transform.localPosition = endPos;
            CurrentOrigin = targetOrigin;
            isSnapping = false;

            if (!isExiting && levelLoader.CheckIfPieceExits(this, CurrentOrigin, out ExitDirection exitDir))
            {
                StartCoroutine(PlayExitAnimation(exitDir));
            }
        }

        private IEnumerator PlayExitAnimation(ExitDirection direction)
        {
            isExiting = true;
            isDragging = false;
            isSnapping = false;

            if (levelLoader != null)
            {
                levelLoader.PlayPieceExitSound();
            }

            Vector3 exitOffset = direction switch
            {
                ExitDirection.Up => Vector3.forward * 10f,
                ExitDirection.Down => Vector3.back * 10f,
                ExitDirection.Left => Vector3.left * 10f,
                ExitDirection.Right => Vector3.right * 10f,
                _ => Vector3.forward * 10f
            };

            Vector3 startPos = transform.localPosition;
            Vector3 endPos = startPos + exitOffset;
            float duration = 0.35f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                transform.localPosition = Vector3.Lerp(startPos, endPos, t);
                yield return null;
            }

            OnExited?.Invoke(this);
            gameObject.SetActive(false);
        }

        private Vector3 GetWorldPointerPosition(PointerEventData eventData)
        {
            Camera cam = eventData.pressEventCamera != null ? eventData.pressEventCamera : Camera.main;
            if (cam == null) return transform.position;

            Ray ray = cam.ScreenPointToRay(eventData.position);
            Plane plane = new Plane(Vector3.up, new Vector3(0f, 0.1f, 0f));

            if (plane.Raycast(ray, out float enter))
            {
                return ray.GetPoint(enter);
            }

            return transform.position;
        }

        /// <summary>
        /// Створює соковитий 3D-вибух уламків цеглинок LEGO при ударі молотком.
        /// </summary>
        public void SpawnShatterDebris()
        {
            Color pieceColor = PieceData != null ? PieceData.GetColor() : Color.red;
            Vector3 center = transform.position;

            GameObject debrisRoot = new GameObject("LegoShatterDebris");
            debrisRoot.transform.position = center;

            int chunkCount = Mathf.Clamp(currentOffsets != null ? currentOffsets.Count * 4 : 8, 8, 18);
            for (int i = 0; i < chunkCount; i++)
            {
                GameObject chunk = GameObject.CreatePrimitive(PrimitiveType.Cube);
                chunk.transform.SetParent(debrisRoot.transform);

                Vector3 spawnOffset = new Vector3(
                    UnityEngine.Random.Range(-0.4f, 0.4f) * cellSize,
                    UnityEngine.Random.Range(0.05f, 0.45f),
                    UnityEngine.Random.Range(-0.4f, 0.4f) * cellSize
                );
                chunk.transform.position = center + spawnOffset;

                float size = UnityEngine.Random.Range(0.14f, 0.26f) * cellSize;
                chunk.transform.localScale = new Vector3(size, size * 0.75f, size);

                var r = chunk.GetComponent<Renderer>();
                if (r != null)
                {
                    r.material.color = Color.Lerp(pieceColor, Color.white, UnityEngine.Random.Range(0f, 0.3f));
                }

                var col = chunk.GetComponent<Collider>();
                if (col != null) Destroy(col);

                Vector3 blastDir = (spawnOffset.normalized + Vector3.up * 1.8f).normalized;
                Vector3 initialVel = blastDir * UnityEngine.Random.Range(4f, 7.5f) + UnityEngine.Random.insideUnitSphere * 1.5f;
                Vector3 rotSpeed = UnityEngine.Random.insideUnitSphere * 360f;

                debrisRoot.AddComponent<PieceDebrisMotion>().Launch(chunk, initialVel, rotSpeed, levelLoader != null ? levelLoader.BoardBaseY : 0f);
            }

            Destroy(debrisRoot, 1.4f);
        }

        private class PieceDebrisMotion : MonoBehaviour
        {
            public void Launch(GameObject target, Vector3 velocity, Vector3 rotSpeed, float floorY)
            {
                StartCoroutine(MotionRoutine(target, velocity, rotSpeed, floorY));
            }

            private IEnumerator MotionRoutine(GameObject target, Vector3 velocity, Vector3 rotSpeed, float floorY)
            {
                float lifetime = 1.2f;
                float elapsed = 0f;
                Vector3 pos = target != null ? target.transform.position : Vector3.zero;

                while (elapsed < lifetime && target != null)
                {
                    elapsed += Time.deltaTime;
                    velocity.y -= 16f * Time.deltaTime;
                    pos += velocity * Time.deltaTime;

                    if (pos.y < floorY + 0.05f)
                    {
                        pos.y = floorY + 0.05f;
                        velocity.y = -velocity.y * 0.35f;
                        velocity.x *= 0.6f;
                        velocity.z *= 0.6f;
                    }

                    target.transform.position = pos;
                    target.transform.Rotate(rotSpeed * Time.deltaTime);

                    if (elapsed > 0.8f)
                    {
                        float shrink = 1f - ((elapsed - 0.8f) / 0.4f);
                        target.transform.localScale = target.transform.localScale * Mathf.Clamp01(shrink);
                    }

                    yield return null;
                }
            }
        }
    }
}
