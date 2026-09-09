using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using RobEveryone.Inventory;
using UnityEngine;

namespace RobEveryone.Shop
{
    // Lobby-scene ready-up pad -- stand on it to start a visible countdown
    // toward the next round, walk off to cancel it. Ready-up is
    // deliberately diegetic (walk onto a spot), not a UI button, per
    // gameplay-design.md's Shop phase design.
    //
    // Networking (Stage 4): every client has its own local copy of this
    // trigger volume, so OnTriggerEnter/Exit are gated to the server the
    // same way ExitPoint's are. The countdown now only starts once *every*
    // connected player is simultaneously standing on a ready spot (not
    // just one) -- anyone stepping off before it finishes cancels it for
    // the whole group. OnCountdownTick/OnCountdownCancelled are broadcast
    // via ClientRpc so every client's ReadyCountdownUI shows the exact
    // same number, not each independently timing their own local guess.
    [RequireComponent(typeof(Collider))]
    public class ReadySpot : NetworkBehaviour
    {
        [SerializeField] private float readyDelay = 5f;

        private readonly HashSet<PlayerInventory> readyPlayers = new();
        private Coroutine readyRoutine;

        // Server-only -- GameFlowManager subscribes from its own
        // server-only code path.
        public event Action OnAllPlayersReady;

        // Client-side UI hookup -- ReadyCountdownUI subscribes to these
        // the same way it always has; they're now driven by ClientRpc
        // calls below instead of firing directly from the coroutine.
        public event Action<float> OnCountdownTick;
        public event Action OnCountdownCancelled;
        // Fired once, right as the countdown finishes successfully (not
        // on a cancel -- that's OnCountdownCancelled above). Without
        // this, the last tick's text (e.g. "Departing in 1...") just sits
        // there forever once the coroutine stops ticking -- nothing ever
        // told the UI the countdown was actually done rather than still
        // running.
        public event Action OnCountdownComplete;

        private void OnTriggerEnter(Collider other)
        {
            if (!isServer) return;

            PlayerInventory player = other.GetComponentInParent<PlayerInventory>();
            if (player == null) return;

            readyPlayers.Add(player);

            if (readyRoutine == null && readyPlayers.Count >= PlayerInventory.AllPlayers.Count && PlayerInventory.AllPlayers.Count > 0)
            {
                readyRoutine = StartCoroutine(Countdown());
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!isServer) return;

            PlayerInventory player = other.GetComponentInParent<PlayerInventory>();
            if (player == null) return;

            readyPlayers.Remove(player);

            if (readyRoutine == null) return;

            StopCoroutine(readyRoutine);
            readyRoutine = null;
            RpcCountdownCancelled();
        }

        private IEnumerator Countdown()
        {
            float remaining = readyDelay;
            while (remaining > 0f)
            {
                RpcCountdownTick(remaining);
                yield return null;
                remaining -= Time.deltaTime;
            }

            readyRoutine = null;
            readyPlayers.Clear();
            RpcCountdownComplete();
            OnAllPlayersReady?.Invoke();
        }

        [ClientRpc]
        private void RpcCountdownTick(float remaining) => OnCountdownTick?.Invoke(remaining);

        [ClientRpc]
        private void RpcCountdownCancelled() => OnCountdownCancelled?.Invoke();

        [ClientRpc]
        private void RpcCountdownComplete() => OnCountdownComplete?.Invoke();
    }
}
