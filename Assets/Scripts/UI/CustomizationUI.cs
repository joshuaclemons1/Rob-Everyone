using System.Collections.Generic;
using RobEveryone.Customization;
using UnityEngine;
using UnityEngine.UI;

namespace RobEveryone.UI
{
    // Drives the character customization screen: Next/Previous skin
    // cycling and a color swatch picker (built from a PlayerColorPalette),
    // both writing to PlayerCosmeticSelection.
    //
    // Issue #39 (2/3): no longer owns the live 3D preview instance --
    // that moved to MenuCharacterPreview, which persists across every
    // Main Menu panel instead of only existing while this specific
    // screen is open. This component just needs to write the selection;
    // MenuCharacterPreview already listens for PlayerCosmeticSelection.
    // OnChanged on its own and reacts independently.
    public class CustomizationUI : MonoBehaviour
    {
        [SerializeField] private PlayerSkinRoster skinRoster;
        [SerializeField] private PlayerColorPalette palette;
        [SerializeField] private Transform swatchContainer;
        [SerializeField] private Button swatchButtonTemplate;

        private bool swatchesBuilt;

        private void OnEnable()
        {
            BuildSwatches();
        }

        public void NextSkin() => ChangeSkin(1);
        public void PreviousSkin() => ChangeSkin(-1);

        private void ChangeSkin(int delta)
        {
            if (skinRoster == null || skinRoster.Count == 0) return;

            int next = (PlayerCosmeticSelection.SkinIndex + delta + skinRoster.Count) % skinRoster.Count;
            PlayerCosmeticSelection.SkinIndex = next;
        }

        private void BuildSwatches()
        {
            if (swatchesBuilt) return;
            if (palette == null || swatchContainer == null || swatchButtonTemplate == null) return;

            IReadOnlyList<Color> colors = palette.Colors;
            for (int i = 0; i < colors.Count; i++)
            {
                int index = i; // capture for the closure below
                Button swatch = Instantiate(swatchButtonTemplate, swatchContainer);
                swatch.gameObject.SetActive(true);

                Image image = swatch.GetComponent<Image>();
                if (image != null) image.color = colors[i];

                swatch.onClick.AddListener(() => SelectColor(index));
            }

            swatchButtonTemplate.gameObject.SetActive(false);
            swatchesBuilt = true;
        }

        private void SelectColor(int index)
        {
            PlayerCosmeticSelection.ColorIndex = index;
        }
    }
}
