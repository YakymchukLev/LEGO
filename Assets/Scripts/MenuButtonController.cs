using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuButtonController : MonoBehaviour
{
    [Header("Scene Settings")]
    [Tooltip("Назва сцени для завантаження")]
    [SerializeField] private string defaultSceneName = "GameScene";

    [Header("Loading UI (Опціонально)")]
    [Tooltip("Екран завантаження")]
    [SerializeField] private GameObject loadingScreen;

    [Tooltip("Слайдер прогресу")]
    [SerializeField] private Slider progressBar;

    /// <summary>
    /// Запуск асинхронного завантаження сцени за замовчуванням (зручно для Unity UI Button)
    /// </summary>
    public void StartGame()
    {
        StartCoroutine(LoadSceneAsyncRoutine(defaultSceneName));
    }

    /// <summary>
    /// Запуск завантаження сцени за її назвою
    /// </summary>
    public void StartGame(string sceneName)
    {
        StartCoroutine(LoadSceneAsyncRoutine(sceneName));
    }

    /// <summary>
    /// Запуск завантаження сцени за індексом у Build Settings
    /// </summary>
    public void StartGame(int sceneIndex)
    {
        StartCoroutine(LoadSceneAsyncRoutine(sceneIndex));
    }

    private IEnumerator LoadSceneAsyncRoutine(string sceneName)
    {
        if (loadingScreen != null)
        {
            loadingScreen.SetActive(true);
        }

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);

        while (!operation.isDone)
        {
            // operation.progress йде від 0 до 0.9 під час завантаження
            float progress = Mathf.Clamp01(operation.progress / 0.9f);

            if (progressBar != null)
            {
                progressBar.value = progress;
            }

            yield return null;
        }
    }

    private IEnumerator LoadSceneAsyncRoutine(int sceneIndex)
    {
        if (loadingScreen != null)
        {
            loadingScreen.SetActive(true);
        }

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneIndex);

        while (!operation.isDone)
        {
            float progress = Mathf.Clamp01(operation.progress / 0.9f);

            if (progressBar != null)
            {
                progressBar.value = progress;
            }

            yield return null;
        }
    }
}
