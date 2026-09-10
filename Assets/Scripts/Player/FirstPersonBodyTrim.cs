using System;
using System.Collections.Generic;
using UnityEngine;

namespace RobEveryone.Player
{
    // Hides parts of the owner's own skin from their first-person camera
    // by scaling the relevant bones to zero:
    //
    //  - grounded: just the head, so the camera isn't clipping through it
    //    and so you can look down and see your own body (and, later, a
    //    held hotbar item in your hands);
    //  - airborne: the whole upper body too, because the jump animation's
    //    spring pushes the torso/shoulders up into the camera otherwise.
    //
    // Restores everything whenever PlayerCameraRig has the camera cut
    // away (the Tab / steal screen and the ragdoll stun both show a full
    // third-person view of you). Owner's copy only -- every other client
    // sees the complete model.
    //
    // Runs in LateUpdate, after the Animator writes its poses.
    public class FirstPersonBodyTrim : MonoBehaviour
    {
        private PlayerCameraRig cameraRig;
        private PlayerRagdoll ragdoll;
        private FirstPersonController fpc;

        private Transform[] groundedHide = Array.Empty<Transform>();
        private Transform[] airborneHide = Array.Empty<Transform>();
        private readonly Dictionary<Transform, Vector3> originalScale = new();

        private void Awake()
        {
            cameraRig = GetComponentInParent<PlayerCameraRig>();
            ragdoll = GetComponentInParent<PlayerRagdoll>();
            fpc = GetComponentInParent<FirstPersonController>();
        }

        public void Apply(Transform skinRoot, string[] groundedBoneNames, string[] airborneBoneNames)
        {
            groundedHide = Resolve(skinRoot, groundedBoneNames);
            airborneHide = Resolve(skinRoot, airborneBoneNames);

            foreach (Transform t in groundedHide) Remember(t);
            foreach (Transform t in airborneHide) Remember(t);

            if (groundedHide.Length == 0 && airborneHide.Length == 0)
            {
                Debug.LogWarning($"FirstPersonBodyTrim on '{name}': no bone names matched -- the body won't be trimmed. Check the skin's bone names.", this);
            }
        }

        private void Remember(Transform t)
        {
            if (t != null && !originalScale.ContainsKey(t)) originalScale[t] = t.localScale;
        }

        private static Transform[] Resolve(Transform root, string[] names)
        {
            if (root == null || names == null || names.Length == 0) return Array.Empty<Transform>();

            var matches = new List<Transform>();
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                foreach (string n in names)
                {
                    if (!string.IsNullOrEmpty(n) && string.Equals(t.name, n, StringComparison.OrdinalIgnoreCase))
                    {
                        matches.Add(t);
                        break;
                    }
                }
            }
            return matches.ToArray();
        }

        private void LateUpdate()
        {
            RestoreAll();

            // Camera cut away (inventory / steal screen, or the ragdoll
            // stun) -> the shot shows your whole body, leave it alone.
            bool cutAway = (cameraRig != null && cameraRig.IsActive) || (ragdoll != null && ragdoll.IsRagdolling);
            if (cutAway) return;

            // IsJumpPending catches the launch frame before isGrounded
            // has caught up, so the upper body is hidden the instant you
            // leave rather than a frame late (when it'd already be
            // clipping the camera).
            bool airborne = fpc != null && (!fpc.IsGrounded || fpc.IsJumpPending);
            Transform[] hide = airborne ? airborneHide : groundedHide;
            for (int i = 0; i < hide.Length; i++)
            {
                if (hide[i] != null) hide[i].localScale = Vector3.zero;
            }
        }

        private void RestoreAll()
        {
            foreach (KeyValuePair<Transform, Vector3> kv in originalScale)
            {
                if (kv.Key != null) kv.Key.localScale = kv.Value;
            }
        }
    }
}
