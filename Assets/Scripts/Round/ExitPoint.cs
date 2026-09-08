using Mirror;
using RobEveryone.Inventory;
using UnityEngine;

namespace RobEveryone.Round
{
    // Trigger volume at the map's extraction point (the taxi). Needs a
    // Collider with "Is Trigger" checked.
    //
    // Networking (Stage 4): every client has its own local copy of this
    // trigger volume and would otherwise independently fire OnTriggerEnter
    // the instant *their own* local player's collider touches it -- the
    // NetworkServer.active guard means only the server's evaluation (the
    // one everyone actually needs to agree on) ever counts. Reaching the
    // exit resolves *that one player* (RoundManager.NotifyPlayerReachedExit)
    // -- the round itself only actually ends once every connected player
    // is resolved, whether by exiting or by being caught elsewhere (see
    // RoundManager, which replaces the old single-player PartyGate: a
    // caught player can never physically reach this trigger, so "wait for
    // everyone to arrive here" alone would deadlock the round once anyone
    // gets caught).
    [RequireComponent(typeof(Collider))]
    public class ExitPoint : MonoBehaviour
    {
        [SerializeField] private RoundManager roundManager;

        private void OnTriggerEnter(Collider other)
        {
            if (!NetworkServer.active) return;
            if (roundManager == null) return;

            PlayerInventory inventory = other.GetComponentInParent<PlayerInventory>();
            if (inventory == null) return;

            roundManager.NotifyPlayerReachedExit(inventory);
        }
    }
}
