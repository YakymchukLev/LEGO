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
            if (!SlotsButton.IsSlotsUnlocked())
            {
                SlotsButton.ShowLockedFloatingHint();
                return;
            }

            if (targetSlotPanel != null)
            {
                targetSlotPanel.Open();
                return;
            }

            SlotMachinePanel.OpenSlotMachine();
        }
    }
}
