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
    // spawns the Player prefab and brings the scene along with it. Host/
    // Join now live on the Play submenu panel (see MenuNavigator and
    // main-menu-customization-setup.md's Play submenu section), reached
    // by sliding in from Main rather than a flat panel swap -- this
    // script no longer owns that navigation itself, only the actions a
    // button inside one of those panels can trigger.
    //
    // Settings is the one panel still a flat SetActive swap (not part of
    // MenuNavigator's slide stack) -- see that script's own comment for
    // why, per main-menu-visual-design.md.
    public class MenuActions : MonoBehaviour
    {
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private TMPro.TMP_InputField joinAddressField;
        [SerializeField] private MenuNavigator menuNavigator;

        // Only the "Join" label text swaps for the input field -- the
        // button itself (brackets, background, Button component/click
        // area) stays put the whole time; BeginJoin just hides the label
        // and shows/focuses the field in the same spot inside it.
        // ConfirmJoin (wired to the input field's own On End Edit, which
        // fires on Enter *or* clicking away) both actually joins and
        // swaps the label back on.
        [SerializeField] private GameObject joinLabelText;
        [SerializeField] private GameObject joinInputFieldObject;

        private void Awake()
        {
            // Registers Main Menu as MenuNavigator's starting panel so it
            // already knows "current" before your first click, instead of
            // only finding out the first time NavigateTo/NavigateBack is
            // called -- see main-menu-customization-setup.md's Part 13
            // step 5.
            if (menuNavigator != null && mainMenuPanel != null)
            {
                menuNavigator.SetInitial(mainMenuPanel.GetComponent<RectTransform>());
            }
        }

        public void HostGame()
        {
            RobEveryoneNetworkManager.singleton.StartHost();
        }

        // Wire the Join button's OnClick to this instead of JoinGame
        // directly -- swaps its label text for the input field and
        // focuses it, so the player can start typing immediately.
        public void BeginJoin()
        {
            if (joinLabelText != null) joinLabelText.SetActive(false);

            if (joinInputFieldObject != null)
            {
                joinInputFieldObject.SetActive(true);
            }

            if (joinAddressField != null)
            {
                joinAddressField.text = string.Empty;
                joinAddressField.Select();
                joinAddressField.ActivateInputField();
            }
        }

        // Wire the input field's On End Edit (fires on Enter, or on
        // clicking/tabbing away) to this instead of JoinGame directly --
        // actually connects, then swaps the label text back on so a
        // failed/cancelled attempt doesn't leave the field stuck open.
        public void ConfirmJoin(string _)
        {
            JoinGame();

            if (joinInputFieldObject != null) joinInputFieldObject.SetActive(false);
            if (joinLabelText != null) joinLabelText.SetActive(true);
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

        public void OpenSettings() => SetPanel(settingsPanel, true);
        public void CloseSettings() => SetPanel(settingsPanel, false);

        private void SetPanel(GameObject panel, bool open)
        {
            if (mainMenuPanel != null) mainMenuPanel.SetActive(!open);
            if (panel != null) panel.SetActive(open);
        }
    }
}
