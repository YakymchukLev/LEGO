using UnityEngine;
using UnityEditor;
using LegoPuzzle.Runtime;

namespace LegoPuzzle.Editor
{
    public static class MenuUIEditorTools
    {
        private static Canvas FindMainCanvas()
        {
            Canvas[] allCanvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var c in allCanvases)
            {
                if (c == null) continue;
                if (c.name.Contains("Dev")) continue;
                if (c.transform.Find("DownPanel") != null || c.transform.Find("SettingsButton") != null || c.name == "Canvas")
                {
                    return c;
                }
            }
            foreach (var c in allCanvases)
            {
                if (c != null && !c.name.Contains("Dev")) return c;
            }
            return null;
        }

        [MenuItem("LEGO/Setup Menu Coins UI")]
        public static void SetupMenuCoinsUIEditor()
        {
            Canvas canvas = FindMainCanvas();
            if (canvas == null)
            {
                Debug.LogWarning("No Canvas found in current scene.");
                return;
            }

            MenuCoinsUI coinsUI = canvas.GetComponent<MenuCoinsUI>();
            if (coinsUI == null)
            {
                coinsUI = canvas.gameObject.AddComponent<MenuCoinsUI>();
            }

            coinsUI.EnsureUIHierarchy();
            coinsUI.UpdateUI(false);
            EditorUtility.SetDirty(canvas.gameObject);
            Debug.Log("<color=green>MenuCoinsUI: Coins widget successfully configured on Canvas!</color>");
        }

        [MenuItem("LEGO/Setup Menu Gems UI")]
        public static void SetupMenuGemsUIEditor()
        {
            Canvas canvas = FindMainCanvas();
            if (canvas == null)
            {
                Debug.LogWarning("No Canvas found in current scene.");
                return;
            }

            MenuGemsUI gemsUI = canvas.GetComponent<MenuGemsUI>();
            if (gemsUI == null)
            {
                gemsUI = canvas.gameObject.AddComponent<MenuGemsUI>();
            }

            gemsUI.EnsureUIHierarchy();
            gemsUI.UpdateUI(false);
            EditorUtility.SetDirty(canvas.gameObject);
            Debug.Log("<color=#D946EF><b>MenuGemsUI:</b> Gems widget successfully configured on Canvas!</color>");
        }
    }
}
