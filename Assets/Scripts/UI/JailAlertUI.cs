using System.Collections;
using TMPro;
using UnityEngine;

namespace RobEveryone.UI
{
    // Top-of-screen fading alert shown to every OTHER client (the caught
    // player themselves already gets their own full message via
    // JailNotificationUI -- see JailState.RpcAnnounceJailed's own skip
    // check) whenever anyone gets jailed. Lets the rest of the lobby know
    // there's a rescue opportunity and what it pays without having to
    // stumble across the cell.
    public class JailAlertUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI alertText;
        [SerializeField] private float displayDuration = 4f;

        private Coroutine hideCoroutine;

        public void Show(string message)
        {
            if (alertText == null) return;
            if (hideCoroutine != null) StopCoroutine(hideCoroutine);

            alertText.text = message;
            alertText.enabled = true;
            hideCoroutine = StartCoroutine(HideAfterDelay());
        }

        private IEnumerator HideAfterDelay()
        {
            yield return new WaitForSeconds(displayDuration);
            alertText.enabled = false;
            hideCoroutine = null;
        }
    }
}
