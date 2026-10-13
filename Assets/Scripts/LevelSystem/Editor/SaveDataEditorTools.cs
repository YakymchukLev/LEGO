using System.IO;
using UnityEditor;
using UnityEngine;
using LegoPuzzle.Runtime;

namespace LegoPuzzle.Editor
{
    public static class SaveDataEditorTools
    {
        [MenuItem("Tools/LEGO/Save Data/Open Save Data File")]
        public static void OpenSaveFile()
        {
            string path = SaveDataService.SaveFilePath;
            if (File.Exists(path))
            {
                EditorUtility.RevealInFinder(path);
            }
            else
            {
                EditorUtility.RevealInFinder(Application.persistentDataPath);
            }
        }

        [MenuItem("Tools/LEGO/Save Data/Print Current Save JSON to Console")]
        public static void PrintSaveJson()
        {
            if (SaveDataService.Instance != null && SaveDataService.Instance.Data != null)
            {
                string json = JsonUtility.ToJson(SaveDataService.Instance.Data, true);
                Debug.Log($"<color=cyan>[SaveDataService] Поточний стан збереження:\n{json}</color>");
            }
            else
            {
                Debug.LogWarning("[SaveDataService] Дані ще не завантажені.");
            }
        }

        [MenuItem("Tools/LEGO/Save Data/Reload Save Data From Disk")]
        public static void ReloadSaveData()
        {
            SaveDataService.Instance.Load();
            Debug.Log("<color=green>[SaveDataService] Сейв перевантажено з диска.</color>");
        }

        [MenuItem("Tools/LEGO/Save Data/Reset All Progress and Currencies")]
        public static void ResetSaveData()
        {
            if (EditorUtility.DisplayDialog("Скидання даних", "Ви впевнені, що хочете скинути весь прогрес, монети, геми та бустери до дефолтних значень?", "Скинути", "Скасувати"))
            {
                SaveDataService.Instance.ResetAllData();
                Debug.Log("<color=yellow>[SaveDataService] Сейв повністю очищено.</color>");
            }
        }

        [MenuItem("Tools/LEGO/Dev Panel/Toggle Dev Panel (F1 or ~)")]
        public static void ToggleDevPanel()
        {
            if (LevelDevPanel.Instance != null)
            {
                LevelDevPanel.Instance.TogglePanel();
            }
            else
            {
                Debug.LogWarning("[LevelDevPanel] Панель доступна під час запуску гри (Play Mode) або на телефоні.");
            }
        }
    }
}
