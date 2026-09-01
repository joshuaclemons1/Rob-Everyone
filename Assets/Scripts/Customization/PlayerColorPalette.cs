using System.Collections.Generic;
using UnityEngine;

namespace RobEveryone.Customization
{
    // A fixed set of preset colors players choose their Body color from.
    // Create via Assets > Create > Rob Everyone > Player Color Palette.
    [CreateAssetMenu(menuName = "Rob Everyone/Player Color Palette")]
    public class PlayerColorPalette : ScriptableObject
    {
        [SerializeField] private Color[] colors = new Color[8];

        public IReadOnlyList<Color> Colors => colors;
    }
}
