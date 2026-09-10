using RobEveryone.Player;
using UnityEngine;

namespace RobEveryone.Inventory
{
    // Swaps the local player's first-person camera to a static,
    // front-facing third-person shot for the duration of the Tab / steal
    // screen -- you see your own character from the front while you drag
    // items around. Same detach-reposition-restore approach as
    // PlayerRagdoll's stun camera (including turning the owner's own skin
    // layer back on in the culling mask, since the FPS camera normally
    // hides your own body), just static instead of chasing a ragdoll.
    //
    // Local player only -- InventoryScreenUI calls Show()/Hide().
    public class InventoryCameraRig : MonoBehaviour
    {
        // In front of the character (local +Z is forward), roughly head
        // height, pulled back a couple of metres.
        [SerializeField] private Vector3 frontOffset = new(0f, 1.6f, 2.2f);
        [SerializeField] private float lookAtHeight = 1.3f;

        private Camera playerCamera;
        private Transform cameraTransform;
        private PlayerSkinSpawner skinSpawner;

        private Transform originalParent;
        private Vector3 originalLocalPosition;
        private Quaternion originalLocalRotation;
        private int originalCullingMask;
        private bool showing;

        private void Awake()
        {
            playerCamera = GetComponentInChildren<Camera>(true);
            if (playerCamera != null) cameraTransform = playerCamera.transform;
            skinSpawner = GetComponent<PlayerSkinSpawner>();
        }

        public void Show()
        {
            if (showing || cameraTransform == null) return;
            showing = true;

            originalParent = cameraTransform.parent;
            originalLocalPosition = cameraTransform.localPosition;
            originalLocalRotation = cameraTransform.localRotation;

            cameraTransform.SetParent(null, true);
            cameraTransform.position = transform.position + transform.rotation * frontOffset;
            cameraTransform.rotation = Quaternion.LookRotation(
                (transform.position + Vector3.up * lookAtHeight - cameraTransform.position).normalized, Vector3.up);

            if (playerCamera != null && skinSpawner != null)
            {
                originalCullingMask = playerCamera.cullingMask;
                playerCamera.cullingMask |= skinSpawner.SkinLayer.value; // show my own body
            }
        }

        public void Hide()
        {
            if (!showing || cameraTransform == null) return;
            showing = false;

            cameraTransform.SetParent(originalParent, false);
            cameraTransform.localPosition = originalLocalPosition;
            cameraTransform.localRotation = originalLocalRotation;

            if (playerCamera != null) playerCamera.cullingMask = originalCullingMask;
        }
    }
}
