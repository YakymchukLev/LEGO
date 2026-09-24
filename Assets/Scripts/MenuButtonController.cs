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

    private void Start()
    {
        SetupBottomNavigationJellyButtons();
    }

    /// <summary>
    /// Configures horizontal jelly squash & stretch animation on bottom navigation buttons (Shop, Main Menu, Play).
    /// </summary>
    public void SetupBottomNavigationJellyButtons()
    {
        Transform downPanel = transform.Find("DownPanel");
        if (downPanel == null)
        {
            var allTrs = Resources.FindObjectsOfTypeAll<Transform>();
            for (int i = 0; i < allTrs.Length; i++)
            {
                Transform tr = allTrs[i];
                if (tr != null && tr.name == "DownPanel" && tr.gameObject.scene == gameObject.scene)
                {
                    downPanel = tr;
                    break;
                }
            }
        }

        if (downPanel != null)
        {
            var buttons = downPanel.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button btn = buttons[i];
                if (btn != null)
                {
                    UIButtonPressEffect.AttachHorizontalJelly(btn.gameObject, 1.25f, 0.80f, 0.45f);
                }
            }
        }
    }

    public void LoadMenu()
    {
        if (GameSettingsManager.HasInstance) GameSettingsManager.Instance.PlayClickSound();
        Time.timeScale = 1f;
        SceneManager.LoadScene(0);
    }

    public void LoadGame()
    {
        if (GameSettingsManager.HasInstance) GameSettingsManager.Instance.PlayClickSound();
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
        if (GameSettingsManager.HasInstance) GameSettingsManager.Instance.PlayClickSound();
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }

    public void LoadScene(int sceneIndex)
    {
        if (GameSettingsManager.HasInstance) GameSettingsManager.Instance.PlayClickSound();
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneIndex);
    }

    public void StartGame()
    {
        if (GameSettingsManager.HasInstance) GameSettingsManager.Instance.PlayClickSound();
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
        if (GameSettingsManager.HasInstance) GameSettingsManager.Instance.PlayClickSound();
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
        if (GameSettingsManager.HasInstance) GameSettingsManager.Instance.PlayClickSound();
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
        // Brief pause to allow the horizontal jelly squeeze and bounce to be enjoyed
        yield return new WaitForSecondsRealtime(0.10f);

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
        // Brief pause to allow the horizontal jelly squeeze and bounce to be enjoyed
        yield return new WaitForSecondsRealtime(0.10f);

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

    /// <summary>
    /// Opens the Lotto / Slot Machine panel in Menu scene.
    /// </summary>
    public void OpenSlotMachine()
    {
        SlotMachinePanel.OpenSlotMachine();
    }
}
