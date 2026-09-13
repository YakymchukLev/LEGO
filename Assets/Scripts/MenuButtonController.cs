using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using LegoPuzzle.Runtime;

public class MenuButtonController : MonoBehaviour
{
    [Header("Scene Settings")]
    [Tooltip("Scene name to load")]
    [SerializeField] private string defaultSceneName = "GameScene";

    [Header("Loading UI (Optional)")]
    [Tooltip("Loading screen")]
    [SerializeField] private GameObject loadingScreen;

    [Tooltip("Progress slider")]
    [SerializeField] private Slider progressBar;

    private const string PREFS_LEVEL_INDEX = "LEGO_CurrentLevelIndex";

    public static int GetSavedLevelIndex()
    {
        return PlayerPrefs.GetInt(PREFS_LEVEL_INDEX, 0);
    }

    [ContextMenu("Reset saved progress (to Level 1)")]
    public void ResetSavedLevelProgress()
    {
        PlayerPrefs.DeleteKey(PREFS_LEVEL_INDEX);
        PlayerPrefs.Save();
        Debug.Log("<color=yellow>MenuButtonController: Progress reset! Next game will start from Level 1.</color>");
    }

    public void LoadMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(0);
    }

    public void LoadGame()
    {
        if (HeartManager.Instance != null && !HeartManager.Instance.HasHearts())
        {
            Debug.LogWarning("<color=red>MenuButtonController: Cannot start game, player has 0 hearts!</color>");
            MenuHeartsUI.TriggerNoHeartsFeedback();
            return;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(1);
    }

    public void LoadScene(string sceneName)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }

    public void LoadScene(int sceneIndex)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneIndex);
    }

    public void StartGame()
    {
        if (HeartManager.Instance != null && !HeartManager.Instance.HasHearts())
        {
            Debug.LogWarning("<color=red>MenuButtonController: Cannot start game, player has 0 hearts!</color>");
            MenuHeartsUI.TriggerNoHeartsFeedback();
            return;
        }

        Time.timeScale = 1f;
        StartCoroutine(LoadSceneAsyncRoutine(defaultSceneName));
    }

    public void StartGame(string sceneName)
    {
        if (HeartManager.Instance != null && !HeartManager.Instance.HasHearts())
        {
            Debug.LogWarning("<color=red>MenuButtonController: Cannot start game, player has 0 hearts!</color>");
            MenuHeartsUI.TriggerNoHeartsFeedback();
            return;
        }

        Time.timeScale = 1f;
        StartCoroutine(LoadSceneAsyncRoutine(sceneName));
    }

    public void StartGame(int sceneIndex)
    {
        if (HeartManager.Instance != null && !HeartManager.Instance.HasHearts())
        {
            Debug.LogWarning("<color=red>MenuButtonController: Cannot start game, player has 0 hearts!</color>");
            MenuHeartsUI.TriggerNoHeartsFeedback();
            return;
        }

        Time.timeScale = 1f;
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
