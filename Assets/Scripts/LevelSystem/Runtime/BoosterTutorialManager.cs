using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace LegoPuzzle.Runtime
{
    public class BoosterTutorialManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private LevelLoader levelLoader;
        
        [Header("Boosters")]
        [SerializeField] private TimeFreezeBooster timeFreezeBooster;
        [SerializeField] private HintBooster hintBooster;
        [SerializeField] private HammerBooster hammerBooster;

        private void Awake()
        {
            if (levelLoader == null) levelLoader = FindAnyObjectByType<LevelLoader>();
            if (timeFreezeBooster == null) timeFreezeBooster = FindAnyObjectByType<TimeFreezeBooster>(FindObjectsInactive.Include);
            if (hintBooster == null) hintBooster = FindAnyObjectByType<HintBooster>(FindObjectsInactive.Include);
            if (hammerBooster == null) hammerBooster = FindAnyObjectByType<HammerBooster>(FindObjectsInactive.Include);
        }

        private void OnEnable()
        {
            if (levelLoader != null)
            {
                levelLoader.OnLevelLoaded += HandleLevelLoaded;
            }
        }

        private void OnDisable()
        {
            if (levelLoader != null)
            {
                levelLoader.OnLevelLoaded -= HandleLevelLoaded;
            }
            IsBoosterTutorialActive = false;
        }

        public static bool IsBoosterTutorialActive { get; private set; }

        private void HandleLevelLoaded(int levelIndex)
        {
            // Якщо це рівень з туторіалом бустера (2, 3 або 4), блокування активується миттєво
            IsBoosterTutorialActive = (levelIndex >= 2 && levelIndex <= 4);
            StartCoroutine(UpdateBoostersRoutine(levelIndex));
        }

        private IEnumerator UpdateBoostersRoutine(int levelIndex)
        {
            yield return new WaitForEndOfFrame();

            if (timeFreezeBooster == null) timeFreezeBooster = FindAnyObjectByType<TimeFreezeBooster>(FindObjectsInactive.Include);
            if (hintBooster == null) hintBooster = FindAnyObjectByType<HintBooster>(FindObjectsInactive.Include);
            if (hammerBooster == null) hammerBooster = FindAnyObjectByType<HammerBooster>(FindObjectsInactive.Include);

            bool freezeEnabled = levelIndex >= 2;
            bool hintEnabled = levelIndex >= 3;
            bool hammerEnabled = levelIndex >= 4; 

            if (timeFreezeBooster != null)
            {
                var btn = timeFreezeBooster.GetComponent<Button>();
                if (btn != null) btn.gameObject.SetActive(freezeEnabled);
            }

            if (hintBooster != null)
            {
                var btn = hintBooster.GetComponent<Button>();
                if (btn != null) btn.gameObject.SetActive(hintEnabled);
            }

            if (hammerBooster != null)
            {
                var btn = hammerBooster.GetComponent<Button>();
                if (btn != null) btn.gameObject.SetActive(hammerEnabled);
            }

            // Туторіали
            GameObject handPrefab = levelLoader != null ? levelLoader.TutorialHandPrefab : null;

            if (levelIndex == 2 && timeFreezeBooster != null)
            {
                IsBoosterTutorialActive = true;
                var btn = timeFreezeBooster.GetComponent<Button>();
                if (btn != null)
                {
                    TutorialHandEffect.ShowUI(btn.GetComponent<RectTransform>(), handPrefab);
                    btn.onClick.RemoveListener(OnTutorialBoosterClicked);
                    btn.onClick.AddListener(OnTutorialBoosterClicked);
                }
            }
            else if (levelIndex == 3 && hintBooster != null)
            {
                IsBoosterTutorialActive = true;
                var btn = hintBooster.GetComponent<Button>();
                if (btn != null)
                {
                    TutorialHandEffect.ShowUI(btn.GetComponent<RectTransform>(), handPrefab);
                    btn.onClick.RemoveListener(OnTutorialBoosterClicked);
                    btn.onClick.AddListener(OnTutorialBoosterClicked);
                }
            }
            else if (levelIndex == 4 && hammerBooster != null)
            {
                IsBoosterTutorialActive = true;
                var btn = hammerBooster.GetComponent<Button>();
                if (btn != null)
                {
                    TutorialHandEffect.ShowUI(btn.GetComponent<RectTransform>(), handPrefab);
                    btn.onClick.RemoveListener(OnTutorialBoosterClicked);
                    btn.onClick.AddListener(OnTutorialBoosterClicked);
                }
            }
            else
            {
                IsBoosterTutorialActive = false;
            }
        }

        private void OnTutorialBoosterClicked()
        {
            IsBoosterTutorialActive = false;
            TutorialHandEffect.Dismiss();
        }
    }
}
