using System.Collections.Generic;
using RobEveryone.Graphics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RobEveryone.UI
{
    // Builds the Graphics tab: resolution/screen-mode/quality dropdowns,
    // the FOV slider+field pair, and the VSync toggle -- all bound to
    // DisplaySettings, which already self-applies on its own
    // (DisplaySettingsApplier, Milestone D), so this component is
    // UI-only, same as every other tab controller in this doc.
    public class GraphicsTabUI : MonoBehaviour
    {
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [SerializeField] private TMP_Dropdown screenModeDropdown;
        [SerializeField] private TMP_Dropdown qualityDropdown;
        [SerializeField] private SliderInputFieldSync fovSync;
        [SerializeField] private Toggle vsyncToggle;

        private Resolution[] resolutions;
        private bool optionsBuilt;

        private void Awake()
        {
            if (fovSync != null) fovSync.OnValueChanged += SetFov;
        }

        private void OnEnable()
        {
            BuildOptions();

            if (resolutionDropdown != null)
            {
                int index = DisplaySettings.ResolutionIndex;
                // Last option in the list is "Current" (index == resolutions.Length) --
                // what -1 (the "no override" default) maps to.
                resolutionDropdown.SetValueWithoutNotify(index >= 0 && index < resolutions.Length ? index : resolutions.Length);
            }
            if (screenModeDropdown != null) screenModeDropdown.SetValueWithoutNotify((int)DisplaySettings.ScreenMode);
            if (qualityDropdown != null) qualityDropdown.SetValueWithoutNotify(DisplaySettings.QualityIndex);
            if (fovSync != null) fovSync.SetValueWithoutNotify(DisplaySettings.FieldOfView);
            if (vsyncToggle != null) vsyncToggle.SetIsOnWithoutNotify(DisplaySettings.VSync);
        }

        private void BuildOptions()
        {
            if (optionsBuilt) return;

            resolutions = Screen.resolutions;
            if (resolutionDropdown != null)
            {
                resolutionDropdown.ClearOptions();
                List<string> labels = new();
                foreach (Resolution r in resolutions)
                {
                    labels.Add($"{r.width} x {r.height} @ {Mathf.RoundToInt((float)r.refreshRateRatio.value)}Hz");
                }
                labels.Add("Current");
                resolutionDropdown.AddOptions(labels);
            }

            if (screenModeDropdown != null)
            {
                screenModeDropdown.ClearOptions();
                // Matches FullScreenMode's own enum order: 0 Exclusive
                // Fullscreen, 1 FullScreenWindow (default), 2 Maximized
                // Window, 3 Windowed.
                screenModeDropdown.AddOptions(new List<string> { "Fullscreen", "Fullscreen Window", "Maximized Window", "Windowed" });
            }

            if (qualityDropdown != null)
            {
                qualityDropdown.ClearOptions();
                qualityDropdown.AddOptions(new List<string> { "Low", "High" }); // matches Mobile_RPAsset / PC_RPAsset, indices 0/1
            }

            optionsBuilt = true;
        }

        public void SetResolution(int index)
        {
            DisplaySettings.ResolutionIndex = (resolutions != null && index >= 0 && index < resolutions.Length) ? index : -1;
        }

        public void SetScreenMode(int index) => DisplaySettings.ScreenMode = (FullScreenMode)index;
        public void SetQuality(int index) => DisplaySettings.QualityIndex = index;
        public void SetVSync(bool value) => DisplaySettings.VSync = value;

        private void SetFov(float value) => DisplaySettings.FieldOfView = value;
    }
}
