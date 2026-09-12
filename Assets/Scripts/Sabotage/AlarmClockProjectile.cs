using System.Collections;
using Mirror;
using RobEveryone.AI;
using RobEveryone.Audio;
using RobEveryone.Inventory;
using RobEveryone.Items;
using UnityEngine;

namespace RobEveryone.Sabotage
{
    // Thrown like Dynamite (fuse-timer, not collision-triggered), but
    // "detonating" here means resolving who's nearby to frame and which
    // Homeowner to alert -- not physically impacting anyone. Single-use,
    // consumed on throw via the same RemoveSlot path Dynamite already
    // uses (MaxUses stays 0, not durability-tracked). Reuses
    // ItemDefinition.BlastRadius's existing "how far this thrown item's
    // effect reaches" meaning as the search radius for both the blamed
    // player and the Homeowner, rather than adding a second radius field
    // just for this one item.
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(NetworkIdentity))]
    public class AlarmClockProjectile : NetworkBehaviour, ILaunchable
    {
        [SerializeField] private float throwSpeed = 10f;
        [SerializeField] private float fuseSeconds = 2.5f;

        [SerializeField] private AudioClip[] ringClips;
        [SerializeField, Range(0f, 1f)] private float ringVolume = 0.8f;

        private Rigidbody body;
        private ItemDefinition sourceItem;
        private NetworkIdentity thrower;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
        }

        public override void OnStartClient()
        {
            if (!isServer) body.isKinematic = true;
        }

        [Server]
        public void ServerLaunch(Vector3 direction, ItemDefinition item, NetworkIdentity throwerIdentity, int remainingUses)
        {
            // remainingUses is irrelevant here -- consumed whole on
            // throw (MaxUses == 0, untracked), never lands as a pickup.
            sourceItem = item;
            thrower = throwerIdentity;
            body.linearVelocity = direction * throwSpeed;
            StartCoroutine(ResolveAfterFuse());
        }

        private IEnumerator ResolveAfterFuse()
        {
            yield return new WaitForSeconds(fuseSeconds);
            Resolve();
        }

        [Server]
        private void Resolve()
        {
            float radius = sourceItem != null ? sourceItem.BlastRadius : 0f;
            if (radius > 0f)
            {
                PlayerInventory blamed = FindNearestOtherPlayer(radius);
                HomeownerAI homeowner = FindNearestHomeowner(radius);
                if (homeowner != null) homeowner.ForceAlert(transform.position, blamed);
            }

            RpcRing();
            NetworkServer.Destroy(gameObject);
        }

        [ClientRpc]
        private void RpcRing()
        {
            SfxPlayer.PlayRandomAt(ringClips, transform.position, ringVolume);
        }

        private PlayerInventory FindNearestOtherPlayer(float radius)
        {
            PlayerInventory nearest = null;
            float nearestDistance = radius;

            foreach (PlayerInventory player in PlayerInventory.AllPlayers)
            {
                if (player == null) continue;
                if (player.GetComponent<NetworkIdentity>() == thrower) continue; // don't frame yourself

                float distance = Vector3.Distance(player.transform.position, transform.position);
                if (distance <= nearestDistance)
                {
                    nearest = player;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        private HomeownerAI FindNearestHomeowner(float radius)
        {
            HomeownerAI nearest = null;
            float nearestDistance = radius;

            foreach (Collider hit in Physics.OverlapSphere(transform.position, radius))
            {
                HomeownerAI homeowner = hit.GetComponentInParent<HomeownerAI>();
                if (homeowner == null) continue;

                float distance = Vector3.Distance(homeowner.transform.position, transform.position);
                if (distance <= nearestDistance)
                {
                    nearest = homeowner;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }
    }
}
