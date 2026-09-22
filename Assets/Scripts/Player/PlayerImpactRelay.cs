using System.Collections;
using Mirror;
using RobEveryone.Audio;
using RobEveryone.Inventory;
using UnityEngine;

namespace RobEveryone.Player
{
    // Thin network wrapper around PlayerRagdoll.ApplyImpact -- kept
    // entirely separate from PlayerRagdoll itself rather than converting
    // that script to a NetworkBehaviour in place, since its bone-hierarchy
    // patching logic has nothing to do with networking and every extra
    // moving part there is a chance to break something delicate.
    //
    // Server entry points: ServerApplyImpact (a traffic-hazard car hit,
    // from CarDriver) and ServerApplyPvpImpact (a sabotage hit, from
    // SabotageUseController / RetrievableProjectile) -- both fan the
    // knockdown out via RpcApplyImpact, which every client (owner and
    // bystanders alike) plays fully locally. This is a client-side
    // cosmetic ragdoll: bone physics are *not* synced frame by frame
    // (each client's copy can settle into a slightly different final
    // pose), the standard tradeoff for a temporary hit-reaction.
    //
    // Issue #81: the PvP path used to also open a *steal window*
    // (IsStealable) -- during it a rival could press E to open a steal
    // screen and drag one item out of this player's hotbar
    // (PlayerTheftTarget). Replaced with an automatic chance to drop one
    // random item on the ground instead (ragdollDropChance below) --
    // IsStealable/stealableUntil/ClearStealableNow are all still present
    // (PlayerTheftTarget and InventoryScreenUI's own Mode.Steal code
    // still reads them), but nothing sets IsStealable true anymore, so
    // that whole flow is now permanently unreachable rather than
    // formally deleted -- ripping out the shared steal-screen UI code
    // safely is its own separate, more invasive cleanup.
    [RequireComponent(typeof(PlayerRagdoll))]
    public class PlayerImpactRelay : NetworkBehaviour
    {
        // One generic "hit" sound for every impact source (Bat/Hammer/
        // Taser/Tranq Gun and a traffic-hazard car alike) -- RpcApplyImpact
        // doesn't know or care which one caused it, same as the ragdoll
        // knockdown itself doesn't vary by source.
        [SerializeField] private AudioClip[] impactClips;
        [SerializeField, Range(0f, 1f)] private float impactVolume = 0.6f;

        // Issue #81: chance a landed PvP hit drops one random carried
        // item, replacing the old steal-window mechanic -- see this
        // class's own comment above.
        [SerializeField, Range(0f, 1f)] private float ragdollDropChance = 0.5f;

        private PlayerRagdoll ragdoll;
        private PlayerInventory inventory;

        // Server-queryable "is this player currently stunned" flag --
        // PlayerRagdoll can't hold this itself (plain MonoBehaviour, no
        // SyncVar), but sabotage systems need to check it server-side
        // (e.g. don't stack a second stun on an already-stunned target).
        [field: SyncVar]
        public bool IsStunned { get; private set; }

        // No longer ever set true (see this class's own comment) --
        // kept only because PlayerTheftTarget/InventoryScreenUI's dormant
        // Mode.Steal code still reads it.
        [field: SyncVar]
        public bool IsStealable { get; private set; }

        // Expiry timestamp (server clock) rather than a coroutine: two PvP
        // hits on the same target in quick succession would otherwise
        // start two independent ClearStealableAfter coroutines, and the
        // first to fire would slam the window shut early for the second
        // attacker. A timestamp just gets pushed later -- never earlier.
        private double stealableUntil;

        private void Awake()
        {
            ragdoll = GetComponent<PlayerRagdoll>();
            inventory = GetComponent<PlayerInventory>();
        }

        [Server]
        public void ServerApplyImpact(Vector3 direction, float force) => ServerApplyImpact(direction, force, ragdoll.DefaultStunDuration);

        [Server]
        public void ServerApplyImpact(Vector3 direction, float force, float duration)
        {
            if (IsStunned) return; // no stacking a second hit on top of an active stun
            IsStunned = true;
            RpcApplyImpact(direction, force, duration);
            StartCoroutine(ClearStunnedAfter(duration));
        }

        // PvP sabotage hits call this instead of the plain
        // ServerApplyImpact -- everything about the stun is identical,
        // this just also rolls the item-drop chance (issue #81). CarDriver
        // keeps calling the plain version unchanged, so a car impact never
        // drops loot, matching the old steal-window's own PvP-only scope.
        [Server]
        public void ServerApplyPvpImpact(Vector3 direction, float force, float duration)
        {
            // Deliberate: the inner ServerApplyImpact no-ops if the target
            // was already stunned (e.g. a car got them first), but the
            // drop roll still happens here regardless -- they're already
            // down, dropping loot is fair game the same way robbing them
            // used to be, and shouldn't depend on which hit type happened
            // to land first.
            ServerApplyImpact(direction, force, duration);

            if (inventory != null && Random.value < ragdollDropChance)
            {
                inventory.DropRandomSlot(transform.position, transform.rotation);
            }
        }

        // Called by a successful steal (PlayerTheftTarget) so a second
        // attacker can't also loot the same stun -- one item per window,
        // matching gameplay-design.md's "take one item" framing.
        [Server]
        public void ClearStealableNow()
        {
            IsStealable = false;
            stealableUntil = 0;
        }

        private void Update()
        {
            // Manual isServer guard (not the [Server] attribute, which
            // would warn every frame on a client) -- matches RoundManager/
            // GameFlowManager's own Update pattern.
            if (!isServer) return;
            if (IsStealable && NetworkTime.time >= stealableUntil) IsStealable = false;
        }

        private IEnumerator ClearStunnedAfter(float duration)
        {
            yield return new WaitForSeconds(duration);
            IsStunned = false;
        }

        [ClientRpc]
        private void RpcApplyImpact(Vector3 direction, float force, float duration)
        {
            ragdoll.ApplyImpact(direction, force, duration);
            SfxPlayer.PlayRandomAt(impactClips, transform.position, impactVolume);
        }
    }
}
