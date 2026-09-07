using System.Collections;
using RobEveryone.Inventory;
using RobEveryone.Player;
using RobEveryone.Round;
using RobEveryone.Shop;
using RobEveryone.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RobEveryone.Core
{
    // Persistent (DontDestroyOnLoad) singleton owning every scene
    // transition between the gameplay map and the Lobby, and the player's
    // repositioning after each load. Exists because the Lobby is a real
    // separate scene (large open area + buildings, doubling as the future
    // pre-game multiplayer lobby) rather than a room tucked into the
    // gameplay map -- round-specific objects (RoundManager, houses,
    // police, exit) are NOT persistent, and are found fresh each time
    // their scene loads, which is fine since round state should reset
    // anyway.
    //
    // Only place one of these in the very first scene that loads
    // (currently SampleScene) -- it carries itself (and the player)
    // forward into the Lobby and back automatically from then on.
    public class GameFlowManager : MonoBehaviour
    {
        public static GameFlowManager Instance { get; private set; }

        [SerializeField] private string gameplaySceneName = "SampleScene";
        [SerializeField] private string lobbySceneName = "Lobby";
        // How much higher the quota goes each batch -- gameplay-design.md
        // leaves the exact curve as an open question, this is a tunable
        // starting point (200 -> 300 -> 450 -> ...).
        [SerializeField] private float quotaGrowthMultiplier = 1.5f;

        private CharacterController playerController;
        private PlayerInventory playerInventory;
        private Camera playerCamera;
        private LoadingScreenUI loadingScreen;
        private RoundManager currentRoundManager;

        // Snapshot of the most recently-seen round's quota -- RoundManager
        // itself isn't persistent (a fresh one exists each time the
        // gameplay scene loads), so anything in the Lobby wanting to
        // compare against "the quota" (e.g. a real Cash progress bar)
        // reads this instead of trying to reach into a scene it doesn't
        // exist in.
        public int LastQuota { get; private set; }

        // The actual persistent batch state -- RoundManager syncs its own
        // Quota from CurrentQuota at Start(), since RoundManager itself
        // gets recreated fresh every time the gameplay scene loads and
        // would otherwise silently reset the quota back to its Inspector
        // default every round, breaking the whole batch concept.
        public int CurrentQuota { get; private set; } = 200;
        public int BatchNumber { get; private set; } = 1;
        public int RoundInBatch { get; private set; } = 1; // 1, 2, or 3

        // Picks the loading screen's message text -- see HandleRoundEnded
        // for how each field gets decided. Plain `set`, not `init` --
        // Unity's runtime library doesn't ship the IsExternalInit marker
        // type init accessors need, even though the compiler otherwise
        // accepts the syntax.
        public struct RoundSummary
        {
            public bool WasCaught { get; set; }
            public bool IsFinalRoundOfBatch { get; set; }
            public bool MetQuota { get; set; } // only meaningful if IsFinalRoundOfBatch
            public int Cash { get; set; }
            public int Quota { get; set; }

            public string BuildMessage()
            {
                return WasCaught
                    ? "Caught by the police!"
                    : IsFinalRoundOfBatch
                        ? (MetQuota ? "Batch quota met!" : "Batch quota not met.")
                        : $"Batch progress: ${Cash} / ${Quota}";
            }
        }

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance == this) SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // First scene this singleton has seen -- find the player once
            // and carry it forward across every future load too.
            if (playerController == null)
            {
                playerInventory = FindFirstObjectByType<PlayerInventory>();
                if (playerInventory != null)
                {
                    playerController = playerInventory.GetComponent<CharacterController>();
                    playerCamera = playerInventory.GetComponentInChildren<Camera>(true);
                    DontDestroyOnLoad(playerInventory.transform.root.gameObject);
                }
            }

            // Same idea, separately -- the loading screen needs its own
            // DontDestroyOnLoad object (a Canvas can't be "half persistent"
            // alongside the rest of a scene's non-persistent UI), found
            // once here rather than requiring it to live under the Player.
            if (loadingScreen == null)
            {
                loadingScreen = FindFirstObjectByType<LoadingScreenUI>();
                if (loadingScreen != null)
                {
                    DontDestroyOnLoad(loadingScreen.transform.root.gameObject);
                    loadingScreen.Hide();
                }
            }

            if (playerController == null) return;

            PlayerSpawnPoint spawn = FindFirstObjectByType<PlayerSpawnPoint>();
            if (spawn != null)
            {
                // Disable/re-enable around the position change so the
                // CharacterController doesn't try to resolve the jump as
                // a collision -- same reasoning as DoorTeleporter.
                playerController.enabled = false;
                playerController.transform.SetPositionAndRotation(spawn.transform.position, spawn.transform.rotation);
                playerController.enabled = true;
            }

            // The Player (and its camera) only ever exists in a freshly-
            // loaded scene via the DontDestroyOnLoad carry-over above --
            // a scene like the Lobby has no camera authored in it at all
            // for a Canvas's "Render Camera" field to reference at edit
            // time. Fill it in here instead, once the real camera
            // actually exists, for any Canvas that's in Screen Space -
            // Camera mode but was left without one.
            if (playerCamera != null)
            {
                foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                {
                    if (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera == null)
                    {
                        canvas.worldCamera = playerCamera;
                    }
                }
            }

            if (scene.name == gameplaySceneName)
            {
                currentRoundManager = FindFirstObjectByType<RoundManager>();
                if (currentRoundManager != null) currentRoundManager.OnRoundEnded += HandleRoundEnded;
            }
            else if (scene.name == lobbySceneName)
            {
                ReadySpot readySpot = FindFirstObjectByType<ReadySpot>();
                if (readySpot != null) readySpot.OnPlayerReady += HandlePlayerReady;
            }
        }

        private void HandleRoundEnded(RoundResult result)
        {
            // Captured here, before the gameplay scene (and its
            // RoundManager) unloads -- see LastQuota's own comment.
            if (currentRoundManager != null) LastQuota = currentRoundManager.Quota;

            bool wasCaught = result == RoundResult.Caught;
            if (wasCaught && playerInventory != null)
            {
                playerInventory.ResetInventory();
            }

            // Only the batch's 3rd round actually decides anything --
            // rounds 1-2 just show cumulative progress toward the same
            // fixed quota, per gameplay-design.md's Quota Batches section.
            bool isFinalRound = RoundInBatch >= 3;
            bool metQuota = false;

            if (isFinalRound)
            {
                metQuota = playerInventory != null && playerInventory.Cash >= CurrentQuota;

                // Anti-hoarding: Cash above quota is deleted at the batch
                // boundary, not carried forward indefinitely.
                if (playerInventory != null) playerInventory.WipeCashSurplus(CurrentQuota);

                CurrentQuota = Mathf.RoundToInt(CurrentQuota * quotaGrowthMultiplier);
                BatchNumber++;
                RoundInBatch = 1;
            }
            else
            {
                RoundInBatch++;
            }

            var summary = new RoundSummary
            {
                WasCaught = wasCaught,
                IsFinalRoundOfBatch = isFinalRound,
                MetQuota = metQuota,
                Cash = playerInventory != null ? playerInventory.Cash : 0,
                Quota = LastQuota,
            };

            // Undo PoliceAI.CatchPlayer's permanent disable if that's what
            // ended the round -- no-op if the round ended some other way.
            FirstPersonController fpc = playerController.GetComponent<FirstPersonController>();
            if (fpc != null) fpc.enabled = true;

            StartCoroutine(TransitionToScene(lobbySceneName, summary.BuildMessage()));
        }

        private void HandlePlayerReady()
        {
            // The gameplay scene's fresh RoundManager calls StartRound()
            // itself in its own Start(), so nothing further is needed
            // beyond the load itself.
            StartCoroutine(TransitionToScene(gameplaySceneName, "Starting next round..."));
        }

        // Covers the screen with a loading spinner (showing the round
        // result/batch-progress message) for the actual scene load --
        // LoadSceneAsync rather than the plain synchronous LoadScene
        // specifically so the spinner keeps animating while the new scene
        // is loading, instead of the whole game freezing on the last
        // frame of the old one. LoadingScreenUI.Hide() decides on its own
        // whether to actually hide yet or hold a bit longer -- see its
        // minimumDisplayDuration.
        private IEnumerator TransitionToScene(string sceneName, string loadingMessage)
        {
            if (loadingScreen != null) loadingScreen.Show(loadingMessage);

            AsyncOperation load = SceneManager.LoadSceneAsync(sceneName);
            while (load != null && !load.isDone) yield return null;

            if (loadingScreen != null) loadingScreen.Hide();
        }
    }
}
