# In-Lobby customization building — implementation plan (issue #52)

Scoping pass before writing any code, revised after a design change:
instead of ~52 always-available skin pedestals (one per roster entry),
a small number of pedestals (5–6) each offer one **randomly rolled**
skin, re-rolled fresh every time the Lobby loads — interacting
**unlocks** that skin into the player's own persistent collection,
rather than selecting it outright. A separate mirror interaction lets
a player cycle (next/previous) through whichever skins they've
actually unlocked so far. Colors stay simple — the full palette (8
entries, a small fixed array) is always available, no rotation.

Grounded in reading the actual current code (`PlayerSkinSpawner`,
`ShopShelfItem`, `CustomizationUI`, `Interactor`,
`PlayerCosmeticSelection`, `GameFlowManager.HandleRoundEnded`), not
just restating the ask.

## Design notes worth stating explicitly

- **"Each lobby phase" = every round-end**, not just every batch —
  confirmed by reading `GameFlowManager.HandleRoundEnded`:
  `ServerChangeScene(lobbySceneName)` runs after *every* round, not
  only the batch's 3rd. That's actually convenient: `Lobby.unity` is a
  single-mode scene, so `ServerChangeScene` fully unloads and reloads
  it every time — every scene-placed `NetworkIdentity` in it (offer
  pedestals included) gets destroyed and freshly recreated by Unity
  automatically. An offer pedestal can just roll its random skin once
  in its own `OnStartServer()` and get "reroll every lobby phase" for
  free, with no new event-hooking needed anywhere.
- **The offer is shared, not personalized** — a pedestal is a networked
  world object every player in the Lobby sees at once (same as a Pawn
  Shop shelf), so all players see the *same* 5–6 rolled skins each
  phase. What's personal is *unlocking* one into your own collection —
  two different players can both interact with the same pedestal and
  each add that skin to their own separate unlocked set. This is the
  natural fit for Mirror's SyncVar model (one shared value broadcast to
  every observer) rather than something to fight against.
- **This subsumes #54's core ask.** #54 ("skin/color unlocks should be
  permanent and per-player") already found that *selected*-skin
  persistence exists today (`PlayerCosmeticSelection`), and that the
  missing piece is a persisted *set of unlocked* skins — which this
  plan's Phase 0 *is*. Once Phase 0 lands, #54 is effectively resolved
  as a side effect; worth updating/closing it then rather than treating
  it as separate work.
- **No Cash-gating here** — same as the original scope, still deferred
  to #25. An "unlock" today is free/automatic the instant you interact;
  #25's future work is adding a cost check at that same interaction
  point, not restructuring anything built here.
- **A batch-placement Editor tool is no longer needed.** The original
  plan proposed one specifically because placing ~52 individual skin
  pedestals by hand was a lot of tedium. At 5–6 offer pedestals + 8
  paint cans + 2 mirror cycle buttons (~15–16 objects total), that's
  squarely hand-placement territory — cut from the plan.

## Phase 0 — persistent per-player skin unlocks (code-only)

New `PlayerSkinUnlocks` static class
(`Assets/Scripts/Customization/`), same shape/pattern as
`PlayerCosmeticSelection` (PlayerPrefs-backed, `OnChanged` event) but
holding a **set** of unlocked skin indices instead of one selected
index — PlayerPrefs has no native array/set type, so stored as a
delimited string under one key, parsed into a `HashSet<int>` on read.

- `IsUnlocked(int skinIndex)` — skin index `0` is always implicitly
  unlocked (so a brand-new player has *something* valid to wear/spawn
  with before ever visiting a Lobby at all); everything else checks
  the stored set.
- `Unlock(int skinIndex)` — idempotent (adding an already-unlocked
  index is a harmless no-op, not an error) — a player can interact with
  a pedestal offering a skin they already have with nothing bad
  happening.
- `UnlockedSkins` — read-only enumerable, what the mirror's
  next/previous cycling below iterates.

## Phase 1 — live skin/color swap capability (code-only)

Unchanged from the original plan — still the real prerequisite
regardless of the pedestal redesign, since actually *wearing* a
different skin (whether chosen via the mirror or, before this issue,
never possible at all) still needs a genuine "swap live" path.
`PlayerSkinSpawner.SpawnSkin` currently has a hard `if (SkinInstance !=
null) return;` guard, and four other scripts cache a reference *into*
that one spawned instance with no reset path:

| Script | What it caches |
|---|---|
| `PlayerAnimationDriver` | `animator` (via `TryResolveAnimator`'s own `if (animator != null) return true;` guard) |
| `PlayerRagdoll` | `hipsRigidbody`, `ragdollBodies`, `restLocalPositions`, `allBones` (via `TryInitializeRagdoll`'s `initialized` guard) |
| `PlayerHeadTalkScale` | `headBone` |
| `HeldItemDisplay` | `handBone` |

**Design**: add a `public event Action OnSkinRebuilt;` on
`PlayerSkinSpawner`, fired once a rebuilt skin instance is fully set
up. Each of the four scripts above subscribes and re-resolves its own
cached reference in the handler — the same event-driven decoupling
`PlayerCosmeticSelection.OnChanged` already uses successfully
elsewhere in this project.

**Networking shape** — `Interactor.CmdInteract` resolves and calls
`Interact()` on the **server only** (confirmed by reading
`Interactor.cs`), so a pedestal/mirror-triggered swap arrives
server-side, unlike the owner-initiated original spawn. Two new pieces
on `PlayerSkinSpawner`:

- `[Server] public void ServerSwapCosmetics(int? skinIndex, int? colorIndex)`
  — plain `[Server]`-tagged method (matching `CarryController.
  ServerAttach`/`ServerDetach`'s existing style). Updates
  `syncedSkinIndex`/`syncedColorIndex`, rebuilds the skin on the
  server's own authoritative copy, and the SyncVar write already makes
  every *other* client's existing `OnCosmeticsChanged` hook rebuild too
  — unchanged, still skips the owner exactly as it does today.
- `[TargetRpc]` back to whichever connection triggered the swap (same
  pattern `GameFlowManager` already uses 3x) — the **owner** learns
  about their own swap this way, rebuilds their own local skin, and
  writes `PlayerCosmeticSelection.SkinIndex`/`ColorIndex` so the choice
  is remembered next session — the one thing the server can never do on
  the owner's behalf, since that's local PlayerPrefs.

## Phase 2 — interaction scripts (code-only)

Three scripts now, not two:

- **`PaintCan.cs`** — unchanged from the original plan. One per
  palette entry (8 total), `[SerializeField] private int colorIndex;`,
  `Interact()` calls `ServerSwapCosmetics(null, colorIndex)`. Always
  available, no unlock/lock state.
- **`SkinOfferPedestal.cs`** (replaces the original flat
  `SkinPedestal.cs`) — the redesigned piece:
  - `[SyncVar] private int offeredSkinIndex;` rolled once in
    `OnStartServer()` — uniformly random from the full roster, and
    (worth doing, cheap given only 5–6 pedestals) sampled without
    replacement across pedestals *within the same phase* so the same
    lobby visit never shows the same skin twice.
  - A SyncVar hook spawns a preview model (`skinRoster.
    GetSkin(offeredSkinIndex)`, instantiated directly in world space
    standing on the pedestal — a real 3D object, not a
    `HotbarSlotUI`-style render-texture trick, since this already lives
    in the 3D Lobby world) on every client, including late joiners
    (SyncVar hooks fire on initial sync too, so this needs no separate
    catch-up logic).
  - `Interact()` (server-side) sends a new `[TargetRpc]
    TargetNotifySkinUnlocked(int skinIndex)` to the interactor's own
    connection; the handler calls `PlayerSkinUnlocks.Unlock(skinIndex)`
    locally. Deliberately routed through the server (not a pure
    client-side write) even though nothing gates it yet — keeps the
    door open for #25's future Cash check to slot into this exact same
    point later without restructuring anything.
- **`MirrorSkinCycleButton.cs`** (new, two placed instances — Next and
  Previous, mirroring the old menu's two buttons as two small physical
  interactables rather than one dual-purpose one) — `Interact()`
  sends a `[TargetRpc]` telling the interactor's own client to advance;
  the **client-side** handler computes the actual next/previous index
  from `PlayerSkinUnlocks.UnlockedSkins` (the only place that data
  exists) and calls the same swap-and-persist path Phase 1 already
  built. Routed through the standard `IInteractable`/`Interactor` flow
  for consistent prompt/crosshair UI, even though the actual
  "which skin is next" computation has to happen client-side.

## Phase 3 — the physical building (Editor-heavy)

Real level-design/placement work, not something to hand-author blind.
Needs your own Editor hands: block out a room (reusing the Pawn Shop's
kit pieces for visual consistency), place 5–6 `SkinOfferPedestal`
markers, 8 `PaintCan` objects, a mirror-facing area, and the two
`MirrorSkinCycleButton` objects flanking it. Small enough now
(~15–16 objects) that this is genuinely just hand-placement, no
supporting Editor tool needed.

## Phase 4 — the mirror reflection rendering (hardest, most novel, do last)

Unchanged from the original plan. No existing precedent in this
project (`HotbarSlotUI`'s previews are an isolated camera-on-a-stage,
not a reflection) — a real mirror needs a second camera positioned as
the true optical reflection of whichever camera is currently rendering
the player, recomputed every frame, rendering to a `RenderTexture`
displayed on the mirror surface's own material. Deliberately last:
zero dependency from Phases 0–3 (the cycle buttons and pedestals are
fully usable and testable with no mirror image rendering at all — you
can already see the result on your own body/other players), and it's
the one piece with real correctness risk that needs actual Editor eyes
to get right. Worth throttling once built (reduced resolution and/or
only rendering while a player is actually near/facing it).

## Phase 5 — remove the Main Menu Customization screen

Only once Phases 0–4 are confirmed working in a real playtest — delete
`CustomizationUI.cs` and its Main Menu scene wiring, per the issue's
own "replaces, not supplements" scope.

## Suggested order to actually build in

0 → 1 → 2 (all code-only, no Editor time needed yet) → 3 (Editor
placement) → playtest 0–3 for real → 4 (the mirror, isolated) →
playtest again → 5 (cleanup).

## Tabled for a future update (explicitly out of scope here)

Mix-and-match cosmetics — selecting head and body separately (e.g. a
ninja head with a chef body) instead of one fixed whole-body skin, with
heads/bodies purchasable independently in the store. Real scope change
from "one skin index" to at least two independent selection axes, plus
whatever art/rigging work splitting the existing 52 whole-body skins
into interchangeable head/body pairs would need — explicitly **not**
part of this plan. Filed as its own tracked issue (see #57) rather than
folded in here, so it doesn't block Phases 0–5 above.

## Where to look

- `Assets/Scripts/Customization/PlayerCosmeticSelection.cs` — the
  existing pattern Phase 0's `PlayerSkinUnlocks` copies.
- `Assets/Scripts/Player/PlayerSkinSpawner.cs` — Phase 1.
- `Assets/Scripts/Player/PlayerAnimationDriver.cs`,
  `PlayerRagdoll.cs`, `PlayerHeadTalkScale.cs` (`Assets/Scripts/Voice/`),
  `HeldItemDisplay.cs` — the four `OnSkinRebuilt` subscribers.
- `Assets/Scripts/Shop/ShopShelfItem.cs` — the interaction pattern
  Phase 2's `PaintCan` copies directly.
- `Assets/Scripts/Interaction/Interactor.cs` — confirms `Interact()`
  runs server-side, which is why both the pedestal and the mirror
  buttons need a `[TargetRpc]` back to the interactor rather than
  relying on the owner's own client to trigger anything directly.
- `Assets/Scripts/Core/GameFlowManager.cs` (`HandleRoundEnded`) —
  confirms the Lobby reloads after every round, which is what makes
  `OnStartServer`-time rerolling work with no extra event wiring.
- `Assets/Scripts/UI/CustomizationUI.cs` — what Phase 5 removes.
- #25 — the deferred Cash-gating half of this feature.
- #54 — effectively resolved by Phase 0; revisit once it lands.
- #57 — the tabled head/body mix-and-match idea.
