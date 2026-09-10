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
            if (Mouse.current == null) return;

            ItemDefinition item = ResolveSelectedItem();
            if (item == null) return;

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                // HasFlag, not a plain == comparison -- SabotageType is
                // [Flags] so a dual-mode item (Hammer: Melee | Thrown)
                // can match more than one case; an exact-value switch
                // would match neither and silently do nothing. Left-click
                // keeps its exact original priority for every existing
                // item (Taser/Bat = Melee, Tranq Gun = Ranged, Dynamite =
                // Thrown) -- Melee wins first for a dual-mode item too,
                // matching a Hammer's "primary" action being its swing.
                if (item.SabotageType.HasFlag(SabotageType.Melee)) TryUseMelee(item);
                else if (item.SabotageType.HasFlag(SabotageType.Ranged)) TryUseRanged(item);
                else if (item.SabotageType.HasFlag(SabotageType.Thrown)) TryUseThrown();
            }
            else if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                // The explicit "throw" action, only meaningful for a
                // dual-mode item (Hammer today) -- a Thrown-only item
                // like Dynamite already throws on left-click, so this
                // only adds new behavior, never changes existing.
                if (item.SabotageType.HasFlag(SabotageType.Melee) && item.SabotageType.HasFlag(SabotageType.Thrown))
                {
                    TryUseThrown();
                }
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

        private void TryUseRanged(ItemDefinition item)
        {
            if (viewPoint == null) return;

            if (Physics.Raycast(viewPoint.position, viewPoint.forward, out RaycastHit hit, item.Range, playerMask))
            {
                NetworkIdentity targetIdentity = hit.collider.GetComponentInParent<NetworkIdentity>();
                if (targetIdentity != null && targetIdentity != netIdentity) CmdUseRanged(targetIdentity);
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
            if (item == null || !item.SabotageType.HasFlag(SabotageType.Melee)) return;
            if (!TryConsumeCooldown(item)) return;

            PlayerImpactRelay targetRelay = targetIdentity.GetComponent<PlayerImpactRelay>();
            if (targetRelay == null || targetIdentity == netIdentity || targetRelay.IsStunned) return;

            // Adversarial PvP, unlike Interactor's low-stakes world
            // pickups -- re-validate range server-side rather than just
            // trusting the client's raycast result.
            float distance = Vector3.Distance(transform.position, targetIdentity.transform.position);
            if (distance > item.Range + meleeRangeSlack) return;

            Vector3 direction = (targetIdentity.transform.position - transform.position).normalized;
            targetRelay.ServerApplyPvpImpact(direction, item.ImpactForce, item.StunDuration);
            inventory.DecrementUses(inventory.SelectedSlot);
        }

        // Ranged is a hitscan, not a travelling projectile -- unlike
        // melee's short reach, a long-range shot genuinely needs a real
        // line-of-sight check server-side (not just a distance check), or
        // a player could "shoot" through a wall by aiming at a target
        // whose position the client happens to know.
        [Command]
        private void CmdUseRanged(NetworkIdentity targetIdentity)
        {
            if (targetIdentity == null) return;

            ItemDefinition item = ResolveSelectedItem();
            if (item == null || !item.SabotageType.HasFlag(SabotageType.Ranged)) return;
            if (!TryConsumeCooldown(item)) return;

            PlayerImpactRelay targetRelay = targetIdentity.GetComponent<PlayerImpactRelay>();
            if (targetRelay == null || targetIdentity == netIdentity || targetRelay.IsStunned) return;

            Vector3 origin = transform.position + Vector3.up * 1.5f;
            Vector3 toTarget = targetIdentity.transform.position - origin;
            if (toTarget.magnitude > item.Range) return;
            if (!Physics.Raycast(origin, toTarget.normalized, out RaycastHit hit, toTarget.magnitude, playerMask)) return;
            if (hit.collider.GetComponentInParent<NetworkIdentity>() != targetIdentity) return; // something else was in the way

            targetRelay.ServerApplyPvpImpact(toTarget.normalized, item.ImpactForce, item.StunDuration);
            inventory.DecrementUses(inventory.SelectedSlot);
        }

        [Command]
        private void CmdUseThrown(Vector3 direction)
        {
            ItemDefinition item = ResolveSelectedItem();
            if (item == null || !item.SabotageType.HasFlag(SabotageType.Thrown) || item.ThrownProjectilePrefab == null) return;

            int slotIndex = inventory.SelectedSlot;
            // Read before RemoveSlot clears it -- a retrievable throw
            // (Hammer) needs to carry its current durability onto the
            // flying projectile so a landed pickup doesn't reset to full.
            int remainingUses = inventory.Slots[slotIndex]?.RemainingUses ?? 0;
            if (!inventory.RemoveSlot(slotIndex)) return; // consumed on throw

            Vector3 origin = viewPoint != null ? viewPoint.position : transform.position + Vector3.up * 1.5f;
            GameObject projectile = Instantiate(item.ThrownProjectilePrefab, origin, Quaternion.LookRotation(direction));
            NetworkServer.Spawn(projectile);
            projectile.GetComponent<ILaunchable>()?.ServerLaunch(direction.normalized, item, netIdentity, remainingUses);
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
