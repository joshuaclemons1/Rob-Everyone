using System.Collections.Generic;
using UnityEngine;

namespace RobEveryone.Customization
{
    // Applies a color to whichever material slot is named "Skin"
    // (case-insensitive, so "Skin", "Skin (Instance)", etc. all match)
    // across every Renderer on this object/its children -- via
    // MaterialPropertyBlock, so the shared Material asset is never
    // touched and every skin instance can carry its own color. Per-skin
    // material schemes vary across this character pack (some use
    // Main/Skin/Details/Grey/Face, others Shirt/Skin/Pants/Belt/Face/
    // Hair), but "Skin" is the one name every character shares, so it's
    // the palette target for now. Could add richer per-model color
    // targeting (e.g. Shirt + Pants separately) later.
    public class PlayerColorizer : MonoBehaviour
    {
        // The Quaternius FBX materials are auto-generated on import (no
        // extracted .mat assets), so whether a slot exposes URP's
        // "_BaseColor" or Built-in/Standard's "_Color" depends on what
        // shader Unity assigned at import time -- check both rather than
        // assuming one.
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        public void ApplyBodyColor(Color color)
        {
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            int matchedSlots = 0;
            List<string> seenNames = new List<string>();

            foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material material = materials[i];
                    if (material == null) continue;
                    seenNames.Add(material.name);

                    if (!material.name.ToLowerInvariant().Contains("skin")) continue;

                    renderer.GetPropertyBlock(block, i);
                    if (material.HasProperty(BaseColorId)) block.SetColor(BaseColorId, color);
                    if (material.HasProperty(ColorId)) block.SetColor(ColorId, color);
                    renderer.SetPropertyBlock(block, i);
                    matchedSlots++;
                }
            }

            if (matchedSlots == 0)
            {
                Debug.LogWarning($"PlayerColorizer on {name} found no material slot named \"Skin\" -- actual material names on this skin: [{string.Join(", ", seenNames)}]", this);
            }
        }
    }
}
