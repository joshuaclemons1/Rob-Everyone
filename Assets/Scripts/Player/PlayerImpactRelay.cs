using System.Collections;
using Mirror;
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
    // The PvP path additionally opens a *steal window* (IsStealable) --
    // during it a rival can press E to open the steal screen and drag one
    // item out of this player's hotbar (PlayerTheftTarget).
    [RequireComponent(typeof(PlayerRagdoll))]
    public class PlayerImpactRelay : NetworkBehaviour
    {
        private PlayerRagdoll ragdoll;

        // Server-queryable "is this player currently stunned" flag --
        // PlayerRagdoll can't hold this itself (plain MonoBehaviour, no
        // SyncVar), but sabotage systems need to check it server-side
        // (e.g. don't stack a second stun on an already-stunned target).
        [field: SyncVar]
        public bool IsStunned { get; private set; }

        // Separate from IsStunned -- a car impact also sets IsStunned but
        // shouldn't open a theft window (gameplay-design.md frames the
        // steal mechanic as specifically "stunning a rival," i.e. PvP
        // sabotage items only). Client-visible so PlayerTheftTarget's
        // CanInteract can gate the "Steal from ..." prompt without a round
        // trip; the steal Command still re-validates server-side.
        [field: SyncVar]
        public bool IsStealable { get; private set; }

        private const float DefaultStealWindowSeconds = 6f;

        // Expiry timestamp (server clock) rather than a coroutine: two PvP
        // hits on the same target in quick succession would otherwise
        // start two independent ClearStealableAfter coroutines, and the
        // first to fire would slam the window shut early for the second
        // attacker. A timestamp just gets pushed later -- never earlier.
        private double stealableUntil;

        private void Awake()
        {
            ragdoll = GetComponent<PlayerRagdoll>();
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
        // this just also opens the steal window. CarDriver keeps calling
        // the plain version unchanged.
        [Server]
        public void ServerApplyPvpImpact(Vector3 direction, float force, float duration) => ServerApplyPvpImpact(direction, force, duration, DefaultStealWindowSeconds);

        [Server]
        public void ServerApplyPvpImpact(Vector3 direction, float force, float duration, float stealWindowSeconds)
        {
            // Deliberate: the inner ServerApplyImpact no-ops if the target
            // was already stunned (e.g. a car got them first), but we
            // still open the steal window here. They're already down --
            // robbing them is fair game, and the window shouldn't depend
            // on which hit type happened to land first.
            ServerApplyImpact(direction, force, duration);
            IsStealable = true;
            stealableUntil = NetworkTime.time + stealWindowSeconds;
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

        // A thrown carried player -- CarryController.ServerDrop calls
        // this. The body is already ragdolling; this just adds the launch
        // impulse on every client.
        [Server]
        public void ServerThrow(Vector3 direction, float force) => RpcThrow(direction, force);

        [ClientRpc]
        private void RpcThrow(Vector3 direction, float force) => ragdoll.ApplyThrowImpulse(direction, force);

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
        }
    }
}
