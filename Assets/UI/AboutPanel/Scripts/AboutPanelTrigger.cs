using UnityEngine;
using UnityEngine.UI;

namespace ARSafe.UI
{
    /// <summary>
    /// Simple bridge between a uGUI button and the UI Toolkit AboutPanelController.
    /// Attach to any Main Menu button and assign the AboutPanelController reference.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class AboutPanelTrigger : MonoBehaviour
    {
        [SerializeField] private AboutPanelController aboutPanel;
        [SerializeField] private bool toggleInsteadOfShow;

        private Button button;

        private void Awake()
        {
            button = GetComponent<Button>();
        }

        private void OnEnable()
        {
            if (button != null)
            {
                button.onClick.AddListener(HandleButtonClicked);
            }
        }

        private void OnDisable()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(HandleButtonClicked);
            }
        }

        private void HandleButtonClicked()
        {
            if (aboutPanel == null)
            {
                Debug.LogWarning("[AboutPanelTrigger] AboutPanelController reference missing.", this);
                return;
            }

            if (toggleInsteadOfShow)
            {
                aboutPanel.Toggle();
            }
            else
            {
                aboutPanel.Show();
            }
        }
    }
}
