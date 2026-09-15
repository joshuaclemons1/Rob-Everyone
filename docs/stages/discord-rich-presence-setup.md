# Discord Rich Presence

Shows "Rob Everyone" plus the current game state (`In Lobby` / `Batch 3:
Round 2`) on a player's Discord profile/friends list while the game is
running. Tracked in
[issue #49](https://github.com/joshuaclemons1/Rob-Everyone/issues/49).

## Why the older IPC protocol, not the Discord Social SDK

Discord's current-generation **Social SDK** *does* support rich
presence, but only as part of a much bigger package — full OAuth2
account linking, friends list access, voice chat — and setting a status
through it requires each player to go through a real Discord login flow
inside the game. That's a lot of surface area for "show what I'm
playing," and not something a 4-person friend-group game needs.

Discord's older, lighter **local IPC Rich Presence protocol** does
exactly this one thing: the running game process talks directly to
whatever Discord client is already open on the same machine over a
named pipe, no login, no account linking, nothing player-facing at all
— it just works the moment both are running. `DiscordRichPresence.cs`
uses [Lachee's `discord-rpc-unity`](https://github.com/Lachee/discord-rpc-unity)
(a Unity-friendly wrapper around `discord-rpc-csharp`) for this.

## What's already done

- **`Packages/manifest.json`** — added `com.lachee.discordrpc` as a git
  URL dependency (same pattern already used for
  `com.mirror.steamworks.net`/`com.veriorpies.parrelsync`). Its one
  dependency, `com.unity.nuget.newtonsoft-json`, was already installed.
- **`Assets/Scripts/Core/DiscordRichPresence.cs`** — the whole
  integration. Self-bootstraps once (`DontDestroyOnLoad`, same shape as
  `SteamManager`), reads `GameFlowManager`'s already-tracked state
  (`InLobbyScene`, `InGameplayScene`, `BatchNumber`, `RoundInBatch`) to
  compute the status text, and only calls `SetPresence` when that text
  actually changes — not every frame.

## Part 1 — Register a Discord Application (needs your own Discord account)

This is the one step that has to happen outside the project — an
Application ID is Discord's way of identifying which "game" a presence
update belongs to.

1. Go to **https://discord.com/developers/applications** and log in
   with your Discord account.
2. Click **New Application** (top right), name it `Rob Everyone`
   (this name is just for your own reference in the portal — it's not
   what shows on a player's profile; see Part 3 for that), and agree to
   the terms.
3. On the **General Information** page that opens, copy the
   **Application ID** near the top — a long number, e.g.
   `1234567890123456789`.

That ID is not a secret (same as a Steam AppID) — safe to paste directly
into the Inspector and commit it.

## Part 2 — Wire it up in the Editor

1. Let Unity finish resolving the new package (it'll do this
   automatically the next time the Editor regains focus/reimports —
   watch the bottom-right corner for a progress spinner). If it doesn't
   pick it up on its own, **Window → Package Manager**, then the little
   refresh icon top-left.
2. Open `MainMenu.unity`.
3. Find the same GameObject that holds **Steam Manager** / **Robeveryone
   Network Manager** (named `NetworkManager` in the Hierarchy).
4. **Add Component** → search **Discord Rich Presence** → add it.
5. In its Inspector, paste your Application ID from Part 1 into
   **Application Id**.
6. Save the scene.

### 🔴 Rest point

Press Play from `MainMenu` with Discord open on the same machine. Check
your own Discord profile (click your own name/avatar in Discord's
bottom-left) — it should show "Playing Rob Everyone" within a few
seconds, with **In Menu** as the status line. Host or join a game and
confirm it updates to **In Lobby**, then to **Batch 1: Round 1** (and
onward) once a round actually starts. If nothing shows up at all, check
the Console for the "no Application Id set" warning (Part 2 step 5) or
confirm Discord itself is actually running.

## Part 3 — Optional polish (not required for the text status to work)

- **Large/small status icons**: back in the Developer Portal, open your
  application → **Rich Presence** tab → **Art Assets**, upload images,
  and give each a **Key** (e.g. `game_icon`). Wire those keys into
  `DiscordRichPresence.cs`'s `RichPresence.Assets` (currently unset) —
  `LargeImageKey`/`LargeImageText`/`SmallImageKey`/`SmallImageText`.
- **A nicer top line**: `Details` is currently a static `"Robbing
  houses"` — could pull something more specific (e.g. which house
  you're in, or your current Cash) if it's worth the extra
  GameFlowManager/PlayerInventory plumbing later.

## Follow-ups (tracked separately)

- None yet — file a new issue if the state text needs to reflect
  anything beyond Lobby/Batch/Round (e.g. Night rounds, jailed).
