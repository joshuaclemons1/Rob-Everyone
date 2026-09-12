using UnityEngine;

namespace RobEveryone.UI
{
    // Drives tab switching for the real SettingsPanel -- one content
    // container shown at a time, same flat show/hide idiom MenuActions
    // already uses (Settings is its own full-screen branch, not part of
    // MenuNavigator's slide stack, per main-menu-visual-design.md).
    // This same component/panel gets reused as-is by the in-game pause
    // overlay (Milestone G) -- settings content is authored once.
    public class SettingsPanelController : MonoBehaviour
    {
        // Audio, Controls, Graphics, Accessibility, in that order --
        // index matches each tab button's wired ShowTab(int) argument.
        [SerializeField] private GameObject[] tabContents;

        private void OnEnable() => ShowTab(0);

        public void ShowTab(int index)
        {
            for (int i = 0; i < tabContents.Length; i++)
            {
                if (tabContents[i] != null) tabContents[i].SetActive(i == index);
            }
        }
    }
}
