using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

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

        private IInteractable currentTarget;

        public IInteractable CurrentTarget => currentTarget;

        private void Update()
        {
            if (!isOwned) return;

            currentTarget = FindTarget();

            if (currentTarget != null && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                NetworkIdentity targetIdentity = (currentTarget as Component)?.GetComponentInParent<NetworkIdentity>();
                if (targetIdentity != null) CmdInteract(targetIdentity);
            }
        }

        private IInteractable FindTarget()
        {
            if (viewPoint == null) return null;

            if (Physics.Raycast(viewPoint.position, viewPoint.forward, out RaycastHit hit, interactRange, interactableMask))
            {
                return hit.collider.GetComponentInParent<IInteractable>();
            }

            return null;
        }

        [Command]
        private void CmdInteract(NetworkIdentity targetIdentity)
        {
            if (targetIdentity == null) return;

            IInteractable interactable = targetIdentity.GetComponent<IInteractable>();
            interactable?.Interact(gameObject);
        }
    }
}
