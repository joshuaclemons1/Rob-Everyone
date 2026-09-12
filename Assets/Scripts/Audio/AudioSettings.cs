using System;
using UnityEngine;

namespace RobEveryone.Audio
{
    // Master/Music/SFX/Voice volumes, 0-1 linear (UI-friendly) --
    // AudioMixerApplier converts to the mixer's dB scale. Same
    // "static class over PlayerPrefs, OnChanged event" shape
    // PlayerCosmeticSelection.cs already establishes.
    public static class AudioSettings
    {
        public static event Action OnChanged;

        public static float MasterVolume { get => Get("Master"); set => Set("Master", value); }
        public static float MusicVolume { get => Get("Music"); set => Set("Music", value); }
        public static float SFXVolume { get => Get("SFX"); set => Set("SFX", value); }
        public static float VoiceVolume { get => Get("Voice"); set => Set("Voice", value); }

        private static float Get(string key) => PlayerPrefs.GetFloat($"RobEveryone.Volume.{key}", 1f);

        private static void Set(string key, float value)
        {
            PlayerPrefs.SetFloat($"RobEveryone.Volume.{key}", value);
            PlayerPrefs.Save();
            OnChanged?.Invoke();
        }
    }
}
