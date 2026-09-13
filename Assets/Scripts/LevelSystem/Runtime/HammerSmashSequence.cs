using System;
using System.Collections;
using UnityEngine;

namespace LegoPuzzle.Runtime
{
    /// <summary>
    /// Контролер процедурної анімації замаху, удару та відскоку молотка (hammer.prefab).
    /// Створює 3D-модель над обраним блоком, плавно замахується, з силою вдаряє вниз,
    /// викликає ефект тріщин HammerCrackEffect, струшує камеру та знищує блок.
    /// </summary>
    public class HammerSmashSequence : MonoBehaviour
    {
        [Header("Налаштування Анімації")]
        [Tooltip("Тривалість фази замаху у секундах")]
        [SerializeField] private float windUpDuration = 0.32f;

        [Tooltip("Тривалість фази стрімкого удару вниз")]
        [SerializeField] private float strikeDuration = 0.13f;

        [Tooltip("Тривалість фази відскоку та зникнення")]
        [SerializeField] private float reboundDuration = 0.25f;

        [Tooltip("Базовий масштаб молотка відносно розміру клітинки (cellSize)")]
        [SerializeField] private float targetHammerHeight = 2.4f;

        private GameObject hammerInstance;
        private Transform pivotTransform;
        private Vector3 initialPivotScale = Vector3.one;

        /// <summary>
        /// Запускає послідовність удару молотка по блоку.
        /// </summary>
        public static HammerSmashSequence Play(
            GameObject hammerPrefab,
            LegoPieceView targetPiece,
            LevelLoader levelLoader,
            AudioClip smashSound = null,
            Action onComplete = null)
        {
            if (targetPiece == null || levelLoader == null)
            {
                onComplete?.Invoke();
                return null;
            }

            GameObject sequenceObj = new GameObject("HammerSmashSequence");
            HammerSmashSequence sequence = sequenceObj.AddComponent<HammerSmashSequence>();
            sequence.StartCoroutine(sequence.ExecuteSequenceRoutine(hammerPrefab, targetPiece, levelLoader, smashSound, onComplete));
            return sequence;
        }

        private IEnumerator ExecuteSequenceRoutine(
            GameObject hammerPrefab,
            LegoPieceView targetPiece,
            LevelLoader levelLoader,
            AudioClip smashSound,
            Action onComplete)
        {
            if (targetPiece == null || levelLoader == null)
            {
                onComplete?.Invoke();
                Destroy(gameObject);
                yield break;
            }

            // Розраховуємо центр обраної деталі у світових координатах
            Vector3 pieceCenterWorld = targetPiece.transform.position;
            var renderers = targetPiece.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds pieceBounds = renderers[0].bounds;
                for (int r = 1; r < renderers.Length; r++)
                {
                    pieceBounds.Encapsulate(renderers[r].bounds);
                }
                pieceCenterWorld = pieceBounds.center;
            }

            float boardY = levelLoader.BoardBaseY;

            // 1. Створюємо батьківський Pivot для обертання та переміщення молотка
            GameObject pivotObj = new GameObject("HammerPivot");
            pivotObj.transform.SetParent(transform, false);
            pivotTransform = pivotObj.transform;

            // 2. Спавнимо модель молотка
            if (hammerPrefab != null)
            {
                hammerInstance = Instantiate(hammerPrefab, pivotTransform);
                hammerInstance.name = "HammerModel";

                // Знешкоджуємо фізику/колайдери префабу
                foreach (var rb in hammerInstance.GetComponentsInChildren<Rigidbody>(true)) Destroy(rb);
                foreach (var c in hammerInstance.GetComponentsInChildren<Collider>(true)) Destroy(c);

                SetupHammerModelOrientation(hammerInstance);
            }
            else
            {
                // Резервний процедурний молоток, якщо префаб не задано
                CreateFallbackHammer(pivotTransform);
            }

            // Позиції та кути для анімації:
            // Точка удару бойка молотка (точно на верхній площині блоку)
            Vector3 impactPosition = new Vector3(pieceCenterWorld.x, pieceCenterWorld.y + 0.12f, pieceCenterWorld.z);
            // Верхня точка замаху (бойок піднятий високо над блоком, рукоятка відведена назад до гравця)
            Vector3 windUpPosition = impactPosition + new Vector3(0.2f, 2.2f, -0.7f);
            Vector3 reboundPosition = impactPosition + new Vector3(0f, 0.45f, -0.15f);

            // Кути:
            // Удар: 90° по осі X спрямовує бойок молотка строго вниз у блок, а рукоятку тримає позаду
            Quaternion strikeRotation = Quaternion.Euler(90f, 0f, 0f);
            // Замах: молоток піднятий високо в повітря і відхилений назад на ~22° для природного замаху
            Quaternion windUpRotation = Quaternion.Euler(22f, -12f, 4f);
            // Відскок: пружний відбій після удару
            Quaternion reboundRotation = Quaternion.Euler(72f, 0f, 0f);

            pivotTransform.position = windUpPosition;
            pivotTransform.rotation = windUpRotation;
            pivotTransform.localScale = Vector3.zero;

            // ==========================================
            // ФАЗА 1: Поява та Замах (Wind-Up)
            // ==========================================
            float elapsed = 0f;
            while (elapsed < windUpDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / windUpDuration);

                // Плавне пружне збільшення масштабу
                float scaleT = Mathf.Sin(t * Mathf.PI * 0.5f);
                pivotTransform.localScale = initialPivotScale * scaleT;

                // Рух у найвищу точку замаху
                float moveT = Mathf.SmoothStep(0f, 1f, t);
                pivotTransform.position = Vector3.Lerp(windUpPosition - Vector3.up * 0.8f, windUpPosition, moveT);
                pivotTransform.rotation = Quaternion.Slerp(Quaternion.Euler(45f, -8f, 2f), windUpRotation, moveT);

                yield return null;
            }

            // Коротка напружена мікро-пауза на піку замаху перед ударом
            yield return new WaitForSeconds(0.04f);

            // ==========================================
            // ФАЗА 2: Стрімкий Удар (Smash Down)
            // ==========================================
            elapsed = 0f;
            Vector3 startStrikePos = pivotTransform.position;
            Quaternion startStrikeRot = pivotTransform.rotation;

            while (elapsed < strikeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / strikeDuration);

                // Потужне експоненційне прискорення вниз (Ease In Cubic)
                float smashT = t * t * t;

                pivotTransform.position = Vector3.LerpUnclamped(startStrikePos, impactPosition, smashT);
                pivotTransform.rotation = Quaternion.SlerpUnclamped(startStrikeRot, strikeRotation, smashT);

                yield return null;
            }

            pivotTransform.position = impactPosition;
            pivotTransform.rotation = strikeRotation;

            // ==========================================
            // ФАЗА 3: Момент Контакту (Impact Event)
            // ==========================================
            Vector3 crackEpicenter = new Vector3(pieceCenterWorld.x, boardY + 0.02f, pieceCenterWorld.z);
            HammerCrackEffect.SpawnAt(crackEpicenter, radius: 1.8f);

            // Струшування камери для "соковитості" удару
            if (levelLoader != null)
            {
                levelLoader.ShakeCamera(0.18f, 0.35f);
            }

            // Звук удару
            if (levelLoader != null)
            {
                levelLoader.PlaySmashSound(smashSound);
            }

            // Знищення блоку зі спавном 3D-уламків
            if (targetPiece != null)
            {
                targetPiece.SpawnShatterDebris();
                levelLoader.DestroyPieceWithHammer(targetPiece);
            }

            // ==========================================
            // ФАЗА 4: Відскок та Зникнення (Rebound & Fade)
            // ==========================================
            elapsed = 0f;

            while (elapsed < reboundDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / reboundDuration);

                // Відскок вгору
                float reboundT = Mathf.Sin(t * Mathf.PI * 0.5f);
                pivotTransform.position = Vector3.Lerp(impactPosition, reboundPosition, reboundT);
                pivotTransform.rotation = Quaternion.Slerp(strikeRotation, reboundRotation, reboundT);

                // Зменшення до нуля в другій половині відскоку
                if (t > 0.35f)
                {
                    float shrinkT = (t - 0.35f) / 0.65f;
                    pivotTransform.localScale = Vector3.Lerp(initialPivotScale, Vector3.zero, shrinkT * shrinkT);
                }

                yield return null;
            }

            onComplete?.Invoke();
            Destroy(gameObject);
        }

        private void SetupHammerModelOrientation(GameObject model)
        {
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            Bounds bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);

            // Визначаємо найбільший габарит молотка для точного масштабування
            float maxDim = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (maxDim > 0.05f)
            {
                float targetScale = targetHammerHeight / maxDim;
                model.transform.localScale = Vector3.one * targetScale;
            }

            // Оновлюємо bounds після масштабування
            bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);

            // Центруємо так, щоб саме ГОЛОВА/БОЙОК молотка (а не рукоятка!) знаходилися в локальному нулі (0,0,0) pivotTransform.
            // У цій 3D-моделі голова знаходиться у верхній частині (близько 76% висоти по Y), а бойок - спереду по Z.
            float headCenterY = bounds.min.y + bounds.size.y * 0.76f;
            float strikingFaceZ = bounds.max.z;
            float centerX = bounds.center.x;

            Vector3 headOffset = new Vector3(
                centerX - model.transform.position.x,
                headCenterY - model.transform.position.y,
                strikingFaceZ - model.transform.position.z
            );
            model.transform.localPosition = -headOffset;

            initialPivotScale = Vector3.one;
        }

        private void CreateFallbackHammer(Transform parent)
        {
            // Голова молотка - точно в нулі pivotTransform (точка удару)
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            head.transform.SetParent(parent, false);
            head.transform.localScale = new Vector3(0.45f, 0.35f, 0.75f);
            head.transform.localPosition = Vector3.zero;
            Destroy(head.GetComponent<Collider>());

            var headRenderer = head.GetComponent<Renderer>();
            if (headRenderer != null)
            {
                headRenderer.material.color = new Color(0.25f, 0.25f, 0.28f);
            }

            // Рукоятка - виходить з голови молотка назад
            GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            handle.transform.SetParent(parent, false);
            handle.transform.localScale = new Vector3(0.12f, 0.7f, 0.12f);
            handle.transform.localPosition = new Vector3(0f, -0.7f, 0f);
            Destroy(handle.GetComponent<Collider>());

            var handleRenderer = handle.GetComponent<Renderer>();
            if (handleRenderer != null)
            {
                handleRenderer.material.color = new Color(0.55f, 0.27f, 0.07f);
            }

            initialPivotScale = Vector3.one;
        }
    }
}
