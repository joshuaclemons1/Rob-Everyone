using Mirror;
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
