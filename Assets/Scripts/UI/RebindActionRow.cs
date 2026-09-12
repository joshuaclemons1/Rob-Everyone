using RobEveryone.Input;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RobEveryone.UI
{
    // One row per rebindable Gameplay action, cloned from a template the
    // same way CustomizationUI's color swatches are (see
    // SettingsPanelController.BuildRebindRows, Milestone F) -- each
    // clone carries its own `action` via Bind, so the Rebind/Reset
    // buttons on this same prefab can stay Inspector-wired to this
    // component's own public methods rather than needing a closure.
    public class RebindActionRow : MonoBehaviour
    {
        [SerializeField] private TMP_Text actionLabel;
        [SerializeField] private TMP_Text bindingLabel;
        [SerializeField] private GameObject waitingForInputIndicator;

        private InputAction action;
        private InputActionRebindingExtensions.RebindingOperation activeRebind;

        public void Bind(InputAction targetAction, string displayName)
        {
            action = targetAction;
            if (actionLabel != null) actionLabel.text = displayName;
            RefreshBindingLabel();
        }

        private void OnDestroy() => activeRebind?.Dispose();

        public void OnRebindButtonPressed()
        {
            if (action == null || activeRebind != null) return; // already mid-rebind

            if (waitingForInputIndicator != null) waitingForInputIndicator.SetActive(true);
            action.Disable();

            activeRebind = action.PerformInteractiveRebinding()
                .WithControlsExcluding("Mouse/position")
                .WithControlsExcluding("Mouse/delta")
                .OnMatchWaitForAnother(0.1f)
                .OnComplete(_ => FinishRebind())
                .OnCancel(_ => FinishRebind())
                .Start();
        }

        private void FinishRebind()
        {
            activeRebind.Dispose();
            activeRebind = null;
            action.Enable();
            if (waitingForInputIndicator != null) waitingForInputIndicator.SetActive(false);
            RefreshBindingLabel();
            KeybindPersistence.Save();
        }

        public void OnResetButtonPressed()
        {
            if (action == null) return;
            KeybindPersistence.ResetAction(action);
            RefreshBindingLabel();
        }

        private void RefreshBindingLabel()
        {
            if (bindingLabel != null && action != null) bindingLabel.text = action.GetBindingDisplayString();
        }
    }
}
