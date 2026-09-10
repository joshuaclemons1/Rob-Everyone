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
- **House pool** — `Real_House_02` also exists now (2 real house prefabs
  total; Zach's further variants still pending, not blocking anything).
  Both prefabs solo-tested (door, loot, homeowner, walk back out). See
  [stage3f-house-pool.md](stages/stage3f-house-pool.md).
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
  Software's air-accelerate formula — chaining jump + air-strafe can
  exceed sprint speed, the documented skill-ceiling tradeoff).
- **Player animation system** — `PlayerAnimationDriver.cs` drives
  movement-state animation blending and jump-anticipation timing;
  `DebugThirdPersonCamera.cs` toggles first/third-person for testing
  animations without needing multiplayer; `PlayerRagdoll.cs` integrates
  with the animator for clean transitions between animated and ragdolled
  states; `PlayerSkinSpawner.cs` shares one animator controller across
  every skin. (No dedicated stage doc yet — see the commit itself,
  `ec73123`, for the full change list.)

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
  `LootTable_Small`/`Medium`/`Large` — though only Medium is wired to a
  house so far; see [todo.md](todo.md)'s "loot variety" note.

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

**Note on why 7/7b exist before Stage 4**: this jumped ahead of
`plan.md`'s build order on purpose — the core loop (loot → quota → exit)
was confirmed fun solo but had no restart path, a round ending just froze
on a result banner with no way to play again. Still deliberately scoped
down from the full `gameplay-design.md` system: no sabotage purchases, no
real Jail & Bail rescue, no personal sabotage-spending quota add-on —
those depend on multiplayer/sabotage items existing first (see
[todo.md](todo.md)).

## Design/reference docs

- [gameplay-design.md](stages/gameplay-design.md) — full economy/capacity/
  jail-bail/sabotage/movement design (source of truth for specifics not
  yet built).
- [ui-design.md](stages/ui-design.md) — full UI element spec, tiered by build
  priority.
- [art-info.md](art-info.md) — style/palette/sourced-asset reference.
- [lawn-shader-setup.md](stages/lawn-shader-setup.md) — procedural striped-lawn
  Shader Graph material for yards.
