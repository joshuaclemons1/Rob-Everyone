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

        // The flythrough camera's own orbit math still lives entirely in
        // MenuBackgroundCamera (on Main Camera) -- this is just a more
        // convenient single Inspector to tune it from, since everything
        // else about the background already lives here. Pushed into the
        // referenced component once in Awake (see ApplyCameraSettings);
        // editing MenuBackgroundCamera's own fields directly still works
        // too if backgroundCamera isn't wired.
        [Header("Camera Orbit")]
        [SerializeField] private MenuBackgroundCamera backgroundCamera;
        [SerializeField] private Vector3 orbitCenter = Vector3.zero;
        [SerializeField] private float orbitRadius = 40f;
        [SerializeField] private float orbitHeight = 28f;
        [SerializeField] private float orbitSpeedDegreesPerSecond = 2.5f;
        [SerializeField] private float lookAheadDegrees = 20f;
        [SerializeField] private float lookTargetRadiusFraction = 0.35f;
        [SerializeField] private float bobAmplitude = 1.5f;
        [SerializeField] private float bobCyclesPerSecond = 0.05f;

        private Scene loadedLobbyScene;
        private bool lobbySceneLoaded;

        private void Awake()
        {
            foreach (Transform root in legacyDioramaRoots)
            {
                if (root != null) root.gameObject.SetActive(false);
            }

            ApplyCameraSettings();

            // Subscribed before the load starts and unsubscribed the
            // instant it fires, so this only ever captures *this* load --
            // never a later real "Lobby" load Mirror kicks off once a
            // player actually hosts/joins.
            SceneManager.sceneLoaded += OnLobbySceneLoaded;
            SceneManager.LoadSceneAsync(lobbySceneName, LoadSceneMode.Additive);
        }

        private void ApplyCameraSettings()
        {
            if (backgroundCamera == null) return;

            backgroundCamera.OrbitCenter = orbitCenter;
            backgroundCamera.OrbitRadius = orbitRadius;
            backgroundCamera.OrbitHeight = orbitHeight;
            backgroundCamera.OrbitSpeedDegreesPerSecond = orbitSpeedDegreesPerSecond;
            backgroundCamera.LookAheadDegrees = lookAheadDegrees;
            backgroundCamera.LookTargetRadiusFraction = lookTargetRadiusFraction;
            backgroundCamera.BobAmplitude = bobAmplitude;
            backgroundCamera.BobCyclesPerSecond = bobCyclesPerSecond;
        }

#if UNITY_EDITOR
        // Lets the orbit be re-tuned live without stopping Play mode --
        // tweak the fields above, they reapply automatically. Editor-only
        // (OnValidate is compiled out of real builds anyway) and no-ops
        // safely outside Play mode since ApplyCameraSettings only ever
        // touches the referenced component's own fields, not anything
        // that requires the scene to be running.
        private void OnValidate()
        {
            ApplyCameraSettings();
        }
#endif

        private void OnLobbySceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != lobbySceneName) return;

            SceneManager.sceneLoaded -= OnLobbySceneLoaded;
            loadedLobbyScene = scene;
            lobbySceneLoaded = true;

            PrepareLobbyForBackground(scene);
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
        //
        // Lobby's own lights (Light/WallLight/LightPole/Lamp -- a good
        // number of them) normally only ever run alone, as the one
        // active scene during a real round. Loaded in here alongside
        // whatever Main Menu itself lights with, all of them casting
        // shadows at once overflowed URP's shadow atlas (confirmed via
        // the Console: "31 shadow maps" fighting over a 4096x4096
        // atlas). This is purely a background flythrough, not a lit
        // gameplay space anyone stands in, so shadows are switched off
        // per light here instead of raising the atlas size or shrinking
        // shadow resolution project-wide -- a change that would affect
        // every *real* lit space too, for a problem only this decorative
        // scene has.
        private static void PrepareLobbyForBackground(Scene scene)
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

                foreach (Light light in root.GetComponentsInChildren<Light>(true))
                {
                    light.shadows = LightShadows.None;
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
