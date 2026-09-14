using DiscordRPC;
using UnityEngine;

namespace RobEveryone.Core
{
    // Issue #49: shows "Rob Everyone" plus the current game state (In
    // Lobby / Batch N: Round M) to friends on Discord. Uses Discord's
    // local IPC Rich Presence protocol (Lachee's discord-rpc-csharp
    // wrapper), deliberately NOT the newer Discord Social SDK -- that one
    // requires full OAuth2 account linking just to set a status (see
    // docs/stages/discord-rich-presence-setup.md for why), a much bigger
    // ask than a friend-group game needs. IPC just talks to whatever
    // Discord client is already running locally on the same machine, no
    // login/account-linking step required.
    //
    // Self-bootstraps once, DontDestroyOnLoad, same shape as SteamManager
    // -- put one on the same "Managers" GameObject as SteamManager/
    // RobEveryoneNetworkManager in MainMenu.unity so it lives for the
    // whole session from the moment the game starts.
    public class DiscordRichPresence : MonoBehaviour
    {
        // From the Discord Developer Portal
        // (discord.com/developers/applications) -- see
        // docs/stages/discord-rich-presence-setup.md Part 1. Not a
        // secret -- an Application ID is a public identifier, same as a
        // Steam AppID, safe to commit.
        [SerializeField] private string applicationId = "";

        // How often to re-check the computed state string, not how often
        // Invoke() runs (that still happens every frame below) -- no
        // need for the displayed status to be frame-perfect, and this
        // keeps SetPresence calls infrequent regardless of what ends up
        // driving ComputeState in the future.
        [SerializeField] private float checkInterval = 0.5f;

        private static DiscordRichPresence instance;

        private DiscordRpcClient client;
        private string lastState;
        private float nextCheck;

        private void Awake()
        {
            if (instance != null)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);

            if (string.IsNullOrEmpty(applicationId))
            {
                Debug.LogWarning("DiscordRichPresence: no Application Id set on the component -- see docs/stages/discord-rich-presence-setup.md Part 1. Disabling.");
                enabled = false;
                return;
            }

            client = new DiscordRpcClient(applicationId);
            client.Initialize();
        }

        private void Update()
        {
            if (client == null) return;

            // Dispatches queued IPC events -- discord-rpc-csharp needs
            // this pumped regularly for the connection to actually work,
            // it isn't automatic on its own background thread here.
            client.Invoke();

            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + checkInterval;

            string state = ComputeState();
            if (state == lastState) return;
            lastState = state;

            client.SetPresence(new RichPresence
            {
                Details = "Robbing houses",
                State = state,
                // Resets every time the state string actually changes,
                // so Discord's "X minutes elapsed" reflects time in the
                // *current* state (e.g. this round), not the whole
                // session.
                Timestamps = Timestamps.Now,
            });
        }

        // GameFlowManager already tracks every piece of this -- nothing
        // new to build there, see issue #49's own notes.
        private static string ComputeState()
        {
            if (GameFlowManager.Instance == null) return "In Menu";
            if (GameFlowManager.Instance.InLobbyScene) return "In Lobby";
            if (GameFlowManager.Instance.InGameplayScene)
                return $"Batch {GameFlowManager.Instance.BatchNumber}: Round {GameFlowManager.Instance.RoundInBatch}";
            return "In Menu";
        }

        private void OnApplicationQuit()
        {
            client?.Dispose();
        }
    }
}
