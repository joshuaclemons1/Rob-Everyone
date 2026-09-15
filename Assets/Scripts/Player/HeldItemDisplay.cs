using Mirror;
using RobEveryone.Inventory;
using RobEveryone.Items;
using UnityEngine;

namespace RobEveryone.Player
{
    // Shows whatever hotbar item is currently selected, instantiated as a
    // child of the skin's hand bone -- visible to the owner in first
    // person (FirstPersonBodyTrim deliberately keeps arms/hands
    // untrimmed for exactly this) and to everyone else in third person.
    //
    // Deliberately a plain MonoBehaviour, not owner-gated -- this runs
    // identically on every client's copy of every player (own and
    // remote alike), since PlayerInventory's slot state is already fully
    // replicated to every client. This is a purely local, visual
    // reaction to that already-synced data -- the same "client-side
    // cosmetic" pattern PlayerRagdoll's bone physics already use, no new
    // networking needed.
    //
    // Once parented under the hand bone, Unity's normal Transform
    // hierarchy does the rest -- the held model automatically follows
    // whatever pose the Animator (including the Shoot/Swing Action
    // layer, see PlayerAnimationDriver) is currently driving that bone
    // to, with no per-frame positioning code needed here.
    [RequireComponent(typeof(PlayerInventory))]
    [RequireComponent(typeof(PlayerSkinSpawner))]
    public class HeldItemDisplay : MonoBehaviour
    {
        // Quaternius rig convention (confirmed across all 52 skin
        // prefabs, including non-humanoid ones like Cow) -- there's no
        // bone literally named "Hand", the wrist-equivalent is Fist.L/R.
        // Right hand matches the one-handed Shoot/Swing animations'
        // established main-hand convention; flip here if that's wrong.
        [SerializeField] private string handBoneName = "Fist.R";

        private PlayerInventory inventory;
        private PlayerSkinSpawner skinSpawner;

        private Transform handBone;
        private GameObject heldInstance;
        private ItemDefinition currentItem;

        // Editor-only tuning hook -- HeldItemPoseTuner forces a specific
        // item to display regardless of actual inventory state, so every
        // item's pose can be dialed in in one sitting instead of having
        // to actually pick each one up individually. Overrides
        // ResolveSelectedItem entirely while active.
        private bool previewActive;
        private ItemDefinition previewItem;

        public void SetPreviewItem(ItemDefinition item)
        {
            previewActive = true;
            previewItem = item;
            currentItem = null; // force Refresh to treat this as a change even if it's the same item as last time
            Refresh();
        }

        public void ClearPreview()
        {
            previewActive = false;
            previewItem = null;
            currentItem = null;
            Refresh();
        }

        // Re-applies the current preview item's pose/scale live as its
        // fields are edited, without destroying/re-instantiating (which
        // Refresh would do, since it treats a same-reference item as "no
        // change" specifically to avoid flicker during normal play).
        public void RefreshPreviewPose()
        {
            if (!previewActive || heldInstance == null || previewItem == null) return;
            ApplyPose(heldInstance, previewItem);
        }

        private void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
            skinSpawner = GetComponent<PlayerSkinSpawner>();
        }

        private void OnEnable()
        {
            inventory.OnSlotsChanged += Refresh;
            inventory.OnSelectedSlotChanged += HandleSelectedSlotChanged;
        }

        private void OnDisable()
        {
            inventory.OnSlotsChanged -= Refresh;
            inventory.OnSelectedSlotChanged -= HandleSelectedSlotChanged;
        }

        private void HandleSelectedSlotChanged(int _) => Refresh();

        private void Update()
        {
            // A remote player's skin can spawn several frames after this
            // component itself does (see PlayerSkinSpawner's own comment
            // on cosmetics arriving as a Client -> Server -> other
            // Clients round trip) -- keep retrying until it's actually
            // there, same lazy-resolve reasoning PlayerRagdoll's own
            // TryInitializeRagdoll already established.
            if (handBone == null) TryResolveHandBone();
        }

        // Issue #14 fix: re-derives the held item's scale every frame
        // instead of once at instantiation time. LateUpdate, same as
        // FirstPersonBodyTrim (Unity doesn't guarantee ordering between two
        // different scripts' LateUpdate without an explicit Script
        // Execution Order, so this doesn't assume it runs strictly before
        // or after that one) -- ApplyScale below skips entirely on a
        // momentarily-zeroed bone rather than latching a wrong fallback
        // value, so being at worst one frame behind FirstPersonBodyTrim's
        // own zero/restore is harmless; the actual bug was the scale never
        // getting a *chance* to recompute at all once Refresh() had
        // already run.
        private void LateUpdate()
        {
            if (heldInstance != null && currentItem != null) ApplyScale(heldInstance, currentItem);
        }

        private void TryResolveHandBone()
        {
            if (skinSpawner.SkinInstance == null) return;

            handBone = FindDescendant(skinSpawner.SkinInstance.transform, handBoneName);
            if (handBone == null)
            {
                Debug.LogWarning($"HeldItemDisplay: no '{handBoneName}' bone found on {skinSpawner.SkinInstance.name} -- held items won't show for this skin.", this);
                return;
            }

            Refresh(); // now that there's a socket, place whatever's already selected
        }

        private void Refresh()
        {
            if (handBone == null) return;

            ItemDefinition item = ResolveSelectedItem();
            if (item == currentItem) return; // no change

            if (heldInstance != null) Destroy(heldInstance);
            heldInstance = null;
            currentItem = item;

            if (item == null || item.WorldModelPrefab == null) return;

            heldInstance = Instantiate(item.WorldModelPrefab, handBone);
            StripInteractiveComponents(heldInstance);
            ApplyPose(heldInstance, item);
        }

        private void ApplyPose(GameObject instance, ItemDefinition item)
        {
            instance.transform.localPosition = item.HeldPositionOffset;
            instance.transform.localEulerAngles = item.HeldRotationOffset;
            ApplyScale(instance, item);
        }

        // WorldModelScale is calibrated for a scale-1 parent (how it sits
        // as a ground pickup) -- the hand bone can carry its own
        // accumulated scale from the rig import, so divide it back out
        // here rather than applying WorldModelScale directly as
        // localScale, or the held item comes out the wrong size. Called
        // every LateUpdate (not just once from ApplyPose above), since
        // handBone.lossyScale isn't a fixed value -- FirstPersonBodyTrim
        // zeroes it out entirely while airborne (part of the upper-body
        // trim), and latching whatever scale happened to be current at the
        // one moment Refresh() ran was the actual bug (issue #14): landing
        // on a zeroed frame fell back to the undivided, way-too-large raw
        // WorldModelScale, permanently, until the next slot change
        // happened to land on a non-zeroed frame.
        private void ApplyScale(GameObject instance, ItemDefinition item)
        {
            Vector3 parentScale = handBone.lossyScale;

            // Skip entirely (keep whatever scale it already has) rather
            // than fall back to an undivided/wrong value -- next frame's
            // call recomputes for real once the bone's scale is no longer
            // momentarily zero.
            if (Mathf.Approximately(parentScale.x, 0f) ||
                Mathf.Approximately(parentScale.y, 0f) ||
                Mathf.Approximately(parentScale.z, 0f))
            {
                return;
            }

            instance.transform.localScale = new Vector3(
                item.WorldModelScale.x / parentScale.x,
                item.WorldModelScale.y / parentScale.y,
                item.WorldModelScale.z / parentScale.z);
        }

        // item.WorldModelPrefab is the exact same prefab used for the
        // real ground pickup -- NetworkIdentity + PickupItem +
        // BoxCollider baked onto it by ItemPrefabBatchTool. Confirmed
        // bug: leaving those on a copy glued to the player's own hand
        // put a live solid collider inside their own body, which was
        // enough to double-trigger Ready Spot/round-end logic. This
        // display is purely cosmetic, so strip the whole interactive/
        // networked shell off the copy -- NetworkBehaviours (PickupItem
        // included) before the Collider/NetworkIdentity/Rigidbody they
        // require, since RequireComponent blocks removing a dependency
        // still in use.
        private static void StripInteractiveComponents(GameObject instance)
        {
            foreach (NetworkBehaviour behaviour in instance.GetComponentsInChildren<NetworkBehaviour>(true))
            {
                DestroyImmediate(behaviour);
            }
            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
            {
                DestroyImmediate(collider);
            }
            foreach (Rigidbody body in instance.GetComponentsInChildren<Rigidbody>(true))
            {
                DestroyImmediate(body);
            }
            foreach (NetworkIdentity identity in instance.GetComponentsInChildren<NetworkIdentity>(true))
            {
                DestroyImmediate(identity);
            }
        }

        // Same -1-means-nothing-selected convention PlayerInventory
        // itself documents (SelectedSlot's own comment) -- carrying a
        // ragdolled body sets this, so a held item automatically
        // disappears while your hands are full, with no separate
        // CarryController check needed here.
        private ItemDefinition ResolveSelectedItem()
        {
            if (previewActive) return previewItem;

            int index = inventory.SelectedSlot;
            if (index < 0 || index >= inventory.Slots.Count) return null;
            return inventory.Slots[index]?.Item;
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name == name) return candidate;
            }
            return null;
        }
    }
}
