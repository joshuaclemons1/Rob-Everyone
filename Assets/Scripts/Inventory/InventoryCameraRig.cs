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
        // frame roughly head-to-knee with room around them.
        [SerializeField] private Vector3 frontOffset = new(0f, 1.5f, 4.0f);
        [SerializeField] private float lookAtHeight = 1.1f;
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
            Vector3 pos = transform.position + transform.rotation * frontOffset;
            Vector3 lookAt = transform.position + Vector3.up * lookAtHeight;
            rig.CutTo(pos, lookAt, showOwnSkin: true, blendDuration);
        }

        public void Hide()
        {
            if (rig != null) rig.Return(blendDuration);
        }
    }
}
