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
        private CarryController carry;
        private PlayerAnimationDriver animDriver;

        // Server-only, per-attacker cooldown tracking, keyed by item name
        // (not by slot index -- a cooldown belongs to the item type, not
        // to whichever physical slot it currently sits in).
        private readonly Dictionary<string, float> nextReadyTime = new();

        // Owner-side mirror of the same clock, for the HUD only -- set at
        // the exact same point the server would actually consume the
        // cooldown (see TryUseMelee/TryUseRanged), not just on every
        // click, so a whiff that never reaches the server doesn't start a
        // countdown the server never started. Purely cosmetic: the
        // dictionary above stays the sole authority on whether a swing/
        // shot actually lands, this can drift and nothing breaks.
        private readonly Dictionary<string, float> localNextReadyTime = new();

        // "My own" SabotageUseController, the same LocalPlayer convention
        // PlayerInventory.LocalPlayer already establishes -- HotbarSlotUI
        // uses this to read the local player's own cooldown for its HUD
        // countdown text.
        public static SabotageUseController LocalPlayer =>
            NetworkClient.localPlayer != null ? NetworkClient.localPlayer.GetComponent<SabotageUseController>() : null;

        private void StartLocalCooldown(ItemDefinition item)
        {
            if (item.CooldownSeconds <= 0f) return;
            localNextReadyTime[item.ItemName] = Time.time + item.CooldownSeconds;
        }

        // Seconds left before `itemName` is usable again -- 0 if it's not
        // on cooldown (or has none tracked at all).
        public float GetCooldownRemaining(string itemName)
        {
            if (string.IsNullOrEmpty(itemName)) return 0f;
            return localNextReadyTime.TryGetValue(itemName, out float ready) ? Mathf.Max(0f, ready - Time.time) : 0f;
        }

        private void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
            carry = GetComponent<CarryController>();
            animDriver = GetComponent<PlayerAnimationDriver>();
        }

        // Fire the arm one-shot the item asks for, on press, regardless of
        // whether the swing/shot actually connects -- a whiffed swing
        // should still animate. None = no gesture.
        private void PlayUseAnimation(ItemDefinition item)
        {
            if (animDriver == null) return;
            switch (item.UseAnimation)
            {
                case UseAnimation.Swing: animDriver.PlayAction(PlayerActionAnim.Swing); break;
                case UseAnimation.Shoot: animDriver.PlayAction(PlayerActionAnim.Shoot); break;
            }
        }

        private ItemDefinition ResolveSelectedItem()
        {
            int i = inventory.SelectedSlot;
            if (i < 0 || i >= PlayerInventory.SlotCount) return null; // -1 = nothing selected (e.g. carrying a body)
            InventorySlot? slot = inventory.Slots[i];
            return slot?.Item;
        }

        private void Update()
        {
            if (!isOwned || RobEveryone.UI.InventoryScreenUI.MenuOpen) return;
            if (carry != null && carry.IsCarrying) return; // LMB is the throw while carrying a body
            if (Mouse.current == null) return;

            ItemDefinition item = ResolveSelectedItem();
            if (item == null) return;

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                // Still cooling down (Taser) -- skip the whole attempt,
                // animation included, rather than swinging/firing for
                // nothing every time the server would reject it anyway.
                if (GetCooldownRemaining(item.ItemName) > 0f) return;

                PlayUseAnimation(item);
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
                    PlayUseAnimation(item);
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
                if (targetIdentity != null && targetIdentity != netIdentity)
                {
                    StartLocalCooldown(item);
                    CmdUseMelee(targetIdentity);
                }
            }
        }

        private void TryUseRanged(ItemDefinition item)
        {
            if (viewPoint == null) return;

            if (Physics.Raycast(viewPoint.position, viewPoint.forward, out RaycastHit hit, item.Range, playerMask))
            {
                NetworkIdentity targetIdentity = hit.collider.GetComponentInParent<NetworkIdentity>();
                if (targetIdentity != null && targetIdentity != netIdentity)
                {
                    StartLocalCooldown(item);
                    CmdUseRanged(targetIdentity);
                }
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
