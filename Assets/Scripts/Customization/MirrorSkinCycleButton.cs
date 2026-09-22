using RobEveryone.Interaction;
using RobEveryone.Player;
using UnityEngine;

namespace RobEveryone.Customization
{
    // Issue #52 (Phase 2): one of two small interactables meant to flank
    // the Lobby's mirror (place a second instance with forward=false for
    // the other direction) -- walk up, press E, cycle to the next/
    // previous skin in the player's own unlocked collection
    // (PlayerSkinUnlocks), mirroring the old Main Menu Customization
    // screen's own Next/Previous buttons but scoped to what's actually
    // been unlocked (via SkinOfferPedestal) instead of the full roster.
    //
    // Interact() runs server-side via the standard Interactor flow, but
    // "which skin is next" depends on data only the interactor's own
    // client has -- see PlayerSkinSpawner.ServerRequestSkinCycle's own
    // comment for the full round trip this kicks off.
    [RequireComponent(typeof(Collider))]
    public class MirrorSkinCycleButton : MonoBehaviour, IInteractable
    {
        [SerializeField] private bool forward = true;

        public string InteractionPrompt => forward ? "Next skin" : "Previous skin";
        public bool CanInteract => true;

        public void Interact(GameObject interactor)
        {
            PlayerSkinSpawner spawner = interactor.GetComponent<PlayerSkinSpawner>();
            spawner?.ServerRequestSkinCycle(forward);
        }
    }
}
