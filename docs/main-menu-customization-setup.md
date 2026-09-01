# Main menu + player customization — Editor setup

**Visual/animation design (sliding panels, title pulse, layout) is in
[main-menu-visual-design.md](main-menu-visual-design.md)** — this doc is
the functional Unity wiring (works with plain placeholder buttons); once
the real art from that doc exists, Parts 5–8 below get re-skinned and the
simple show/hide panel switching gets replaced with the slide+dim
animation described there.

Detailed, click-by-click version. This is out of the normal build order
(menus/cosmetics were "Tier 3, lower urgency" in [ui-design.md](ui-design.md))
but self-contained, so it doesn't block or get blocked by Stage 3's
remaining work.

**No button art exists yet — that's fine.** Everywhere below that says
"add a Button," we're using Unity's built-in default button (a plain
gray rounded rectangle with text on it). That's the placeholder. Once
real button art exists, reskinning is a 30-second swap (change the
Button's **Source Image**) — nothing about the wiring/logic in this doc
changes. Don't wait on art to get this functional.

## The end state (read this first)

Here's the finished Hierarchy you're building toward, so you have a map
before diving in. `Canvas` holds three "screens" as sibling panels —
only one is ever active at a time, controlled by the `MenuActions`
script:

```
MainMenu (scene)
├── Canvas
│   ├── MainMenuPanel        (active by default)
│   │   ├── TitleText
│   │   └── ButtonList
│   │       ├── PlayButton
│   │       ├── CustomizeButton
│   │       ├── SettingsButton
│   │       └── QuitButton
│   ├── CustomizePanel       (starts inactive)
│   │   ├── BackButton
│   │   ├── PreviousButton
│   │   ├── NextButton
│   │   └── SwatchContainer
│   │       └── SwatchButtonTemplate   (starts inactive)
│   └── SettingsPanel        (starts inactive)
│       └── BackButton
├── EventSystem               (auto-created with Canvas)
├── MenuManager                (empty GameObject, holds scripts)
├── PreviewSpawnPoint          (3D world position, not UI)
└── Main Camera + a Directional Light
```

Four scripts are involved — three already exist, one is new:

- `Assets/Scripts/UI/MenuActions.cs` **(new)** — Play/Quit/panel-switching
  logic. Buttons call into this.
- `Assets/Scripts/UI/CustomizationUI.cs` — skin cycling + swatch picker +
  live preview (built last session).
- `Assets/Scripts/Customization/PlayerColorPalette.cs` — your preset
  colors.
- `Assets/Scripts/Customization/PlayerColorizer.cs` /
  `PlayerCosmeticSelection.cs` — recoloring + save/load, nothing to touch
  directly, `CustomizationUI` drives them.

---

## Part 1 — Create the scene

1. **File → New Scene**. In the dialog, pick the **Basic** template
   (whichever default option is offered — it doesn't matter much, we're
   about to gut it anyway) → **Create**.
2. **File → Save As...** → navigate into `Assets/Scenes/` → filename
   `MainMenu` → **Save**.
3. If the new scene came with a default 3D object or extra camera you
   don't need, that's fine to leave for now — it won't interfere.

## Part 2 — Add the scene to Build Settings

Unity needs to know both scenes exist and which one launches first.

1. **File → Build Settings...** (a separate window opens).
2. If `MainMenu` isn't already listed under **Scenes In Build**, click
   **Add Open Scenes** (adds whichever scene is currently open — make
   sure `MainMenu` is your active scene first).
3. Do the same for `SampleScene`: open it (double-click it in the Project
   window under `Assets/Scenes/`), then back in Build Settings click **Add
   Open Scenes** again.
4. **Order matters** — drag `MainMenu` to the **top** of the list (index
   0). Unity launches whichever scene is listed first.
5. Re-open `MainMenu` (double-click it in the Project window) before
   continuing — Build Settings switches your active scene around.
6. Close the Build Settings window.

## Part 3 — Add the Canvas

1. In the Hierarchy, right-click empty space → **UI → Canvas**. This
   creates a `Canvas` GameObject, and Unity **auto-creates an
   `EventSystem`** object alongside it the first time — that's normal and
   required (it's what makes buttons actually clickable; don't delete it).
2. Select `Canvas` in the Hierarchy. In the Inspector, find **Canvas
   Scaler** and set:
   - **UI Scale Mode** → **Scale With Screen Size**
   - **Reference Resolution** → `X: 3840`, `Y: 2160`
   - **Screen Match Mode** → **Match Width Or Height**
   - **Match** slider → `0.5`
   (Same settings as the crosshair fix, for consistency across scenes —
   otherwise UI sized here will look wrong-scaled relative to the HUD.)

## Part 4 — Add the Menu Actions script

1. In the Hierarchy, right-click empty space → **Create Empty**. Rename
   it `MenuManager`.
2. With `MenuManager` selected, in the Inspector click **Add Component**
   → search `Menu Actions` → select it (`RobEveryone.UI`).
3. Leave its fields empty for now — you'll wire `Main Menu Panel`,
   `Customize Panel`, `Settings Panel` in Part 8, once those objects
   actually exist.

## Part 5 — Build MainMenuPanel

1. Right-click `Canvas` in the Hierarchy → **UI → Panel**. Rename it
   `MainMenuPanel`. (A Panel is just an Image that fills its parent by
   default — good as both a background and a container.)
2. Right-click `MainMenuPanel` → **UI → Text - TextMeshPro** (accept the
   TMP Essentials import prompt if it appears again). Rename it
   `TitleText`, set its text to `ROB EVERYONE` (placeholder). Position it
   near the top — Anchor Preset: top-center, then adjust **Pos Y** down a
   bit from the very edge.
3. Right-click `MainMenuPanel` → **Create Empty**. Rename it
   `ButtonList`. This is going to auto-stack its child buttons for you —
   no manual position math needed:
   - Add Component → **Vertical Layout Group**. Set **Child Alignment**
     → Middle Center, **Spacing** → `20`, leave **Child Force Expand**
     both unchecked.
   - Add Component → **Content Size Fitter**. Set **Vertical Fit** →
     Preferred Size (keeps the group sized to exactly fit its buttons).
   - On `ButtonList`'s own **Rect Transform**, set Anchor Preset to
     middle-center, Pos `0, 0` — centers the whole button stack on
     screen.
4. Right-click `ButtonList` → **UI → Button - TextMeshPro**. Rename it
   `PlayButton`. Expand it in the Hierarchy, select its child `Text
   (TMP)`, change the text to `Play`.
5. Repeat step 4 three more times for `CustomizeButton` ("Customize"),
   `SettingsButton` ("Settings"), `QuitButton` ("Quit"). Since they're all
   children of `ButtonList`, the Vertical Layout Group stacks them
   automatically in the order they appear in the Hierarchy — drag to
   reorder if you want a different button order.

At this point, press Play — you should see a plain gray title + 4 plain
gray buttons, stacked and centered. That's the correct placeholder look.

## Part 6 — Build CustomizePanel

1. Right-click `Canvas` → **UI → Panel**. Rename it `CustomizePanel`.
2. Right-click `CustomizePanel` → **UI → Button - TextMeshPro**. Rename
   it `BackButton`, text `Back`. Position it top-left (Anchor Preset:
   top-left).
3. Add two more buttons the same way: `PreviousButton` (text `<`) and
   `NextButton` (text `>`), positioned left/right of center, roughly
   where you'd want to flank a character preview.
4. Right-click `CustomizePanel` → **Create Empty**. Rename it
   `SwatchContainer`.
   - Add Component → **Horizontal Layout Group**. **Spacing** → `10`,
     **Child Alignment** → Middle Center.
   - Position it near the bottom of the panel (Anchor Preset: bottom-
     center, adjust Pos Y up from the very edge).
5. Inside `SwatchContainer`, right-click → **UI → Button**. (Plain
   Button, not TextMeshPro this time — it doesn't need a label, just a
   colored Image.) Rename it `SwatchButtonTemplate`.
   - On its **Image** component, note the **Color** field — this is what
     `CustomizationUI` overwrites per-swatch at runtime, so its color
     here doesn't matter.
   - Set its Width/Height to something small and square, e.g. `60 x 60`.
   - **Disable it**: uncheck the checkbox next to its name at the very
     top of the Inspector (not the GameObject's active state via
     right-click — the actual checkbox). `CustomizationUI` instantiates
     copies of this at runtime; the template itself must stay hidden or
     you'll see an extra unstyled "9th swatch."
6. `CustomizePanel` should start **hidden**: select it, uncheck its
   active checkbox (top-left of the Inspector, next to its name).

## Part 7 — Add the 3D preview spot

This lives in the actual 3D scene, not inside the Canvas.

1. Right-click empty Hierarchy space → **Create Empty**. Rename it
   `PreviewSpawnPoint`.
2. Position it a few units in front of wherever your **Main Camera**
   looks — e.g. if the camera sits at `(0, 1, 0)` looking down `+Z`, put
   this at roughly `(0, 0, 3)`. Eyeball it, then adjust once you can see
   a model spawn there in Play mode.
3. Optional but recommended: add a simple pedestal so the character
   doesn't look like it's floating — right-click Hierarchy → **3D Object
   → Cube**, flatten it (small Y scale), position it directly under
   `PreviewSpawnPoint`.
4. Make sure there's a **Directional Light** in the scene (Basic template
   usually includes one) so the preview isn't pitch black.

## Part 8 — Build SettingsPanel (stub)

Settings content isn't designed yet — this is just a placeholder shell so
the button has somewhere to go.

1. Right-click `Canvas` → **UI → Panel**. Rename it `SettingsPanel`.
2. Right-click `SettingsPanel` → **UI → Button - TextMeshPro**. Rename it
   `BackButton`, text `Back`, position top-left.
3. Right-click `SettingsPanel` → **UI → Text - TextMeshPro**. Text:
   `Settings coming soon`.
4. Start it hidden, same as `CustomizePanel` (uncheck its active
   checkbox).

## Part 9 — Wire everything together

This is the part that actually connects all the pieces you just built.

### 9a. MenuManager (MenuActions component)

Select `MenuManager`. On its **Menu Actions** component:

- `Main Menu Panel` → drag `MainMenuPanel`
- `Customize Panel` → drag `CustomizePanel`
- `Settings Panel` → drag `SettingsPanel`

### 9b. Button OnClick() events

For each button below: select it → in the Inspector find the **Button**
component → its **On Click ()** list → click the **+** at the bottom →
drag `MenuManager` into the new empty **Object** slot → click the
**function dropdown** (says "No Function") → find **Menu Actions** in the
list → pick the matching method.

| Button | OnClick target | Function |
|---|---|---|
| `PlayButton` | `MenuManager` | `MenuActions → PlayGame ()` |
| `QuitButton` | `MenuManager` | `MenuActions → QuitGame ()` |
| `CustomizeButton` | `MenuManager` | `MenuActions → OpenCustomize ()` |
| `SettingsButton` | `MenuManager` | `MenuActions → OpenSettings ()` |
| `BackButton` (under `CustomizePanel`) | `MenuManager` | `MenuActions → CloseCustomize ()` |
| `BackButton` (under `SettingsPanel`) | `MenuManager` | `MenuActions → CloseSettings ()` |

### 9c. Customization UI component

Add component **Customization UI** to `MenuManager` (same object is
fine — it can hold multiple scripts). Wire:

- `Skin Prefabs` → drag in a handful of skin `.fbx` files from
  `Assets/Art/Characters/Quaternius-UltimateAnimatedCharacterPack/FBX/`
  (start with 5–6, not all 50+)
- `Palette` → your `PlayerColorPalette` asset (create one first via
  Project window right-click → **Create → Rob Everyone → Player Color
  Palette** if you haven't yet, and fill in its `Colors` array)
- `Preview Spawn Point` → `PreviewSpawnPoint`
- `Swatch Container` → `SwatchContainer`
- `Swatch Button Template` → `SwatchButtonTemplate`

### 9d. Previous/Next buttons

- `PreviousButton`'s On Click () → `+` → drag `MenuManager` → function
  dropdown → **Customization UI → PreviousSkin ()**
- `NextButton`'s On Click () → same, but **NextSkin ()**

## Part 10 — Test

Press Play:

1. You should land on `MainMenuPanel` — plain gray title + 4 buttons.
2. Click **Customize** → `MainMenuPanel` hides, `CustomizePanel` shows, a
   character model appears at `PreviewSpawnPoint`.
3. Click a swatch → the model's Body color should change immediately
   (not the Head).
4. Click **`<`**/**`>`** → the model should swap to a different skin.
5. Click **Back** → returns to `MainMenuPanel`.
6. Click **Play** → loads `SampleScene`.
7. Stop Play, press Play again, go straight to Customize — your last
   skin/color choice should already be selected (that's the `PlayerPrefs`
   save working).

## Troubleshooting

- **Nothing happens when I click a button**: check there's exactly one
  `EventSystem` in the scene (Canvas creation should have added it
  automatically — if you made multiple Canvases across attempts, you may
  have duplicates; delete extras, keep one).
- **Buttons don't stack/center correctly**: double-check `ButtonList` has
  both **Vertical Layout Group** and **Content Size Fitter**, and that
  its own Rect Transform anchor is middle-center.
- **Swatches don't appear**: confirm `PlayerColorPalette`'s `Colors`
  array actually has entries (empty array = no swatches to build), and
  that `SwatchButtonTemplate` is disabled (unchecked) rather than
  deleted — `CustomizationUI` needs it to exist to copy from.
- **Model spawns T-posing instead of idling**: expected for now — none
  of these preview skins have an Animator wired the way
  `HomeownerAnimator` does (Stage 3d). Fine for a static preview; wire
  one later if you want it to idle.
- **"Play" button does nothing / errors**: confirm `SampleScene` is
  actually spelled that way in Build Settings (case-sensitive) and is
  checked/included in the list.

## Notes / open follow-ups

- No skin **unlocking** yet — every prefab in `Skin Prefabs` is pickable
  immediately. Gating this behind meta-progression (per
  [gameplay-design.md](gameplay-design.md)) is future work.
- This preview system is unrelated to the actual multiplayer `Player`
  prefab for now — nothing wires the chosen skin/color onto a real player
  yet, since networking (Stage 4–5) doesn't exist. Future work: once
  players are networked, spawn each player's chosen skin
  (`PlayerCosmeticSelection.SkinIndex`) and call
  `PlayerColorizer.ApplyBodyColor` with their palette color at spawn.
- Every placeholder button/panel here is a plain Unity default look on
  purpose — swap `Source Image` on each once real button art exists, no
  logic changes needed.
