using RobEveryone.Inventory;
using UnityEngine;

namespace RobEveryone.Round
{
    // Trigger volume at the map's extraction point (the taxi). Needs a
    // Collider with "Is Trigger" checked. Routes through a PartyGate
    // rather than ending the round the instant one player arrives --
    // solo play resolves this immediately (see PartyGate), but the round
    // only actually ends once every present player has reached the exit.
    [RequireComponent(typeof(Collider))]
    public class ExitPoint : MonoBehaviour
    {
        [SerializeField] private RoundManager roundManager;
        [SerializeField] private PartyGate partyGate;

        private void OnEnable()
        {
            if (partyGate != null) partyGate.OnAllArrived += HandleAllArrived;
        }

        private void OnDisable()
        {
            if (partyGate != null) partyGate.OnAllArrived -= HandleAllArrived;
        }

        private void OnTriggerEnter(Collider other)
        {
            PlayerInventory inventory = other.GetComponentInParent<PlayerInventory>();
            if (inventory == null) return;

            if (partyGate != null)
            {
                partyGate.NotifyArrived(inventory);
            }
            else if (roundManager != null)
            {
                // Fallback if no gate is wired -- today's instant behavior.
                roundManager.NotifyExitReached();
            }
        }

        private void HandleAllArrived()
        {
            if (roundManager != null) roundManager.NotifyExitReached();
        }
    }
}
