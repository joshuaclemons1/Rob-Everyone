using RobEveryone.Customization;
using UnityEngine;

namespace RobEveryone.Player
{
    // Instantiates whichever skin/color the player picked in the main
    // menu (PlayerCosmeticSelection) as a child of this Transform at
    // gameplay start -- reads the same PlayerSkinRoster/PlayerColorPalette
    // assets CustomizationUI's menu preview uses, so there's one shared
    // list rather than gameplay hardcoding a specific character. This is
    // also the seam a future networked spawn hooks into: a remote player's
    // spawn would read the *owning* player's synced skin/color choice and
    // call the same instantiate-plus-colorize logic, rather than needing a
    // different code path.
    public class PlayerSkinSpawner : MonoBehaviour
    {
        [SerializeField] private PlayerSkinRoster skinRoster;
        [SerializeField] private PlayerColorPalette palette;
        [SerializeField] private LayerMask skinLayer;

        public GameObject SkinInstance { get; private set; }
        // PlayerRagdoll needs this to toggle PlayerCamera's Culling Mask
        // during the stun -- kept as the single source of truth here
        // rather than a second, separately-configured field there.
        public LayerMask SkinLayer => skinLayer;

        private void Awake()
        {
            if (skinRoster == null || skinRoster.Count == 0) return;

            GameObject prefab = skinRoster.GetSkin(PlayerCosmeticSelection.SkinIndex);
            if (prefab == null) return;

            SkinInstance = Instantiate(prefab, transform.position, transform.rotation, transform);
            // The model's own root is authored at its feet (ground level),
            // but the Player's pivot sits at the CharacterController
            // capsule's *center*, not its bottom -- placing the model
            // there directly leaves it floating at roughly waist/chest
            // height instead of standing on the ground. Offset it down by
            // however far the capsule's center sits above its own bottom.
            CharacterController controller = GetComponent<CharacterController>();
            if (controller != null)
            {
                float feetOffset = controller.center.y - controller.height * 0.5f;
                SkinInstance.transform.localPosition = new Vector3(0f, feetOffset, 0f);
            }

            SetLayerRecursively(SkinInstance.transform);
            WidenSkinnedMeshBounds(SkinInstance.transform);
            DisableAnimator(SkinInstance.transform);
            ReattachOrphanedBones(SkinInstance.transform);

            PlayerColorizer colorizer = SkinInstance.GetComponent<PlayerColorizer>();
            if (colorizer == null) colorizer = SkinInstance.AddComponent<PlayerColorizer>();

            if (palette != null && palette.Colors.Count > 0)
            {
                int colorIndex = Mathf.Clamp(PlayerCosmeticSelection.ColorIndex, 0, palette.Colors.Count - 1);
                colorizer.ApplyBodyColor(palette.Colors[colorIndex]);
            }
        }

        // A SkinnedMeshRenderer computes its visibility bounds around where
        // the mesh sits during normal animation, and can stop updating the
        // skin entirely once it decides that bounds is off-screen. A
        // ragdoll sending bones flying outside that expected volume then
        // reads as the mesh freezing/stretching while the bones underneath
        // keep moving (or, in a wide shot, as barely appearing to move at
        // all, since only the still-frozen mesh is what's visible).
        // updateWhenOffscreen forces it to always recompute bounds from the
        // real current bone positions instead of relying on a cached
        // guess -- a one-time manual bounds override didn't hold, since
        // Unity recalculates it on its own once it thinks the mesh is
        // visible again anyway.
        private static void WidenSkinnedMeshBounds(Transform root)
        {
            foreach (SkinnedMeshRenderer renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
#pragma warning disable CS0618 // Obsolete in newer Unity, but still functional and still the correct fix for this
                renderer.updateWhenOffscreen = true;
#pragma warning restore CS0618
            }
        }

        // This rig's Foot.L/Foot.R/PoleTarget.L/PoleTarget.R (an IK
        // pole-target pair, used to aim knee-bend direction) ship as
        // siblings of the actual skeleton root ("Body"), all hanging off a
        // shared, never-moving "Bone" group object -- not as children of
        // LowerLeg like a normal foot bone would be. None of them have a
        // Rigidbody of their own, so nothing was ever moving them: during a
        // ragdoll they just sat frozen exactly where they spawned while the
        // physics-driven leg flew off, stretching the mesh between the
        // frozen foot vertices and the flying shin. Re-parenting them under
        // the matching LowerLeg bone (world position preserved) makes them
        // inherit its motion the way any other passive child bone should.
        // Confirmed via direct prefab inspection to be the same broken
        // parenting on every skin in the pack, not a one-off.
        private static void ReattachOrphanedBones(Transform skinRoot)
        {
            ReattachToLeg(skinRoot, "Foot.L", "LowerLeg.L");
            ReattachToLeg(skinRoot, "Foot.R", "LowerLeg.R");
            ReattachToLeg(skinRoot, "PoleTarget.L", "LowerLeg.L");
            ReattachToLeg(skinRoot, "PoleTarget.R", "LowerLeg.R");
            ReattachSkeletonRootToHips(skinRoot);
        }

        // "Bone" and its own child named "Body" (the skeleton root group --
        // not the separate, top-level "Body" GameObject that holds the
        // SkinnedMeshRenderer itself; the two just happen to share a name)
        // sit *above* Hips in the hierarchy as its static, never-moving
        // ancestors. The mesh is skinned directly to both of them, not just
        // to Hips (confirmed via the SkinnedMeshRenderer's own bone list),
        // so with neither having a Rigidbody, and neither downstream of
        // one, any vertices weighted to them stayed frozen exactly at the
        // spawn/impact point while everything hanging off Hips flew away,
        // stretching the mesh all the way back to the stationary Player
        // root.
        //
        // Driving Bone/Body's transform to chase Hips every frame (an
        // earlier attempt at this fix) looked right but wasn't: Hips (and
        // its Body-parented siblings, e.g. UpperLeg.L/R) are *children* of
        // Body, so moving Body to Hips's current world position each frame
        // doesn't just relocate Body -- it also shifts Hips's own computed
        // world position by whatever Hips's local offset from Body happens
        // to be (world = parent * local), and since that shifted position
        // is what gets read back out next frame, the same offset gets
        // added again on top of itself every single frame. That unbounded
        // compounding drift is what was launching the ragdoll far outside
        // the map even from a tiny impact force -- it was never about the
        // force at all.
        //
        // The real fix has to break the ancestor relationship structurally,
        // once, instead of fighting it every frame: pull Hips (and
        // whatever else Body was parenting, e.g. UpperLeg.L/R) out from
        // under Body entirely first -- a non-kinematic Rigidbody doesn't
        // care who its Transform parent is, so this is free -- which
        // leaves Body with nothing left underneath it, and only then is it
        // safe to fold Body (and Bone above it) onto Hips like any other
        // passive child bone, the same fix as Foot/PoleTarget just with
        // this extra untangling step first.
        private static void ReattachSkeletonRootToHips(Transform skinRoot)
        {
            Transform boneGroup = FindDescendant(skinRoot, "Bone");
            Transform hips = FindDescendant(skinRoot, "Hips");
            if (boneGroup == null || hips == null) return;

            Transform skeletonBody = boneGroup.Find("Body");
            if (skeletonBody == null || hips.parent != skeletonBody) return;

            Transform armature = boneGroup.parent;

            // Move every other child Body was parenting (e.g.
            // UpperLeg.L/R) onto Hips first, so nothing gets orphaned once
            // Body stops being their parent.
            for (int i = skeletonBody.childCount - 1; i >= 0; i--)
            {
                Transform child = skeletonBody.GetChild(i);
                if (child == hips) continue;
                child.SetParent(hips, true);
            }

            // Detach Hips from Body onto Bone's own parent -- not onto
            // Bone itself, which would just recreate the same
            // ancestor-cycle problem one level up.
            hips.SetParent(armature, true);

            // Body and Bone now have no physics descendants left -- safe
            // to fold them onto Hips like any other passive child bone.
            skeletonBody.SetParent(hips, true);
            boneGroup.SetParent(hips, true);
        }

        private static void ReattachToLeg(Transform skinRoot, string boneName, string newParentName)
        {
            Transform bone = FindDescendant(skinRoot, boneName);
            Transform newParent = FindDescendant(skinRoot, newParentName);
            if (bone == null || newParent == null || bone.parent == newParent) return;

            bone.SetParent(newParent, true);
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name == name) return candidate;
            }
            return null;
        }

        // Quaternius FBX imports come with an Animator/Avatar for their
        // built-in animation clips -- nothing in this project drives
        // player-model animation yet, so left enabled it just fights the
        // ragdoll: physics correctly moves/computes velocity on a bone, but
        // Animator.Update() then overwrites that same Transform back to
        // wherever its (unused) clip says it should be, every single
        // frame. That's what was actually causing both the "no momentum"
        // and the stretching -- physics was working the whole time, the
        // rendered result just kept getting overwritten before it showed.
        private static void DisableAnimator(Transform root)
        {
            foreach (Animator animator in root.GetComponentsInChildren<Animator>(true))
            {
                animator.enabled = false;
            }
        }

        // Everything on the skin goes on a dedicated layer (assign it to
        // whatever layer your own PlayerCamera excludes from its Culling
        // Mask) -- that's what makes "others see your body, you don't see
        // your own" work, instead of SetActive(false), which would hide it
        // from every camera, not just your own.
        private void SetLayerRecursively(Transform root)
        {
            int layer = LayerMaskToLayer(skinLayer);
            if (layer < 0) return;

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = layer;
            }
        }

        private static int LayerMaskToLayer(LayerMask mask)
        {
            int value = mask.value;
            for (int i = 0; i < 32; i++)
            {
                if ((value & (1 << i)) != 0) return i;
            }
            return -1;
        }
    }
}
