using Mirror;
using RobEveryone.Core;
using RobEveryone.Input;
using RobEveryone.Inventory;
using RobEveryone.Player;
using UnityEngine;

namespace RobEveryone.Round
{
    // On every Player. ExitPoint.Interact calls EnterCar to seat this
    // player at the exit instead of resolving the round immediately --
    // they sit there, unable to act, for carWaitDuration, one last risk
    // before actually getting away: getting hit/stunned by a rival while
    // seated works exactly like any other stun (see the Update() relay
    // check below), letting the normal PlayerTheftTarget/ragdoll steal
    // flow take over completely -- being seated removes any immunity to
    // that, it doesn't grant a *separate* always-lootable state on its
    // own. Pressing E again (locally polled, not routed through
    // Interactor/IInteractable -- there's nothing to aim at, this is a
    // standing self-prompt shown by CrosshairUI) climbs back out and
    // keeps playing instead of waiting it out.
    [RequireComponent(typeof(FirstPersonController))]
    [RequireComponent(typeof(PlayerInventory))]
    public class ExitCarState : NetworkBehaviour
    {
        [SyncVar] private bool isWaiting;
        public bool IsWaiting => isWaiting;

        // Set once FinalizeExit resolves this player and never cleared
        // until ForceRelease (round end). Guards EnterCar below --
        // staying frozen/seated (rather than immediately regaining look
        // control) after extracting means this player is now much more
        // likely to still be facing the exit point than before, and
        // Interactor doesn't check ExitCarFrozen at all, so a stray
        // E-press could otherwise re-trigger EnterCar and incorrectly
        // flip isWaiting back on (making an already-safely-resolved
        // player vulnerable to the stun-cancels-extraction check again).
        //
        // SyncVar, not a plain field -- CrosshairUI reads this on the
        // owning client to show the right prompt (see its own fix),
        // which needs the server's own mutation actually replicated down
        // rather than a remote client's local copy silently staying
        // false forever.
        [SyncVar] private bool hasExtracted;
        public bool HasExtracted => hasExtracted;

        private FirstPersonController fpc;
        private PlayerImpactRelay relay;
        private ExitPoint currentExit;
        private float waitTimer;

        private void Awake()
        {
            fpc = GetComponent<FirstPersonController>();
            relay = GetComponent<PlayerImpactRelay>();
        }

        private void Update()
        {
            if (isOwned && isWaiting && InputManager.Gameplay.Interact.WasPressedThisFrame())
            {
                CmdExitCar();
            }

            if (!isServer || !isWaiting) return;

            // Getting stunned while waiting cancels the extraction attempt
            // outright -- the normal ragdoll/steal flow takes over
            // completely instead of this player awkwardly staying
            // "mid-extraction" once they recover.
            if (relay != null && relay.IsStunned)
            {
                ForceRelease();
                return;
            }

            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f) FinalizeExit();
        }

        [Server]
        public void EnterCar(ExitPoint exit)
        {
            if (isWaiting || hasExtracted || exit == null) return;

            isWaiting = true;
            currentExit = exit;
            waitTimer = exit.CarWaitDuration;
            // Local-only movement lock (FirstPersonController.
            // ExitCarFrozen), deliberately NOT the synced IsFrozen flag --
            // PlayerRagdoll.EndRagdoll refuses to hand control back while
            // IsFrozen is true (originally written for the old permanent
            // jail freeze), which would leave a player who gets hit by a
            // car while seated here stuck unable to move forever
            // afterward, including once carried into the Lobby. Using a
            // separate flag keeps this from ever touching that guard, so
            // a stun while seated recovers exactly like a normal stun.
            fpc.ExitCarFrozen = true;

            // Issue #45 fix: ClaimSeat hands back a real seatPoints[]
            // Transform's pose for however many physical seats are
            // configured on the car model, a computed non-overlapping
            // offset beyond that -- see its own comment on ExitPoint for
            // why an unclaimed shared seat used to just silently shove a
            // second rider out instead.
            exit.ClaimSeat(this, out Vector3 seatPosition, out Quaternion seatRotation);
            GameFlowManager.Instance?.TeleportPlayerTo(transform, seatPosition, seatRotation);
        }

        [Command]
        private void CmdExitCar()
        {
            if (!isWaiting) return;

            isWaiting = false;
            fpc.ExitCarFrozen = false;

            if (currentExit != null && currentExit.StandPoint != null)
            {
                GameFlowManager.Instance?.TeleportPlayerTo(transform, currentExit.StandPoint);
            }
            currentExit?.ReleaseSeat(this);
            currentExit = null;
        }

        // Nobody chose to climb back out within the wait window -- they're
        // safely committed now (isWaiting false means Update's E-press and
        // stun-cancels-extraction checks above both stop applying to them).
        //
        // Deliberately does NOT unfreeze or release the seat here --
        // issue #48 fix: this used to hand control straight back the
        // instant *this player's own* carWaitDuration ran out, which
        // visibly yanked them out of the car while other players were
        // still mid-round, rather than the whole group getting away
        // together. NotifyPlayerExtracted still resolves them with
        // RoundManager immediately though, so RoundManager.
        // CheckForEarlyEnd still correctly ends the round early the
        // moment every other player is also resolved/jailed -- staying
        // physically seated is a presentation-only change from here on.
        // RoundManager.EndRound() releases every still-seated player
        // (ForceRelease, below) once the round is actually over for
        // everyone, whether that's via CheckForEarlyEnd or the round
        // timing out.
        [Server]
        private void FinalizeExit()
        {
            isWaiting = false;
            hasExtracted = true;
            currentExit?.NotifyPlayerExtracted(GetComponent<PlayerInventory>());
        }

        // Two callers: getting stunned while still mid-wait (Update above,
        // cancels the extraction attempt outright), and RoundManager.
        // EndRound calling this on every player once the round is
        // actually over -- the normal way an already-extracted (isWaiting
        // already false, FinalizeExit already ran) player still seated
        // here gets their freeze/seat cleared up, now that staying seated
        // until the whole group is done is the deliberate behavior. Safe
        // to call on a player who was never seated at all (currentExit
        // stays null the whole time, every line below is a no-op).
        [Server]
        public void ForceRelease()
        {
            isWaiting = false;
            // Reset here, not just on stun-cancel -- this same Player
            // GameObject persists (DontDestroyOnLoad) into next round's
            // fresh RoundManager/ExitPoint, so leaving this true forever
            // would permanently lock EnterCar out for every future round
            // too, not just the one that just ended.
            hasExtracted = false;
            currentExit?.ReleaseSeat(this);
            currentExit = null;
            fpc.ExitCarFrozen = false;
        }
    }
}
