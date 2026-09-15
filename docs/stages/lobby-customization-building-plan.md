# In-Lobby customization building — implementation plan (issue #52)

Scoping pass before writing code. Grounded in reading the actual current
code (`PlayerSkinSpawner`, `ShopShelfItem`, `CustomizationUI`,
`Interactor`, `PlayerCosmeticSelection`), not just re-stating the issue.
Five phases, ordered so each one is independently useful/testable before
the next starts, and so the riskiest, least-precedented piece (the
mirror) comes last rather than blocking everything behind it.

## Phase 1 — live skin/color swap capability (code-only)

The real technical gap, exactly as the issue itself flags: nothing can
change a player's skin/color after spawn today.
`PlayerSkinSpawner.SpawnSkin` has a hard `if (SkinInstance != null)
return;` guard, and four other scripts cache a reference *into* that
one spawned instance with no reset path:

| Script | What it caches |
|---|---|
| `PlayerAnimationDriver` | `animator` (via `TryResolveAnimator`'s own `if (animator != null) return true;` guard) |
| `PlayerRagdoll` | `hipsRigidbody`, `ragdollBodies`, `restLocalPositions`, `allBones` (via `TryInitializeRagdoll`'s `initialized` guard) |
| `PlayerHeadTalkScale` | `headBone` |
| `HeldItemDisplay` | `handBone` |

**Design**: add a `public event Action OnSkinRebuilt;` on
`PlayerSkinSpawner`, fired once a rebuilt skin instance is fully set up
(same spot `SpawnSkin` already finishes its own setup today). Each of
the four scripts above subscribes and re-resolves its own cached
reference in the handler — the same event-driven decoupling
`PlayerCosmeticSelection.OnChanged` already uses successfully elsewhere
in this project (`MenuCharacterPreview` reacts to it independently),
not a new pattern.

**Networking shape** — reusing `ShopShelfItem`'s confirmed-server-side
`Interact()` pattern (`Interactor.CmdInteract` resolves and calls
`Interact()` on the **server**, never on a client) means the trigger
for a swap arrives server-side, not owner-initiated the way the
original spawn is. That needs two new pieces on `PlayerSkinSpawner`,
distinct from the existing owner-initiated `CmdSetCosmetics`:

- `[Server] public void ServerSwapCosmetics(int? skinIndex, int? colorIndex)`
  — a plain `[Server]`-tagged method (matching `CarryController.
  ServerAttach`/`ServerDetach`'s existing style), callable directly by
  a pedestal/paint-can's own server-side `Interact()`. Updates
  `syncedSkinIndex`/`syncedColorIndex`, rebuilds the skin on the
  server's own authoritative copy (matters for ragdoll physics), and
  the SyncVar write already makes every *other* client's existing
  `OnCosmeticsChanged` hook rebuild too — unchanged, still skips the
  owner exactly as it does today.
- `[TargetRpc]` back to the interacting player specifically (same
  pattern `GameFlowManager` already uses 3x) — the **owner** learns
  about their own swap this way instead of through
  `OnCosmeticsChanged` (which stays owner-skipped, exactly as-is,
  since the *initial* spawn still doesn't need a round trip). The
  handler rebuilds the owner's own local skin **and** writes
  `PlayerCosmeticSelection.SkinIndex`/`ColorIndex` — the one thing the
  server can never do on the owner's behalf, since that's local
  PlayerPrefs — so the choice is remembered next session, matching
  what the old menu screen already did.

**Testable before Phase 2 exists at all**: once this lands, the swap
path can be smoke-tested by temporarily calling
`ServerSwapCosmetics`/the owner path from anywhere convenient (a debug
key, or just trusting the logic through code review) — but honestly,
building Phase 2 immediately after is probably more useful than trying
to test this in isolation with no real trigger.

## Phase 2 — interaction scripts (code-only)

Two new scripts, both copying `ShopShelfItem`'s exact shape
(`IInteractable`, `[RequireComponent(typeof(Collider))]`,
`InteractionPrompt`/`CanInteract`):

- **`SkinPedestal.cs`** — `[SerializeField] private int skinIndex;`,
  `Interact()` calls `ServerSwapCosmetics(skinIndex, null)` (color
  untouched).
- **`PaintCan.cs`** — `[SerializeField] private int colorIndex;`,
  `Interact()` calls `ServerSwapCosmetics(null, colorIndex)`.

No unlock gating on either (explicitly out of scope here, deferred to
#25) — `CanInteract` is always true, no `InteractionPrompt` branch for
a locked state the way `ShopShelfItem` has one.

## Phase 3 — the physical building (Editor-heavy)

This is real level-design/placement work — not something to
hand-author blind the way a single new component reference is. Needs
your own Editor hands: block out a room (reusing the Pawn Shop's own
kit pieces for visual consistency is the obvious starting point),
place a mirror-facing area, and place one pedestal + one paint can per
roster/palette entry.

**One thing worth building to make that placement less painful**: the
skin roster has ~52 entries — placing and wiring 52 pedestals one at a
time by hand is a lot of tedium. Worth a small Editor tool, same spirit
as `PlayerAnimatorBuilder`/`RagdollBatchTool`: given a set of
hand-placed empty marker Transforms (fast to rough in, no per-marker
configuration needed) and the `PlayerSkinRoster`, one button
instantiates a preview model + `SkinPedestal` (with `skinIndex` already
wired) at each marker in roster order. Turns "52 manual drag-and-drop
placements" into "rough in 52 empty markers + click a button." Worth
building this *as part of* Phase 3, not deferring — it's the difference
between this phase taking an evening and taking most of a day.

## Phase 4 — the mirror (hardest, most novel, do last)

Flagging plainly, same as the issue itself does: no existing precedent
in this project. `HotbarSlotUI`'s item previews are an isolated
camera-on-a-stage, not a reflection — a real mirror needs a second
camera positioned as the true optical reflection of whichever camera
is currently rendering the player (position and rotation mirrored
across the mirror's own plane, recomputed every frame), rendering to a
`RenderTexture` displayed on the mirror surface's own material.

Deliberately last, not because it's less wanted (you already chose "a
real reflection" over the cheaper preview-camera shortcut when this was
first scoped) but because it's the one piece with real correctness risk
(reflection-plane math is fiddly to get exactly right without seeing
it) and zero dependency from anything else in this feature — pedestals
and paint cans are fully usable and testable with no mirror at all.
Worth throttling once built (reduced resolution and/or only rendering
while a player is actually near/facing it) rather than paying a second
full camera pass every frame regardless of visibility, especially with
several players' clients all doing this at once.

## Phase 5 — remove the Main Menu Customization screen

Only once Phases 1–4 are confirmed working in a real playtest — delete
`CustomizationUI.cs` and its Main Menu scene wiring (the Customize
panel, Next/Previous buttons, swatch grid), per the issue's own
explicit "replaces, not supplements" scope. Not before, so there's
always a working way to change skins while this is mid-build.

## Suggested order to actually build in

1 → 2 (code-only, no Editor time needed yet, get the mechanism fully
working and reviewed) → 3 (Editor placement + the batch-placement
tool) → playtest 1–3 for real → 4 (the mirror, isolated) → playtest
again → 5 (cleanup, once everything above is confirmed).

## Where to look

- `Assets/Scripts/Player/PlayerSkinSpawner.cs` — Phase 1.
- `Assets/Scripts/Player/PlayerAnimationDriver.cs`,
  `PlayerRagdoll.cs`, `PlayerHeadTalkScale.cs` (`Assets/Scripts/Voice/`),
  `HeldItemDisplay.cs` — the four `OnSkinRebuilt` subscribers.
- `Assets/Scripts/Shop/ShopShelfItem.cs` — the interaction pattern
  Phase 2 copies.
- `Assets/Scripts/Interaction/Interactor.cs` — confirms `Interact()`
  runs server-side, which is why Phase 1 needs a `[TargetRpc]` back to
  the interactor rather than relying on the owner's own client to
  trigger anything directly.
- `Assets/Scripts/Customization/PlayerCosmeticSelection.cs` — the
  local-persistence half the `[TargetRpc]` handler writes to.
- `Assets/Scripts/UI/CustomizationUI.cs` — what Phase 5 removes.
- `Assets/Scripts/Editor/PlayerAnimatorBuilder.cs` — the style of
  Editor batch-tool Phase 3's pedestal placer should follow.
- #25 — the deferred Cash-gating half of this feature (not in scope
  here).
