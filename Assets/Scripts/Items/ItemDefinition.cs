using UnityEngine;

namespace RobEveryone.Items
{
    // None = ordinary loot, no use-behavior. Melee = swing at a target in
    // range (Taser, Bat). Thrown = launched as a physics projectile that
    // detonates after a fuse -- BlastRadius > 0 makes it AOE (Dynamite),
    // BlastRadius == 0 would be a single-target hit on landing.
    // Deliberately not a separate enum value for AOE vs. single-target
    // thrown -- that's just data on the same Thrown path. Ranged = an
    // instant server-validated hitscan at longer range (Tranquilizer
    // Gun). [Flags] so an item can combine modes (Hammer is both Melee
    // and Thrown) -- always check membership with HasFlag, never a plain
    // == comparison, or a combined item silently matches nothing.
    [System.Flags]
    public enum SabotageType { None = 0, Melee = 1, Thrown = 2, Ranged = 4 }

    // Which arm animation the character plays when this item is used --
    // decoupled from SabotageType because the mapping isn't 1:1 (the
    // Taser is Melee-typed but reads better as a point-and-zap Shoot; a
    // thrown Dynamite reuses the Swing clip as a close-enough lob). None
    // = no arm animation at all (plain loot, or a use that shouldn't
    // gesture). Consumed by PlayerAnimationDriver's Action layer.
    public enum UseAnimation { None, Swing, Shoot }

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
        // How many of PlayerInventory's 5 hotbar slots this item eats up
        // at once -- most things are 1, but a bulky item (a fridge, a
        // safe) should plausibly cost most or all of your carrying
        // capacity just to haul out. See item-creation.md's price table
        // for the values actually used. Clamped to at least 1 in
        // InventorySize below -- 0 or negative would let an item occupy
        // no slots at all, which isn't a real state PlayerInventory's
        // slot-finding logic is written to handle.
        [SerializeField, Min(1)] private int inventorySize = 1;

        [Header("Sabotage (leave Type as None for regular loot)")]
        [SerializeField] private SabotageType sabotageType = SabotageType.None;
        [SerializeField] private float stunDuration = 2f;
        [SerializeField] private float impactForce = 40f;
        [SerializeField] private float cooldownSeconds = 0f; // 0 = no recharge needed
        [SerializeField] private float range = 2.5f; // melee reach
        [SerializeField] private float blastRadius = 0f; // >0 = AOE on landing (Dynamite); 0 = single-target
        [SerializeField] private GameObject thrownProjectilePrefab; // only used when SabotageType == Thrown
        // 0 = untracked/unlimited (Taser's cooldown is the limiter instead,
        // Dynamite/Alarm Clock are consumed whole on use). >0 = this many
        // uses before the slot empties -- Bat/Hammer durability, Tranq Gun
        // ammo. See PlayerInventory's parallel slotUses SyncList.
        [SerializeField] private int maxUses = 0;
        // Arm animation on use -- Swing for the Bat/Hammer (and thrown
        // items, close enough), Shoot for the Tranq Gun and Taser, None
        // for anything that shouldn't gesture. See UseAnimation above.
        [SerializeField] private UseAnimation useAnimation = UseAnimation.None;

        public string ItemName => itemName;
        public int Value => value;
        public Sprite Icon => icon;
        public GameObject WorldModelPrefab => worldModelPrefab;
        public Vector3 WorldModelScale => worldModelScale;
        public int InventorySize => Mathf.Max(1, inventorySize);

        public SabotageType SabotageType => sabotageType;
        public float StunDuration => stunDuration;
        public float ImpactForce => impactForce;
        public float CooldownSeconds => cooldownSeconds;
        public float Range => range;
        public float BlastRadius => blastRadius;
        public GameObject ThrownProjectilePrefab => thrownProjectilePrefab;
        public int MaxUses => maxUses;
        public UseAnimation UseAnimation => useAnimation;
    }
}
