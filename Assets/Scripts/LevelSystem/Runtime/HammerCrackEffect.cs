using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LegoPuzzle.Runtime
{
    /// <summary>
    /// Процедурний візуальний ефект розгалужених тріщин на підлозі, що розходяться
    /// від епіцентру удару молотка. Має яскравий спалах при контакті, стрімке розростання
    /// гілок тріщин, перехід у темний колір розлому та плавне затухання.
    /// </summary>
    public class HammerCrackEffect : MonoBehaviour
    {
        [Header("Параметри Тріщин")]
        [Tooltip("Кількість головних гілок тріщин, що виходять з центру")]
        [SerializeField] private int branchCount = 7;

        [Tooltip("Кількість зламів у кожній гілці")]
        [SerializeField] private int segmentsPerBranch = 6;

        [Tooltip("Максимальний радіус розповзання тріщин")]
        [SerializeField] private float crackRadius = 1.8f;

        [Tooltip("Ширина ліній тріщин біля центру")]
        [SerializeField] private float startLineWidth = 0.09f;

        [Tooltip("Ширина кінчиків тріщин")]
        [SerializeField] private float endLineWidth = 0.02f;

        [Tooltip("Час розростання тріщин від центру (у секундах)")]
        [SerializeField] private float expansionDuration = 0.12f;

        [Tooltip("Загальний час існування ефекту до повного зникнення")]
        [SerializeField] private float totalLifetime = 1.4f;

        [Header("Кольори")]
        [SerializeField] private Color impactFlashColor = new Color(1f, 0.95f, 0.7f, 1f);
        [SerializeField] private Color crackBaseColor = new Color(0.12f, 0.11f, 0.10f, 0.95f);

        private static Material crackMaterial;

        private class CrackBranch
        {
            public LineRenderer line;
            public Vector3[] finalPoints;
            public Vector3[] currentPoints;
        }

        private List<CrackBranch> branches = new List<CrackBranch>();
        private LineRenderer shockwaveRing;

        /// <summary>
        /// Створює та запускає ефект тріщин у заданій точці простору.
        /// </summary>
        public static HammerCrackEffect SpawnAt(Vector3 worldPosition, float radius = 1.8f, Transform parent = null)
        {
            GameObject effectObj = new GameObject("HammerCrackEffect");
            if (parent != null)
            {
                effectObj.transform.SetParent(parent);
            }
            effectObj.transform.position = worldPosition;

            HammerCrackEffect effect = effectObj.AddComponent<HammerCrackEffect>();
            effect.crackRadius = radius;
            effect.InitializeAndPlay();
            return effect;
        }

        private static Material GetOrCreateCrackMaterial()
        {
            if (crackMaterial != null) return crackMaterial;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");

            crackMaterial = new Material(shader != null ? shader : Shader.Find("Diffuse"));
            crackMaterial.name = "M_HammerCrack_Runtime";
            return crackMaterial;
        }

        public void InitializeAndPlay()
        {
            Material mat = GetOrCreateCrackMaterial();

            // 1. Створюємо розгалужені лінії тріщин
            float baseAngleStep = 360f / Mathf.Max(1, branchCount);

            for (int i = 0; i < branchCount; i++)
            {
                float angleDeg = i * baseAngleStep + Random.Range(-18f, 18f);
                float angleRad = angleDeg * Mathf.Deg2Rad;

                float branchLen = crackRadius * Random.Range(0.65f, 1.05f);
                Vector3 branchDir = new Vector3(Mathf.Cos(angleRad), 0f, Mathf.Sin(angleRad)).normalized;

                // Генеруємо ламані точки для гілки
                Vector3[] points = new Vector3[segmentsPerBranch + 1];
                points[0] = Vector3.zero;

                Vector3 currentPos = Vector3.zero;
                for (int s = 1; s <= segmentsPerBranch; s++)
                {
                    float t = (float)s / segmentsPerBranch;
                    Vector3 idealPos = branchDir * (branchLen * t);

                    // Бічне відхилення для створення зубчастості (zigzag)
                    Vector3 lateral = new Vector3(-branchDir.z, 0f, branchDir.x) * Random.Range(-0.12f, 0.12f) * branchLen;
                    Vector3 jitter = branchDir * Random.Range(-0.05f, 0.05f) * branchLen;

                    currentPos = idealPos + lateral + jitter;
                    currentPos.y = 0f; // строго в площині
                    points[s] = currentPos;
                }

                // Створюємо LineRenderer для цієї гілки
                GameObject branchObj = new GameObject($"Branch_{i}");
                branchObj.transform.SetParent(transform, false);
                branchObj.transform.localPosition = Vector3.zero;

                LineRenderer lr = branchObj.AddComponent<LineRenderer>();
                lr.material = mat;
                lr.useWorldSpace = false;
                lr.startWidth = startLineWidth;
                lr.endWidth = endLineWidth;
                lr.positionCount = points.Length;
                lr.numCapVertices = 2;
                lr.numCornerVertices = 2;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;

                Vector3[] initialPoints = new Vector3[points.Length];
                for (int p = 0; p < initialPoints.Length; p++)
                {
                    initialPoints[p] = Vector3.zero;
                }
                lr.SetPositions(initialPoints);

                branches.Add(new CrackBranch
                {
                    line = lr,
                    finalPoints = points,
                    currentPoints = initialPoints
                });
            }

            // 2. Створюємо ударне кільце (Shockwave Ring)
            CreateShockwaveRing(mat);

            // 3. Запускаємо анімацію
            StartCoroutine(AnimateCracksRoutine());
        }

        private void CreateShockwaveRing(Material mat)
        {
            GameObject ringObj = new GameObject("ShockwaveRing");
            ringObj.transform.SetParent(transform, false);
            ringObj.transform.localPosition = Vector3.zero;

            shockwaveRing = ringObj.AddComponent<LineRenderer>();
            shockwaveRing.material = mat;
            shockwaveRing.useWorldSpace = false;
            shockwaveRing.loop = true;
            shockwaveRing.startWidth = 0.07f;
            shockwaveRing.endWidth = 0.07f;
            shockwaveRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            shockwaveRing.receiveShadows = false;

            int segments = 24;
            shockwaveRing.positionCount = segments;
            Vector3[] ringPoints = new Vector3[segments];
            for (int i = 0; i < segments; i++)
            {
                float rad = (i / (float)segments) * Mathf.PI * 2f;
                ringPoints[i] = new Vector3(Mathf.Cos(rad) * 0.1f, 0f, Mathf.Sin(rad) * 0.1f);
            }
            shockwaveRing.SetPositions(ringPoints);
        }

        private IEnumerator AnimateCracksRoutine()
        {
            float elapsed = 0f;

            // ФАЗА 1: Стрімке розповзання від центру + спалах світла
            while (elapsed < expansionDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / expansionDuration);
                // Енергійне швидке розкриття (Ease Out Quad)
                float t = 1f - (1f - progress) * (1f - progress);

                // Оновлюємо положення вершин ліній тріщин
                foreach (var b in branches)
                {
                    for (int i = 1; i < b.finalPoints.Length; i++)
                    {
                        b.currentPoints[i] = Vector3.LerpUnclamped(Vector3.zero, b.finalPoints[i], t);
                    }
                    b.line.SetPositions(b.currentPoints);

                    // Колір спалаху переходить у базовий колір розлому
                    Color branchColor = Color.Lerp(impactFlashColor, crackBaseColor, progress * 0.7f);
                    b.line.startColor = branchColor;
                    b.line.endColor = branchColor;
                }

                // Анімація ударної хвилі
                if (shockwaveRing != null)
                {
                    float ringRadius = Mathf.Lerp(0.1f, crackRadius * 0.85f, t);
                    int count = shockwaveRing.positionCount;
                    Vector3[] ringPts = new Vector3[count];
                    for (int i = 0; i < count; i++)
                    {
                        float rad = (i / (float)count) * Mathf.PI * 2f;
                        ringPts[i] = new Vector3(Mathf.Cos(rad) * ringRadius, 0f, Mathf.Sin(rad) * ringRadius);
                    }
                    shockwaveRing.SetPositions(ringPts);

                    Color ringColor = impactFlashColor;
                    ringColor.a = 1f - progress;
                    shockwaveRing.startColor = ringColor;
                    shockwaveRing.endColor = ringColor;
                }

                yield return null;
            }

            // Завершуємо остаточне положення ліній
            foreach (var b in branches)
            {
                b.line.SetPositions(b.finalPoints);
            }

            if (shockwaveRing != null)
            {
                shockwaveRing.enabled = false;
            }

            // ФАЗА 2: Утримання тріщин та плавне затухання
            float holdTime = 0.45f;
            yield return new WaitForSeconds(holdTime);

            float fadeDuration = Mathf.Max(0.1f, totalLifetime - expansionDuration - holdTime);
            float fadeElapsed = 0f;

            while (fadeElapsed < fadeDuration)
            {
                fadeElapsed += Time.deltaTime;
                float fadeT = Mathf.Clamp01(fadeElapsed / fadeDuration);
                float alpha = (1f - fadeT);

                Color c = crackBaseColor;
                c.a = alpha * crackBaseColor.a;

                foreach (var b in branches)
                {
                    if (b.line != null)
                    {
                        b.line.startColor = c;
                        b.line.endColor = c;
                        b.line.startWidth = startLineWidth * (1f - fadeT * 0.3f);
                    }
                }

                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
