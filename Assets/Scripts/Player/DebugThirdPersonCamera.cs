using RobEveryone.Input;
using UnityEngine;

namespace RobEveryone.Player
{
    // TEMPORARY debug tool -- lets you see your own character's
    // animations play without needing multiplayer (the real, eventual way
    // another player would see them). Press T to toggle between the
    // normal first-person camera and a third-person chase camera. Uses a
    // *separate* Camera rather than repositioning the first-person one,
    // so it never fights FirstPersonController's own mouse-look code for
    // control of the same Transform -- movement/look keep working
    // normally in both modes, only which camera renders changes. Remove
    // this once animations are confirmed working and/or real
    // multiplayer/spectator visibility exists.
    public class DebugThirdPersonCamera : MonoBehaviour
    {
        [SerializeField] private Camera firstPersonCamera;
        [SerializeField] private Camera thirdPersonCamera;
        [SerializeField] private Vector3 offset = new(0f, 2.5f, -5f);
        [SerializeField] private float lookAtHeightOffset = 1f;
        [SerializeField] private float followSpeed = 8f;

        private bool isThirdPerson;

        private void Start()
        {
            if (thirdPersonCamera != null) thirdPersonCamera.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (InputManager.Gameplay.DebugSpectate.WasPressedThisFrame())
            {
                Toggle();
            }

            if (isThirdPerson) UpdateThirdPersonPosition();
        }

        private void Toggle()
        {
            isThirdPerson = !isThirdPerson;

            if (firstPersonCamera != null) firstPersonCamera.gameObject.SetActive(!isThirdPerson);
            if (thirdPersonCamera != null) thirdPersonCamera.gameObject.SetActive(isThirdPerson);
        }

        private void UpdateThirdPersonPosition()
        {
            if (thirdPersonCamera == null) return;

            Vector3 desiredPosition = transform.position + transform.TransformDirection(offset);
            thirdPersonCamera.transform.position = Vector3.Lerp(thirdPersonCamera.transform.position, desiredPosition, followSpeed * Time.deltaTime);

            Vector3 lookTarget = transform.position + Vector3.up * lookAtHeightOffset;
            thirdPersonCamera.transform.rotation = Quaternion.LookRotation((lookTarget - thirdPersonCamera.transform.position).normalized, Vector3.up);
        }
    }
}
