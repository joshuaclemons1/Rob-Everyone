#if !DISABLESTEAMWORKS
using Mirror;
using Steamworks;
using UnityEngine;

namespace RobEveryone.Core
{
    // Stage 5: the Steam-overlay-invite equivalent of Stage 4's "type in
    // an IP" join flow. FizzySteamworks (the transport, swapped in on the
    // NetworkManager's Inspector) expects networkAddress to hold the
    // *host's SteamID64 as a string*, not an IP -- this script's whole
    // job is turning "click Host" / "accept a Steam invite" into that
    // string being set correctly before Mirror's own StartHost/StartClient
    // runs. Put one of these on the same persistent GameObject as
    // RobEveryoneNetworkManager. #if !DISABLESTEAMWORKS matches the
    // symbol Steamworks.NET itself defines/checks, so this compiles out
    // cleanly on a build where Steamworks.NET isn't present.
    public class SteamLobby : MonoBehaviour
    {
        public static SteamLobby Instance { get; private set; }

        private CSteamID currentLobbyId;

        protected Callback<LobbyCreated_t> lobbyCreated;
        protected Callback<GameLobbyJoinRequested_t> gameLobbyJoinRequested;
        protected Callback<LobbyEnter_t> lobbyEntered;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (!SteamManager.Initialized) return;

            lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
            gameLobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
            lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
        }

        // Call this from MenuActions.HostGame instead of StartHost
        // directly -- the actual StartHost() call happens once
        // OnLobbyCreated confirms Steam's side succeeded, not before.
        public void HostLobby()
        {
            if (!SteamManager.Initialized)
            {
                Debug.LogError("SteamLobby: Steam isn't initialized -- is steam_appid.txt present and Steam running? See stage5-steam-multiplayer.md Part 3.");
                return;
            }

            // FriendsOnly, not Public -- matches "invite your friend
            // through the Steam overlay" rather than a public server
            // browser, which this game has no need for.
            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, RobEveryoneNetworkManager.singleton.maxConnections);
        }

        private void OnLobbyCreated(LobbyCreated_t callback)
        {
            if (callback.m_eResult != EResult.k_EResultOK) return;

            currentLobbyId = new CSteamID(callback.m_ulSteamIDLobby);

            // The host's own SteamID is stored as lobby data so joiners
            // (who only get the *lobby* ID from the Steam overlay invite,
            // not the host's SteamID directly) can read it back out in
            // OnLobbyEntered below.
            SteamMatchmaking.SetLobbyData(currentLobbyId, "HostAddress", SteamUser.GetSteamID().ToString());

            RobEveryoneNetworkManager.singleton.StartHost();
        }

        // Fires when this client accepts a Steam invite or clicks "Join
        // Game" on a friend in the Steam overlay/friends list.
        private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback)
        {
            SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
        }

        // Fires for everyone who ends up in the lobby, host included --
        // only the non-host side needs to actually start a Mirror client
        // (the host already called StartHost in OnLobbyCreated above).
        private void OnLobbyEntered(LobbyEnter_t callback)
        {
            currentLobbyId = new CSteamID(callback.m_ulSteamIDLobby);

            if (NetworkServer.active) return; // we're the host, already started

            string hostAddress = SteamMatchmaking.GetLobbyData(currentLobbyId, "HostAddress");
            if (string.IsNullOrEmpty(hostAddress)) return;

            RobEveryoneNetworkManager.singleton.networkAddress = hostAddress;
            RobEveryoneNetworkManager.singleton.StartClient();
        }
    }
}
#endif
