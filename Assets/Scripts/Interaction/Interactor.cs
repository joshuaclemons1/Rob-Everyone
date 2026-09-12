using Mirror;
using RobEveryone.Input;
using RobEveryone.Items;
using RobEveryone.Player;
using RobEveryone.Round;
using RobEveryone.Shop;
using UnityEngine;

namespace RobEveryone.Interaction
{
    // Casts a ray from the given view point every frame and lets the player
    // trigger whatever IInteractable is currently being looked at.
    //
    // Networking (Stage 4): the raycast/prompt stays fully local (only the
    // owner needs to know what they're looking at), but the actual
    // Interact() call is server-authoritative now -- whatever an
    // interactable does (take an item, sell loot, ready up) mutates
    // networked state, so it has to run on the server. CmdInteract sends
    // just the target's NetworkIdentity across; the server resolves
    // IInteractable from it and calls Interact() there, never on a client.
    [DisallowMultipleComponent]
    public class Interactor : NetworkBehaviour
    {
        [SerializeField] private Transform viewPoint;
        [SerializeField] private float interactRange = 3f;
        [SerializeField] private LayerMask interactableMask = ~0;
        // A ragdolled, PvP-stunned rival is simultaneously a valid steal
        // target (tap) and a valid carry target (hold) -- an instant tap
        // response can't tell which one the player meant, so this one
        // ambiguous case gets a disambiguation window. Every other
        // interactable (loot, Sell Station, Ready Spot, or a steal target
        // that's no longer actively ragdolling) keeps its exact original
        // zero-latency tap response below.
        [SerializeField] private float carryHoldDuration = 0.5f;
        // A ragdolled player's CharacterController is disabled for the
        // duration of the stun (see PlayerRagdoll.ImpactSequence), so the
        // only raycastable colliders left are their individual small
        // ragdoll limb bones -- any collider-based cast (even a wide
        // SphereCast) is still unreasonably precise to aim. Below,
        // FindRagdolledPlayerNearby ignores colliders entirely for this
        // one case and just checks "is there a grabbable body roughly in
        // my general direction and within range" -- this angle is how
        // generous that cone is, not tied to any collider size.
        [SerializeField] private float ragdollAssistMaxAngle = 30f;
        // Same idea as ragdollAssistMaxAngle, for a different collider
        // problem: a jailed player's cell (bars/walls) sits physically
        // between the raycast and them, so the precise raycast above can
        // never resolve them at all, not even imprecisely. See
        // FindJailedPlayerNearby below.
        [SerializeField] private float jailAssistMaxAngle = 30f;

        private IInteractable currentTarget;
        private CarryController carry;
        private PlayerAnimationDriver animDriver;

        private float eHoldStart = -1f;
        private bool grabbedThisHold;

        public IInteractable CurrentTarget => currentTarget;

        private void Awake()
        {
            carry = GetComponent<CarryController>();
            animDriver = GetComponent<PlayerAnimationDriver>();
        }

        private void Update()
        {
            if (!isOwned) return;
            if (RobEveryone.UI.InventoryScreenUI.MenuOpen) { currentTarget = null; eHoldStart = -1f; return; }

            currentTarget = FindTarget();

            bool pressedThisFrame = InputManager.Gameplay.Interact.WasPressedThisFrame();
            bool releasedThisFrame = InputManager.Gameplay.Interact.WasReleasedThisFrame();

            Carryable carryable = carry != null ? (currentTarget as Component)?.GetComponentInParent<Carryable>() : null;
            bool ambiguous = currentTarget != null && carryable != null && carryable.CanBeGrabbed;

            if (!ambiguous)
            {
                eHoldStart = -1f;
                if (currentTarget != null && pressedThisFrame) FireInteract();
                return;
            }

            if (pressedThisFrame)
            {
                eHoldStart = Time.time;
                grabbedThisHold = false;
            }

            if (InputManager.Gameplay.Interact.IsPressed() && !grabbedThisHold && eHoldStart >= 0f
                && Time.time - eHoldStart >= carryHoldDuration)
            {
                grabbedThisHold = true;
                NetworkIdentity targetIdentity = (currentTarget as Component)?.GetComponentInParent<NetworkIdentity>();
                if (targetIdentity != null) carry.TryGrabFromHold(targetIdentity);
            }

            if (releasedThisFrame)
            {
                if (!grabbedThisHold && eHoldStart >= 0f) FireInteract(); // quick tap -- steal, not carry
                eHoldStart = -1f;
            }
        }

        private void FireInteract()
        {
            NetworkIdentity targetIdentity = (currentTarget as Component)?.GetComponentInParent<NetworkIdentity>();
            if (targetIdentity == null) return;

            // A reach-down grab reads right for loot; a Sell
            // Station / Ready Spot is just a touch, no wind-up.
            if (currentTarget is PickupItem && animDriver != null)
                animDriver.PlayAction(PlayerActionAnim.PickUp);
            CmdInteract(targetIdentity);
        }

        private IInteractable FindTarget()
        {
            if (viewPoint == null) return null;

            if (Physics.Raycast(viewPoint.position, viewPoint.forward, out RaycastHit hit, interactRange, interactableMask))
            {
                IInteractable target = hit.collider.GetComponentInParent<IInteractable>();
                if (target != null && target.CanInteract)
                {
                    // Hands full while carrying a body -- can't pick loot
                    // up or sell it (no hotbar item to hand over). Ready
                    // Spot is still fine.
                    if (carry != null && carry.IsCarrying && (target is PickupItem || target is SellStation)) return null;
                    return target;
                }
            }

            IInteractable ragdollTarget = FindRagdolledPlayerNearby();
            if (ragdollTarget != null) return ragdollTarget;

            return FindJailedPlayerNearby();
        }

        // Fallback only, and deliberately collider-free -- picks the
        // nearest currently-grabbable ragdoll within range whose
        // direction from the viewpoint is within ragdollAssistMaxAngle of
        // where the camera's actually pointed, full stop. This can never
        // make loot/doors/Sell Station easier to aim at (those still only
        // ever resolve through the precise raycast above), since it only
        // ever returns something that's also a valid carry target.
        //
        // FindObjectsByType, not PlayerInventory.AllPlayers -- that list
        // is server-only (populated from OnStartServer/OnStopServer), so
        // reading it here (this runs on the owner's own client, which
        // isn't the server for a remote connection) would silently come
        // back empty for anyone but the host.
        private IInteractable FindRagdolledPlayerNearby()
        {
            Carryable best = null;
            float bestAngle = ragdollAssistMaxAngle;

            foreach (Carryable candidate in FindObjectsByType<Carryable>(FindObjectsSortMode.None))
            {
                if (candidate.gameObject == gameObject || !candidate.CanBeGrabbed) continue;

                Vector3 toTarget = candidate.transform.position - viewPoint.position;
                if (toTarget.magnitude > interactRange) continue;

                float angle = Vector3.Angle(viewPoint.forward, toTarget);
                if (angle >= bestAngle) continue;

                bestAngle = angle;
                best = candidate;
            }

            if (best == null) return null;

            IInteractable target = best.GetComponent<IInteractable>();
            if (target == null || !target.CanInteract) return null;

            if (carry != null && carry.IsCarrying) return null; // hands full regardless
            return target;
        }

        // Same collider-free shape as FindRagdolledPlayerNearby above --
        // a jail cell's own bars/walls block the precise raycast from
        // ever reaching whoever's inside, so this checks every currently-
        // jailed player's real distance/angle from the viewpoint instead
        // of going through physics at all.
        private IInteractable FindJailedPlayerNearby()
        {
            JailState best = null;
            float bestAngle = jailAssistMaxAngle;

            foreach (JailState candidate in FindObjectsByType<JailState>(FindObjectsSortMode.None))
            {
                if (candidate.gameObject == gameObject || !candidate.IsJailed) continue;

                Vector3 toTarget = candidate.transform.position - viewPoint.position;
                if (toTarget.magnitude > interactRange) continue;

                float angle = Vector3.Angle(viewPoint.forward, toTarget);
                if (angle >= bestAngle) continue;

                bestAngle = angle;
                best = candidate;
            }

            return best; // JailState.CanInteract already mirrors IsJailed
        }

        [Command]
        private void CmdInteract(NetworkIdentity targetIdentity)
        {
            if (targetIdentity == null) return;

            // A player's own NetworkIdentity can now carry more than one
            // IInteractable (PlayerTheftTarget for robbing a ragdolled
            // rival, JailState for a bail) -- plain GetComponent<T>
            // would arbitrarily return whichever one happens to be
            // earliest in the component list regardless of which is
            // actually valid right now (confirmed bug: pressing E on a
            // jailed player silently ran PlayerTheftTarget.Interact
            // instead, which no-ops since it's not also ragdoll-stunned).
            // They're mutually exclusive by design (see JailState's own
            // comment), so the one whose CanInteract is true is the same
            // one this client's own FindTarget would have shown a prompt
            // for.
            foreach (IInteractable candidate in targetIdentity.GetComponents<IInteractable>())
            {
                if (!candidate.CanInteract) continue;
                candidate.Interact(gameObject);
                return;
            }
        }
    }
}
