using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RobEveryone.UI
{
    // Full-screen cover shown while a scene transition is actually
    // loading. Must live as a child of the "NetworkManager" GameObject in
    // MainMenu.unity (the one carrying RobEveryoneNetworkManager/
    // GameFlowManager, marked Don't Destroy On Load) -- NOT as a normal
    // object inside Lobby or SampleScene. Those two get destroyed and
    // recreated on every single round-trip; a copy living in just one of
    // them only ever exists for the handful of frames between a Show()
    // call and that scene's own teardown, which is exactly why this used
    // to flash on-screen for a fraction of a second with its spinner
    // frozen on frame 0 instead of actually covering the load, and never
    // appeared at all for the Lobby -> gameplay transition. The script's
    // own GameObject stays enabled at all times (so
    // FindFirstObjectByType can always find it) -- only `panel` (the
    // actual visible content) gets toggled by Show/Hide.
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
            if (messageText != null)
            {
                messageText.text = message;
                ShrinkToFitIfNeeded(messageText);
            }
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

        // Issue #15 fix. This field shows short static strings most of
        // the time ("Starting next round...") but also GameFlowManager's
        // dynamically-built batch-progress summary
        // ($"Batch progress: ${cash} cash + ${inventory} / ${quota}"),
        // whose length depends entirely on how much cash/inventory a
        // player is carrying -- a fixed font size tuned for the short
        // messages can run off the edge of the panel once those numbers
        // get large. Turns on TMP's own shrink-to-fit rather than hand-
        // picking a smaller size, and only the first time (doesn't fight
        // whatever's already configured if auto-size was already turned
        // on in the Editor) -- fontSizeMax is capped at whatever size was
        // already authored, so short messages still render exactly as
        // before and only long ones actually shrink.
        private static void ShrinkToFitIfNeeded(TextMeshProUGUI text)
        {
            if (text.enableAutoSizing) return;

            text.fontSizeMax = text.fontSize;
            text.fontSizeMin = Mathf.Max(1f, text.fontSize * 0.6f);
            text.enableAutoSizing = true;
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
