using Mirror;
using RobEveryone.Interaction;
using RobEveryone.Inventory;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RobEveryone.Player
{
    // Owner input for picking up, carrying and throwing a downed rival.
    //  - E grabs a ragdolled player you're looking at -- but only when
    //    Interactor has no target of its own, so a steal (or any world
    //    interaction) always wins the keypress first.
    //  - While carrying: G sets them down gently; hold LMB to charge a
    //    throw, release to launch along your view. E stays free for
    //    interacting with the world.
    //  - You drop the body instantly if you get ragdolled, and you can't
    //    bhop while carrying (FirstPersonController.CarryingSomething).
    [RequireComponent(typeof(Interactor))]
    [RequireComponent(typeof(FirstPersonController))]
    [RequireComponent(typeof(PlayerRagdoll))]
    [RequireComponent(typeof(PlayerInventory))]
    public class CarryController : NetworkBehaviour
    {
        [SerializeField] private Transform viewPoint;
        [SerializeField] private Transform carryAnchor;   // where a carried body sits (over the shoulder / in front)
        [SerializeField] private float grabRange = 2.5f;
        [SerializeField] private float minThrowForce = 8f;
        [SerializeField] private float maxThrowForce = 45f;
        [SerializeField] private float throwChargeTime = 1.2f;
        [SerializeField] private Key grabKey = Key.E;
        [SerializeField] private Key setDownKey = Key.G;
        // See Interactor.ragdollAssistMaxAngle's own comment -- collider-
        // free on purpose, not a raycast/SphereCast radius.
        [SerializeField] private float grabAssistMaxAngle = 30f;

        [SyncVar] private NetworkIdentity carried; // the victim, server-set

        public Transform CarryAnchor => carryAnchor;
        public bool IsCarrying => carried != null;

        private Interactor interactor;
        private FirstPersonController fpc;
        private PlayerRagdoll ownRagdoll;
        private PlayerInventory ownInventory;
        private PlayerAnimationDriver animDriver;
        private float chargeStart = -1f;

        private void Awake()
        {
            interactor = GetComponent<Interactor>();
            fpc = GetComponent<FirstPersonController>();
            ownRagdoll = GetComponent<PlayerRagdoll>();
            ownInventory = GetComponent<PlayerInventory>();
            animDriver = GetComponent<PlayerAnimationDriver>();
        }

        private void Update()
        {
            // Server: the carrier ragdolling drops the body immediately.
            if (isServer && carried != null && ownRagdoll.IsRagdolling)
            {
                ServerDrop(thrown: false);
            }

            if (isOwned) OwnerUpdate();
        }

        private void OwnerUpdate()
        {
            fpc.CarryingSomething = carried != null;

            if (fpc.IsFrozen || ownRagdoll.IsRagdolling) return;
            if (RobEveryone.UI.InventoryScreenUI.MenuOpen) return; // the Tab/steal screen owns input

            if (carried == null)
            {
                if (Keyboard.current != null && Keyboard.current[grabKey].wasPressedThisFrame
                    && interactor.CurrentTarget == null
                    && TryFindCarryable(out NetworkIdentity target))
                {
                    if (animDriver != null) animDriver.PlayAction(PlayerActionAnim.PickUp);
                    CmdGrab(target);
                }
                return;
            }

            // Carrying.
            if (Keyboard.current != null && Keyboard.current[setDownKey].wasPressedThisFrame)
            {
                chargeStart = -1f;
                CmdDrop(false, Vector3.zero, 0f);
                return;
            }

            if (Mouse.current == null) return;
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                chargeStart = Time.time;
            }
            else if (Mouse.current.leftButton.wasReleasedThisFrame && chargeStart >= 0f)
            {
                float t = Mathf.Clamp01((Time.time - chargeStart) / Mathf.Max(0.01f, throwChargeTime));
                float force = Mathf.Lerp(minThrowForce, maxThrowForce, t);
                chargeStart = -1f;
                CmdDrop(true, viewPoint != null ? viewPoint.forward : transform.forward, force);
            }
        }

        // 0..1 while LMB is held mid-carry, for a HUD charge meter.
        public float ThrowCharge01 =>
            chargeStart < 0f ? 0f : Mathf.Clamp01((Time.time - chargeStart) / Mathf.Max(0.01f, throwChargeTime));

        // Collider-free, same reasoning as Interactor.FindRagdolledPlayerNearby
        // -- picks the nearest grabbable ragdoll within grabRange whose
        // direction from the viewpoint is within grabAssistMaxAngle of
        // where the camera's pointed, rather than requiring a precise hit
        // against a ragdolling player's small individual limb colliders
        // (their CharacterController is disabled for the whole stun).
        private bool TryFindCarryable(out NetworkIdentity target)
        {
            target = null;
            Transform origin = viewPoint != null ? viewPoint : transform;

            Carryable best = null;
            float bestAngle = grabAssistMaxAngle;

            foreach (Carryable candidate in FindObjectsByType<Carryable>(FindObjectsSortMode.None))
            {
                if (candidate.gameObject == gameObject || !candidate.CanBeGrabbed) continue;

                Vector3 toTarget = candidate.transform.position - origin.position;
                if (toTarget.magnitude > grabRange) continue;

                float angle = Vector3.Angle(origin.forward, toTarget);
                if (angle >= bestAngle) continue;

                bestAngle = angle;
                best = candidate;
            }

            if (best == null) return false;

            target = best.GetComponent<NetworkIdentity>();
            return target != null;
        }

        // Called by Interactor once a hold-to-grab disambiguation succeeds
        // (the target is both a valid steal target and a valid carry
        // target right now -- Interactor itself arbitrates tap-vs-hold
        // for that specific overlap and calls this directly, bypassing
        // the plain tap-grab branch above which only fires when
        // Interactor has no target of its own at all).
        public void TryGrabFromHold(NetworkIdentity target)
        {
            if (fpc.IsFrozen || ownRagdoll.IsRagdolling) return;
            if (carried != null || target == null) return;

            if (animDriver != null) animDriver.PlayAction(PlayerActionAnim.PickUp);
            CmdGrab(target);
        }

        [Command]
        private void CmdGrab(NetworkIdentity target)
        {
            if (carried != null || target == null || target == netIdentity) return;

            Carryable c = target.GetComponent<Carryable>();
            if (c == null || !c.CanBeGrabbed) return;
            if (Vector3.Distance(transform.position, target.transform.position) > grabRange + 1f) return;

            c.ServerAttach(netIdentity);
            carried = target;
            if (ownInventory != null) ownInventory.SetCarryHold(true); // hands full -- no hotbar item
        }

        [Command]
        private void CmdDrop(bool thrown, Vector3 direction, float force) => ServerDrop(thrown, direction, force);

        [Server]
        public void ServerDrop(bool thrown, Vector3 direction = default, float force = 0f)
        {
            if (carried == null) return;

            NetworkIdentity victim = carried;
            carried = null;
            if (ownInventory != null) ownInventory.SetCarryHold(false); // restore the pre-carry selection

            // thrown/direction/force ride along on the same detach call --
            // see Carryable.ServerDetach's own comment for why this can't
            // be a second, separate RPC.
            Carryable c = victim.GetComponent<Carryable>();
            if (c != null) c.ServerDetach(thrown && force > 0f, direction.normalized, force);
        }

        // RobEveryoneNetworkManager.OnServerDisconnect -- release before
        // the carrier's object is torn down.
        [Server]
        public void ServerReleaseOnDisconnect() => ServerDrop(false);
    }
}
