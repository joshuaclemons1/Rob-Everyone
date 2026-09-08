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

        public override void OnStartLocalPlayer()
        {
            int skinIndex = PlayerCosmeticSelection.SkinIndex;
            int colorIndex = PlayerCosmeticSelection.ColorIndex;

            SpawnSkin(skinIndex, colorIndex);
            CmdSetCosmetics(skinIndex, colorIndex);
        }

        [Command]
        private void CmdSetCosmetics(int skinIndex, int colorIndex)
        {
            syncedSkinIndex = skinIndex;
            syncedColorIndex = colorIndex;
        }

        // Fires on every non-owner client once the server-held values
        // arrive (including late joiners, who get the current SyncVar
        // value immediately on spawn -- no separate "catch up" logic
        // needed). Ignored on the owner's own client, which already
        // spawned itself synchronously in OnStartLocalPlayer above rather
        // than waiting on a round trip to see its own choice.
        private void OnCosmeticsChanged(int _, int _2)
        {
            if (isOwned) return;
            if (syncedSkinIndex < 0 || syncedColorIndex < 0) return;

            SpawnSkin(syncedSkinIndex, syncedColorIndex);
        }

        private void SpawnSkin(int skinIndex, int colorIndex)
        {
            if (SkinInstance != null) return; // already spawned -- both hooks can fire once each, guard against a double-spawn
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

            PlayerColorizer colorizer = SkinInstance.GetComponent<PlayerColorizer>();
            if (colorizer == null) colorizer = SkinInstance.AddComponent<PlayerColorizer>();

            if (palette != null && palette.Colors.Count > 0 && colorIndex >= 0 && colorIndex < palette.Colors.Count)
            {
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
