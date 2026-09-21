# Batch unlock popup — Editor setup (#61)

`BatchUnlockPopupUI.cs` is written and needs a Canvas hierarchy built by
hand in `Lobby.unity` and wired up in the Inspector — pure Editor work,
no code. The "ItemPreview" layer this reuses (same one `HotbarSlotUI`
already uses for its own spinning hotbar previews) already exists in
the project, so nothing to add there.

## Current behavior

Per direct request: items unlock (and this popup shows) starting the
Lobby that **precedes** a batch's own final/Night round, not the Lobby
that follows it — giving players a shot at the next tier's gear before
their toughest round of the batch, rather than only after they've
already gotten through it.

Concretely: standing in the Lobby with the **Night skybox** (the one
right before playing round 3 of the current batch) is when the *next*
batch's items become buyable and this popup appears — not the Morning-
skybox Lobby that comes after that round ends. `GameFlowManager.
EffectiveShopBatch` (`batchNumber + 1` once `RoundInBatch >= 3`, `
batchNumber` otherwise) is what both `ShopShelfItem.Unlocked` and this
popup read, so the shelf and the popup always agree on what's newly
available. Pricing is untouched — `ShopShelfItem.CurrentPrice` still
keys off the literal, not-yet-incremented `BatchNumber`, so buying
during this early window means slightly better pricing than the same
item costs once the batch officially turns over.

## Build the Canvas hierarchy

1. In `Lobby.unity`, create a new Canvas (or add to an existing
   full-screen UI Canvas already in the scene) — `Screen Space -
   Overlay` is fine, this doesn't need to render in-world.
2. Under it, add an empty GameObject called `BatchUnlockManager` —
   this is what carries the `BatchUnlockPopupUI` component itself, and
   it must stay active the whole time the Lobby is loaded (don't ever
   set this one inactive).
3. Under `BatchUnlockManager`, add a *separate* child GameObject called
   `Panel`, stretched to fill the screen, with a semi-transparent dark
   background image (an `Image` component, alpha around `0.6-0.75`) so
   the spinning item reads clearly against it. This is the one that
   gets shown/hidden — safe to do, since it's not the object the script
   lives on (see "Why the script and Panel are separate objects" below
   if that split seems unnecessary).
4. Inside `Panel`:
   - A `TextMeshPro - Text (UI)` for the title, large, centered near
     the top third of the screen. Not bound to any text in the
     Inspector — `BatchUnlockPopupUI` sets it to `"Item Unlocked"` at
     runtime.
   - A `RawImage`, centered, sized noticeably larger than a hotbar
     slot's own preview (e.g. 400×400 or bigger) — this is what
     `modelImage` renders the spinning model onto.
   - A second `TextMeshPro - Text (UI)` below the RawImage for the
     item's name (e.g. "Bat", "Alarm Clock").
5. Add the `BatchUnlockPopupUI` component to `BatchUnlockManager` (the
   parent from step 2 — **not** `Panel`).
6. Wire the Inspector fields on `BatchUnlockManager`:
   - `Panel` → the `Panel` child GameObject from step 3.
   - `Title Text` → the first TMP text.
   - `Item Name Text` → the second TMP text.
   - `Model Image` → the RawImage.
   - Leave `Preview Padding` / `Spin Speed` / `Seconds Per Item` at
     their defaults to start (`1.3`, `40°/sec`, `3s`) — tune by eye
     once you can see it running.
7. Leave both `BatchUnlockManager` and `Panel` active in the Editor —
   the script hides `Panel` itself at runtime the instant it determines
   nothing needs to show, so there's nothing to pre-hide by hand. If
   you see a red error in the Console the moment you enter Play Mode,
   `Panel` is still pointed back at `BatchUnlockManager` itself — fix
   that before testing further.

🔴 **Rest point**: play through to the Lobby right **before** a batch's
final (night) round — the one with the Night skybox. You should see
"Item Unlocked" + the item's name + a large spinning model for each
newly-unlocked item, a few seconds each, then nothing (panel hides
itself). The Morning-skybox Lobby that follows the night round should
show nothing new (already shown, or nothing left to show).

## Why the script and Panel are separate objects

`BatchUnlockPopupUI` hides `Panel` by calling `panel.SetActive(false)`.
If that script lived on `Panel` itself, that call would disable the
very GameObject the script is running on — which silently stops any
coroutine running on it (Unity won't run a coroutine on an inactive
GameObject), so the popup could never actually show, no matter what
unlocked. This is exactly the same problem `LoadingScreenUI` already
solves correctly elsewhere in this project (its own script stays
enabled at all times; only its `panel` field, a separate object, gets
toggled) — worth reading that class's own comment if this still seems
like unnecessary indirection. `BatchUnlockPopupUI.Awake()` logs a loud
error if `Panel` is ever pointed back at its own GameObject, so this
can't silently recur.

## Design history (for context, not needed to follow the steps above)

This feature went through three real, confirmed bugs during
development, each fixed in turn:

1. **First attempt**: tried appending "Batch N unlocks: X, Y" text onto
   the loading-screen message shown as a round ends. Never actually
   appeared — `GameFlowManager.HandleRoundEnded` (where that text was
   built) runs while the *gameplay* scene is still active, before the
   Lobby scene (where the shop shelves live) has loaded again, so there
   was nothing to read from. Replaced entirely by this in-Lobby popup.
2. **Wiring bug**: an earlier version of this doc said to put
   `BatchUnlockPopupUI` directly on `Panel` — see "Why the script and
   Panel are separate objects" above for why that silently broke
   everything.
3. **Sync race**: even correctly wired, the popup still didn't show.
   `BatchUnlockPopupUI` did a one-shot state check in `Start()`, which
   can race `GameFlowManager`'s own spawn/sync timing on a joining/
   scene-loading client — the *exact* bug class `NightModeVisuals`
   already hit and fixed once before (issue #12). Fixed the same way:
   subscribes to `GameFlowManager.OnTimeOfDayChanged` instead of
   trusting a single `Start()` read.
4. **Design correction** (not a bug): the shelf's own unlock timing
   was actually working exactly as coded — the disagreement was about
   *which* Lobby items should unlock in. Originally built to unlock
   after a batch's Night round; the "Current behavior" section above
   reflects the corrected, intended design (unlocks before the Night
   round instead), via `GameFlowManager.EffectiveShopBatch`.

## Where to look

- `Assets/Scripts/UI/BatchUnlockPopupUI.cs` — the component.
- `Assets/Scripts/Core/GameFlowManager.cs` — `EffectiveShopBatch`,
  what both this popup and the shelf's unlock check read.
- `Assets/Scripts/UI/HotbarSlotUI.cs` — the spinning-preview technique
  this reuses (offscreen stage + RenderTexture), for reference if the
  preview doesn't look right and you want to compare against a known-
  working example already in the scene.
- `Assets/Scripts/Shop/ShopShelfItem.cs` — `UnlockBatch`/`Item`, what
  the popup queries to decide what's new.
- `Assets/Scripts/World/NightModeVisuals.cs` — the Night/Morning
  skybox timing this whole feature is keyed off of, and issue #12's own
  sync-race fix this popup's own fix reused.
- Issue [#61](https://github.com/joshuaclemons1/Rob-Everyone/issues/61).
