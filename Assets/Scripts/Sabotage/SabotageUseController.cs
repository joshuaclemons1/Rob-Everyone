using System.Collections.Generic;
using Mirror;
using RobEveryone.Inventory;
using RobEveryone.Items;
using RobEveryone.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RobEveryone.Sabotage
{
    // Left mouse button uses the currently selected hotbar item, if it's a
    // sabotage item. Same client-raycasts-locally-for-responsiveness,
    // server-self-validates pattern Interactor.cs already establishes for
    // world interaction -- here applied to PvP instead of pickups, so the
    // server additionally re-checks range itself rather than trusting the
    // client's raycast outright.
    [RequireComponent(typeof(PlayerInventory))]
    public class SabotageUseController : NetworkBehaviour
    {
        [SerializeField] private Transform viewPoint;
        [SerializeField] private LayerMask playerMask = ~0;
        // Server-side leniency beyond ItemDefinition.Range, to absorb
        // normal movement/latency between the client's raycast and the
        // server's own distance check.
        [SerializeField] private float meleeRangeSlack = 0.5f;

        private PlayerInventory inventory;

        // Server-only, per-attacker cooldown tracking, keyed by item name
        // (not by slot index -- a cooldown belongs to the item type, not
        // to whichever physical slot it currently sits in).
        private readonly Dictionary<string, float> nextReadyTime = new();

        private void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
        }

        private ItemDefinition ResolveSelectedItem()
        {
            InventorySlot? slot = inventory.Slots[inventory.SelectedSlot];
            return slot?.Item;
        }

        private void Update()
        {
            if (!isOwned) return;
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;

            ItemDefinition item = ResolveSelectedItem();
            if (item == null) return;

            switch (item.SabotageType)
            {
                case SabotageType.Melee:
                    TryUseMelee(item);
                    break;
                case SabotageType.Thrown:
                    TryUseThrown();
                    break;
            }
        }

        private void TryUseMelee(ItemDefinition item)
        {
            if (viewPoint == null) return;

            if (Physics.Raycast(viewPoint.position, viewPoint.forward, out RaycastHit hit, item.Range, playerMask))
            {
                NetworkIdentity targetIdentity = hit.collider.GetComponentInParent<NetworkIdentity>();
                if (targetIdentity != null && targetIdentity != netIdentity) CmdUseMelee(targetIdentity);
            }
        }

        private void TryUseThrown()
        {
            if (viewPoint == null) return;
            CmdUseThrown(viewPoint.forward);
        }

        [Command]
        private void CmdUseMelee(NetworkIdentity targetIdentity)
        {
            if (targetIdentity == null) return;

            ItemDefinition item = ResolveSelectedItem();
            if (item == null || item.SabotageType != SabotageType.Melee) return;
            if (!TryConsumeCooldown(item)) return;

            PlayerImpactRelay targetRelay = targetIdentity.GetComponent<PlayerImpactRelay>();
            if (targetRelay == null || targetIdentity == netIdentity || targetRelay.IsStunned) return;

            // Adversarial PvP, unlike Interactor's low-stakes world
            // pickups -- re-validate range server-side rather than just
            // trusting the client's raycast result.
            float distance = Vector3.Distance(transform.position, targetIdentity.transform.position);
            if (distance > item.Range + meleeRangeSlack) return;

            Vector3 direction = (targetIdentity.transform.position - transform.position).normalized;
            targetRelay.ServerApplyImpact(direction, item.ImpactForce, item.StunDuration);
        }

        [Command]
        private void CmdUseThrown(Vector3 direction)
        {
            ItemDefinition item = ResolveSelectedItem();
            if (item == null || item.SabotageType != SabotageType.Thrown || item.ThrownProjectilePrefab == null) return;

            int slotIndex = inventory.SelectedSlot;
            if (!inventory.RemoveSlot(slotIndex)) return; // consumed on throw

            Vector3 origin = viewPoint != null ? viewPoint.position : transform.position + Vector3.up * 1.5f;
            GameObject projectile = Instantiate(item.ThrownProjectilePrefab, origin, Quaternion.LookRotation(direction));
            NetworkServer.Spawn(projectile);
            projectile.GetComponent<SabotageProjectile>()?.ServerLaunch(direction.normalized, item, netIdentity);
        }

        private bool TryConsumeCooldown(ItemDefinition item)
        {
            if (item.CooldownSeconds <= 0f) return true;
            if (nextReadyTime.TryGetValue(item.ItemName, out float ready) && Time.time < ready) return false;
            nextReadyTime[item.ItemName] = Time.time + item.CooldownSeconds;
            return true;
        }
    }
}
