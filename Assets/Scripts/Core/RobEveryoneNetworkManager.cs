using System.Collections.Generic;
using Mirror;
using RobEveryone.Inventory;
using RobEveryone.Player;
using RobEveryone.Round;
using RobEveryone.UI;
using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

namespace RobEveryone.Core
{
    // Thin subclass of Mirror's own NetworkManager -- almost everything
    // (spawning the Player Prefab per connection, moving connections
    // between scenes, keeping this object alive via its own built-in
    // DontDestroyOnLoad) is stock Mirror behavior, configured entirely in
    // the Inspector (see stage4-multiplayer-mirror.md Part 1). The only
    // override is routing scene changes through GameFlowManager instead
    // of Mirror's default "load whatever scene is typed in the Online
    // Scene field the instant the server starts" -- GameFlowManager
    // already owns *when* to change scenes (round end, ready-up), this
    // just gives it the server-only API to actually do it
    // (NetworkManager.ServerChangeScene) instead of the plain
    // SceneManager.LoadSceneAsync it used single-player.
    public class RobEveryoneNetworkManager : NetworkManager
    {
        public static new RobEveryoneNetworkManager singleton => (RobEveryoneNetworkManager)NetworkManager.singleton;

        // Issue #53: how long a disconnected player's state stays
        // preserved, waiting for the same SteamID to reconnect, before
        // it's given up on and actually torn down for good. Keyed by
        // SteamID64 (ResolveSteamId) -- the host's own local connection
        // never resolves one (its address is always the literal string
        // "localhost", see ResolveDisplayName's own comment), so a host
        // disconnect always falls through to the normal immediate-
        // teardown path in OnServerDisconnect below; that's correct,
        // since the host going down ends the whole session for everyone
        // regardless of any of this.
        [SerializeField] private float reconnectWindowSeconds = 120f;

        private readonly Dictionary<string, PendingReconnect> pendingReconnects = new();

        private struct PendingReconnect
        {
            public NetworkIdentity identity;
            public float disconnectedAt;
        }

        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            string steamId = ResolveSteamId(conn);

            // Issue #53: a returning player -- rebind their still-alive,
            // still-fully-stateful object (hotbar, Cash, wherever they
            // physically are, mid-round or in the Lobby) to this new
            // connection instead of the stock base.OnServerAddPlayer's
            // Instantiate-a-fresh-one path. NetworkServer.AddPlayerForConnection
            // directly -- the same call base would have made, just handed
            // an existing GameObject instead of a freshly Instantiated
            // one -- not ReplacePlayerForConnection, which assumes
            // there's already a player object on this connection to
            // replace (it unconditionally dereferences the "previous"
            // one internally); a fresh connection never has one.
            if (!string.IsNullOrEmpty(steamId)
                && pendingReconnects.TryGetValue(steamId, out PendingReconnect pending)
                && pending.identity != null)
            {
                pendingReconnects.Remove(steamId);

                NetworkServer.AddPlayerForConnection(conn, pending.identity.gameObject);

                var reconnectedInv = pending.identity.GetComponent<PlayerInventory>();
                if (reconnectedInv != null) reconnectedInv.SetDisplayName(ResolveDisplayName(conn));

                Debug.Log($"[RobEveryoneNetworkManager] {ResolveDisplayName(conn)} reconnected (SteamID {steamId}) -- resumed their existing run.");

                // Deliberately NOT HandlePlayerAdded -- that positions a
                // *brand-new* player at a fresh spawn point. A returning
                // player should resume exactly wherever their preserved
                // body already is (which never moved while they were
                // disconnected), not get teleported to a join spot.
                return;
            }

            base.OnServerAddPlayer(conn);

            // Player objects are otherwise destroyed by every single-mode
            // ServerChangeScene (SampleScene<->Lobby, every round) -- Unity's
            // scene load unloads whatever scene the player happens to be
            // sitting in, and Mirror's own OnClientSceneChanged just quietly
            // spawns a brand new one to replace it (see NetworkManager.cs's
            // own doc comment on OnClientSceneChanged: "Scene changes can
            // cause player objects to be destroyed... default implementation
            // is to add a player object if none exists"). A brand new
            // PlayerInventory means Cash and any carried loot both reset to
            // their defaults -- confirmed bug: carried loot vanished the
            // instant a round ended and Lobby loaded, before ever reaching
            // the SellStation. DontDestroyOnLoad keeps the *same* object
            // (and all its SyncVars/SyncLists) alive across every scene
            // change instead -- HandleSceneLoaded's PositionPlayer loop
            // already repositions every connected player into the new
            // scene's own spawn point regardless of whether the object is
            // new or reused, so nothing else needs to change for that to
            // keep working, and this now only runs once per connection's
            // whole lifetime instead of on every scene change too.
            if (conn.identity != null)
            {
                DontDestroyOnLoad(conn.identity.gameObject);

                var inv = conn.identity.GetComponent<PlayerInventory>();
                if (inv != null) inv.SetDisplayName(ResolveDisplayName(conn));
            }

            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.HandlePlayerAdded(conn.identity);
            }
        }

        // FizzySteamworks always reports the remote peer's SteamID64 as
        // conn.address (see ResolveDisplayName's own comment) -- factored
        // out since OnServerDisconnect (issue #53) needs the raw ID as a
        // dictionary key, not just a display name. Returns null for the
        // host's own local connection (never a real SteamID to parse) and
        // for the KCP-transport local-testing path (no Steam address to
        // parse either) -- both correctly mean "reconnect-matching isn't
        // meaningful for this connection," not "treat it as some blank
        // identity that could collide with another blank one."
        private static string ResolveSteamId(NetworkConnectionToClient conn)
        {
            if (conn.connectionId == NetworkConnection.LocalConnectionId) return null;
            return ulong.TryParse(conn.address, out ulong steamId64) ? steamId64.ToString() : null;
        }

        // FizzySteamworks' own server implementation (NextServer.
        // ServerGetClientAddress) returns the remote peer's SteamID64 as a
        // string -- Mirror's own NetworkConnectionToClient.address is set
        // straight from that at connect time (NetworkServer.
        // OnTransportConnectedWithAddress), so this needs no networking of
        // its own to resolve a real Steam name server-side.
        // GetFriendPersonaName works for anyone sharing a Steam lobby with
        // you, not only an actual Friends-list entry (Valve's own doc for
        // that call) -- covers every player here, since SteamLobby only
        // ever creates FriendsOnly lobbies. Falls back to a join-ordinal
        // placeholder off Steam entirely (the KCP transport path used for
        // local testing, or if anything above comes back empty).
        private static string ResolveDisplayName(NetworkConnectionToClient conn)
        {
#if !DISABLESTEAMWORKS
            if (SteamManager.Initialized)
            {
                // The host's own player connects through Mirror's special
                // LocalConnectionToClient, whose address is always the
                // literal string "localhost" (see its own constructor) --
                // never a SteamID to parse, since the host never actually
                // dials itself through the transport. GetPersonaName (no
                // ID needed) is the local user's own name instead.
                if (conn.connectionId == NetworkConnection.LocalConnectionId)
                    return SteamFriends.GetPersonaName();

                string remoteSteamId = ResolveSteamId(conn);
                if (remoteSteamId != null && ulong.TryParse(remoteSteamId, out ulong steamId64))
                {
                    string personaName = SteamFriends.GetFriendPersonaName(new CSteamID(steamId64));
                    if (!string.IsNullOrEmpty(personaName) && personaName != "[unknown]") return personaName;
                }
            }
#endif
            // By this point the new player's PlayerInventory.OnStartServer
            // has already added it to AllPlayers, so Count is the join
            // ordinal.
            return $"Player {PlayerInventory.AllPlayers.Count}";
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            if (conn.identity != null)
            {
                // If they were hauling someone, drop the body before their
                // object is torn down (or, per issue #53 below, held for a
                // possible reconnect) -- can't leave a carried victim
                // hanging off a carrier who isn't coming back for a while.
                conn.identity.GetComponent<CarryController>()?.ServerReleaseOnDisconnect();

                // Issue #45: release their claimed exit-car seat too, so a
                // disconnect mid-wait doesn't leave a phantom occupant
                // permanently holding a slot no one will ever sit in again.
                conn.identity.GetComponent<ExitCarState>()?.ForceRelease();

                // Issue #53: preserve this player's state instead of
                // letting the disconnect destroy it, so the same SteamID
                // can resume it later (OnServerAddPlayer above) -- hotbar,
                // Cash, and wherever they physically are, mid-round or in
                // the Lobby, all just keep existing untouched, unowned,
                // exactly as they were. No serialize/restore code needed:
                // RemovePlayerForConnection(KeepActive) detaches ownership
                // without unspawning, so nothing about the object itself
                // changes. base.OnServerDisconnect below still runs
                // afterward (still needed for real connection-level
                // cleanup -- removing this connection from every object's
                // observer list, etc.) but by then this identity is no
                // longer in conn.owned, so its own
                // DestroyPlayerForConnection -> DestroyOwnedObjects pass
                // (which is what actually destroys a *non-preserved*
                // player's object) has nothing left of this one to touch.
                string steamId = ResolveSteamId(conn);
                if (!string.IsNullOrEmpty(steamId))
                {
                    NetworkIdentity identity = conn.identity;
                    NetworkServer.RemovePlayerForConnection(conn, RemovePlayerOptions.KeepActive);
                    pendingReconnects[steamId] = new PendingReconnect { identity = identity, disconnectedAt = Time.time };

                    Debug.Log($"[RobEveryoneNetworkManager] Player disconnected (SteamID {steamId}) -- state held for up to {reconnectWindowSeconds}s in case they reconnect.");
                }
                else if (GameFlowManager.Instance != null)
                {
                    // No resolvable SteamID (the host, or local/KCP
                    // testing) -- reconnect-matching isn't meaningful, so
                    // this is a real, permanent departure exactly like
                    // before this issue.
                    GameFlowManager.Instance.HandlePlayerRemoved(conn.identity);
                }
            }

            base.OnServerDisconnect(conn);
        }

        // Issue #53: gives up on a disconnected player who never came
        // back within reconnectWindowSeconds and actually tears their
        // object down for good -- holding a preserved player forever
        // isn't sustainable (a permanently-abandoned body would keep
        // sitting in the world, still counted in PlayerInventory.
        // AllPlayers, forever). NetworkServer.Destroy triggers the same
        // normal unspawn/OnStopServer teardown (AllPlayers.Remove, etc.)
        // a disconnect would have caused immediately before this issue --
        // this is just that same teardown, deferred.
        //
        // pendingReconnects is only ever populated server-side (from
        // OnServerDisconnect, itself only ever invoked server-side by
        // Mirror), so on a client this dictionary simply always stays
        // empty regardless of the NetworkServer.active guard below --
        // kept anyway as the actual correctness condition, not just an
        // optimization.
        private readonly List<string> expiredReconnectsScratch = new();

        public override void Update()
        {
            base.Update();

            if (!NetworkServer.active || pendingReconnects.Count == 0) return;

            expiredReconnectsScratch.Clear();
            foreach (KeyValuePair<string, PendingReconnect> kvp in pendingReconnects)
            {
                if (Time.time - kvp.Value.disconnectedAt >= reconnectWindowSeconds)
                {
                    expiredReconnectsScratch.Add(kvp.Key);
                }
            }

            foreach (string steamId in expiredReconnectsScratch)
            {
                PendingReconnect pending = pendingReconnects[steamId];
                pendingReconnects.Remove(steamId);

                if (pending.identity == null) continue;

                Debug.Log($"[RobEveryoneNetworkManager] Reconnect window expired for SteamID {steamId} -- giving up their held state for good.");

                if (GameFlowManager.Instance != null) GameFlowManager.Instance.HandlePlayerRemoved(pending.identity);
                NetworkServer.Destroy(pending.identity.gameObject);
            }
        }

        // Stale pending reconnects from a previous hosted session
        // shouldn't carry over into a new one -- this singleton persists
        // across a Stop/Start cycle (Mirror's own DontDestroyOnLoad on
        // itself), but every preserved NetworkIdentity from the last
        // session is already gone once the server that spawned it shuts
        // down, so there's nothing left for a leftover entry to
        // meaningfully rebind to.
        public override void OnStopServer()
        {
            base.OnStopServer();
            pendingReconnects.Clear();
        }

        // Fires on every client once their own local copy of a server-
        // requested scene change has finished loading -- the right
        // moment to hide the loading screen. Deliberately does NOT go
        // through GameFlowManager.Instance (as it used to) -- that's
        // itself a scene-placed NetworkIdentity which starts disabled
        // until the server's separate spawn-message batch reaches this
        // client, and that batch arrives *after* the client's own local
        // scene load finishes (confirmed in Mirror's own
        // FinishLoadSceneClientOnly). Instance was reliably still null
        // at exactly this moment for a genuine remote client, silently
        // no-oping through the old `?.` and leaving the loading screen
        // stuck forever -- confirmed bug: a joining player could move
        // and hear the game running underneath, but only ever saw the
        // loading screen. Hiding it here directly has no real dependency
        // on GameFlowManager's own state, so there was nothing to lose
        // by not routing through it.
        public override void OnClientSceneChanged()
        {
            base.OnClientSceneChanged();

            LoadingScreenUI screen = FindAnyObjectByType<LoadingScreenUI>();
            if (screen != null) screen.Hide();
        }

        // Hosting's own "Loading..." is shown here instead of
        // OnClientConnect below -- OnStartHost fires synchronously as the
        // very first step of StartHost(), before ServerChangeScene's
        // scene load or NetworkServer.SpawnObjects() can possibly have
        // run yet, which guarantees this always happens before
        // GameFlowManager.OnStartServer's matching Hide() (see its own
        // comment). OnClientConnect's local-host connection message isn't
        // processed on that same synchronous timeline -- it can land on a
        // *later* frame than OnStartServer's hide, which was the actual
        // bug: Show() firing after the one moment meant to hide it, with
        // nothing left to ever hide it again.
        public override void OnStartHost()
        {
            base.OnStartHost();

            LoadingScreenUI screen = FindAnyObjectByType<LoadingScreenUI>();
            if (screen != null) screen.Show("Loading...");
        }

        // Fires the moment this client's connection to the server is
        // actually established. For a genuine remote connection this is
        // the earliest point that can show "Joining game..." -- its own
        // OnClientSceneChanged (above) fires once its local copy of the
        // Online Scene finishes loading, the natural moment to hide it.
        // Skipped entirely while hosting -- OnStartHost above already
        // showed the host's own message earlier and more reliably; this
        // firing again on top of it risked landing *after*
        // GameFlowManager.OnStartServer's hide instead of before it
        // (frame-timing dependent, unlike OnStartHost), re-showing the
        // screen with nothing left to hide it again.
        public override void OnClientConnect()
        {
            base.OnClientConnect();

            if (NetworkServer.active) return;

            LoadingScreenUI screen = FindAnyObjectByType<LoadingScreenUI>();
            if (screen != null) screen.Show("Joining game...");
        }
    }
}
