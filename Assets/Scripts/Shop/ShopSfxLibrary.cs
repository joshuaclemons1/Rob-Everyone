using UnityEngine;

namespace RobEveryone.Shop
{
    public enum ShopSfx { PurchaseSuccess, InsufficientFunds, ItemSold }

    // Shared Resources-loaded clip pools for shop/economy feedback, same
    // self-bootstrap shape as PickupSfxLibrary -- one asset, no per-shelf
    // or per-item wiring needed.
    //
    // Editor setup: Assets/Resources -> Create -> RobEveryone -> Shop Sfx
    // Library, name it exactly "ShopSfxLibrary", drop clips into each pool.
    [CreateAssetMenu(fileName = "ShopSfxLibrary", menuName = "RobEveryone/Shop Sfx Library")]
    public class ShopSfxLibrary : ScriptableObject
    {
        [SerializeField] private AudioClip[] purchaseSuccessClips;
        [SerializeField] private AudioClip[] insufficientFundsClips;
        [SerializeField] private AudioClip[] itemSoldClips;

        private static ShopSfxLibrary instance;
        private static bool loadAttempted;

        public static ShopSfxLibrary Instance
        {
            get
            {
                if (!loadAttempted)
                {
                    instance = Resources.Load<ShopSfxLibrary>("ShopSfxLibrary");
                    loadAttempted = true;
                }
                return instance;
            }
        }

        public AudioClip[] ClipsFor(ShopSfx sfx) => sfx switch
        {
            ShopSfx.PurchaseSuccess => purchaseSuccessClips,
            ShopSfx.InsufficientFunds => insufficientFundsClips,
            ShopSfx.ItemSold => itemSoldClips,
            _ => null,
        };
    }
}
