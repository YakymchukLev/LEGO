#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using LegoPuzzle.Runtime;

namespace LegoPuzzle.Editor
{
    public static class SafeAreaEditorHelper
    {
        [MenuItem("LEGO/Apply SafeArea to Active Canvas", false, 40)]
        public static void ApplySafeAreaToActiveCanvas()
        {
            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                EditorUtility.DisplayDialog("SafeArea", "У поточній сцені не знайдено жодного Canvas!", "OK");
                return;
            }

            // Перевіряємо, чи вже є SafeAreaContainer
            Transform existing = canvas.transform.Find("SafeAreaContainer");
            if (existing != null)
            {
                SafeArea sa = existing.GetComponent<SafeArea>();
                if (sa == null) sa = existing.gameObject.AddComponent<SafeArea>();
                sa.ApplySafeArea(true);
                Selection.activeGameObject = existing.gameObject;
                EditorUtility.DisplayDialog("SafeArea", "SafeAreaContainer вже існує на Canvas. Налаштування оновлено!", "OK");
                return;
            }

            // Створюємо SafeAreaContainer під Canvas
            GameObject container = new GameObject("SafeAreaContainer", typeof(RectTransform));
            container.transform.SetParent(canvas.transform, false);
            container.transform.SetAsFirstSibling();

            RectTransform rt = container.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            SafeArea safeAreaComp = container.AddComponent<SafeArea>();
            safeAreaComp.ApplySafeArea(true);

            Undo.RegisterCreatedObjectUndo(container, "Create SafeAreaContainer");
            Selection.activeGameObject = container;

            EditorUtility.SetDirty(canvas.gameObject);
            EditorUtility.DisplayDialog("SafeArea", "Успішно створено 'SafeAreaContainer' з компонентом SafeArea!\nТепер ви можете розмістити елементи інтерфейсу (HUD, верхні кнопки, панелі) всередину цього контейнера.", "OK");
        }

        [MenuItem("CONTEXT/RectTransform/Add SafeArea Component")]
        private static void AddSafeAreaToRectTransform(MenuCommand command)
        {
            RectTransform rt = command.context as RectTransform;
            if (rt == null) return;

            SafeArea existing = rt.GetComponent<SafeArea>();
            if (existing == null)
            {
                existing = Undo.AddComponent<SafeArea>(rt.gameObject);
            }
            existing.ApplySafeArea(true);
            EditorUtility.SetDirty(rt.gameObject);
        }

        [MenuItem("LEGO/Setup Background Music in Current Scene", false, 41)]
        public static void SetupBackgroundMusicInScene()
        {
            GameSettingsManager manager = Object.FindAnyObjectByType<GameSettingsManager>();
            if (manager == null)
            {
                GameObject obj = new GameObject("[GameSettingsManager]");
                manager = obj.AddComponent<GameSettingsManager>();
                Undo.RegisterCreatedObjectUndo(obj, "Create GameSettingsManager");
            }

            manager.EnsureAudioSources();
            if (manager.MenuMusicClip == null)
            {
                manager.MenuMusicClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/main-menu.wav");
            }
            if (manager.GameMusicClip == null)
            {
                manager.GameMusicClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/lvlsound.mp3");
            }

            EditorUtility.SetDirty(manager);
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (activeScene.isLoaded)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
            }

            EditorUtility.DisplayDialog("Background Music", "Фонова музика налаштована!\n- Меню: Assets/Sound/main-menu.wav\n- Гра: Assets/Sound/lvlsound.mp3", "OK");
        }
    }
}
#endif
