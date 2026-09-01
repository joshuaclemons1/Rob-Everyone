using System;
using RobEveryone.Inventory;
using UnityEngine;

namespace RobEveryone.Round
{
    public enum RoundResult { QuotaMet, QuotaNotMet, Caught }

    // Owns the round's quota and countdown timer. Ends the round when the
    // timer runs out, when ExitPoint reports the player reached the exit, or
    // when PoliceAI reports a catch.
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
            bool metQuota = playerInventory != null && playerInventory.TotalValue >= quota;
            OnRoundEnded?.Invoke(metQuota ? RoundResult.QuotaMet : RoundResult.QuotaNotMet);
        }
    }
}
