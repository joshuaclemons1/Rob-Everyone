using UnityEngine;
using UnityEngine.Audio;

namespace RobEveryone.Audio
{
    // Translates AudioSettings' 0-1 linear values into the mixer's dB
    // scale (a mixer's exposed volume param is logarithmic, not linear
    // -- a straight 0-1 slider feeding SetFloat directly would make most
    // of the slider's range sound like "basically silent" or "basically
    // max"). Self-bootstraps at startup the same way InputManager does
    // -- no scene placement, no Inspector wiring -- by loading the
    // mixer from Resources (Editor work: create Assets/Resources/
    // MainMixer.mixer with Master/Music/SFX/Voice groups, each Volume
    // parameter exposed with these exact names).
    public static class AudioMixerApplier
    {
        private static AudioMixer mixer;
        private static bool subscribed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            mixer = Resources.Load<AudioMixer>("MainMixer");
            if (mixer == null)
            {
                Debug.LogWarning("[AudioMixerApplier] No Assets/Resources/MainMixer.mixer found yet -- volume settings are inert until it's created (see settings-menu-setup.md Milestone C).");
                return;
            }

            if (!subscribed)
            {
                AudioSettings.OnChanged += ApplyAll;
                subscribed = true;
            }
            ApplyAll();
        }

        private static void ApplyAll()
        {
            Apply("MasterVolume", AudioSettings.MasterVolume);
            Apply("MusicVolume", AudioSettings.MusicVolume);
            Apply("SFXVolume", AudioSettings.SFXVolume);
            Apply("VoiceVolume", AudioSettings.VoiceVolume);
        }

        private static void Apply(string exposedParam, float linear)
        {
            float dB = linear <= 0.0001f ? -80f : Mathf.Log10(linear) * 20f;
            mixer.SetFloat(exposedParam, dB);
        }
    }
}
