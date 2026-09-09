# Stage 4 — Multiplayer over Mirror (localhost)

**Scope note:** you chose full system sync, not the minimal Stage 4
deliverable — this doc and Stage 5 together network *everything*: player
movement/animation/skin, loot/inventory, Homeowner/Police AI, the traffic
hazard cars, and the round/batch economy. That's a lot of moving parts,
so it's broken into small Parts, each ending in a **🔴 Rest Point** —
something concrete to test in the Editor before moving to the next Part.
Don't skip ahead past a failing rest point; every later Part assumes the
ones before it actually work.

All the code for both this stage and Stage 5 already exists in the repo
(written alongside this doc) — everything below is what to wire up in
the Unity Editor, not code to write yourself, unless a Part says
otherwise.

**Before you start**: commit or stash anything uncommitted. This touches
a huge fraction of the codebase in one pass — you want a clean baseline
to diff against if something goes sideways.

---

## Part 0 — Install Mirror

1. `Window → Package Manager` → the `+` button (top-left) → **Install
   package from git URL...**
2. Paste: `https://github.com/MirrorNetworking/Mirror.git?path=/Assets/Mirror`
3. Wait for it to resolve (can take a minute or two the first time).
4. If that specific URL fails to resolve: Mirror's GitHub repo
   (github.com/MirrorNetworking/Mirror) always has the current
   recommended install instructions in its README — check there for
   whatever the current git URL/tag is, package installs sometimes move.
   A `.unitypackage` download + manual import is the fallback method if
   git-URL install won't work in your environment at all.
5. Let Unity finish compiling.

### 🔴 Rest Point 0
The project should compile with **zero errors** — Mirror adds a bunch of
new types (`NetworkBehaviour`, `NetworkManager`, etc.) but nothing in the
project references them yet at this point, so nothing should be red.
Confirm the Console is clean before continuing.

---

## Part 1 — NetworkManager + player movement

**First, a mixup to head off**: this project has never had a `Player`
*prefab* — the Player has only ever existed as a GameObject hand-placed
directly in `SampleScene`, since it was single-player until now. The
`BaseCharacter`/ragdoll prefabs you already have are a completely
different thing — those are the *skin* models `PlayerSkinSpawner`
instantiates as children at runtime, not the Player root itself (the
object carrying `FirstPersonController`, `CharacterController`,
`PlayerInventory`, `Interactor`, `PlayerSkinSpawner`, etc.). Step 5 below
is where you actually create the missing `Player` prefab asset, from the
scene object you already have — don't go looking for an existing one.

1. In `MainMenu` — **not** `SampleScene` — create an empty GameObject
   named `NetworkManager`. It has to live in whichever scene loads
   first: Mirror's `NetworkManager` persists itself across every later
   scene load (`DontDestroyOnLoad`), but it has to actually exist
   *somewhere* before the Host/Join buttons (which live in `MainMenu`)
   can call `RobEveryoneNetworkManager.singleton` — putting it in
   `SampleScene` instead leaves that null until after `SampleScene` has
   already loaded, which never happens without a working Host/Join in
   the first place.
2. Add component **Network Manager** (Mirror's own). Add component
   **Kcp Transport** (Mirror's built-in transport, good enough for
   localhost/LAN testing — this gets swapped for Steam's transport in
   Stage 5, not before).
3. Add component **`Rob Everyone Network Manager`** (the project's
   subclass, `Assets/Scripts/Core/RobEveryoneNetworkManager.cs`) —
   **remove** the plain `Network Manager` component first if adding the
   subclass didn't already replace it (Unity sometimes keeps both; you
   only want one).
4. On the same GameObject, add component **`Game Flow Manager`** (yes,
   the same object as the NetworkManager now — see that script's own
   comment for why) and **Network Identity** (search "Network Identity"
   in Add Component). Leave Network Identity's settings at default.
5. **Create the `Player` prefab.** In the Hierarchy, find the existing
   `Player` GameObject in `SampleScene` (it's the one with
   `FirstPersonController` etc. already on it — not `BaseCharacter` or
   anything under `Assets/Prefabs/PlayerSkins/`). Select it, then **drag
   it from the Hierarchy into the Project window**, into
   `Assets/Prefabs/` (create that folder first if it doesn't exist). This
   creates a new `Player.prefab` asset *and* automatically turns the
   Hierarchy object into a blue-highlighted instance linked to it — you
   haven't lost anything, you've just given it a reusable asset to point
   the NetworkManager at.
6. Open that new `Player.prefab` (double-click it in the Project window,
   not the scene instance). Add component **Network Identity**. Add
   component **Network Transform Reliable** (Mirror's position/rotation
   sync) — set **Sync Direction** to **Client To Server** (this is what
   makes the *owner's* locally-simulated movement authoritative, matching
   `FirstPersonController` already being fully client-predicted).
7. Back on the `NetworkManager` GameObject, set its fields:
   - **Transport**: drag the Kcp Transport component here.
   - **Player Prefab**: drag `Assets/Prefabs/Player.prefab` here — the
     **asset** from the Project window, not the scene instance in the
     Hierarchy (dragging the wrong one is exactly what "wants a
     GameObject not a prefab" looks like — Mirror's field technically
     accepts either, but only a real prefab asset actually works for
     spawning).
   - **Auto Create Player**: checked.
   - **Online Scene**: set this to `Lobby`, not `SampleScene` — Host/Join
     both load whichever scene is set here, and dropping straight into
     the middle of `SampleScene` (houses already spawned, no ready-up)
     skips the normal entry point. `Lobby` already has a `ReadySpot`
     wired to `GameFlowManager`'s existing round-start logic, so walking
     onto it takes you into `SampleScene` the same way it does between
     every later round — including for solo testing (loot spawns,
     houses, etc.), not just multiplayer.
8. Delete the `Player` instance still sitting in `SampleScene`'s
   Hierarchy (and in `Lobby`, if one's there too) — now that it's a
   prefab the NetworkManager spawns itself, a leftover copy hand-placed
   in the scene would just be a second, non-networked duplicate sitting
   uselessly in the world.
9. Add a `PlayerSpawnPoint` object (empty GameObject with that component)
   in both `SampleScene` and `Lobby` if you don't already have one in
   each — `GameFlowManager` repositions every connected player onto these
   after each scene load. Add a couple more `PlayerSpawnPoint`s in each
   scene, spread apart, so a second player doesn't spawn stacked on the
   first (`GameFlowManager` round-robins across however many exist).
10. Host/Join buttons live on the Main Menu's **Play submenu** panel, not
    directly on Main — see
    [main-menu-customization-setup.md](main-menu-customization-setup.md)'s
    Part 13 for the exact steps (a small sliding-panel system, built
    specifically to unblock this). Wire a **Host** button to
    `MenuActions.HostGame()` and a **Join** button to
    `MenuActions.JoinGame()` on that panel. Optionally drag a
    `TMP_InputField` into **Join Address Field** for typing an IP — leave
    it unassigned for now and `JoinGame()` defaults to `"localhost"`,
    which is all you need for this Part's test.

### Testing two instances on one machine
The easiest way to run two game instances against each other without a
second PC: install **ParrelSync** (`Window → Package Manager → Install
package from git URL`: `https://github.com/VeriorPies/ParrelSync.git?path=/ParrelSync`),
then `ParrelSync → Clones Manager → Add new clone` — this gives you a
second Editor window pointed at the same project, so you can hit Play in
both and have them actually connect to each other.

### 🔴 Rest Point 1
In one Editor instance (or the clone), click **Host**. In the other,
click **Join**. Confirm:
- Both players spawn into `SampleScene` without errors.
- Each client can move its own player around (WASD/mouse), and that
  movement is visible in **the other client's** window (even if the
  moving character is just a bare capsule/no visible skin yet — that's
  Part 2).
- Nobody can move the *other* player's character by pressing keys.

If movement looks jittery or a remote player gets shoved around
strangely, that's likely the `CharacterController`/`NetworkTransform`
interaction flagged in `FirstPersonController`'s own comments — note what
you're seeing and we'll add a guard for it.

---

## Part 2 — Skin & color sync

1. Confirm `PlayerSkinSpawner` on the `Player` prefab still has its
   `Skin Roster`/`Palette`/`Skin Layer`/`Player Animator Controller`
   fields all still assigned (they should carry over from before, just
   double-check nothing got cleared when the prefab was edited in Part 1).
2. That's it for this Part — the sync logic (`OnStartLocalPlayer` spawns
   your own choice immediately, a `[Command]` tells the server, other
   clients' `[SyncVar]` hooks spawn the correct skin for you) is already
   written into `PlayerSkinSpawner.cs`.

### 🔴 Rest Point 2
Host and Join with **two different skins/colors picked** in each
instance's Main Menu customization screen beforehand. Confirm each
client sees **the other's actual chosen skin and color**, not a default/
placeholder, and not their own skin duplicated onto the other player.

---

## Part 3 — Animation sync

1. On the `Player` prefab, select the `PlayerAnimationDriver` component.
2. In the Inspector, find the **Sync Direction** dropdown at the top of
   the component (every `NetworkBehaviour` has this) and set it to
   **Client To Server**. This is what lets the owner write
   `syncedSpeed`/`syncedGrounded` directly — without this setting those
   writes are silently ignored (Mirror's default assumes only the server
   writes SyncVars).

### 🔴 Rest Point 3
Walk, sprint, crouch, and jump on one client; confirm the **other**
client's view of that character actually plays the matching
Idle/Walk/Run blend and the Jump animation at roughly the right time —
not just sliding around in a T-pose or Idle pose while moving.

---

## Part 4 — Interaction, pickups, and inventory

1. Every `ItemDefinition`'s **World Model Prefab** (`Assets/Data/Items/`)
   needs a **Network Identity** component added *on the prefab asset
   itself* (select the prefab, Add Component, not on an instance in a
   scene) — `LootSpawnPoint` will log a clear error naming which item is
   missing this if you miss one.
2. On the `NetworkManager` object, find **Spawnable Prefabs** and add:
   every `ItemDefinition`'s World Model Prefab, and (skip ahead once you
   reach them) every house prefab (Part 5) and car prefab (Part 7).
3. Every scene object the player can `E`-interact with needs a **Network
   Identity** too, since `Interactor` now sends the target across as a
   `NetworkIdentity` reference: the Lobby's `SellStation`, and the
   `ReadySpot` trigger (Part 8 needs this one workable already).
4. Confirm `PlayerInventory`'s **Catalog** field (drag your
   `ItemCatalog` asset — create one via `Create → Rob Everyone → Item
   Catalog` if you don't have one yet, and list every `ItemDefinition` in
   it) is assigned on the `Player` prefab.

### 🔴 Rest Point 4
Player A picks up an item. Confirm:
- It disappears from the world for **both** A and B, not just A.
- A's own Cash/hotbar updates correctly; B's doesn't (it's not B's item).
- A full 5-slot inventory correctly leaves a 6th item in the world
  untouched (same as before, just confirm it still holds true).

---

## Part 5 — Houses spawn identically for everyone

1. Every house prefab (`Real_House_01`, `Real_House_02`, ...) needs a
   **Network Identity** added at its root, on the prefab asset.
2. Add each house prefab to the NetworkManager's **Spawnable Prefabs**
   list (Part 4 step 2 already told you to do this — just confirm it's
   done for every house, not just items).

### 🔴 Rest Point 5
Host + Join. Confirm both clients see the **exact same house** in the
**exact same slot** for every one of the 15+2 slots — not each client
independently rolling its own random layout. Walk into 2-3 houses on
each client and confirm loot/Homeowner still work (this is really
re-confirming Stage 3g's original test, now under Mirror).

---

## Part 6 — Homeowner & Police AI

Nothing to add for `HomeownerAI` — it's nested inside each house prefab,
which already got a `Network Identity` in Part 5, and Mirror
automatically discovers `NetworkBehaviour` components (like
`HomeownerAI`) anywhere in that same hierarchy.

For `PoliceAI` (a single hand-placed scene object, not spawned):
1. Select the Police GameObject in `SampleScene`.
2. Add component **Network Identity**.

### 🔴 Rest Point 6
With two players in the same house at once (or taking turns), confirm a
Homeowner's Suspicious/Alerted color change is visible **identically** to
both clients at the same moment. Have Police chase and catch **one**
player while the other keeps playing — confirm only the caught player
freezes (`IsFrozen`), the other isn't affected, and Police goes looking
for the still-free player afterward instead of just standing still.

---

## Part 7 — Traffic hazard cars

1. Every car prefab needs a **Network Identity** at its root (prefab
   asset) plus a **Network Transform Reliable** component (Sync
   Direction: **Server To Client**, the default — cars are entirely
   server-driven, unlike the client-authoritative player).
2. Add every car prefab to the NetworkManager's **Spawnable Prefabs**.

### 🔴 Rest Point 7
Confirm cars appear identically for both clients, on the same lap, and
that a car hitting **either** player produces the ragdoll knockdown
correctly on both clients' screens (the horn/yell SFX should also play
for both, via `RpcPlayImpactSfx`) — not just for whoever got hit.

---

## Part 8 — Round & batch economy, ready-up

This is the biggest rearchitecture in the whole stage — `RoundManager`
and `GameFlowManager` were rewritten from "track one player" to "track
every connected player, each resolving independently (caught vs. exited)
but sharing one round timer and one batch quota number." Read both
scripts' class-doc comments if anything below doesn't make sense; there's
no separate Editor wiring beyond what earlier Parts already covered
(`ReadySpot` needing a `Network Identity`, from Part 4 step 3) —
this Part is really just a *test*, not new setup.

### 🔴 Rest Point 8
Play a **full 3-round batch with two players**:
1. Both players loot, one gets caught, the other reaches the exit. Confirm
   the round only actually ends (cuts to the Lobby) once *both* are
   resolved — not the instant the first one finishes.
2. In the Lobby, confirm each client's own loading-screen message on the
   way in reflected *their own* outcome (one saying "Caught by the
   police!", the other showing batch progress), not the same message for
   both.
3. Sell loot at the `SellStation` (each player's own Cash should update
   independently).
4. Both players need to stand on/near the `ReadySpot` **at the same
   time** before the countdown starts — confirm one player alone doesn't
   trigger it, and confirm either one stepping off cancels it for both.
5. On the batch's 3rd round, confirm the quota grows ×1.5 for the next
   batch and each player's Cash surplus above quota gets wiped
   independently (a player who banked way more than quota loses the
   excess; a player who banked less doesn't).

---

## Part 9 — Menu polish

1. Confirm `MenuActions`' **Host**/**Join** buttons (wired back in Part
   1) still work correctly now that everything else is networked.
2. Optional: wire the `Join Address Field` input field for real (type a
   LAN IP to test across two actual machines on the same network, not
   just two Editor windows on one machine).

### 🔴 Rest Point 9 — full playtest
From the Main Menu, on two separate machines (or two Editor
instances) if you don't have a second PC handy yet: Host on one, Join on
the other, play a full batch start-to-finish. This is the real "Stage 4
done" milestone — once this works, tell me and we move to Stage 5 (Steam,
so this works over the actual internet instead of just a LAN/localhost).
