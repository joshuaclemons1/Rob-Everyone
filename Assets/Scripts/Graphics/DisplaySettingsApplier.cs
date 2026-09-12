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
            if (index >= 0 && index < resolutions.Length)
            {
                Resolution r = resolutions[index];
                Screen.SetResolution(r.width, r.height, DisplaySettings.ScreenMode);
            }
            else
            {
                Screen.fullScreenMode = DisplaySettings.ScreenMode;
            }

            QualitySettings.SetQualityLevel(DisplaySettings.QualityIndex, applyExpensiveChanges: true);
            QualitySettings.vSyncCount = DisplaySettings.VSync ? 1 : 0;
        }
    }
}
