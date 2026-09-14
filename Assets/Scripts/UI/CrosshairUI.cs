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
        private ExitCarState localExitCar;

        private void Update()
        {
            if (interactor == null)
            {
                if (NetworkClient.localPlayer == null) return;
                interactor = NetworkClient.localPlayer.GetComponent<Interactor>();
                localJail = NetworkClient.localPlayer.GetComponent<JailState>();
                localSpectator = NetworkClient.localPlayer.GetComponent<SpectatorController>();
                localExitCar = NetworkClient.localPlayer.GetComponent<ExitCarState>();
                if (interactor == null) return;
            }

            bool jailed = localJail != null && localJail.IsJailed;
            bool spectating = localSpectator != null && localSpectator.IsSpectating;
            bool waitingInCar = localExitCar != null && localExitCar.IsWaiting;
            // Resolved (rode out their own carWaitDuration) but still
            // physically frozen/seated until the whole group is done --
            // confirmed bug: without this, once IsWaiting clears the
            // prompt fell through to the normal per-target branch below,
            // and since ExitCarFrozen leaves them still looking straight
            // at the exit point, it showed the exit point's own "Press E
            // to get away!" prompt again -- misleading, since EnterCar
            // now refuses to re-trigger (see ExitCarState.HasExtracted's
            // own comment) and the player genuinely can't act at all
            // right now.
            bool extractedInCar = localExitCar != null && localExitCar.HasExtracted;
            // While jailed or seated (waiting OR already extracted) at
            // the exit, FirstPersonController's entire look/move loop is
            // frozen and there's nothing meaningful left to aim at --
            // these lines show their own standing hint instead of the
            // normal per-target prompt.
            bool hasTarget = !jailed && !waitingInCar && !extractedInCar && interactor.CurrentTarget != null;

            if (interactHint != null) interactHint.SetActive(hasTarget);

            if (promptText != null)
            {
                promptText.text = spectating
                    ? "[T] Stop spectating   Click to switch"
                    : jailed
                        ? "Press T to spectate"
                        : waitingInCar
                            ? "Press E to exit the car"
                            : extractedInCar
                                ? "Waiting for the rest of your crew..."
                                : hasTarget ? $"[E] {interactor.CurrentTarget.InteractionPrompt}" : "";
                promptText.enabled = jailed || waitingInCar || extractedInCar || hasTarget;
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
