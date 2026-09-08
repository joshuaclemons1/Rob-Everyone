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

        private void Awake()
        {
            ragdoll = GetComponent<PlayerRagdoll>();
        }

        [Server]
        public void ServerApplyImpact(Vector3 direction, float force)
        {
            RpcApplyImpact(direction, force);
        }

        [ClientRpc]
        private void RpcApplyImpact(Vector3 direction, float force)
        {
            ragdoll.ApplyImpact(direction, force);
        }
    }
}
