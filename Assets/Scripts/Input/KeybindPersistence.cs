using UnityEngine;
using UnityEngine.InputSystem;

namespace RobEveryone.Input
{
    // Save/load rebind overrides as one JSON blob -- same "static class
    // over PlayerPrefs" shape PlayerCosmeticSelection.cs already
    // establishes. Load() is called once from InputManager.EnsureLoaded,
    // right after the asset is enabled, so any saved rebinds apply
    // before anything reads an action this session.
    public static class KeybindPersistence
    {
        private const string OverridesKey = "RobEveryone.KeybindOverrides";

        public static void Load()
        {
            string json = PlayerPrefs.GetString(OverridesKey, "");
            if (!string.IsNullOrEmpty(json))
            {
                InputManager.Asset.LoadBindingOverridesFromJson(json);
            }
        }

        public static void Save()
        {
            PlayerPrefs.SetString(OverridesKey, InputManager.Asset.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
        }

        public static void ResetAction(InputAction action)
        {
            action.RemoveAllBindingOverrides();
            Save();
        }

        public static void ResetAll()
        {
            InputManager.Asset.RemoveAllBindingOverrides();
            Save();
        }
    }
}
