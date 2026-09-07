# Stage 7b — Batch economy + hotbar Editor setup

Scripts are already written (`HotbarController`, `HotbarSlotUI`,
`HotbarUI`, plus changes to `PlayerInventory`, `PickupItem`,
`RoundManager`, `GameFlowManager`, `RoundUI`). This is what to wire up by
hand in the Unity Editor.

**What this adds**: quota now grows ×1.5 every 3-round batch instead of
staying fixed forever, with a Cash surplus wipe at each batch boundary
(anti-hoarding). Carried loot becomes a real 5-slot inventory — a
Minecraft-style hotbar at the bottom of the screen, selectable with
number keys 1-5 or the scroll wheel, with a 6th item simply staying in
the world if all 5 slots are full.

**No new scenes or Build Settings changes needed** — this only touches
UI/logic already living in `SampleScene` and `Lobby`.

## 1. Build the hotbar (in `SampleScene` first)

Build this in `SampleScene`'s existing Canvas first, verify it there,
*then* prefab it and place a copy in `Lobby` — same lesson learned from
`CashHUD` in `stage7-shop-lobby-setup.md` (it's much easier to catch a
Canvas-parenting or Canvas-Scaler mismatch while everything's still a
normal scene object than after it's a prefab living in two scenes).

1. Right-click directly on the `Canvas` GameObject in the Hierarchy →
   **Create Empty**. Rename it `Hotbar`. (Right-clicking `Canvas`
   specifically, not empty Hierarchy space, guarantees this is created
   already parented inside it — see the earlier `CashHUD` bug for what
   goes wrong if it isn't.)
2. Position `Hotbar`'s **Rect Transform**: set **Anchor Min/Max** to
   `(0.5, 0)` (bottom-center) via the anchor preset picker (hold
   Alt+Shift while clicking a preset to also set the pivot to match), and
   **Anchored Position** to roughly `(0, 60)` (just above the bottom
   edge) — exact numbers aren't critical, adjust visually afterward.
3. Under `Hotbar`, create 5 child GameObjects named `Slot0` through
   `Slot4`. For each:
   - Add component **Image** (this is the slot's background box —
     assign any placeholder sprite, or leave it as Unity's default white
     square for now).
   - Set its **Rect Transform** size to something square, e.g. `64 x 64`.
   - Add a child **TextMeshPro - Text** object inside it (for the item
     name), centered, reasonably small font size (item names need to
     fit in a 64px box).
   - Add component **Hotbar Slot UI** (`RobEveryone.UI`) directly on the
     slot GameObject (not the text child). Drag its own child text object
     into **Item Text**.
   - Position `Slot0`...`Slot4` left to right along `Hotbar`'s Anchored
     Position X (e.g. `-140, -70, 0, 70, 140` for 5 slots with a bit of
     gap, adjust to taste).
4. On `Hotbar` itself, add component **Hotbar UI** (`RobEveryone.UI`).
   Drag `Slot0` through `Slot4` into its **Slots** array **in left-to-
   right order** (index 0 = leftmost) — this order matters, since it's
   what number key 1-5 maps to. Leave **Inventory** empty (auto-found).
5. On the **Player** GameObject (in `SampleScene`), add component
   **Hotbar Controller** (`RobEveryone.Inventory`) — this is what
   actually reads number keys 1-5 and the scroll wheel; without it,
   items will still display correctly but selecting between slots won't
   do anything.
6. **Verify before making the prefab**: press Play, pick up an item —
   confirm its name appears in the leftmost empty slot box. Press `2`,
   `3`, etc. — confirm the corresponding slot visibly grows slightly
   (the `Selected Scale` on `Hotbar Slot UI`, default `1.15`). Scroll the
   mouse wheel — confirm selection moves and wraps around at both ends.
   Pick up items until all 5 slots are full, then try to pick up a 6th —
   confirm it stays in the world instead of disappearing.
7. Once confirmed working, drag `Hotbar` from the Hierarchy into your
   Project window (e.g. `Assets/Prefabs/UI/Hotbar.prefab`) to make it a
   prefab.

## 2. Place the hotbar in the Lobby

1. Open `Lobby.unity`. Drag the `Hotbar` prefab from the Project window
   into its Canvas.
2. **Check its Rect Transform values match the prefab's own** (same
   `CashHUD` lesson: a stray Reset or manual drag can leave a position
   override diverging from the prefab). If it looks shifted or oddly
   sized compared to `SampleScene`, right-click the overridden fields
   (shown in bold/blue) and choose **Revert**.
3. Nothing else needed for the Player here — **Hotbar Controller** was
   already added in `SampleScene` (step 1.5 above), and carries over
   automatically via `GameFlowManager`'s `DontDestroyOnLoad`.

## 3. Confirm the batch economy fields

1. Select the `GameFlowManager` GameObject in `SampleScene`.
2. Confirm the new **Quota Growth Multiplier** field reads `1.5` (this
   is the default — only change it if you want a different curve).

Nothing else needs wiring — `RoundManager`, `RoundUI`, and `CashBarUI`
all read the new batch state automatically through the changes already
made to their scripts.

## 3b. Loading screen (result message + spinner during the transition)

Added because the result/batch-progress banner was disappearing almost
instantly — the scene load used to start the same frame it appeared.
Now `GameFlowManager` shows the banner alone for a couple of seconds,
*then* covers the screen with this loading panel while the actual scene
load happens. This only needs to exist in `SampleScene` — like
`GameFlowManager` itself, it carries itself forward via
`DontDestroyOnLoad` once found.

1. In `SampleScene`, right-click empty Hierarchy space (not on the
   existing `Canvas`) → **UI → Canvas**. Rename it `LoadingScreenCanvas`.
   This is deliberately a *separate* Canvas from the main HUD one — it
   needs to persist independently, and a Canvas can't be "half
   persistent." Leave its Render Mode as **Screen Space - Overlay** (no
   blur effect needed here, so no camera required) and set its **Sort
   Order** to something high like `10` so it always renders on top of
   the scene's own HUD Canvas.
2. Under it, add an **Image** stretched to fill the entire screen (anchor
   preset: stretch both axes, all offsets `0`), solid dark color. Name it
   `Panel`. **Uncheck its Active checkbox** in the Inspector so it starts
   hidden.
3. Under `Panel`, add a **TextMeshPro - Text** element, centered, for the
   message. Add an **Image**, positioned in the center, for the loading
   animation — leave its **Source Image** empty for now, `LoadingScreenUI`
   sets it every frame.
4. On `LoadingScreenCanvas` itself (or a new empty child), add component
   **Loading Screen UI** (`RobEveryone.UI`). Wire: `Panel` = the `Panel`
   GameObject, `Message Text` = your TMP text, `Spinner Image` = the
   Image from step 3. Drag all 5 of your loading-animation sprites into
   **Spinner Frames**, in playback order. Leave **Frames Per Second** at
   `8` (how fast it cycles through the 5 sprites) — raise or lower to
   taste once you see it in motion.
5. On `GameFlowManager`, confirm the new **Result Display Duration**
   field reads `2.5` (seconds the banner shows alone before the loading
   screen takes over) — tune to taste.
6. Test: finish a round, confirm the result text is now clearly readable
   for ~2.5s, then the loading panel (spinner + same message) covers the
   screen while the Lobby loads, then disappears once it's ready.

## 4. Test the full loop

1. Press Play. Note the starting quota shown in the HUD (should read
   `$200`).
2. Complete round 1 (any way — exit, timer, caught). In the Lobby,
   confirm the result banner reads something like `Batch progress: $X /
   $200` (not a pass/fail message) unless you got Caught, in which case
   it should still say `Caught by the police!`.
3. Ready up, complete round 2 — same check, still `Batch progress: ...`.
4. Ready up, complete round 3 (the batch's final round) — confirm the
   banner now reads either `Batch quota met!` or `Batch quota not met.`
   (a real verdict, not progress).
5. Back in the gameplay scene for the new batch (round 1 of batch 2),
   confirm the HUD quota text now reads `$300` (200 × 1.5).
6. If you had more Cash than quota going into that 3rd round, confirm it
   was clamped down to exactly the quota amount, not carried over in
   full, once you're back and check the Cash readout.
7. Confirm the hotbar itself: pick up items, sell at the Sell Station in
   the Lobby, confirm all 5 slots empty out and the Cash readout
   updates.

If the banner text looks wrong (e.g. always shows progress, never a
verdict) — check the Console for a null reference; the most likely cause
is `GameFlowManager.Instance` being null when `RoundUI` tries to
subscribe, which would mean `GameFlowManager` isn't in the scene, or
`RoundUI`'s `OnEnable` ran before `GameFlowManager.Awake()` set
`Instance` (shouldn't happen given Script Execution Order, but worth
checking `GameFlowManager` is present in `SampleScene` if this comes up).
