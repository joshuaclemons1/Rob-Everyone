using System.Collections;
using UnityEngine;

namespace RobEveryone.Player
{
    // Ragdolls whatever skin PlayerSkinSpawner instantiated -- generic on
    // purpose (was CarImpactReceiver; ApplyImpact(direction, force) is
    // exactly as reusable for a future PvP takedown as it is for a car).
    // Built with Unity's Ragdoll Wizard on each skin *prefab* (Rigidbody +
    // CharacterJoint per limb, kinematic by default so the model just
    // stands rigidly until this script flips it off). Unlike the old
    // version, the skin model stays active/visible at all times now (a
    // future networked player needs to be visible to *other* players, not
    // just its owner) -- visibility toward the owner's own camera is a
    // separate concern, handled by Culling Mask on PlayerCamera excluding
    // the skin's layer, not by hiding the object itself.
    //
    // That exclusion is normally on (you never see your own body during
    // regular first-person play), but the third-person stun cutaway uses
    // this exact same camera -- so the mask is toggled back *on* for the
    // skin layer only while that cutaway is active, otherwise the one
    // moment you're supposed to see your own ragdoll would be the one
    // moment the camera is still hiding it.
    //
    // The camera detaches for the stun and follows the ragdoll's hips
    // (found via the RagdollHips marker, not a hardcoded bone name, since
    // the selectable skins don't necessarily share bone names) with a
    // damped lag rather than instant tracking -- a perfectly-centering
    // camera cancels out any visible sign of real travel, since the
    // character stays glued to the same spot in frame while the camera
    // does all the moving with it. The facing/offset direction itself is
    // locked to the yaw captured *at the moment of impact*, not updated
    // live, so the physics can't spin the camera around.
    [RequireComponent(typeof(FirstPersonController))]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerSkinSpawner))]
    public class PlayerRagdoll : MonoBehaviour
    {
        [SerializeField] private float stunDuration = 2f;

        [Header("Third-person ragdoll view")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private Vector3 thirdPersonOffset = new(0f, 2.5f, -5f);
        [SerializeField] private float lookAtHeightOffset = 0.5f;
        // How quickly the camera catches up to the character -- lower
        // reads as more "static" (real travel stays clearly visible,
        // camera feels detached), higher reads as more "locked on" (feels
        // alive/dynamic, but starts canceling out visible travel the
        // closer it gets to instant). This is deliberately not instant.
        [SerializeField] private float cameraFollowSpeed = 3f;

        private FirstPersonController firstPersonController;
        private CharacterController characterController;
        private PlayerSkinSpawner skinSpawner;
        private Camera playerCamera;
        private int cameraOriginalCullingMask;

        private Rigidbody[] ragdollBodies;
        private Vector3[] restLocalPositions;
        private Quaternion[] restLocalRotations;
        private Rigidbody hipsRigidbody;
        // Disabled for the duration of a stun and re-enabled afterward --
        // PlayerAnimationDriver drives this the rest of the time, and a
        // driven Animator fights ragdoll physics exactly like the old,
        // permanently-on Animator used to (Animator.Update() overwriting a
        // bone's physics-computed Transform every frame with wherever the
        // locomotion clip says it should be).
        private Animator animator;
        // Fixed direction+distance (rotated by the yaw at impact), not an
        // absolute position -- re-added to the target's *current* position
        // every frame in UpdateThirdPersonView, so the camera keeps a
        // stable facing/distance while still following.
        private Vector3 fixedCameraOffset;

        private Transform cameraOriginalParent;
        private Vector3 cameraOriginalLocalPosition;
        private Quaternion cameraOriginalLocalRotation;

        private bool isStunned;

        private void Awake()
        {
            firstPersonController = GetComponent<FirstPersonController>();
            characterController = GetComponent<CharacterController>();
            skinSpawner = GetComponent<PlayerSkinSpawner>();

            if (cameraTransform != null) playerCamera = cameraTransform.GetComponent<Camera>();
        }

        private void Start()
        {
            // Start, not Awake -- guarantees PlayerSkinSpawner's own Awake
            // (which does the actual Instantiate) has already run.
            GameObject skin = skinSpawner.SkinInstance;
            if (skin == null)
            {
                Debug.LogWarning("PlayerRagdoll found no skin instance from PlayerSkinSpawner -- check the Player Skin Roster is assigned and has at least one entry.", this);
                return;
            }

            RagdollHips hips = skin.GetComponentInChildren<RagdollHips>(true);
            if (hips == null)
            {
                Debug.LogWarning($"PlayerRagdoll: the currently selected skin ({skin.name}) has no RagdollHips marker -- it needs the Ragdoll Wizard run on its prefab, with the Pelvis bone marked. Impacts will do nothing until then.", this);
                return;
            }

            hipsRigidbody = hips.Rigidbody;
            animator = skin.GetComponentInChildren<Animator>(true);
            ragdollBodies = skin.GetComponentsInChildren<Rigidbody>(true);
            restLocalPositions = new Vector3[ragdollBodies.Length];
            restLocalRotations = new Quaternion[ragdollBodies.Length];
            for (int i = 0; i < ragdollBodies.Length; i++)
            {
                restLocalPositions[i] = ragdollBodies[i].transform.localPosition;
                restLocalRotations[i] = ragdollBodies[i].transform.localRotation;
                ragdollBodies[i].isKinematic = true;
            }

            FindBridgeBones(skin.transform);
            BuildHierarchyPatches(skin.transform, hipsRigidbody.transform);
            IgnoreSelfCollisions();
        }

        // CharacterJoint.enableCollision (false by default, and never set
        // otherwise here) only suppresses collision between two bones that
        // are *directly* jointed to each other -- it does nothing for
        // non-adjacent pairs, e.g. the left arm swinging into the torso, or
        // into the right arm. Under a hard impact, limbs flailing within
        // even normal joint limits can still intersect each other's
        // capsule colliders, and each overlap triggers a depenetration
        // push -- repeated every physics step, that compounds into
        // exactly the kind of escalating, coherent "spaz then launch"
        // motion this was producing, without ever showing individual
        // joints tearing apart from each other. Self-collision between a
        // single ragdoll's own body parts is essentially never wanted
        // anyway, so this is switched off unconditionally rather than
        // trying to selectively fix collider sizes per skin.
        private void IgnoreSelfCollisions()
        {
            Collider[] colliders = new Collider[ragdollBodies.Length];
            for (int i = 0; i < ragdollBodies.Length; i++)
            {
                colliders[i] = ragdollBodies[i].GetComponent<Collider>();
            }

            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] == null) continue;
                for (int j = i + 1; j < colliders.Length; j++)
                {
                    if (colliders[j] == null) continue;
                    Physics.IgnoreCollision(colliders[i], colliders[j], true);
                }
            }
        }

        // This rig's skeleton alternates physics bones with plain
        // pass-through bones at every joint -- Hips(RB) -> Abdomen(no RB) ->
        // Torso(RB), Torso(RB) -> Neck(no RB) -> Head(RB), and likewise
        // Torso -> Shoulder.L/R (no RB) -> UpperArm.L/R (RB). The Ragdoll
        // Wizard only gives physics to Hips/Spine/Head/limbs, so these
        // "helper" bones have no Rigidbody of their own -- they just stay
        // rigidly glued to their *parent's* orientation forever, while the
        // joint-driven bone on their other side swings independently under
        // impact. Since both are skinned to the same mesh region (the
        // waist/neck/shoulder seam), a hard hit can swing one side far from
        // the other, stretching the mesh between them. There's no
        // Rigidbody/joint to fix here -- these bones need to be driven by
        // script instead, each frame, to sit between their two physics
        // neighbors rather than rigidly following only one of them.
        private static readonly (string bone, string parent, string child)[] BridgeBoneNames =
        {
            ("Abdomen", "Hips", "Torso"),
            ("Neck", "Torso", "Head"),
            ("Shoulder.L", "Torso", "UpperArm.L"),
            ("Shoulder.R", "Torso", "UpperArm.R"),
        };

        private Transform[] bridgeBones;
        private Transform[] bridgeParents;
        private Transform[] bridgeChildren;
        private Vector3[] bridgeRestLocalPositions;
        private Quaternion[] bridgeRestLocalRotations;

        private void FindBridgeBones(Transform skinRoot)
        {
            Transform[] allTransforms = skinRoot.GetComponentsInChildren<Transform>(true);
            Transform Find(string name)
            {
                foreach (Transform t in allTransforms)
                {
                    if (t.name == name) return t;
                }
                return null;
            }

            var bones = new System.Collections.Generic.List<Transform>();
            var parents = new System.Collections.Generic.List<Transform>();
            var children = new System.Collections.Generic.List<Transform>();

            foreach (var (boneName, parentName, childName) in BridgeBoneNames)
            {
                Transform bone = Find(boneName);
                Transform parent = Find(parentName);
                Transform child = Find(childName);
                if (bone == null || parent == null || child == null) continue;

                // "bone" (e.g. Abdomen) is the Transform *ancestor* of
                // "child" (e.g. Torso) here, not just a nearby sibling --
                // driving bone's position/rotation by script every frame
                // would otherwise shift child's own computed world
                // position too (world = parent * local), which then feeds
                // back into next frame's Lerp target, compounding without
                // bound. The actual re-parenting that avoids this (moving
                // child directly onto parent, bypassing bone) is handled
                // by BuildHierarchyPatches/ApplyRagdollHierarchy instead of
                // here, since it has to be temporary -- reversed the
                // moment the ragdoll ends -- rather than permanent, or a
                // future Generic-rig walk/run/jump animation clip
                // targeting these bones by hierarchy path would silently
                // fail to bind.
                bones.Add(bone);
                parents.Add(parent);
                children.Add(child);
            }

            bridgeBones = bones.ToArray();
            bridgeParents = parents.ToArray();
            bridgeChildren = children.ToArray();
            bridgeRestLocalPositions = new Vector3[bridgeBones.Length];
            bridgeRestLocalRotations = new Quaternion[bridgeBones.Length];
            for (int i = 0; i < bridgeBones.Length; i++)
            {
                bridgeRestLocalPositions[i] = bridgeBones[i].localPosition;
                bridgeRestLocalRotations[i] = bridgeBones[i].localRotation;
            }
        }

        // Called every frame during the stun -- see FindBridgeBones for why
        // these specific bones need to be manually placed rather than left
        // to physics or normal Transform parenting.
        private void UpdateBridgeBones()
        {
            if (bridgeBones == null) return;

            for (int i = 0; i < bridgeBones.Length; i++)
            {
                if (bridgeBones[i] == null) continue;

                bridgeBones[i].position = Vector3.Lerp(bridgeParents[i].position, bridgeChildren[i].position, 0.5f);
                bridgeBones[i].rotation = Quaternion.Slerp(bridgeParents[i].rotation, bridgeChildren[i].rotation, 0.5f);
            }
        }

        // Bundles a single temporary re-parent: what bone moves, where it
        // goes while ragdolling, and everything needed to put it back
        // exactly where the FBX authored it (original parent plus its
        // original local pose relative to that parent) once the ragdoll
        // ends. This rig imports as Generic, not Humanoid (confirmed via
        // the FBX import settings) -- Generic clips bind animation curves
        // by exact hierarchy *path*, not by bone name or an avatar
        // mapping, so any of this left permanently reparented (an earlier
        // version of these fixes did exactly that) would silently break
        // whichever future walk/run/jump clips target these bones by
        // their original path.
        private struct HierarchyPatch
        {
            public Transform bone;
            public Transform ragdollParent;
            public Transform originalParent;
            public Vector3 originalLocalPosition;
            public Quaternion originalLocalRotation;
        }

        // Applied in this exact order when the ragdoll starts, reversed
        // in exact reverse order when it ends -- several of these steps
        // only avoid creating a parent/child cycle *because* of what an
        // earlier step already did (e.g. Body can't safely fold onto Hips
        // until Hips has already been pulled out from under Body), so
        // both directions have to walk the list in a specific order, not
        // just "whichever bone happens to need fixing."
        private HierarchyPatch[] hierarchyPatches;

        private void BuildHierarchyPatches(Transform skinRoot, Transform hips)
        {
            var patches = new System.Collections.Generic.List<HierarchyPatch>();

            void Patch(Transform bone, Transform ragdollParent)
            {
                if (bone == null || ragdollParent == null) return;
                patches.Add(new HierarchyPatch
                {
                    bone = bone,
                    ragdollParent = ragdollParent,
                    originalParent = bone.parent,
                    originalLocalPosition = bone.localPosition,
                    originalLocalRotation = bone.localRotation,
                });
            }

            // Foot.L/R and PoleTarget.L/R (an IK pole-target pair used to
            // aim knee-bend direction) ship parented to the skeleton's
            // static "Bone" group instead of the moving shin bone --
            // without this, they sit frozen at the spawn point while the
            // leg flies off, stretching the mesh between them.
            Transform lowerLegL = FindDescendant(skinRoot, "LowerLeg.L");
            Transform lowerLegR = FindDescendant(skinRoot, "LowerLeg.R");
            Patch(FindDescendant(skinRoot, "Foot.L"), lowerLegL);
            Patch(FindDescendant(skinRoot, "Foot.R"), lowerLegR);
            Patch(FindDescendant(skinRoot, "PoleTarget.L"), lowerLegL);
            Patch(FindDescendant(skinRoot, "PoleTarget.R"), lowerLegR);

            // The skeleton's root "Bone"/"Body" group objects are Hips's
            // ancestors, but are *also* directly skinned mesh bones --
            // any vertices weighted to them stay frozen at the spawn
            // point otherwise (neither has a Rigidbody, and neither is
            // downstream of one). Hips (and whatever else Body was
            // parenting, e.g. UpperLeg.L/R) has to come out from under
            // Body first -- a non-kinematic Rigidbody doesn't care who
            // its Transform parent is, so this is free -- before Body and
            // Bone can safely fold onto Hips without creating a
            // parent/child cycle (Body was Hips's ancestor to begin
            // with).
            Transform boneGroup = FindDescendant(skinRoot, "Bone");
            Transform skeletonBody = boneGroup != null ? boneGroup.Find("Body") : null;
            if (boneGroup != null && skeletonBody != null && hips.parent == skeletonBody)
            {
                Transform armature = boneGroup.parent;

                for (int i = skeletonBody.childCount - 1; i >= 0; i--)
                {
                    Transform child = skeletonBody.GetChild(i);
                    if (child == hips) continue;
                    Patch(child, hips);
                }

                Patch(hips, armature);
                Patch(skeletonBody, hips);
                Patch(boneGroup, hips);
            }

            // Abdomen/Neck/Shoulder.L/R are passive bones sandwiched
            // between two jointed bones with no physics of their own --
            // UpdateBridgeBones drives them each frame so the mesh
            // doesn't tear at the waist/neck/shoulder seam. That only
            // works once Torso/Head/UpperArm.L/R are pulled out from
            // being that passive bone's *descendant* first -- driving an
            // ancestor's transform by script otherwise shifts its own
            // child's computed world position too (world = parent *
            // local), compounding into runaway drift every frame (this
            // is what was launching the ragdoll off the map even at a
            // tiny impact force).
            foreach (var (boneName, parentName, childName) in BridgeBoneNames)
            {
                Transform bone = FindDescendant(skinRoot, boneName);
                Transform parent = FindDescendant(skinRoot, parentName);
                Transform child = FindDescendant(skinRoot, childName);
                if (bone == null || parent == null || child == null) continue;
                if (child.parent != bone) continue;

                Patch(child, parent);
            }

            hierarchyPatches = patches.ToArray();
        }

        private void ApplyRagdollHierarchy()
        {
            if (hierarchyPatches == null) return;

            for (int i = 0; i < hierarchyPatches.Length; i++)
            {
                hierarchyPatches[i].bone.SetParent(hierarchyPatches[i].ragdollParent, true);
            }
        }

        // Must run before anything else in EndRagdoll reads a patched
        // bone's rest pose (e.g. the ragdollBodies loop's localPosition
        // reset) -- those values were captured relative to each bone's
        // *original* parent, so the parent has to already be back in
        // place before they're reapplied.
        private void RestoreOriginalHierarchy()
        {
            if (hierarchyPatches == null) return;

            for (int i = hierarchyPatches.Length - 1; i >= 0; i--)
            {
                HierarchyPatch patch = hierarchyPatches[i];
                patch.bone.SetParent(patch.originalParent, false);
                patch.bone.localPosition = patch.originalLocalPosition;
                patch.bone.localRotation = patch.originalLocalRotation;
            }
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name == name) return candidate;
            }
            return null;
        }

        public void ApplyImpact(Vector3 direction, float force)
        {
            if (isStunned || hipsRigidbody == null) return;
            StartCoroutine(ImpactSequence(direction, force));
        }

        private IEnumerator ImpactSequence(Vector3 direction, float force)
        {
            isStunned = true;

            firstPersonController.enabled = false;
            characterController.enabled = false;
            if (animator != null) animator.enabled = false;

            // Captured once, before the ragdoll starts moving -- keeps the
            // third-person camera's facing stable even though the hips
            // it's tracking are about to fly off unpredictably.
            Quaternion rigYaw = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

            BeginRagdoll(direction, force);
            BeginThirdPersonView(rigYaw);

            float elapsed = 0f;
            while (elapsed < stunDuration)
            {
                UpdateThirdPersonView();
                UpdateBridgeBones();
                elapsed += Time.deltaTime;
                yield return null;
            }

            EndThirdPersonView();
            EndRagdoll();

            characterController.enabled = true;
            firstPersonController.enabled = true;
            if (animator != null) animator.enabled = true;

            isStunned = false;
        }

        private void BeginRagdoll(Vector3 direction, float force)
        {
            // Restructure the hierarchy first, while everything is still
            // standing still and kinematic -- see BuildHierarchyPatches
            // for why several bones need a different Transform parent
            // while ragdolling than the FBX originally authored.
            ApplyRagdollHierarchy();

            for (int i = 0; i < ragdollBodies.Length; i++)
            {
                ragdollBodies[i].isKinematic = false;
            }

            hipsRigidbody.linearVelocity = Vector3.zero;
            hipsRigidbody.angularVelocity = Vector3.zero;
            hipsRigidbody.AddForce(direction * force, ForceMode.Impulse);
        }

        private void EndRagdoll()
        {
            // Carry the player's logical position to wherever the ragdoll
            // actually ended up (it can roll/slide well away from the
            // impact point) rather than snapping back to where they got
            // hit -- a raycast down finds the real ground height there
            // instead of trusting the ragdoll's own (possibly mid-air or
            // sunk-into-the-floor) Y position.
            Vector3 landed = hipsRigidbody.position;
            if (Physics.Raycast(landed + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 20f))
            {
                landed.y = hit.point.y;
            }
            transform.position = landed;

            // Re-level pitch/roll -- keep facing, just stand back up.
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

            // Must happen after reading hipsRigidbody.position above (Hips
            // is itself one of the patched bones -- restoring it snaps its
            // local pose back to rest) and before the loops below, whose
            // cached rest values are relative to each bone's *original*
            // parent.
            RestoreOriginalHierarchy();

            for (int i = 0; i < ragdollBodies.Length; i++)
            {
                ragdollBodies[i].linearVelocity = Vector3.zero;
                ragdollBodies[i].angularVelocity = Vector3.zero;
                ragdollBodies[i].transform.localPosition = restLocalPositions[i];
                ragdollBodies[i].transform.localRotation = restLocalRotations[i];
                ragdollBodies[i].isKinematic = true;
            }

            if (bridgeBones != null)
            {
                for (int i = 0; i < bridgeBones.Length; i++)
                {
                    if (bridgeBones[i] == null) continue;
                    bridgeBones[i].localPosition = bridgeRestLocalPositions[i];
                    bridgeBones[i].localRotation = bridgeRestLocalRotations[i];
                }
            }
        }

        private void BeginThirdPersonView(Quaternion rigYaw)
        {
            if (cameraTransform == null) return;

            cameraOriginalParent = cameraTransform.parent;
            cameraOriginalLocalPosition = cameraTransform.localPosition;
            cameraOriginalLocalRotation = cameraTransform.localRotation;

            // Rotated once, at the moment of impact -- kept fixed for the
            // whole stun so the camera holds a stable facing/distance
            // rather than re-orbiting as the ragdoll tumbles and rotates.
            fixedCameraOffset = rigYaw * thirdPersonOffset;

            cameraTransform.SetParent(null, true);

            // Start exactly at the ideal framing (no lerp yet) -- the
            // follow-lag in UpdateThirdPersonView only needs to kick in
            // once the target actually starts moving away from here.
            cameraTransform.position = hipsRigidbody.position + fixedCameraOffset;

            // Turn the skin layer back ON for this camera, just for the
            // cutaway -- otherwise the same Culling Mask that hides your
            // body during normal play would also hide it here, the one
            // moment you're actually meant to see it.
            if (playerCamera != null)
            {
                cameraOriginalCullingMask = playerCamera.cullingMask;
                playerCamera.cullingMask |= skinSpawner.SkinLayer.value;
            }
        }

        private void UpdateThirdPersonView()
        {
            if (cameraTransform == null) return;

            // Chases the target's current offset position, but with
            // exponential lag (not a perfect every-frame snap) -- an
            // instant-tracking camera cancels out any visible sign of real
            // travel, since the character stays glued to the same spot in
            // frame while the camera does all the moving with it. Lagging
            // behind means the camera still visibly follows (doesn't feel
            // static), but the character can be seen actually pulling away
            // from/across frame as it travels, rather than staying frozen
            // in the same spot on screen the whole time.
            Vector3 desiredPosition = hipsRigidbody.position + fixedCameraOffset;
            cameraTransform.position = Vector3.Lerp(cameraTransform.position, desiredPosition, cameraFollowSpeed * Time.deltaTime);

            Vector3 lookTarget = hipsRigidbody.position + Vector3.up * lookAtHeightOffset;
            cameraTransform.rotation = Quaternion.LookRotation((lookTarget - cameraTransform.position).normalized, Vector3.up);
        }

        private void EndThirdPersonView()
        {
            if (cameraTransform == null) return;

            cameraTransform.SetParent(cameraOriginalParent, false);
            cameraTransform.localPosition = cameraOriginalLocalPosition;
            cameraTransform.localRotation = cameraOriginalLocalRotation;

            if (playerCamera != null) playerCamera.cullingMask = cameraOriginalCullingMask;
        }
    }
}
