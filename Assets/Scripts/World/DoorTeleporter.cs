using Mirror;
using RobEveryone.Inventory;
using UnityEngine;

namespace RobEveryone.World
{
    // Sits on a trigger volume at a doorway. When the player walks through
    // it, teleports them to `destination` instead of relying on the
    // building's mesh having a real modeled opening -- lets a building's
    // original, unmodified collider (walls, bushes, fences, everything)
    // stay exactly as imported, with zero custom collision shaping needed.
    // Place one of these at the outside threshold pointing inward, and a
    // matching one just inside pointing back out, for a working round trip.
    //
    // Networking (Stage 4): every client has its own local copy of every
    // player, so without the isOwned check below, a bystander's client
    // would also locally fire this trigger (and teleport its own,
    // non-authoritative copy of someone else's player) the instant that
    // player's synced position crosses the threshold -- harmless in that
    // NetworkTransform corrects it back right away, but wasted work and a
    // visible flicker. Only the owning client's own copy should actually
    // move itself, same reasoning as FirstPersonController's own guard.
    [RequireComponent(typeof(Collider))]
    public class DoorTeleporter : MonoBehaviour
    {
        [SerializeField] private Transform destination;
        [SerializeField] private float cooldown = 0.5f;

        private float lastTeleportTime = -999f;

        private void OnTriggerEnter(Collider other)
        {
            if (destination == null) return;
            if (Time.time - lastTeleportTime < cooldown) return;
            if (other.GetComponentInParent<PlayerInventory>() == null) return;

            NetworkIdentity identity = other.GetComponentInParent<NetworkIdentity>();
            if (identity != null && !identity.isOwned) return;

            CharacterController controller = other.GetComponentInParent<CharacterController>();
            if (controller == null) return;

            // Disable/re-enable around the position change so the
            // CharacterController doesn't try to resolve the teleport as a
            // collision (which would clip it against the solid wall).
            controller.enabled = false;
            controller.transform.position = destination.position;
            controller.enabled = true;

            lastTeleportTime = Time.time;
        }
    }
}
