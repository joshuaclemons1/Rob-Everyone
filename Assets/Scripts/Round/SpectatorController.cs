using System.Collections.Generic;
using Mirror;
using RobEveryone.Input;
using RobEveryone.Inventory;
using RobEveryone.Player;
using UnityEngine;

namespace RobEveryone.Round
{
    // While jailed there's otherwise no way to look around at all --
    // FirstPersonController's whole Update() halts under IsFrozen, mouse
    // movement included -- so pressing T here pops the jailed player into
    // a simple third-person "chase cam" trailing another connected
    // player, left-click cycling which one. Purely a local rendering
    // choice for this one client -- nobody else's view or the actual
    // player Transform ever changes, so none of this needs to be synced.
    [RequireComponent(typeof(JailState))]
    [RequireComponent(typeof(FirstPersonController))]
    public class SpectatorController : NetworkBehaviour
    {
        // The normal first-person camera/listener (drag the same objects
        // FirstPersonController's own cameraTransform points at) -- these
        // get disabled while spectating and re-enabled once it ends.
        [SerializeField] private Camera fpsCamera;
        [SerializeField] private AudioListener fpsListener;
        // A separate camera/listener child, disabled by default, used
        // only while spectating -- kept distinct from the FPS camera
        // rather than repurposing it, since FirstPersonController's own
        // HandleLook/HandleCrouch write to that Transform in local space
        // every frame it's not frozen.
        [SerializeField] private Camera spectatorCamera;
        [SerializeField] private AudioListener spectatorListener;
        [SerializeField] private Vector3 chaseOffset = new Vector3(0f, 2.5f, -4.5f);
        [SerializeField] private float followLerp = 6f;

        private JailState jail;
        private FirstPersonController fpc;
        private bool isSpectating;
        private int spectateIndex = -1;
        private Transform spectateTarget;

        public bool IsSpectating => isSpectating;

        private void Awake()
        {
            jail = GetComponent<JailState>();
            fpc = GetComponent<FirstPersonController>();
        }

        private void Update()
        {
            if (!isOwned) return;

            // Being un-jailed (rescued, self-bailed, or the round simply
            // ending) always forces back to the normal first-person view.
            if (!jail.IsJailed)
            {
                if (isSpectating) StopSpectating();
                return;
            }

            if (InputManager.Gameplay.DebugSpectate.WasPressedThisFrame())
            {
                if (isSpectating) StopSpectating();
                else StartSpectating();
            }

            if (!isSpectating) return;

            if (InputManager.Gameplay.PrimaryAction.WasPressedThisFrame())
            {
                CycleTarget();
            }

            if (spectateTarget == null || spectatorCamera == null) return;

            Vector3 desired = spectateTarget.position + spectateTarget.TransformDirection(chaseOffset);
            Transform camTransform = spectatorCamera.transform;
            camTransform.position = Vector3.Lerp(camTransform.position, desired, followLerp * Time.deltaTime);
            camTransform.rotation = Quaternion.LookRotation((spectateTarget.position + Vector3.up * 1.5f) - camTransform.position);
        }

        private void StartSpectating()
        {
            isSpectating = true;
            spectateIndex = -1;
            CycleTarget();

            if (fpc != null) fpc.SpectatingFrozen = true;
            if (fpsCamera != null) fpsCamera.enabled = false;
            if (fpsListener != null) fpsListener.enabled = false;
            if (spectatorCamera != null) spectatorCamera.enabled = true;
            if (spectatorListener != null) spectatorListener.enabled = true;
        }

        private void StopSpectating()
        {
            isSpectating = false;
            spectateTarget = null;

            if (spectatorCamera != null) spectatorCamera.enabled = false;
            if (spectatorListener != null) spectatorListener.enabled = false;
            if (fpsCamera != null) fpsCamera.enabled = true;
            if (fpsListener != null) fpsListener.enabled = true;
            if (fpc != null) fpc.SpectatingFrozen = false;
        }

        // FindObjectsByType, not PlayerInventory.AllPlayers -- that list
        // is server-only, and this runs on whichever client owns this
        // jailed player, which isn't necessarily the host (same reasoning
        // as Interactor.FindRagdolledPlayerNearby).
        private void CycleTarget()
        {
            PlayerInventory[] all = FindObjectsByType<PlayerInventory>();
            List<Transform> candidates = new();

            foreach (PlayerInventory player in all)
            {
                if (player.gameObject == gameObject) continue;
                JailState theirJail = player.GetComponent<JailState>();
                if (theirJail != null && theirJail.IsJailed) continue; // only free players are worth watching
                candidates.Add(player.transform);
            }

            if (candidates.Count == 0)
            {
                spectateTarget = null;
                return;
            }

            spectateIndex = (spectateIndex + 1) % candidates.Count;
            spectateTarget = candidates[spectateIndex];
        }
    }
}
