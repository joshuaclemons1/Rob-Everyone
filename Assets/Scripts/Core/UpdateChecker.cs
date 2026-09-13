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

        private void Start()
        {
            StartCoroutine(CheckForUpdate());
        }

        private IEnumerator CheckForUpdate()
        {
            using UnityWebRequest request = UnityWebRequest.Get(LatestReleaseUrl);
            // Mandatory -- GitHub's API outright rejects any request with
            // no User-Agent header (403), authenticated or not.
            request.SetRequestHeader("User-Agent", "RobEveryone-UpdateChecker");
            yield return request.SendWebRequest();

            // Fails open on any network hiccup/rate-limit/malformed
            // response -- a broken update check should never block
            // someone from playing, just silently skip the popup.
            if (request.result != UnityWebRequest.Result.Success) yield break;

            ReleaseInfo release;
            try { release = JsonUtility.FromJson<ReleaseInfo>(request.downloadHandler.text); }
            catch { yield break; }

            if (release == null || string.IsNullOrEmpty(release.tag_name)) yield break;
            if (release.tag_name == Application.version) yield break; // already current

            if (popup != null) popup.Show(release.tag_name, release.html_url);
        }
    }
}
