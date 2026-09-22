using UnityEngine;

namespace RobEveryone.Customization
{
    // Replaces the live reflection-camera mirror (MirrorReflectionCamera,
    // removed -- see git history for the full debugging saga) with a
    // static idle preview of the player's own currently-selected skin,
    // standing "inside" the mirror frame. Same proven pattern
    // MenuCharacterPreview already uses for the Main Menu (isolated
    // stage on its own layer, dedicated camera, spawn/color/idle the
    // model, refresh on PlayerCosmeticSelection.OnChanged) -- just
    // pointed at the mirror's own already-working RenderTexture/
    // Material/Quad chain instead of a UI RawImage. Sidesteps every
    // geometry/depth/culling problem a true planar reflection has: there's
    // no real scene being captured at all, just a dressed mannequin on an
    // isolated stage nobody else ever renders.
    public class MirrorCharacterPreview : MonoBehaviour
    {
        private const string PreviewLayerName = "MirrorPreview";

        [SerializeField] private PlayerSkinRoster skinRoster;
        [SerializeField] private PlayerColorPalette palette;
        [SerializeField] private RuntimeAnimatorController playerAnimatorController;

        // The mirror's existing camera, repurposed -- already correctly
        // wired to MirrorTexture (Target Texture) and displayed via
        // MirrorMaterial on the mirror Quad from the reflection-camera
        // setup. Only its culling mask changes here (set below, to
        // isolate it to this stage); the RenderTexture/Material/Quad
        // chain downstream of it is untouched and still correct.
        [SerializeField] private Camera previewCamera;

        // Where the mannequin stands and which way it faces "into" the
        // frame -- position and rotate this in the Editor so it's a few
        // units in front of previewCamera, inside the mirror's opening,
        // facing back toward the camera. Independent of the mirror
        // Quad's own Transform.
        [SerializeField] private Transform modelSpawnPoint;

        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int GroundedParam = Animator.StringToHash("Grounded");
        private static readonly int CarryingParam = Animator.StringToHash("Carrying");

        private GameObject modelInstance;
        private PlayerColorizer modelColorizer;

        private void Awake()
        {
            int layer = LayerMask.NameToLayer(PreviewLayerName);
            if (layer < 0)
            {
                Debug.LogWarning($"MirrorCharacterPreview: layer '{PreviewLayerName}' doesn't exist -- add it in Project Settings > Tags and Layers.");
                return;
            }

            if (previewCamera != null) previewCamera.cullingMask = 1 << layer;
        }

        private void OnEnable()
        {
            PlayerCosmeticSelection.OnChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            PlayerCosmeticSelection.OnChanged -= Refresh;
        }

        private void Refresh()
        {
            SpawnModel();
            ApplyColor();
        }

        private void SpawnModel()
        {
            if (modelSpawnPoint == null || skinRoster == null || skinRoster.Count == 0) return;

            GameObject prefab = skinRoster.GetSkin(PlayerCosmeticSelection.SkinIndex);
            if (prefab == null) return;

            if (modelInstance != null) Destroy(modelInstance);

            // Identity local rotation -- unlike MenuCharacterPreview's
            // fixed 180-degree flip (which assumes its camera's own fixed
            // local position/rotation relative to the model), this stage's
            // camera and spawn point are both freely Editor-placed, so
            // whichever way modelSpawnPoint itself faces is what the
            // model faces. Rotate modelSpawnPoint in the Editor to aim
            // the model at previewCamera, not this.
            modelInstance = Instantiate(prefab, modelSpawnPoint);
            modelInstance.transform.localPosition = Vector3.zero;
            modelInstance.transform.localRotation = Quaternion.identity;
            SetLayerRecursively(modelInstance, LayerMask.NameToLayer(PreviewLayerName));

            modelColorizer = modelInstance.GetComponent<PlayerColorizer>();
            if (modelColorizer == null) modelColorizer = modelInstance.AddComponent<PlayerColorizer>();

            ConfigureAnimator(modelInstance.transform);
        }

        // Same reasoning as MenuCharacterPreview.ConfigureAnimator -- an
        // unassigned Animator Controller leaves the model at bind pose,
        // and Unity's default culling would otherwise freeze the
        // animation entirely since this stage's isolated camera is the
        // only thing that ever renders it.
        private void ConfigureAnimator(Transform root)
        {
            if (playerAnimatorController == null) return;

            foreach (Animator animator in root.GetComponentsInChildren<Animator>(true))
            {
                animator.runtimeAnimatorController = playerAnimatorController;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                animator.SetFloat(SpeedParam, 0f);
                animator.SetBool(GroundedParam, true);
                animator.SetBool(CarryingParam, false);
            }
        }

        private void ApplyColor()
        {
            if (modelColorizer == null || palette == null || palette.Colors.Count == 0) return;

            int colorIndex = Mathf.Clamp(PlayerCosmeticSelection.ColorIndex, 0, palette.Colors.Count - 1);
            modelColorizer.ApplyBodyColor(palette.Colors[colorIndex]);
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform) SetLayerRecursively(child.gameObject, layer);
        }
    }
}
