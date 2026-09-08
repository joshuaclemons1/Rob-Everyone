using Mirror;
using RobEveryone.Interaction;
using UnityEngine;

namespace RobEveryone.UI
{
    // Toggles a separate "interact available" hint icon next to the
    // crosshair dot -- the dot itself never changes sprite, size, or
    // position, so there's nothing to glitch when interactability
    // changes. Drag the hint icon's GameObject into `interactHint`.
    //
    // Networking (Stage 4): `interactor` used to be a direct Inspector
    // drag onto the one hand-placed Player object -- that Player no
    // longer exists in the scene at edit time (Mirror spawns it at
    // runtime from a prefab), so this resolves *this client's own*
    // Interactor lazily instead, the same NetworkClient.localPlayer
    // pattern PlayerInventory.LocalPlayer uses.
    public class CrosshairUI : MonoBehaviour
    {
        [SerializeField] private GameObject interactHint;

        private Interactor interactor;

        private void Update()
        {
            if (interactor == null)
            {
                if (NetworkClient.localPlayer == null) return;
                interactor = NetworkClient.localPlayer.GetComponent<Interactor>();
                if (interactor == null) return;
            }

            if (interactHint == null) return;

            interactHint.SetActive(interactor.CurrentTarget != null);
        }
    }
}
