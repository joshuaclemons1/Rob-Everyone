# Settings menu — full build plan

Not part of `plan.md`'s original build order — this is a new cross-
cutting feature, same standalone-doc treatment as `voip-setup.md`. The
menu *shell* already exists: `MainMenu.unity` has a working
`SettingsPanel` stub (`Back` button + a "Settings coming soon" label),
opened/closed via `MenuActions.OpenSettings()/CloseSettings()`
(`Assets/Scripts/UI/MenuActions.cs:108-115`), per
`main-menu-customization-setup.md` Part 10 and the screen-behavior spec
in `main-menu-visual-design.md` (full-screen branch off Main, character
preview falls through/in). **Nothing behind that panel exists yet** —
no audio mixer, no resolution/quality control, no keybind remapping, no
accessibility toggles. This plan builds all of it into that same panel,
then reuses the same content for a new in-game pause overlay.

## Design decisions (settled with the user before writing this doc)

- **All four categories, v1**: Audio, Controls, Graphics/Display,
  Gameplay/Accessibility.
- **Audio**: full mixer — Master, Music, SFX, Voice groups, each with
  its own slider. Requires creating a real `AudioMixer` asset and
  routing every existing `AudioSource` into a group (none are grouped
  today). Also finally builds the mute/per-player-volume system
  `voip-setup.md` Part 5 sketched but never implemented.
- **Controls**: full rebindable-keybind screen on Unity's **new Input
  System** (the package is installed, `Assets/InputSystem_Actions.
  inputactions` exists, but every gameplay script still polls
  `Keyboard.current`/`Mouse.current` directly — confirmed by grep, zero
  exceptions). This is a real migration, not additive.
- **Migration sequencing**: the Input System migration is its **own
  isolated milestone (A)**, built and playtested with *identical
  default bindings* before any rebind UI, audio, graphics, or
  accessibility work lands on top — deliberately de-risked, since it
  touches nearly every input-handling script in the project.
- **Graphics**: resolution + fullscreen/windowed/borderless, quality
  preset (Low/High, mapped onto the two URP tiers — `Mobile_RPAsset`/
  `PC_RPAsset` — that already exist in Project Settings but nothing
  in-game reads today), FOV slider (new field, doesn't exist), VSync
  toggle.
- **Accessibility**: invert-Y look, and a voice-chat captions/name-tag
  HUD ("PlayerName is talking") as a supplemental cue alongside the
  existing `PlayerHeadTalkScale` pulse, for players who are hard of
  hearing or playing with sound off.
- **Pause behavior**: opening the in-game overlay is **local-only** —
  it does not pause `RoundManager`'s timer, AI, or any other client.
  Standard for a real-time competitive multiplayer game; stops one
  player's menu from griefing everyone else's round.
- **Reach**: Main Menu (existing `SettingsPanel`) **and** a new in-game
  pause overlay, sharing the same settings content rather than building
  it twice.

---

## Milestone A — Input System migration (foundation, no behavior change) — ✅ done, awaiting playtest confirmation

**Goal:** every script that currently polls `Keyboard.current`/
`Mouse.current` directly instead reads from a generated Input Actions
wrapper, with the *exact same default bindings as today*. No rebind UI,
no new keys, nothing should feel different to play — this milestone is
purely "swap the plumbing," verified by the fact that nothing changed.

**Confirmed current polling sites** (every one of these was moved —
grepping `Assets/Scripts` for `Keyboard\.current|Mouse\.current` now
returns zero real call sites, one leftover explanatory comment only).
Two files (`Round/ExitCarState.cs`, `Sabotage/SabotageUseController.cs`)
turned up during implementation that hadn't been caught in the initial
survey, and `HotbarController.cs`'s actual slot count is 5, not 9
(`PlayerInventory.SlotCount = 5`) — both corrections reflected below:

| File : line | Current | Became action |
|---|---|---|
| `Player/FirstPersonController.cs` | `W/A/S/D` | `Gameplay/Move` (Vector2) |
| `Player/FirstPersonController.cs` | `Mouse.current.delta` | `Gameplay/Look` (Vector2) |
| `Player/FirstPersonController.cs` | `LeftShift` | `Gameplay/Sprint` (button) |
| `Player/FirstPersonController.cs` | `LeftCtrl` | `Gameplay/Crouch` (button) |
| `Player/FirstPersonController.cs` | `Space` | `Gameplay/Jump` (button) |
| `Interaction/Interactor.cs` | `E` | `Gameplay/Interact` (button) |
| `Player/CarryController.cs` | `E` (grab) | `Gameplay/Interact`, shared |
| `Player/CarryController.cs` | `G` (set down) | `Gameplay/SetDown` (button) |
| `Player/CarryController.cs` | Mouse left (throw charge) | `Gameplay/PrimaryAction`, shared |
| `Round/ExitCarState.cs` | `E` (climb out) | `Gameplay/Interact`, shared |
| `Sabotage/SabotageUseController.cs` | Mouse left (use item) | `Gameplay/PrimaryAction`, shared |
| `Sabotage/SabotageUseController.cs` | Mouse right (explicit throw) | `Gameplay/SecondaryAction` (button) |
| `Round/SpectatorController.cs` | Mouse left (cycle target) | `Gameplay/PrimaryAction`, shared |
| `Player/PlayerDropController.cs` | `Q` | `Gameplay/DropItem` (button) |
| `Inventory/HotbarController.cs` | `Digit1`-`Digit5` loop | `Gameplay/Hotbar1`...`Hotbar5` (5 button actions, kept 1:1 rather than one composite — the existing per-slot loop stays, just reads `InputAction`s instead of `Key`s) |
| `Inventory/HotbarController.cs` | `Mouse.current.scroll` | `Gameplay/Scroll` (Vector2, PassThrough) |
| `UI/InventoryScreenUI.cs` | `Tab` | `Gameplay/ToggleInventory` (button) |
| `UI/InventoryScreenUI.cs` | `Escape` | **`UI/Cancel`** — moved to the stock UI map, not Gameplay, so it's shared with Milestone G's pause overlay |
| `Round/SpectatorController.cs` | `T` | `Gameplay/DebugSpectate` (button) |
| `Player/DebugThirdPersonCamera.cs` | `T` | same `Gameplay/DebugSpectate` action, both scripts read the one action |
| `Voice/SteamVoiceCapture.cs` | `V` (`pushToTalkKey` field, now removed) | `Gameplay/PushToTalk` (button) |

`PrimaryAction`/`SecondaryAction` deliberately alias one physical mouse
button across several scripts (carry-throw-charge, sabotage use,
spectate-cycle) exactly the way `Interact`/`E` already aliases
`Interactor`, `CarryController`, and `ExitCarState` — each consumer's
own game-state guard (`carry.IsCarrying`, `isWaiting`, jailed-and-
spectating) already made these mutually exclusive before this
migration; the action is just the physical key, not the meaning.

**Reworked the existing asset, didn't replace it.** `InputSystem_Actions.
inputactions` was Unity's unedited default template (`Player` map with
`Move/Look/Attack/Interact/Crouch/Jump/Previous/Next/Sprint`, plus a
stock `UI` map with `Navigate/Submit/Cancel/Point/Click/...`).
Renamed to `Assets/Resources/RobEveryoneControls.inputactions` (same
asset GUID, carried over via its `.meta`, so this reads as a rename to
Unity, not a new asset) and the `Player` map renamed to `Gameplay`,
reshaped into the table above. **The `UI` map was kept as-is** — its
`Cancel` action is already bound to Escape by default, which is exactly
what Milestone G needs, and it's free gamepad-menu-navigation support
later with zero extra work now.

**Deviation from the original plan, and why:** the doc originally
called for checking **Generate C# Class** in the asset's importer and
hand-writing the wrapper by hand off of what that would produce.
Actually implementing this without Unity's Editor open to run that
codegen step meant there was no way to verify a hand-typed generated
class actually matches what Unity would emit — a real risk of a subtle
mismatch nobody would catch until the project's next Editor open. Went
with a **`Resources.Load`-based self-bootstrap** instead: the asset
moved into `Assets/Resources/`, and `InputManager` is a plain static
class (not a MonoBehaviour) that lazily loads it, calls `.Enable()`,
and resolves every action once via `InputActionMap.FindAction` by name
— same "self-bootstraps on first use, no scene placement, no Inspector
wiring" shape `SteamManager`'s own lazy `Instance` getter already
established in this project, just without needing a live GameObject at
all since nothing here needs a `Transform`. **No Editor work of any
kind is needed for this milestone** — a stronger, simpler result than
the original plan's "check a box, wire a field" step.

**`Assets/Scripts/Input/InputManager.cs`** (as actually built):

```csharp
using UnityEngine;
using UnityEngine.InputSystem;

namespace RobEveryone.Input
{
    public static class InputManager
    {
        private static InputActionAsset asset;
        private static GameplayActions gameplay;
        private static UiActions ui;

        public static GameplayActions Gameplay { get { EnsureLoaded(); return gameplay; } }
        public static UiActions UI { get { EnsureLoaded(); return ui; } }
        public static InputActionAsset Asset { get { EnsureLoaded(); return asset; } } // Milestone B

        private static void EnsureLoaded()
        {
            if (asset != null) return;
            asset = Resources.Load<InputActionAsset>("RobEveryoneControls");
            asset.Enable();
            gameplay = new GameplayActions(asset.FindActionMap("Gameplay", throwIfNotFound: true));
            ui = new UiActions(asset.FindActionMap("UI", throwIfNotFound: true));
        }

        public class GameplayActions
        {
            public InputAction Move { get; }
            // ...Look, Sprint, Crouch, Jump, Interact, SetDown, DropItem,
            // ToggleInventory, DebugSpectate, PushToTalk, PrimaryAction,
            // SecondaryAction, Scroll -- each resolved once via
            // map.FindAction("Name", throwIfNotFound: true)
            public InputAction Hotbar(int index) => hotbar[index]; // 5 slots, cached the same way
        }

        public class UiActions
        {
            public InputAction Cancel { get; }
        }
    }
}
```

**Per-file migration pattern** (`FirstPersonController.cs` as the
example — every other file in the table above follows the identical
shape, swapping which action/field):

```csharp
// Before:
Vector2 input = Vector2.zero;
if (Keyboard.current.wKey.isPressed) input.y += 1f;
// ...(similarly for a/s/d)

// After:
Vector2 input = Vector2.ClampMagnitude(InputManager.Gameplay.Move.ReadValue<Vector2>(), 1f);
```

```csharp
// Before:
bool wantSprint = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;

// After:
bool wantSprint = InputManager.Gameplay.Sprint.IsPressed();
```

```csharp
// Before:
if (Keyboard.current[pushToTalkKey].isPressed) ...

// After (SteamVoiceCapture.cs):
if (InputManager.Gameplay.PushToTalk.IsPressed()) ...
```

Button-press-this-frame sites (`E`, `G`, `Q`, `Tab`, hotbar digits, `T`)
swapped `Keyboard.current[Key.X].wasPressedThisFrame` for
`InputManager.Gameplay.ActionName.WasPressedThisFrame()` the same way,
and every per-instance `[SerializeField] private Key ...` field that
used to hold a hardcoded key (`CarryController.grabKey/setDownKey`,
`PlayerDropController.dropKey`, `SteamVoiceCapture.pushToTalkKey`) was
removed entirely — the binding now lives solely in the `.inputactions`
asset, which is the whole point (Milestone B rebinds it from there, not
from a per-prefab Inspector field). Every touched file's
`using UnityEngine.InputSystem;` was removed too (confirmed via grep —
nothing left in any migrated file references `Key`, `Keyboard`, or
`Mouse` directly), replaced with `using RobEveryone.Input;`.

**Note on `mouseSensitivity`** (`FirstPersonController.cs`): stays
exactly as-is for this milestone — applied the same way, just to
`InputManager.Gameplay.Look.ReadValue<Vector2>()` instead of
`Mouse.current.delta.ReadValue()`. Making it a live setting is
Milestone F, not this one.

**Editor work: none.** No scene changes, no new components, no
Inspector wiring — genuinely code-only, stronger than originally
planned (see the deviation note above).

**Verify:** full playtest pass, single Editor then two-Editor
(ParrelSync) — movement, look, sprint, crouch, jump, interact, carry
pickup/set-down/throw-charge, drop, all 5 hotbar slots + scroll, Tab
inventory screen, Escape closing it, T debug-spectate, V push-to-talk,
sabotage item use (both mouse buttons), and the exit-car climb-out
prompt **all still work identically to before this milestone**, default
keys unchanged. Nothing should feel different. This is the "isolated,
playtested first" checkpoint the user explicitly asked for before
anything else in this doc builds on top.

---

## Milestone B — Keybind rebinding UI + persistence

**Goal:** let a player rebind any of the `Gameplay` map's button actions
(not `Move`/`Look` — those are axis composites, rebinding a WASD
composite one key at a time is a bigger UI than this needs; expose
those as sensitivity/invert settings in Milestone E instead, not
rebinding).

**New file `Assets/Scripts/Input/KeybindPersistence.cs`** — save/load
binding overrides as one JSON blob, same "static class over
PlayerPrefs" shape `PlayerCosmeticSelection.cs` already establishes:

```csharp
using UnityEngine;
using UnityEngine.InputSystem;

namespace RobEveryone.Input
{
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
```

Call `KeybindPersistence.Load()` right after the asset is first loaded
(add the call inside `InputManager.EnsureLoaded()`, immediately after
`asset.Enable()`), so overrides apply before anything reads an action
this session.

**New file `Assets/Scripts/UI/RebindActionRow.cs`** — one row per
rebindable action, template-and-instantiate the same way
`CustomizationUI`'s swatches work (one prefab row, cloned per action
into a vertical list):

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using RobEveryone.Input;

namespace RobEveryone.UI
{
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
            actionLabel.text = displayName;
            RefreshBindingLabel();
        }

        public void OnRebindButtonPressed()
        {
            if (activeRebind != null) return; // already mid-rebind

            waitingForInputIndicator.SetActive(true);
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
            waitingForInputIndicator.SetActive(false);
            RefreshBindingLabel();
            KeybindPersistence.Save();
        }

        public void OnResetButtonPressed()
        {
            KeybindPersistence.ResetAction(action);
            RefreshBindingLabel();
        }

        private void RefreshBindingLabel()
        {
            bindingLabel.text = action.GetBindingDisplayString();
        }
    }
}
```

**Editor work:**
1. Build a `RebindRowTemplate` prefab (label + current-binding text +
   Rebind button + Reset button + a hidden "Press any key..." text),
   parallel to how `SwatchTemplate` is built in
   `main-menu-customization-setup.md` Part 6.
2. A `ControlsTab` container inside `SettingsPanel` (see Milestone F)
   that instantiates one row per rebindable action from
   `InputManager.Gameplay` at panel-open time.
3. A "Reset All Keybinds" button wired to `KeybindPersistence.ResetAll()`.

**Verify:** rebind Interact from `E` to another key, close and reopen
the game, confirm the new binding persisted and the old `E` no longer
interacts. Reset it, confirm `E` works again.

---

## Milestone C — Audio mixer + volume/mute settings — code done, one Editor asset remains

**Goal:** a real `AudioMixer` with Master/Music/SFX/Voice groups, a
slider per group, and the mute/per-player-volume system
`voip-setup.md` Part 5 planned but never built.

**New asset** `Assets/Audio/MainMixer.mixer` — four groups: `Master`
(root) → `Music`, `SFX`, `Voice` (children). Right-click each group's
Volume parameter → **Expose to script** → name them exactly
`MasterVolume`, `MusicVolume`, `SFXVolume`, `VoiceVolume` (these exact
names are what the code below calls `SetFloat`/`GetFloat` with).

**New file `Assets/Scripts/Audio/AudioSettings.cs`** — static
PlayerPrefs-backed values (0-1 linear, UI-friendly), same shape as
`PlayerCosmeticSelection`:

```csharp
using System;
using UnityEngine;

namespace RobEveryone.Audio
{
    public static class AudioSettings
    {
        public static event Action OnChanged;

        public static float MasterVolume { get => Get("Master"); set => Set("Master", value); }
        public static float MusicVolume { get => Get("Music"); set => Set("Music", value); }
        public static float SFXVolume { get => Get("SFX"); set => Set("SFX", value); }
        public static float VoiceVolume { get => Get("Voice"); set => Set("Voice", value); }

        private static float Get(string key) => PlayerPrefs.GetFloat($"RobEveryone.Volume.{key}", 1f);

        private static void Set(string key, float value)
        {
            PlayerPrefs.SetFloat($"RobEveryone.Volume.{key}", value);
            PlayerPrefs.Save();
            OnChanged?.Invoke();
        }
    }
}
```

**New file `Assets/Scripts/Audio/AudioMixerApplier.cs`** — the one
component that actually knows about the `AudioMixer` asset, lives on
the same persistent bootstrap object as `SteamManager`:

```csharp
using UnityEngine;
using UnityEngine.Audio;

namespace RobEveryone.Audio
{
    // Translates AudioSettings' 0-1 linear values into the mixer's dB
    // scale (a mixer's exposed volume param is logarithmic, not linear
    // -- a straight 0-1 slider feeding SetFloat directly would make most
    // of the slider's range sound like "basically silent" or "basically
    // max," per Unity's own documented AudioMixer conversion).
    public class AudioMixerApplier : MonoBehaviour
    {
        [SerializeField] private AudioMixer mixer;

        private void OnEnable()
        {
            AudioSettings.OnChanged += ApplyAll;
            ApplyAll();
        }

        private void OnDisable() => AudioSettings.OnChanged -= ApplyAll;

        private void ApplyAll()
        {
            Apply("MasterVolume", AudioSettings.MasterVolume);
            Apply("MusicVolume", AudioSettings.MusicVolume);
            Apply("SFXVolume", AudioSettings.SFXVolume);
            Apply("VoiceVolume", AudioSettings.VoiceVolume);
        }

        private void Apply(string exposedParam, float linear)
        {
            float dB = linear <= 0.0001f ? -80f : Mathf.Log10(linear) * 20f;
            mixer.SetFloat(exposedParam, dB);
        }
    }
}
```

**New file `Assets/Scripts/Voice/VoiceMuteList.cs`** — the mute half of
`voip-setup.md` Part 5, exactly as that doc sketched it (session-local,
not persisted — muting someone only matters while they're in your
current lobby):

```csharp
using System.Collections.Generic;

namespace RobEveryone.Voice
{
    public static class VoiceMuteList
    {
        private static readonly HashSet<uint> muted = new();

        public static bool IsMuted(uint netId) => muted.Contains(netId);
        public static void SetMuted(uint netId, bool value)
        {
            if (value) muted.Add(netId);
            else muted.Remove(netId);
        }
    }
}
```

**Modify `Assets/Scripts/Voice/SteamVoicePlayback.cs`** — guard the top
of `EnqueueCompressed`:

```csharp
public void EnqueueCompressed(byte[] frame)
{
    if (!SteamManager.Initialized) return;
    if (VoiceMuteList.IsMuted(GetComponent<NetworkIdentity>().netId)) return;
    // ...unchanged
}
```

**Deviation, code side:** `AudioMixerApplier` and the mute check are
already implemented and committed — both self-bootstrap (the applier
via `RuntimeInitializeOnLoadMethod`, same shape as `InputManager`/
`DisplaySettingsApplier`; `SteamVoicePlayback.Awake` auto-finds the
`Voice` group via `mixer.FindMatchingGroups("Voice")` at runtime).
Neither needs any Inspector field wired — only the asset itself needs
creating.

**Editor work (the only remaining part of this milestone):**
1. Create `Assets/Resources/MainMixer.mixer` (must be exactly this path
   — that's what `Resources.Load<AudioMixer>("MainMixer")` looks for),
   the 4 groups (`Master` → `Music`/`SFX`/`Voice` children), expose the
   4 volume params with these **exact** names: `MasterVolume`,
   `MusicVolume`, `SFXVolume`, `VoiceVolume`.
2. Route every existing gameplay `AudioSource` into `SFX` (footsteps,
   car horn/yell, item pickup, etc. — whatever exists today) and any
   future music source into `Music`. **Nothing plays through `Music`
   yet** (`todo.md` confirms ambient music isn't built) — the group and
   slider exist now so that work slots in later without another mixer
   pass. `Voice` routes itself automatically once this asset exists —
   no manual step needed there.

**Verify:** drag Master to 0 → silent (footsteps, voice, everything).
Drag Voice to 0 with Master up → still hear SFX/footsteps, not voice.
Mute a specific player (via the row UI built in Milestone F) → you stop
hearing just them, others unaffected; unmute restores it. Restart the
game → sliders persisted; mute list resets (by design, session-local).

---

## Milestone D — Graphics/Display settings — ✅ code done, no Editor work needed

**Goal:** resolution, fullscreen mode, quality preset, FOV, VSync —
all either genuinely new or (for quality) finally exposed.

**New file `Assets/Scripts/Graphics/DisplaySettings.cs`**:

```csharp
using System;
using UnityEngine;

namespace RobEveryone.Graphics
{
    public static class DisplaySettings
    {
        public static event Action OnChanged;

        public static int ResolutionIndex
        {
            get => PlayerPrefs.GetInt("RobEveryone.ResolutionIndex", -1); // -1 = current/native, resolved at apply time
            set { PlayerPrefs.SetInt("RobEveryone.ResolutionIndex", value); Save(); }
        }

        public static FullScreenMode ScreenMode
        {
            get => (FullScreenMode)PlayerPrefs.GetInt("RobEveryone.ScreenMode", (int)FullScreenMode.FullScreenWindow);
            set { PlayerPrefs.SetInt("RobEveryone.ScreenMode", (int)value); Save(); }
        }

        public static int QualityIndex // 0 = Low (Mobile_RPAsset), 1 = High (PC_RPAsset) -- matches existing QualitySettings.asset tiers
        {
            get => PlayerPrefs.GetInt("RobEveryone.QualityIndex", 1);
            set { PlayerPrefs.SetInt("RobEveryone.QualityIndex", value); Save(); }
        }

        public static float FieldOfView
        {
            get => PlayerPrefs.GetFloat("RobEveryone.FOV", 75f);
            set { PlayerPrefs.SetFloat("RobEveryone.FOV", value); Save(); }
        }

        public static bool VSync
        {
            get => PlayerPrefs.GetInt("RobEveryone.VSync", 1) == 1;
            set { PlayerPrefs.SetInt("RobEveryone.VSync", value ? 1 : 0); Save(); }
        }

        private static void Save()
        {
            PlayerPrefs.Save();
            OnChanged?.Invoke();
        }
    }
}
```

**New file `Assets/Scripts/Graphics/DisplaySettingsApplier.cs`** —
applies on change and once at startup, on the same persistent bootstrap
object:

```csharp
using UnityEngine;

namespace RobEveryone.Graphics
{
    public static class DisplaySettingsApplier
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            DisplaySettings.OnChanged += Apply;
            Apply();
        }

        private static void Apply()
        {
            Resolution[] resolutions = Screen.resolutions;
            int index = DisplaySettings.ResolutionIndex;
            if (index >= 0 && index < resolutions.Length)
            {
                Resolution r = resolutions[index];
                Screen.SetResolution(r.width, r.height, DisplaySettings.ScreenMode);
            }
            else
            {
                Screen.fullScreenMode = DisplaySettings.ScreenMode;
            }

            QualitySettings.SetQualityLevel(DisplaySettings.QualityIndex, applyExpensiveChanges: true);
            QualitySettings.vSyncCount = DisplaySettings.VSync ? 1 : 0;
        }
    }
}
```

Static + `RuntimeInitializeOnLoadMethod`, not a MonoBehaviour on a
bootstrap object — same self-bootstrapping shape `InputManager` and
`AudioMixerApplier` already use, so **no Editor wiring needed** here
either.

**FOV** got its live consumer directly in `PlayerCameraRig.cs` (read
during implementation, not guessed): a new `SetNormalFov(float)` method
sets `cam.fieldOfView` immediately while the rig is `Docked` (the
ordinary case), or updates `dockFov` if a cutscene cut is currently
active (`BlendingIn`/`Held`/`BlendingOut`) so the new value takes over
the moment the cut ends and restores it — `cutFov`'s own cut-specific
value is left completely alone either way. `PlayerCameraRig` subscribes
to `DisplaySettings.OnChanged` itself (`OnEnable`/`OnDisable`) and
applies once in `Awake`, so `FirstPersonController` never needed
touching for this.

**Editor work: none** — same as Milestone A, this is fully self-
contained, no scene/Inspector changes.

**Verify:** change resolution/fullscreen, quality preset, VSync — each
takes effect immediately and survives a restart. Slide FOV — visibly
widens/narrows the first-person view live, doesn't fight the cutscene
camera cuts.

---

## Milestone E — Gameplay/Accessibility — code done, one Editor step remains

**Goal:** invert-Y look, and a voice-chat captions/name-tag HUD.

**`Assets/Scripts/Player/ControlSettings.cs`** — also picked up
`MouseSensitivity` here (moved up from Milestone F, since
`FirstPersonController` was already being touched for invert-Y in this
same milestone — no separate pass needed later):

```csharp
using System;
using UnityEngine;

namespace RobEveryone.Player
{
    public static class ControlSettings
    {
        public static event Action OnChanged;

        public static bool InvertY { get; set; } // PlayerPrefs-backed, see source
        public static bool CaptionsEnabled { get; set; } // PlayerPrefs-backed, see source
        public static float MouseSensitivity { get; set; } // PlayerPrefs-backed, default 2f, see source
    }
}
```

`FirstPersonController.cs`'s old `[SerializeField] private float
mouseSensitivity = 2f;` field was removed entirely — `HandleLook` now
reads `ControlSettings.MouseSensitivity` and applies `InvertY` (flips
`delta.y`) live, no restart needed.

**`Assets/Scripts/UI/VoiceCaptionsHUD.cs`** — one HUD-corner list of
currently-speaking rivals, a supplemental cue to `PlayerHeadTalkScale`'s
pulse (not a replacement). Keyed by the `SteamVoicePlayback` instance
itself, not a display-name string — two rivals can both still read
"rival" before their name's `SyncVar` arrives, so the name is looked up
fresh from a paired `PlayerInventory` every frame instead of captured
once at register time:

```csharp
public class VoiceCaptionsHUD : MonoBehaviour
{
    [SerializeField] private TMP_Text listText;
    public static VoiceCaptionsHUD Instance { get; private set; }
    private readonly Dictionary<SteamVoicePlayback, PlayerInventory> tracked = new();

    public void Register(SteamVoicePlayback playback, PlayerInventory inventory) => tracked[playback] = inventory;
    public void Unregister(SteamVoicePlayback playback) => tracked.Remove(playback);
    // Update() rebuilds listText.text from every tracked pair whose
    // Amplitude is above threshold, gated entirely on CaptionsEnabled
}
```

`PlayerVoice.cs` already calls `VoiceCaptionsHUD.Instance?.Register(...)`
/ `Unregister(...)` from its own `OnStartClient`/`OnStopClient` (added
alongside the rest of Milestone A/E's changes) — every player object on
every client registers itself, not just the local one, since captions
need to show every rival talking. The null-conditional means this is a
no-op wherever the HUD element doesn't exist yet (Lobby, Main Menu).

**Beyond the original plan — live head portraits, not just names.**
Per the user's request, each caption row now shows an actual live
render of that speaker's real skin/color next to their name, not just
text. New `Assets/Scripts/Voice/VoicePortraitPool.cs`: a small fixed
pool (default 8, matching voip-setup.md's stated player-count target)
of offstage "rig" GameObjects, each built entirely at runtime (no
per-slot Editor authoring) far below the map (`(0,-500,0)` + spacing) —
nothing else is ever near there, so no special culling layer is needed
to isolate the shot. Each slot has its own `Camera` rendering into its
own `RenderTexture` (transparent background), reusing the exact
`PlayerSkinRoster`/`PlayerColorPalette` + `PlayerColorizer` pattern
`CustomizationUI`'s own preview already establishes — `Acquire(player)`
spawns/re-skins a free slot to match that player's real
`PlayerSkinSpawner.SkinIndex`/`ColorIndex` (both newly exposed as
public read-only properties) and returns the `RenderTexture`;
`Release(player)` frees it. `VoiceCaptionsHUD` was reworked from a
single concatenated `Text` block into a proper row list (new
`Assets/Scripts/UI/VoiceCaptionRow.cs`: `RawImage` + `TMP_Text`,
instantiated per currently-speaking player from a hidden template —
same template-and-instantiate shape `CustomizationUI`'s swatches and
`RebindActionRow` already use), each row's `RawImage.texture` set to
that player's acquired portrait.

**Untested geometric assumption, flagged rather than guessed:** the
portrait camera assumes this character pack's skins face `+Z` (the
standard authored-forward for the Quaternius rig, matching how
`PlayerSkinSpawner` instantiates a skin under the Player's own
`transform`) and positions the camera in front of that face looking
back at it. If a portrait comes out showing the back of someone's head
instead of their face, flip `cameraDistance` to a negative value on the
`VoicePortraitPool` component (Inspector) rather than editing code.

**Also beyond the original plan — your own row, in the same list.** The
rival caption list structurally can't show your own talking status:
your own voice frames never reach your own `SteamVoicePlayback`
(`PlayerVoice`'s `RpcReceiveVoice` is `includeOwner:false`, the same
"never hear yourself" rule the head-pulse feature already follows), so
there's no `Amplitude` signal for yourself to key off. `VoiceCaptionsHUD`
handles this with a second, parallel code path (`UpdateSelfRow`) that
reads `SteamVoiceCapture.Transmitting` directly instead — already-local
state (true exactly while PTT is held / open mic is on), no network
signal needed — and instantiates/destroys **one row in the exact same
`rowContainer`** the rival rows use (`SetAsFirstSibling()` so "you"
consistently show first, rivals below), rather than a separate
indicator elsewhere. Portrait comes from the same
`VoicePortraitPool.Acquire(PlayerInventory.LocalPlayer)` a rival's row
uses. Your own row is **not** gated by `ControlSettings.CaptionsEnabled`
— it's your own mic status, not a rival caption, so it shows regardless
of that accessibility toggle.

**Editor work (the only remaining part of this milestone):**
1. Add a `VoicePortraitPool` component to a GameObject in `SampleScene`
   (and `Lobby.unity`, since voice chat works there too) — wire its
   **Skin Roster** field to `Assets/PlayerSkinRoster.asset` and
   **Palette** field to `Assets/PlayerColorPalette.asset` (the same two
   assets already wired on the Player prefab's `PlayerSkinSpawner`).
2. Build a `VoiceCaptionRow` template prefab (a small horizontal row:
   a `RawImage` on the left ~64×64, a `TMP_Text` beside it) and a
   `RowContainer` (a `Vertical Layout Group`) on the gameplay HUD Canvas
   in `SampleScene`, wire `VoiceCaptionsHUD`'s **Row Container**/**Row
   Template** fields to them — per the full walkthrough given directly
   to the user — then repeat the same container+`VoiceCaptionsHUD`
   setup in `Lobby.unity`'s Canvas. No second row/object needed anywhere
   — the self row lives in this same container automatically.

**Verify:** enable Invert-Y, confirm mouse-look Y flips immediately (no
restart needed). Enable captions, have another player talk — a row
appears in the HUD showing their actual skin/color (facing the right
way) beside their name while they're transmitting, and disappears when
they stop; toggle captions off — every rival row disappears. Two people
talking at once — two independent rows, correct portraits each. Hold
your own push-to-talk key — a "You" row appears at the top of that same
list showing your own portrait, independent of the Captions toggle
(toggling it off hides rivals but never your own row).

---

## Milestone F — Assemble the real SettingsPanel

**Goal:** replace the `SettingsPanel` stub's "Settings coming soon"
label with real tabbed content wired to everything above, still
reachable exactly the way it is today (`MenuActions.OpenSettings()`).

**New file `Assets/Scripts/UI/SettingsPanelController.cs`** — the one
component both the Main-Menu panel and the in-game pause overlay
(Milestone G) instantiate/reference, so settings content is authored
once:

- 4 tab buttons (Audio / Controls / Graphics / Accessibility), each
  showing/hiding its own child container — same flat show/hide idiom
  `MenuActions.SetPanel` already uses, no need for `MenuNavigator`'s
  slide-stack here (Settings is already its own full-screen branch per
  `main-menu-visual-design.md`).
- Audio tab: 4 sliders bound to `AudioSettings`' 4 properties, plus a
  per-connected-player mute row (name + mute toggle) built the same
  template-and-instantiate way as `RebindActionRow`, driving
  `VoiceMuteList.SetMuted`.
- Controls tab: the `RebindActionRow` list from Milestone B, plus a
  mouse-sensitivity slider (finally makes `FirstPersonController.
  mouseSensitivity` a live `PlayerPrefs`-backed setting instead of an
  Inspector constant — same static-class-with-`OnChanged` shape as
  everything else in this doc).
- Graphics tab: resolution dropdown (populated from `Screen.
  resolutions`), screen-mode dropdown, quality dropdown (`Low`/`High`
  labels, indices 0/1), FOV slider, VSync toggle — all bound to
  `DisplaySettings`.
- Accessibility tab: Invert-Y toggle, Captions toggle, both bound to
  `ControlSettings`.
- A single `Back` button (already exists on the stub) stays wired to
  `MenuActions.CloseSettings()` when opened from Main Menu; Milestone
  G gives it a second context (closing the pause overlay instead)
  without duplicating the panel.

**Editor work:** the bulk of this milestone is Inspector work inside
`SettingsPanel` — build the 4 tab containers, drop in slider/dropdown/
toggle prefabs from the existing pixel UI kit (`ui-design.md`'s
established source), wire every control's `OnValueChanged` to the
matching static property setter. No new design system needed — reuse
whatever slider/toggle/dropdown pieces the kit already has, same
"adapt before building new" instruction `main-menu-visual-design.md`
gives for every other menu asset.

**Verify:** every control in every tab visibly changes behavior
immediately (no "Apply" button needed, matches every other setting in
this doc being live-applied). Close and reopen the game — every
setting persisted. `Back` still returns to Main Menu correctly.

---

## Milestone G — In-game pause overlay

**Goal:** the same `SettingsPanelController` content, reachable mid-
round via Escape, without pausing the round for anyone (per the
settled design decision).

**Escape key conflict, resolved:** `InventoryScreenUI.cs:408` already
polls Escape to close the inventory screen (`EscapePressed()`, gating
via its own `MenuOpen` static flag). Milestone A moved this onto the
stock `UI` map's `Cancel` action rather than `Gameplay` — the actual
behavior needed is: **Escape closes the inventory screen first if it's
open; otherwise it toggles the pause overlay.** Both consumers read the
same `UI.Cancel` action; whichever menu is currently open handles it,
and only the pause overlay's own script checks
`!InventoryScreenUI.MenuOpen` before reacting, so the two can never
both react to the same press.

**New file `Assets/Scripts/UI/PauseMenuUI.cs`**:

```csharp
using UnityEngine;
using RobEveryone.Input;
using RobEveryone.Player;
using RobEveryone.UI;

public class PauseMenuUI : MonoBehaviour
{
    [SerializeField] private GameObject pausePanel; // contains one instance of the same content Milestone F built
    [SerializeField] private FirstPersonController localController; // resolved from the local player on spawn, not hand-wired in the Inspector -- see note below

    private bool open;

    private void Update()
    {
        if (InventoryScreenUI.MenuOpen) return; // Tab/inventory owns Escape while it's open

        if (InputManager.UI.Cancel.WasPressedThisFrame())
        {
            SetOpen(!open);
        }
    }

    private void SetOpen(bool value)
    {
        open = value;
        pausePanel.SetActive(open);

        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = open;

        // Local-only freeze, same dedicated-flag pattern ExitCarFrozen/
        // SpectatingFrozen already established -- never the synced
        // IsFrozen, and deliberately does NOT touch RoundManager/AI, so
        // the round keeps running for every other client while this
        // player has the menu open.
        if (localController != null) localController.MenuFrozen = open;
    }

    public void OnBackButtonPressed() => SetOpen(false);
}
```

**Modify `FirstPersonController.cs`** — add the same shape as
`ExitCarFrozen`/`SpectatingFrozen`:

```csharp
public bool MenuFrozen { get; set; }
```

and extend `Update()`'s early-return gate to
`if (IsFrozen || SpectatingFrozen || ExitCarFrozen || MenuFrozen) return;`
— same reasoning as the exit-car fix earlier this project: a dedicated
local-only bool, never the synced `IsFrozen`, so nothing about the
jail/ragdoll/round-resolution flow can interact with "menu happens to
be open" in a way that leaves someone stuck.

**Editor work:**
1. Add a `PauseMenuUI` GameObject to the gameplay HUD Canvas in
   `SampleScene`, containing its own instance of the `SettingsPanel`
   content built in Milestone F (same prefab/layout, different parent
   panel — start hidden).
2. Wire `PauseMenuUI.pausePanel` to that instance.
3. `localController` resolves itself (add an `OnStartLocalPlayer`-style
   lookup, e.g. `FindFirstObjectByType` filtered to `isOwned`, or have
   `FirstPersonController.OnStartLocalPlayer` push a reference into a
   static "local player" slot the way other local-only systems in this
   project already do) rather than being Inspector-wired, since this
   object isn't a per-player prefab instance.

**Verify:** mid-round, press Escape — pause overlay opens, cursor
unlocks, your own movement/look freezes; the round timer, AI, and the
other player (two-Editor test) keep moving/playing completely
normally. Adjust a volume slider or keybind from inside this overlay —
same live effect as from the Main Menu. Press Escape again (or Back) —
closes, cursor relocks, you can move again. Open the Tab inventory
screen, press Escape — closes the inventory screen only, pause overlay
does *not* also open on that same press.

---

## Cross-cutting notes

- **Namespaces**: `RobEveryone.Input` (new), `RobEveryone.Audio` (new),
  `RobEveryone.Graphics` (new) follow the existing folder-to-namespace
  convention; `ControlSettings`/`PauseMenuUI` land in the existing
  `RobEveryone.Player`/`RobEveryone.UI` namespaces since they extend
  those systems directly rather than starting new ones.
- **Persistence pattern**: every new settings class mirrors
  `PlayerCosmeticSelection.cs` exactly — static class, `PlayerPrefs`-
  backed, an `OnChanged` event, no MonoBehaviour needed for the data
  itself. Only the *appliers* (`AudioMixerApplier`,
  `DisplaySettingsApplier`) are actual components, and only because
  they need a live scene reference (the `AudioMixer` asset) or must run
  every startup.
- **Nothing here is networked.** Every setting in this doc is
  genuinely local-only (even the mute list, deliberately, per
  `voip-setup.md`'s own Part 5 design) — no `[SyncVar]`, no `[Command]`,
  anywhere in this plan.
- **Bootstrap object**: `AudioMixerApplier` and `DisplaySettingsApplier`
  both belong on the same persistent (`DontDestroyOnLoad`) GameObject
  `SteamManager` already lives on — one more addition to a pattern
  that object already establishes, not a new persistence mechanism.

## Files touched (new)

- `Assets/Scripts/Input/InputManager.cs`, `KeybindPersistence.cs`
- `Assets/Scripts/UI/RebindActionRow.cs`, `SettingsPanelController.cs`,
  `PauseMenuUI.cs`, `VoiceCaptionsHUD.cs`
- `Assets/Scripts/Audio/AudioSettings.cs`, `AudioMixerApplier.cs`
- `Assets/Scripts/Voice/VoiceMuteList.cs`
- `Assets/Scripts/Graphics/DisplaySettings.cs`, `DisplaySettingsApplier.cs`
- `Assets/Scripts/Player/ControlSettings.cs`
- `Assets/Audio/MainMixer.mixer`
- `Assets/Resources/RobEveryoneControls.inputactions` (renamed/moved
  from `Assets/InputSystem_Actions.inputactions`, same GUID — done in
  Milestone A, ahead of the rest of this list)

## Files touched (modified)

- `Player/FirstPersonController.cs` — Input System migration (done),
  `MenuFrozen`, invert-Y, live FOV/sensitivity (later milestones)
- `Interaction/Interactor.cs`, `Player/CarryController.cs`,
  `Player/PlayerDropController.cs`, `Inventory/HotbarController.cs`,
  `UI/InventoryScreenUI.cs`, `Round/SpectatorController.cs`,
  `Player/DebugThirdPersonCamera.cs`, `Round/ExitCarState.cs`,
  `Sabotage/SabotageUseController.cs` — Input System migration, done in
  Milestone A
- `Voice/SteamVoiceCapture.cs` — Input System migration for
  push-to-talk (done in Milestone A)
- `Voice/SteamVoicePlayback.cs` — mute check, mixer group routing
- `Player/PlayerCameraRig.cs` — live FOV consumer
- `UI/MenuActions.cs` / `SettingsPanel` (scene) — real content replaces
  the "coming soon" stub
