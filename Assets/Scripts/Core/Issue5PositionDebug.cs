using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RobEveryone.Core
{
    // TEMPORARY debug instrumentation for issue #5 (position desync in
    // the Lobby: a player looks correct on their own screen but shows
    // up in a random/airborne/off-map position for the host and every
    // other client). Remove this file (and its RequireComponent on
    // PlayerInventory) once #5 is actually diagnosed and fixed.
    //
    // NetworkTransformReliable is owner-authoritative -- the owner's
    // own client always trusts its own locally-simulated position
    // directly, so a jump here would never come from the owner's own
    // side. This only watches NON-owned instances specifically to
    // catch a remote observer's own synced copy of another player's
    // NetworkTransform snapping or lerping somewhere nonsensical
    // instead of cleanly following the server-intended position --
    // exactly the "correct for them, wrong for everyone watching"
    // failure mode #5 describes. A single-frame jump past
    // teleportJumpThreshold logs the before/after position, since a
    // real per-frame movement update should never cover that much
    // ground at once outside of an intentional teleport.
    public class Issue5PositionDebug : MonoBehaviour
    {
        [SerializeField] private float teleportJumpThreshold = 5f;

        private NetworkIdentity identity;
        private Vector3 lastPosition;
        private bool hasLastPosition;

        private void Awake()
        {
            identity = GetComponent<NetworkIdentity>();
        }

        private void LateUpdate()
        {
            // Only watching what THIS client sees for OTHER players --
            // the owner's own copy is never the one at fault here.
            if (identity == null || identity.isOwned) return;

            Vector3 current = transform.position;
            if (hasLastPosition)
            {
                float delta = Vector3.Distance(lastPosition, current);
                if (delta > teleportJumpThreshold)
                {
                    Debug.LogWarning($"[Issue5] Remote player '{name}' jumped {delta:F1}m in one frame -- " +
                        $"{lastPosition} -> {current} (scene: {SceneManager.GetActiveScene().name}, t={Time.time:F2})");
                }
            }

            lastPosition = current;
            hasLastPosition = true;
        }
    }
}
