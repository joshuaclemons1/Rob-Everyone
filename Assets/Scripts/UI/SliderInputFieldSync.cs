using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RobEveryone.UI
{
    // Keeps a Slider and a TMP_InputField showing/editing the same
    // value in sync both ways -- some settings (mouse sensitivity, FOV
    // later) want both a draggable slider AND the ability to type an
    // exact number. Owns only the sync/parsing/clamping; the actual
    // setting write happens via OnValueChanged, wired in code by
    // whichever tab controller uses this (same "small bridge" shape as
    // AccessibilityTabUI) -- a plain C# event, not a UnityEvent, so it
    // can't be wired from the Inspector.
    public class SliderInputFieldSync : MonoBehaviour
    {
        [SerializeField] private Slider slider;
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private string numberFormat = "0.##";

        public event Action<float> OnValueChanged;

        private bool updating;

        private void Awake()
        {
            slider.onValueChanged.AddListener(OnSliderChanged);
            inputField.onEndEdit.AddListener(OnFieldSubmitted);
        }

        private void OnDestroy()
        {
            slider.onValueChanged.RemoveListener(OnSliderChanged);
            inputField.onEndEdit.RemoveListener(OnFieldSubmitted);
        }

        // Sets both controls to `value` without re-triggering each other
        // or firing OnValueChanged -- call this once from the owning
        // tab's OnEnable to show the current setting.
        public void SetValueWithoutNotify(float value)
        {
            updating = true;
            slider.SetValueWithoutNotify(value);
            inputField.SetTextWithoutNotify(value.ToString(numberFormat));
            updating = false;
        }

        private void OnSliderChanged(float value)
        {
            if (updating) return;
            updating = true;
            inputField.SetTextWithoutNotify(value.ToString(numberFormat));
            updating = false;
            OnValueChanged?.Invoke(value);
        }

        private void OnFieldSubmitted(string text)
        {
            if (updating) return;

            float value = float.TryParse(text, out float parsed)
                ? Mathf.Clamp(parsed, slider.minValue, slider.maxValue)
                : slider.value; // not a number -- snap the field back to the current value rather than accepting garbage

            updating = true;
            slider.SetValueWithoutNotify(value);
            inputField.SetTextWithoutNotify(value.ToString(numberFormat));
            updating = false;
            OnValueChanged?.Invoke(value);
        }
    }
}
