using System;
using UnityEngine;

namespace RobEveryone.Customization
{
    // The player's chosen skin/color, persisted locally via PlayerPrefs --
    // static so both the customization UI and (later) networked player
    // spawning can read/write it without needing a scene reference. Stores
    // indices into whatever skin list/PlayerColorPalette the UI is
    // configured with, not raw values, so re-tuning the palette or skin
    // order later doesn't invalidate a saved choice.
    //
    // No skin unlock/purchase gating yet -- every configured skin is
    // pickable. That's planned (see gameplay-design.md's meta-progression
    // section) but not built; this only tracks *which* skin/color is
    // currently selected.
    public static class PlayerCosmeticSelection
    {
        private const string SkinIndexKey = "RobEveryone.SkinIndex";
        private const string ColorIndexKey = "RobEveryone.ColorIndex";

        public static event Action OnChanged;

        public static int SkinIndex
        {
            get => PlayerPrefs.GetInt(SkinIndexKey, 0);
            set
            {
                PlayerPrefs.SetInt(SkinIndexKey, value);
                PlayerPrefs.Save();
                OnChanged?.Invoke();
            }
        }

        public static int ColorIndex
        {
            get => PlayerPrefs.GetInt(ColorIndexKey, 0);
            set
            {
                PlayerPrefs.SetInt(ColorIndexKey, value);
                PlayerPrefs.Save();
                OnChanged?.Invoke();
            }
        }
    }
}
