#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using LegoPuzzle.Runtime;
using LegoPuzzle.Data;
using System.Collections.Generic;

namespace LegoPuzzle.Editor
{
    [CustomEditor(typeof(LevelLoader))]
    public class LevelLoaderEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawDefaultInspector();

            LevelLoader loader = (LevelLoader)target;

            LevelData previewLevel = loader.CurrentLevel != null ? loader.CurrentLevel : (LevelData)serializedObject.FindProperty("testLevelData").objectReferenceValue;
            if (previewLevel != null)
            {
                var bounds = loader.GetLevelActiveBounds(previewLevel);
                int maxDim = Mathf.Max(bounds.activeWidth, bounds.activeHeight);
                string scaleCategory = (maxDim <= 6) ? "Малий рівень (Наближений зум)" : (maxDim >= 12 ? "Великий рівень (Віддалений зум)" : "Середній рівень (Адаптивний зум)");

                EditorGUILayout.Space(8);
                EditorGUILayout.HelpBox(
                    $"🎯 Активна зона рівня '{previewLevel.name}':\n" +
                    $"• Реальні розміри: {bounds.activeWidth} x {bounds.activeHeight} клітинок (Загальна сітка: {previewLevel.gridWidth} x {previewLevel.gridHeight})\n" +
                    $"• Центр активної зони: X={bounds.centerCellX:F1}, Y={bounds.centerCellY:F1}\n" +
                    $"• Статус: {scaleCategory}",
                    MessageType.Info
                );
            }

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Швидкі дії LevelLoader", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.4f, 0.8f, 1f);
            if (GUILayout.Button("🔄 Оновити кадрування камери", GUILayout.Height(28)))
            {
                loader.RefreshCameraFraming();
            }

            GUI.backgroundColor = new Color(0.5f, 1f, 0.6f);
            if (GUILayout.Button("🔍 Заповнити всі LevelData", GUILayout.Height(28)))
            {
                PopulateAllLevels(loader);
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();
        }

        private void PopulateAllLevels(LevelLoader loader)
        {
            string[] guids = AssetDatabase.FindAssets("t:LevelData");
            var list = new List<LevelData>();

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
                if (level != null)
                {
                    list.Add(level);
                }
            }

            list.Sort((a, b) => a.levelIndex.CompareTo(b.levelIndex));

            Undo.RecordObject(loader, "Populate All Levels");
            var prop = serializedObject.FindProperty("allLevels");
            if (prop != null)
            {
                prop.ClearArray();
                for (int i = 0; i < list.Count; i++)
                {
                    prop.InsertArrayElementAtIndex(i);
                    prop.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
                }
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(loader);
            Debug.Log($"<color=green>LevelLoader: Знайдено та впорядковано {list.Count} рівнів у списку All Levels!</color>");
        }
    }

    [CustomEditor(typeof(LevelData))]
    public class LevelDataEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            LevelData level = (LevelData)target;

            EditorGUILayout.Space(6);
            if (GUILayout.Button("🎨 Відкрити в LEGO Level Editor", GUILayout.Height(30)))
            {
                LevelEditorWindow.OpenWindow();
            }

            EditorGUILayout.Space(8);
            DrawDefaultInspector();

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
