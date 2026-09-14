using kcp2k;
using Mirror;
using UnityEngine;

namespace RobEveryone.Core
{
    // Dev-only local testing path -- entirely separate from the real
    // Host/Join buttons (SteamLobby.HostLobby/OpenOverlay), which stay
    // Steam-only and untouched. FizzySteamworks can't route a plain
    // "localhost" address (it expects a SteamID64), so testing on one
    // machine (e.g. two ParrelSync clones under the same logged-in
    // Steam account, which can't invite/accept a Steam lobby with
    // itself) needs KcpTransport active instead.
    //
    // Switches NetworkManager's active transport to Kcp for the rest of
    // this session -- there's no automatic switch back to
    // FizzySteamworks, so don't mix a local test and a real Steam
    // session in the same running instance/build.
    public class LocalTestingActions : MonoBehaviour
    {
        // Hides the whole local-test button group outside the Editor/a
        // Development Build, so a real distributed release never shows
        // it to an actual player.
        [SerializeField] private GameObject localTestPanel;

        // Resolved via GetComponent in Start, not Inspector-wired -- both
        // transports already live on the same GameObject as
        // NetworkManager, so there's nothing to get wrong by finding
        // them in code instead of a manual drag.
        private KcpTransport kcpTransport;

        // Start, not Awake -- NetworkManager.singleton is itself only set
        // in ITS OWN Awake, and Unity doesn't guarantee Awake order
        // across different GameObjects. Every Awake across the whole
        // scene is guaranteed to finish before any Start runs, so this
        // can't race it.
        private void Start()
        {
            if (localTestPanel != null) localTestPanel.SetActive(Debug.isDebugBuild);

            kcpTransport = RobEveryoneNetworkManager.singleton.GetComponent<KcpTransport>();

            if (kcpTransport == null)
                Debug.LogError("LocalTestingActions: no KcpTransport found on the NetworkManager GameObject -- Host/Join Local can't work.");
        }

        // Confirmed root cause (via hop-by-hop logging): setting
        // NetworkManager.transport alone isn't enough. Mirror actually
        // dispatches everything through the separate static
        // Transport.active, which NetworkManager.InitializeSingleton
        // only ever copies .transport into ONCE -- it short-circuits
        // with an early `if (singleton == this) return true;` on every
        // call after the very first (i.e. every StartHost/StartClient
        // after the one at scene load), never reaching the line that
        // would refresh Transport.active. So .transport correctly
        // becomes Kcp here, but Transport.active silently stayed
        // FizzySteamworks (whatever it was at the very first scene
        // load) for the rest of the session regardless. Setting
        // Transport.active directly is the actual fix.
        private void UseKcp()
        {
            if (kcpTransport == null) return;
            RobEveryoneNetworkManager.singleton.transport = kcpTransport;
            Transport.active = kcpTransport;
        }

        // Wire the "Host Local" button's OnClick to this.
        public void HostLocal()
        {
            UseKcp();
            RobEveryoneNetworkManager.singleton.StartHost();
        }

        // Wire the "Join Local" button's OnClick to this.
        public void JoinLocal()
        {
            UseKcp();
            RobEveryoneNetworkManager.singleton.networkAddress = "localhost";
            RobEveryoneNetworkManager.singleton.StartClient();
        }
    }
}
