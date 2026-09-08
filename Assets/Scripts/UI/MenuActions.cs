using RobEveryone.Core;
using UnityEngine;

namespace RobEveryone.UI
{
    // Button hookups for the Main Menu scene. Buttons can't reference
    // static classes (NetworkManager, Application) directly in their
    // OnClick() list -- they need a component on a scene GameObject to
    // call into, which is all this is. Drag this component's GameObject
    // into each Button's OnClick() list and pick the matching method.
    //
    // Networking (Stage 4): PlayGame's plain SceneManager.LoadScene is
    // gone -- entering the gameplay scene now means actually starting or
    // joining a Mirror session (Host for the player who's inviting/
    // hosting, Join for everyone connecting to them), which is what
    // spawns the Player prefab and brings the scene along with it. Wire
    // a Host button to HostGame and a Join button to JoinGame, instead of
    // a single Play button -- see stage4-multiplayer-mirror.md Part 9.
    public class MenuActions : MonoBehaviour
    {
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject customizePanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private TMPro.TMP_InputField joinAddressField;

        public void HostGame()
        {
            RobEveryoneNetworkManager.singleton.StartHost();
        }

        public void JoinGame()
        {
            string address = joinAddressField != null && !string.IsNullOrWhiteSpace(joinAddressField.text)
                ? joinAddressField.text
                : "localhost";

            RobEveryoneNetworkManager.singleton.networkAddress = address;
            RobEveryoneNetworkManager.singleton.StartClient();
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
