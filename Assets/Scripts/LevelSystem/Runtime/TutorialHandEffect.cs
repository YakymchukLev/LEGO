using System.Collections;
using UnityEngine;

namespace LegoPuzzle.Runtime
{
    /// <summary>
    /// Візуальний ефект туторіалу: анімована рука, що вказує напрямок руху блоку.
    /// Автоматично зникає при дотику або зміні ходу.
    /// </summary>
    public class TutorialHandEffect : MonoBehaviour
    {
        private static TutorialHandEffect activeInstance;
        public static TutorialHandEffect ActiveInstance => activeInstance;

        private LegoPieceView targetPiece;
        private Vector2Int moveDelta;
        private float cellSize;
        private GameObject handObject;
        private Coroutine animationCoroutine;

        public static void Show(LegoPieceView piece, Vector2Int delta, float size = 1f, GameObject customHandPrefab = null)
        {
            Dismiss();

            if (piece == null) return;

            GameObject obj = new GameObject("TutorialHandEffect");
            obj.transform.SetParent(piece.transform.parent != null ? piece.transform.parent : piece.transform);

            TutorialHandEffect effect = obj.AddComponent<TutorialHandEffect>();
            effect.Initialize(piece, delta, size, customHandPrefab);
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

        private void Initialize(LegoPieceView piece, Vector2Int delta, float size, GameObject customHandPrefab)
        {
            targetPiece = piece;
            moveDelta = delta;
            cellSize = size;

            // Зменшуємо масштаб кореневого об'єкта, щоб обійти можливі конфлікти з Animator на префабі руки
            transform.localScale = Vector3.one * 0.4f;

            CreateHandIndicator(customHandPrefab);
            animationCoroutine = StartCoroutine(HandAnimationRoutine());
        }

        private RectTransform targetUI;

        public static void ShowUI(RectTransform uiTarget, GameObject customHandPrefab = null)
        {
            Dismiss();
            if (uiTarget == null) return;

            GameObject obj = new GameObject("TutorialHandEffectUI");
            obj.transform.SetParent(uiTarget.transform.parent != null ? uiTarget.transform.parent : uiTarget.transform, false);

            var canvas = obj.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 20;

            TutorialHandEffect effect = obj.AddComponent<TutorialHandEffect>();
            effect.InitializeUI(uiTarget, customHandPrefab);
            activeInstance = effect;
        }

        private void InitializeUI(RectTransform uiTarget, GameObject customHandPrefab)
        {
            targetUI = uiTarget;
            // Зменшуємо масштаб втричі (1.0 / 3 ~ 0.33f)
            transform.localScale = Vector3.one * 0.33f;

            if (customHandPrefab != null)
            {
                if (customHandPrefab.GetComponentInChildren<UnityEngine.UI.Image>() != null || customHandPrefab.GetComponent<RectTransform>() != null)
                {
                    handObject = Instantiate(customHandPrefab, transform);
                }
                else
                {
                    handObject = new GameObject("HandImage", typeof(RectTransform));
                    handObject.transform.SetParent(transform, false);
                    var img = handObject.AddComponent<UnityEngine.UI.Image>();
                    img.raycastTarget = false;
                    
                    var srPrefab = customHandPrefab.GetComponentInChildren<SpriteRenderer>();
                    if (srPrefab != null && srPrefab.sprite != null)
                    {
                        img.sprite = srPrefab.sprite;
                        img.SetNativeSize();
                    }
                    else
                    {
                        Sprite handSprite = Resources.Load<Sprite>("HandIcon");
                        if (handSprite != null)
                        {
                            img.sprite = handSprite;
                            img.SetNativeSize();
                        }
                    }
                }
            }
            else
            {
                handObject = new GameObject("HandImage", typeof(RectTransform));
                handObject.transform.SetParent(transform, false);
                var img = handObject.AddComponent<UnityEngine.UI.Image>();
                img.raycastTarget = false;
                Sprite handSprite = Resources.Load<Sprite>("HandIcon");
                if (handSprite != null)
                {
                    img.sprite = handSprite;
                    img.SetNativeSize();
                }
            }

            var sr = handObject.GetComponentInChildren<SpriteRenderer>();
            if (sr != null) sr.sortingOrder = 100;

            handObject.transform.position = uiTarget.position;
            
            // Задаємо локальне зміщення, щоб рука була трохи збоку
            handObject.transform.localPosition += new Vector3(50f, -50f, 0f);
            // Для UI краще не крутити по X, хай буде звичайний поворот
            handObject.transform.localRotation = Quaternion.Euler(0, 0, 15f);

            animationCoroutine = StartCoroutine(UIPressAnimationRoutine());
        }

        private IEnumerator UIPressAnimationRoutine()
        {
            Vector3 basePos = handObject.transform.localPosition;
            float duration = 1.25f;
            float elapsed = 0f;

            // Напрямок руху пальця при натисканні (плавно до центру кнопки)
            Vector3 pressOffset = new Vector3(-20f, 20f, 0f);

            while (true)
            {
                if (targetUI == null || handObject == null) yield break;

                elapsed += Time.deltaTime;
                float cycle = (elapsed % duration) / duration;

                float pressFactor = 0f;

                if (cycle < 0.65f)
                {
                    // Синусоїдальний підйом та спуск: від 0 плавно до 1 і назад до 0
                    float phase = cycle / 0.65f;
                    float rawSin = Mathf.Sin(phase * Mathf.PI);
                    // SmoothStep усуває будь-яку різкість на старті і фініші
                    pressFactor = Mathf.SmoothStep(0f, 1f, rawSin);
                }
                else
                {
                    // Природна пауза між тапами
                    pressFactor = 0f;
                }

                handObject.transform.localPosition = basePos + pressOffset * pressFactor;
                float scale = Mathf.Lerp(1.0f, 0.86f, pressFactor);
                handObject.transform.localScale = Vector3.one * scale;

                yield return null;
            }
        }

        private void Update()
        {
            if (targetPiece != null && !targetPiece.gameObject.activeInHierarchy)
            {
                Dismiss();
            }
            if (targetUI != null && !targetUI.gameObject.activeInHierarchy)
            {
                Dismiss();
            }
        }

        private void CreateHandIndicator(GameObject customHandPrefab)
        {
            Vector3 pieceCenter = targetPiece.transform.position;
            float pieceHeight = 0.5f;
            var renderers = targetPiece.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                pieceCenter = b.center;
                pieceHeight = b.size.y;
            }

            Vector3 startPos = pieceCenter + Vector3.up * (pieceHeight * 0.5f + 0.5f);

            if (customHandPrefab != null)
            {
                handObject = Instantiate(customHandPrefab, transform);
                var srs = handObject.GetComponentsInChildren<SpriteRenderer>(true);
                foreach (var s in srs) s.sortingOrder = 100;
            }
            else
            {
                // Створюємо базовий SpriteRenderer (Placeholder) якщо префаб не задано
                handObject = new GameObject("HandSprite");
                handObject.transform.SetParent(transform, false);
                
                SpriteRenderer sr = handObject.AddComponent<SpriteRenderer>();
                sr.sortingOrder = 100; // Поверх усього

                // Можна завантажити дефолтний спрайт з ресурсів, якщо він є
                Sprite handSprite = Resources.Load<Sprite>("HandIcon");
                if (handSprite != null)
                {
                    sr.sprite = handSprite;
                }
                else
                {
                    // Або створюємо 3D капсулу як тимчасовий палець
                    GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    capsule.transform.SetParent(handObject.transform, false);
                    capsule.transform.localScale = new Vector3(0.3f, 0.5f, 0.3f);
                    capsule.transform.localRotation = Quaternion.Euler(60f, 0f, 0f); // Нахил пальця
                    Destroy(capsule.GetComponent<Collider>());
                    
                    Material mat = new Material(Shader.Find("Standard"));
                    mat.color = Color.white;
                    capsule.GetComponent<Renderer>().material = mat;
                }
            }

            handObject.transform.position = startPos;
            
            // Повертаємо спрайт так, щоб він лежав на площині (дивився вгору на камеру) 
            // і завжди вказував пальцем "вгору" по екрану (вздовж осі Z), як справжня рука.
            handObject.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
        }

        private IEnumerator HandAnimationRoutine()
        {
            Vector3 handBasePos = handObject.transform.position;
            float duration = 1.2f;
            float elapsed = 0f;

            // Спеціальний плавний тап на місці (наприклад, для вибору блоку молотком)
            if (moveDelta == Vector2Int.zero)
            {
                // Розміщуємо руку так, щоб кінчик пальця вказував у центр блоку (зсув трохи назад по Z)
                Vector3 basePos = handBasePos + Vector3.back * (0.35f * cellSize);
                handObject.transform.position = basePos;

                while (true)
                {
                    if (targetPiece == null || handObject == null) yield break;

                    elapsed += Time.deltaTime;
                    float cycle = (elapsed % duration) / duration;
                    float pressFactor = 0f;

                    if (cycle < 0.65f)
                    {
                        float phase = cycle / 0.65f;
                        pressFactor = Mathf.SmoothStep(0f, 1f, Mathf.Sin(phase * Mathf.PI));
                    }

                    // Тап вперед по екрану та вниз до блоку для виразного руху з камери зверху
                    Vector3 tapOffset = (Vector3.forward * 0.18f + Vector3.down * 0.35f) * (cellSize * pressFactor);
                    handObject.transform.position = basePos + tapOffset;
                    handObject.transform.localScale = Vector3.one * Mathf.Lerp(1.0f, 0.82f, pressFactor);

                    yield return null;
                }
            }

            Vector3 moveDir = new Vector3(moveDelta.x, 0f, moveDelta.y).normalized;
            Vector3 startPos = handBasePos - moveDir * (0.25f * cellSize);
            Vector3 targetPos = startPos + moveDir * (0.8f * cellSize);

            while (true)
            {
                elapsed += Time.deltaTime;
                float t = elapsed % duration;

                if (targetPiece == null || handObject == null) yield break;

                float normalizedT = t / duration;
                
                if (normalizedT < 0.2f)
                {
                    // Опускається
                    float subT = normalizedT / 0.2f;
                    float smoothSubT = subT * subT * (3f - 2f * subT);
                    handObject.transform.position = startPos + Vector3.up * Mathf.Lerp(0.5f, 0f, smoothSubT);
                }
                else if (normalizedT < 0.7f)
                {
                    // Тягне
                    float subT = (normalizedT - 0.2f) / 0.5f;
                    float smoothT = subT * subT * (3f - 2f * subT);
                    handObject.transform.position = Vector3.Lerp(startPos, targetPos, smoothT);
                }
                else
                {
                    // Піднімається і повертається плавно назад
                    float subT = (normalizedT - 0.7f) / 0.3f;
                    Vector3 upPos = Vector3.Lerp(targetPos, startPos, subT);
                    upPos += Vector3.up * Mathf.Sin(subT * Mathf.PI) * 0.5f;
                    handObject.transform.position = upPos;
                }

                // Плавна пульсація масштабу при дотику
                float scale = 1.0f;
                if (normalizedT >= 0.15f && normalizedT <= 0.35f)
                {
                    float touchPhase = (normalizedT - 0.15f) / 0.2f;
                    float touchPulse = Mathf.Sin(touchPhase * Mathf.PI);
                    scale = Mathf.Lerp(1.0f, 0.85f, touchPulse);
                }
                handObject.transform.localScale = Vector3.one * scale;

                yield return null;
            }
        }

        private void CleanupAndDestroy()
        {
            if (animationCoroutine != null)
            {
                StopCoroutine(animationCoroutine);
                animationCoroutine = null;
            }
            Destroy(gameObject);
        }
    }
}
