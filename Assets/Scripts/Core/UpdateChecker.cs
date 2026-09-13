using System;
using System.Collections;
using RobEveryone.UI;
using UnityEngine;
using UnityEngine.Networking;

namespace RobEveryone.Core
{
    // Fire-and-forget check against GitHub's public Releases API for a
    // newer tag than this build's own Application.version -- this
    // project ships as a plain zip off GitHub Releases (no Steam depot,
    // no real auto-patcher), so the best we can do without a much bigger
    // update-installer project is tell the player a newer build exists
    // and point them at the download page.
    //
    // Convention: bump Player Settings' Version (Application.version) to
    // match the exact tag name of each new release before building (e.g.
    // "alpha-v2"). This does a plain string comparison, not real semver
    // range logic -- the two need to agree exactly for "you're current"
    // to read correctly.
    public class UpdateChecker : MonoBehaviour
    {
        private const string LatestReleaseUrl = "https://api.github.com/repos/joshuaclemons1/Rob-Everyone/releases/latest";

        [SerializeField] private UpdateAvailablePopupUI popup;

        // Only the two fields UpdateChecker actually reads -- JsonUtility
        // matches by field name and silently ignores everything else in
        // GitHub's much larger response object, so this doesn't need to
        // mirror the whole schema.
        [Serializable]
        private class ReleaseInfo
        {
            public string tag_name;
            public string html_url;
        }

        // Held at class level (not just local to the coroutine) so
        // OnDestroy can reach and dispose it if this object gets torn
        // down mid-request -- confirmed real hazard: accepting a Steam
        // invite mid-check changes scenes (MainMenu -> Lobby), which
        // would otherwise abandon the coroutine without ever letting its
        // `using` block run, leaking the request's native handle while
        // still in flight.
        private UnityWebRequest request;

        private void Awake()
        {
            // Never torn down by the MainMenu -> Lobby scene change a
            // Steam invite accept triggers -- this object only exists to
            // run one check, so DontDestroyOnLoad plus self-destruction
            // once that check finishes (success, failure, or popup
            // shown) is simpler than teaching the check itself to
            // survive an interrupted scene unload.
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            StartCoroutine(CheckForUpdate());
        }

        private void OnDestroy()
        {
            request?.Dispose();
        }

        private IEnumerator CheckForUpdate()
        {
            request = UnityWebRequest.Get(LatestReleaseUrl);
            // Mandatory -- GitHub's API outright rejects any request with
            // no User-Agent header (403), authenticated or not.
            request.SetRequestHeader("User-Agent", "RobEveryone-UpdateChecker");
            yield return request.SendWebRequest();

            // Fails open on any network hiccup/rate-limit/malformed
            // response -- a broken update check should never block
            // someone from playing, just silently skip the popup.
            if (request.result == UnityWebRequest.Result.Success)
            {
                ReleaseInfo release;
                try { release = JsonUtility.FromJson<ReleaseInfo>(request.downloadHandler.text); }
                catch { release = null; }

                if (release != null && !string.IsNullOrEmpty(release.tag_name) && release.tag_name != Application.version)
                {
                    if (popup != null) popup.Show(release.tag_name, release.html_url);
                }
            }

            // OnDestroy disposes `request` -- this object only ever runs
            // one check, so it's done its whole job the moment this
            // coroutine reaches here either way.
            Destroy(gameObject);
        }
    }
}
