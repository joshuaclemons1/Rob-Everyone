using System;
using RobEveryone.Core;
using RobEveryone.Inventory;
using UnityEngine;

namespace RobEveryone.Round
{
    // Just how the round ended -- quota met/not-met is no longer a
    // per-round concept (see GameFlowManager's batch tracking), so this
    // only distinguishes a normal end (exit/timer) from being Caught.
    public enum RoundResult { RoundComplete, Caught }

    // Owns this round's quota and countdown timer. Ends the round when the
    // timer runs out, when ExitPoint reports the player reached the exit, or
    // when PoliceAI reports a catch. Deliberately not persistent -- a fresh
    // instance exists each time the gameplay scene loads, syncing its quota
    // from GameFlowManager.CurrentQuota (which *is* persistent) so a 3-round
    // batch's quota survives the Lobby round-trip between rounds.
    public class RoundManager : MonoBehaviour
    {
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private int quota = 200;
        [SerializeField] private float roundDuration = 180f;

        private float timeRemaining;
        private bool roundActive;

        public int Quota => quota;
        public float RoundDuration => roundDuration;
        public float TimeRemaining => timeRemaining;
        public bool RoundActive => roundActive;

        public event Action OnRoundStarted;
        public event Action<RoundResult> OnRoundEnded;

        private void Start()
        {
            if (GameFlowManager.Instance != null) quota = GameFlowManager.Instance.CurrentQuota;
            StartRound();
        }

        private void Update()
        {
            if (!roundActive) return;

            timeRemaining -= Time.deltaTime;
            if (timeRemaining <= 0f)
            {
                EndRound();
            }
        }

        public void StartRound()
        {
            // Discards whatever carried loot (not Cash) survived from the
            // previous round -- unsold loot at ready-up is simply lost,
            // a deliberate consequence of Cash being the persistent value
            // now instead of TotalValue.
            if (playerInventory != null) playerInventory.ResetInventory();

            timeRemaining = roundDuration;
            roundActive = true;
            OnRoundStarted?.Invoke();
        }

        public void NotifyExitReached()
        {
            if (!roundActive) return;
            EndRound();
        }

        public void NotifyPlayerCaught()
        {
            if (!roundActive) return;
            roundActive = false;
            OnRoundEnded?.Invoke(RoundResult.Caught);
        }

        private void EndRound()
        {
            roundActive = false;
            OnRoundEnded?.Invoke(RoundResult.RoundComplete);
        }
    }
}
