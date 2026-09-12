using System.Collections;
using System.Collections.Generic;
using Mirror;
using RobEveryone.Audio;
using RobEveryone.Items;
using RobEveryone.Player;
using UnityEngine;

namespace RobEveryone.Sabotage
{
    // A thrown sabotage item in flight (Dynamite first; a future Hammer
    // throw would reuse this). Detonates on a fuse timer rather than on
    // collision -- CarDriver already had to debounce OnTriggerEnter firing
    // once per ragdoll limb collider on a single real hit, and a
    // fuse-based detonation sidesteps that problem entirely rather than
    // re-solving it here, at the cost of not exploding early on a direct
    // hit.
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(NetworkIdentity))]
    public class SabotageProjectile : NetworkBehaviour, ILaunchable
    {
        [SerializeField] private float throwSpeed = 12f;
        [SerializeField] private float fuseSeconds = 2.5f;

        // Not yet populated -- no CC0 explosion sound sourced yet (Kenney's
        // Impact/Interface/RPG Audio packs don't have one). Wire this once
        // an explosion pack is downloaded; empty is a safe no-op via
        // SfxPlayer in the meantime, not a crash.
        [SerializeField] private AudioClip[] explosionClips;
        [SerializeField, Range(0f, 1f)] private float explosionVolume = 0.8f;

        private Rigidbody body;
        private ItemDefinition sourceItem;
        private NetworkIdentity thrower;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
        }

        public override void OnStartClient()
        {
            // Only the server simulates this Rigidbody -- every other
            // client just displays wherever NetworkTransform (Server
            // Authority) says it is, same reasoning FirstPersonController's
            // isOwned guard uses for player movement.
            if (!isServer) body.isKinematic = true;
        }

        [Server]
        public void ServerLaunch(Vector3 direction, ItemDefinition item, NetworkIdentity throwerIdentity, int remainingUses)
        {
            // remainingUses is irrelevant here -- Dynamite is consumed
            // whole on throw (MaxUses == 0, untracked), never lands as a
            // pickup.
            sourceItem = item;
            thrower = throwerIdentity;
            body.linearVelocity = direction * throwSpeed;
            StartCoroutine(DetonateAfterFuse());
        }

        private IEnumerator DetonateAfterFuse()
        {
            yield return new WaitForSeconds(fuseSeconds);
            Detonate();
        }

        [Server]
        private void Detonate()
        {
            if (sourceItem != null && sourceItem.BlastRadius > 0f)
            {
                var alreadyHit = new HashSet<PlayerImpactRelay>();
                foreach (Collider hit in Physics.OverlapSphere(transform.position, sourceItem.BlastRadius))
                {
                    PlayerImpactRelay relay = hit.GetComponentInParent<PlayerImpactRelay>();
                    // The thrower is excluded from their own blast.
                    if (relay == null || relay.netIdentity == thrower || !alreadyHit.Add(relay)) continue;

                    Vector3 direction = relay.transform.position - transform.position;
                    direction.y = 0f;
                    direction = direction.sqrMagnitude > 0.01f ? direction.normalized : Vector3.forward;
                    relay.ServerApplyPvpImpact(direction + Vector3.up * 0.5f, sourceItem.ImpactForce, sourceItem.StunDuration);
                }
            }

            RpcExplode();
            NetworkServer.Destroy(gameObject);
        }

        [ClientRpc]
        private void RpcExplode()
        {
            SfxPlayer.PlayRandomAt(explosionClips, transform.position, explosionVolume);
        }
    }
}
