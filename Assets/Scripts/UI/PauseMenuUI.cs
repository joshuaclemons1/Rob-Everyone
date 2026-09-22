using Mirror;
using RobEveryone.Input;
using RobEveryone.Inventory;
using RobEveryone.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RobEveryone.UI
{
    // The in-game pause overlay -- same SettingsPanel content Milestone
    // F built (reused as a prefab, not rebuilt), reachable mid-round via
    // Escape. Deliberately local-only: freezes only this client's own
    // input (FirstPersonController.MenuFrozen), never touches
    // RoundManager/AI, so the round keeps running normally for every
    // other player while one player has this open.
    public class PauseMenuUI : MonoBehaviour
    {
        [SerializeField] private GameObject pausePanel;

        private FirstPersonController localController;
        private bool open;

        private void Update()
        {
            if (InventoryScreenUI.MenuOpen) return; // Tab/inventory owns Escape while it's open

            if (InputManager.UI.Cancel.WasPressedThisFrame())
            {
                SetOpen(!open);
            }
        }

        private void SetOpen(bool value)
        {
            open = value;
            pausePanel.SetActive(open);

            Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = open;

            // Resolved lazily (not in Awake) so this works regardless of
            // whether the local player has spawned yet by the time this
            // component wakes up -- by the time a player actually
            // presses Escape, they're definitely in the round.
            if (localController == null)
            {
                PlayerInventory local = PlayerInventory.LocalPlayer;
                if (local != null) localController = local.GetComponent<FirstPersonController>();
            }
            if (localController != null) localController.MenuFrozen = open;
        }

        public void OnBackButtonPressed() => SetOpen(false);

        // Wire to a "Leave Game" button added specifically for the
        // in-game pause context -- see the Back button's own pattern in
        // SettingsPanel.prefab for why this needs a scene-level override
        // rather than living in the shared prefab's default wiring: the
        // same prefab is also the Main Menu's own Settings screen, which
        // has no game to leave.
        //
        // RobEveryoneNetworkManager.offlineScene is deliberately unset
        // (see its own field), so stopping the host/client doesn't load
        // anything on its own -- explicitly load MainMenu after, same
        // target IntroSequence uses once the intro finishes.
        public void OnLeaveGameButtonPressed()
        {
            if (NetworkServer.active) NetworkManager.singleton.StopHost();
            else if (NetworkClient.isConnected) NetworkManager.singleton.StopClient();

            SceneManager.LoadScene("MainMenu");
        }
    }
}
