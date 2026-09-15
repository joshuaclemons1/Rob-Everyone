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
    // Settings still isn't part of MenuNavigator's own horizontal
    // slide-stack (per that script's own comment) -- but it's no longer
    // a flat SetActive swap either. It now slides vertically into place
    // (see SettingsSlideRoutine below), so opening/closing Settings
    // finally reads as an animation instead of an instant cut, matching
    // the rest of the menu's animated feel.
    public class MenuActions : MonoBehaviour
    {
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private MenuNavigator menuNavigator;

        // Vertical slide for the Settings panel itself. Distance/duration
        // deliberately separate fields from the character preview's own
        // fall below -- they're two different elements moving together,
        // not the same motion reused twice.
        [SerializeField] private float settingsSlideDistance = 1200f;
        [SerializeField] private float settingsSlideDuration = 0.5f;

        // Issue #39 (3/3): the character preview falls through the
        // frame entering Settings, and falls back in from the sky
        // leaving it -- per main-menu-visual-design.md's own "Character
        // preview behavior" section. Optional on purpose (null-checked
        // throughout) -- Settings should still open/close correctly even
        // before this is wired up in the Editor.
        [SerializeField] private MenuCharacterPreview characterPreview;
        [SerializeField] private float fallDistance = 2000f;
        // Was 0.4s -- fast enough that the fall was over before it
        // registered as motion at all. 1s makes it actually readable.
        [SerializeField] private float fallDuration = 1f;

        private Coroutine fallCoroutine;
        private Vector3 previewRestLocalPosition;
        private bool previewRestCaptured;

        private RectTransform settingsRect;
        private CanvasGroup settingsGroup;
        private Vector2 settingsRestPosition;
        private bool settingsRestCaptured;
        private Coroutine settingsSlideCoroutine;

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
            if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
            PlaySettingsSlide(opening: true);
            PlayFall(fallingThrough: true);
        }

        public void CloseSettings()
        {
            if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
            PlaySettingsSlide(opening: false);
            PlayFall(fallingThrough: false);
        }

        private void PlaySettingsSlide(bool opening)
        {
            if (settingsPanel == null) return;

            if (settingsRect == null) settingsRect = settingsPanel.GetComponent<RectTransform>();
            if (settingsRect == null) return;
            if (settingsGroup == null) settingsGroup = GetOrAddCanvasGroup(settingsPanel);

            // Captured once, the first time this ever runs (same reasoning
            // as previewRestCaptured below) -- whatever position the panel
            // was authored at in the Editor is "home", regardless of what
            // that number actually is.
            if (!settingsRestCaptured)
            {
                settingsRestPosition = settingsRect.anchoredPosition;
                settingsRestCaptured = true;
            }

            if (settingsSlideCoroutine != null) StopCoroutine(settingsSlideCoroutine);
            settingsSlideCoroutine = StartCoroutine(SettingsSlideRoutine(opening));
        }

        // opening: slides up from below into its resting position.
        // !opening: slides back down out of frame, then deactivates.
        // Same ease-out-cubic curve MenuNavigator's own slide-stack uses,
        // so this reads as the same animation language rather than a
        // different one bolted on just for Settings.
        private IEnumerator SettingsSlideRoutine(bool opening)
        {
            if (opening) settingsPanel.SetActive(true);

            settingsGroup.interactable = false;
            settingsGroup.blocksRaycasts = false;

            Vector2 offScreen = settingsRestPosition + Vector2.down * settingsSlideDistance;
            Vector2 startPos = opening ? offScreen : settingsRect.anchoredPosition;
            Vector2 endPos = opening ? settingsRestPosition : offScreen;
            float startAlpha = settingsGroup.alpha;
            float endAlpha = opening ? 1f : 0f;

            float elapsed = 0f;
            while (elapsed < settingsSlideDuration)
            {
                elapsed += Time.deltaTime;
                float eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / settingsSlideDuration), 3f);
                settingsRect.anchoredPosition = Vector2.Lerp(startPos, endPos, eased);
                settingsGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, eased);
                yield return null;
            }

            settingsRect.anchoredPosition = endPos;
            settingsGroup.alpha = endAlpha;

            if (opening)
            {
                settingsGroup.interactable = true;
                settingsGroup.blocksRaycasts = true;
            }
            else
            {
                settingsPanel.SetActive(false);
            }

            settingsSlideCoroutine = null;
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

        private static CanvasGroup GetOrAddCanvasGroup(GameObject target)
        {
            CanvasGroup group = target.GetComponent<CanvasGroup>();
            if (group == null) group = target.AddComponent<CanvasGroup>();
            return group;
        }
    }
}
