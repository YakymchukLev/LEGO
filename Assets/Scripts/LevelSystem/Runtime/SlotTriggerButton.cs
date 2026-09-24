using UnityEngine;
using UnityEngine.UI;

namespace LegoPuzzle.Runtime
{
    /// <summary>
    /// Attach this component to any UI Button to trigger opening the Lotto / Slot Machine modal.
    /// Works out-of-the-box in any scene; automatically discovers or instantiates the SlotMachinePanel if needed.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class SlotTriggerButton : MonoBehaviour
    {
        [Tooltip("Optional explicit reference to the SlotMachinePanel. If null, auto-finds or creates on Canvas.")]
        [SerializeField] private SlotMachinePanel targetSlotPanel;

        private Button button;

        private void Awake()
        {
            button = GetComponent<Button>();
            if (button != null)
            {
                button.onClick.AddListener(OnClick);
            }
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(OnClick);
            }
        }

        public void OnClick()
        {
            if (targetSlotPanel != null)
            {
                targetSlotPanel.Open();
                return;
            }

            if (SlotMachinePanel.Instance != null)
            {
                SlotMachinePanel.Instance.Open();
                return;
            }

            var existing = FindAnyObjectByType<SlotMachinePanel>(FindObjectsInactive.Include);
            if (existing != null)
            {
                targetSlotPanel = existing;
                existing.Open();
                return;
            }

            // Auto-create on the nearest active Canvas
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindAnyObjectByType<Canvas>();
            if (canvas != null)
            {
                GameObject panelObj = new GameObject("SlotMachinePanel");
                panelObj.transform.SetParent(canvas.transform, false);
                targetSlotPanel = panelObj.AddComponent<SlotMachinePanel>();
                targetSlotPanel.Open();
            }
            else
            {
                Debug.LogWarning("[SlotTriggerButton] Could not find Canvas in scene to create SlotMachinePanel.");
            }
        }
    }
}
