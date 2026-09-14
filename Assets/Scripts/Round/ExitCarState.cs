using Mirror;
using RobEveryone.Core;
using RobEveryone.Input;
using RobEveryone.Inventory;
using RobEveryone.Player;
using UnityEngine;

namespace RobEveryone.Round
{
    // On every Player. ExitPoint.Interact calls EnterCar to seat this
    // player at the exit instead of resolving the round immediately.
    // Two phases while seated (isWaiting covers both):
    //
    //  1. Vulnerable (isReady false, the first carWaitDuration seconds) --
    //     one last risk before actually getting away: getting hit/stunned
    //     by a rival cancels the whole attempt outright (Update's relay
    //     check below), letting the normal PlayerTheftTarget/ragdoll
    //     steal flow take over completely.
    //  2. Ready (isReady true, from carWaitDuration onward) -- safe from
    //     that stun-cancel, registered with RoundManager as accounted
    //     for, but still only *provisionally* resolved: the actual
    //     group-wide extraction only happens once every player is
    //     ready-at-exit or jailed (or the round times out). A ready
    //     player can still change their mind.
    //
    // Pressing E at ANY point while isWaiting is true (locally polled,
    // not routed through Interactor/IInteractable -- there's nothing to
    // aim at, this is a standing self-prompt shown by CrosshairUI) climbs
    // back out and keeps playing instead of waiting it out -- including
    // after becoming ready, per the actual design: carWaitDuration is
    // only the minimum vulnerable window before the group-wide getaway,
    // not a point of no return for the individual player.
    [RequireComponent(typeof(FirstPersonController))]
    [RequireComponent(typeof(PlayerInventory))]
    public class ExitCarState : NetworkBehaviour
    {
        [SyncVar] private bool isWaiting;
        public bool IsWaiting => isWaiting;

        // True from the moment carWaitDuration elapses until this player
        // either climbs back out (CmdExitCar) or the round ends. isWaiting
        // itself stays true through both phases -- this only exists to
        // gate the vulnerable-window-only checks in Update (stun-cancel,
        // the countdown itself) and to tell RoundManager/CrosshairUI
        // which phase this player is actually in.
        [SyncVar] private bool isReady;
        public bool IsReady => isReady;

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

            // Once ready, the vulnerable-window checks below (stun-cancel,
            // the countdown) stop applying -- RoundManager alone decides
            // when the group as a whole actually resolves from here.
            if (!isServer || !isWaiting || isReady) return;

            // Getting stunned while still vulnerable cancels the whole
            // attempt outright -- the normal ragdoll/steal flow takes
            // over completely instead of this player awkwardly staying
            // "mid-extraction" once they recover.
            if (relay != null && relay.IsStunned)
            {
                ForceRelease();
                return;
            }

            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f) BecomeReady();
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

            // Issue #45 fix: ClaimSeat hands back a real seatPoints[]
            // Transform's pose for however many physical seats are
            // configured on the car model, a computed non-overlapping
            // offset beyond that -- see its own comment on ExitPoint for
            // why an unclaimed shared seat used to just silently shove a
            // second rider out instead.
            exit.ClaimSeat(this, out Vector3 seatPosition, out Quaternion seatRotation);
            GameFlowManager.Instance?.TeleportPlayerTo(transform, seatPosition, seatRotation);
        }

        // The vulnerable window's own timer ran out -- safe now, but only
        // provisionally: RoundManager tracks this as "accounted for," and
        // only actually resolves the whole group once everyone is
        // ready-at-exit or jailed (or the round itself times out).
        [Server]
        private void BecomeReady()
        {
            isReady = true;
            currentExit?.NotifyPlayerReady(GetComponent<PlayerInventory>());
        }

        // Works whether still in the vulnerable window or already ready --
        // isWaiting alone gates this, and stays true through both phases.
        [Command]
        private void CmdExitCar()
        {
            if (!isWaiting) return;

            bool wasReady = isReady;
            isWaiting = false;
            isReady = false;
            fpc.ExitCarFrozen = false;

            if (currentExit != null && currentExit.StandPoint != null)
            {
                GameFlowManager.Instance?.TeleportPlayerTo(transform, currentExit.StandPoint);
            }
            // Only relevant if they'd already registered as ready --
            // NotifyPlayerUnready is a harmless no-op otherwise.
            if (wasReady) currentExit?.NotifyPlayerUnready(GetComponent<PlayerInventory>());
            currentExit?.ReleaseSeat(this);
            currentExit = null;
        }

        // Two callers: getting stunned while still vulnerable (Update
        // above, cancels the whole attempt outright), and RoundManager.
        // EndRound calling this on every player once the round is
        // actually over -- the normal way a player who was still seated
        // (ready or not) here gets their freeze/seat cleared up.
        [Server]
        public void ForceRelease()
        {
            isWaiting = false;
            isReady = false;
            currentExit?.NotifyPlayerUnready(GetComponent<PlayerInventory>());
            currentExit?.ReleaseSeat(this);
            currentExit = null;
            fpc.ExitCarFrozen = false;
        }
    }
}
