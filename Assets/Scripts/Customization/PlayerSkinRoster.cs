using UnityEngine;

namespace RobEveryone.Customization
{
    // The single shared list of selectable player skins -- both the menu's
    // CustomizationUI (preview) and gameplay's PlayerSkinSpawner read the
    // same list by the same PlayerCosmeticSelection.SkinIndex, so there's
    // one place to add a new skin rather than keeping two lists in sync.
    // Create via Assets > Create > Rob Everyone > Player Skin Roster.
    [CreateAssetMenu(menuName = "Rob Everyone/Player Skin Roster")]
    public class PlayerSkinRoster : ScriptableObject
    {
        [SerializeField] private GameObject[] skinPrefabs;

        public int Count => skinPrefabs.Length;

        public GameObject GetSkin(int index)
        {
            if (skinPrefabs.Length == 0) return null;
            return skinPrefabs[Mathf.Clamp(index, 0, skinPrefabs.Length - 1)];
        }
    }
}
