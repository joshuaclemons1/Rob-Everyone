using RobEveryone.Player;
using UnityEngine;

namespace RobEveryone.Inventory
{
    // Knows the framing for the Tab / steal screen -- a wide, static
    // front-facing shot of the character -- and hands it to the shared
    // PlayerCameraRig, which does the actual smooth blend in/out.
    // InventoryScreenUI calls Show()/Hide().
    [RequireComponent(typeof(PlayerCameraRig))]
    public class InventoryCameraRig : MonoBehaviour
    {
        // In front of the character (local +Z is forward). Wide enough to
        // frame the whole character with room around them -- pull Z out
        // further for more of the scene, or bump fieldOfView instead if
        // there isn't clear space behind the player.
        [SerializeField] private Vector3 frontOffset = new(0f, 1.7f, 6.0f);
        [SerializeField] private float lookAtHeight = 1.0f;
        [SerializeField] private float fieldOfView = 62f; // <= 0 leaves the camera's own FOV
        [SerializeField] private float blendDuration = 0.35f;

        private PlayerCameraRig rig;

        // True while the camera is mid-blend either way -- InventoryScreenUI
        // holds the menu "closing" (input still parked) until this clears
        // so the first-person controller doesn't fight the blend-back.
        public bool Transitioning => rig != null && rig.IsActive;

        private void Awake() => rig = GetComponent<PlayerCameraRig>();

        public void Show()
        {
            if (rig == null) return;
            // Following (not a static pose) so the shot stays framed on
            // the character while they walk around with the screen open.
            rig.CutToFollowing(FramePose, chaseSpeed: 0f, showOwnSkin: true, blendDuration, fieldOfView);
        }

        private (Vector3 pos, Vector3 lookAt) FramePose()
        {
            Vector3 pos = transform.position + transform.rotation * frontOffset;
            Vector3 lookAt = transform.position + Vector3.up * lookAtHeight;
            return (pos, lookAt);
        }

        public void Hide()
        {
            if (rig != null) rig.Return(blendDuration);
        }
    }
}
