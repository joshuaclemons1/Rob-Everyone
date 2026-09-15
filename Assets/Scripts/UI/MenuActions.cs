using System.Collections;
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

        // Issue #39 (3/3): the character preview falls through the
        // frame entering Settings, and falls back in from the sky
        // leaving it -- per main-menu-visual-design.md's own "Character
        // preview behavior" section. Optional on purpose (null-checked
        // throughout) -- Settings should still open/close correctly even
        // before this is wired up in the Editor.
        [SerializeField] private MenuCharacterPreview characterPreview;
        [SerializeField] private float fallDistance = 2000f;
        [SerializeField] private float fallDuration = 0.4f;

        private Coroutine fallCoroutine;
        private Vector3 previewRestLocalPosition;
        private bool previewRestCaptured;

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

        public void OpenSettings()
        {
            SetPanel(settingsPanel, true);
            PlayFall(fallingThrough: true);
        }

        public void CloseSettings()
        {
            SetPanel(settingsPanel, false);
            PlayFall(fallingThrough: false);
        }

        private void SetPanel(GameObject panel, bool open)
        {
            if (mainMenuPanel != null) mainMenuPanel.SetActive(!open);
            if (panel != null) panel.SetActive(open);
        }

        private void PlayFall(bool fallingThrough)
        {
            if (characterPreview == null || characterPreview.PreviewTransform == null) return;

            if (!previewRestCaptured)
            {
                // Captured once, the first time this ever runs, rather
                // than in Awake -- PreviewTransform's own local position
                // isn't guaranteed set up yet that early (MenuCharacterPreview
                // builds its stage in its own Awake, and Unity doesn't
                // guarantee ordering between two different components'
                // Awake calls), but by the time a player has actually
                // clicked Settings once, it's had every chance to finish.
                previewRestLocalPosition = characterPreview.PreviewTransform.localPosition;
                previewRestCaptured = true;
            }

            if (fallCoroutine != null) StopCoroutine(fallCoroutine);
            fallCoroutine = StartCoroutine(FallRoutine(fallingThrough));
        }

        // fallingThrough: drops straight down out of frame (entering
        // Settings). !fallingThrough: drops in from above back to rest
        // (leaving Settings) -- same distance/duration either way, just
        // which end of the fall it starts/ends at, so the two always
        // look like mirrors of each other.
        private IEnumerator FallRoutine(bool fallingThrough)
        {
            Transform preview = characterPreview.PreviewTransform;
            Vector3 start = fallingThrough ? previewRestLocalPosition : previewRestLocalPosition + Vector3.down * fallDistance;
            Vector3 end = fallingThrough ? previewRestLocalPosition + Vector3.down * fallDistance : previewRestLocalPosition;

            // Falling through starts slow and accelerates (ease-in, like
            // gravity taking hold); falling from the sky starts fast and
            // settles (ease-out, like landing) -- the same asymmetry a
            // real fall/landing has, rather than one curve doing double
            // duty for both directions.
            float elapsed = 0f;
            while (elapsed < fallDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fallDuration);
                float eased = fallingThrough ? t * t : 1f - Mathf.Pow(1f - t, 3f);
                preview.localPosition = Vector3.Lerp(start, end, eased);
                yield return null;
            }

            preview.localPosition = end;
            fallCoroutine = null;
        }
    }
}
