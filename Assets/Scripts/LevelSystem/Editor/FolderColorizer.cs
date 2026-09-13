using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LegoPuzzle.Editor
{
    /// <summary>
    /// Custom lightweight Unity Editor extension to change folder colors in the Project window.
    /// Right-click any folder -> "Set Folder Color" -> choose a color.
    /// </summary>
    [InitializeOnLoad]
    public static class FolderColorizer
    {
        private const string PREFS_KEY_PREFIX = "LEGO_FolderColor_";
        private static readonly Dictionary<string, Color> colorCache = new Dictionary<string, Color>();

        static FolderColorizer()
        {
            EditorApplication.projectWindowItemOnGUI -= OnProjectWindowItemGUI;
            EditorApplication.projectWindowItemOnGUI += OnProjectWindowItemGUI;
        }

        private static void OnProjectWindowItemGUI(string guid, Rect selectionRect)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (string.IsNullOrEmpty(guid)) return;

            if (!TryGetFolderColor(guid, out Color color)) return;

            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!AssetDatabase.IsValidFolder(path)) return;

            bool isListMode = selectionRect.height <= 20f;
            if (isListMode)
            {
                // 1. Subtle background glow for the folder row
                Rect rowGlow = new Rect(selectionRect.x - 2f, selectionRect.y, selectionRect.width + 4f, selectionRect.height);
                EditorGUI.DrawRect(rowGlow, new Color(color.r, color.g, color.b, 0.14f));

                // 2. Crisp vertical accent strip on the left edge
                Rect stripRect = new Rect(selectionRect.x - 3f, selectionRect.y + 1f, 3.5f, selectionRect.height - 2f);
                EditorGUI.DrawRect(stripRect, color);

                // 3. Colored tint overlay over the folder icon
                Rect iconRect = new Rect(selectionRect.x, selectionRect.y + 1f, 16f, 16f);
                EditorGUI.DrawRect(iconRect, new Color(color.r, color.g, color.b, 0.38f));

                // 4. Small vivid accent dot on the icon corner
                Rect dotRect = new Rect(selectionRect.x + 11f, selectionRect.y + 10f, 5f, 5f);
                EditorGUI.DrawRect(dotRect, color);
            }
            else
            {
                // Grid View (Large Icons)
                // 1. Subtle background card glow
                Rect cardGlow = new Rect(selectionRect.x, selectionRect.y, selectionRect.width, selectionRect.height);
                EditorGUI.DrawRect(cardGlow, new Color(color.r, color.g, color.b, 0.12f));

                // 2. Tint over the main folder icon area
                float iconSize = Mathf.Min(selectionRect.width, selectionRect.height - 18f);
                Rect iconRect = new Rect(
                    selectionRect.x + (selectionRect.width - iconSize) * 0.5f,
                    selectionRect.y + 2f,
                    iconSize,
                    iconSize - 4f
                );
                EditorGUI.DrawRect(iconRect, new Color(color.r, color.g, color.b, 0.28f));

                // 3. Colored accent bar under the folder icon
                Rect pillRect = new Rect(selectionRect.x + (selectionRect.width - 32f) * 0.5f, selectionRect.y + iconSize, 32f, 3f);
                EditorGUI.DrawRect(pillRect, color);
            }
        }

        public static bool TryGetFolderColor(string guid, out Color color)
        {
            if (colorCache.TryGetValue(guid, out color))
            {
                return color.a > 0.01f;
            }

            string prefKey = PREFS_KEY_PREFIX + guid;
            if (EditorPrefs.HasKey(prefKey))
            {
                string hex = EditorPrefs.GetString(prefKey, "");
                if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString("#" + hex, out color))
                {
                    colorCache[guid] = color;
                    return true;
                }
            }

            color = Color.clear;
            return false;
        }

        public static void SetFolderColorDirect(string guid, Color color)
        {
            if (string.IsNullOrEmpty(guid)) return;
            string hex = ColorUtility.ToHtmlStringRGBA(color);
            EditorPrefs.SetString(PREFS_KEY_PREFIX + guid, hex);
            colorCache[guid] = color;
            EditorApplication.RepaintProjectWindow();
        }

        private static void ApplyColorToSelection(Color color)
        {
            HashSet<string> targetGuids = GetSelectedFolderGuids();
            foreach (string guid in targetGuids)
            {
                SetFolderColorDirect(guid, color);
            }
        }

        private static void ClearColorForSelection()
        {
            HashSet<string> targetGuids = GetSelectedFolderGuids();
            foreach (string guid in targetGuids)
            {
                EditorPrefs.DeleteKey(PREFS_KEY_PREFIX + guid);
                colorCache.Remove(guid);
            }
            EditorApplication.RepaintProjectWindow();
        }

        private static HashSet<string> GetSelectedFolderGuids()
        {
            HashSet<string> result = new HashSet<string>();

            if (Selection.assetGUIDs != null && Selection.assetGUIDs.Length > 0)
            {
                foreach (string guid in Selection.assetGUIDs)
                {
                    string p = AssetDatabase.GUIDToAssetPath(guid);
                    if (AssetDatabase.IsValidFolder(p))
                    {
                        result.Add(guid);
                    }
                }
            }

            if (Selection.objects != null)
            {
                foreach (var obj in Selection.objects)
                {
                    string p = AssetDatabase.GetAssetPath(obj);
                    if (!string.IsNullOrEmpty(p) && AssetDatabase.IsValidFolder(p))
                    {
                        string g = AssetDatabase.AssetPathToGUID(p);
                        if (!string.IsNullOrEmpty(g))
                        {
                            result.Add(g);
                        }
                    }
                }
            }

            return result;
        }

        // ================= CONTEXT MENU ITEMS =================
        // Clean ASCII paths so Unity's native Win32 popup menu parser never fails

        [MenuItem("Assets/Set Folder Color/Red", false, 100)]
        private static void SetColorRed() => ApplyColorToSelection(new Color(0.95f, 0.28f, 0.28f, 1f));

        [MenuItem("Assets/Set Folder Color/Orange", false, 101)]
        private static void SetColorOrange() => ApplyColorToSelection(new Color(0.98f, 0.55f, 0.15f, 1f));

        [MenuItem("Assets/Set Folder Color/Yellow", false, 102)]
        private static void SetColorYellow() => ApplyColorToSelection(new Color(0.99f, 0.82f, 0.15f, 1f));

        [MenuItem("Assets/Set Folder Color/Green", false, 103)]
        private static void SetColorGreen() => ApplyColorToSelection(new Color(0.28f, 0.82f, 0.38f, 1f));

        [MenuItem("Assets/Set Folder Color/Blue", false, 104)]
        private static void SetColorBlue() => ApplyColorToSelection(new Color(0.22f, 0.60f, 0.98f, 1f));

        [MenuItem("Assets/Set Folder Color/Purple", false, 105)]
        private static void SetColorPurple() => ApplyColorToSelection(new Color(0.68f, 0.38f, 0.96f, 1f));

        [MenuItem("Assets/Set Folder Color/Cyan", false, 106)]
        private static void SetColorCyan() => ApplyColorToSelection(new Color(0.15f, 0.82f, 0.88f, 1f));

        [MenuItem("Assets/Set Folder Color/Pink", false, 107)]
        private static void SetColorPink() => ApplyColorToSelection(new Color(0.98f, 0.38f, 0.65f, 1f));

        [MenuItem("Assets/Set Folder Color/Dark Slate", false, 108)]
        private static void SetColorSlate() => ApplyColorToSelection(new Color(0.55f, 0.62f, 0.70f, 1f));

        [MenuItem("Assets/Set Folder Color/Custom Color...", false, 120)]
        private static void SetColorCustom()
        {
            var guids = GetSelectedFolderGuids();
            string[] arr = new string[guids.Count];
            guids.CopyTo(arr);
            CustomFolderColorWindow.Open(arr);
        }

        [MenuItem("Assets/Set Folder Color/Reset Color", false, 140)]
        private static void ResetFolderColor() => ClearColorForSelection();
    }

    /// <summary>
    /// Modal color picker popup for custom folder colors.
    /// </summary>
    public class CustomFolderColorWindow : EditorWindow
    {
        private Color chosenColor = new Color(0.25f, 0.75f, 1f, 1f);
        private string[] targetGuids;

        public static void Open(string[] guids)
        {
            var win = CreateInstance<CustomFolderColorWindow>();
            win.titleContent = new GUIContent("Choose Folder Color");
            win.targetGuids = guids;
            win.minSize = new Vector2(270, 95);
            win.maxSize = new Vector2(270, 95);
            win.ShowUtility();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(12);
            chosenColor = EditorGUILayout.ColorField("Color", chosenColor);
            EditorGUILayout.Space(12);

            if (GUILayout.Button("Apply Color", GUILayout.Height(28)))
            {
                if (targetGuids != null)
                {
                    foreach (string guid in targetGuids)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        if (AssetDatabase.IsValidFolder(path))
                        {
                            FolderColorizer.SetFolderColorDirect(guid, chosenColor);
                        }
                    }
                }
                Close();
            }
        }
    }
}
