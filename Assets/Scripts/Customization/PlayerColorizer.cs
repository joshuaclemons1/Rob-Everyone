using UnityEngine;

namespace RobEveryone.Customization
{
    // Applies a color to whichever material slot is named "Body"
    // (case-insensitive, so "Body", "Body (Instance)", etc. all match)
    // across every Renderer on this object/its children -- via
    // MaterialPropertyBlock, so the shared Material asset is never
    // touched and every skin instance can carry its own color. The
    // "Head" material is deliberately left alone (Body-only palette).
    public class PlayerColorizer : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public void ApplyBodyColor(Color color)
        {
            MaterialPropertyBlock block = new MaterialPropertyBlock();

            foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null || !materials[i].name.ToLowerInvariant().Contains("body")) continue;

                    renderer.GetPropertyBlock(block, i);
                    block.SetColor(BaseColorId, color);
                    renderer.SetPropertyBlock(block, i);
                }
            }
        }
    }
}
