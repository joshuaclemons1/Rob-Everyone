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

            // Mirror's own StartServer() runs the Online Scene change
            // (into Lobby) *before* NetworkServer.SpawnObjects() -- which
            // is what triggers this override on a scene-placed
            // NetworkIdentity like this one. That means the very first
            // time this runs, Lobby (or whichever scene Online Scene
            // points at) has *already* finished loading -- its own
            // sceneLoaded event already fired and passed with nobody
            // subscribed yet, so the subscription above alone would miss
            // it entirely (confirmed bug: ReadySpot's countdown completed
            // but nothing ever set currentReadySpot, so
            // OnAllPlayersReady had zero subscribers). Processing the
            // *current* active scene once here, manually, catches that
            // first scene the same way a real sceneLoaded event would.
            HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
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
            StartCoroutine(PositionNewPlayerNextFrame(playerIdentity, PlayerInventory.AllPlayers.Count - 1));
        }

        // A one-frame delay before the very first PositionPlayer call for
        // a brand-new connection -- unlike every later reposition
        // (HandleSceneLoaded, on an already-established player that's had
        // plenty of time to settle), this player's own spawn message may
        // not have fully finished reaching every observer (including its
        // own owner) in the same server frame it was created, and
        // PositionPlayer's TargetRpc -> CmdTeleport -> RpcTeleport chain
        // needs that to already be solid. Confirmed bug: a position
        // mismatch between clients specifically on this first-join
        // placement, not on later scene transitions.
        private IEnumerator PositionNewPlayerNextFrame(NetworkIdentity playerIdentity, int index)
        {
            yield return null;
            PositionPlayer(playerIdentity.transform, index);
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
            // Just repositioning -- safe to do here since PlayerSpawnPoint
            // is a plain MonoBehaviour marker, not gated behind a
            // NetworkIdentity, so it's already findable the instant the
            // scene finishes loading. RoundManager/ReadySpot registration
            // used to also happen here (FindFirstObjectByType, right below
            // this comment used to sit) -- moved out to
            // RegisterRoundManager/RegisterReadySpot below because those
            // *are* NetworkIdentity scene objects, and Mirror disables
            // every scene NetworkIdentity by default, only re-enabling it
            // inside NetworkServer.SpawnObjects() -- which runs *after*
            // this SceneManager.sceneLoaded-triggered call, on every scene
            // transition past the very first one GameFlowManager itself
            // loads into. FindFirstObjectByType doesn't see a disabled
            // object, so currentRoundManager/currentReadySpot silently
            // came back null here and nothing ever subscribed -- the round
            // (or ready-up) still ran fine since RoundManager/ReadySpot's
            // own OnStartServer doesn't depend on this, but nothing was
            // ever listening for it to end. Confirmed via
            // NetworkServer.SpawnObjects()'s own comment ("NetworkIdentity
            // objects in a scene are disabled by default").
            List<PlayerInventory> players = PlayerInventory.AllPlayers;
            for (int i = 0; i < players.Count; i++)
            {
                PositionPlayer(players[i].transform, i);
            }
        }

        // Called from RoundManager's own OnStartServer instead of being
        // looked up here -- see HandleSceneLoaded's comment for why that
        // timing is the only reliable one.
        [Server]
        public void RegisterRoundManager(RoundManager roundManager)
        {
            currentRoundManager = roundManager;
            roundManager.OnPlayerResolved += HandlePlayerResolved;
            roundManager.OnRoundEnded += HandleRoundEnded;
        }

        // Called from ReadySpot's own OnStartServer -- same reasoning as
        // RegisterRoundManager above.
        [Server]
        public void RegisterReadySpot(ReadySpot readySpot)
        {
            currentReadySpot = readySpot;
            readySpot.OnAllPlayersReady += HandleAllPlayersReady;
        }

        // NetworkTransformReliable on the Player prefab is Client To
        // Server (owner-authoritative, per stage4-multiplayer-mirror.md
        // Part 1 -- matches FirstPersonController already being fully
        // client-predicted). That means a direct server-side write to
        // player.position only actually sticks for the host's own player
        // (host and server are the same process, so "the server's write"
        // and "the owner's own simulated position" are the same thing
        // there) -- for a real remote connection, the server changing
        // this Transform never reaches that client at all, since it's not
        // the authoritative source for that object. Confirmed bug: a
        // joining client spawned wherever they happened to be left over
        // from the Lobby instead of at a PlayerSpawnPoint. So a remote
        // player has to be told to move *itself* via TargetRpc instead --
        // the same way it already moves itself for normal input -- and
        // only the host's own player gets positioned directly here.
        [Server]
        private void PositionPlayer(Transform player, int index)
        {
            PlayerSpawnPoint[] spawns = FindObjectsByType<PlayerSpawnPoint>(FindObjectsSortMode.None);
            if (spawns.Length == 0) return;

            Transform spawn = spawns[index % spawns.Length].transform;

            NetworkIdentity identity = player.GetComponent<NetworkIdentity>();
            bool remote = identity != null && !identity.isLocalPlayer && identity.connectionToClient != null;

            if (remote)
            {
                TargetPositionPlayer(identity.connectionToClient, spawn.position, spawn.rotation);
                return;
            }

            // Host's own player -- same process as the server, so
            // ServerTeleport both moves it and (via RpcTeleport to every
            // other client) resets everyone else's interpolation buffer
            // for it too.
            WithCharacterControllerDisabled(player, () =>
            {
                NetworkTransformReliable netTransform = player.GetComponent<NetworkTransformReliable>();
                if (netTransform != null) netTransform.ServerTeleport(spawn.position, spawn.rotation);
                else player.SetPositionAndRotation(spawn.position, spawn.rotation);
            });
        }

        [TargetRpc]
        private void TargetPositionPlayer(NetworkConnectionToClient target, Vector3 position, Quaternion rotation)
        {
            StartCoroutine(PositionLocalPlayerWhenReady(position, rotation));
        }

        // Mirrors WireLocalCameraToCanvases' own reasoning above -- this
        // TargetRpc can arrive before NetworkClient.localPlayer is set on
        // this client (e.g. right after a fresh join, or if this RPC beats
        // this client's own scene-load/player-reference bookkeeping), and
        // a one-shot null check would just silently drop the reposition
        // instead of retrying.
        private IEnumerator PositionLocalPlayerWhenReady(Vector3 position, Quaternion rotation)
        {
            float timeout = Time.time + 5f;
            while (NetworkClient.localPlayer == null && Time.time < timeout) yield return null;

            if (NetworkClient.localPlayer == null)
            {
                Debug.LogWarning("[GameFlowManager] TargetPositionPlayer timed out waiting for NetworkClient.localPlayer.");
                yield break;
            }

            Transform player = NetworkClient.localPlayer.transform;

            // A raw Transform.position set here moves the object locally,
            // but NetworkTransform's own interpolation/delta-compression
            // snapshot buffer on this object never gets told about it --
            // every *other* client watching this player would then either
            // smoothly (and across a scene change, nonsensically) slide
            // from the old position toward the new one, or read stale
            // snapshots that don't match where the object actually is.
            // Confirmed bug: a position mismatch between clients after
            // this reposition. CmdTeleport is Mirror's own API for
            // exactly this -- client-authoritative teleport that also
            // resets the buffer on every observer via RpcTeleport.
            //
            // CmdTeleport is a [Command] -- when *this* client calls it,
            // the method body doesn't run here at all, only on the
            // server, which then broadcasts RpcTeleport back out to
            // everyone (including this same client) to actually apply
            // it. That round trip means this client's own local position
            // doesn't move until it completes, even though the server
            // and every other client are already correct -- Mirror's own
            // NetworkTransformBase.CmdTeleport source carries a TODO
            // acknowledging exactly this gap, recommending the caller
            // also set the position directly for immediate local
            // correctness. Confirmed bug: the clone's own camera stayed
            // at a stale position (a fixed, non-drifting offset -- not
            // a sync/interpolation issue) until that round trip caught
            // up, which in practice is what you'd end up standing at.
            WithCharacterControllerDisabled(player, () =>
            {
                player.SetPositionAndRotation(position, rotation);

                NetworkTransformReliable netTransform = player.GetComponent<NetworkTransformReliable>();
                if (netTransform != null) netTransform.CmdTeleport(position, rotation);
            });
        }

        private static void WithCharacterControllerDisabled(Transform player, System.Action action)
        {
            // Disable/re-enable around the position change so the
            // CharacterController doesn't try to resolve the jump as a
            // collision -- same reasoning as DoorTeleporter.
            CharacterController controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;

            // See FirstPersonController.ResetMotion's own comment -- any
            // velocity accumulated before this teleport (gravity runs
            // every frame regardless of position) would otherwise keep
            // being applied right after landing at the correct spot,
            // pulling the character down through the floor.
            FirstPersonController fpc = player.GetComponent<FirstPersonController>();
            if (fpc != null) fpc.ResetMotion();

            action();
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
