using UnityEngine;

namespace RobEveryone.Items
{
    // One entry in the loot catalog -- name, sell value, hotbar icon, and
    // a reference to the world model/prefab it uses. Create one asset per
    // item type (Assets/Data/Items/ -- right-click -> Create -> Rob
    // Everyone -> Item Definition) and drag it into a PickupItem, instead
    // of typing the same name/value into every prefab by hand.
    [CreateAssetMenu(menuName = "Rob Everyone/Item Definition", fileName = "NewItem")]
    public class ItemDefinition : ScriptableObject
    {
        [SerializeField] private string itemName = "Watch";
        [SerializeField] private int value = 25;
        [SerializeField] private Sprite icon;
        [SerializeField] private GameObject worldModelPrefab;
        // The scale WorldModelPrefab needs to actually look right in the
        // world -- a raw imported model's own scale (1,1,1) rarely
        // matches how big it should render, and this is a property of the
        // model itself, not of any one LootSpawnPoint that happens to
        // roll it.
        [SerializeField] private Vector3 worldModelScale = Vector3.one;

        public string ItemName => itemName;
        public int Value => value;
        public Sprite Icon => icon;
        public GameObject WorldModelPrefab => worldModelPrefab;
        public Vector3 WorldModelScale => worldModelScale;
    }
}
