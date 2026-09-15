using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace RobEveryone.UI
{
    // Issue #51 (revised): additively loads the REAL Lobby scene behind
    // the Main Menu for MenuBackgroundCamera to fly over, replacing a
    // first attempt that procedurally built an abstract stand-in
    // neighborhood -- confirmed working, but real playtest feedback was
    // that it didn't actually look like either playable area, which was
    // the whole point. This is the issue's own "option 1" after all
    // (additively load a real scene), just Lobby instead of SampleScene.
    //
    // Lobby, not SampleScene: confirmed by reading Lobby.unity directly
    // that it has none of the risk factors the issue's own feasibility
    // notes raised against SampleScene -- zero PoliceAI/HomeownerAI
    // (both NetworkBehaviours that lean on a live RoundManager/server
    // context Main Menu doesn't have), zero NetworkManager, zero
    // AudioListener. Its ~19 scene-placed NetworkIdentity objects (shop
    // items, pickups) already carry valid baked sceneIds from Lobby's own
    // normal use as a real, already-shipping gameplay scene, and Main
    // Menu's own NetworkManager never auto-starts a server/client by
    // itself -- only SteamLobby's Host/Join buttons do (see that script)
    // -- so loading Lobby purely as an inert, client-side decorative
    // scene should leave those objects untouched.
    //
    // The one real risk that *does* need handling: if a player actually
    // clicks Host/Join while this decorative copy is still loaded, Mirror
    // will load its own real "Lobby" scene on top of it -- two scenes
    // with the same name loaded at once is asking for trouble (duplicate
    // NetworkIdentity scene objects, ambiguous SceneManager lookups by
    // name). Handled below by watching for NetworkServer/NetworkClient
    // going active and immediately unloading this specific decorative
    // Scene handle (captured directly from this script's own load, never
    // looked up by name) before Mirror's own scene transition can land.
    public class MenuBackgroundBuilder : MonoBehaviour
    {
        [SerializeField] private string lobbySceneName = "Lobby";

        // The old single-house diorama's root transforms (Roads, and the
        // Exterior/Fence/Grass bundle) -- disabled rather than deleted,
        // so this stays reversible instead of destructively editing
        // scene content that was hand-placed once already. Safe to
        // delete for real once this is confirmed working in the Editor.
        [SerializeField] private List<Transform> legacyDioramaRoots = new();

        private Scene loadedLobbyScene;
        private bool lobbySceneLoaded;

        private void Awake()
        {
            foreach (Transform root in legacyDioramaRoots)
            {
                if (root != null) root.gameObject.SetActive(false);
            }

            // Subscribed before the load starts and unsubscribed the
            // instant it fires, so this only ever captures *this* load --
            // never a later real "Lobby" load Mirror kicks off once a
            // player actually hosts/joins.
            SceneManager.sceneLoaded += OnLobbySceneLoaded;
            SceneManager.LoadSceneAsync(lobbySceneName, LoadSceneMode.Additive);
        }

        private void OnLobbySceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != lobbySceneName) return;

            SceneManager.sceneLoaded -= OnLobbySceneLoaded;
            loadedLobbyScene = scene;
            lobbySceneLoaded = true;

            DisableLobbyUI(scene);
        }

        // Lobby.unity ships its own root-level "Canvas" (hotbar, cash
        // bar, pause menu, etc.) -- a Screen Space Overlay canvas like
        // Main Menu's own, so with nothing to stop it, it renders
        // directly on top of the menu regardless of which camera is
        // active. Confirmed showing through in a real Play-mode test.
        // Only the 3D geometry is wanted here, so every Canvas this
        // additively-loaded scene brings in gets disabled -- found by
        // component rather than hardcoding the name "Canvas", so this
        // still works correctly if Lobby's UI hierarchy ever changes.
        //
        // Same problem, same fix, for Lobby's own EventSystem -- uGUI
        // only ever wants exactly one active in the scene at a time, and
        // Main Menu already has its own. Confirmed spamming "There are 2
        // event systems in the scene" once Lobby's copy loaded in too.
        private static void DisableLobbyUI(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
                {
                    canvas.gameObject.SetActive(false);
                }

                foreach (EventSystem eventSystem in root.GetComponentsInChildren<EventSystem>(true))
                {
                    eventSystem.gameObject.SetActive(false);
                }
            }
        }

        private void Update()
        {
            // The moment real networking starts (Host or Join), tear this
            // decorative copy down before Mirror's own scene transition
            // can load a second, real "Lobby" alongside it.
            if (lobbySceneLoaded && (NetworkServer.active || NetworkClient.active))
            {
                lobbySceneLoaded = false;
                if (loadedLobbyScene.IsValid() && loadedLobbyScene.isLoaded)
                {
                    SceneManager.UnloadSceneAsync(loadedLobbyScene);
                }
            }
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnLobbySceneLoaded;
        }
    }
}
