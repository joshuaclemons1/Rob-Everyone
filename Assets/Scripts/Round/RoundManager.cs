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
        [SerializeField] private float roundDuration = 300f;

        [SyncVar] private float timeRemaining;
        [SyncVar] private bool roundActive;

        private readonly HashSet<PlayerInventory> resolvedPlayers = new();
        // Caught-but-not-yet-finalized players (Jail & Bail) -- kept
        // separate from resolvedPlayers so a rescue (NotifyPlayerRescued)
        // can pull someone back out and keep the round running for them.
        // Only actually counted as Caught (folded into resolvedPlayers)
        // once the round ends with them still in here -- CheckForEarlyEnd
        // or the timeout branch below.
        private readonly HashSet<PlayerInventory> jailedPlayers = new();
        // Issue #48/#49: a player who's ridden out their own exit-car
        // carWaitDuration -- safe now, but NOT yet permanently resolved.
        // They can still press E to climb back out and keep playing
        // (ExitCarState.CmdExitCar -> NotifyPlayerUnready) right up until
        // the whole group is accounted for. Kept separate from
        // resolvedPlayers for exactly that reason -- only folded in once
        // CheckForEarlyEnd's group-wide condition actually passes.
        private readonly HashSet<PlayerInventory> readyAtExit = new();

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
                // Still-jailed players time out as Caught, not as a normal
                // RoundComplete -- finalize them first so the timeout loop
                // below doesn't also resolve them the other way. A
                // still-ready-at-exit player gets swept into
                // ResolveRemainingPlayersOnTimeout below as a normal
                // RoundComplete same as anyone else not yet resolved --
                // readyAtExit itself just needs clearing after, since
                // nothing above removes individual entries from it.
                FinalizeJailedPlayersAsCaught();
                ResolveRemainingPlayersOnTimeout();
                readyAtExit.Clear();
                EndRound();
            }
        }

        [Server]
        public void StartRound()
        {
            resolvedPlayers.Clear();
            jailedPlayers.Clear();
            readyAtExit.Clear();

            // Discards whatever carried *loot* (not Cash, not sabotage
            // items) survived from the previous round -- unsold loot at
            // ready-up is simply lost, a deliberate consequence of Cash
            // being the persistent value now instead of TotalValue.
            // Sabotage items bought in the shop persist across this
            // round-trip on purpose (ClearLoot leaves them alone) --
            // only dropping the item or running out of uses removes
            // them. Every connected player, not just one.
            foreach (PlayerInventory player in PlayerInventory.AllPlayers)
            {
                player.ClearLoot();
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

        // Called by ExitCarState once a seated player's own carWaitDuration
        // elapses -- they're safe now, but only counted as "accounted for"
        // toward CheckForEarlyEnd, not permanently resolved yet. A player
        // who later changes their mind and climbs back out
        // (NotifyPlayerUnready) removes themselves from this again and
        // keeps playing.
        [Server]
        public void NotifyPlayerReady(PlayerInventory player)
        {
            if (!roundActive) return;
            if (resolvedPlayers.Contains(player) || jailedPlayers.Contains(player) || readyAtExit.Contains(player)) return;

            readyAtExit.Add(player);
            CheckForEarlyEnd();
        }

        // Called by ExitCarState.CmdExitCar when a player who'd already
        // ridden out their own carWaitDuration chooses to climb back out
        // anyway, before the group as a whole ever resolved.
        [Server]
        public void NotifyPlayerUnready(PlayerInventory player) => readyAtExit.Remove(player);

        // Jail & Bail: no longer resolves the round for this player
        // immediately. Loot loss (below) is instant and irreversible, but
        // the round outcome itself stays open -- JailState.EnterJail puts
        // them in a real cell, and only a still-jailed player at the
        // moment the round actually ends gets finalized as Caught
        // (FinalizeJailedPlayersAsCaught). A rescue before then
        // (NotifyPlayerRescued) keeps them playing this same round.
        [Server]
        public void NotifyPlayerCaught(PlayerInventory player)
        {
            // Issue #77: caught with nothing to actually arrest them over
            // (no real stolen loot -- sabotage tools and the Prison Wallet
            // both excluded, see HasAnyStolenLoot's own comment) means
            // Police search and let them go instead of jailing them --
            // rewards dumping your loot under pressure as its own valid
            // way to survive a chase. Checked before DropAllSlots below so
            // an empty-handed player (nothing to drop anyway) takes this
            // path cleanly rather than falling through.
            if (!player.HasAnyStolenLoot())
            {
                player.GetComponent<JailState>()?.NotifySearchedAndReleased();
                return;
            }

            // Caught players lose whatever they were carrying -- an exited
            // (or timed-out) player keeps theirs to sell at the Lobby.
            // Issue #81: dropped on the ground at the catch spot as real,
            // pickupable items instead of ResetInventory's old silent
            // full wipe -- DropAllSlots already excludes the Prison
            // Wallet the same way ResetInventory did, so that slot stays
            // protected exactly as before.
            player.DropAllSlots(player.transform.position, player.transform.rotation);
            player.GetComponent<JailState>()?.EnterJail(endOfBatch: false);
            NotifyPlayerJailed(player, endOfBatch: false);
        }

        // Also called directly by GameFlowManager.HandleRoundStarted for
        // the separate end-of-batch quota-failure jailing (that jailing
        // itself, EnterJail, is called there too -- this just adds the
        // round-tracking half).
        [Server]
        public void NotifyPlayerJailed(PlayerInventory player, bool endOfBatch)
        {
            if (!roundActive) return;
            if (resolvedPlayers.Contains(player) || jailedPlayers.Contains(player)) return;

            jailedPlayers.Add(player);
            CheckForEarlyEnd();
        }

        // Called by JailState.Interact on a successful rescue -- pulls
        // the player back out of jail-tracking so they keep playing this
        // round instead of being finalized Caught at its end.
        [Server]
        public void NotifyPlayerRescued(PlayerInventory player) => jailedPlayers.Remove(player);

        private void ResolvePlayer(PlayerInventory player, RoundResult result)
        {
            if (!roundActive) return;
            if (!resolvedPlayers.Add(player)) return; // already resolved -- ignore a second catch/exit

            OnPlayerResolved?.Invoke(player, result);
            CheckForEarlyEnd();
        }

        // The round ends early the instant every connected player is
        // resolved, currently jailed, *or* ready-at-exit -- a jailed
        // player no longer blocks this the way an old permanent-freeze
        // catch effectively did, since they can still be rescued and
        // keep playing right up until this check (or the timeout) fires;
        // a ready-at-exit player can similarly still climb back out
        // (NotifyPlayerUnready) any time before this actually passes.
        private void CheckForEarlyEnd()
        {
            if (resolvedPlayers.Count + jailedPlayers.Count + readyAtExit.Count < PlayerInventory.AllPlayers.Count) return;

            // Everyone's accounted for -- the whole group gets away
            // together now. Finalize ready players first (folds them
            // into resolvedPlayers) so FinalizeJailedPlayersAsCaught's
            // own resolvedPlayers.Add below can't somehow race an
            // already-in-progress finalize for the same player.
            FinalizeReadyPlayersAsExtracted();
            FinalizeJailedPlayersAsCaught();
            EndRound();
        }

        // Everyone still in readyAtExit the instant the whole group is
        // accounted for gets permanently resolved now -- same
        // RoundResult.RoundComplete path NotifyPlayerReachedExit already
        // uses, just deferred until this moment instead of the instant
        // their own carWaitDuration elapsed.
        private void FinalizeReadyPlayersAsExtracted()
        {
            foreach (PlayerInventory player in new List<PlayerInventory>(readyAtExit))
            {
                readyAtExit.Remove(player);
                if (resolvedPlayers.Add(player)) OnPlayerResolved?.Invoke(player, RoundResult.RoundComplete);
            }
        }

        // Whoever's still jailed the instant the round actually ends
        // (early via CheckForEarlyEnd, or via the Update() timeout) never
        // got rescued in time -- finalize them as Caught now. A copy of
        // jailedPlayers is iterated since ForceRelease below doesn't
        // itself touch the set, but this keeps the loop safe regardless.
        private void FinalizeJailedPlayersAsCaught()
        {
            foreach (PlayerInventory player in new List<PlayerInventory>(jailedPlayers))
            {
                jailedPlayers.Remove(player);
                player.GetComponent<JailState>()?.ForceRelease();
                if (resolvedPlayers.Add(player)) OnPlayerResolved?.Invoke(player, RoundResult.Caught);
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

            // Issue #48/#49: a player who rode out their own exit-car
            // carWaitDuration deliberately stays physically seated/frozen
            // (readyAtExit already got finalized into resolvedPlayers by
            // now, via CheckForEarlyEnd or the timeout above) until the
            // round is actually over for the whole group -- ForceRelease
            // here is what finally lets them go, whether they were ready
            // or still mid-vulnerable-window when the round ended.
            // Harmless no-op for anyone who was never seated.
            foreach (PlayerInventory player in PlayerInventory.AllPlayers)
            {
                player.GetComponent<ExitCarState>()?.ForceRelease();
            }

            OnRoundEnded?.Invoke();
        }
    }
}
