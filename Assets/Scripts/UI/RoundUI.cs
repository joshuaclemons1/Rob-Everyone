using RobEveryone.Round;
using TMPro;
using UnityEngine;

namespace RobEveryone.UI
{
    // Shows quota and the countdown timer, plus a result banner once the
    // round ends. Drag the scene's RoundManager into `roundManager`.
    public class RoundUI : MonoBehaviour
    {
        [SerializeField] private RoundManager roundManager;
        [SerializeField] private TextMeshProUGUI quotaText;
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private TextMeshProUGUI resultText;

        private void OnEnable()
        {
            if (roundManager == null) return;
            roundManager.OnRoundEnded += ShowResult;
            if (resultText != null) resultText.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            if (roundManager == null) return;
            roundManager.OnRoundEnded -= ShowResult;
        }

        private void Update()
        {
            if (roundManager == null) return;

            if (quotaText != null)
            {
                quotaText.text = $"Quota: ${roundManager.Quota}";
            }

            if (timerText != null)
            {
                int seconds = Mathf.Max(0, Mathf.CeilToInt(roundManager.TimeRemaining));
                timerText.text = $"{seconds / 60}:{seconds % 60:00}";
            }
        }

        private void ShowResult(RoundResult result)
        {
            if (resultText == null) return;
            resultText.gameObject.SetActive(true);
            resultText.text = result switch
            {
                RoundResult.QuotaMet => "Quota met!",
                RoundResult.Caught => "Caught by the police!",
                _ => "Quota not met.",
            };
        }
    }
}
