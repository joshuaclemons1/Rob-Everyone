using System;
using System.Collections.Generic;
using Mirror;
using RobEveryone.Core;
using RobEveryone.Inventory;
using UnityEngine;

namespace RobEveryone.Round
{
    // Just how one player's round ended -- quota met/not-met is no longer
    // a per-round concept (see GameFlowManager's batch tracking), so this
    // only distinguishes a normal end (exit/timer) from being Caught.
    public enum RoundResult { RoundComplete, Caught }

    // Owns this round's countdown timer and, per player, whether they were
    // Caught or made it out. Ends the *round* only once every connected
    // player has individually resolved (caught or reached the exit) or the
    // timer runs out -- a single-player round always resolves the instant
    // its one player does either. Deliberately not persistent -- a fresh
    // instance exists each time the gameplay scene loads, syncing its
    // quota from GameFlowManager.CurrentQuota (which *is* persistent) so a
    // 3-round batch's quota survives the Lobby round-trip between rounds.
    //
    // Networking (Stage 4): server-authoritative (NetworkBehaviour,
    // isServer-equivalent via OnStartServer/[Server]). timeRemaining and
    // roundActive are SyncVars so every client's RoundUI shows the exact
    // same countdown instead of each ticking its own locally and drifting.
    // Being caught only ends *that* player's participation now (see
    // PoliceAI.CatchPlayer), not the whole round for everyone -- this
    // replaces the old single-player PartyGate: a caught player being
    // "resolved" immediately (instead of only resolvable by physically
    // reaching the exit) is what stops the round from deadlocking once
    // someone's jailed.
    public class RoundManager : NetworkBehaviour
    {
        [SerializeField] private int quota = 200;
        [SerializeField] private float roundDuration = 180f;

        [SyncVar] private float timeRemaining;
        [SyncVar] private bool roundActive;

        private readonly HashSet<PlayerInventory> resolvedPlayers = new();

        public int Quota => quota;
        public float RoundDuration => roundDuration;
        public float TimeRemaining => timeRemaining;
        public bool RoundActive => roundActive;

        // Server-only events -- GameFlowManager subscribes to these from
        // its own server-only code path (see HandlePlayerAdded/OnStartServer
        // there), never from a plain client.
        public event Action OnRoundStarted;
        public event Action<PlayerInventory, RoundResult> OnPlayerResolved;
        public event Action OnRoundEnded;

        public override void OnStartServer()
        {
            if (GameFlowManager.Instance != null)
            {
                quota = GameFlowManager.Instance.CurrentQuota;
                // Registering here (rather than GameFlowManager finding
                // this via FindFirstObjectByType off Unity's own
                // SceneManager.sceneLoaded) is what actually makes this
                // reliable -- Mirror disables every scene NetworkIdentity
                // by default and only re-enables this one inside
                // NetworkServer.SpawnObjects(), which runs *after*
                // sceneLoaded fires. OnStartServer only ever runs once
                // that's actually happened, so it's the one moment
                // GameFlowManager can safely be told about this round's
                // instance.
                GameFlowManager.Instance.RegisterRoundManager(this);
            }
            StartRound();
        }

        private void Update()
        {
            if (!isServer || !roundActive) return;

            timeRemaining -= Time.deltaTime;
            if (timeRemaining <= 0f)
            {
                ResolveRemainingPlayersOnTimeout();
                EndRound();
            }
        }

        [Server]
        public void StartRound()
        {
            resolvedPlayers.Clear();

            // Discards whatever carried loot (not Cash) survived from the
            // previous round -- unsold loot at ready-up is simply lost, a
            // deliberate consequence of Cash being the persistent value
            // now instead of TotalValue. Every connected player, not just
            // one.
            foreach (PlayerInventory player in PlayerInventory.AllPlayers)
            {
                player.ResetInventory();
            }

            timeRemaining = roundDuration;
            roundActive = true;
            OnRoundStarted?.Invoke();
        }

        [Server]
        public void NotifyPlayerReachedExit(PlayerInventory player)
        {
            ResolvePlayer(player, RoundResult.RoundComplete);
        }

        [Server]
        public void NotifyPlayerCaught(PlayerInventory player)
        {
            // Caught players lose whatever they were carrying -- an exited
            // (or timed-out) player keeps theirs to sell at the Lobby.
            player.ResetInventory();
            ResolvePlayer(player, RoundResult.Caught);
        }

        private void ResolvePlayer(PlayerInventory player, RoundResult result)
        {
            if (!roundActive) return;
            if (!resolvedPlayers.Add(player)) return; // already resolved -- ignore a second catch/exit

            OnPlayerResolved?.Invoke(player, result);

            if (resolvedPlayers.Count >= PlayerInventory.AllPlayers.Count)
            {
                EndRound();
            }
        }

        private void ResolveRemainingPlayersOnTimeout()
        {
            foreach (PlayerInventory player in PlayerInventory.AllPlayers)
            {
                if (resolvedPlayers.Contains(player)) continue;
                resolvedPlayers.Add(player);
                OnPlayerResolved?.Invoke(player, RoundResult.RoundComplete);
            }
        }

        private void EndRound()
        {
            if (!roundActive) return;
            roundActive = false;
            OnRoundEnded?.Invoke();
        }
    }
}
