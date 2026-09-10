using Mirror;
using UnityEngine;

namespace RobEveryone.Player
{
    // The "can this downed player be picked up, and is one being carried"
    // half of the carry mechanic (CarryController is the other). While
    // carried: the carried player's own client drives their networked
    // root to the carrier's carry anchor (NetworkTransform propagates it
    // to everyone else), and every client pins the local ragdoll hips
    // there too so the body hangs from the carry point with the rest of
    // the limbs still flopping under physics.
    [RequireComponent(typeof(PlayerRagdoll))]
    [RequireComponent(typeof(NetworkIdentity))]
    public class Carryable : NetworkBehaviour
    {
        // Non-null while this player is being carried -- the carrier's
        // NetworkIdentity. Server-set; synced so every client renders the
        // follow and PlayerTheftTarget can hide the steal prompt.
        [SyncVar] public NetworkIdentity carriedBy;

        // How long the ragdoll is held past a drop/throw before the
        // player can stand up (fed to PlayerRagdoll.NotifyReleasedFromCarry).
        [SerializeField] private float carryReleaseStun = 1.5f;

        private PlayerRagdoll ragdoll;
        private Rigidbody hips;

        public bool IsCarried => carriedBy != null;

        // Only a ragdolled player who isn't already carried can be
        // grabbed. Car hits and sabotage stuns both leave PlayerRagdoll
        // ragdolling, so this covers both.
        public bool CanBeGrabbed => ragdoll != null && ragdoll.IsRagdolling && carriedBy == null;

        private void Awake()
        {
            ragdoll = GetComponent<PlayerRagdoll>();
        }

        [Server]
        public void ServerAttach(NetworkIdentity carrier)
        {
            if (!CanBeGrabbed || carrier == null) return;
            carriedBy = carrier;
        }

        [Server]
        public void ServerDetach()
        {
            carriedBy = null;
            RpcOnDetached();
        }

        [ClientRpc]
        private void RpcOnDetached()
        {
            // Un-pin the hips so the full ragdoll physics resume, and
            // hold the ragdoll a bit longer before standing up.
            if (hips != null) hips.isKinematic = false;
            if (ragdoll != null) ragdoll.NotifyReleasedFromCarry(carryReleaseStun);
        }

        private void Update()
        {
            if (carriedBy == null) return;

            CarryController carrier = carriedBy.GetComponent<CarryController>();
            if (carrier == null || carrier.CarryAnchor == null) return;

            Vector3 pos = carrier.CarryAnchor.position;
            Quaternion rot = carrier.CarryAnchor.rotation;

            // Every client + the server: pin the local ragdoll hips to
            // the carry point (the rest of the ragdoll flops from
            // physics off the pinned hips).
            LazyResolveHips();
            if (hips != null)
            {
                hips.isKinematic = true;
                hips.position = pos;
                hips.rotation = rot;
            }

            // The carried player's own client owns the networked root
            // transform (NetworkTransform is client-authoritative) --
            // move it and let the sync carry it to everyone else. Their
            // FirstPersonController is disabled (they're ragdolled), so
            // nothing on their side fights this.
            if (isOwned)
            {
                transform.SetPositionAndRotation(pos, rot);
            }
        }

        private void LazyResolveHips()
        {
            if (hips != null) return;
            RagdollHips h = GetComponentInChildren<RagdollHips>(true);
            if (h != null) hips = h.Rigidbody;
        }
    }
}
