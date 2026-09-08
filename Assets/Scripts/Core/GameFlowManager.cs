using System.Collections;
using System.Collections.Generic;
using Mirror;
using RobEveryone.Inventory;
using RobEveryone.Player;
using RobEveryone.Round;
using RobEveryone.Shop;
using RobEveryone.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RobEveryone.Core
{
    // Owns every scene transition between the gameplay map and the Lobby,
    // and every connected player's repositioning after each load, plus the
    // persistent batch/quota state that survives the Lobby round-trip.
    //
    // Networking (Stage 4): lives on the *same* GameObject as
    // RobEveryoneNetworkManager (a NetworkIdentity there makes this a
    // server-authoritative scene object -- Mirror auto-recognizes/spawns
    // a NetworkIdentity already placed in the very first scene at server
    // start, no NetworkServer.Spawn call needed). Scene changes now go
    // through NetworkManager.ServerChangeScene (server-only -- Mirror
    // automatically brings every connected client along, no per-client
    // SceneManager.LoadScene needed or wanted). Every "the player"
    // reference from the single-player version is now "every connected
    // player" (PlayerInventory.AllPlayers); the loading screen shows a
    // personalized message per connection (TargetRpc) since different
    // players can have different outcomes (one caught, one made it out)
    // in the same round.
    public class GameFlowManager : NetworkBehaviour
    {
        public static GameFlowManager Instance { get; private set; }

        [SerializeField] private string gameplaySceneName = "SampleScene";
        [SerializeField] private string lobbySceneName = "Lobby";
        // How much higher the quota goes each batch -- gameplay-design.md
        // leaves the exact curve as an open question, this is a tunable
        // starting point (200 -> 300 -> 450 -> ...).
        [SerializeField] private float quotaGrowthMultiplier = 1.5f;

        private RoundManager currentRoundManager;
        private ReadySpot currentReadySpot;

        // The actual persistent batch state -- shared across every
        // connected player (see plan.md/todo.md for the "shared quota
        // number, independent pass/fail per player" interpretation taken
        // here). RoundManager syncs its own Quota from CurrentQuota at
        // OnStartServer, since RoundManager itself gets recreated fresh
        // every time the gameplay scene loads and would otherwise
        // silently reset the quota back to its Inspector default every
        // round.
        [SyncVar] private int currentQuota = 200;
        [SyncVar] private int batchNumber = 1;
        [SyncVar] private int roundInBatch = 1; // 1, 2, or 3
        public int CurrentQuota => currentQuota;
        public int BatchNumber => batchNumber;
        public int RoundInBatch => roundInBatch;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        public override void OnStartServer()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        public override void OnStopServer()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        // Separate from the server-only subscription above -- wiring a
        // Screen Space - Camera Canvas's Render Camera is a purely local,
        // per-client visual concern (each client has its own camera, on
        // its own local player), so this runs on *every* client
        // (including the host, which is also its own client) rather than
        // being folded into the server-authoritative HandleSceneLoaded.
        public override void OnStartClient()
        {
            SceneManager.sceneLoaded += HandleSceneLoadedClient;
        }

        public override void OnStopClient()
        {
            SceneManager.sceneLoaded -= HandleSceneLoadedClient;
        }

        private void HandleSceneLoadedClient(Scene scene, LoadSceneMode mode)
        {
            StartCoroutine(WireLocalCameraToCanvases());
        }

        private IEnumerator WireLocalCameraToCanvases()
        {
            // The local player may not have finished (re)spawning into
            // the just-loaded scene yet -- wait for it rather than
            // assuming it's already there.
            float timeout = Time.time + 5f;
            while (NetworkClient.localPlayer == null && Time.time < timeout) yield return null;
            if (NetworkClient.localPlayer == null) yield break;

            Camera playerCamera = NetworkClient.localPlayer.GetComponentInChildren<Camera>(true);
            if (playerCamera == null) yield break;

            foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera == null)
                {
                    canvas.worldCamera = playerCamera;
                }
            }
        }

        // Called by RobEveryoneNetworkManager -- a newly-connected player's
        // spawned object needs positioning at whatever spawn point the
        // *current* scene has, since it didn't exist yet for the last
        // HandleSceneLoaded pass.
        [Server]
        public void HandlePlayerAdded(NetworkIdentity playerIdentity)
        {
            // By this point base.OnServerAddPlayer has already spawned
            // this player's object, which means its PlayerInventory's own
            // OnStartServer has already added it to AllPlayers -- so it's
            // the last entry, and that's the index to place it at.
            PositionPlayer(playerIdentity.transform, PlayerInventory.AllPlayers.Count - 1);
        }

        [Server]
        public void HandlePlayerRemoved(NetworkIdentity playerIdentity)
        {
            // No bookkeeping needed here beyond what PlayerInventory's own
            // OnStopServer already does (removing itself from AllPlayers)
            // -- kept as a named hook in case a disconnect mid-round ever
            // needs special handling (e.g. auto-resolving their round).
        }

        [Server]
        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            List<PlayerInventory> players = PlayerInventory.AllPlayers;
            for (int i = 0; i < players.Count; i++)
            {
                PositionPlayer(players[i].transform, i);
            }

            if (scene.name == gameplaySceneName)
            {
                currentRoundManager = FindFirstObjectByType<RoundManager>();
                if (currentRoundManager != null)
                {
                    currentRoundManager.OnPlayerResolved += HandlePlayerResolved;
                    currentRoundManager.OnRoundEnded += HandleRoundEnded;
                }
            }
            else if (scene.name == lobbySceneName)
            {
                currentReadySpot = FindFirstObjectByType<ReadySpot>();
                if (currentReadySpot != null) currentReadySpot.OnAllPlayersReady += HandleAllPlayersReady;
            }
        }

        [Server]
        private void PositionPlayer(Transform player, int index)
        {
            PlayerSpawnPoint[] spawns = FindObjectsByType<PlayerSpawnPoint>(FindObjectsSortMode.None);
            if (spawns.Length == 0) return;

            Transform spawn = spawns[index % spawns.Length].transform;

            // Disable/re-enable around the position change so the
            // CharacterController doesn't try to resolve the jump as a
            // collision -- same reasoning as DoorTeleporter.
            CharacterController controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            player.SetPositionAndRotation(spawn.position, spawn.rotation);
            if (controller != null) controller.enabled = true;
        }

        // Per-player outcome, recorded as each one resolves rather than
        // waiting for the whole round to end -- a caught player's loot is
        // already gone (RoundManager.NotifyPlayerCaught handles that
        // directly), this is just the personalized loading-screen text
        // sent once the *whole* round ends (HandleRoundEnded), keyed by
        // whichever result each player got.
        private readonly Dictionary<PlayerInventory, RoundResult> lastResults = new();

        private void HandlePlayerResolved(PlayerInventory player, RoundResult result)
        {
            lastResults[player] = result;
        }

        [Server]
        private void HandleRoundEnded()
        {
            if (currentRoundManager != null)
            {
                currentRoundManager.OnPlayerResolved -= HandlePlayerResolved;
                currentRoundManager.OnRoundEnded -= HandleRoundEnded;
            }

            // Only the batch's 3rd round actually decides anything --
            // rounds 1-2 just show cumulative progress toward the same
            // fixed quota, per gameplay-design.md's Quota Batches section.
            bool isFinalRound = roundInBatch >= 3;

            foreach (PlayerInventory player in PlayerInventory.AllPlayers)
            {
                lastResults.TryGetValue(player, out RoundResult result);
                bool wasCaught = result == RoundResult.Caught;
                bool metQuota = isFinalRound && player.Cash >= currentQuota;

                if (isFinalRound)
                {
                    // Anti-hoarding: Cash above quota is deleted at the
                    // batch boundary, not carried forward indefinitely.
                    player.WipeCashSurplus(currentQuota);
                }

                string message = wasCaught
                    ? "Caught by the police!"
                    : isFinalRound
                        ? (metQuota ? "Batch quota met!" : "Batch quota not met.")
                        : $"Batch progress: ${player.Cash} / ${currentQuota}";

                NetworkIdentity identity = player.GetComponent<NetworkIdentity>();
                if (identity != null && identity.connectionToClient != null)
                {
                    TargetShowLoadingScreen(identity.connectionToClient, message);
                }

                // Undo PoliceAI.CatchPlayer's freeze if that's what ended
                // this player's round -- no-op otherwise.
                FirstPersonController fpc = player.GetComponent<FirstPersonController>();
                if (fpc != null) fpc.IsFrozen = false;
            }

            lastResults.Clear();

            if (isFinalRound)
            {
                currentQuota = Mathf.RoundToInt(currentQuota * quotaGrowthMultiplier);
                batchNumber++;
                roundInBatch = 1;
            }
            else
            {
                roundInBatch++;
            }

            RobEveryoneNetworkManager.singleton.ServerChangeScene(lobbySceneName);
        }

        private void HandleAllPlayersReady()
        {
            if (currentReadySpot != null) currentReadySpot.OnAllPlayersReady -= HandleAllPlayersReady;

            RpcShowLoadingScreen("Starting next round...");
            RobEveryoneNetworkManager.singleton.ServerChangeScene(gameplaySceneName);
        }

        [TargetRpc]
        private void TargetShowLoadingScreen(NetworkConnectionToClient target, string message)
        {
            LoadingScreenUI screen = FindFirstObjectByType<LoadingScreenUI>();
            if (screen != null) screen.Show(message);
        }

        [ClientRpc]
        private void RpcShowLoadingScreen(string message)
        {
            LoadingScreenUI screen = FindFirstObjectByType<LoadingScreenUI>();
            if (screen != null) screen.Show(message);
        }

        // Called by RobEveryoneNetworkManager.OnClientSceneChanged, which
        // fires on every client once their own local copy of a server-
        // requested scene change has finished loading -- the right moment
        // to hide whichever loading screen variant (Target or broadcast
        // Rpc above) was showing for that transition.
        public void HandleClientSceneChanged()
        {
            LoadingScreenUI screen = FindFirstObjectByType<LoadingScreenUI>();
            if (screen != null) screen.Hide();
        }
    }
}
