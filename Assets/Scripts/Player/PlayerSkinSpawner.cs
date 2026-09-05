using RobEveryone.Customization;
using UnityEngine;

namespace RobEveryone.Player
{
    // Instantiates whichever skin/color the player picked in the main
    // menu (PlayerCosmeticSelection) as a child of this Transform at
    // gameplay start -- reads the same PlayerSkinRoster/PlayerColorPalette
    // assets CustomizationUI's menu preview uses, so there's one shared
    // list rather than gameplay hardcoding a specific character. This is
    // also the seam a future networked spawn hooks into: a remote player's
    // spawn would read the *owning* player's synced skin/color choice and
    // call the same instantiate-plus-colorize logic, rather than needing a
    // different code path.
    public class PlayerSkinSpawner : MonoBehaviour
    {
        [SerializeField] private PlayerSkinRoster skinRoster;
        [SerializeField] private PlayerColorPalette palette;
        [SerializeField] private LayerMask skinLayer;

        public GameObject SkinInstance { get; private set; }
        // PlayerRagdoll needs this to toggle PlayerCamera's Culling Mask
        // during the stun -- kept as the single source of truth here
        // rather than a second, separately-configured field there.
        public LayerMask SkinLayer => skinLayer;

        private void Awake()
        {
            if (skinRoster == null || skinRoster.Count == 0) return;

            GameObject prefab = skinRoster.GetSkin(PlayerCosmeticSelection.SkinIndex);
            if (prefab == null) return;

            SkinInstance = Instantiate(prefab, transform.position, transform.rotation, transform);
            SetLayerRecursively(SkinInstance.transform);

            PlayerColorizer colorizer = SkinInstance.GetComponent<PlayerColorizer>();
            if (colorizer == null) colorizer = SkinInstance.AddComponent<PlayerColorizer>();

            if (palette != null && palette.Colors.Count > 0)
            {
                int colorIndex = Mathf.Clamp(PlayerCosmeticSelection.ColorIndex, 0, palette.Colors.Count - 1);
                colorizer.ApplyBodyColor(palette.Colors[colorIndex]);
            }
        }

        // Everything on the skin goes on a dedicated layer (assign it to
        // whatever layer your own PlayerCamera excludes from its Culling
        // Mask) -- that's what makes "others see your body, you don't see
        // your own" work, instead of SetActive(false), which would hide it
        // from every camera, not just your own.
        private void SetLayerRecursively(Transform root)
        {
            int layer = LayerMaskToLayer(skinLayer);
            if (layer < 0) return;

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = layer;
            }
        }

        private static int LayerMaskToLayer(LayerMask mask)
        {
            int value = mask.value;
            for (int i = 0; i < 32; i++)
            {
                if ((value & (1 << i)) != 0) return i;
            }
            return -1;
        }
    }
}
