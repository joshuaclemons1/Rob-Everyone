using UnityEngine;

namespace RobEveryone.UI
{
    // Button hookups for the Main Menu scene. Buttons can't reference
    // static classes (NetworkManager, Application) directly in their
    // OnClick() list -- they need a component on a scene GameObject to
    // call into, which is all this is. Drag this component's GameObject
    // into each Button's OnClick() list and pick the matching method.
    //
    // Host/Join themselves live on SteamLobby now, not here -- both
    // buttons are wired directly to SteamLobby.HostLobby/OpenOverlay in
    // the Inspector (see that script's own comment). This only still
    // owns the menu-navigation actions (Settings, panel/skin swapping,
    // Quit) that have nothing to do with networking.
    //
    // Settings is the one panel still a flat SetActive swap (not part of
    // MenuNavigator's slide stack) -- see that script's own comment for
    // why, per main-menu-visual-design.md.
    public class MenuActions : MonoBehaviour
    {
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private MenuNavigator menuNavigator;

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
