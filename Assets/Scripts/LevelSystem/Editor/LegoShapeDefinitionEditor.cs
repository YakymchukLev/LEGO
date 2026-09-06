#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using LegoPuzzle.Data;

namespace LegoPuzzle.Editor
{
    [CustomEditor(typeof(LegoShapeDefinition))]
    public class LegoShapeDefinitionEditor : UnityEditor.Editor
    {
        private const int MATRIX_SIZE = 5;

        public override void OnInspectorGUI()
        {
            LegoShapeDefinition shape = (LegoShapeDefinition)target;

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Налаштування форми LEGO", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            shape.shapeId = EditorGUILayout.TextField("ID Форми", shape.shapeId);
            shape.prefab = (GameObject)EditorGUILayout.ObjectField("3D Префаб", shape.prefab, typeof(GameObject), false);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Візуальний макет форми (клікайте по клітинках):", EditorStyles.boldLabel);

            // Відображаємо сітку 5x5 для малювання форми
            for (int y = MATRIX_SIZE - 1; y >= 0; y--)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                for (int x = 0; x < MATRIX_SIZE; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    bool isOccupied = shape.localOccupiedCells != null && shape.localOccupiedCells.Contains(cell);

                    Color originalBg = GUI.backgroundColor;
                    GUI.backgroundColor = isOccupied ? new Color(0.2f, 0.7f, 1f) : new Color(0.3f, 0.3f, 0.3f, 0.4f);

                    string btnText = isOccupied ? "■" : "+";

                    if (GUILayout.Button(btnText, GUILayout.Width(36), GUILayout.Height(36)))
                    {
                        Undo.RecordObject(shape, "Toggle Shape Cell");
                        if (isOccupied)
                        {
                            shape.localOccupiedCells.Remove(cell);
                        }
                        else
                        {
                            if (shape.localOccupiedCells == null)
                                shape.localOccupiedCells = new System.Collections.Generic.List<Vector2Int>();

                            shape.localOccupiedCells.Add(cell);
                        }

                        // Автоматично оновлюємо розміри bounds
                        UpdateShapeBounds(shape);
                        EditorUtility.SetDirty(shape);
                    }

                    GUI.backgroundColor = originalBg;
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Швидкі шаблони стандартних форм:", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("1x1 (Block 1)"))
            {
                ApplyPreset(shape, "Shape_1x1", "11ace2a8b7a4b1443a7fc5e113b6f597", new[] { new Vector2Int(0, 0) });
            }
            if (GUILayout.Button("1x2 (Block 2)"))
            {
                ApplyPreset(shape, "Shape_1x2", "415f33a2e675e6f4c895c6382923c3b8", new[] { new Vector2Int(0, 0), new Vector2Int(0, 1) });
            }
            if (GUILayout.Button("1x3 (Block 3)"))
            {
                ApplyPreset(shape, "Shape_1x3", "cc06fc4221fe3824f9f2c23c4bb3bfbe", new[] { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2) });
            }
            if (GUILayout.Button("2x2 (Block 4)"))
            {
                ApplyPreset(shape, "Shape_2x2", "37d4c3b709b03e2429e0bd01b8f9632e", new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1) });
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("L 2x2 (Block 5)"))
            {
                ApplyPreset(shape, "Shape_L_2x2", "6103ec50f8d81184296f97dd7afcdc4e", new[] { new Vector2Int(0, 1), new Vector2Int(0, 0), new Vector2Int(1, 0) });
            }
            if (GUILayout.Button("L 3x2 (Block 6)"))
            {
                ApplyPreset(shape, "Shape_L_3x2", "9bd4832cb241cb34ba4596f0de8cf1ec", new[] { new Vector2Int(0, 2), new Vector2Int(0, 1), new Vector2Int(0, 0), new Vector2Int(1, 0) });
            }
            if (GUILayout.Button("T 3x2 (Block 7)"))
            {
                ApplyPreset(shape, "Shape_T_3x2", "2f647a87ece48c64fb5255b2b03059cd", new[] { new Vector2Int(0, 2), new Vector2Int(0, 1), new Vector2Int(0, 0), new Vector2Int(1, 1) });
            }
            if (GUILayout.Button("Cross 3x3 (Block 8)"))
            {
                ApplyPreset(shape, "Shape_Cross_3x3", "e05bacd348016e54ea912c46fabb719d", new[] { new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(1, 2), new Vector2Int(0, 1), new Vector2Int(2, 1) });
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);
            if (GUILayout.Button("Очистити форму"))
            {
                Undo.RecordObject(shape, "Clear Shape");
                shape.localOccupiedCells.Clear();
                shape.bounds = Vector2Int.one;
                EditorUtility.SetDirty(shape);
            }

            if (EditorGUI.EndChangeCheck())
            {
                UpdateShapeBounds(shape);
                EditorUtility.SetDirty(shape);
            }
        }

        private void ApplyPreset(LegoShapeDefinition shape, string shapeId, string prefabGuid, Vector2Int[] cells)
        {
            Undo.RecordObject(shape, "Apply Shape Preset");
            shape.shapeId = shapeId;

            string path = AssetDatabase.GUIDToAssetPath(prefabGuid);
            if (!string.IsNullOrEmpty(path))
            {
                shape.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }

            shape.localOccupiedCells = new System.Collections.Generic.List<Vector2Int>(cells);
            UpdateShapeBounds(shape);
            EditorUtility.SetDirty(shape);
        }

        private void UpdateShapeBounds(LegoShapeDefinition shape)
        {
            if (shape.localOccupiedCells == null || shape.localOccupiedCells.Count == 0)
            {
                shape.bounds = Vector2Int.one;
                return;
            }

            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;

            foreach (var cell in shape.localOccupiedCells)
            {
                if (cell.x < minX) minX = cell.x;
                if (cell.x > maxX) maxX = cell.x;
                if (cell.y < minY) minY = cell.y;
                if (cell.y > maxY) maxY = cell.y;
            }

            shape.bounds = new Vector2Int(maxX - minX + 1, maxY - minY + 1);
        }
    }
}
#endif
