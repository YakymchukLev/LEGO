using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LegoPuzzle.Runtime
{
    /// <summary>
    /// Attach to any UI Button to guarantee click feedback audio (Button_Click.wav).
    /// Works with PointerClick (touch / mouse) and respects Button.interactable.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Selectable))]
    public class UIButtonAudioClick : MonoBehaviour, IPointerClickHandler
    {
        private Selectable selectable;

        private void Awake()
        {
            selectable = GetComponent<Selectable>();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (selectable != null && !selectable.IsInteractable()) return;

            if (GameSettingsManager.HasInstance)
            {
                GameSettingsManager.Instance.PlayClickSound();
            }
        }

        /// <summary>
        /// Helper method to attach UIButtonAudioClick to a specific GameObject.
        /// </summary>
        public static UIButtonAudioClick AttachTo(GameObject target)
        {
            if (target == null) return null;
            var click = target.GetComponent<UIButtonAudioClick>();
            if (click == null)
            {
                click = target.AddComponent<UIButtonAudioClick>();
            }
            return click;
        }

        /// <summary>
        /// Helper method to attach UIButtonAudioClick to all Buttons in a hierarchy.
        /// </summary>
        public static void AttachToAllIn(GameObject root)
        {
            if (root == null) return;
            var buttons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null)
                {
                    AttachTo(buttons[i].gameObject);
                }
            }
        }
    }
}
