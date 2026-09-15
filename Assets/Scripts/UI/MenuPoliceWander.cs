using UnityEngine;

namespace RobEveryone.UI
{
    // Issue #51: a lightweight, non-networked stand-in for the real
    // PoliceAI patrol state machine -- see that script's own comment for
    // why the real one can't just be dropped into the Main Menu (it's a
    // NetworkBehaviour that leans on a live RoundManager/server context
    // MainMenu never has before a player actually clicks Play). This
    // only needs to read as "patrolling" from a distant drone-shot
    // camera, not actually behave like the real AI -- pick a random
    // point nearby, walk to it, repeat, forever.
    public class MenuPoliceWander : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 3.2f;
        [SerializeField] private float rotationSpeed = 4f;
        [SerializeField] private float arrivalDistance = 1.5f;
        [SerializeField] private Vector2 wanderExtents = new(35f, 35f);
        [SerializeField] private string animatorSpeedParam = "Speed";

        // Animator lives on a child of the model root (Police.prefab's
        // skeleton/mesh hierarchy), not the root itself -- confirmed by
        // reading the prefab directly rather than assuming GetComponent
        // would find it.
        private Animator animator;
        private int speedHash;
        private Vector3 origin;
        private Vector3 targetPoint;

        private void Awake()
        {
            animator = GetComponentInChildren<Animator>();
            speedHash = Animator.StringToHash(animatorSpeedParam);
            origin = transform.position;
            PickNewTarget();
        }

        // Called by MenuBackgroundBuilder right after AddComponent, to
        // hand it a wander area sized to where it was actually spawned
        // (inside the house ring) instead of relying on this field's
        // Inspector default.
        public void Initialize(Vector2 extents)
        {
            wanderExtents = extents;
            PickNewTarget();
        }

        private void Update()
        {
            Vector3 toTarget = targetPoint - transform.position;
            toTarget.y = 0f;

            if (toTarget.magnitude <= arrivalDistance)
            {
                PickNewTarget();
                return;
            }

            Vector3 direction = toTarget.normalized;
            transform.position += direction * moveSpeed * Time.deltaTime;

            Quaternion desired = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, desired, rotationSpeed * Time.deltaTime);

            if (animator != null) animator.SetFloat(speedHash, moveSpeed);
        }

        private void PickNewTarget()
        {
            Vector2 offset = Random.insideUnitCircle;
            targetPoint = origin + new Vector3(offset.x * wanderExtents.x, 0f, offset.y * wanderExtents.y);
        }
    }
}
