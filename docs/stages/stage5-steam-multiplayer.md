# Stage 5 — Steam multiplayer

Builds directly on Stage 4 — do not start this until every Rest Point in
`stage4-multiplayer-mirror.md` passes. This stage swaps the transport
(how bytes actually travel between players) from Mirror's built-in Kcp
(localhost/LAN only) to Steam's P2P networking, and swaps the "type an
IP" join flow for a real Steam overlay invite — the actual gameplay code
underneath doesn't change at all.

Per `plan.md`: test AppID **480** (Valve's public "Spacewar" test app)
until you're close to an actual release, which needs its own $100
Steamworks fee and a real AppID.

---

## Part 1 — Install Steamworks.NET

1. Download the latest release `.unitypackage` from
   github.com/rlabrecque/Steamworks.NET (Releases page) — this is the
   standard, actively-maintained C# wrapper around Valve's Steamworks
   SDK, and the one virtually every Mirror+Steam tutorial assumes.
2. `Assets → Import Package → Custom Package...`, import it.
3. Let it compile. `SteamManager.cs` (already in
   `Assets/Scripts/Core/`) is written to match this package's API —
   confirm it compiles cleanly now that the real Steamworks.NET types
   exist (it was wrapped in `#if !DISABLESTEAMWORKS` so it silently
   didn't compile at all before this point).

### 🔴 Rest Point 1
Project compiles with zero errors. Nothing behaves differently yet.

---

## Part 2 — Install FizzySteamworks (the transport)

1. `Window → Package Manager → Install package from git URL`:
   `https://github.com/Chykary/FizzySteamworks.git`
2. Let it resolve and compile.

### 🔴 Rest Point 2
Project compiles with zero errors.

---

## Part 3 — steam_appid.txt + SteamManager/SteamLobby setup

1. Create a plain text file named exactly `steam_appid.txt` containing
   just `480`, and place it in the **project's root folder** (same level
   as the `Assets` folder) — this is what lets `SteamAPI.Init()` succeed
   while testing in the Editor. For an actual build, this file needs to
   sit next to the `.exe`.
2. **Steam must be running and you must be logged in** for any of this
   to work at all, in-Editor or in a build.
3. On the same persistent GameObject as `RobEveryoneNetworkManager` (from
   Stage 4 Part 1), add components:
   - **`Steam Manager`** (`Assets/Scripts/Core/SteamManager.cs`)
   - **`Steam Lobby`** (`Assets/Scripts/Core/SteamLobby.cs`)
4. Add component **Fizzy Steam Works** (the transport) to that same
   object. On the `RobEveryoneNetworkManager` component, change the
   **Transport** field from Kcp Transport to this new Fizzy Steam Works
   component. (Leave the Kcp Transport component sitting there
   disabled/unused rather than deleting it -- an easy way to switch back
   to LAN testing later if Steam ever gets in the way of a quick test.)
5. Update `MenuActions.HostGame()`'s button wiring: point the **Host**
   button at `SteamLobby.Instance.HostLobby()` instead of
   `MenuActions.HostGame()` directly (that method still exists and still
   works for LAN testing, but the Steam flow needs `SteamLobby` to create
   the lobby *before* `StartHost()` runs, which is what `HostLobby()`
   sequences correctly).
6. The **Join** button/IP text field from Stage 4 is no longer the
   primary join path — Steam joining happens through the Steam overlay
   (see Part 4) — but leave it wired for now as a LAN fallback, it
   doesn't hurt anything.

### 🔴 Rest Point 3
Click Host (with Steam running, logged in). Check the Console: no
`SteamManager`/`SteamAPI.Init` errors. This alone doesn't prove
networking works yet, just that Steam itself initialized correctly.

---

## Part 4 — Steam overlay invite + join

Nothing left to build here — `SteamLobby.cs`'s
`OnGameLobbyJoinRequested`/`OnLobbyEntered` callbacks already handle a
friend accepting an invite and joining automatically. This Part is pure
testing.

### 🔴 Rest Point 4 — the real test
You'll need **two actual separate Steam accounts** (yours and a
friend's, or two of your own on two machines/computers — a single Steam
account can't run two game instances against itself the way ParrelSync
let you fake two players locally) for this to mean anything:

1. You Host. Open the Steam overlay (Shift+Tab), find yourself in your
   friends list, and send a "Join Game" invite — or have your friend see
   you're in-game and click Join Game on your profile, however Steam
   surfaces it for this AppID.
2. Your friend accepts. Confirm they connect and spawn in correctly, same
   as the ParrelSync test but now over the actual internet, no shared
   network required.
3. Play a full batch together, same checklist as Stage 4's Rest Point 9.

Stage 4/5 are both done, including this Rest Point — confirmed via a
real two-Steam-account overlay invite test during the alpha-v1.0.2
build/testing cycle. See
[issue #34](https://github.com/joshuaclemons1/Rob-Everyone/issues/34)
and [issue #17](https://github.com/joshuaclemons1/Rob-Everyone/issues/17)
(closed).
