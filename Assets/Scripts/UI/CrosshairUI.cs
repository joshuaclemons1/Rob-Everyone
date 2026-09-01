using RobEveryone.Interaction;
using UnityEngine;
using UnityEngine.UI;

namespace RobEveryone.UI
{
    // Swaps the crosshair sprite based on whether Interactor is currently
    // looking at something interactable. Drag the Player's Interactor into
    // `interactor`, the crosshair's own Image into `crosshairImage`, and
    // the two sprites into `defaultSprite`/`interactSprite`.
    public class CrosshairUI : MonoBehaviour
    {
        [SerializeField] private Interactor interactor;
        [SerializeField] private Image crosshairImage;
        [SerializeField] private Sprite defaultSprite;
        [SerializeField] private Sprite interactSprite;

        private void Update()
        {
            if (interactor == null || crosshairImage == null) return;

            crosshairImage.sprite = interactor.CurrentTarget != null ? interactSprite : defaultSprite;
        }
    }
}
