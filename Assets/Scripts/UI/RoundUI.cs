using RobEveryone.Round;
using TMPro;
using UnityEngine;

namespace RobEveryone.UI
{
    // Shows quota and the countdown timer. The round-end result/batch-
    // progress message is shown by the loading screen instead (see
    // LoadingScreenUI) -- GameFlowManager passes it the same
    // RoundSummary.BuildMessage() text.
    public class RoundUI : MonoBehaviour
    {
        [SerializeField] private RoundManager roundManager;
        [SerializeField] private TextMeshProUGUI quotaText;
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private LevelBarUI timerBar;

        private void Update()
        {
            if (roundManager == null) return;

            if (quotaText != null)
            {
                quotaText.text = $"${roundManager.Quota}";
            }

            if (timerText != null)
            {
                int seconds = Mathf.Max(0, Mathf.CeilToInt(roundManager.TimeRemaining));
                timerText.text = $"{seconds / 60}:{seconds % 60:00}";
            }

            if (timerBar != null && roundManager.RoundDuration > 0f)
            {
                timerBar.SetRatio(roundManager.TimeRemaining / roundManager.RoundDuration);
            }
        }
    }
}
