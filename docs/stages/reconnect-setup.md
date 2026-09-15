# Rejoin an in-progress lobby (issue #53, core mechanism)

Lets a disconnected player (crash, alt+F4, network blip) reconnect to
the same still-running lobby and resume their run — hotbar, Cash, and
wherever they physically are — instead of the game needing a full
restart for everyone, which was the actual motivating pain point
(playtesting: one crash meant restarting the whole lobby and everyone
reconnecting from scratch).

## What this covers, and what it doesn't (yet)

**Covers**: the actual reconnection mechanism — recognizing a returning
player by SteamID and rebinding their preserved game state to a new
connection. This is the substantial, previously-entirely-missing piece
the issue itself calls "new infrastructure, not a fix."

**Doesn't cover**: an explicit "Leave" button in the Esc/pause menu
(the issue's own first line). That's a separate, much smaller,
UI-only follow-up (a button wired to `NetworkManager.singleton.
StopHost()`/`StopClient()`, plus deciding how to get back to Main Menu
afterward — `offlineScene` isn't currently configured on
`RobEveryoneNetworkManager`, so that's its own small decision) —
deliberately scoped out of this pass since the reconnection mechanism
itself was the real unknown and the real risk. The mechanism here
works identically whether the original disconnect was graceful or a
crash — Mirror's `OnServerDisconnect` fires the same way either way —
so it already solves the "can't rejoin after something goes wrong"
problem without needing that button first.

## How it works

All in `Assets/Scripts/Core/RobEveryoneNetworkManager.cs`. No changes
needed anywhere else — `PlayerInventory`'s `OnStartServer`/`OnStopServer`
and `GameFlowManager`'s `PositionPlayer`/`TeleportPlayerTo` already
handle a temporarily-unowned player object correctly with zero changes
(see below).

- **On disconnect** (`OnServerDisconnect`): instead of letting the
  connection's player object be destroyed (Mirror's normal behavior),
  `NetworkServer.RemovePlayerForConnection(conn, RemovePlayerOptions.
  KeepActive)` detaches ownership **without unspawning** — the
  GameObject, and every SyncVar/SyncList on it (hotbar, Cash, position,
  jailed state, everything), just keeps existing, unowned, exactly as
  it was the instant they disconnected. No serialize/restore code at
  all — this is the "keep the object alive" option from the issue's own
  writeup, not the "serialize state and restore it later" one, and
  Mirror already has a first-class API for exactly this. Tracked in a
  `SteamID64 -> (NetworkIdentity, disconnect time)` dictionary.
- **On reconnect** (`OnServerAddPlayer`): if the new connection's
  resolved SteamID matches a pending entry, `NetworkServer.
  AddPlayerForConnection(conn, existingGameObject)` rebinds that same
  preserved object to the new connection — the same Mirror API a fresh
  join already uses, just handed an existing GameObject instead of a
  freshly `Instantiate`d one. This is deliberately **not**
  `ReplacePlayerForConnection` — that API assumes there's already a
  player object on the connection to replace (it unconditionally
  touches the "previous" one internally) and would throw on a fresh
  connection, which never has one.
- **Positioning**: none needed. `HandlePlayerAdded` (which places a
  *brand-new* player at a fresh spawn point) is deliberately **not**
  called on reconnect — the returning player resumes exactly wherever
  their preserved body already is. If a round/scene transition happens
  while they're disconnected, `GameFlowManager.HandleSceneLoaded`'s
  existing per-player reposition loop already covers their object too
  (it's still in `PlayerInventory.AllPlayers` the whole time, since it
  was never actually destroyed) — and `TeleportPlayerTo`'s existing
  `identity.connectionToClient != null` check already falls back
  correctly to a direct server-side teleport for an unowned object,
  the same path already used for host-authoritative moves. Confirmed
  by reading both methods directly; no code changes needed there.
- **Timeout**: `reconnectWindowSeconds` (default 120s, on
  `RobEveryoneNetworkManager`'s own Inspector) — a periodic sweep in
  `Update()` gives up on anyone who doesn't return in time and
  `NetworkServer.Destroy`s their object for real at that point (the
  same teardown a disconnect would have triggered immediately before
  this issue, just deferred).
- **Scoped to real reconnects only**: the host's own connection (and
  the KCP-transport local-testing path) never resolves a SteamID
  (`conn.address` is always the literal string `"localhost"` for the
  host), so both fall through to the exact same immediate-teardown
  behavior as before this issue — correct, since the host disconnecting
  ends the whole session for everyone regardless.

## This needs real two-client testing — I can't verify any of it myself

This is genuinely new Mirror connection-lifecycle code, reasoned
through by reading Mirror's own source (`NetworkServer.cs`) rather than
guessed — but multiplayer timing/ownership behavior like this is
exactly the category of thing that needs real play, not just careful
reading. Specific things to check:

1. **Basic reconnect**: two real clients (Steam-hosted, not
   same-machine only — same-machine testing won't exercise the SteamID
   address-parsing path realistically). Have the non-host disconnect
   (alt+F4 is fine — that's the actual motivating case) and relaunch +
   rejoin the same lobby within 2 minutes. Confirm: same hotbar
   contents, same Cash total, spawns back exactly where they were (not
   at a fresh spawn point).
2. **Client-side re-initialization on reconnect** — the one real
   unknown I flagged going in: does everything that normally sets up
   once on a fresh join (camera, HUD, `OnStartLocalPlayer`-driven setup
   across various scripts) come up correctly on a *rebind* rather than
   a fresh spawn? Should work identically (Mirror doesn't distinguish
   "fresh" vs. "existing" GameObject in its own ownership-assignment
   messaging), but this is the piece most worth watching closely.
3. **Mid-round reconnect** — disconnect and rejoin while mid-round
   (not just in the Lobby). Confirm the round doesn't break for the
   other players while one is disconnected, and that reconnecting
   drops them back into the live round correctly.
4. **Jailed reconnect** — disconnect while jailed, reconnect before and
   after the jail timer would have released them.
5. **Timeout path** — disconnect and *don't* reconnect within 2
   minutes; confirm the abandoned body actually cleans up (check the
   Console for the "giving up their held state" log) rather than
   sitting in the world forever.
6. **Steam lobby membership** — worth specifically checking whether the
   Steam Lobby itself (separate from the Mirror connection) stays
   joinable after a crash/alt+F4, since Steam might auto-remove a
   crashed member from its own lobby member list independent of
   anything this fix controls. If the returning player can't even see/
   rejoin the lobby through Steam's own invite/overlay flow, that's a
   Steam-lobby-level issue outside what `RobEveryoneNetworkManager` can
   fix — worth a distinct bug report if so, not a sign this fix itself
   is broken.

## Where to look

- `Assets/Scripts/Core/RobEveryoneNetworkManager.cs` —
  `OnServerAddPlayer`/`OnServerDisconnect`/`Update`/`ResolveSteamId`,
  all the actual logic.
- `Assets/Mirror/Core/NetworkServer.cs` — `RemovePlayerForConnection`/
  `AddPlayerForConnection`/`ReplacePlayerForConnection`, Mirror's own
  primitives this is built on (worth reading directly if anything here
  behaves unexpectedly — the comments in `RobEveryoneNetworkManager.cs`
  cite the exact behavior read from this file).
