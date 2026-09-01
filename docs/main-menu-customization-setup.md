# Main menu + player customization — Editor setup

This is out of the normal build order (per [plan.md](plan.md), menus/
cosmetics were "Tier 3, lower urgency" in [ui-design.md](ui-design.md))
but is being built now anyway — that's fine, it's self-contained and
doesn't block or get blocked by Stage 3's remaining work. Four scripts
already added:

- `Assets/Scripts/Customization/PlayerColorPalette.cs` — a ScriptableObject
  holding a fixed list of preset `Color`s.
- `Assets/Scripts/Customization/PlayerCosmeticSelection.cs` — static
  holder for the current skin/color choice (as indices, not raw values),
  persisted via `PlayerPrefs`. No unlock/purchase gating yet — every
  configured skin is pickable (that's planned per gameplay-design.md's
  meta-progression section, not built).
- `Assets/Scripts/Customization/PlayerColorizer.cs` — finds whichever
  material slot is named "Body" on a model (confirmed consistent across
  the Quaternius pack — checked `Casual_Male.fbx` and
  `BlueSoldier_Male.fbx`, both have `Head`/`Body` materials) and applies a
  color to it via `MaterialPropertyBlock`, leaving `Head` untouched
  (Body-only palette, per design decision).
- `Assets/Scripts/UI/CustomizationUI.cs` — drives Next/Previous skin
  cycling, builds swatch buttons from a `PlayerColorPalette`, and keeps a
  live preview instance in sync with both.

## 1. Create the palette asset

1. Project window → right-click → **Create → Rob Everyone → Player Color
   Palette**. Name it `PlayerColorPalette`.
2. In the Inspector, expand **Colors** and set 8 (or however many you
   want) preset swatches. Pull a few from the locked palette in
   [art-info.md](art-info.md) if you want thematic consistency, plus a
   handful of other saturated, clearly-distinct colors — these need to
   read as different from across a room in multiplayer, so avoid two
   colors that are too close in hue/brightness.

## 2. Create the Main Menu scene

1. **File → New Scene**, save as `Assets/Scenes/MainMenu.unity`.
2. **File → Build Settings** → add both `MainMenu` and `SampleScene` to
   **Scenes In Build**, with `MainMenu` first (index 0) — that's what
   Unity launches into by default.
3. Basic menu Canvas: **Play**, **Settings**, **Quit** buttons (per
   ui-design.md's Tier 3 note — simple vertical list, don't over-design
   this part). Add a **Customize** button too, leading to the screen
   below (either a separate panel toggled on/off in the same Canvas, or
   embedded directly in the main menu — your call, a toggled panel is
   simpler to wire).
4. Wire **Play** to load `SampleScene`
   (`UnityEngine.SceneManagement.SceneManager.LoadScene("SampleScene")` —
   easiest via a small one-line script and a Button OnClick event, or
   Unity's built-in **Events → SceneManagement → LoadScene** OnClick
   target if using the version that exposes it directly).

## 3. Build the customization screen

**Live preview, simplest version — no second camera/RenderTexture
needed:** since `MainMenu` is otherwise empty 3D space, just point the
scene's actual Main Camera at a spot where the preview model spawns.

1. In the Hierarchy, create an empty GameObject `PreviewSpawnPoint`,
   positioned a few units in front of wherever the Main Camera sits/looks
   (e.g. camera at origin looking down +Z, spawn point at `0, 0, 3`).
2. Add a simple ground/pedestal (a flattened Cube) under the spawn point
   so the character doesn't look like it's floating — reuse this scene's
   own lighting (a Directional Light) so the preview isn't pitch black.
3. In the Canvas, lay out:
   - **Previous** / **Next** buttons for skin cycling.
   - A **swatch container** — an empty GameObject with a **Horizontal
     Layout Group** (or Grid Layout Group) component, so swatch buttons
     auto-arrange as they're created.
   - A **swatch button template** — one Button with an Image component
     (the swatch's own color goes on this Image), **disabled
     (`SetActive(false)`) in the scene** — `CustomizationUI` instantiates
     copies of this at runtime and re-disables the template itself so it
     never shows up as an extra "9th swatch."
4. Add component **Customization UI** (`RobEveryone.UI`) somewhere
   persistent in the scene (e.g. a `MenuManager` object). Wire:
   - `Skin Prefabs` → drag in every skin `.fbx` from
     `Assets/Art/Characters/Quaternius-UltimateAnimatedCharacterPack/FBX/`
     you want selectable (start with a handful, not all 50+, to keep the
     first pass manageable).
   - `Palette` → the `PlayerColorPalette` asset from step 1.
   - `Preview Spawn Point` → `PreviewSpawnPoint`.
   - `Swatch Container` → the swatch container object.
   - `Swatch Button Template` → the template Button.
5. Wire the **Previous**/**Next** buttons' OnClick to
   `CustomizationUI.PreviousSkin()` / `NextSkin()`.
6. Press Play. The preview model should spawn at `PreviewSpawnPoint`,
   swatch buttons should appear in the palette's colors, clicking one
   should recolor the model's Body (not Head) live, and Previous/Next
   should cycle through the configured skins — all saved via
   `PlayerPrefs`, so it should still be selected next time you press Play
   or relaunch the Editor.

## Notes / open follow-ups

- No skin **unlocking** yet — every prefab in `Skin Prefabs` is pickable
  immediately. Gating this behind meta-progression (per
  gameplay-design.md) is future work once that system exists.
- This preview system is unrelated to the actual multiplayer `Player`
  prefab for now — nothing wires the chosen skin/color onto a real player
  yet, since networking (Stage 4–5) doesn't exist. That wiring is future
  work: once players are networked, spawn each player's chosen skin
  prefab (via `PlayerCosmeticSelection.SkinIndex`) and call
  `PlayerColorizer.ApplyBodyColor` with their chosen palette color as
  part of player spawn setup.
- If a chosen skin's idle pose looks like a T-pose/A-pose in the preview
  rather than actually idling, it doesn't have an Animator/Controller
  wired the way `HomeownerAnimator` does (Stage 3d) — that's fine for a
  static preview for now, wire one later if you want the preview to idle.
