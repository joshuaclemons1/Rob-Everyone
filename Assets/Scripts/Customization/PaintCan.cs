using RobEveryone.Interaction;
using RobEveryone.Player;
using UnityEngine;

namespace RobEveryone.Customization
{
    // Issue #52 (Phase 2): a Lobby paint can -- walk up, press E, your
    // body color switches to this can's color. Always available, no
    // unlock gating (colors aren't rationed the way skins are -- see
    // SkinOfferPedestal for that). Same interaction shape as the Pawn
    // Shop's ShopShelfItem: plain MonoBehaviour + IInteractable, and
    // Interact() runs server-side via the standard Interactor flow, so
    // this can just call straight into PlayerSkinSpawner's server-side
    // swap method with no round trip of its own needed.
    [RequireComponent(typeof(Collider))]
    public class PaintCan : MonoBehaviour, IInteractable
    {
        [SerializeField] private int colorIndex;

        public string InteractionPrompt => "Change color";
        public bool CanInteract => true;

        public void Interact(GameObject interactor)
        {
            PlayerSkinSpawner spawner = interactor.GetComponent<PlayerSkinSpawner>();
            spawner?.ServerSwapCosmetics(null, colorIndex);
        }
    }
}
