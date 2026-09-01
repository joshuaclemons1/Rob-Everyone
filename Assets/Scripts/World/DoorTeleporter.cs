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
