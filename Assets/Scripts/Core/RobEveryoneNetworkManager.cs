using Mirror;
using RobEveryone.Inventory;
using UnityEngine;

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

                // Placeholder label until real Steam persona names are
                // wired (Stage 5 follow-up). By this point the new
                // player's PlayerInventory.OnStartServer has already added
                // it to AllPlayers, so Count is the join ordinal.
                var inv = conn.identity.GetComponent<PlayerInventory>();
                if (inv != null) inv.SetDisplayName($"Player {PlayerInventory.AllPlayers.Count}");
            }

            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.HandlePlayerAdded(conn.identity);
            }
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            if (GameFlowManager.Instance != null && conn.identity != null)
            {
                GameFlowManager.Instance.HandlePlayerRemoved(conn.identity);
            }

            base.OnServerDisconnect(conn);
        }

        // Fires on every client once their own local copy of a server-
        // requested scene change has finished loading -- see
        // GameFlowManager.HandleClientSceneChanged for why this is the
        // right moment to hide the loading screen.
        public override void OnClientSceneChanged()
        {
            base.OnClientSceneChanged();

            if (GameFlowManager.Instance != null) GameFlowManager.Instance.HandleClientSceneChanged();
        }
    }
}
