#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LegoPuzzle.Runtime;

namespace LegoPuzzle.Editor
{
    [InitializeOnLoad]
    public static class SlotMachineSceneSetup
    {
        static SlotMachineSceneSetup()
        {
            EditorApplication.delayCall += EnsureSlotMachineInSceneDelay;
        }

        private static void EnsureSlotMachineInSceneDelay()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying) return;
            EnsureSlotMachineInScene(false);
        }

        [MenuItem("LEGO/Create Lotto Slot Machine Panel in Menu Scene")]
        public static void CreateSlotMachineInScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying)
            {
                Debug.LogWarning("[SlotMachineSceneSetup] Не можна змінювати сцену під час Play Mode.");
                return;
            }
            EnsureSlotMachineInScene(true);
        }

        private static void EnsureSlotMachineInScene(bool selectCreated)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying) return;

            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!activeScene.isLoaded) return;

            // Only setup in Menu scenes (Lotto is a menu-exclusive feature)
            bool isMenuScene = activeScene.name.IndexOf("Menu", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                               Object.FindAnyObjectByType<MenuButtonController>() != null ||
                               Object.FindAnyObjectByType<MenuHeartsUI>() != null;

            if (!isMenuScene) return;

            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null) return;

            // 1. Ensure SlotsButton in Menu
            Transform slotsBtnTr = canvas.transform.Find("SlotsButton");
            if (slotsBtnTr == null)
            {
                foreach (Transform child in canvas.transform)
                {
                    if (child.name.Equals("SlotsButton", System.StringComparison.OrdinalIgnoreCase))
                    {
                        slotsBtnTr = child;
                        break;
                    }
                    var textMesh = child.GetComponentInChildren<TextMeshProUGUI>();
                    if (textMesh != null && textMesh.text.IndexOf("Slots", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        slotsBtnTr = child;
                        break;
                    }
                }
            }

            if (slotsBtnTr != null)
            {
                slotsBtnTr.gameObject.name = "SlotsButton";
                var btn = slotsBtnTr.GetComponent<Button>();
                if (btn == null)
                {
                    btn = Undo.AddComponent<Button>(slotsBtnTr.gameObject);
                }
                var slotsScript = slotsBtnTr.GetComponent<SlotsButton>();
                if (slotsScript == null)
                {
                    slotsScript = Undo.AddComponent<SlotsButton>(slotsBtnTr.gameObject);
                }
                EditorUtility.SetDirty(slotsBtnTr.gameObject);
            }

            // 2. Ensure SlotMachinePanel in Menu Canvas
            SlotMachinePanel existing = canvas.GetComponentInChildren<SlotMachinePanel>(true);
            if (existing == null)
            {
                Transform existingChild = canvas.transform.Find("SlotMachinePanel");
                if (existingChild != null)
                {
                    existing = existingChild.GetComponent<SlotMachinePanel>();
                    if (existing == null)
                    {
                        existing = Undo.AddComponent<SlotMachinePanel>(existingChild.gameObject);
                    }
                }
                else
                {
                    GameObject slotObj = new GameObject("SlotMachinePanel");
                    slotObj.transform.SetParent(canvas.transform, false);
                    existing = slotObj.AddComponent<SlotMachinePanel>();
                    Undo.RegisterCreatedObjectUndo(slotObj, "Create SlotMachinePanel");
                }
            }

            if (existing != null)
            {
                existing.BuildUIEditor();
                existing.gameObject.SetActive(false); // Modal hidden by default

                if (selectCreated)
                {
                    Selection.activeGameObject = existing.gameObject;
                }

                if (!EditorApplication.isPlayingOrWillChangePlaymode && !Application.isPlaying)
                {
                    EditorUtility.SetDirty(existing.gameObject);
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
                }
            }
        }
    }
}
#endif
