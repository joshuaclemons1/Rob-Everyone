using System.Collections;
using UnityEngine;

namespace RobEveryone.Player
{
    // Ragdolls whatever skin PlayerSkinSpawner instantiated -- generic on
    // purpose (was CarImpactReceiver; ApplyImpact(direction, force) is
    // exactly as reusable for a future PvP takedown as it is for a car).
    // Built with Unity's Ragdoll Wizard on each skin *prefab* (Rigidbody +
    // CharacterJoint per limb, kinematic by default so the model just
    // stands rigidly until this script flips it off). Unlike the old
    // version, the skin model stays active/visible at all times now (a
    // future networked player needs to be visible to *other* players, not
    // just its owner) -- visibility toward the owner's own camera is a
    // separate concern, handled by Culling Mask on PlayerCamera excluding
    // the skin's layer, not by hiding the object itself.
    //
    // That exclusion is normally on (you never see your own body during
    // regular first-person play), but the third-person stun cutaway uses
    // this exact same camera -- so the mask is toggled back *on* for the
    // skin layer only while that cutaway is active, otherwise the one
    // moment you're supposed to see your own ragdoll would be the one
    // moment the camera is still hiding it.
    //
    // The camera detaches for the stun and holds a fixed-angle third-person
    // view tracking the ragdoll's hips (found via the RagdollHips marker,
    // not a hardcoded bone name, since the 6 selectable skins don't
    // necessarily share bone names) -- the angle itself is locked to the
    // yaw captured *at the moment of impact*, not updated live, so the
    // physics can't spin the camera around.
    [RequireComponent(typeof(FirstPersonController))]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerSkinSpawner))]
    public class PlayerRagdoll : MonoBehaviour
    {
        [SerializeField] private float stunDuration = 2f;

        [Header("Third-person ragdoll view")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private Vector3 thirdPersonOffset = new(0f, 2.5f, -5f);
        [SerializeField] private float lookAtHeightOffset = 0.5f;

        private FirstPersonController firstPersonController;
        private CharacterController characterController;
        private PlayerSkinSpawner skinSpawner;
        private Camera playerCamera;
        private int cameraOriginalCullingMask;

        private Rigidbody[] ragdollBodies;
        private Vector3[] restLocalPositions;
        private Quaternion[] restLocalRotations;
        private Rigidbody hipsRigidbody;

        private Transform cameraOriginalParent;
        private Vector3 cameraOriginalLocalPosition;
        private Quaternion cameraOriginalLocalRotation;

        private bool isStunned;

        private void Awake()
        {
            firstPersonController = GetComponent<FirstPersonController>();
            characterController = GetComponent<CharacterController>();
            skinSpawner = GetComponent<PlayerSkinSpawner>();

            if (cameraTransform != null) playerCamera = cameraTransform.GetComponent<Camera>();
        }

        private void Start()
        {
            // Start, not Awake -- guarantees PlayerSkinSpawner's own Awake
            // (which does the actual Instantiate) has already run.
            GameObject skin = skinSpawner.SkinInstance;
            if (skin == null)
            {
                Debug.LogWarning("PlayerRagdoll found no skin instance from PlayerSkinSpawner -- check the Player Skin Roster is assigned and has at least one entry.", this);
                return;
            }

            RagdollHips hips = skin.GetComponentInChildren<RagdollHips>(true);
            if (hips == null)
            {
                Debug.LogWarning($"PlayerRagdoll: the currently selected skin ({skin.name}) has no RagdollHips marker -- it needs the Ragdoll Wizard run on its prefab, with the Pelvis bone marked. Impacts will do nothing until then.", this);
                return;
            }

            hipsRigidbody = hips.Rigidbody;
            ragdollBodies = skin.GetComponentsInChildren<Rigidbody>(true);
            restLocalPositions = new Vector3[ragdollBodies.Length];
            restLocalRotations = new Quaternion[ragdollBodies.Length];
            for (int i = 0; i < ragdollBodies.Length; i++)
            {
                restLocalPositions[i] = ragdollBodies[i].transform.localPosition;
                restLocalRotations[i] = ragdollBodies[i].transform.localRotation;
                ragdollBodies[i].isKinematic = true;
            }
        }

        public void ApplyImpact(Vector3 direction, float force)
        {
            if (isStunned || hipsRigidbody == null) return;
            StartCoroutine(ImpactSequence(direction, force));
        }

        private IEnumerator ImpactSequence(Vector3 direction, float force)
        {
            isStunned = true;

            firstPersonController.enabled = false;
            characterController.enabled = false;

            // Captured once, before the ragdoll starts moving -- keeps the
            // third-person camera's facing stable even though the hips
            // it's tracking are about to fly off unpredictably.
            Quaternion rigYaw = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

            BeginRagdoll(direction, force);
            BeginThirdPersonView();

            float elapsed = 0f;
            while (elapsed < stunDuration)
            {
                UpdateThirdPersonView(rigYaw);
                elapsed += Time.deltaTime;
                yield return null;
            }

            EndThirdPersonView();
            EndRagdoll();

            characterController.enabled = true;
            firstPersonController.enabled = true;

            isStunned = false;
        }

        private void BeginRagdoll(Vector3 direction, float force)
        {
            for (int i = 0; i < ragdollBodies.Length; i++)
            {
                ragdollBodies[i].isKinematic = false;
            }

            hipsRigidbody.linearVelocity = Vector3.zero;
            hipsRigidbody.angularVelocity = Vector3.zero;
            hipsRigidbody.AddForce(direction * force, ForceMode.Impulse);
        }

        private void EndRagdoll()
        {
            // Carry the player's logical position to wherever the ragdoll
            // actually ended up (it can roll/slide well away from the
            // impact point) rather than snapping back to where they got
            // hit -- a raycast down finds the real ground height there
            // instead of trusting the ragdoll's own (possibly mid-air or
            // sunk-into-the-floor) Y position.
            Vector3 landed = hipsRigidbody.position;
            if (Physics.Raycast(landed + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 20f))
            {
                landed.y = hit.point.y;
            }
            transform.position = landed;

            // Re-level pitch/roll -- keep facing, just stand back up.
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

            for (int i = 0; i < ragdollBodies.Length; i++)
            {
                ragdollBodies[i].linearVelocity = Vector3.zero;
                ragdollBodies[i].angularVelocity = Vector3.zero;
                ragdollBodies[i].transform.localPosition = restLocalPositions[i];
                ragdollBodies[i].transform.localRotation = restLocalRotations[i];
                ragdollBodies[i].isKinematic = true;
            }
        }

        private void BeginThirdPersonView()
        {
            if (cameraTransform == null) return;

            cameraOriginalParent = cameraTransform.parent;
            cameraOriginalLocalPosition = cameraTransform.localPosition;
            cameraOriginalLocalRotation = cameraTransform.localRotation;

            cameraTransform.SetParent(null, true);

            // Turn the skin layer back ON for this camera, just for the
            // cutaway -- otherwise the same Culling Mask that hides your
            // body during normal play would also hide it here, the one
            // moment you're actually meant to see it.
            if (playerCamera != null)
            {
                cameraOriginalCullingMask = playerCamera.cullingMask;
                playerCamera.cullingMask |= skinSpawner.SkinLayer.value;
            }
        }

        private void UpdateThirdPersonView(Quaternion rigYaw)
        {
            if (cameraTransform == null) return;

            Vector3 trackedPosition = hipsRigidbody.position;

            Vector3 desiredPosition = trackedPosition + rigYaw * thirdPersonOffset;
            cameraTransform.position = desiredPosition;

            Vector3 lookTarget = trackedPosition + Vector3.up * lookAtHeightOffset;
            cameraTransform.rotation = Quaternion.LookRotation((lookTarget - desiredPosition).normalized, Vector3.up);
        }

        private void EndThirdPersonView()
        {
            if (cameraTransform == null) return;

            cameraTransform.SetParent(cameraOriginalParent, false);
            cameraTransform.localPosition = cameraOriginalLocalPosition;
            cameraTransform.localRotation = cameraOriginalLocalRotation;

            if (playerCamera != null) playerCamera.cullingMask = cameraOriginalCullingMask;
        }
    }
}
