# Player animations — carry gait + use one-shots

Adds the carry walk/run, a pick-up gesture, and one-handed shoot / bat
swing to the player Animator. Mostly a one-click controller rebuild plus
a few Inspector values.

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
  PickUp / Shoot_OneHanded / SwordSlash / RecieveHit by `ActionType`.

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

Then tell me and it gets filed as a closed, `enhancement`-labeled GitHub Issue.
