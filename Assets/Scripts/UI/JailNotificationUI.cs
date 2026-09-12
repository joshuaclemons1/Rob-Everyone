using System.Collections;
using TMPro;
using UnityEngine;

namespace RobEveryone.UI
{
    // A big centered message shown only to a player the instant they're
    // jailed, covering the otherwise-abrupt teleport into a cell.
    // Deliberately separate from LoadingScreenUI -- that panel's Hide()
    // is coupled to real scene-transition timing
    // (RobEveryoneNetworkManager.OnClientSceneChanged), and jailing
    // doesn't change scenes, so reusing it risks the two colliding (e.g.
    // an end-of-batch jailing that lands right as a new round's own
    // scene load is finishing). This script's own GameObject stays
    // enabled at all times (so FindFirstObjectByType can always find it,
    // same convention as LoadingScreenUI) -- only `panel` toggles.
    public class JailNotificationUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TextMeshProUGUI messageText;

        private Coroutine hideCoroutine;

        public void Show(string message, float duration)
        {
            if (hideCoroutine != null) StopCoroutine(hideCoroutine);

            if (panel != null) panel.SetActive(true);
            if (messageText != null) messageText.text = message;

            hideCoroutine = StartCoroutine(HideAfterDelay(duration));
        }

        private IEnumerator HideAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (panel != null) panel.SetActive(false);
            hideCoroutine = null;
        }
    }
}
