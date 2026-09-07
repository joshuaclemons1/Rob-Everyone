using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RobEveryone.UI
{
    // Persistent (lives on the same DontDestroyOnLoad object GameFlowManager
    // carries the Player under) full-screen cover shown while a scene
    // transition is actually loading. The script's own GameObject stays
    // enabled at all times (so GameFlowManager can always find it) -- only
    // `panel` (the actual visible content) gets toggled by Show/Hide.
    public class LoadingScreenUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private Image spinnerImage;
        [SerializeField] private Sprite[] spinnerFrames;
        [SerializeField] private float framesPerSecond = 8f;
        // The actual scene load is often faster than this -- Hide() still
        // waits out the rest of this time from the matching Show() call, so
        // the panel (and its message) stays readable regardless of how fast
        // the scene behind it loads.
        [SerializeField] private float minimumDisplayDuration = 5f;

        private float frameTimer;
        private int frameIndex;
        private float shownAt;
        private Coroutine hideCoroutine;

        public void Show(string message)
        {
            if (hideCoroutine != null)
            {
                StopCoroutine(hideCoroutine);
                hideCoroutine = null;
            }

            if (panel != null) panel.SetActive(true);
            if (messageText != null) messageText.text = message;
            shownAt = Time.time;

            // Restart the animation from frame 0 each time it's shown,
            // rather than resuming wherever it happened to leave off.
            frameTimer = 0f;
            frameIndex = 0;
            if (spinnerImage != null && spinnerFrames != null && spinnerFrames.Length > 0)
            {
                spinnerImage.sprite = spinnerFrames[0];
            }
        }

        // Called once the scene load itself is actually done -- doesn't
        // necessarily hide immediately, see minimumDisplayDuration above.
        public void Hide()
        {
            float remaining = minimumDisplayDuration - (Time.time - shownAt);
            if (remaining > 0f)
            {
                hideCoroutine = StartCoroutine(HideAfterDelay(remaining));
            }
            else
            {
                HideNow();
            }
        }

        private IEnumerator HideAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            HideNow();
            hideCoroutine = null;
        }

        private void HideNow()
        {
            if (panel != null) panel.SetActive(false);
        }

        private void Update()
        {
            if (panel == null || !panel.activeSelf) return;
            if (spinnerImage == null || spinnerFrames == null || spinnerFrames.Length == 0) return;

            float frameDuration = 1f / Mathf.Max(framesPerSecond, 0.01f);
            frameTimer += Time.deltaTime;

            if (frameTimer >= frameDuration)
            {
                frameTimer -= frameDuration;
                frameIndex = (frameIndex + 1) % spinnerFrames.Length;
                spinnerImage.sprite = spinnerFrames[frameIndex];
            }
        }
    }
}
