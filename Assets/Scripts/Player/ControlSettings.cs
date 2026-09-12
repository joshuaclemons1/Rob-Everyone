using System;
using UnityEngine;

namespace RobEveryone.Player
{
    public static class ControlSettings
    {
        public static event Action OnChanged;

        public static bool InvertY
        {
            get => PlayerPrefs.GetInt("RobEveryone.InvertY", 0) == 1;
            set { PlayerPrefs.SetInt("RobEveryone.InvertY", value ? 1 : 0); PlayerPrefs.Save(); OnChanged?.Invoke(); }
        }

        // Defaults ON for now -- there's no Settings UI toggle for this
        // yet (Milestone F), so defaulting it off would make the whole
        // feature untestable/invisible with no way to turn it on.
        // Revisit this default once that toggle actually exists --
        // captions defaulting off (opt-in) is the more conventional
        // choice once a player can actually choose.
        public static bool CaptionsEnabled
        {
            get => PlayerPrefs.GetInt("RobEveryone.Captions", 1) == 1;
            set { PlayerPrefs.SetInt("RobEveryone.Captions", value ? 1 : 0); PlayerPrefs.Save(); OnChanged?.Invoke(); }
        }

        public static float MouseSensitivity
        {
            get => PlayerPrefs.GetFloat("RobEveryone.MouseSensitivity", 2f);
            set { PlayerPrefs.SetFloat("RobEveryone.MouseSensitivity", value); PlayerPrefs.Save(); OnChanged?.Invoke(); }
        }
    }
}
