using RobEveryone.Player;
using UnityEngine;
using UnityEngine.UI;

namespace RobEveryone.UI
{
    // Binds the Accessibility tab's two toggles to ControlSettings --
    // Unity's Inspector can't wire a UnityEvent directly to a static
    // property setter, so this is the small instance-method bridge for
    // that. Reads the current values back in OnEnable so reopening
    // Settings shows what's actually set, not just each Toggle's own
    // serialized default.
    public class AccessibilityTabUI : MonoBehaviour
    {
        [SerializeField] private Toggle invertYToggle;
        [SerializeField] private Toggle captionsToggle;

        private void OnEnable()
        {
            if (invertYToggle != null) invertYToggle.SetIsOnWithoutNotify(ControlSettings.InvertY);
            if (captionsToggle != null) captionsToggle.SetIsOnWithoutNotify(ControlSettings.CaptionsEnabled);
        }

        public void SetInvertY(bool value) => ControlSettings.InvertY = value;
        public void SetCaptionsEnabled(bool value) => ControlSettings.CaptionsEnabled = value;
    }
}
