# Main Menu character preview + Settings fall animation (issue #39, 2/3 and 3/3)

Covers the two harder pieces of #39 that the title pulse (1/3, already
done) didn't need: making the character preview persistent across every
Main Menu panel instead of only existing while Customize is open, and
Settings' fall-through-frame/fall-from-sky animation. All the logic is
written and wired as far as it safely can be without seeing it rendered
— this doc is the remaining Editor checklist.

## What changed, and why (read this before touching the Editor)

The character preview used to be owned entirely by `CustomizationUI`:
spawned in its own `OnEnable`, so it only ever existed while the
Customize panel specifically was active — never on Main or Play, and
Settings had no way to reach it at all for a fall animation.
`main-menu-visual-design.md` calls for it to persist across Main/Lobby/
Customization, which the old setup never actually did.

That's also exactly where issue #51 (moving the background from a
static diorama to a drone-shot flythrough) would have collided with
this: the old preview was rendered by the same single `Main Camera`
that draws the background. If #51 replaces or moves that camera, the
preview would move/break right along with it.

Fixed both at once with **`MenuCharacterPreview`** (new,
`Assets/Scripts/UI/MenuCharacterPreview.cs`), attached to `MenuManager`
(a root-level object never torn down by panel switching):

- Its own isolated camera stage, rendering to a `RenderTexture` — the
  exact same proven pattern `HotbarSlotUI` already uses for item
  previews (far-away stage position, a dedicated `MenuPreview` layer
  added to Project Settings > Tags and Layers, a camera culled to just
  that layer). Completely decoupled from whatever camera renders the
  background — #51 can do anything it wants to that camera without ever
  touching this.
- Subscribes to `PlayerCosmeticSelection.OnChanged` (already existed)
  and reacts on its own — `CustomizationUI` no longer owns or spawns
  the preview at all, it just writes the selection like before.
- Auto-frames the camera to whatever the current skin's actual bounds
  are (same technique as `HotbarSlotUI.FrameCamera`), so it isn't
  hand-tuned per skin.

`MenuActions` gained the Settings fall animation, driving
`MenuCharacterPreview.PreviewTransform` directly — falls straight down
out of frame opening Settings (ease-in, like gravity taking hold),
falls back in from above closing it (ease-out, like landing). Already
wired to the new component in the scene.

## Editor steps still needed

1. **Add a `RawImage` to display the preview**, in the character
   preview's safe zone from `main-menu-visual-design.md` (roughly
   `x: 2200–3700, y: 500–2000` on the 3840×2160 design canvas — treat
   that as a *starting point*, not an exact formula: the actual
   `TitleWordmark` element doesn't sit exactly where its own spec'd
   region would put it either, so whatever looks right in the Game view
   wins over the math). It needs to be a **sibling of
   MainMenuPanel/CustomizePanel/SettingsPanel/PlayPanel** directly under
   `Canvas` — not a child of any one of them — so it stays visible
   across every panel switch instead of disappearing when one panel
   deactivates.
2. **Wire it up**: select `MenuManager` in the Hierarchy, find its
   `Menu Character Preview` component, drag the new RawImage into
   `Preview Image`. `Skin Roster` and `Palette` are already wired to the
   same assets `CustomizationUI` uses.
3. **Press Play, check the framing** on a few different skins — the
   auto-framing should keep the whole character in view regardless of
   proportions, but this is exactly the kind of thing that needs actual
   eyes on it, not just reading the numbers (same as #55's view bob
   needed a tuning pass after the logic was already correct).
4. **Confirm the Settings fall animation** looks right — `fallDistance`
   (2000) and `fallDuration` (0.4s) on `MenuActions` are starting
   values, not final ones.
5. Optional cleanup: the old `PreviewSpawnPoint` Transform in the scene
   is now unused (nothing references it anymore) — safe to delete if
   you want to tidy it up, harmless to leave.

## Where to look

- `Assets/Scripts/UI/MenuCharacterPreview.cs` — the new persistent
  preview owner.
- `Assets/Scripts/UI/CustomizationUI.cs` — now just skin/color cycling,
  no preview ownership.
- `Assets/Scripts/UI/MenuActions.cs` — `OpenSettings`/`CloseSettings`
  now call `PlayFall`.
- `Assets/Scripts/UI/HotbarSlotUI.cs` — the proven isolated-camera-stage
  pattern this was modeled on.
