#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using LegoPuzzle.Data;

namespace LegoPuzzle.Editor
{
    /// <summary>
    /// Утиліта для високоякісного, контрастного та яскравого малювання сітки рівнів
    /// у вікнах LevelGeneratorWindow та LevelEditorWindow.
    /// Виправляє тьмяний/темний вигляд стандартних GUI-елементів та забезпечує
    /// чітку видимість усіх елементів поля, воріт і деталей LEGO.
    /// </summary>
    public static class LevelGridRenderer
    {
        public const float DEFAULT_CELL_SIZE = 38f;
        public const float MIN_CELL_SIZE = 26f;
        public const float MAX_CELL_SIZE = 54f;

        // Палітра базових кольорів поля
        public static readonly Color WalkableBaseColor = new Color(0.92f, 0.90f, 0.84f, 1f);
        public static readonly Color WalkablePinColor = new Color(0.76f, 0.74f, 0.68f, 1f);
        public static readonly Color ObstacleBaseColor = new Color(0.28f, 0.26f, 0.25f, 1f);
        public static readonly Color ObstacleBevelColor = new Color(0.38f, 0.35f, 0.33f, 1f);
        public static readonly Color GridBorderColor = new Color(0.18f, 0.18f, 0.20f, 1f);
        public static readonly Color EmptyCellColor = new Color(0.14f, 0.14f, 0.16f, 0.7f);

        // Кешовані GUI-стилі для запобігання зайвим алокаціям
        private static GUIStyle labelStyle;

        /// <summary>
        /// Повертає висококонтрастний колір тексту (білий або темно-сірий)
        /// залежно від яскравості фону (luminance).
        /// </summary>
        public static Color GetContrastingTextColor(Color bg)
        {
            float luminance = 0.299f * bg.r + 0.587f * bg.g + 0.114f * bg.b;
            return luminance > 0.52f ? new Color(0.12f, 0.12f, 0.14f, 0.98f) : Color.white;
        }

        private static GUIStyle GetStyle(Color textColor, int fontSize)
        {
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };
            }
            labelStyle.fontSize = fontSize;
            labelStyle.normal.textColor = textColor;
            return labelStyle;
        }

        /// <summary>
        /// Малює одну клітинку поля з повною передачею кольору, 3D-фасками,
        /// пінами LEGO та контрастними іконками.
        /// </summary>
        public static void DrawCell(Rect rect, CellData cell, LegoPieceData piece, int x, int y, float size)
        {
            // Зовнішня рамка клітинки
            EditorGUI.DrawRect(rect, GridBorderColor);

            Rect innerRect = new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f);

            if (cell.cellType == CellType.Empty)
            {
                EditorGUI.DrawRect(innerRect, EmptyCellColor);
                return;
            }

            if (piece != null)
            {
                DrawPieceCell(rect, innerRect, piece, x, y, size);
            }
            else
            {
                DrawBoardCell(rect, innerRect, cell, x, y, size);
            }
        }

        private static void DrawPieceCell(Rect outerRect, Rect innerRect, LegoPieceData piece, int x, int y, float size)
        {
            Color pieceColor = piece.GetColor();

            // Чітка темніша рамка навколо деталі LEGO
            Color pieceBorder = Color.Lerp(pieceColor, Color.black, 0.38f);
            pieceBorder.a = 1f;
            EditorGUI.DrawRect(outerRect, pieceBorder);
            EditorGUI.DrawRect(innerRect, pieceColor);

            // 3D Фаска LEGO (світла зверху/зліва, темна знизу/справа)
            Color highlight = Color.Lerp(pieceColor, Color.white, 0.40f);
            Color shadow = Color.Lerp(pieceColor, Color.black, 0.35f);
            EditorGUI.DrawRect(new Rect(innerRect.x, innerRect.y, innerRect.width, 2f), highlight);
            EditorGUI.DrawRect(new Rect(innerRect.x, innerRect.y, 2f, innerRect.height), highlight);
            EditorGUI.DrawRect(new Rect(innerRect.x, innerRect.yMax - 2f, innerRect.width, 2f), shadow);
            EditorGUI.DrawRect(new Rect(innerRect.xMax - 2f, innerRect.y, 2f, innerRect.height), shadow);

            // Якщо деталь заморожена: малюємо стильну крижану окантовку навколо клітинки
            if (piece.freezeCount > 0)
            {
                Color iceCyan = new Color(0.35f, 0.85f, 1f, 0.95f);
                EditorGUI.DrawRect(new Rect(innerRect.x, innerRect.y, innerRect.width, 3f), iceCyan);
                EditorGUI.DrawRect(new Rect(innerRect.x, innerRect.y, 3f, innerRect.height), iceCyan);
                EditorGUI.DrawRect(new Rect(innerRect.x, innerRect.yMax - 3f, innerRect.width, 3f), iceCyan);
                EditorGUI.DrawRect(new Rect(innerRect.xMax - 3f, innerRect.y, 3f, innerRect.height), iceCyan);
            }

            // Центральний шип (Stud) LEGO
            float studSize = Mathf.Round(size * 0.46f);
            Rect studRect = new Rect(
                Mathf.Round(innerRect.center.x - studSize * 0.5f),
                Mathf.Round(innerRect.center.y - studSize * 0.5f),
                studSize,
                studSize
            );

            Color studBorder = piece.freezeCount > 0 ? new Color(0.1f, 0.4f, 0.7f, 1f) : Color.Lerp(pieceColor, Color.black, 0.25f);
            Color studBg = piece.freezeCount > 0 ? new Color(0.2f, 0.65f, 0.95f, 1f) : Color.Lerp(pieceColor, Color.white, 0.20f);
            EditorGUI.DrawRect(studRect, studBorder);
            EditorGUI.DrawRect(new Rect(studRect.x + 1f, studRect.y + 1f, studRect.width - 2f, studRect.height - 2f), studBg);

            // Іконка обмеження руху або лічильник заморозки або пін
            string glyph;
            if (piece.freezeCount > 0)
            {
                glyph = $"❄{piece.freezeCount}";
            }
            else
            {
                glyph = piece.moveRestriction switch
                {
                    MoveRestriction.HorizontalOnly => "↔",
                    MoveRestriction.VerticalOnly => "↕",
                    MoveRestriction.Locked => "🔒",
                    _ => "●"
                };
            }

            Color textCol = piece.freezeCount > 0 ? Color.white : GetContrastingTextColor(studBg);
            int fontSize = piece.freezeCount > 0
                ? Mathf.RoundToInt(size * 0.30f)
                : (piece.moveRestriction == MoveRestriction.Free ? Mathf.RoundToInt(size * 0.36f) : Mathf.RoundToInt(size * 0.42f));
            GUIStyle style = GetStyle(textCol, fontSize);

            string freezeInfo = piece.freezeCount > 0 ? $"\n❄ Лічильник заморозки: {piece.freezeCount}" : "";
            string flipInfo = piece.flipHorizontal ? "\nДзеркало X: так" : "";
            string tooltip = $"🧩 Деталь: {piece.pieceId}\nКолір: {piece.colorType}\nРух: {piece.moveRestriction}{freezeInfo}{flipInfo}\nКлітинка: ({x}, {y})";
            GUI.Label(innerRect, new GUIContent(glyph, tooltip), style);
        }

        private static void DrawBoardCell(Rect outerRect, Rect innerRect, CellData cell, int x, int y, float size)
        {
            switch (cell.cellType)
            {
                case CellType.Walkable:
                {
                    EditorGUI.DrawRect(innerRect, WalkableBaseColor);

                    // Центральний пін для вільних клітинок поля
                    float pinSize = Mathf.Max(3f, Mathf.Round(size * 0.14f));
                    Rect pinRect = new Rect(
                        Mathf.Round(innerRect.center.x - pinSize * 0.5f),
                        Mathf.Round(innerRect.center.y - pinSize * 0.5f),
                        pinSize,
                        pinSize
                    );
                    EditorGUI.DrawRect(pinRect, WalkablePinColor);

                    string tooltip = $"Вільне поле ({x}, {y})";
                    GUI.Label(innerRect, new GUIContent("", tooltip));
                    break;
                }

                case CellType.Obstacle:
                {
                    EditorGUI.DrawRect(innerRect, ObstacleBaseColor);

                    // Фаска перешкоди
                    EditorGUI.DrawRect(new Rect(innerRect.x + 2f, innerRect.y + 2f, innerRect.width - 4f, innerRect.height - 4f), ObstacleBevelColor);
                    EditorGUI.DrawRect(new Rect(innerRect.x + 4f, innerRect.y + 4f, innerRect.width - 8f, innerRect.height - 8f), ObstacleBaseColor);

                    GUIStyle style = GetStyle(new Color(0.92f, 0.92f, 0.92f), Mathf.RoundToInt(size * 0.38f));
                    string tooltip = $"🧱 Перешкода ({x}, {y})";
                    GUI.Label(innerRect, new GUIContent("■", tooltip), style);
                    break;
                }

                case CellType.HalfObstacle:
                {
                    // Фон — вільне поле
                    EditorGUI.DrawRect(innerRect, WalkableBaseColor);

                    // Блокована половина
                    Rect blockedRect = cell.exitDirection switch
                    {
                        ExitDirection.Up => new Rect(innerRect.x, innerRect.y, innerRect.width, innerRect.height * 0.5f),
                        ExitDirection.Down => new Rect(innerRect.x, innerRect.y + innerRect.height * 0.5f, innerRect.width, innerRect.height * 0.5f),
                        ExitDirection.Left => new Rect(innerRect.x, innerRect.y, innerRect.width * 0.5f, innerRect.height),
                        ExitDirection.Right => new Rect(innerRect.x + innerRect.width * 0.5f, innerRect.y, innerRect.width * 0.5f, innerRect.height),
                        _ => innerRect
                    };
                    EditorGUI.DrawRect(blockedRect, ObstacleBaseColor);

                    string tooltip = $"🧱 Половинна перешкода: {cell.exitDirection} ({x}, {y})";
                    GUI.Label(innerRect, new GUIContent("", tooltip));
                    break;
                }

                case CellType.QuarterObstacle:
                {
                    // Фон — вільне поле
                    EditorGUI.DrawRect(innerRect, WalkableBaseColor);

                    // Блокований куток: Left=TL, Up=TR, Down=BL, Right=BR
                    Rect cornerRect = cell.exitDirection switch
                    {
                        ExitDirection.Left => new Rect(innerRect.x, innerRect.y, innerRect.width * 0.5f, innerRect.height * 0.5f),
                        ExitDirection.Up => new Rect(innerRect.x + innerRect.width * 0.5f, innerRect.y, innerRect.width * 0.5f, innerRect.height * 0.5f),
                        ExitDirection.Down => new Rect(innerRect.x, innerRect.y + innerRect.height * 0.5f, innerRect.width * 0.5f, innerRect.height * 0.5f),
                        ExitDirection.Right => new Rect(innerRect.x + innerRect.width * 0.5f, innerRect.y + innerRect.height * 0.5f, innerRect.width * 0.5f, innerRect.height * 0.5f),
                        _ => new Rect(innerRect.x, innerRect.y, innerRect.width * 0.5f, innerRect.height * 0.5f)
                    };
                    EditorGUI.DrawRect(cornerRect, ObstacleBaseColor);

                    string tooltip = $"🧱 Чвертна перешкода: {cell.exitDirection} ({x}, {y})";
                    GUI.Label(innerRect, new GUIContent("", tooltip));
                    break;
                }

                case CellType.ExitGate:
                {
                    Color gateColor = cell.GetEffectiveColor();

                    // Рамка воріт
                    Color gateBorder = Color.Lerp(gateColor, Color.black, 0.35f);
                    gateBorder.a = 1f;
                    EditorGUI.DrawRect(outerRect, gateBorder);
                    EditorGUI.DrawRect(innerRect, gateColor);

                    // Біла смуга на зовнішньому краї виходу
                    float barThick = 3.5f;
                    switch (cell.exitDirection)
                    {
                        case ExitDirection.Up:
                            EditorGUI.DrawRect(new Rect(innerRect.x, innerRect.y, innerRect.width, barThick), Color.white);
                            break;
                        case ExitDirection.Down:
                            EditorGUI.DrawRect(new Rect(innerRect.x, innerRect.yMax - barThick, innerRect.width, barThick), Color.white);
                            break;
                        case ExitDirection.Left:
                            EditorGUI.DrawRect(new Rect(innerRect.x, innerRect.y, barThick, innerRect.height), Color.white);
                            break;
                        case ExitDirection.Right:
                            EditorGUI.DrawRect(new Rect(innerRect.xMax - barThick, innerRect.y, barThick, innerRect.height), Color.white);
                            break;
                    }

                    string arrow = cell.exitDirection switch
                    {
                        ExitDirection.Up => "▲",
                        ExitDirection.Down => "▼",
                        ExitDirection.Left => "◀",
                        ExitDirection.Right => "▶",
                        _ => "O"
                    };

                    if (cell.gateColorType == BlockColorType.Universal)
                    {
                        arrow = "★" + arrow;
                    }

                    Color textCol = GetContrastingTextColor(gateColor);
                    GUIStyle style = GetStyle(textCol, Mathf.RoundToInt(size * 0.44f));

                    string tooltip = $"🚪 Ворота: {cell.exitDirection}\nКолір: {cell.gateColorType}\nКлітинка: ({x}, {y})";
                    GUI.Label(innerRect, new GUIContent(arrow, tooltip), style);
                    break;
                }

                case CellType.HalfExitGate:
                {
                    // Фон — вільне поле
                    EditorGUI.DrawRect(innerRect, WalkableBaseColor);
                    Color gateColor = cell.GetEffectiveColor();

                    // 0 => Top (▀), 1 => Bottom (▄), 2 => Left (▌), 3 => Right (▐)
                    Rect gateHalf = cell.gateAlignment switch
                    {
                        0 => new Rect(innerRect.x, innerRect.y, innerRect.width, innerRect.height * 0.5f),
                        1 => new Rect(innerRect.x, innerRect.y + innerRect.height * 0.5f, innerRect.width, innerRect.height * 0.5f),
                        2 => new Rect(innerRect.x, innerRect.y, innerRect.width * 0.5f, innerRect.height),
                        3 => new Rect(innerRect.x + innerRect.width * 0.5f, innerRect.y, innerRect.width * 0.5f, innerRect.height),
                        _ => innerRect
                    };
                    EditorGUI.DrawRect(gateHalf, gateColor);

                    string arrow = cell.exitDirection switch
                    {
                        ExitDirection.Up => "▲",
                        ExitDirection.Down => "▼",
                        ExitDirection.Left => "◀",
                        ExitDirection.Right => "▶",
                        _ => ""
                    };

                    if (cell.gateColorType == BlockColorType.Universal)
                    {
                        arrow = "★" + arrow;
                    }

                    Color textCol = GetContrastingTextColor(gateColor);
                    GUIStyle style = GetStyle(textCol, Mathf.RoundToInt(size * 0.36f));

                    string tooltip = $"🚪 Половинні ворота: {cell.exitDirection} (Align: {cell.gateAlignment})\nКолір: {cell.gateColorType}\nКлітинка: ({x}, {y})";
                    GUI.Label(gateHalf, new GUIContent(arrow, tooltip), style);
                    break;
                }
            }
        }

        /// <summary>
        /// Малює блок компактної та стильної легенди кольорів і позначень.
        /// </summary>
        public static void DrawLegend()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("📌 Легенда позначень:", EditorStyles.boldLabel, GUILayout.Width(130));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            DrawLegendBadge("🚪 Ворота виходу", new Color(0.18f, 0.48f, 0.95f), Color.white, "▲");
            DrawLegendBadge("🧱 Стіна / Перешкода", ObstacleBaseColor, Color.white, "■");
            DrawLegendBadge("⬜ Вільне поле", WalkableBaseColor, new Color(0.2f, 0.2f, 0.2f), "·");
            DrawLegendBadge("🧩 Слайдер (H)", new Color(0.92f, 0.22f, 0.22f), Color.white, "↔");
            DrawLegendBadge("🧩 Слайдер (V)", new Color(0.28f, 0.85f, 0.28f), new Color(0.1f, 0.1f, 0.1f), "↕");
            DrawLegendBadge("🧩 Вільна деталь", new Color(0.98f, 0.85f, 0.15f), new Color(0.1f, 0.1f, 0.1f), "●");
            DrawLegendBadge("❄ Заморожений", new Color(0.20f, 0.65f, 0.95f), Color.white, "❄");
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private static void DrawLegendBadge(string text, Color bg, Color textCol, string symbol)
        {
            GUIContent content = new GUIContent($"{symbol} {text}");
            Vector2 textSize = EditorStyles.miniLabel.CalcSize(content);
            Rect r = GUILayoutUtility.GetRect(textSize.x + 14f, 22f, GUILayout.ExpandWidth(false));

            EditorGUI.DrawRect(r, GridBorderColor);
            Rect inner = new Rect(r.x + 1f, r.y + 1f, r.width - 2f, r.height - 2f);
            EditorGUI.DrawRect(inner, bg);

            GUIStyle s = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = textCol }
            };
            GUI.Label(inner, content, s);
            GUILayout.Space(4f);
        }
    }
}
#endif
