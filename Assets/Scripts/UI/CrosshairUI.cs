using Mirror;
using RobEveryone.Interaction;
using TMPro;
using UnityEngine;

namespace RobEveryone.UI
{
    // Toggles a separate "interact available" hint icon next to the
    // crosshair dot -- the dot itself never changes sprite, size, or
    // position, so there's nothing to glitch when interactability
    // changes. Drag the hint icon's GameObject into `interactHint`.
    // Also shows the current target's own IInteractable.InteractionPrompt
    // text (prefixed with the key) next to it -- with several different
    // actions now possible on the same look-at (rob vs. carry a stunned
    // rival), the icon alone no longer says which one E will do.
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
        [SerializeField] private TextMeshProUGUI promptText;

        private Interactor interactor;

        private void Update()
        {
            if (interactor == null)
            {
                if (NetworkClient.localPlayer == null) return;
                interactor = NetworkClient.localPlayer.GetComponent<Interactor>();
                if (interactor == null) return;
            }

            bool hasTarget = interactor.CurrentTarget != null;

            if (interactHint != null) interactHint.SetActive(hasTarget);

            if (promptText != null)
            {
                promptText.text = hasTarget ? $"[E] {interactor.CurrentTarget.InteractionPrompt}" : "";
                promptText.enabled = hasTarget;
            }
        }
    }
}
