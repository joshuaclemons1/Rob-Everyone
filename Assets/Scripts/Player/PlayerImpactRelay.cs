using System.Collections;
using Mirror;
using UnityEngine;

namespace RobEveryone.Player
{
    // Thin network wrapper around PlayerRagdoll.ApplyImpact -- kept
    // entirely separate from PlayerRagdoll itself rather than converting
    // that script to a NetworkBehaviour in place, since its bone-hierarchy
    // patching logic has nothing to do with networking and every extra
    // moving part there is a chance to break something delicate. Only the
    // server ever calls RpcApplyImpact (from CarDriver's own isServer-
    // gated impact detection) -- every client that receives it (owner and
    // bystanders alike) then plays the ragdoll fully locally. This is a
    // client-side cosmetic ragdoll: bone physics are *not* synced frame by
    // frame between clients (each client's copy can settle into a
    // slightly different final pose/position after tumbling), which is
    // the standard tradeoff for this kind of hit-reaction -- syncing
    // every ragdoll bone's Rigidbody over the network is expensive and
    // not worth it for a temporary knockdown.
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
        // CanInteract can gate the "Steal item" prompt without a round
        // trip; Interact() still re-validates server-side regardless.
        [field: SyncVar]
        public bool IsStealable { get; private set; }

        private const float DefaultStealWindowSeconds = 6f;

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

        // PvP sabotage hits (Taser/Bat/Hammer/Tranq Gun/Dynamite) call
        // this instead of the plain ServerApplyImpact above -- everything
        // else about the stun is identical, this just additionally opens
        // the steal window. CarDriver keeps calling the plain version
        // unchanged.
        [Server]
        public void ServerApplyPvpImpact(Vector3 direction, float force, float duration) => ServerApplyPvpImpact(direction, force, duration, DefaultStealWindowSeconds);

        [Server]
        public void ServerApplyPvpImpact(Vector3 direction, float force, float duration, float stealWindowSeconds)
        {
            ServerApplyImpact(direction, force, duration);
            IsStealable = true;
            StartCoroutine(ClearStealableAfter(stealWindowSeconds));
        }

        // Called by a successful theft (PlayerTheftTarget.Interact) so a
        // second attacker can't also loot the same stun -- one theft per
        // window, matching the design doc's "take one item" framing.
        [Server]
        public void ClearStealableNow() => IsStealable = false;

        private IEnumerator ClearStunnedAfter(float duration)
        {
            yield return new WaitForSeconds(duration);
            IsStunned = false;
        }

        private IEnumerator ClearStealableAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            IsStealable = false;
        }

        [ClientRpc]
        private void RpcApplyImpact(Vector3 direction, float force, float duration)
        {
            ragdoll.ApplyImpact(direction, force, duration);
        }
    }
}
