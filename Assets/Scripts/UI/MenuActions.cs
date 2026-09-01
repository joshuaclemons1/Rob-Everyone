using UnityEngine;
using UnityEngine.SceneManagement;

namespace RobEveryone.UI
{
    // Button hookups for the Main Menu scene. Buttons can't reference
    // static classes (SceneManager, Application) directly in their
    // OnClick() list -- they need a component on a scene GameObject to
    // call into, which is all this is. Drag this component's GameObject
    // into each Button's OnClick() list and pick the matching method.
    public class MenuActions : MonoBehaviour
    {
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject customizePanel;
        [SerializeField] private GameObject settingsPanel;

        public void PlayGame()
        {
            SceneManager.LoadScene("SampleScene");
        }

        public void QuitGame()
        {
            Application.Quit();
        }

        public void OpenCustomize() => SetPanel(customizePanel, true);
        public void CloseCustomize() => SetPanel(customizePanel, false);
        public void OpenSettings() => SetPanel(settingsPanel, true);
        public void CloseSettings() => SetPanel(settingsPanel, false);

        private void SetPanel(GameObject panel, bool open)
        {
            if (mainMenuPanel != null) mainMenuPanel.SetActive(!open);
            if (panel != null) panel.SetActive(open);
        }
    }
}
