# Completed work

A flowing log of everything actually built and confirmed, so `plan.md`
doesn't have to carry a growing essay. Grouped by system, roughly in the
order it landed. Each entry links to its detailed how-to doc where one
exists — those docs are kept as procedural reference (how it was built),
not status trackers; this doc and [todo.md](todo.md) are the status
trackers now.

**A note on trust**: this doc was reconstructed by cross-checking git
history against the existing docs, several of which had drifted out of
sync with what was actually committed (a doc claiming something was "not
done yet" when a later commit had already finished it, that kind of
thing). Treat this doc and `todo.md` as more current than any inline
status line inside an individual stage doc.

## Core offline loop (Stage 2–3)

- **Stage 2/3 base loop** — single house → multi-house map, quota/timer
  UI, an exit that ends the round. See
  [stage2-editor-setup.md](stages/stage2-editor-setup.md),
  [stage3-editor-setup.md](stages/stage3-editor-setup.md).
- **Homeowner AI** — Idle → Suspicious → Alerted vision-cone state
  machine, calls police on Alert. `HomeownerAI.cs`. See
  [stage3b-homeowner-setup.md](stages/stage3b-homeowner-setup.md).
- **Police AI** — Patrol → Respond → Search → Chase → Catch, NavMesh-driven,
  responds to any Homeowner's alert via a static event (no manual
  wiring). `PoliceAI.cs`. See
  [stage3c-police-setup.md](stages/stage3c-police-setup.md).
- **Character art** — Homeowner/Police reskinned with real Quaternius
  characters; `HomeownerAnimator` (Idle) and `PoliceAnimator`
  (Idle/Walk/Run Blend Tree). See
  [stage3d-character-art.md](stages/stage3d-character-art.md).
- **Real house prefab** — `Real_House_01`: real Kenney building + 40×40
  yard padding + Furniture Kit interior + nested Homeowner + loot spot,
  self-contained prefab. `DoorTeleporter.cs` (paired-trigger doorway
  workaround, no door-gap modeling needed). See
  [stage3e-house-prefabs.md](stages/stage3e-house-prefabs.md).
- **House pool** — `Real_House_02` also exists (2 real house prefabs
  total at the time). Both prefabs solo-tested (door, loot, homeowner,
  walk back out). See [stage3f-house-pool.md](stages/stage3f-house-pool.md).
  **`Real_House_03`** (Zach's design, built in `HouseDesigner.unity` and
  extracted into its own prefab) brings the pool to 3 — wired into both
  `SampleScene` and `MainMenu`'s spawner registrations, and rolls off
  `LootTable_Small` rather than `01`/`02`'s `Medium`, so the loot-variety
  note below is partially addressed.
- **Map slot layout + spawner** — `HousePoolSpawner.cs` randomly assigns
  a prefab per slot on `Start()`; `HomeownerAI`/`PoliceAI` auto-find the
  player if not hand-wired, so runtime-spawned houses need zero manual
  Inspector work. Full rectangular-ring layout (15 outer house slots + 2
  Good House slots + 3-building compound stack) matched to the team's
  actual map sketch. `OnDrawGizmos` draws a 40×40 wireframe box per slot
  at all times (not just Play mode) since houses don't exist until
  spawned. See [stage3g-map-layout.md](stages/stage3g-map-layout.md).
- **Roads, compound, exit** — road loop + driveways placed; compound
  fenced with a real chain-link/barbed-wire/gate kit (TampaJoey's Chain
  Link Fence Pack); police station placed with an essential interior (3
  jail cells + 2 desks); `Exit` repositioned away from the compound; the
  2 Good House slots repositioned to read as inside/adjacent to the
  compound. **Full-loop playtest confirmed working** — Stage 3 is
  feature-complete on real art. See
  [stage3h-map-dressing.md](stages/stage3h-map-dressing.md).
- **Skybox + background** — day skybox (Kenney Skyboxes pack, wired and
  live), background skyline, forest ring (also serves as the map's world
  boundary, `ForestRingSpawner.cs`), background hills. See
  [stage3i-skybox-skyline.md](stages/stage3i-skybox-skyline.md).
- **Traffic hazard** — cars drive one lap of the road loop and knock the
  player down (knockback + stun, not a catch) via a real per-limb ragdoll
  (Unity's Ragdoll Wizard). `CarDriver.cs`, `CarSpawnManager.cs`,
  `PlayerRagdoll.cs`. Built multiplayer-ready without adding networking:
  the ragdoll shows whichever skin `PlayerCosmeticSelection` has picked,
  hidden from the owner's own camera via Culling Mask (not `SetActive`)
  so a future networked player stays visible to others.
  **`RagdollBatchTool.cs`** (`Assets/Scripts/Editor/`) builds the ragdoll
  setup once on a template skin and copies it (correct bone-to-bone
  remapping, not blind copy-paste) onto the rest — all 52 skins in
  `Quaternius-UltimateAnimatedCharacterPack` now have a ragdoll variant
  in `Assets/Prefabs/PlayerSkins/_Ragdoll/`, `PlayerSkinRoster` updated
  to point at them. Playtested and confirmed working end to end. See
  [stage3j-traffic-hazard.md](stages/stage3j-traffic-hazard.md).
- **`HouseDesigner` scene** — a dev-tool scene with a `HouseTemplate`
  object, built to speed up creating further `Real_House_0X` variants for
  the pool (not a player-facing scene).

## Player movement & animation

- **Sprint/crouch/jump/bhop** — `FirstPersonController.cs`: sprint and
  crouch (resizes the `CharacterController` relative to original
  standing values, not recomputed from scratch, so standing height never
  drifts), jump, and real bhop-style air-strafe acceleration (id
  Software's air-accelerate formula).
- **Player feel pass** — jump is instant now (the launch applies the
  frame Space goes down; the old animation-anticipation delay is gone,
  `PlayerAnimationDriver` just fires the clip). Bhop tuned toward Source
  values (`airAcceleration` 100, `airWishSpeed` 1.0) with **hold-Space
  autohop** and a hard `maxAirSpeed` ceiling (~2x sprint) so air-strafe
  redirects momentum but can't run away with it. First-person now
  renders your own body — `FirstPersonBodyTrim` scales the head to zero
  on your copy only (grounded), plus the upper body while airborne (the
  jump spring clips the camera otherwise), and restores everything while
  `PlayerCameraRig` has the camera cut away. See
  [player-feel-setup.md](stages/player-feel-setup.md).
- **Player animation system** — `PlayerAnimationDriver.cs` drives
  movement-state animation blending and jump-anticipation timing;
  `DebugThirdPersonCamera.cs` toggles first/third-person for testing
  animations without needing multiplayer; `PlayerRagdoll.cs` integrates
  with the animator for clean transitions between animated and ragdolled
  states; `PlayerSkinSpawner.cs` shares one animator controller across
  every skin. (No dedicated stage doc yet — see the commit itself,
  `ec73123`, for the full change list.)
- **Running + carry animation states** — done, controller rebuilt and
  playtested. The `Assets > Rob Everyone > Rebuild Player Animator`
  tool builds a carry gait (Walk_Carry / Run_Carry + a frozen
  carry-idle pose) into the base layer and an upper-body Action layer
  for pick-up / one-handed shoot (Taser, Tranq Gun) / bat swing (Bat,
  Hammer), all networked. See
  [player-animations-setup.md](stages/player-animations-setup.md).
  Deferred: a distinct sprint state (Run is doing double duty), a
  "carrying something bulky" two-handed gait for multi-slot loot, and
  `RecieveHit` is wired but unused until a non-ragdoll hit exists.
- **Ragdoll get-up timing fix** — a car hit at force ~60 launches the
  ragdoll ~20 m/s, but the old fixed 2 s stun timer let players stand
  up mid-air / mid-tumble. `PlayerRagdoll` now holds a 3.5 s minimum
  and then waits until the hips have been near-still for `settleHoldTime`
  (hard-capped by `settleTimeoutExtra`) before ending. Tuning knobs on
  `Player Ragdoll`. Playtested.
- **Ragdoll carry / throw** — done and playtested. `E` hoists any
  ragdolled rival (they stay floppy — hips pinned to a `CarryAnchor`,
  limbs flop), walk them around with no bhop, `G` to set down gently,
  hold LMB to charge a throw and release to launch them along your
  view. Carried players are helpless: can't stand until a delay after
  being dropped/thrown, and can't be stolen from (carrying is a
  grief/relocate toy, not body-passing to strip a friend). Carrying
  fills both hands — the hotbar goes to no-slot-selected, and the Sell
  Station / Tab screen do nothing until the body is down. Drops
  instantly if the carrier is ragdolled or disconnects. `Carryable` +
  `CarryController`, with hooks in `PlayerRagdoll` / `PlayerImpactRelay`
  / `PlayerTheftTarget` / `FirstPersonController` / `PlayerInventory` /
  `HotbarController` / `Interactor` / `SabotageUseController` /
  `SellStation` / `RobEveryoneNetworkManager`. See
  [ragdoll-carry-setup.md](stages/ragdoll-carry-setup.md). A real string
  of bugs surfaced during the two-Editor playtest, fixed in order:
  - Un-ragdoll-too-fast after a car hit — `defaultStunDuration` bumped
    to 3.5s, `ImpactSequence` waits until the hips have been near-still
    for `settleHoldTime` past that (hard-capped at `+settleTimeoutExtra`).
  - E always stole instead of carrying, since a PvP-stunned rival is
    both a valid steal target and a valid carry target and `Interactor`
    claimed the keypress first — added tap-vs-hold disambiguation
    (`Interactor.carryHoldDuration`, 0.5s) scoped to exactly that
    overlap; every other interactable keeps its original zero-latency
    tap.
  - Throwing looked identical to a gentle drop — root cause was the
    unpin (`Carryable`) and the throw impulse (`PlayerImpactRelay`)
    firing as two separate `ClientRpc`s with no ordering guarantee;
    merged into one atomic RPC. Turned out insufficient on its own —
    real fix (found via hop-by-hop diagnostic logging, then diffing
    against the already-working car-impact code path) was that
    `ApplyThrowImpulse` only ever impulsed the hips, while every other
    ragdoll body had spent the whole carry hanging non-kinematically off
    that one pinned point via `CharacterJoint`s — freeing only the hips
    let its own joints immediately absorb the impulse. Now frees and
    impulses every ragdoll body together, matching `BeginRagdoll`.
  - Carried body looked static/stiff and blocked the carrier's own view
    — `Carryable.Update()` was using a direct `.position`/`.rotation`
    assignment on the pinned hips instead of `MovePosition`/
    `MoveRotation`, which teleports a kinematic Rigidbody without
    informing the physics engine of any velocity, starving every
    joint-connected limb of natural lag. Fixed, plus retuned the carry
    anchor position (now in front and low, not shoulder-height).
  - Walking into a carried body felt like hitting a wall, and it was
    also silently absorbing the throw impulse — the carried player's
    individual ragdoll limb colliders stay solid the whole time (only
    their `CharacterController` is disabled), and the close carry anchor
    kept them overlapping the carrier. Fixed with
    `Physics.IgnoreCollision` between the carrier's `CharacterController`
    and the carried player's ragdoll colliders for the carry's duration
    plus a brief window after release.
  - Standing up too early, sometimes — `RagdollAtRest()` was a velocity-
    only proxy, not a real ground check, so a body could read "at rest"
    at the apex of a throw arc or resting on something mid-air. Added
    `IsHipsNearGround()` (a downward raycast filtered to ignore the
    player's own colliders) as an additional requirement, and bumped
    `settleHoldTime` to a real 2s.
- **Held item in hand** — done. `HeldItemDisplay` (plain
  `MonoBehaviour`, not owner-gated — runs identically for every
  player's copy on every client, reacting to `PlayerInventory`'s
  already-synced slot state, no new networking needed) instantiates the
  selected item's `WorldModelPrefab` as a child of the skin's `Fist.R`
  bone (all 52 skins share this bone name — no bone is literally named
  "Hand"), so it automatically follows the Shoot/Swing Action-layer
  animations for free once parented, and disappears on its own while
  carrying a body (`PlayerInventory.SelectedSlot` already goes to `-1`
  then). New per-item `HeldPositionOffset`/`HeldRotationOffset` on
  `ItemDefinition` (same reasoning as `WorldModelScale` — every source
  model's raw pivot/orientation is different, has to be tuned per item
  not shared globally), tuned via a new Editor tool, **Rob Everyone →
  Held Item Pose Tuner** (`Assets/Scripts/Editor/HeldItemPoseTuner.cs`)
  — forces any `ItemDefinition` to preview in the local player's hand
  regardless of real inventory state, Prev/Next to cycle every item in
  one Play session, live Position/Rotation/Scale fields (writes
  straight to the asset via `SerializedObject`, persisting after
  stopping Play Mode since it's asset data not scene state) with
  typed-value + step-sized nudge buttons instead of the default
  drag-numeric-field UI, which wasn't precise enough for the sub-0.01
  adjustments this needed. Bugs found and fixed during setup:
  - Every item came out way too large in-hand — `WorldModelScale` is
    calibrated for a scale-1 parent, but the hand bone carries its own
    accumulated scale from the rig import; now divides it back out via
    the bone's `lossyScale` before applying.
  - Picking up *any* item immediately ended the round and returned to
    the Lobby, which then auto-started the next round — root cause:
    `ItemDefinition.WorldModelPrefab` is the *same* prefab used for the
    real ground pickup, with `NetworkIdentity`/`PickupItem`/
    `BoxCollider` baked onto it by `ItemPrefabBatchTool`. Instantiating
    it wholesale glued a live solid collider inside the player's own
    body, which was enough to double-trigger Ready Spot/round-end
    logic. Fixed by stripping every `NetworkBehaviour`/`Collider`/
    `Rigidbody`/`NetworkIdentity` off the instantiated copy immediately
    (dependency-safe order — `PickupItem` before the components it
    requires), leaving only the visual mesh.
  - A landed, thrown Hammer (`RetrievableProjectile`'s pickup respawn)
    came out massive — the one spawn path that never applied
    `WorldModelScale` after instantiating (every other spawner —
    `LootSpawnPoint`, `PlayerInventory`'s drop — already did). Fixed.
  - `SellStation` now sells only the currently-selected/held item, not
    the whole carried haul at once (`PlayerInventory.SellSelectedSlot`,
    replacing `SellCarried`), with a dynamic `"Sell {Item} for $X"`
    prompt read from `PlayerInventory.LocalPlayer`'s selection; hides
    the prompt entirely when nothing's selected. `PickupItem`'s prompt
    is now `"Pick Up {Item}"` instead of `"Take {Item} (${Value})"`.

## UI

- **Crosshair** — neutral dot + separate interact-hint icon (two
  permanent Images, not a sprite-swap, so size/position never shifts).
  See [ui-implementation-setup.md](stages/ui-implementation-setup.md).
- **Cash/Quota/Timer bars** — pixel-art level-bar HUD (`LevelBarUI.cs`,
  `EconomyBarsUI.cs`). See
  [ui-implementation-setup.md](stages/ui-implementation-setup.md).
- **Main menu + customization** — Play/Settings/Quit, bracket-style
  buttons (`Button_L`/`Button_R` caps flanking text, reusable prefab),
  skin-cycling + color-swatch picker with a live 3D preview
  (`CustomizationUI.cs`, `PlayerColorizer.cs` targeting each skin's
  shared `Skin` material slot, `PlayerCosmeticSelection` for
  PlayerPrefs-backed persistence). See
  [main-menu-visual-design.md](stages/main-menu-visual-design.md),
  [main-menu-customization-setup.md](stages/main-menu-customization-setup.md).
- **Inventory / UX** — one shared **Tab / steal screen**: a shared
  `PlayerCameraRig` cuts the first-person camera to a smoothly-blended,
  following front-facing third-person shot (the ragdoll stun cutaway
  uses the same rig now), the hotbar rises + scales, the cursor
  unlocks, and every slot box becomes drag-and-drop. Drag to rearrange
  the 5 slots or to/from the **Prison Wallet** (its own SyncVar pair,
  outside `ResetInventory`'s loop so it survives being caught; placeable
  only mid-round, retrievable only in the Lobby, locked once filled,
  with imported lock/unlock HUD icons). `Q` drops the selected item as
  a spinning, walk-through world pickup. `E` on a stunned rival opens
  the same screen with their hotbar above yours — drag one item down,
  one steal per stun. You stay fully vulnerable and can still
  walk/sprint/jump/crouch while it's open (only mouse-look parks).
  `InventoryScreenUI`/`InventoryDragSlot`/`WalletSlotUI`/
  `InventoryCameraRig`/`PlayerCameraRig`/`PlayerDropController`, plus
  `PlayerInventory` (wallet + move/drop), `PlayerTheftTarget`
  (drag-driven rework), `PlayerImpactRelay` (steal window as an expiry
  timestamp), `PickupItem` (dropped spin + trigger colliders),
  `PlayerRagdoll` (camera moved onto the rig). **Playtested and
  confirmed.** See
  [inventory-ux-setup.md](stages/inventory-ux-setup.md).

## Meta-game (jumped ahead of build order — see note below)

- **v1 Shop/Lobby loop** — a real restart path after a round ends:
  separate `Lobby` scene (sell loot at a Sell Station, stand on a Ready
  Spot for a countdown, back to a fresh round), driven by a new
  persistent `GameFlowManager.cs`, plus a loading screen so the
  transition doesn't freeze on the result banner. **Playtested and
  confirmed working**: reach exit / timer out / caught → Lobby loads →
  sell loot → Cash updates → Ready Spot → countdown → gameplay scene
  reloads with fresh timer/quota. See
  [stage7-shop-lobby-setup.md](stages/stage7-shop-lobby-setup.md).
- **Batch economy + hotbar** — quota grows ×1.5 per 3-round batch (Cash
  surplus above quota wiped at the boundary); carried loot is a real
  5-slot hotbar (Minecraft-style, number keys 1-5 or scroll to select),
  replacing the old unlimited list. `HotbarController.cs`,
  `GameFlowManager.cs`'s quota-growth field. Built in the same commit as
  the scenes/prefabs it needs (`Hotbar.prefab`, `CashHUD.prefab`,
  `Lobby.unity` wiring) — despite the how-to doc's own header still
  saying "Editor setup not done yet," the commit that added it (`5e9ce4f`)
  shipped the scene/prefab work in the same pass. **Playtested and
  confirmed working** — full 3-round batch: ×1.5 quota growth, surplus
  wipe at the batch boundary, and the 5-slot hotbar all behave. See
  [stage7b-batch-economy-hotbar-setup.md](stages/stage7b-batch-economy-hotbar-setup.md).
- **Item catalog + loot tables** — replaced one-off `(name, value)` pairs
  baked into `PickupItem` with a shared `ItemDefinition` catalog (name,
  value, icon, world model + scale) and a `LootTable`/`LootSpawnPoint`
  system, so houses roll a random item at runtime instead of a specific
  item being hand-placed and baked in. 33 `ItemDefinition`s now exist
  (built via `ItemPrefabBatchTool.cs`), split across
  `LootTable_Small`/`Medium`/`Large` — `Real_House_01`/`02` roll `Medium`,
  `Real_House_03` rolls `Small`; `Large` still isn't wired to any house.
  See [todo.md](todo.md)'s "loot variety" note.

## Multiplayer (Stage 4/5)

- **Stage 4 — Mirror over localhost** — full system sync (player
  movement/animation/skin, loot/inventory, Homeowner/Police AI, traffic
  hazard cars, round/batch economy) rewritten for multiple connected
  players. **All 9 Rest Points confirmed working** via two-Editor
  ParrelSync testing. A long tail of real bugs surfaced and got fixed
  along the way, not just Editor wiring: every client rendering every
  player's Camera/AudioListener instead of just their own; a shared
  "hide own body" skin layer hiding everyone's body from everyone, not
  just its owner; `PlayerAnimationDriver`/`PlayerRagdoll` both doing a
  one-shot skin lookup that could run before a remote player's cosmetics
  sync finished, permanently breaking animation/ragdoll for that player;
  scene-transition repositioning using a raw position set instead of
  NetworkTransform's real teleport API, causing position mismatches
  between clients; `LootSpawnPoint` parenting a spawned item under
  itself, which Mirror doesn't replicate, landing loot at the world
  origin for remote clients; `PickupItem`'s item reference never being
  synced (always null on non-owner clients); Police missing a
  NetworkTransform entirely (frozen on every non-host client); traffic
  cars never triggering an impact at all (project-wide Auto Sync
  Transforms was disabled, and `PlayerImpactRelay` was never actually
  added to the Player prefab); and a single car hit firing the impact
  once per ragdoll limb collider (up to 9x over). See
  [stage4-multiplayer-mirror.md](stages/stage4-multiplayer-mirror.md).
- **Stage 5 — Steam multiplayer** — Steamworks.NET + FizzySteamworks
  (installed via `?path=/com.mirror.steamworks.net`, the actual UPM
  package root in that repo) swapped in as the transport;
  `SteamLobby`/`SteamManager` wired on the persistent NetworkManager
  object; Host button calls `SteamLobby.HostLobby()` (an instance
  method — has to be wired via a dragged-in `SteamLobby` object
  reference in the Inspector, not typed as `SteamLobby.Instance.
  HostLobby()`); `DISABLESTEAMWORKS` removed from Player Settings'
  Scripting Define Symbols so `SteamManager.cs`/`SteamLobby.cs` actually
  compile. Editor setup done through Part 3 (Steam initializes cleanly).
  **Rest Point 4 (the real two-Steam-account overlay invite test) not
  yet run** — see [todo.md](todo.md).

## Sabotage items (Stage 6)

- **Phase 1 — foundation, Taser + Dynamite** — done and tested. All 6
  items have a prefab + `ItemDefinition`, in `ItemCatalog`/Spawnable
  Prefabs. `ItemDefinition` gained a `SabotageType` (Melee/Thrown) plus
  stun/force/cooldown/range/blastRadius/projectile fields;
  `PlayerRagdoll`/`PlayerImpactRelay` take a configurable stun
  duration, with `PlayerImpactRelay.IsStunned` as a real server-synced
  flag (also fixed the bug where a police-frozen player mid-stun could
  get control handed back early); new `Assets/Scripts/Sabotage/`
  folder (`SabotageUseController`, `SabotageProjectile`) wires
  left-click to melee/throw, confirmed working two-Editor (Taser melee
  hit/cooldown/no-restack, Dynamite throw/AOE/consumed-on-use, all per
  [stage6-sabotage-items-setup.md](stages/stage6-sabotage-items-setup.md)'s
  Part 7). Also fixed along the way: `Player.prefab`'s
  `CharacterController` (`Height`/`Center`) was undersized for its own
  camera height, so head-height hits (Taser, and implicitly Police
  vision/anything else raycasting the player) silently missed — now
  `Height: 3, Center: (0, 0.5, 0)`, chosen so the capsule top clears
  eye height with margin while the origin (and camera, a fixed child
  offset from it) lands at exactly the original height;
  `DynamiteProjectile.prefab` had its `NetworkTransformReliable` left
  at the default Client-To-Server sync instead of Server-To-Client,
  breaking it for non-host clients; and `GameFlowManager` gained a
  periodic server-side fall-through safety net (any player below Y
  `-20` gets teleported to a spawn point, skipping anyone still
  mid-ragdoll so `PlayerRagdoll.EndRagdoll` doesn't fight the rescue)
  for the edge case where ragdoll physics carries a player off the map.
- **Phase 2 — Bat, Hammer, Tranquilizer Gun, PvP steal-window, Alarm
  Clock framing** — done and playtested. `ItemDefinition` gained
  `MaxUses` (per-slot durability/ammo) and `SabotageType` became
  `[Flags]` (`Ranged` added, Hammer is `Melee | Thrown`) — every
  exact-value `SabotageType` comparison across `SabotageUseController`
  was rewritten to `HasFlag`. `PlayerInventory` gained a parallel
  `slotUses` SyncList, an `AddItem(item, overrideUses)` overload, and
  `DecrementUses`. `PlayerImpactRelay` gained `IsStealable` +
  `ServerApplyPvpImpact` (a car impact still only calls the plain
  `ServerApplyImpact` — no steal window from that). The E-key
  `IInteractable` pipeline gained a `CanInteract` check, and a new
  `PlayerTheftTarget` component makes a stunned player themself a valid
  steal target through that same existing pipeline. New `ILaunchable`
  interface abstracts "thing a thrown item spawns" —
  `SabotageProjectile` (Dynamite, unchanged) and the new
  `RetrievableProjectile` (Hammer: collision-triggered, lands and
  respawns as a real pickup carrying its remaining uses via a new
  `PickupItem.Initialize(item, uses)` overload) both implement it.
  `HomeownerAI` gained `ForceAlert(position, blamed)` and
  `OnAlertRaised` now carries a `PlayerInventory` to blame (`null` for
  every existing organic sighting); `PoliceAI.HandleAlertRaised` chases
  a blamed player directly via `EnterChase` instead of just moving
  toward a position. New `Assets/Scripts/Sabotage/AlarmClockProjectile.cs`
  resolves who's nearby to frame and which Homeowner to alert. All 5
  Editor setup Parts done (hotbar uses-count UI, `PlayerTheftTarget` on
  the Player prefab, `HammerProjectile.prefab` and
  `AlarmClockProjectile.prefab` built + registered), full two-Editor
  playtest passed (Bat, Hammer swing/throw, Tranq Gun, Alarm Clock, the
  steal-window). Permanent `LootSpawnPoint` placements for the
  sabotage items added in `Real_House_01`/`Real_House_02` afterward
  (previously only hand-placed test pickups). See
  [stage6-sabotage-items-phase2-setup.md](stages/stage6-sabotage-items-phase2-setup.md),
  [item-creation.md](stages/item-creation.md)'s Section 4b, and
  `gameplay-design.md`'s Sabotage items section for intended numbers.

- **Stage 7c Milestones A–E — the rest of the meta-game** — built in 7
  sequential milestones (see
  [stage7c-meta-game-setup.md](stages/stage7c-meta-game-setup.md), which
  is kept current with a status table and a full bug list per milestone
  — this is a condensed pointer, not a replacement for it):
  - **A — Buy-side shop + Lobby practice mode** — `ShopShelfItem.cs`:
    walk up to a pawn-shop shelf, `E` buys straight into your hotbar, no
    menu. Prices scale on the same ×1.5-per-batch curve as quota;
    items unlock by batch number. Spending Cash *is* the entire
    implementation of `gameplay-design.md`'s "sabotage-spending quota
    add-on" — it's the same balance the batch-end quota check reads, so
    no separate tracking was needed. Sabotage items don't consume
    ammo/durability or leave your hand on a throw while in the Lobby, so
    friends can test gear risk-free before a round starts.
  - **B — Real Jail & Bail** — `JailState.cs`: a catch (mid-round or
    end-of-batch quota failure) is now reversible — lose your 5 slots,
    get teleported to a real cell, only finalized as `Caught` if nobody
    frees you first. `E` on a jailed player pays **the rescuer** (not the
    jailed player) a flat Cash bond/bounty (`CurrentQuota / 6` mid-round,
    `/ 3` end-of-batch) and frees them both to a `JailExitPoint`.
    End-of-batch jailing self-bails after one full round if unrescued. A
    jailed player isn't fully frozen — they can walk their cell, and
    press `T` for a third-person spectate cam (`SpectatorController.cs`)
    to watch another player until freed.
  - **C — Homeowner patrol** — `HomeownerAI` gained a real NavMesh patrol
    loop (mirroring `PoliceAI`'s own pattern), a Suspicious state that
    stops and stares/points at whoever triggered it (searching around
    for a few seconds if it loses them before resuming patrol), and an
    Alerted state that flees to its own spawn point and waits there
    until the area's genuinely clear rather than just standing there.
  - **D — Police dispatch pooling** — new `PoliceDispatcher.cs` is the
    sole listener for a Homeowner's alert now: redirects only the
    single closest `Patrol`-state officer, and separately may spawn a
    brand-new one from a real prefab up to a player-count-scaled cap
    (existing baseline officers count against that cap too). A
    dispatched officer walks back to its spawn point and despawns once
    it gives up, instead of patrolling forever. Patrol routes are
    randomized, not a fixed cycle.
  - **E — Night mode** — the last round of every 3-round batch is a
    deterministic night round: Homeowners go inert and bed down, Police
    get speed/vision/chase-persistence multipliers and a higher dispatch
    cap, and `NightModeVisuals.cs` swaps skybox/lighting (the Lobby
    previews the upcoming round's time-of-day during the pre-round shop
    phase). Extended to a real `Morning`/`Day`/`Night` enum so rounds 1
    and 2 get visually distinct skyboxes while staying
    gameplay-identical to each other.
  - Also folded into this pass: the exit now blocks for the first 2
    minutes of a 5-minute round, then seats a reachable player in the
    car for a short robbable window (`ExitCarState.cs`) instead of
    instantly ending the round; Cash wipes to zero at the start of each
    new batch's Morning round; background car patrol paths are
    Catmull-Rom-smoothed instead of needing dense hand-placed waypoints.
  - **Still open**: Milestone **F** (a HUD ping when a rival is spotted
    or chased) and optional Milestone **G** (randomized item value
    ranges per pickup) — see `stage7c-meta-game-setup.md` and
    [todo.md](todo.md).

**Note on why 7/7b/7c exist before Stage 4**: this jumped ahead of
`plan.md`'s build order on purpose — the core loop (loot → quota → exit)
was confirmed fun solo but had no restart path, a round ending just froze
on a result banner with no way to play again. With Milestones A–E above,
the full `gameplay-design.md` meta-game (sabotage purchases, real Jail &
Bail, the sabotage-spending quota add-on) is now built — only the HUD
ping (F) and the optional per-pickup value range (G) remain, tracked in
[todo.md](todo.md).

## Voice & settings

- **VoIP — Steam proximity voice** — done, playtested working on first
  try. `SteamVoiceCapture`/`SteamVoicePlayback`/`PlayerVoice.cs`: push-to-
  talk, captured locally via Steam's own voice API, relayed as an
  unreliable Mirror Rpc over the FizzySteamworks connection, played back
  through a 3D `AudioSource` on the speaker's own player object so
  distance falloff is automatic (proximity, not team-wide; you never hear
  yourself). `PlayerHeadTalkScale.cs` pulses a speaker's head bone in
  proportion to decoded volume as a visual "who's talking" cue. See
  [voip-setup.md](stages/voip-setup.md).
- **Settings menu** — done, playtested working end to end (Milestones
  A–G). Every `Keyboard.current`/`Mouse.current` poll site in the project
  now reads through a new `InputManager` static class backed by
  `RobEveryoneControls.inputactions`, the foundation the rest of this
  sits on. Four tabs: **Audio** (a real `MainMixer` with Master/Music/
  SFX/Voice groups + a live per-rival mute list, `VoiceMuteList.cs`);
  **Controls** (a scrollable rebind list, `RebindActionRow.cs` +
  `KeybindPersistence.cs`, plus mouse sensitivity); **Graphics**
  (resolution/screen mode/quality/VSync/FOV, live-applied via
  `DisplaySettings`/`DisplaySettingsApplier`); **Accessibility**
  (invert-Y, and a voice captions HUD — `VoiceCaptionsHUD.cs` — that
  renders a live head portrait per speaker via an offstage camera/
  RenderTexture rig framed off each skin's real `Head` bone). Also a
  local-only in-game pause overlay (`PauseMenuUI.cs`, Escape) reusing the
  same Settings content, working in both `Lobby.unity` and
  `SampleScene.unity` without pausing the round for anyone else. Every
  button in the project picked up a shared visual pivot along the way —
  the old pixel-art end caps replaced with the game's mosaic
  (`HotbarSlotBlur_Mat`) look + a black outline, fixed once at the
  `MenuButton.prefab` level. See
  [settings-menu-setup.md](stages/settings-menu-setup.md).

## Audio

- **Footstep + pickup SFX** — `PlayerFootstepAudio.cs`: interval-timed
  footsteps driven off `PlayerAnimationDriver`'s effective speed/grounded
  state, gait picked from `FirstPersonController`'s own tuned speeds, an
  absolute next-step timestamp (not a countdown) so rapid tap-stop-tap
  movement can't spam-fire steps. `PickupSfxLibrary.cs`: a shared
  Resources-loaded clip pool so all ~40 item prefabs get pickup sound
  with zero per-prefab wiring.
- **Shop / AI / jail stinger SFX** — `ShopSfxLibrary.cs` (purchase,
  insufficient-funds, item-sold, only the buyer/seller hears it via a
  `TargetRpc`); Homeowner suspicion/alerted stingers, Police chase-start,
  jailed/rescued stingers; a generic PvP/car-impact hit sound played from
  the already-networked impact Rpc so every client hears it. All routed
  through a shared `SfxPlayer.PlayRandomAt` helper (Kenney CC0 packs).
  **Still open**: no use/impact SFX specific to any individual sabotage
  item yet (Dynamite's `explosionClips` field exists but is unwired — no
  CC0 explosion pack sourced) — see [todo.md](todo.md).
- **Intro video** — a new `Intro.unity` scene (Build Settings scene 0,
  ahead of `MainMenu`) plays a video on load, `IntroSequence.cs` advances
  to `MainMenu` on clip end or any key/click/gamepad press.

## Build & release

- **First real build** — swapped the transport from `KcpTransport`
  (local testing) to `FizzySteamworks`; Host/Join now go through
  `SteamLobby` directly (Join opens the Steam Friends overlay); joining
  players get their real Steam persona name (`conn.address`) instead of
  a "Player N" placeholder. Fixed a real bug found in the process: the
  scene had the raw unnamespaced Steamworks.NET sample `SteamManager`
  attached alongside the project's own namespaced one, so a
  `SteamManager.Initialized` check silently spun up a second, independent
  instance the first time anything read it — deleted the raw sample.
  `steam_appid.txt` ships from `StreamingAssets` via a `PostProcessBuild`
  step that copies it next to the built `.exe`, where Steam expects it.
- **Update checker** — `UpdateChecker.cs` compares this build's
  `Application.version` against GitHub's Releases API on Main Menu load
  and shows a blocking popup if a newer tag exists; fails open on any
  network issue. Hardened against a real hazard: the checker's
  GameObject had no persistence, so a Steam invite accepted mid-request
  (triggering the MainMenu → Lobby scene change) abandoned its coroutine
  without disposing the in-flight `UnityWebRequest`'s native handle. Now
  `DontDestroyOnLoad`'d and self-destroys with a proper `Dispose` once
  its one-time check finishes. **A friend's join crash reported around
  this time is confirmed fixed as of this change** — no recurrence since.
- **Post-first-build bugfix chain** — three alpha releases went out
  (`alpha-v1` → `alpha-v1.0.3`), each patching something only a real
  Standalone build surfaced (none of these showed in Editor Play mode):
  a `NullReferenceException` closing the Tab/inventory screen from a
  plain self-open (`victimHotbarUI.Bind(null)` touching UI that had never
  run `Awake`, since `victimRow` starts inactive) that made Tab/Escape
  look completely broken; Static Batching enabled itself on the first
  real build and threw "mesh is read-only" errors against the Kenney
  tree meshes (disabled); and a loading screen that never hid for a
  joining client — it was waiting on `GameFlowManager.Instance`, a
  scene-placed `NetworkIdentity` that (confirmed against Mirror's own
  `FinishLoadSceneClientOnly`) starts disabled until after the client's
  local scene load already finishes, so `Instance` was reliably still
  null at exactly the moment that needed it; hidden directly now instead
  of routing through `GameFlowManager`.

## Design/reference docs

- [gameplay-design.md](stages/gameplay-design.md) — full economy/capacity/
  jail-bail/sabotage/movement design (source of truth for specifics not
  yet built).
- [ui-design.md](stages/ui-design.md) — full UI element spec, tiered by build
  priority.
- [art-info.md](art-info.md) — style/palette/sourced-asset reference.
- [lawn-shader-setup.md](stages/lawn-shader-setup.md) — procedural striped-lawn
  Shader Graph material for yards.
