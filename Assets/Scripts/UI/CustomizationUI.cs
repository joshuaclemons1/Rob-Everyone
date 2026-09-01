using System.Collections.Generic;
using RobEveryone.Customization;
using UnityEngine;
using UnityEngine.UI;

namespace RobEveryone.UI
{
    // Drives the character customization screen: Next/Previous skin
    // cycling and a color swatch picker (built from a PlayerColorPalette),
    // both writing to PlayerCosmeticSelection, plus a live 3D preview
    // instance that updates immediately on either change.
    public class CustomizationUI : MonoBehaviour
    {
        [SerializeField] private GameObject[] skinPrefabs;
        [SerializeField] private PlayerColorPalette palette;
        [SerializeField] private Transform previewSpawnPoint;
        [SerializeField] private Transform swatchContainer;
        [SerializeField] private Button swatchButtonTemplate;

        private GameObject previewInstance;
        private PlayerColorizer previewColorizer;

        private void OnEnable()
        {
            BuildSwatches();
            Refresh();
        }

        public void NextSkin() => ChangeSkin(1);
        public void PreviousSkin() => ChangeSkin(-1);

        private void ChangeSkin(int delta)
        {
            if (skinPrefabs.Length == 0) return;

            int next = (PlayerCosmeticSelection.SkinIndex + delta + skinPrefabs.Length) % skinPrefabs.Length;
            PlayerCosmeticSelection.SkinIndex = next;
            Refresh();
        }

        private void BuildSwatches()
        {
            if (palette == null || swatchContainer == null || swatchButtonTemplate == null) return;
            if (swatchContainer.childCount > 0) return; // already built from a previous OnEnable

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
        }

        private void SelectColor(int index)
        {
            PlayerCosmeticSelection.ColorIndex = index;
            Refresh();
        }

        private void Refresh()
        {
            SpawnPreview();
            ApplyColor();
        }

        private void SpawnPreview()
        {
            if (skinPrefabs.Length == 0 || previewSpawnPoint == null) return;

            int skinIndex = Mathf.Clamp(PlayerCosmeticSelection.SkinIndex, 0, skinPrefabs.Length - 1);

            if (previewInstance != null) Destroy(previewInstance);

            previewInstance = Instantiate(skinPrefabs[skinIndex], previewSpawnPoint.position, previewSpawnPoint.rotation, previewSpawnPoint);
            previewColorizer = previewInstance.GetComponent<PlayerColorizer>();
            if (previewColorizer == null) previewColorizer = previewInstance.AddComponent<PlayerColorizer>();
        }

        private void ApplyColor()
        {
            if (previewColorizer == null || palette == null) return;

            IReadOnlyList<Color> colors = palette.Colors;
            if (colors.Count == 0) return;

            int colorIndex = Mathf.Clamp(PlayerCosmeticSelection.ColorIndex, 0, colors.Count - 1);
            previewColorizer.ApplyBodyColor(colors[colorIndex]);
        }
    }
}
