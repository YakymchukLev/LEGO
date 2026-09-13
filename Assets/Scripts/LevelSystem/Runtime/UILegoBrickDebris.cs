using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LegoPuzzle.Runtime
{
    /// <summary>
    /// Spawns tumbling 2D LEGO bricks ("anti-confetti") that crumble and fall
    /// downwards when the player fails the level, adding visual charm and personality.
    /// </summary>
    public class UILegoBrickDebris : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Number of falling LEGO bricks to spawn")]
        [SerializeField] private int brickCount = 5;

        [Tooltip("Delay in seconds before releasing falling bricks (after panel lands)")]
        [SerializeField] private float launchDelay = 0.35f;

        [Tooltip("Gravity acceleration pulling bricks down")]
        [SerializeField] private float gravity = 1400f;

        [Tooltip("Lifetime of falling bricks in seconds")]
        [SerializeField] private float brickLifetime = 2.4f;

        // Classic LEGO brick colors
        private static readonly Color[] LegoColors = new Color[]
        {
            new Color(0.90f, 0.22f, 0.21f, 1f), // Classic Red
            new Color(0.12f, 0.53f, 0.90f, 1f), // Royal Blue
            new Color(0.99f, 0.85f, 0.21f, 1f), // LEGO Yellow
            new Color(0.26f, 0.63f, 0.28f, 1f), // Kelly Green
            new Color(0.98f, 0.55f, 0.00f, 1f)  // Orange
        };

        private RectTransform containerRect;
        private List<BrickDebris> activeBricks = new List<BrickDebris>();
        private Coroutine debrisCoroutine;

        private class BrickDebris
        {
            public GameObject obj;
            public RectTransform rt;
            public Image mainImg;
            public Image[] studImgs;
            public Vector2 velocity;
            public float rotationSpeed;
            public float lifetime;
            public float maxLifetime;
            public Color baseColor;
        }

        private void Awake()
        {
            EnsureContainer();
        }

        private void EnsureContainer()
        {
            if (containerRect != null) return;

            Transform existing = transform.Find("__LegoDebrisContainer");
            if (existing != null)
            {
                containerRect = existing.GetComponent<RectTransform>();
            }
            else
            {
                GameObject cObj = new GameObject("__LegoDebrisContainer");
                cObj.transform.SetParent(transform, false);
                containerRect = cObj.AddComponent<RectTransform>();
                containerRect.anchorMin = new Vector2(0.5f, 0.5f);
                containerRect.anchorMax = new Vector2(0.5f, 0.5f);
                containerRect.pivot = new Vector2(0.5f, 0.5f);
                containerRect.anchoredPosition = Vector2.zero;
                containerRect.sizeDelta = new Vector2(900f, 1400f);
                // In front of panel elements
                containerRect.SetAsLastSibling();
            }
        }

        private void OnEnable()
        {
            EnsureContainer();
            Play();
        }

        private void OnDisable()
        {
            if (debrisCoroutine != null)
            {
                StopCoroutine(debrisCoroutine);
                debrisCoroutine = null;
            }
            ClearBricks();
        }

        public void Play()
        {
            if (debrisCoroutine != null)
            {
                StopCoroutine(debrisCoroutine);
            }
            ClearBricks();

            if (gameObject.activeInHierarchy)
            {
                debrisCoroutine = StartCoroutine(ReleaseDebrisRoutine());
            }
        }

        private void ClearBricks()
        {
            for (int i = 0; i < activeBricks.Count; i++)
            {
                if (activeBricks[i] != null && activeBricks[i].obj != null)
                {
                    Destroy(activeBricks[i].obj);
                }
            }
            activeBricks.Clear();
        }

        private IEnumerator ReleaseDebrisRoutine()
        {
            if (launchDelay > 0f)
            {
                yield return new WaitForSecondsRealtime(launchDelay);
            }

            EnsureContainer();
            if (containerRect == null) yield break;

            // Spawn 5 crumbling LEGO bricks around the header area
            float startY = 320f;
            float[] xOffsets = new float[] { -150f, -70f, 0f, 80f, 160f };

            for (int i = 0; i < brickCount; i++)
            {
                float posX = (i < xOffsets.Length) ? xOffsets[i] : Random.Range(-180f, 180f);
                Vector2 origin = new Vector2(posX + Random.Range(-20f, 20f), startY + Random.Range(-25f, 25f));

                // Small initial upward pop & outward scattering
                float vx = Random.Range(-180f, 180f);
                float vy = Random.Range(160f, 320f);

                Color brickColor = LegoColors[i % LegoColors.Length];
                BrickDebris brick = CreateLegoBrick(origin, new Vector2(vx, vy), brickColor);
                activeBricks.Add(brick);
            }

            // Animate tumbling physics
            while (activeBricks.Count > 0)
            {
                float dt = Time.unscaledDeltaTime;

                for (int i = activeBricks.Count - 1; i >= 0; i--)
                {
                    BrickDebris b = activeBricks[i];
                    if (b == null || b.obj == null)
                    {
                        activeBricks.RemoveAt(i);
                        continue;
                    }

                    b.lifetime += dt;
                    if (b.lifetime >= b.maxLifetime)
                    {
                        Destroy(b.obj);
                        activeBricks.RemoveAt(i);
                        continue;
                    }

                    // Gravity & velocity
                    b.velocity.y -= gravity * dt;
                    b.velocity.x *= Mathf.Clamp01(1f - 0.5f * dt);

                    // Position & tumbling rotation
                    b.rt.anchoredPosition += b.velocity * dt;
                    b.rt.Rotate(0f, 0f, b.rotationSpeed * dt);

                    // Fade out in last 20%
                    float progress = b.lifetime / b.maxLifetime;
                    if (progress > 0.80f)
                    {
                        float alpha = Mathf.Clamp01((1f - progress) / 0.20f);
                        if (b.mainImg != null)
                        {
                            Color mc = b.baseColor;
                            mc.a = alpha;
                            b.mainImg.color = mc;
                        }
                        if (b.studImgs != null)
                        {
                            for (int s = 0; s < b.studImgs.Length; s++)
                            {
                                if (b.studImgs[s] != null)
                                {
                                    Color sc = b.studImgs[s].color;
                                    sc.a = alpha * 0.85f;
                                    b.studImgs[s].color = sc;
                                }
                            }
                        }
                    }
                }

                yield return null;
            }

            debrisCoroutine = null;
        }

        private BrickDebris CreateLegoBrick(Vector2 origin, Vector2 velocity, Color color)
        {
            GameObject brickRoot = new GameObject("LegoBrick");
            brickRoot.transform.SetParent(containerRect, false);

            RectTransform rt = brickRoot.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = origin;
            rt.sizeDelta = new Vector2(50f, 28f);
            rt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-35f, 35f));

            // Main body
            Image bodyImg = brickRoot.AddComponent<Image>();
            bodyImg.color = color;
            bodyImg.raycastTarget = false;
            bodyImg.sprite = GetBrickSprite();
            bodyImg.type = Image.Type.Sliced;

            // 2 LEGO studs on top
            List<Image> studs = new List<Image>();
            float[] studX = new float[] { -13f, 13f };

            Color studColor = Color.Lerp(color, Color.white, 0.25f); // slightly brighter for 3D bevel look
            Sprite circleSprite = GetCircleSprite();

            for (int s = 0; s < studX.Length; s++)
            {
                GameObject studObj = new GameObject($"Stud_{s}");
                studObj.transform.SetParent(brickRoot.transform, false);

                RectTransform sRt = studObj.AddComponent<RectTransform>();
                sRt.anchorMin = new Vector2(0.5f, 1f);
                sRt.anchorMax = new Vector2(0.5f, 1f);
                sRt.pivot = new Vector2(0.5f, 0.5f);
                sRt.anchoredPosition = new Vector2(studX[s], 3f);
                sRt.sizeDelta = new Vector2(14f, 8f);

                Image sImg = studObj.AddComponent<Image>();
                sImg.color = studColor;
                sImg.raycastTarget = false;
                sImg.sprite = circleSprite;
                sImg.type = Image.Type.Simple;
                studs.Add(sImg);
            }

            return new BrickDebris
            {
                obj = brickRoot,
                rt = rt,
                mainImg = bodyImg,
                studImgs = studs.ToArray(),
                velocity = velocity,
                rotationSpeed = Random.Range(-220f, 220f),
                lifetime = 0f,
                maxLifetime = brickLifetime + Random.Range(-0.2f, 0.3f),
                baseColor = color
            };
        }

        private static Sprite cachedBrickSprite;
        private static Sprite cachedCircleSprite;

        private static Sprite GetBrickSprite()
        {
            if (cachedBrickSprite != null) return cachedBrickSprite;

            int size = 32;
            int cornerRadius = 6;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color32[] cols = new Color32[size * size];
            Color32 white = new Color32(255, 255, 255, 255);
            Color32 clear = new Color32(255, 255, 255, 0);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int cx = x < cornerRadius ? cornerRadius - x : (x >= size - cornerRadius ? x - (size - cornerRadius - 1) : 0);
                    int cy = y < cornerRadius ? cornerRadius - y : (y >= size - cornerRadius ? y - (size - cornerRadius - 1) : 0);
                    if (cx * cx + cy * cy <= cornerRadius * cornerRadius)
                    {
                        cols[y * size + x] = white;
                    }
                    else
                    {
                        cols[y * size + x] = clear;
                    }
                }
            }
            tex.SetPixels32(cols);
            tex.Apply();

            cachedBrickSprite = Sprite.Create(
                tex,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(cornerRadius, cornerRadius, cornerRadius, cornerRadius)
            );
            return cachedBrickSprite;
        }

        private static Sprite GetCircleSprite()
        {
            if (cachedCircleSprite != null) return cachedCircleSprite;

            int size = 32;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color32[] cols = new Color32[size * size];
            float r = (size - 1) * 0.5f;
            Color32 white = new Color32(255, 255, 255, 255);
            Color32 clear = new Color32(255, 255, 255, 0);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - r;
                    float dy = y - r;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist <= r)
                    {
                        cols[y * size + x] = white;
                    }
                    else
                    {
                        cols[y * size + x] = clear;
                    }
                }
            }
            tex.SetPixels32(cols);
            tex.Apply();

            cachedCircleSprite = Sprite.Create(
                tex,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f)
            );
            return cachedCircleSprite;
        }

        public static UILegoBrickDebris AttachTo(GameObject panel)
        {
            if (panel == null) return null;
            var debris = panel.GetComponent<UILegoBrickDebris>();
            if (debris == null)
            {
                debris = panel.AddComponent<UILegoBrickDebris>();
            }
            return debris;
        }
    }
}
