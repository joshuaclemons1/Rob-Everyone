using TMPro;
using UnityEngine;

namespace RobEveryone.UI
{
    // Shown by UpdateChecker when GitHub's latest release tag doesn't
    // match this build's own Application.version. Blocks the Main Menu
    // behind a dim background (same pattern as SettingsPanel/
    // InventoryScreenUI) until the player dismisses it -- "a pop-up
    // before they open the game," not a passive corner toast they could
    // miss entirely.
    public class UpdateAvailablePopupUI : MonoBehaviour
    {
        [SerializeField] private GameObject root; // dim background + panel, toggled
        [SerializeField] private TMP_Text messageText;

        private string downloadUrl;

        private void Awake()
        {
            if (root != null) root.SetActive(false);
        }

        public void Show(string newVersion, string url)
        {
            downloadUrl = url;
            if (messageText != null)
                messageText.text = $"A new version ({newVersion}) is available!\nYou're running {Application.version}.";
            if (root != null) root.SetActive(true);
        }

        // Wire the "Download" button's OnClick to this.
        public void OpenDownloadPage()
        {
            if (!string.IsNullOrEmpty(downloadUrl)) Application.OpenURL(downloadUrl);
        }

        // Wire the "Play Anyway" / close button's OnClick to this.
        public void Dismiss()
        {
            if (root != null) root.SetActive(false);
        }
    }
}
