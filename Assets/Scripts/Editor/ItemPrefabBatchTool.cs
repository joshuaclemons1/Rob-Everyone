using System.IO;
using Mirror;
using RobEveryone.Items;
using UnityEditor;
using UnityEngine;

namespace RobEveryone.EditorTools
{
    // Turns every raw model in Assets/Art/Items/ into a spawnable loot
    // item in one pass: wraps it in a prefab (fitted Box Collider +
    // NetworkIdentity, per stage4-multiplayer-mirror.md Part 4's
    // requirement) and creates its ItemDefinition (name/value/prefab
    // pre-filled, World Model Scale left at (1,1,1) to tune by eye
    // afterward). See item-creation.md for the full pipeline this
    // automates and the reasoning behind the price list below.
    //
    // Deliberately does NOT touch ItemCatalog or any LootTable -- those
    // are one-time, fast, visually-verifiable drag-and-drop steps
    // (multi-select the 33 new ItemDefinitions, drag into a list field)
    // better done by hand than automated, per item-creation.md Section
    // 1's size-tiered exclusion design (which table each item belongs in
    // is a placement decision, not something this tool can know).
    //
    // Every model here is a plain static mesh (no bones, no skeleton, no
    // joints) -- unlike RagdollBatchTool, there's no hierarchy remapping
    // or physics setup involved, just a fitted collider and two
    // components. Safe to re-run: an item whose prefab/ItemDefinition
    // already exists is skipped, not duplicated or overwritten.
    public static class ItemPrefabBatchTool
    {
        private const string SourceFolder = "Assets/Art/Items";
        private const string PrefabOutputFolder = "Assets/Prefabs/Items";
        private const string ItemDefinitionOutputFolder = "Assets/Data/Items";

        private struct ItemSpec
        {
            public string SourceFileName; // exact filename in Assets/Art/Items/, no extension needed if unambiguous
            public string DisplayName;
            public int Value;
            // How many of PlayerInventory's 5 slots this item costs to
            // carry -- see ItemDefinition.InventorySize. Small/Medium
            // items all fit in one hand, so 1; Large (appliance-scale)
            // items scale up to reflect actually being bulky to haul,
            // with Safe deliberately costing the entire inventory.
            public int InventorySize;

            public ItemSpec(string sourceFileName, string displayName, int value, int inventorySize = 1)
            {
                SourceFileName = sourceFileName;
                DisplayName = displayName;
                Value = value;
                InventorySize = inventorySize;
            }
        }

        // Matches item-creation.md's price table exactly -- Laptop is
        // excluded, it already has a working prefab/ItemDefinition from
        // before this tool existed. ComputerKeyboardMouse was one row in
        // the doc's table but is two separate source files -- split into
        // two separate items here (Keyboard, Mouse) so every source file
        // maps 1:1 to one output item, no combined-prefab special case.
        private static readonly ItemSpec[] Items =
        {
            // Small -- all InventorySize 1 (default, omitted below)
            new("Key11_with_tag.001", "Car Keys", 8),
            new("Prop_Coins", "Coins", 10),
            new("books", "Books", 10),
            new("Sunglasses_01", "Sunglasses", 12),
            new("computerKeyboard", "Keyboard", 8),
            new("computerMouse", "Mouse", 7),
            new("Headphones", "Headphones", 18),
            new("Wallet", "Wallet", 20),
            new("Wirst Watch", "Watch", 30),
            new("Smartphone", "Smartphone", 35),
            new("Camera", "Camera", 35),
            new("Jewelry", "Jewelry", 40),
            new("diamond_ring", "Diamond Ring", 65),
            new("Gold_Ingots", "Gold Ingots", 90),

            // Medium -- all InventorySize 1 (default, omitted below)
            new("toaster", "Toaster", 15),
            new("Purse_01", "Purse", 18),
            new("lampRoundTable", "Table Lamp", 20),
            new("Backpack", "Backpack", 20),
            new("radio", "Radio", 22),
            new("kitchenBlender", "Blender", 25),
            new("kitchenCoffeeMachine", "Coffee Machine", 28),
            new("speaker", "Speaker", 32),
            new("computerScreen", "Computer Monitor", 35),
            new("kitchenMicrowave", "Microwave", 45),

            // Large -- genuinely bulky, InventorySize scales with how
            // much of a 5-slot inventory hauling it out plausibly costs.
            new("televisionVintage", "Vintage Television", 60, 2),
            new("dryer", "Dryer", 70, 2),
            new("washer", "Washer", 75, 2),
            new("kitchenStove", "Stove", 80, 2),
            new("kitchenStoveElectric", "Electric Stove", 85, 2),
            new("televisionModern", "Modern Television", 90, 2),
            new("kitchenFridge", "Fridge", 100, 3),
            new("kitchenFridgeLarge", "Large Fridge", 130, 4),
            new("Safe", "Safe", 250, 5), // the whole inventory, on purpose -- the jackpot item
        };

        [MenuItem("Rob Everyone/Batch Create Loot Items")]
        public static void RunBatch()
        {
            EnsureFolder(PrefabOutputFolder);
            EnsureFolder(ItemDefinitionOutputFolder);

            int prefabsCreated = 0;
            int itemDefsCreated = 0;
            int skipped = 0;
            int failed = 0;

            foreach (ItemSpec spec in Items)
            {
                try
                {
                    ProcessItem(spec, ref prefabsCreated, ref itemDefsCreated, ref skipped);
                }
                catch (System.Exception e)
                {
                    failed++;
                    Debug.LogError($"ItemPrefabBatchTool: failed on '{spec.DisplayName}' ({spec.SourceFileName}) -- {e.Message}");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"ItemPrefabBatchTool: {prefabsCreated} prefabs created, {itemDefsCreated} ItemDefinitions created, {skipped} already existed (skipped), {failed} failed. Check the Console above for exactly which, if any, failed.");
        }

        private static void ProcessItem(ItemSpec spec, ref int prefabsCreated, ref int itemDefsCreated, ref int skipped)
        {
            string prefabPath = $"{PrefabOutputFolder}/{spec.DisplayName.Replace(" ", "")}.prefab";
            string itemDefPath = $"{ItemDefinitionOutputFolder}/{spec.DisplayName.Replace(" ", "")}.asset";

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                prefab = CreatePrefab(spec, prefabPath);
                if (prefab == null)
                {
                    Debug.LogError($"ItemPrefabBatchTool: couldn't find a source model for '{spec.DisplayName}' -- looked for '{spec.SourceFileName}' (.fbx/.obj) in {SourceFolder}/. Check the exact filename in the Project window matches item-creation.md's table.");
                    return;
                }
                prefabsCreated++;
            }
            else
            {
                skipped++;
            }

            if (AssetDatabase.LoadAssetAtPath<ItemDefinition>(itemDefPath) == null)
            {
                CreateItemDefinition(spec, prefab, itemDefPath);
                itemDefsCreated++;
            }
        }

        private static GameObject CreatePrefab(ItemSpec spec, string prefabPath)
        {
            GameObject sourceAsset = FindSourceModel(spec.SourceFileName);
            if (sourceAsset == null) return null;

            GameObject instance = PrefabUtility.InstantiatePrefab(sourceAsset) as GameObject;
            if (instance == null)
            {
                // Fallback for a source asset Unity doesn't treat as a
                // "model prefab" for some reason -- a plain Instantiate
                // still gives us a real scene copy to add components to.
                instance = Object.Instantiate(sourceAsset);
            }
            instance.name = spec.DisplayName.Replace(" ", "");

            try
            {
                AddFittedBoxCollider(instance);

                if (instance.GetComponent<NetworkIdentity>() == null)
                {
                    instance.AddComponent<NetworkIdentity>();
                }

                GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath, out bool success);
                if (!success || savedPrefab == null)
                {
                    Debug.LogError($"ItemPrefabBatchTool: SaveAsPrefabAsset failed for '{spec.DisplayName}' at {prefabPath}.");
                    return null;
                }

                return savedPrefab;
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        // Searches Assets/Art/Items/ for a .fbx or .obj whose filename
        // (without extension) matches exactly -- explicit exact-match
        // search rather than a fuzzy guess, so a missing/renamed file
        // fails loudly (see the "couldn't find a source model" error
        // above) instead of silently picking the wrong thing.
        private static GameObject FindSourceModel(string fileNameWithoutExtension)
        {
            foreach (string ext in new[] { "fbx", "obj" })
            {
                string path = $"{SourceFolder}/{fileNameWithoutExtension}.{ext}";
                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset != null) return asset;
            }
            return null;
        }

        // Fits a Box Collider to the combined bounds of every Renderer
        // under the instance, converted into the instance's own local
        // space -- a default Add Component -> Box Collider (1x1x1 at
        // local center) would be wrong for almost every model here,
        // since none of these imported models have their visible mesh
        // sitting at exactly that size/position.
        private static void AddFittedBoxCollider(GameObject root)
        {
            if (root.GetComponent<Collider>() != null) return;

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                Debug.LogWarning($"ItemPrefabBatchTool: '{root.name}' has no Renderer -- added a default 1x1x1 Box Collider, check it by hand.");
                root.AddComponent<BoxCollider>();
                return;
            }

            Bounds worldBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) worldBounds.Encapsulate(renderers[i].bounds);

            Vector3 localCenter = root.transform.InverseTransformPoint(worldBounds.center);
            Vector3 localSize = root.transform.InverseTransformVector(worldBounds.size);
            localSize = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));

            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.center = localCenter;
            collider.size = localSize;
        }

        // Uses SerializedObject/SerializedProperty rather than adding
        // public setters to ItemDefinition -- avoids widening that
        // class's API just for this tool (it already had InventorySize
        // added for the multi-slot mechanic itself, but nothing here
        // needs a public setter for any of these fields either way).
        private static void CreateItemDefinition(ItemSpec spec, GameObject prefab, string assetPath)
        {
            ItemDefinition item = ScriptableObject.CreateInstance<ItemDefinition>();

            SerializedObject so = new SerializedObject(item);
            so.FindProperty("itemName").stringValue = spec.DisplayName;
            so.FindProperty("value").intValue = spec.Value;
            so.FindProperty("worldModelPrefab").objectReferenceValue = prefab;
            so.FindProperty("worldModelScale").vector3Value = Vector3.one;
            so.FindProperty("inventorySize").intValue = spec.InventorySize;
            so.ApplyModifiedProperties();

            AssetDatabase.CreateAsset(item, assetPath);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string newFolderName = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, newFolderName);
        }
    }
}
