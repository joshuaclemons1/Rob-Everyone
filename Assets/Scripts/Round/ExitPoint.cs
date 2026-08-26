using RobEveryone.Inventory;
using UnityEngine;

namespace RobEveryone.Round
{
    // Trigger volume at the map's extraction point. Needs a Collider with
    // "Is Trigger" checked. Ends the round the moment the player walks in.
    [RequireComponent(typeof(Collider))]
    public class ExitPoint : MonoBehaviour
    {
        [SerializeField] private RoundManager roundManager;

        private void OnTriggerEnter(Collider other)
        {
            if (roundManager == null) return;
            if (other.GetComponentInParent<PlayerInventory>() == null) return;

            roundManager.NotifyExitReached();
        }
    }
}
