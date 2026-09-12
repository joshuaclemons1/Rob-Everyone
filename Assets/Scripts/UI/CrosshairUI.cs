using Mirror;
using RobEveryone.Interaction;
using RobEveryone.Round;
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
        // Second line under promptText -- only a handful of
        // interactables (IInteractableWarning) ever have anything to
        // put here (e.g. a shop shelf you can't currently afford), so
        // this stays hidden the rest of the time. Color this red in the
        // Inspector; the script only ever touches text/enabled, same as
        // promptText.
        [SerializeField] private TextMeshProUGUI warningText;

        private Interactor interactor;
        private JailState localJail;
        private SpectatorController localSpectator;

        private void Update()
        {
            if (interactor == null)
            {
                if (NetworkClient.localPlayer == null) return;
                interactor = NetworkClient.localPlayer.GetComponent<Interactor>();
                localJail = NetworkClient.localPlayer.GetComponent<JailState>();
                localSpectator = NetworkClient.localPlayer.GetComponent<SpectatorController>();
                if (interactor == null) return;
            }

            bool jailed = localJail != null && localJail.IsJailed;
            bool spectating = localSpectator != null && localSpectator.IsSpectating;
            // While jailed, FirstPersonController's entire look/move loop
            // is frozen and there's nothing meaningful left to aim at --
            // this line shows the spectate hint instead of the normal
            // per-target prompt.
            bool hasTarget = !jailed && interactor.CurrentTarget != null;

            if (interactHint != null) interactHint.SetActive(hasTarget);

            if (promptText != null)
            {
                promptText.text = spectating
                    ? "[T] Stop spectating   Click to switch"
                    : jailed
                        ? "Press T to spectate"
                        : hasTarget ? $"[E] {interactor.CurrentTarget.InteractionPrompt}" : "";
                promptText.enabled = jailed || hasTarget;
            }

            if (warningText != null)
            {
                string warning = hasTarget ? (interactor.CurrentTarget as IInteractableWarning)?.WarningText : null;
                bool showWarning = !string.IsNullOrEmpty(warning);
                warningText.text = showWarning ? warning : "";
                warningText.enabled = showWarning;
            }
        }
    }
}
