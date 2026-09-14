using UnityEngine;
using UnityEngine.InputSystem;

namespace RobEveryone.Graphics
{
    // Issue #11 failsafe. A bad resolution/screen-mode combo (see
    // DisplaySettingsApplier's own comment on FullScreenWindow silently
    // ignoring a requested size) can desync the actual rendered window
    // from Screen.width/height badly enough that every screen-space UI
    // raycast -- including the Settings menu's own Back button -- stops
    // landing where it visually appears to. That's a real softlock: the
    // player has no working UI left to undo the change with.
    //
    // F9, unbound anywhere in RobEveryoneControls.inputactions, polled
    // directly off the raw device the same way IntroSequence already
    // does rather than through InputManager/the Gameplay action map --
    // this has to work regardless of whatever's currently broken on
    // screen or which action map (if any) is even active, so it
    // deliberately doesn't depend on either.
    //
    // Self-bootstraps via RuntimeInitializeOnLoadMethod, same pattern as
    // DisplaySettingsApplier -- no scene placement, so it can never be
    // the one thing missing when a player actually needs it.
    public static class DisplaySettingsResetHotkey
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            GameObject host = new("DisplaySettingsResetHotkey");
            Object.DontDestroyOnLoad(host);
            host.AddComponent<Pump>();
        }

        // RuntimeInitializeOnLoadMethod itself can't get a per-frame
        // callback -- needs a live component on a persistent GameObject
        // for that, same reason DisplaySettingsApplier can get away
        // without one (it only ever needs to run once per change, not
        // every frame).
        private class Pump : MonoBehaviour
        {
            private void Update()
            {
                if (Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame)
                {
                    DisplaySettings.ResetToSafeDefaults();
                }
            }
        }
    }
}
