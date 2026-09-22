using System;
using System.Collections.Generic;
using Mirror;
using RobEveryone.Customization;
using UnityEngine;

namespace RobEveryone.Player
{
    // Instantiates whichever skin/color the player picked in the main
    // menu (PlayerCosmeticSelection) as a child of this Transform at
    // gameplay start -- reads the same PlayerSkinRoster/PlayerColorPalette
    // assets CustomizationUI's menu preview uses, so there's one shared
    // list rather than gameplay hardcoding a specific character.
    //
    // Networking (Stage 4): PlayerCosmeticSelection is a local PlayerPrefs
    // read -- only the *owner* actually knows their own choice. The
    // owner spawns its own skin immediately (no need to wait on a round
    // trip just to see yourself correctly) and also tells the server via
    // Command; the server stores it in SyncVars, which is what every
    // *other* client's copy of this same object reads via the hooks below
    // to spawn the correct skin for a player that isn't them.
    public class PlayerSkinSpawner : NetworkBehaviour
    {
        [SerializeField] private PlayerSkinRoster skinRoster;
        [SerializeField] private PlayerColorPalette palette;
        [SerializeField] private LayerMask skinLayer;
        // On the owner's own copy only, these bones get scaled to zero so
        // the first-person camera (which renders your body now -- it's not
        // a floating nothing, and it's the anchor for the future "held
        // hotbar item in your hands") doesn't clip through your own model.
        // Grounded = just the head (you can still look down and see your
        // torso/arms/legs); airborne = the whole upper body too, since
        // the jump animation's spring pushes it into the camera. Every
        // other client's copy of you keeps the full model. Exact
        // bone-name match; Quaternius names them Head / Torso.
        [SerializeField] private string[] firstPersonHiddenBonesGrounded = { "Head", "Head_end" };
        [SerializeField] private string[] firstPersonHiddenBonesAirborne = { "Head", "Head_end", "Torso" };
        // Shared across every skin -- only BaseCharacter.fbx actually has
        // baked-in clips (Idle/Walk/Run/Jump/etc.), the other 51 are bare
        // meshes on the *identical* Generic rig topology, so one
        // Animator Controller referencing BaseCharacter's clips binds and
        // plays correctly on any of them by hierarchy path.
        [SerializeField] private RuntimeAnimatorController playerAnimatorController;

        [SyncVar(hook = nameof(OnCosmeticsChanged))] private int syncedSkinIndex = -1;
        [SyncVar(hook = nameof(OnCosmeticsChanged))] private int syncedColorIndex = -1;

        public GameObject SkinInstance { get; private set; }
        // PlayerRagdoll needs this to toggle PlayerCamera's Culling Mask
        // during the stun -- kept as the single source of truth here
        // rather than a second, separately-configured field there.
        public LayerMask SkinLayer => skinLayer;
        // VoicePortraitPool reads these to spawn a matching head portrait
        // for whoever's currently talking -- read-only, the SyncVars
        // themselves stay private since nothing outside this class
        // should ever set them directly.
        public int SkinIndex => syncedSkinIndex;
        public int ColorIndex => syncedColorIndex;

        // Issue #52 (Phase 1): fired once a rebuilt skin instance is
        // fully set up (same point BuildSkinInstance finishes its own
        // setup, first spawn or later swap alike). Every script that
        // caches a reference *into* SkinInstance (PlayerAnimationDriver's
        // Animator, PlayerRagdoll's ragdoll bodies, PlayerHeadTalkScale/
        // HeldItemDisplay's bone lookups) subscribes and re-resolves its
        // own cached reference here, instead of PlayerSkinSpawner needing
        // hardcoded knowledge of every consumer -- same event-driven
        // decoupling PlayerCosmeticSelection.OnChanged already uses
        // successfully elsewhere in this project.
        public event Action OnSkinRebuilt;

        // The skin/color combination currently actually displayed --
        // distinct from syncedSkinIndex/syncedColorIndex themselves,
        // which OnCosmeticsChanged compares against to tell "this SyncVar
        // update is just confirming what I already applied locally"
        // (the owner's own initial spawn, applied synchronously in
        // OnStartLocalPlayer before the Command round-trips back) apart
        // from "this is a genuine new change" (any later swap, whether
        // triggered by this client or, for an observer, by someone
        // else's).
        private int displayedSkinIndex = -1;
        private int displayedColorIndex = -1;

        public override void OnStartLocalPlayer()
        {
            int skinIndex = PlayerCosmeticSelection.SkinIndex;
            int colorIndex = PlayerCosmeticSelection.ColorIndex;

            BuildSkinInstance(skinIndex, colorIndex);
            CmdSetCosmetics(skinIndex, colorIndex);
        }

        [Command]
        private void CmdSetCosmetics(int skinIndex, int colorIndex)
        {
            syncedSkinIndex = skinIndex;
            syncedColorIndex = colorIndex;
        }

        // Fires whenever either SyncVar changes -- the initial sync for
        // every client (including a late joiner, who gets the
        // then-current value immediately) and every later live swap
        // alike (issue #52's SkinOfferPedestal/MirrorSkinCycleButton
        // both apply their result server-side first and rely entirely on
        // this hook to tell every client, owner included, rather than a
        // separate owner-specific path). The displayedSkinIndex/
        // displayedColorIndex check above is what actually distinguishes
        // "just confirming what I already applied" from "a genuine
        // change" -- not a blanket isOwned skip, which would also
        // incorrectly skip a later swap the owner didn't trigger through
        // OnStartLocalPlayer's own synchronous path.
        private void OnCosmeticsChanged(int _, int _2)
        {
            if (syncedSkinIndex < 0 || syncedColorIndex < 0) return;
            if (syncedSkinIndex == displayedSkinIndex && syncedColorIndex == displayedColorIndex) return;

            if (SkinInstance == null) BuildSkinInstance(syncedSkinIndex, syncedColorIndex);
            else RebuildSkin(syncedSkinIndex, syncedColorIndex);

            // Only the owner's own choice should ever persist to their
            // own local PlayerPrefs -- an observer reacting to someone
            // else's skin change has nothing of their own to remember
            // here.
            if (isOwned)
            {
                PlayerCosmeticSelection.SkinIndex = syncedSkinIndex;
                PlayerCosmeticSelection.ColorIndex = syncedColorIndex;
            }
        }

        private void BuildSkinInstance(int skinIndex, int colorIndex)
        {
            if (SkinInstance != null) return; // already spawned -- RebuildSkin below is the actual "replace it" path
            if (skinRoster == null || skinRoster.Count == 0) return;

            GameObject prefab = skinRoster.GetSkin(skinIndex);
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
            ConfigureAnimator(SkinInstance.transform);

            // Owner-only: trim the head (and, airborne, the upper body)
            // so first person isn't a floating camera and doesn't clip
            // through the model. isOwned is reliable here regardless of
            // whether this is the owner's very first spawn or a later
            // live swap -- both go through this same method.
            if (isOwned)
            {
                SkinInstance.AddComponent<FirstPersonBodyTrim>()
                    .Apply(SkinInstance.transform, firstPersonHiddenBonesGrounded, firstPersonHiddenBonesAirborne);
            }

            PlayerColorizer colorizer = SkinInstance.GetComponent<PlayerColorizer>();
            if (colorizer == null) colorizer = SkinInstance.AddComponent<PlayerColorizer>();

            if (palette != null && palette.Colors.Count > 0 && colorIndex >= 0 && colorIndex < palette.Colors.Count)
            {
                colorizer.ApplyBodyColor(palette.Colors[colorIndex]);
            }

            displayedSkinIndex = skinIndex;
            displayedColorIndex = colorIndex;

            OnSkinRebuilt?.Invoke();
        }

        // Issue #52 (Phase 1): tears down the current skin and builds a
        // fresh one in its place -- unlike BuildSkinInstance (guarded,
        // first-spawn only), this is the actual "swap live" path a
        // pedestal/mirror interaction needs. Reuses BuildSkinInstance for
        // the actual instantiate/setup work rather than duplicating it --
        // clearing SkinInstance first is what lets that guard fall
        // through instead of no-op'ing.
        private void RebuildSkin(int skinIndex, int colorIndex)
        {
            if (SkinInstance != null)
            {
                Destroy(SkinInstance);
                SkinInstance = null;
            }

            BuildSkinInstance(skinIndex, colorIndex);
        }

        // ---- issue #52: pedestal / paint-can / mirror interactions ----

        // Called by PaintCan.Interact() (server-side, via the standard
        // IInteractable/Interactor flow -- confirmed Interact() itself
        // always runs server-side, never on a client). Colors are always
        // fully available with no unlock gating, so this applies
        // immediately -- the SyncVar write above is what makes every
        // client (including the interactor's own) pick it up via
        // OnCosmeticsChanged, no separate round trip back to the
        // interactor needed the way the two methods below require.
        [Server]
        public void ServerSwapCosmetics(int? skinIndex, int? colorIndex)
        {
            if (skinIndex.HasValue) syncedSkinIndex = skinIndex.Value;
            if (colorIndex.HasValue) syncedColorIndex = colorIndex.Value;
        }

        // Called by SkinOfferPedestal.Interact() (server-side). Unlocking
        // is purely local PlayerPrefs state (PlayerSkinUnlocks), which
        // only the interactor's own client can actually write -- doesn't
        // touch syncedSkinIndex/syncedColorIndex at all, since unlocking
        // a skin doesn't equip it (see the cycle methods below for the
        // actual equip path, via the mirror).
        [Server]
        public void ServerNotifySkinUnlocked(int skinIndex)
        {
            TargetNotifySkinUnlocked(connectionToClient, skinIndex);
        }

        [TargetRpc]
        private void TargetNotifySkinUnlocked(NetworkConnectionToClient target, int skinIndex)
        {
            PlayerSkinUnlocks.Unlock(skinIndex);
        }

        // Called by MirrorSkinCycleButton.Interact() (server-side).
        // "Which skin is next" depends on the interactor's own unlocked
        // set, which -- same as above -- only their own client actually
        // has, so this asks their client to work it out and report back
        // rather than the server guessing at something it can't see.
        [Server]
        public void ServerRequestSkinCycle(bool forward)
        {
            TargetComputeSkinCycle(connectionToClient, forward);
        }

        [TargetRpc]
        private void TargetComputeSkinCycle(NetworkConnectionToClient target, bool forward)
        {
            CmdApplySkinCycle(ComputeCycledSkinIndex(forward));
        }

        [Command]
        private void CmdApplySkinCycle(int skinIndex) => ServerSwapCosmetics(skinIndex, null);

        // Cyclic, wrapping at both ends -- same shape as the old Main
        // Menu Customization screen's own ChangeSkin, just walking
        // PlayerSkinUnlocks.UnlockedSkins (this player's own accumulated
        // set) instead of the full roster. If the currently-selected
        // skin somehow isn't in the unlocked list at all (shouldn't
        // normally happen -- everything selected got there by being
        // unlocked first), starts from the beginning rather than
        // throwing.
        private static int ComputeCycledSkinIndex(bool forward)
        {
            IReadOnlyList<int> unlocked = PlayerSkinUnlocks.UnlockedSkins;
            if (unlocked.Count == 0) return PlayerCosmeticSelection.SkinIndex; // shouldn't happen -- 0 is always included

            int currentPosition = 0;
            for (int i = 0; i < unlocked.Count; i++)
            {
                if (unlocked[i] == PlayerCosmeticSelection.SkinIndex)
                {
                    currentPosition = i;
                    break;
                }
            }

            int delta = forward ? 1 : -1;
            int nextPosition = (currentPosition + delta + unlocked.Count) % unlocked.Count;
            return unlocked[nextPosition];
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

        // Quaternius FBX imports come with an Animator/Avatar of their own,
        // but only pointed at whatever (if anything) that specific
        // character's own prefab happened to have configured -- assigning
        // the shared playerAnimatorController here means every skin plays
        // the same Idle/Walk/Run/Jump state machine regardless of which of
        // the 52 was picked, instead of needing it hand-configured 52
        // times. Left enabled now that PlayerAnimationDriver actually
        // drives it -- an earlier version of this project disabled the
        // Animator entirely, back when nothing did, since a driverless
        // Animator just fights ragdoll physics (Animator.Update()
        // overwriting a bone's physics-computed Transform every frame).
        // PlayerRagdoll disables this same component for the duration of
        // a stun for exactly that reason.
        private void ConfigureAnimator(Transform root)
        {
            foreach (Animator animator in root.GetComponentsInChildren<Animator>(true))
            {
                if (playerAnimatorController != null) animator.runtimeAnimatorController = playerAnimatorController;

                // Animator's default culling skips writing bone poses
                // entirely once it decides nothing is rendering this
                // Animator's mesh to any Camera -- and the owner's own
                // Camera deliberately excludes this exact layer (that's
                // the whole "you don't see your own body" mechanism), so
                // with no other camera in the scene rendering it yet
                // (multiplayer observers are Stage 4+), Unity would
                // otherwise conclude this Animator is never visible and
                // stop posing it -- the state machine keeps "running"
                // regardless, which is why it can still show a state
                // looping in the Animator window while every bone stays
                // frozen at bind pose. AlwaysAnimate forces posing
                // regardless of camera visibility.
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
        }

        // Every skin (yours and everyone else's) goes on the ordinary
        // always-rendered Default layer now -- your own body is meant to
        // be visible in first person (trimmed, see FirstPersonBodyTrim).
        // `skinLayer` is kept as a field for
        // PlayerRagdoll/PlayerCameraRig's culling-mask toggle, which is
        // now a harmless no-op since Default is always in the mask
        // anyway; left in place rather than ripping it out of two other
        // scripts for no functional gain.
        private void SetLayerRecursively(Transform root)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = 0; // Default
            }
        }
    }
}
