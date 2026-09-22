# Player animations — carry gait + use one-shots

Adds the carry walk/run, a pick-up gesture, and one-handed shoot / bat
swing to the player Animator. Mostly a one-click controller rebuild plus
a few Inspector values.

**Issue #50 update**: jumping while moving (a running jump / bhop
chain) now plays an arms-only overlay instead of snapping the whole
body into the full-body Jump pose — see its own section below. Requires
re-running the controller rebuild (step 1) to take effect.

This did **not** originally cover "the selected hotbar item shows in
your hand" — that was a separate pass, `HeldItemDisplay`, built shortly
after this one and now done too (see
[issue #31](https://github.com/joshuaclemons1/Rob-Everyone/issues/31)).

## What changed (code)

- **`ItemDefinition`** gained a **Use Animation** dropdown
  (`None` / `Swing` / `Shoot`) — which arm one-shot fires when the item
  is used. Decoupled from `SabotageType` on purpose: the Taser is
  `Melee`-typed but reads better as a point-and-zap `Shoot`.
- **`PlayerAnimationDriver`** drives four new things on the Animator:
  - `Carrying` (bool) — read straight off `CarryController`'s SyncVar,
    so every client sees it with no extra sync. Toggles the base layer
    between normal locomotion and the carry gait.
  - `Action` (trigger) + `ActionType` (float 0–3) — fires a one-shot on
    a new **upper-body Action layer**. `PlayAction(PickUp/Shoot/Swing/
    ReceiveHit)` triggers locally for the owner and relays to observers
    via Command→ClientRpc, same pattern as the jump trigger.
  - The driver fades the Action layer's weight in/out (`Action Blend
    Speed`, default 8) so the arms blend back to the base pose over the
    clip's tail instead of snapping.
- **`Interactor`** plays `PickUp` when you `E` a loot pickup (not for a
  Sell Station / Ready Spot — those are just a touch).
- **`CarryController`** plays `PickUp` the frame you `E`-grab a downed
  rival.
- **`SabotageUseController`** plays the item's `Use Animation` on
  click — on press, whether or not the swing/shot connects (a whiffed
  swing still animates).
- **`PlayerActionAnim.ReceiveHit`** (ActionType 3, `RecieveHit` clip) is
  wired but nothing calls it yet — reserved for a future non-ragdoll
  hit reaction.

## Issue #50: arms-only jump while moving/bhopping

Previously every jump — standing still or mid-sprint — played the same
full-body `Jump` state on the base layer, yanking the legs out of the
run cycle on every hop. Fixed by routing a *moving* jump through the
existing Action layer instead of the base layer at all:

- **`PlayerActionAnim`** gained `Jump = 4` — the arms-only overlay,
  fired through the exact same `PlayAction`/Command→ClientRpc path
  PickUp/Shoot/Swing already use (no new networking plumbing).
- **`PlayerAnimationDriver.HandleJumped`** now checks
  `ShouldUseArmsOnlyJump()` first: if the player's `HorizontalSpeed` is
  above `Moving Jump Speed Threshold` (default 0.1) **and** they aren't
  carrying, it calls `PlayAction(PlayerActionAnim.Jump)` and returns —
  the base layer never leaves `Locomotion`, so the legs keep running
  the whole time. Otherwise (standing still, or carrying — carrying
  already has its own distinct `jumpToCarry` transition) it's the exact
  same full-body `Jump` trigger as before this issue, byte-for-byte
  unchanged.
- **The Action blend tree** gained a 5th child: the same `Jump` clip
  the base layer's own `Jump` state uses, at `ActionType` threshold 4,
  masked to the upper body like every other Action-layer clip.

Deliberately does **not** rescale the arms-only clip's playback speed
to match real air time the way the base layer's own Jump state does via
`JumpSpeed` — that rescaling exists to land a *full-body* pose in sync
with ground contact, which doesn't apply here since the legs are
already handling real movement continuously; the arms overlay just
plays as a quick one-shot at its own authored speed and fades out on
its own timeline (same as PickUp/Shoot/Swing already do), which also
means bhop re-triggering doesn't need any new relay plumbing for a
speed multiplier.

**Known risk, flagged in the issue itself**: the Action layer's
fade-in/fade-out timing (`Action Blend Speed`, the 80%-through
crossfade-back) was tuned for one-shot item actions, which don't repeat
anywhere near bhop speed. A very fast bhop chain might re-trigger the
arms overlay before the previous one's fade-out finishes — genuinely
needs a real playtest to see whether that looks fine (it re-triggers
and restarts cleanly, same as PickUp does today) or needs its own
tuning pass.

## What the tool builds

`Assets > Rob Everyone > Rebuild Player Animator`
([PlayerAnimatorBuilder.cs](../../Assets/Scripts/Editor/PlayerAnimatorBuilder.cs))
rewrites **`PlayerAnimator.controller`** in place (its GUID is
unchanged, so `PlayerSkinSpawner`'s reference survives) and
(re)generates **`PlayerActionMask.mask`** next to it:

- **Base Layer** — `Locomotion` (Idle/Walk/Run blend on `Speed`),
  `CarryIdle` (Walk_Carry frozen on frame 0 — the "standing, arms full"
  pose, since there's no Idle_Carry clip), `CarryMove` (Walk_Carry/
  Run_Carry blend on `Speed`), `Jump`. `Carrying` swaps between them;
  the Jump state is gated to `Carrying == false`.
- **Action Layer** — upper-body override, masked to the spine + both
  arms (built from `BaseCharacter.fbx`'s own bone names), weight driven
  from code. `None` (empty default) → `Action` blend tree selecting
  PickUp / Shoot_OneHanded / SwordSlash / RecieveHit / **Jump** (issue
  #50 — the arms-only bhop/moving-jump overlay) by `ActionType`.

Re-runnable any time; it wipes and rebuilds, so nothing accumulates.

## Editor steps

### 1. Rebuild the controller

Run **`Assets > Rob Everyone > Rebuild Player Animator`**. Watch the
Console for the confirmation log (or a "missing clips" dialog — if that
shows, the `BaseCharacter.fbx` import is the problem, fix that first).

### 2. Check the clip loop settings on `BaseCharacter.fbx`

Select
`Assets/Art/Characters/Quaternius-UltimateAnimatedCharacterPack/FBX/BaseCharacter.fbx`
→ **Animation** tab → **Clips** list. Quaternius usually ships these
right, but verify:

| Clip | Loop Time |
|---|---|
| `Idle`, `Walk`, `Run`, `Walk_Carry`, `Run_Carry` | **ON** |
| `Jump`, `PickUp`, `Shoot_OneHanded`, `SwordSlash`, `RecieveHit` | **OFF** |

If you change any, hit **Apply**, then re-run the tool (step 1) so the
controller picks up the reimported clips.

### 3. Set `Use Animation` on the sabotage items

In `Assets/Data/Items/`, on each `ItemDefinition`:

| Item | Use Animation |
|---|---|
| Baseball Bat | `Swing` |
| Hammer | `Swing` |
| Taser | `Shoot` |
| Tranquilizer Gun | `Shoot` |
| Dynamite | `Swing` |
| Alarm Clock | `Swing` |
| all regular loot | `None` (leave default) |

### 4. Player prefab — nothing to wire

`Carrying` is read from `CarryController` automatically; the action
hooks are `GetComponent` calls in `Awake` on components already on the
Player root (`Interactor`, `CarryController`, `SabotageUseController`
all sit next to `PlayerAnimationDriver`). The only new Inspector field
is **Action Blend Speed** on `Player Animation Driver` — leave it at 8.

## 🔴 Rest points (playtest)

1. **Nothing regressed** — walk/run blend and the jump clip look exactly
   as before.
2. **Carry gait** — pick up a ragdolled rival: walking plays
   `Walk_Carry`, sprinting `Run_Carry`, standing still holds the frozen
   carry pose. No jump animation plays while carrying (a single hop
   still moves you, it just doesn't animate).
3. **Pick-up gesture** — `E` on loot plays `PickUp` on the arms only;
   you can keep walking through it. `E`-grabbing a body plays `PickUp`
   then flows into the carry gait.
4. **Use one-shots** — Taser / Tranq Gun play `Shoot_OneHanded` on the
   upper body while your legs keep running; Bat / Hammer play
   `SwordSlash`. A missed swing still animates.
5. **Two Editors** — the observer sees every one of the above on the
   other player (carry gait, pickup, shoot, swing), not just the owner.
6. **Issue #50 — moving jump** — sprint and jump: legs should stay in
   the Run cycle the whole time, arms play a quick jump gesture. Standing
   still and jumping should look exactly like it always has (full-body
   pose). Bhop chain (jump repeatedly while sprinting, landing and
   re-jumping fast): watch specifically for the arms overlay
   re-triggering cleanly vs. looking glitchy/interrupted — this is the
   one part of #50 that's a real unknown until played, not just a
   values-tuning pass. Jumping while carrying should be unchanged
   (still no jump animation, per rest point 2).

Then tell me and it gets filed as a closed, `enhancement`-labeled GitHub Issue.
