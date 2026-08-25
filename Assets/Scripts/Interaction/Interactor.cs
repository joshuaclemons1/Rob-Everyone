using UnityEngine;
using UnityEngine.InputSystem;

namespace RobEveryone.Interaction
{
    // Casts a ray from the given view point every frame and lets the player
    // trigger whatever IInteractable is currently being looked at.
    [DisallowMultipleComponent]
    public class Interactor : MonoBehaviour
    {
        [SerializeField] private Transform viewPoint;
        [SerializeField] private float interactRange = 3f;
        [SerializeField] private LayerMask interactableMask = ~0;

        private IInteractable currentTarget;

        public IInteractable CurrentTarget => currentTarget;

        private void Update()
        {
            currentTarget = FindTarget();

            if (currentTarget != null && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                currentTarget.Interact(gameObject);
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
    }
}
