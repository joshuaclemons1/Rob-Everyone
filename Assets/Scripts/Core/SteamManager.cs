#if !DISABLESTEAMWORKS
using Steamworks;
using UnityEngine;

namespace RobEveryone.Core
{
    // Standard Steamworks.NET bootstrap -- this is the well-known
    // "SteamManager" sample from Steamworks.NET's own repo (Initialize/
    // RunCallbacks/Shutdown lifecycle), copied in as-is rather than
    // reinvented, since it's exactly what every Steamworks.NET + Mirror
    // integration guide expects to already exist. One of these needs to
    // live in the very first scene, alongside RobEveryoneNetworkManager
    // and SteamLobby -- see stage5-steam-multiplayer.md Part 3.
    [DisallowMultipleComponent]
    public class SteamManager : MonoBehaviour
    {
        protected static bool everInitialized;

        private static SteamManager instance;
        private static SteamManager Instance
        {
            get
            {
                if (instance == null) return new GameObject("SteamManager").AddComponent<SteamManager>();
                return instance;
            }
        }

        private bool initializedThisRun;

        public static bool Initialized => Instance.initializedThisRun;

        private void Awake()
        {
            if (instance != null)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            if (everInitialized)
            {
                // This should only ever happen if you Destroy() an
                // existing SteamManager instance and re-Awake another --
                // Steam doesn't support re-initializing per session.
                throw new System.Exception("Tried to Initialize the SteamAPI twice in one session.");
            }

            // steam_appid.txt (test AppID 480) must sit next to the .exe
            // (or in the project root during Editor testing) -- see
            // stage5-steam-multiplayer.md Part 3.
            if (!Packsize.Test())
            {
                Debug.LogError("[SteamManager] Packsize Test returned false, the wrong version of Steamworks.NET is being run in this platform.");
            }

            if (!DllCheck.Test())
            {
                Debug.LogError("[SteamManager] DllCheck Test returned false, one or more of the Steamworks binaries seems to be the wrong version.");
            }

            try
            {
                initializedThisRun = SteamAPI.Init();
            }
            catch (System.DllNotFoundException e)
            {
                Debug.LogError("[SteamManager] Could not load the Steam API DLL. This usually happens if Steam is not running. " + e);
                initializedThisRun = false;
            }

            everInitialized = true;
        }

        private void OnEnable()
        {
            if (instance == null) instance = this;
        }

        private void OnDestroy()
        {
            if (instance != this) return;

            instance = null;

            if (initializedThisRun) SteamAPI.Shutdown();
        }

        private void Update()
        {
            if (!initializedThisRun) return;

            SteamAPI.RunCallbacks();
        }
    }
}
#endif
