using UnityEngine;
using UnityEngine.InputSystem;

namespace RobEveryone.Input
{
    // Central owner of the one RobEveryoneControls.inputactions asset
    // every migrated script reads through, instead of each constructing/
    // enabling its own copy (which would silently fight over which one
    // is "enabled"). Self-bootstraps the first time anything asks for
    // Gameplay/UI -- same "no manual scene placement needed" shape as
    // SteamManager's own lazy Instance getter -- by loading the asset
    // from Resources rather than needing an Inspector-wired reference on
    // a hand-placed GameObject.
    //
    // Every action is resolved once here, not looked up by name at each
    // call site every frame, so migrated scripts get the same ergonomics
    // a generated wrapper class would give (InputManager.Gameplay.Move)
    // without actually depending on Unity's C# code generator having run
    // -- this project's InputActionAsset has "Generate C# Class" off.
    public static class InputManager
    {
        private static InputActionAsset asset;
        private static GameplayActions gameplay;
        private static UiActions ui;

        public static GameplayActions Gameplay
        {
            get { EnsureLoaded(); return gameplay; }
        }

        public static UiActions UI
        {
            get { EnsureLoaded(); return ui; }
        }

        // Whole-asset access for Milestone B's KeybindPersistence
        // (SaveBindingOverridesAsJson/LoadBindingOverridesFromJson work
        // on the asset as a whole, not per-map).
        public static InputActionAsset Asset
        {
            get { EnsureLoaded(); return asset; }
        }

        private static void EnsureLoaded()
        {
            if (asset != null) return;

            asset = Resources.Load<InputActionAsset>("RobEveryoneControls");
            asset.Enable();
            KeybindPersistence.Load();

            gameplay = new GameplayActions(asset.FindActionMap("Gameplay", throwIfNotFound: true));
            ui = new UiActions(asset.FindActionMap("UI", throwIfNotFound: true));
        }

        public class GameplayActions
        {
            public InputAction Move { get; }
            public InputAction Look { get; }
            public InputAction Sprint { get; }
            public InputAction Crouch { get; }
            public InputAction Jump { get; }
            public InputAction Interact { get; }
            public InputAction SetDown { get; }
            public InputAction DropItem { get; }
            public InputAction ToggleInventory { get; }
            public InputAction DebugSpectate { get; }
            public InputAction PushToTalk { get; }
            public InputAction PrimaryAction { get; }
            public InputAction SecondaryAction { get; }
            public InputAction Scroll { get; }

            // Matches PlayerInventory.SlotCount (5) -- kept as a plain
            // array here rather than referencing that class, since Input
            // shouldn't need a dependency on Inventory just for a count.
            private readonly InputAction[] hotbar = new InputAction[5];

            public InputAction Hotbar(int index) => hotbar[index];

            public GameplayActions(InputActionMap map)
            {
                Move = map.FindAction("Move", throwIfNotFound: true);
                Look = map.FindAction("Look", throwIfNotFound: true);
                Sprint = map.FindAction("Sprint", throwIfNotFound: true);
                Crouch = map.FindAction("Crouch", throwIfNotFound: true);
                Jump = map.FindAction("Jump", throwIfNotFound: true);
                Interact = map.FindAction("Interact", throwIfNotFound: true);
                SetDown = map.FindAction("SetDown", throwIfNotFound: true);
                DropItem = map.FindAction("DropItem", throwIfNotFound: true);
                ToggleInventory = map.FindAction("ToggleInventory", throwIfNotFound: true);
                DebugSpectate = map.FindAction("DebugSpectate", throwIfNotFound: true);
                PushToTalk = map.FindAction("PushToTalk", throwIfNotFound: true);
                PrimaryAction = map.FindAction("PrimaryAction", throwIfNotFound: true);
                SecondaryAction = map.FindAction("SecondaryAction", throwIfNotFound: true);
                Scroll = map.FindAction("Scroll", throwIfNotFound: true);

                for (int i = 0; i < hotbar.Length; i++)
                {
                    hotbar[i] = map.FindAction($"Hotbar{i + 1}", throwIfNotFound: true);
                }
            }
        }

        public class UiActions
        {
            public InputAction Cancel { get; }

            public UiActions(InputActionMap map)
            {
                Cancel = map.FindAction("Cancel", throwIfNotFound: true);
            }
        }
    }
}
