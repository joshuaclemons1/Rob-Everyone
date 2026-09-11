using System.Collections;
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
        [SyncVar(hook = nameof(OnCarriedByChanged))] public NetworkIdentity carriedBy;

        // How long the ragdoll is held past a drop/throw before the
        // player can stand up (fed to PlayerRagdoll.NotifyReleasedFromCarry).
        [SerializeField] private float carryReleaseStun = 1.5f;

        // How long collision with the carrier stays ignored after
        // release, on top of restoring it at all -- gives a thrown body a
        // moment to actually clear the carrier before becoming solid to
        // them again, so the very impulse that's supposed to launch it
        // away doesn't immediately collide with (and get absorbed by) the
        // carrier's own CharacterController while still overlapping it.
        [SerializeField] private float postReleaseCollisionIgnoreTime = 0.3f;

        private PlayerRagdoll ragdoll;
        private Rigidbody hips;

        // Cached the moment collision gets ignored, so it can be restored
        // later even though carriedBy (the only other way to find the
        // carrier) may have already gone stale/null by then.
        private CharacterController ignoredCarrierController;
        private bool collisionIgnored;

        // Set synchronously the instant RpcOnDetached fires on this
        // client, independent of whether the carriedBy SyncVar has
        // actually ticked to null here yet -- a SyncVar's own sync can
        // lag a beat behind an RPC sent the same server frame, and
        // Update() below must never re-pin the hips (and cancel a throw
        // impulse also carried on that RPC, see ServerDetach) just
        // because carriedBy still reads stale-non-null for one more tick.
        private bool detachedLocally;

        private void OnCarriedByChanged(NetworkIdentity oldValue, NetworkIdentity newValue)
        {
            if (newValue != null) detachedLocally = false; // a fresh attach -- clear any stale flag from a prior carry
        }

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
            detachedLocally = false;
        }

        // thrown/direction/force ride along on the SAME detach call
        // (and therefore the same RPC) as the unpin, rather than
        // CarryController firing a second, separate RPC afterward -- two
        // independent ClientRpcs from two different NetworkBehaviours on
        // the same GameObject, sent in the same server frame, aren't
        // guaranteed to execute in send order on every client. Confirmed
        // bug: a max-charge throw looked identical to a gentle drop --
        // the impulse could land before the unpin (if it went through a
        // separate RPC), or the unpin's own Update() re-pin could race it
        // -- either way, the throw velocity got silently discarded.
        // Bundling both into one RPC removes the ordering question
        // entirely.
        [Server]
        public void ServerDetach(bool thrown = false, Vector3 throwDirection = default, float throwForce = 0f)
        {
            carriedBy = null;
            RpcOnDetached(thrown, throwDirection, throwForce);
        }

        [ClientRpc]
        private void RpcOnDetached(bool thrown, Vector3 throwDirection, float throwForce)
        {
            detachedLocally = true;

            // Un-pin the hips so the full ragdoll physics resume, and
            // hold the ragdoll a bit longer before standing up.
            if (hips != null) hips.isKinematic = false;
            if (ragdoll != null) ragdoll.NotifyReleasedFromCarry(carryReleaseStun);

            if (thrown && throwForce > 0f && ragdoll != null)
            {
                ragdoll.ApplyThrowImpulse(throwDirection, throwForce);
            }

            if (collisionIgnored) StartCoroutine(RestoreCollisionAfterDelay());
        }

        private void Update()
        {
            if (carriedBy == null || detachedLocally) return;

            CarryController carrier = carriedBy.GetComponent<CarryController>();
            if (carrier == null || carrier.CarryAnchor == null) return;

            // The carry anchor holds this body directly against (often
            // overlapping) the carrier -- without this, the carrier's own
            // CharacterController physically collides with their own
            // cargo's still-solid ragdoll colliders (only the
            // CharacterController is disabled while ragdolling, not the
            // individual limb colliders). Confirmed bug: walking forward
            // while carrying felt like pushing into a wall, and a thrown
            // body could get its launch impulse absorbed by an immediate
            // collision with the carrier it's still overlapping.
            if (!collisionIgnored)
            {
                CharacterController carrierController = carrier.GetComponent<CharacterController>();
                if (carrierController != null && ragdoll != null)
                {
                    SetCollisionIgnored(carrierController, true);
                    ignoredCarrierController = carrierController;
                    collisionIgnored = true;
                }
            }

            Vector3 pos = carrier.CarryAnchor.position;
            Quaternion rot = carrier.CarryAnchor.rotation;

            // Every client + the server: pin the local ragdoll hips to
            // the carry point (the rest of the ragdoll flops from
            // physics off the pinned hips). MovePosition/MoveRotation,
            // not a direct .position/.rotation assignment -- the latter
            // teleports a kinematic Rigidbody without telling the physics
            // engine any velocity, which starves every joint-connected
            // limb (including the head) of the reaction/lag physics that
            // makes a carried body look floppy instead of just dragged
            // along rigidly, and very plausibly left the hips in a bad
            // internal state for the very next frame's switch to
            // non-kinematic + AddForce impulse on throw.
            LazyResolveHips();
            if (hips != null)
            {
                hips.isKinematic = true;
                hips.MovePosition(pos);
                hips.MoveRotation(rot);
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

        private void SetCollisionIgnored(CharacterController controller, bool ignore)
        {
            if (controller == null || ragdoll == null) return;
            foreach (Collider c in ragdoll.GetRagdollColliders())
            {
                if (c != null) Physics.IgnoreCollision(controller, c, ignore);
            }
        }

        private IEnumerator RestoreCollisionAfterDelay()
        {
            yield return new WaitForSeconds(postReleaseCollisionIgnoreTime);
            SetCollisionIgnored(ignoredCarrierController, false);
            ignoredCarrierController = null;
            collisionIgnored = false;
        }
    }
}
