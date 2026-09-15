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

        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
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

                if (ulong.TryParse(conn.address, out ulong steamId64))
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
                // object is torn down.
                conn.identity.GetComponent<CarryController>()?.ServerReleaseOnDisconnect();

                // Issue #45: release their claimed exit-car seat too, so a
                // disconnect mid-wait doesn't leave a phantom occupant
                // permanently holding a slot no one will ever sit in again.
                conn.identity.GetComponent<ExitCarState>()?.ForceRelease();

                if (GameFlowManager.Instance != null)
                {
                    GameFlowManager.Instance.HandlePlayerRemoved(conn.identity);
                }
            }

            base.OnServerDisconnect(conn);
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
