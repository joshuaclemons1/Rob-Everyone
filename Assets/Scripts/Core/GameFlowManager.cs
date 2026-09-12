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
        // Exposed for ShopShelfItem -- sabotage prices ride the same
        // curve quota itself grows on (Stage 7), rather than needing a
        // second, separately-tuned price curve.
        public float QuotaGrowthMultiplier => quotaGrowthMultiplier;

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

        // A simple globally-incrementing "which round is this" counter --
        // the natural clock JailState's self-bail check needs ("released
        // after exactly one full round unrescued"), since roundInBatch
        // alone resets every batch and can't tell "a round has passed"
        // apart from "a new batch started."
        [SyncVar] private int roundOrdinal;
        public int RoundOrdinal => roundOrdinal;

        // Players who failed the batch quota this round, queued here at
        // HandleRoundEnded and actually jailed once the *next* round
        // starts (HandleRoundStarted) -- jailing them immediately would
        // put them in a cell in a round that's already over.
        private readonly HashSet<PlayerInventory> pendingEndOfBatchJail = new();

        // Which physical JailPoint slot each currently-jailed player is
        // occupying -- claimed in TeleportToJail (ClaimJailPoint below),
        // released in TeleportToJailExit, so multiple simultaneous
        // jailings spread across however many cell slots exist instead of
        // everyone stacking on the same one marker. Cleared at the start
        // of every fresh round (HandleRoundStarted) since the gameplay
        // scene's own JailPoint objects are recreated then -- any old
        // reference in here would otherwise be stale.
        private readonly Dictionary<PlayerInventory, JailPoint> occupiedJailPoints = new();

        // The gameplay-vs-shop phase gate (used by PlayerInventory's
        // Prison Wallet rules: stash only mid-round, retrieve only in the
        // Lobby). ServerChangeScene is single-mode, so the active scene
        // is reliably exactly one of these two.
        public bool InGameplayScene => SceneManager.GetActiveScene().name == gameplaySceneName;
        public bool InLobbyScene => SceneManager.GetActiveScene().name == lobbySceneName;

        // Catches a player left below the map -- normally impossible, but
        // ragdoll physics (a car impact, a Dynamite blast) can carry
        // someone past a boundary or through a thin/missing collider
        // while EndRagdoll's own raycast-to-ground has nothing under it to
        // find, leaving them wherever they happened to be when the stun
        // timer ran out. A slow periodic sweep (not every frame -- falling
        // to -20 takes real time, no need to check 60x/sec) rather than a
        // trigger volume, since a trigger would need to cover every map's
        // boundary by hand; this instead catches *any* way a player ends
        // up below the world, on any map, for free.
        [SerializeField] private float fallSafetyThresholdY = -20f;
        [SerializeField] private float fallSafetyCheckInterval = 1f;
        private float nextFallSafetyCheck;

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

            // Hides the host's own "Loading..." screen (RobEveryoneNetworkManager.
            // OnClientConnect) -- a hosting client never gets a real
            // OnClientSceneChanged callback for its own local connection
            // the way a genuine remote client does (there's no separate
            // client-side scene load to wait on, since the server
            // already loaded it here), so nothing would otherwise ever
            // hide it. OnStartServer only fires once per server lifetime
            // for this persistent, never-respawned object, so this can't
            // accidentally re-fire on every later round's scene change.
            // Harmless no-op on a headless dedicated server (nothing to
            // find/hide there) and on a joining, non-hosting client
            // (their own OnClientSceneChanged already covers it).
            HandleClientSceneChanged();
        }

        public override void OnStopServer()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void Update()
        {
            if (!isServer) return;
            if (Time.time < nextFallSafetyCheck) return;
            nextFallSafetyCheck = Time.time + fallSafetyCheckInterval;

            foreach (PlayerInventory player in PlayerInventory.AllPlayers)
            {
                if (player.transform.position.y >= fallSafetyThresholdY) continue;

                // Still ragdolling -- EndRagdoll will reposition them
                // based on the ragdoll body's own (separate) physics once
                // the stun ends, which would fight a rescue attempted
                // now. Leave them be; if they're still below the
                // threshold once the stun clears, the next tick catches
                // them then.
                PlayerImpactRelay relay = player.GetComponent<PlayerImpactRelay>();
                if (relay != null && relay.IsStunned) continue;

                RescuePlayer(player.transform);
            }
        }

        // Reuses PositionPlayer's own host-vs-remote split (see its
        // comment) rather than duplicating a second, subtly different
        // teleport path -- any spawn point is fine here (unlike
        // PositionPlayer's round-start distribution across several, this
        // is an emergency catch-all, not a fairness concern).
        [Server]
        private void RescuePlayer(Transform player)
        {
            PlayerSpawnPoint[] spawns = FindObjectsByType<PlayerSpawnPoint>(FindObjectsSortMode.None);
            if (spawns.Length == 0) return;

            TeleportPlayerTo(player, spawns[0].transform);
        }

        // Jail & Bail: teleports the just-caught (or end-of-batch-jailed)
        // player to a free JailPoint slot in the gameplay scene's real
        // jail cells.
        [Server]
        public void TeleportToJail(Transform player)
        {
            JailPoint slot = ClaimJailPoint(player.GetComponent<PlayerInventory>());
            if (slot != null) TeleportPlayerTo(player, slot.transform);
        }

        // Hands out a free JailPoint slot and records who's standing in
        // it (TeleportToJailExit below is what frees it back up again) --
        // spreads simultaneous jailings across however many physical
        // slots exist instead of stacking everyone on one marker.
        [Server]
        private JailPoint ClaimJailPoint(PlayerInventory player)
        {
            JailPoint[] slots = FindObjectsByType<JailPoint>(FindObjectsSortMode.None);
            if (slots.Length == 0) return null;

            foreach (JailPoint slot in slots)
            {
                if (occupiedJailPoints.ContainsValue(slot)) continue;
                if (player != null) occupiedJailPoints[player] = slot;
                return slot;
            }

            // Every slot already occupied (more jailed players than
            // physical cell slots) -- overlap the first one rather than
            // leaving this player un-teleported.
            return slots[0];
        }

        // Jail & Bail: teleports a just-released player (rescued or
        // self-bailed) to the jail's exit point -- also used to bring the
        // rescuer along, per gameplay-design.md's "teleports both players
        // outside the back door." Frees this player's claimed JailPoint
        // slot, if any (a no-op for the rescuer, who never claimed one).
        [Server]
        public void TeleportToJailExit(Transform player)
        {
            PlayerInventory inventory = player.GetComponent<PlayerInventory>();
            if (inventory != null) occupiedJailPoints.Remove(inventory);

            JailExitPoint exit = FindFirstObjectByType<JailExitPoint>();
            if (exit != null) TeleportPlayerTo(player, exit.transform);
        }

        // Jail & Bail: forwards a successful rescue to the active round's
        // own jailed-player bookkeeping (RoundManager.NotifyPlayerRescued)
        // so that player keeps playing this round instead of being
        // finalized Caught once it ends.
        [Server]
        public void HandlePlayerRescued(PlayerInventory player) => currentRoundManager?.NotifyPlayerRescued(player);

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
            roundManager.OnRoundStarted += HandleRoundStarted;
            roundOrdinal++;
        }

        // Fires every time a fresh round's RoundManager.StartRound() runs
        // -- registration above happens *before* OnStartServer's own
        // StartRound() call, so this subscription is already in place by
        // the time OnRoundStarted first invokes for a given RoundManager
        // instance. That also means this runs after that same round's own
        // resolvedPlayers/jailedPlayers.Clear(), so nothing here can race
        // against a stale jailedPlayers entry from the previous round.
        [Server]
        private void HandleRoundStarted()
        {
            // The gameplay scene's own JailPoint objects were just
            // recreated by this scene load -- any slot claimed by the
            // old, now-destroyed instances would be stale.
            occupiedJailPoints.Clear();

            // Self-bail: an end-of-batch jailed player is released
            // unconditionally after exactly one full round unrescued,
            // regardless of what happens in it.
            foreach (PlayerInventory player in PlayerInventory.AllPlayers)
            {
                JailState jail = player.GetComponent<JailState>();
                if (jail == null || !jail.IsJailed || !jail.IsEndOfBatchJail) continue;
                if (roundOrdinal > jail.JailedAtRoundOrdinal) jail.ForceRelease();
            }

            // Apply any end-of-batch jailing queued by the batch boundary
            // that just passed -- this round is that jailed player's
            // "round 1" of being caught, so JailedAtRoundOrdinal (used by
            // the self-bail check above on some *future* round) is set
            // relative to the current roundOrdinal, already incremented
            // for this round by RegisterRoundManager above.
            foreach (PlayerInventory player in pendingEndOfBatchJail)
            {
                player.GetComponent<JailState>()?.EnterJail(endOfBatch: true);
                currentRoundManager?.NotifyPlayerJailed(player, true);
            }
            pendingEndOfBatchJail.Clear();
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

            TeleportPlayerTo(player, spawns[index % spawns.Length].transform);
        }

        // Shared host-vs-remote-client teleport body -- originally
        // PositionPlayer's own, now also used by RescuePlayer and the two
        // Jail & Bail teleport helpers above so there's exactly one place
        // that has to know about the isLocalPlayer/ServerTeleport vs.
        // TargetRpc split.
        [Server]
        private void TeleportPlayerTo(Transform player, Transform target)
        {
            NetworkIdentity identity = player.GetComponent<NetworkIdentity>();
            bool remote = identity != null && !identity.isLocalPlayer && identity.connectionToClient != null;

            if (remote)
            {
                TargetPositionPlayer(identity.connectionToClient, target.position, target.rotation);
                return;
            }

            // Host's own player -- same process as the server, so
            // ServerTeleport both moves it and (via RpcTeleport to every
            // other client) resets everyone else's interpolation buffer
            // for it too.
            WithCharacterControllerDisabled(player, () =>
            {
                NetworkTransformReliable netTransform = player.GetComponent<NetworkTransformReliable>();
                if (netTransform != null) netTransform.ServerTeleport(target.position, target.rotation);
                else player.SetPositionAndRotation(target.position, target.rotation);
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
                currentRoundManager.OnRoundStarted -= HandleRoundStarted;
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

                    // Independent of wasCaught -- a player caught and
                    // rescued earlier in this same final round who's also
                    // under quota still gets end-of-batch jailed.
                    if (!metQuota) pendingEndOfBatchJail.Add(player);
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

                // Whatever ended the round (timeout, extraction, caught),
                // nobody should carry a body through the scene change --
                // confirmed bug: a carried player followed their carrier
                // into the Lobby. ServerDrop no-ops if this player wasn't
                // carrying anyone.
                CarryController carry = player.GetComponent<CarryController>();
                if (carry != null) carry.ServerDrop(false);
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
