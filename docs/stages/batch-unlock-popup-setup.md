# Batch unlock popup — Editor setup (#61)

`BatchUnlockPopupUI.cs` is written and needs a Canvas hierarchy built by
hand in `Lobby.unity` and wired up in the Inspector — pure Editor work,
no code. The "ItemPreview" layer this reuses (same one `HotbarSlotUI`
already uses for its own spinning hotbar previews) already exists in
the project, so nothing to add there.

## What changed since the first attempt

The original #61 approach tried to append "Batch N unlocks: X, Y" text
onto the loading-screen message shown as a round ends. Confirmed via
real playtest that this never actually appeared — `HandleRoundEnded`
(where that text was built) runs while the *gameplay* scene is still
active, before the Lobby scene (where the shop shelves live) has
loaded again, so there was nothing to read from yet. That code has been
fully reverted.

The real, separate thing you noticed — the shelf itself staying
**Locked** in the Lobby right after playing the night round, and only
turning **Unlocked** in the Lobby after the *next* round — isn't a bug.
`NightModeVisuals` (in both `SampleScene` and `Lobby`) shows the
*upcoming* round's time of day, not the one that just finished:
`roundInBatch` (and so the Night skybox) updates at the *end* of the
previous round, before the scene change into Lobby even happens — so
the Lobby with the Night skybox is the one **previewing** the batch's
final round, not the one **after** it. The batch only actually
increments once that Night round is *played* and ends. So:

- "Night Lobby" (Night skybox, shop still Locked) = the Lobby right
  before playing the batch's 3rd/final round. Still the *old* batch —
  correctly still locked.
- "Morning of the next batch" (Morning skybox, shop Unlocked) = the
  Lobby right after that Night round ends. The *new* batch has already
  started — correctly unlocked.

Nothing to fix there; just a mislabeling of which Lobby is which.
`BatchUnlockPopupUI` is built to show in that second one — see its own
`RoundInBatch == 1 && BatchNumber > 1` check.

## Build the Canvas hierarchy

1. In `Lobby.unity`, create a new Canvas (or add to an existing
   full-screen UI Canvas already in the scene) — `Screen Space -
   Overlay` is fine, this doesn't need to render in-world.
2. Under it, add a `Panel` GameObject (`BatchUnlockPopup`), stretched to
   fill the screen, with a semi-transparent dark background image (an
   `Image` component, alpha around `0.6-0.75`) so the spinning item
   reads clearly against it.
3. Inside the panel:
   - A `TextMeshPro - Text (UI)` for the title, large, centered near
     the top third of the screen. Not bound to any text in the
     Inspector — `BatchUnlockPopupUI` sets it to `"Item Unlocked"` at
     runtime.
   - A `RawImage`, centered, sized noticeably larger than a hotbar
     slot's own preview (e.g. 400×400 or bigger) — this is what
     `modelImage` renders the spinning model onto.
   - A second `TextMeshPro - Text (UI)` below the RawImage for the
     item's name (e.g. "Bat", "Alarm Clock").
4. Add the `BatchUnlockPopupUI` component to the `BatchUnlockPopup`
   panel GameObject itself (or a manager object elsewhere in the scene
   — doesn't need to be on the panel, just needs a reference to it).
5. Wire the Inspector fields:
   - `Panel` → the `BatchUnlockPopup` GameObject itself.
   - `Title Text` → the first TMP text.
   - `Item Name Text` → the second TMP text.
   - `Model Image` → the RawImage.
   - Leave `Preview Padding` / `Spin Speed` / `Seconds Per Item` at
     their defaults to start (`1.3`, `40°/sec`, `3s`) — tune by eye
     once you can see it running.
6. Make sure the panel starts inactive in the scene (or just trust
   `Start()` — it calls `panel.SetActive(false)` immediately, then only
   activates it if there's actually something new to show).

🔴 **Rest point**: play through to the Lobby right after a batch's
final (night) round ends — the one your earlier test correctly found
already-unlocked. You should see "Item Unlocked" + the item's name +
a large spinning model for each newly-unlocked item, a few seconds
each, then nothing (panel hides itself). The Lobby *before* that (the
Night-skybox one) should show nothing, since `BatchNumber` hasn't
incremented yet there.

## Where to look

- `Assets/Scripts/UI/BatchUnlockPopupUI.cs` — the new component.
- `Assets/Scripts/UI/HotbarSlotUI.cs` — the spinning-preview technique
  this reuses (offscreen stage + RenderTexture), for reference if the
  preview doesn't look right and you want to compare against a known-
  working example already in the scene.
- `Assets/Scripts/Shop/ShopShelfItem.cs` — `UnlockBatch`/`Item`, what
  the popup queries to decide what's new.
- `Assets/Scripts/World/NightModeVisuals.cs` — its own comment is what
  explains the "Night Lobby previews the *next* round" behavior above.
- Issue [#61](https://github.com/joshuaclemons1/Rob-Everyone/issues/61).
