using System;
using System.Collections;
using RobEveryone.Inventory;
using UnityEngine;

namespace RobEveryone.Shop
{
    // Lobby-scene ready-up pad -- stand on it to start a visible countdown
    // toward the next round, walk off to cancel it. Ready-up is
    // deliberately diegetic (walk onto a spot), not a UI button, per
    // gameplay-design.md's Shop phase design.
    [RequireComponent(typeof(Collider))]
    public class ReadySpot : MonoBehaviour
    {
        [SerializeField] private float readyDelay = 5f;

        private Coroutine readyRoutine;

        public event Action OnPlayerReady;
        // Fires every frame while the countdown is running, with the
        // remaining seconds -- ReadyCountdownUI uses this to show a live
        // "Departing in N..." readout instead of an invisible delay.
        public event Action<float> OnCountdownTick;
        public event Action OnCountdownCancelled;

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<PlayerInventory>() == null) return;
            if (readyRoutine != null) return;

            readyRoutine = StartCoroutine(Countdown());
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.GetComponentInParent<PlayerInventory>() == null) return;
            if (readyRoutine == null) return;

            StopCoroutine(readyRoutine);
            readyRoutine = null;
            OnCountdownCancelled?.Invoke();
        }

        private IEnumerator Countdown()
        {
            float remaining = readyDelay;
            while (remaining > 0f)
            {
                OnCountdownTick?.Invoke(remaining);
                yield return null;
                remaining -= Time.deltaTime;
            }

            readyRoutine = null;
            OnPlayerReady?.Invoke();
        }
    }
}
