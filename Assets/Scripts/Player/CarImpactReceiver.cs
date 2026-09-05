using System.Collections;
using UnityEngine;

namespace RobEveryone.Player
{
    // Reacts to CarDriver hitting the player: not a real per-bone ragdoll
    // (the player rig isn't set up for that, and it's a much bigger task
    // than a car-hazard side feature) -- instead swaps the whole capsule
    // from CharacterController-driven movement to a plain physics
    // Rigidbody for a few seconds so it actually tumbles from the impact,
    // then stands it back up and hands control back to
    // FirstPersonController. Pure knockback + stun, no round/loot
    // consequence -- see stage3j-traffic-hazard.md for why.
    [RequireComponent(typeof(FirstPersonController))]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    public class CarImpactReceiver : MonoBehaviour
    {
        [SerializeField] private float stunDuration = 1.5f;

        private FirstPersonController firstPersonController;
        private CharacterController characterController;
        private Rigidbody rb;
        private CapsuleCollider capsuleCollider;

        private bool isStunned;

        private void Awake()
        {
            firstPersonController = GetComponent<FirstPersonController>();
            characterController = GetComponent<CharacterController>();
            rb = GetComponent<Rigidbody>();
            capsuleCollider = GetComponent<CapsuleCollider>();

            // Both start disabled -- CharacterController drives the player
            // normally, these only turn on for the tumble.
            rb.isKinematic = true;
            capsuleCollider.enabled = false;
        }

        public void ApplyImpact(Vector3 direction, float force)
        {
            if (isStunned) return;
            StartCoroutine(ImpactSequence(direction, force));
        }

        private IEnumerator ImpactSequence(Vector3 direction, float force)
        {
            isStunned = true;

            firstPersonController.enabled = false;
            characterController.enabled = false;

            capsuleCollider.enabled = true;
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.AddForce(direction * force, ForceMode.Impulse);
            // A bit of spin makes the tumble read as "knocked over" rather
            // than just sliding stiffly across the ground.
            rb.AddTorque(new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * force, ForceMode.Impulse);

            yield return new WaitForSeconds(stunDuration);

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            capsuleCollider.enabled = false;

            // Stand back up -- keep wherever it landed (X/Z position and
            // facing), just re-level pitch/roll.
            Vector3 uprightEuler = new(0f, transform.eulerAngles.y, 0f);
            transform.rotation = Quaternion.Euler(uprightEuler);

            characterController.enabled = true;
            firstPersonController.enabled = true;

            isStunned = false;
        }
    }
}
