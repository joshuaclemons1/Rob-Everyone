using System;
using UnityEngine;

namespace RobEveryone.Graphics
{
    // Resolution, screen mode, quality preset, FOV, VSync -- same
    // "static class over PlayerPrefs, OnChanged event" shape every other
    // settings class in this project uses.
    public static class DisplaySettings
    {
        public static event Action OnChanged;

        public static int ResolutionIndex // -1 = current/native, resolved at apply time
        {
            get => PlayerPrefs.GetInt("RobEveryone.ResolutionIndex", -1);
            set { PlayerPrefs.SetInt("RobEveryone.ResolutionIndex", value); Save(); }
        }

        public static FullScreenMode ScreenMode
        {
            get => (FullScreenMode)PlayerPrefs.GetInt("RobEveryone.ScreenMode", (int)FullScreenMode.FullScreenWindow);
            set { PlayerPrefs.SetInt("RobEveryone.ScreenMode", (int)value); Save(); }
        }

        // 0 = Low (Mobile_RPAsset), 1 = High (PC_RPAsset) -- matches the
        // two tiers already set up in Project Settings > Quality.
        public static int QualityIndex
        {
            get => PlayerPrefs.GetInt("RobEveryone.QualityIndex", 1);
            set { PlayerPrefs.SetInt("RobEveryone.QualityIndex", value); Save(); }
        }

        public static float FieldOfView
        {
            get => PlayerPrefs.GetFloat("RobEveryone.FOV", 75f);
            set { PlayerPrefs.SetFloat("RobEveryone.FOV", value); Save(); }
        }

        public static bool VSync
        {
            get => PlayerPrefs.GetInt("RobEveryone.VSync", 1) == 1;
            set { PlayerPrefs.SetInt("RobEveryone.VSync", value ? 1 : 0); Save(); }
        }

        private static void Save()
        {
            PlayerPrefs.Save();
            OnChanged?.Invoke();
        }

        // Issue #11 failsafe -- see DisplaySettingsResetHotkey. Only
        // touches the two settings actually implicated in the
        // resolution/screen-mode softlock (DisplaySettingsApplier's own
        // comment on FullScreenWindow silently ignoring a requested
        // size); leaves quality/VSync/FOV alone since a stuck display
        // isn't caused by those.
        public static void ResetToSafeDefaults()
        {
            ResolutionIndex = -1;
            ScreenMode = FullScreenMode.FullScreenWindow;
        }
    }
}
