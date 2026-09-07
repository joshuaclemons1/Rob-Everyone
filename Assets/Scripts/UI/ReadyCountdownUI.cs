using RobEveryone.Shop;
using TMPro;
using UnityEngine;

namespace RobEveryone.UI
{
    // Shows a live "Departing in N..." readout while standing on the
    // Lobby's ReadySpot, hidden the rest of the time. Drag the scene's
    // ReadySpot into `readySpot` and a TextMeshProUGUI into `countdownText`.
    public class ReadyCountdownUI : MonoBehaviour
    {
        [SerializeField] private ReadySpot readySpot;
        [SerializeField] private TextMeshProUGUI countdownText;

        private void OnEnable()
        {
            if (readySpot == null) return;
            readySpot.OnCountdownTick += UpdateText;
            readySpot.OnCountdownCancelled += Hide;
            Hide();
        }

        private void OnDisable()
        {
            if (readySpot == null) return;
            readySpot.OnCountdownTick -= UpdateText;
            readySpot.OnCountdownCancelled -= Hide;
        }

        private void UpdateText(float remaining)
        {
            if (countdownText == null) return;
            countdownText.gameObject.SetActive(true);
            countdownText.text = $"Departing in {Mathf.CeilToInt(remaining)}...";
        }

        private void Hide()
        {
            if (countdownText != null) countdownText.gameObject.SetActive(false);
        }
    }
}
