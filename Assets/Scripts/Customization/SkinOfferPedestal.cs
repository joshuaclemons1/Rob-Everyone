using Mirror;
using RobEveryone.Interaction;
using RobEveryone.Player;
using UnityEngine;

namespace RobEveryone.Customization
{
    // Issue #52 (Phase 2): one pedestal in the Lobby's customization
    // building, displaying a randomly-rolled skin (assigned once per
    // Lobby load by SkinOfferManager, not rolled by this pedestal
    // itself -- see that class for why several pedestals need a shared
    // coordinator instead of each rolling independently) that any
    // player can walk up to and unlock into their own persistent
    // collection. Unlocking does NOT equip it -- see
    // MirrorSkinCycleButton for the actual "wear this" step, which only
    // ever offers skins the player has already unlocked somewhere.
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(NetworkIdentity))]
    public class SkinOfferPedestal : NetworkBehaviour, IInteractable
    {
        [SerializeField] private PlayerSkinRoster skinRoster;
        // Where the display model stands -- defaults to this object's
        // own Transform if left unset, but a separate child anchor lets
        // the pedestal's own collider/base be sized independently of
        // exactly where a ~2m-tall character model's feet need to land.
        [SerializeField] private Transform previewAnchor;

        [SyncVar(hook = nameof(OnOfferedSkinChanged))]
        private int offeredSkinIndex = -1;

        private GameObject previewInstance;

        public string InteractionPrompt =>
            offeredSkinIndex < 0 ? "" : $"Unlock {SkinDisplayName()}";

        // Deliberately not gated on offeredSkinIndex >= 0 -- same
        // reasoning as ShopShelfItem's own CanInteract comment: showing
        // no prompt at all (rather than one that reads oddly) while the
        // offer briefly hasn't synced in yet would look like a broken
        // pedestal instead of a loading one. In practice the SyncVar's
        // initial value arrives essentially immediately after spawn, so
        // this window is not really visible.
        public bool CanInteract => true;

        // Called by SkinOfferManager once per Lobby load -- this
        // pedestal never rolls for itself.
        [Server]
        public void ServerAssignOfferedSkin(int skinIndex)
        {
            offeredSkinIndex = skinIndex;
        }

        public void Interact(GameObject interactor)
        {
            if (offeredSkinIndex < 0) return;

            PlayerSkinSpawner spawner = interactor.GetComponent<PlayerSkinSpawner>();
            spawner?.ServerNotifySkinUnlocked(offeredSkinIndex);
        }

        // Fires on every client, including a late joiner (who gets the
        // then-current offer immediately on spawn, no separate catch-up
        // needed) -- spawns a plain display model standing on the
        // pedestal. Visual only, not the full PlayerSkinSpawner
        // machinery (no Animator/networking setup needed for something
        // nobody actually controls) -- just enough to see what's on
        // offer.
        private void OnOfferedSkinChanged(int _, int newValue)
        {
            if (previewInstance != null)
            {
                Destroy(previewInstance);
                previewInstance = null;
            }

            if (newValue < 0 || skinRoster == null) return;

            GameObject prefab = skinRoster.GetSkin(newValue);
            if (prefab == null) return;

            Transform anchor = previewAnchor != null ? previewAnchor : transform;
            previewInstance = Instantiate(prefab, anchor.position, anchor.rotation, anchor);
        }

        private string SkinDisplayName()
        {
            if (skinRoster == null) return "skin";
            GameObject prefab = skinRoster.GetSkin(offeredSkinIndex);
            return prefab != null ? prefab.name : "skin";
        }
    }
}
