using RobEveryone.Input;
using RobEveryone.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RobEveryone.UI
{
    // Builds the Controls tab: one RebindActionRow per rebindable
    // Gameplay action (built once, on first enable) and the mouse-
    // sensitivity slider+field pair.
    public class ControlsTabUI : MonoBehaviour
    {
        [SerializeField] private Transform rebindRowContainer;
        [SerializeField] private RebindActionRow rebindRowTemplate;
        [SerializeField] private SliderInputFieldSync sensitivitySync;

        private bool rowsBuilt;

        private void Awake()
        {
            if (sensitivitySync != null) sensitivitySync.OnValueChanged += SetMouseSensitivity;
        }

        private void OnEnable()
        {
            BuildRebindRows();
            if (sensitivitySync != null) sensitivitySync.SetValueWithoutNotify(ControlSettings.MouseSensitivity);
        }

        // Not Move/Look (axis composites, not meaningfully single-key
        // rebindable the same way), PrimaryAction/SecondaryAction
        // (shared mouse buttons across several mutually-exclusive
        // systems -- see settings-menu-setup.md Milestone A), Hotbar1-5
        // (digit keys, a dedicated 1-5 remap UI is more than this needs
        // right now), or DebugSpectate (temporary dev-only binding).
        private void BuildRebindRows()
        {
            if (rowsBuilt) return;
            if (rebindRowContainer == null || rebindRowTemplate == null) return;

            AddRow(InputManager.Gameplay.Interact, "Interact");
            AddRow(InputManager.Gameplay.SetDown, "Set Down");
            AddRow(InputManager.Gameplay.DropItem, "Drop Item");
            AddRow(InputManager.Gameplay.ToggleInventory, "Toggle Inventory");
            AddRow(InputManager.Gameplay.PushToTalk, "Push To Talk");
            AddRow(InputManager.Gameplay.Sprint, "Sprint");
            AddRow(InputManager.Gameplay.Crouch, "Crouch");
            AddRow(InputManager.Gameplay.Jump, "Jump");

            rowsBuilt = true;
        }

        private void AddRow(InputAction action, string displayName)
        {
            RebindActionRow row = Instantiate(rebindRowTemplate, rebindRowContainer);
            row.gameObject.SetActive(true);
            row.Bind(action, displayName);
        }

        private void SetMouseSensitivity(float value) => ControlSettings.MouseSensitivity = value;

        public void ResetAllKeybinds() => KeybindPersistence.ResetAll();
    }
}
