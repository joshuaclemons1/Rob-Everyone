using RobEveryone.Interaction;
using UnityEngine;

namespace RobEveryone.UI
{
    // Toggles a separate "interact available" hint icon next to the
    // crosshair dot -- the dot itself never changes sprite, size, or
    // position, so there's nothing to glitch when interactability
    // changes. Drag the Player's Interactor into `interactor` and the
    // hint icon's GameObject into `interactHint`.
    public class CrosshairUI : MonoBehaviour
    {
        [SerializeField] private Interactor interactor;
        [SerializeField] private GameObject interactHint;

        private void Update()
        {
            if (interactor == null || interactHint == null) return;

            interactHint.SetActive(interactor.CurrentTarget != null);
        }
    }
}
