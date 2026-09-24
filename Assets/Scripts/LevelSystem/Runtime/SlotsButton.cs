using UnityEngine;
using UnityEngine.UI;

namespace LegoPuzzle.Runtime
{
    /// <summary>
    /// Component attached to the SlotsButton in the main menu to open the Lotto / Slot Machine panel.
    /// Supports automatic discovery of SlotMachinePanel, animated press response, and UnityEvent hookup.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class SlotsButton : MonoBehaviour
    {
        [Tooltip("Optional direct reference to the SlotMachinePanel. If null, auto-finds in scene.")]
        [SerializeField] private SlotMachinePanel targetSlotPanel;

        [Header("Juicy Polish")]
        [Tooltip("Attach spring jelly press effect automatically")]
        [SerializeField] private bool addPressEffect = true;

        private Button button;

        private void Awake()
        {
            button = GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveListener(OnClick);
                button.onClick.AddListener(OnClick);
            }

            if (addPressEffect && GetComponent<UIButtonPressEffect>() == null)
            {
                UIButtonPressEffect.AttachTo(gameObject);
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

            SlotMachinePanel.OpenSlotMachine();
        }
    }
}
