using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LegoPuzzle.Runtime
{
    /// <summary>
    /// Spawns a festive, multi-colored UI confetti burst on victory.
    /// Dual cannon party popper physics: shoots colorful paper ribbons upwards from left and right,
    /// which flutter with 3D paper spin and drift gracefully downwards across the Win Panel.
    /// </summary>
    public class UIConfettiEffect : MonoBehaviour
    {
        [Header("Confetti Configuration")]
        [Tooltip("Total number of confetti pieces spawned")]
        [SerializeField] private int confettiCount = 55;

        [Tooltip("Delay in seconds after panel activation before firing confetti cannons")]
        [SerializeField] private float launchDelay = 0.35f;

        [Tooltip("Lifetime of confetti in seconds")]
        [SerializeField] private float confettiLifetime = 3.2f;

        [Tooltip("Gravity acceleration pulling confetti down")]
        [SerializeField] private float gravity = 900f;

        [Tooltip("Upward explosion force")]
        [SerializeField] private float burstForceMin = 650f;
        [SerializeField] private float burstForceMax = 1050f;

        [Tooltip("Horizontal spread velocity")]
        [SerializeField] private float horizontalSpread = 320f;

        // Vibrant celebratory LEGO puzzle color palette
        private static readonly Color[] ConfettiColors = new Color[]
        {
            new Color(1.00f, 0.82f, 0.00f, 1f), // Gold / LEGO Yellow
            new Color(0.98f, 0.22f, 0.22f, 1f), // Bright Red
            new Color(0.12f, 0.53f, 1.00f, 1f), // Electric Blue
            new Color(0.20f, 0.80f, 0.35f, 1f), // Emerald Green
            new Color(1.00f, 0.58f, 0.00f, 1f), // Vivid Orange
            new Color(1.00f, 0.22f, 0.48f, 1f), // Hot Pink / Magenta
            new Color(0.10f, 0.82f, 0.85f, 1f), // Sky Cyan
            new Color(0.68f, 0.35f, 0.95f, 1f)  // Violet
        };

        private RectTransform containerRect;
        private List<ConfettiPiece> activePieces = new List<ConfettiPiece>();
        private Coroutine burstCoroutine;

        private class ConfettiPiece
        {
            public GameObject obj;
            public RectTransform rt;
            public Image img;
            public Vector2 velocity;
            public float baseWidth;
            public float baseHeight;
            public float flutterSpeed;
            public float flutterPhase;
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

            Transform existing = transform.Find("__ConfettiContainer");
            if (existing != null)
            {
                containerRect = existing.GetComponent<RectTransform>();
            }
            else
            {
                GameObject cObj = new GameObject("__ConfettiContainer");
                cObj.transform.SetParent(transform, false);
                containerRect = cObj.AddComponent<RectTransform>();
                containerRect.anchorMin = new Vector2(0.5f, 0.5f);
                containerRect.anchorMax = new Vector2(0.5f, 0.5f);
                containerRect.pivot = new Vector2(0.5f, 0.5f);
                containerRect.anchoredPosition = Vector2.zero;
                containerRect.sizeDelta = new Vector2(1000f, 1500f);
                // Placed in front of panel contents
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
            if (burstCoroutine != null)
            {
                StopCoroutine(burstCoroutine);
                burstCoroutine = null;
            }
            ClearPieces();
        }

        /// <summary>
        /// Fires the confetti celebration.
        /// </summary>
        public void Play()
        {
            if (burstCoroutine != null)
            {
                StopCoroutine(burstCoroutine);
            }
            ClearPieces();

            if (gameObject.activeInHierarchy)
            {
                burstCoroutine = StartCoroutine(ConfettiCelebrationRoutine());
            }
        }

        private void ClearPieces()
        {
            for (int i = 0; i < activePieces.Count; i++)
            {
                if (activePieces[i] != null && activePieces[i].obj != null)
                {
                    Destroy(activePieces[i].obj);
                }
            }
            activePieces.Clear();
        }

        private IEnumerator ConfettiCelebrationRoutine()
        {
            if (launchDelay > 0f)
            {
                yield return new WaitForSecondsRealtime(launchDelay);
            }

            EnsureContainer();
            if (containerRect == null) yield break;

            // Spawn confetti pieces from two side cannons (left and right party poppers)
            float panelHalfW = 280f;
            float cannonY = -250f;

            for (int i = 0; i < confettiCount; i++)
            {
                // Alternate between left cannon (shoots up-right) and right cannon (shoots up-left)
                bool isLeft = (i % 2 == 0);
                float originX = isLeft ? -panelHalfW : panelHalfW;
                Vector2 origin = new Vector2(originX + Random.Range(-40f, 40f), cannonY + Random.Range(-30f, 30f));

                // Upward velocity
                float speedY = Random.Range(burstForceMin, burstForceMax);
                // Inward angle with spread
                float speedX;
                if (isLeft)
                {
                    speedX = Random.Range(80f, horizontalSpread + 100f);
                }
                else
                {
                    speedX = Random.Range(-horizontalSpread - 100f, -80f);
                }

                // Add small center fountain for center fullness
                if (i % 5 == 0)
                {
                    origin = new Vector2(Random.Range(-80f, 80f), cannonY + 60f);
                    speedX = Random.Range(-180f, 180f);
                    speedY = Random.Range(burstForceMin * 0.9f, burstForceMax * 1.05f);
                }

                ConfettiPiece piece = CreatePiece(origin, new Vector2(speedX, speedY));
                activePieces.Add(piece);
            }

            // Animate pieces physics & fluttering
            while (activePieces.Count > 0)
            {
                float dt = Time.unscaledDeltaTime;

                for (int i = activePieces.Count - 1; i >= 0; i--)
                {
                    ConfettiPiece p = activePieces[i];
                    if (p == null || p.obj == null)
                    {
                        activePieces.RemoveAt(i);
                        continue;
                    }

                    p.lifetime += dt;
                    if (p.lifetime >= p.maxLifetime)
                    {
                        Destroy(p.obj);
                        activePieces.RemoveAt(i);
                        continue;
                    }

                    // Physics: gravity & air drag
                    p.velocity.y -= gravity * dt;
                    p.velocity.x *= Mathf.Clamp01(1f - 0.75f * dt);

                    // Fluttering paper simulation (3D flip on X axis)
                    p.flutterPhase += dt * p.flutterSpeed;
                    float flutterCos = Mathf.Cos(p.flutterPhase);
                    p.rt.localScale = new Vector3(flutterCos, 1f, 1f);

                    // Continuous Z rotation
                    p.rt.Rotate(0f, 0f, p.rotationSpeed * dt);

                    // Move position
                    p.rt.anchoredPosition += p.velocity * dt;

                    // Fade out in last 25% of life
                    float fadeProgress = p.lifetime / p.maxLifetime;
                    if (fadeProgress > 0.72f)
                    {
                        float alpha = Mathf.Clamp01((1f - fadeProgress) / 0.28f);
                        Color c = p.baseColor;
                        c.a = alpha;
                        p.img.color = c;
                    }
                }

                yield return null;
            }

            burstCoroutine = null;
        }

        private ConfettiPiece CreatePiece(Vector2 startPos, Vector2 velocity)
        {
            GameObject pObj = new GameObject("Confetti");
            pObj.transform.SetParent(containerRect, false);

            RectTransform rt = pObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = startPos;

            // Random aspect ratio: small paper rectangles
            float w = Random.Range(14f, 22f);
            float h = Random.Range(8f, 14f);
            rt.sizeDelta = new Vector2(w, h);
            rt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

            Color pieceColor = ConfettiColors[Random.Range(0, ConfettiColors.Length)];

            Image img = pObj.AddComponent<Image>();
            img.color = pieceColor;
            img.raycastTarget = false;

            ConfettiPiece piece = new ConfettiPiece
            {
                obj = pObj,
                rt = rt,
                img = img,
                velocity = velocity,
                baseWidth = w,
                baseHeight = h,
                flutterSpeed = Random.Range(6f, 14f),
                flutterPhase = Random.Range(0f, Mathf.PI * 2f),
                rotationSpeed = Random.Range(-240f, 240f),
                lifetime = 0f,
                maxLifetime = confettiLifetime + Random.Range(-0.4f, 0.4f),
                baseColor = pieceColor
            };

            return piece;
        }

        /// <summary>
        /// Attaches or gets UIConfettiEffect on the target panel GameObject.
        /// </summary>
        public static UIConfettiEffect AttachTo(GameObject panel)
        {
            if (panel == null) return null;
            var confetti = panel.GetComponent<UIConfettiEffect>();
            if (confetti == null)
            {
                confetti = panel.AddComponent<UIConfettiEffect>();
            }
            return confetti;
        }
    }
}
