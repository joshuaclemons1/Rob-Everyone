using kcp2k;
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

        // Resolved via GetComponent in Awake, not Inspector-wired --
        // confirmed bug: a Transport's own `.enabled` flag has zero
        // bearing on whether Mirror actually uses it (NetworkServer/
        // NetworkClient drive Transport.active's Update methods
        // directly, never through Unity's normal MonoBehaviour
        // dispatch), and NetworkManager.StartHost/StartClient falls
        // back to GetComponent<Transport>() -- which finds a component
        // regardless of enabled state -- the instant `transport` is
        // null. A slightly-off Inspector drag silently produced exactly
        // that null, and the fallback happened to grab FizzySteamworks
        // instead of Kcp, so "Host/Join Local" was still using Steam
        // sockets the whole time. Both transports already live on the
        // same GameObject as NetworkManager, so there's nothing to get
        // wrong by finding them in code instead.
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

            // Temporary hop-by-hop logging -- two fix attempts have
            // failed the exact same way (Host/Join Local still using
            // Steam sockets) despite this all checking out on paper, so
            // this pins down exactly which link in the chain is wrong
            // instead of guessing a third time.
            Debug.Log($"[LocalTestingActions] Start: kcpTransport={(kcpTransport == null ? "NULL" : kcpTransport.ToString())}, " +
                $"NetworkManager instance={RobEveryoneNetworkManager.singleton.GetInstanceID()}, " +
                $"current transport={RobEveryoneNetworkManager.singleton.transport}");

            if (kcpTransport == null)
                Debug.LogError("LocalTestingActions: no KcpTransport found on the NetworkManager GameObject -- Host/Join Local can't work.");
        }

        private void UseKcp()
        {
            Debug.Log($"[LocalTestingActions] UseKcp: kcpTransport={(kcpTransport == null ? "NULL" : kcpTransport.ToString())}, " +
                $"transport BEFORE={RobEveryoneNetworkManager.singleton.transport}");

            if (kcpTransport == null) return;
            RobEveryoneNetworkManager.singleton.transport = kcpTransport;

            Debug.Log($"[LocalTestingActions] UseKcp: transport AFTER={RobEveryoneNetworkManager.singleton.transport}, " +
                $"Transport.active={Mirror.Transport.active}");
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
