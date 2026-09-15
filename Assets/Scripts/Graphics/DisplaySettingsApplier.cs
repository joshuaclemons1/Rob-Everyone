using UnityEngine;

namespace RobEveryone.Graphics
{
    // Applies DisplaySettings at startup and on every change. Self-
    // bootstraps via RuntimeInitializeOnLoadMethod -- no scene
    // placement, no Inspector wiring, same shape as AudioMixerApplier.
    public static class DisplaySettingsApplier
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            DisplaySettings.OnChanged += Apply;
            Apply();
        }

        private static void Apply()
        {
            Resolution[] resolutions = Screen.resolutions;
            int index = DisplaySettings.ResolutionIndex;
            FullScreenMode mode = DisplaySettings.ScreenMode;

            // Issue #11 fix: FullScreenWindow (borderless) always renders
            // at the OS desktop's native size -- Unity silently ignores
            // whatever width/height SetResolution is given in this mode
            // rather than actually resizing anything. Forwarding a custom
            // index into it anyway is exactly what desyncs Screen.width/
            // height (and every screen-space UI raycast built on top of
            // them, including the Settings menu's own Back button) from
            // what's actually on screen -- the "breaks the UI and can
            // softlock the game" report. Treat FullScreenWindow as always
            // "Current," same as index == -1 below, since a custom size
            // was never actually going to apply there regardless.
            if (index >= 0 && index < resolutions.Length && mode != FullScreenMode.FullScreenWindow)
            {
                Resolution r = resolutions[index];
                Screen.SetResolution(r.width, r.height, mode);
            }
            else
            {
                Screen.fullScreenMode = mode;
            }

            QualitySettings.SetQualityLevel(DisplaySettings.QualityIndex, applyExpensiveChanges: true);
            QualitySettings.vSyncCount = DisplaySettings.VSync ? 1 : 0;
        }
    }
}
