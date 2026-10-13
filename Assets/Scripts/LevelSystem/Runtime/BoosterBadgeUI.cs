using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace LegoPuzzle.Runtime
{
    /// <summary>
    /// Допоміжний клас для створення та оновлення бейджів балансу бустерів (TimeFreeze, Hint, Hammer).
    /// Створює гарний, стильний круглий індикатор кількості бустерів у правому нижньому кутку кнопки.
    /// </summary>
    public static class BoosterBadgeUI
    {
        private static Sprite cachedCircleSprite;

        public static Sprite GetCircleSprite()
        {
            if (cachedCircleSprite != null) return cachedCircleSprite;

            var sprites = Resources.FindObjectsOfTypeAll<Sprite>();
            foreach (var s in sprites)
            {
                if (s != null && s.name == "Knob")
                {
                    cachedCircleSprite = s;
                    return s;
                }
            }

            // Процедурний якісний антиаліасинг-круг
            int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "BoosterBadgeCircle",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            float center = size * 0.5f;
            float radius = center - 1.5f;

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(center, center));
                    float alpha = Mathf.Clamp01(radius - dist + 0.5f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();

            cachedCircleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return cachedCircleSprite;
        }

        public static TMP_FontAsset GetDefaultTMPFont()
        {
            var fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            if (fonts != null && fonts.Length > 0)
            {
                foreach (var f in fonts)
                {
                    if (f != null && (f.name.Contains("rimouski") || f.name.Contains("GROBOLD") || f.name.Contains("Bold")))
                        return f;
                }
                return fonts[0];
            }
            return null;
        }

        public static TMP_Text EnsureBadge(Transform boosterTransform, TMP_Text currentTMP, string prefix = "")
        {
            if (currentTMP != null) return currentTMP;
            if (boosterTransform == null) return null;

            // 1. Шукаємо вже існуючий бейдж
            Transform existing = boosterTransform.Find("ChargesBadge");
            if (existing != null)
            {
                var tmp = existing.GetComponentInChildren<TMP_Text>();
                if (tmp != null) return tmp;
            }

            // 2. Створюємо контейнер ChargesBadge
            GameObject badgeObj = new GameObject("ChargesBadge");
            badgeObj.transform.SetParent(boosterTransform, false);

            RectTransform rt = badgeObj.AddComponent<RectTransform>();
            // Фіксуємо у правому нижньому кутку кнопки бустера
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(-28f, 28f);
            rt.sizeDelta = new Vector2(52f, 52f);

            // Круглий фон
            Image badgeImg = badgeObj.AddComponent<Image>();
            badgeImg.sprite = GetCircleSprite();
            badgeImg.type = Image.Type.Simple;
            badgeImg.color = new Color(0.10f, 0.14f, 0.22f, 0.96f); // Глибокий індиго/темний слейт
            badgeImg.raycastTarget = false;

            // Світла обводка круга
            Outline badgeOutline = badgeObj.AddComponent<Outline>();
            badgeOutline.effectColor = new Color(1f, 1f, 1f, 0.45f);
            badgeOutline.effectDistance = new Vector2(1.5f, -1.5f);

            // Текстовий об'єкт
            GameObject textObj = new GameObject("BadgeText");
            textObj.transform.SetParent(badgeObj.transform, false);

            RectTransform textRt = textObj.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            TextMeshProUGUI tmpText = textObj.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset font = GetDefaultTMPFont();
            if (font != null) tmpText.font = font;

            tmpText.fontSize = 26f;
            tmpText.fontSizeMin = 14f;
            tmpText.fontSizeMax = 28f;
            tmpText.enableAutoSizing = true;
            tmpText.fontStyle = FontStyles.Bold;
            tmpText.alignment = TextAlignmentOptions.Center;
            tmpText.color = Color.white;
            tmpText.raycastTarget = false;

            // Тінь для тексту
            Shadow textShadow = textObj.AddComponent<Shadow>();
            textShadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
            textShadow.effectDistance = new Vector2(1.5f, -1.5f);

            return tmpText;
        }

        public static void UpdateBadge(TMP_Text tmp, int availableCharges, string prefix = "")
        {
            if (tmp == null) return;

            Transform badgeTransform = tmp.transform.parent;
            if (badgeTransform != null)
            {
                badgeTransform.gameObject.SetActive(true);
            }

            Image badgeImg = (badgeTransform != null) ? badgeTransform.GetComponent<Image>() : null;

            if (availableCharges < 0)
            {
                // Нескінченна кількість
                tmp.text = "∞";
                tmp.color = Color.white;
                if (badgeImg != null) badgeImg.color = new Color(0.10f, 0.14f, 0.22f, 0.96f);
            }
            else if (availableCharges == 0)
            {
                // 0 бустерів — показуємо 0 з червонуватим акцентом або яскравий нуль
                tmp.text = "0";
                tmp.color = new Color(1f, 0.4f, 0.4f, 1f);
                if (badgeImg != null) badgeImg.color = new Color(0.24f, 0.08f, 0.10f, 0.96f);
            }
            else
            {
                // Є заряди: x1, x2, x3 або просто число
                if (string.IsNullOrEmpty(prefix))
                {
                    tmp.text = $"{availableCharges}";
                }
                else
                {
                    tmp.text = $"{prefix}{availableCharges}";
                }

                tmp.color = Color.white;
                if (badgeImg != null) badgeImg.color = new Color(0.10f, 0.14f, 0.22f, 0.96f);
            }
        }
    }
}
