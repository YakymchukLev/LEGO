using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LegoPuzzle.Runtime
{
    /// <summary>
    /// Візуальний ефект підказки наступного ходу:
    /// 1. Анімована 3D-стрілочка, що ширяє над деталлю і вказує напрямок зсуву.
    /// 2. М'який мікро-зсув (Nudge) деталі в напрямку ходу, наочно показуючи рух.
    /// 3. Золотиста пульсація деталі.
    /// 4. Автоматичне зникнення при дотику гравця або після таймауту.
    /// </summary>
    public class HintIndicatorEffect : MonoBehaviour
    {
        private static HintIndicatorEffect activeInstance;
        public static HintIndicatorEffect ActiveInstance => activeInstance;

        private LegoPieceView targetPiece;
        private Vector2Int moveDelta;
        private float cellSize;
        private Vector3 originalPieceLocalPos;
        private Vector3 originalPieceScale;
        private GameObject arrowObject;
        private Coroutine nudgeCoroutine;
        private float autoDismissTimer = 6f;

        public static void Show(LegoPieceView piece, Vector2Int delta, float size = 1f)
        {
            Dismiss();

            if (piece == null || delta == Vector2Int.zero) return;

            GameObject obj = new GameObject("HintIndicatorEffect");
            obj.transform.SetParent(piece.transform.parent != null ? piece.transform.parent : piece.transform);

            HintIndicatorEffect effect = obj.AddComponent<HintIndicatorEffect>();
            effect.Initialize(piece, delta, size);
            activeInstance = effect;
        }

        public static void Dismiss()
        {
            if (activeInstance != null)
            {
                activeInstance.CleanupAndDestroy();
                activeInstance = null;
            }
        }

        private void Initialize(LegoPieceView piece, Vector2Int delta, float size)
        {
            targetPiece = piece;
            moveDelta = delta;
            cellSize = size;
            originalPieceLocalPos = targetPiece.transform.localPosition;
            originalPieceScale = targetPiece.transform.localScale;

            CreateArrowIndicator();
            nudgeCoroutine = StartCoroutine(HintAnimationRoutine());
        }

        private void Update()
        {
            if (targetPiece == null || !targetPiece.gameObject.activeInHierarchy)
            {
                Dismiss();
                return;
            }

            autoDismissTimer -= Time.deltaTime;
            if (autoDismissTimer <= 0f)
            {
                Dismiss();
            }
        }

        private void CreateArrowIndicator()
        {
            arrowObject = new GameObject("HintArrow");
            arrowObject.transform.SetParent(transform, false);

            Vector3 moveDir = new Vector3(moveDelta.x, 0f, moveDelta.y).normalized;
            Vector3 pieceCenter = targetPiece.transform.position;

            // Розраховуємо висоту над моделлю деталі
            float pieceHeight = 0.5f;
            var renderers = targetPiece.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                pieceCenter = b.center;
                pieceHeight = b.size.y;
            }

            Vector3 arrowStartPos = pieceCenter + Vector3.up * (pieceHeight * 0.7f + 0.35f);
            arrowObject.transform.position = arrowStartPos;

            // Орієнтуємо стрілку в напрямку руху
            if (moveDir.sqrMagnitude > 0.001f)
            {
                arrowObject.transform.rotation = Quaternion.LookRotation(moveDir, Vector3.up);
            }

            // Малюємо процедурну 3D-стрілку через LineRenderer
            Material lineMat = GetOrCreateArrowMaterial();

            // Стовбур стрілки
            GameObject stemObj = new GameObject("Stem");
            stemObj.transform.SetParent(arrowObject.transform, false);
            LineRenderer stem = stemObj.AddComponent<LineRenderer>();
            SetupLineRenderer(stem, lineMat, 0.14f, 0.14f);
            stem.positionCount = 2;
            stem.SetPositions(new Vector3[] { new Vector3(0f, 0f, -0.35f * cellSize), new Vector3(0f, 0f, 0.25f * cellSize) });

            // Наконечник стрілки (V-подібний шеврон)
            GameObject headObj = new GameObject("Head");
            headObj.transform.SetParent(arrowObject.transform, false);
            LineRenderer head = headObj.AddComponent<LineRenderer>();
            SetupLineRenderer(head, lineMat, 0.16f, 0.08f);
            head.positionCount = 3;
            float tipZ = 0.55f * cellSize;
            float barbX = 0.28f * cellSize;
            float barbZ = 0.18f * cellSize;
            head.SetPositions(new Vector3[]
            {
                new Vector3(-barbX, 0f, barbZ),
                new Vector3(0f, 0f, tipZ),
                new Vector3(barbX, 0f, barbZ)
            });
        }

        private static Material arrowMaterial;
        private static Material GetOrCreateArrowMaterial()
        {
            if (arrowMaterial != null) return arrowMaterial;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");

            arrowMaterial = new Material(shader != null ? shader : Shader.Find("Diffuse"));
            arrowMaterial.color = new Color(1f, 0.84f, 0.05f, 1f); // Яскраве неоново-золоте сяйво
            return arrowMaterial;
        }

        private void SetupLineRenderer(LineRenderer lr, Material mat, float startW, float endW)
        {
            lr.material = mat;
            lr.useWorldSpace = false;
            lr.startWidth = startW;
            lr.endWidth = endW;
            lr.startColor = new Color(1f, 0.88f, 0.15f, 1f);
            lr.endColor = new Color(1f, 0.65f, 0.05f, 1f);
            lr.numCapVertices = 4;
            lr.numCornerVertices = 4;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
        }

        private IEnumerator HintAnimationRoutine()
        {
            Vector3 moveDir = new Vector3(moveDelta.x, 0f, moveDelta.y).normalized;
            Vector3 arrowBasePos = arrowObject.transform.position;

            float elapsed = 0f;
            while (true)
            {
                elapsed += Time.deltaTime;

                if (targetPiece == null) yield break;

                // 1. Анімація польоту стрілочки вперед-назад
                float arrowSlide = Mathf.PingPong(elapsed * 2.4f, 0.35f * cellSize);
                arrowObject.transform.position = arrowBasePos + moveDir * arrowSlide + Vector3.up * (Mathf.Sin(elapsed * 4f) * 0.08f);

                // 2. М'який Nudge (мікро-зсув) самої деталі раз на цикл
                // Цикл триває 1.5 секунди: перші 0.5с - плавний кивок вперед-назад, 1.0с - спокій
                float cycleT = (elapsed % 1.5f);
                if (cycleT < 0.55f)
                {
                    float nudgeT = Mathf.Sin((cycleT / 0.55f) * Mathf.PI);
                    float nudgeDistance = 0.18f * cellSize * nudgeT;
                    Vector3 nudgeOffset = new Vector3(moveDir.x * nudgeDistance, 0f, moveDir.z * nudgeDistance);
                    targetPiece.transform.localPosition = originalPieceLocalPos + nudgeOffset;
                }
                else
                {
                    targetPiece.transform.localPosition = originalPieceLocalPos;
                }

                // 3. М'яка золотиста пульсація масштабу
                float pulse = 1f + 0.05f * Mathf.Sin(elapsed * 5f);
                targetPiece.transform.localScale = originalPieceScale * pulse;

                yield return null;
            }
        }

        private void CleanupAndDestroy()
        {
            if (nudgeCoroutine != null)
            {
                StopCoroutine(nudgeCoroutine);
                nudgeCoroutine = null;
            }

            if (targetPiece != null)
            {
                targetPiece.transform.localPosition = originalPieceLocalPos;
                targetPiece.transform.localScale = originalPieceScale;
            }

            Destroy(gameObject);
        }
    }
}
