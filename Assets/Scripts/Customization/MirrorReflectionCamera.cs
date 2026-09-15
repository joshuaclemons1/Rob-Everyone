using Mirror;
using UnityEngine;

namespace RobEveryone.Customization
{
    // Issue #52 (Phase 4): drives the Lobby mirror's reflection camera --
    // a second Camera positioned/rotated every frame as the true optical
    // reflection of whichever camera the LOCAL player is currently
    // looking through, rendering to a RenderTexture displayed on the
    // mirror surface's own material. Client-side only, same "purely
    // local visual reaction to already-synced/local state, no new
    // networking needed" pattern PlayerRagdoll's bone physics and
    // HeldItemDisplay already use -- every client renders its own
    // reflection of its own local view independently; nothing here is
    // replicated or needs to be.
    //
    // No existing precedent in this project to build on -- HotbarSlotUI's
    // item previews are an isolated camera-on-a-stage, not a true
    // reflection across a plane. See docs/stages/
    // lobby-customization-building-editor-setup.md for the Editor side
    // of wiring this up (RenderTexture creation, the mirror surface's own
    // material, exactly where mirrorPlane needs to sit).
    public class MirrorReflectionCamera : MonoBehaviour
    {
        [SerializeField] private Camera reflectionCamera;
        // The mirror surface's own plane, in world space -- position is
        // any point on the mirror (its own Transform is fine), forward
        // is the direction the mirror faces (the direction a player
        // standing in front of it looks *into* it, not the direction the
        // glass's own backing points).
        [SerializeField] private Transform mirrorPlane;

        [SerializeField, Range(0f, 0.2f)] private float updateInterval = 0.05f;
        // Only actually renders while a viewer is this close and roughly
        // facing the mirror -- paying for a second full camera pass
        // every frame regardless of visibility isn't worth it, especially
        // with several players' clients all doing this at once in a
        // shared Lobby.
        [SerializeField] private float maxActiveDistance = 6f;
        [SerializeField, Range(0f, 180f)] private float maxActiveAngle = 100f;

        private Camera viewerCamera;
        private float nextUpdateTime;

        private void Update()
        {
            if (reflectionCamera == null || mirrorPlane == null) return;
            if (Time.time < nextUpdateTime) return;
            nextUpdateTime = Time.time + updateInterval;

            if (!TryResolveViewerCamera())
            {
                reflectionCamera.enabled = false;
                return;
            }

            Vector3 toViewer = viewerCamera.transform.position - mirrorPlane.position;
            float distance = toViewer.magnitude;
            float angle = distance > 0.0001f ? Vector3.Angle(mirrorPlane.forward, -toViewer.normalized) : 0f;

            bool shouldRender = distance <= maxActiveDistance && angle <= maxActiveAngle;
            reflectionCamera.enabled = shouldRender;
            if (!shouldRender) return;

            ApplyReflection();
        }

        // Re-resolved lazily rather than cached permanently, and
        // re-checked every call rather than trusted once found -- the
        // local player doesn't exist yet at Lobby scene load (Mirror
        // spawns it a moment after the scene finishes loading), and
        // which of a player's own cameras is actually active can change
        // over time (SpectatorController toggles fpsCamera/
        // spectatorCamera's own .enabled depending on jailed/spectating
        // state -- same convention this reads by, rather than hardcoding
        // a specific camera's name).
        private bool TryResolveViewerCamera()
        {
            if (viewerCamera != null && viewerCamera.enabled) return true;
            viewerCamera = null;

            if (NetworkClient.localPlayer == null) return false;

            foreach (Camera candidate in NetworkClient.localPlayer.GetComponentsInChildren<Camera>(true))
            {
                if (!candidate.enabled) continue;
                viewerCamera = candidate;
                return true;
            }

            return false;
        }

        // Standard planar-mirror technique: reflect the viewer camera's
        // position and facing across the mirror's own plane (point =
        // mirrorPlane.position, normal = mirrorPlane.forward). Doesn't
        // apply an oblique near-clip plane (the usual next refinement, to
        // stop geometry behind the mirror surface from reflecting into
        // view) -- deliberately left out of this first pass since getting
        // that projection-matrix math right needs to be verified visually,
        // not guessed; add it only if that specific artifact actually
        // shows up once this is in the Editor.
        private void ApplyReflection()
        {
            Vector3 planePoint = mirrorPlane.position;
            Vector3 planeNormal = mirrorPlane.forward;

            Vector3 viewerPos = viewerCamera.transform.position;
            float distance = Vector3.Dot(viewerPos - planePoint, planeNormal);
            reflectionCamera.transform.position = viewerPos - 2f * distance * planeNormal;

            Vector3 reflectedForward = Reflect(viewerCamera.transform.forward, planeNormal);
            Vector3 reflectedUp = Reflect(viewerCamera.transform.up, planeNormal);
            reflectionCamera.transform.rotation = Quaternion.LookRotation(reflectedForward, reflectedUp);

            reflectionCamera.fieldOfView = viewerCamera.fieldOfView;
            reflectionCamera.nearClipPlane = viewerCamera.nearClipPlane;
            reflectionCamera.farClipPlane = viewerCamera.farClipPlane;
        }

        private static Vector3 Reflect(Vector3 vector, Vector3 planeNormal) =>
            vector - 2f * Vector3.Dot(vector, planeNormal) * planeNormal;
    }
}
