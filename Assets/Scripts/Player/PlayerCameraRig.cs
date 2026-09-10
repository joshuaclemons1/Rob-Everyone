using System;
using UnityEngine;

namespace RobEveryone.Player
{
    // Owns the player's camera for any "cut away from first person"
    // moment -- the stun ragdoll cutaway (PlayerRagdoll) and the Tab /
    // steal inventory screen (InventoryCameraRig). One place for: detach
    // from the first-person rig, blend smoothly to a target pose, hold or
    // follow it, blend smoothly back, re-dock, and the "show my own body"
    // culling-mask toggle.
    //
    // Blends both ways (SmoothStep over `defaultBlend` seconds) so nothing
    // snaps. The blend-back tracks the dock's live world position each
    // frame, so it still lands correctly if the player teleported while
    // the camera was out (EndRagdoll's raycast-to-ground reposition).
    //
    // Local player only in practice -- remote players' cameras are
    // disabled and nothing calls this on them.
    public class PlayerCameraRig : MonoBehaviour
    {
        [SerializeField] private float defaultBlend = 0.35f;

        private Camera cam;
        private Transform camT;
        private PlayerSkinSpawner skinSpawner;

        private Transform dockParent;
        private Vector3 dockLocalPos;
        private Quaternion dockLocalRot;
        private int dockCullingMask;

        private enum Phase { Docked, BlendingIn, Held, BlendingOut }
        private Phase phase = Phase.Docked;

        private float t, rate;
        private Vector3 blendFromPos;
        private Quaternion blendFromRot;

        private Func<(Vector3 pos, Vector3 lookAt)> liveTarget; // null for a static cut
        private Vector3 staticPos;
        private Vector3 staticLookAt;
        private float followSpeed; // <= 0 = track the target exactly while Held

        public bool IsActive => phase != Phase.Docked;

        private void Awake()
        {
            cam = GetComponentInChildren<Camera>(true);
            if (cam != null) camT = cam.transform;
            skinSpawner = GetComponent<PlayerSkinSpawner>();
        }

        // Cut to a fixed world pose (facing `lookAt`) and hold it.
        public void CutTo(Vector3 worldPos, Vector3 lookAt, bool showOwnSkin, float blend = -1f)
        {
            liveTarget = null;
            staticPos = worldPos;
            staticLookAt = lookAt;
            BeginCut(showOwnSkin, blend);
        }

        // Cut to a target re-evaluated every frame -- after the entry
        // blend, chase it with `chaseSpeed` exponential lag (PlayerRagdoll's
        // follow-with-lag feel); pass <= 0 to track it exactly.
        public void CutToFollowing(Func<(Vector3 pos, Vector3 lookAt)> target, float chaseSpeed, bool showOwnSkin, float blend = -1f)
        {
            liveTarget = target;
            followSpeed = chaseSpeed;
            BeginCut(showOwnSkin, blend);
        }

        // Blend back to the docked first-person pose; re-parent + restore
        // the culling mask on arrival.
        public void Return(float blend = -1f)
        {
            if (camT == null || phase == Phase.Docked) return;
            blendFromPos = camT.position;
            blendFromRot = camT.rotation;
            t = 0f;
            rate = BlendRate(blend);
            phase = Phase.BlendingOut;
        }

        private void BeginCut(bool showOwnSkin, float blend)
        {
            if (camT == null) return;

            if (phase == Phase.Docked)
            {
                dockParent = camT.parent;
                dockLocalPos = camT.localPosition;
                dockLocalRot = camT.localRotation;
                camT.SetParent(null, true);

                if (cam != null)
                {
                    dockCullingMask = cam.cullingMask;
                    if (showOwnSkin && skinSpawner != null) cam.cullingMask |= skinSpawner.SkinLayer.value;
                }
            }

            blendFromPos = camT.position;
            blendFromRot = camT.rotation;
            t = 0f;
            rate = BlendRate(blend);
            phase = Phase.BlendingIn;
        }

        private float BlendRate(float blend) => 1f / Mathf.Max(0.01f, blend < 0f ? defaultBlend : blend);

        private void LateUpdate()
        {
            if (camT == null || phase == Phase.Docked) return;

            if (phase == Phase.Held)
            {
                (Vector3 pos, Vector3 look) = SampleTarget();
                camT.position = followSpeed > 0f
                    ? Vector3.Lerp(camT.position, pos, followSpeed * Time.deltaTime)
                    : pos;
                FaceToward(look);
                return;
            }

            t += Time.deltaTime * rate;
            float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));

            if (phase == Phase.BlendingIn)
            {
                (Vector3 pos, Vector3 look) = SampleTarget();
                camT.position = Vector3.Lerp(blendFromPos, pos, e);
                camT.rotation = Quaternion.Slerp(blendFromRot, FacingRotation(pos, look), e);
                if (t >= 1f) phase = Phase.Held;
            }
            else // BlendingOut -- track the live dock pose
            {
                Vector3 dockPos = dockParent != null ? dockParent.TransformPoint(dockLocalPos) : dockLocalPos;
                Quaternion dockRot = dockParent != null ? dockParent.rotation * dockLocalRot : dockLocalRot;
                camT.position = Vector3.Lerp(blendFromPos, dockPos, e);
                camT.rotation = Quaternion.Slerp(blendFromRot, dockRot, e);

                if (t >= 1f)
                {
                    camT.SetParent(dockParent, false);
                    camT.localPosition = dockLocalPos;
                    camT.localRotation = dockLocalRot;
                    if (cam != null) cam.cullingMask = dockCullingMask;
                    phase = Phase.Docked;
                    liveTarget = null;
                }
            }
        }

        private (Vector3 pos, Vector3 look) SampleTarget()
        {
            if (liveTarget != null) return liveTarget();
            return (staticPos, staticLookAt);
        }

        private void FaceToward(Vector3 lookAt)
        {
            Vector3 d = lookAt - camT.position;
            if (d.sqrMagnitude > 0.0001f) camT.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
        }

        private static Quaternion FacingRotation(Vector3 from, Vector3 lookAt)
        {
            Vector3 d = lookAt - from;
            return Quaternion.LookRotation(d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.forward, Vector3.up);
        }
    }
}
