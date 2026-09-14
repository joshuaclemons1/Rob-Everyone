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
            if (isWaiting || exit == null) return;

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

            // Issue #45 fix: ClaimSeatPosition hands back the real seat
            // for the first occupant, a computed non-overlapping offset
            // for every occupant after that -- see its own comment on
            // ExitPoint for why that used to just silently shove a
            // second rider out instead.
            Transform seat = exit.SeatPoint != null ? exit.SeatPoint : exit.transform;
            Vector3 seatPosition = exit.ClaimSeatPosition(this);
            GameFlowManager.Instance?.TeleportPlayerTo(transform, seatPosition, seat.rotation);
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

        // Nobody chose to climb back out within the wait window -- they
        // actually get away now.
        [Server]
        private void FinalizeExit()
        {
            isWaiting = false;
            fpc.ExitCarFrozen = false;

            ExitPoint exit = currentExit;
            currentExit = null;
            exit?.ReleaseSeat(this);
            exit?.NotifyPlayerExtracted(GetComponent<PlayerInventory>());
        }

        // Safety net -- a round ending some other way (timeout, everyone
        // else resolved) while this player is still mid-wait, or getting
        // stunned while seated (Update above).
        [Server]
        public void ForceRelease()
        {
            isWaiting = false;
            currentExit?.ReleaseSeat(this);
            currentExit = null;
            fpc.ExitCarFrozen = false;
        }
    }
}
