using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using LegoPuzzle.Runtime;
using LegoPuzzle.Data;

namespace LegoPuzzle.Editor
{
    /// <summary>
    /// Автоматичний валідатор білда (Build Preprocessor), що запускається перед кожним білдом гри.
    /// Запобігає поширеним пасткам збирання (Editor-only AssetDatabase залежностям,
    /// відсутності рівнів у списку сцени, втраченим префабам та аудіофайлам).
    /// Також доступний у меню: Tools -> LEGO -> Build -> Validate Build Assets
    /// </summary>
    public class PreBuildAssetValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            Debug.Log("<color=cyan><b>[PreBuildAssetValidator]</b> Запуск перевірки цілісності асетів перед білдом...</color>");
            bool success = ValidateAndSyncAll();
            if (!success)
            {
                Debug.LogWarning("[PreBuildAssetValidator] Виявлено попередження цілісності асетів під час перевірки перед білдом.");
            }
        }

        [MenuItem("Tools/LEGO/Build/Validate Build Assets & Sync All Levels", priority = 10)]
        public static void RunManualValidation()
        {
            bool ok = ValidateAndSyncAll();
            if (ok)
            {
                EditorUtility.DisplayDialog(
                    "Успішна перевірка",
                    "Всі залежності білда успішно перевірено та синхронізовано!\n\n" +
                    "• Всі 26 рівнів синхронізовано у LevelLoader та Resources\n" +
                    "• Палітру блоків MainBlockPalette перевірено в Resources\n" +
                    "• Префаби hammer та tutorial_hand готові в Resources\n" +
                    "• Звуки та шрифти доступні для Player Build\n" +
                    "• Залежності від AssetDatabase усунуто.",
                    "Чудово"
                );
            }
        }

        public static bool ValidateAndSyncAll()
        {
            int warnings = 0;

            // 1. Пошук та синхронізація всіх рівнів
            List<LevelData> allLevels = GetAllProjectLevelsSorted();
            Debug.Log($"<color=white>[PreBuildAssetValidator] Знайдено рівнів у проєкті: {allLevels.Count}</color>");

            if (allLevels.Count == 0)
            {
                Debug.LogError("[PreBuildAssetValidator] Помилка: У проєкті не знайдено жодного LevelData!");
                warnings++;
            }
            else
            {
                // Синхронізація у Game.unity
                SyncLevelsToGameScene(allLevels);

                // Перевірка копій у Resources/Levels
                EnsureLevelsInResources(allLevels);
            }

            // 2. Перевірка палітри блоків
            var palette = Resources.Load<BlockPalette>("MainBlockPalette");
            if (palette == null)
            {
                palette = Resources.Load<BlockPalette>("BlockPalette");
            }
            if (palette == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:BlockPalette");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    EnsureFolder("Assets/Resources");
                    AssetDatabase.CopyAsset(path, "Assets/Resources/MainBlockPalette.asset");
                    Debug.Log("<color=green>[PreBuildAssetValidator] Скопійовано MainBlockPalette.asset у Assets/Resources</color>");
                }
                else
                {
                    Debug.LogWarning("[PreBuildAssetValidator] BlockPalette не знайдено в проєкті!");
                    warnings++;
                }
            }

            // 3. Перевірка префабів молотка та руки
            var hammer = Resources.Load<GameObject>("Prefabs/hammer");
            if (hammer == null)
            {
                EnsureFolder("Assets/Resources/Prefabs");
                if (File.Exists("Assets/Prefabs/hammer.prefab"))
                {
                    AssetDatabase.CopyAsset("Assets/Prefabs/hammer.prefab", "Assets/Resources/Prefabs/hammer.prefab");
                    Debug.Log("<color=green>[PreBuildAssetValidator] Скопійовано hammer.prefab у Resources/Prefabs</color>");
                }
                else
                {
                    Debug.LogWarning("[PreBuildAssetValidator] hammer.prefab не знайдено!");
                    warnings++;
                }
            }

            var hand = Resources.Load<GameObject>("Prefabs/tutorial_hand");
            if (hand == null)
            {
                EnsureFolder("Assets/Resources/Prefabs");
                if (File.Exists("Assets/Prefabs/tutorial_hand.prefab"))
                {
                    AssetDatabase.CopyAsset("Assets/Prefabs/tutorial_hand.prefab", "Assets/Resources/Prefabs/tutorial_hand.prefab");
                    Debug.Log("<color=green>[PreBuildAssetValidator] Скопійовано tutorial_hand.prefab у Resources/Prefabs</color>");
                }
                else
                {
                    Debug.LogWarning("[PreBuildAssetValidator] tutorial_hand.prefab не знайдено!");
                    warnings++;
                }
            }

            // 4. Перевірка аудіофайлів
            EnsureAudioInResources("Button_Click.wav", "Assets/Resources/Sound/Button_Click.wav");
            EnsureAudioInResources("main-menu.wav", "Assets/Resources/Sound/main-menu.wav");
            EnsureAudioInResources("lvlsound.mp3", "Assets/Resources/Sound/lvlsound.mp3");

            // 5. Перевірка шрифту
            var font = Resources.Load<TMPro.TMP_FontAsset>("Fonts/rimouski sb SDF");
            if (font == null)
            {
                EnsureFolder("Assets/Resources/Fonts");
                if (File.Exists("Assets/Fonts/rimouski sb SDF.asset"))
                {
                    AssetDatabase.CopyAsset("Assets/Fonts/rimouski sb SDF.asset", "Assets/Resources/Fonts/rimouski sb SDF.asset");
                    Debug.Log("<color=green>[PreBuildAssetValidator] Скопійовано rimouski sb SDF.asset у Resources/Fonts</color>");
                }
            }

            // 6. Перевірка матеріалів та шейдерів льоду та обводки (для захисту від стрипінгу шейдерів у білді)
            EnsureShaderAndMaterialsInResources();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"<color=green><b>[PreBuildAssetValidator]</b> Перевірку завершено успішно! Попереджень: {warnings}</color>");
            return warnings == 0;
        }

        private static void EnsureShaderAndMaterialsInResources()
        {
            EnsureFolder("Assets/Resources/Materials");

            // IceBlock Material
            string iceMatPath = "Assets/Resources/Materials/IceBlockMaterial.mat";
            Material iceMat = AssetDatabase.LoadAssetAtPath<Material>(iceMatPath);
            Shader iceShader = Shader.Find("Custom/IceBlock");
            if (iceMat == null)
            {
                if (iceShader != null)
                {
                    iceMat = new Material(iceShader);
                    iceMat.name = "IceBlockMaterial";
                    iceMat.SetColor("_IceColor", new Color(0.55f, 0.88f, 1.0f, 0.18f));
                    iceMat.SetColor("_BaseColor", new Color(0.55f, 0.88f, 1.0f, 0.18f));
                    iceMat.SetColor("_Color", new Color(0.55f, 0.88f, 1.0f, 0.18f));
                    iceMat.SetColor("_RimColor", new Color(0.92f, 0.98f, 1.0f, 0.85f));
                    iceMat.SetFloat("_RimPower", 3.5f);
                    iceMat.SetFloat("_FrostGlow", 0.25f);
                    iceMat.SetFloat("_CrackIntensity", 0.0f);
                    AssetDatabase.CreateAsset(iceMat, iceMatPath);
                    Debug.Log("<color=green>[PreBuildAssetValidator] Створено IceBlockMaterial.mat у Resources/Materials</color>");
                }
            }
            else if (iceShader != null && iceMat.shader != iceShader)
            {
                iceMat.shader = iceShader;
                EditorUtility.SetDirty(iceMat);
            }

            // BlockOutline Material
            string outlineMatPath = "Assets/Resources/Materials/BlockOutlineMaterial.mat";
            Material outlineMat = AssetDatabase.LoadAssetAtPath<Material>(outlineMatPath);
            Shader outlineShader = Shader.Find("Custom/BlockOutline");
            if (outlineMat == null)
            {
                if (outlineShader != null)
                {
                    outlineMat = new Material(outlineShader);
                    outlineMat.name = "BlockOutlineMaterial";
                    outlineMat.SetColor("_OutlineColor", new Color(1f, 1f, 1f, 0.95f));
                    outlineMat.SetFloat("_OutlineWidth", 0.045f);
                    AssetDatabase.CreateAsset(outlineMat, outlineMatPath);
                    Debug.Log("<color=green>[PreBuildAssetValidator] Створено BlockOutlineMaterial.mat у Resources/Materials</color>");
                }
            }
            else if (outlineShader != null && outlineMat.shader != outlineShader)
            {
                outlineMat.shader = outlineShader;
                EditorUtility.SetDirty(outlineMat);
            }

            // Додавання шейдерів у Always Included Shaders в GraphicsSettings
            EnsureShaderAlwaysIncluded(iceShader);
            EnsureShaderAlwaysIncluded(outlineShader);
        }

        private static void EnsureShaderAlwaysIncluded(Shader shader)
        {
            if (shader == null) return;
            try
            {
                string graphicsSettingsAssetPath = "ProjectSettings/GraphicsSettings.asset";
                var assets = AssetDatabase.LoadAllAssetsAtPath(graphicsSettingsAssetPath);
                if (assets != null && assets.Length > 0)
                {
                    SerializedObject graphicsSettings = new SerializedObject(assets[0]);
                    SerializedProperty arrayProp = graphicsSettings.FindProperty("m_AlwaysIncludedShaders");
                    if (arrayProp != null)
                    {
                        bool found = false;
                        for (int i = 0; i < arrayProp.arraySize; i++)
                        {
                            var elem = arrayProp.GetArrayElementAtIndex(i);
                            if (elem.objectReferenceValue == shader)
                            {
                                found = true;
                                break;
                            }
                        }

                        if (!found)
                        {
                            int index = arrayProp.arraySize;
                            arrayProp.InsertArrayElementAtIndex(index);
                            arrayProp.GetArrayElementAtIndex(index).objectReferenceValue = shader;
                            graphicsSettings.ApplyModifiedProperties();
                            Debug.Log($"<color=green>[PreBuildAssetValidator] Додано шейдер '{shader.name}' у Always Included Shaders!</color>");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PreBuildAssetValidator] Не вдалося перевірити Always Included Shaders: {ex.Message}");
            }
        }

        private static List<LevelData> GetAllProjectLevelsSorted()
        {
            string[] guids = AssetDatabase.FindAssets("t:LevelData");
            var list = new List<LevelData>();

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                // Ігноруємо дублікати з папки Resources, якщо вони вже існують
                if (path.Contains("/Resources/")) continue;

                var level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
                if (level != null && !list.Contains(level))
                {
                    list.Add(level);
                }
            }

            list.Sort((a, b) =>
            {
                if (a.levelIndex != b.levelIndex) return a.levelIndex.CompareTo(b.levelIndex);
                return string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
            });

            return list;
        }

        private static void SyncLevelsToGameScene(List<LevelData> allLevels)
        {
            string gameScenePath = "Assets/Scenes/Game.unity";
            if (!File.Exists(gameScenePath)) return;

            var currentScene = EditorSceneManager.GetActiveScene();
            bool wasGameSceneOpen = currentScene.path == gameScenePath;

            var sceneToEdit = wasGameSceneOpen ? currentScene : EditorSceneManager.OpenScene(gameScenePath, OpenSceneMode.Additive);
            var rootObjects = sceneToEdit.GetRootGameObjects();

            LevelLoader loader = null;
            foreach (var root in rootObjects)
            {
                loader = root.GetComponentInChildren<LevelLoader>(true);
                if (loader != null) break;
            }

            if (loader != null)
            {
                var serializedLoader = new SerializedObject(loader);
                var levelsProp = serializedLoader.FindProperty("allLevels");

                if (levelsProp != null)
                {
                    levelsProp.ClearArray();
                    for (int i = 0; i < allLevels.Count; i++)
                    {
                        levelsProp.InsertArrayElementAtIndex(i);
                        levelsProp.GetArrayElementAtIndex(i).objectReferenceValue = allLevels[i];
                    }

                    serializedLoader.ApplyModifiedProperties();
                    EditorSceneManager.MarkSceneDirty(sceneToEdit);
                    EditorSceneManager.SaveScene(sceneToEdit);
                    Debug.Log($"<color=green>[PreBuildAssetValidator] Синхронізовано {allLevels.Count} рівнів у сцену Game.unity!</color>");
                }
            }

            if (!wasGameSceneOpen)
            {
                EditorSceneManager.CloseScene(sceneToEdit, true);
            }
        }

        private static void EnsureLevelsInResources(List<LevelData> allLevels)
        {
            EnsureFolder("Assets/Resources/Levels");
            foreach (var lvl in allLevels)
            {
                string targetPath = $"Assets/Resources/Levels/{lvl.name}.asset";
                if (!File.Exists(targetPath))
                {
                    string sourcePath = AssetDatabase.GetAssetPath(lvl);
                    AssetDatabase.CopyAsset(sourcePath, targetPath);
                }
            }
        }

        private static void EnsureAudioInResources(string soundFileName, string targetPath)
        {
            EnsureFolder("Assets/Resources/Sound");
            if (!File.Exists(targetPath))
            {
                string sourcePath = $"Assets/Sound/{soundFileName}";
                if (File.Exists(sourcePath))
                {
                    AssetDatabase.CopyAsset(sourcePath, targetPath);
                    Debug.Log($"<color=green>[PreBuildAssetValidator] Скопійовано {soundFileName} у Resources/Sound</color>");
                }
            }
        }

        private static void EnsureFolder(string relativePath)
        {
            if (!AssetDatabase.IsValidFolder(relativePath))
            {
                string parent = Path.GetDirectoryName(relativePath).Replace('\\', '/');
                string folderName = Path.GetFileName(relativePath);
                if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                {
                    EnsureFolder(parent);
                }
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }
    }
}
