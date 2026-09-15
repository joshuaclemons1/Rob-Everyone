using RobEveryone.Customization;
using UnityEngine;
using UnityEngine.UI;

namespace RobEveryone.UI
{
    // Issue #39 (2/3): the character preview, persistent across every
    // Main Menu panel (Main/Play/Customize/Settings) instead of only
    // existing while CustomizationUI's own panel happens to be active --
    // see main-menu-visual-design.md's "persists across Main, Lobby, and
    // Customization" requirement, which the old CustomizationUI-owned
    // version never actually met (it only spawned in OnEnable, so it
    // only ever existed while the Customize panel itself was active).
    //
    // Deliberately its own isolated camera stage rendering to a
    // RenderTexture -- the exact same "far away in world space,
    // dedicated layer, dedicated camera, only that camera ever renders
    // it" pattern HotbarSlotUI already established (and already proven
    // working) for item previews, just showing a full character instead
    // of a spinning item. Deliberately NOT sharing whatever camera
    // renders the background behind the canvas (today: a static
    // diorama; see #51, planned: a moving drone shot) -- if the preview
    // rode along on that same camera, #51's own work would drag the
    // character around with it every time that camera moves. This way
    // #51 can replace or move that camera however it wants without ever
    // touching this.
    //
    // Lives on MenuManager (a root-level object, never SetActive'd by
    // panel switching) rather than under any one panel, so it survives
    // every Main/Play/Customize/Settings transition instead of being
    // torn down and rebuilt every time.
    public class MenuCharacterPreview : MonoBehaviour
    {
        private const string PreviewLayerName = "MenuPreview";
        private const int PreviewTextureSize = 1024;

        [SerializeField] private PlayerSkinRoster skinRoster;
        [SerializeField] private PlayerColorPalette palette;
        // The UI element that actually displays the RenderTexture --
        // needs to live outside the panel hierarchy (a sibling of
        // MainMenuPanel/CustomizePanel/SettingsPanel/PlayPanel, in the
        // character-preview safe zone from main-menu-visual-design.md)
        // so it stays visible across every panel switch. See this
        // issue's own setup doc for exactly where to place it -- not
        // something to get right by guessing blind at RectTransform
        // values with no way to actually see the result.
        [SerializeField] private RawImage previewImage;
        [SerializeField] private float previewPadding = 1.15f;

        private Transform modelAnchor;
        private GameObject modelInstance;
        private PlayerColorizer modelColorizer;
        private Camera previewCamera;
        private RenderTexture renderTexture;

        // Exposed for MenuActions' Settings fall-through/fall-from-sky
        // animation to move -- that's the only thing outside this class
        // that ever needs to touch the preview's Transform directly.
        public Transform PreviewTransform => modelAnchor;

        private void Awake()
        {
            BuildStage();
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

        private void OnDestroy()
        {
            if (renderTexture != null) renderTexture.Release();
        }

        private void BuildStage()
        {
            int layer = LayerMask.NameToLayer(PreviewLayerName);
            if (layer < 0)
            {
                Debug.LogWarning($"MenuCharacterPreview: layer '{PreviewLayerName}' doesn't exist -- add it in Project Settings > Tags and Layers.");
                return;
            }

            // Far outside normal play space -- same reasoning as
            // HotbarSlotUI's own stages. Physically irrelevant to every
            // OTHER camera regardless (none of them include this layer
            // in their culling mask), but parking it well clear of real
            // geometry avoids any chance of a stray light/reflection
            // probe picking it up.
            var stageRoot = new GameObject("MenuPreviewStage");
            stageRoot.transform.position = new Vector3(0f, 5000f, 0f);
            stageRoot.layer = layer;

            modelAnchor = new GameObject("ModelAnchor").transform;
            modelAnchor.SetParent(stageRoot.transform, false);
            modelAnchor.gameObject.layer = layer;

            var cameraGO = new GameObject("PreviewCamera");
            cameraGO.transform.SetParent(stageRoot.transform, false);
            cameraGO.transform.localPosition = new Vector3(0f, 0f, -4f);
            cameraGO.layer = layer;

            previewCamera = cameraGO.AddComponent<Camera>();
            previewCamera.orthographic = true;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            previewCamera.cullingMask = 1 << layer;

            renderTexture = new RenderTexture(PreviewTextureSize, PreviewTextureSize, 16) { name = "MenuPreviewRT" };
            previewCamera.targetTexture = renderTexture;
            if (previewImage != null) previewImage.texture = renderTexture;
        }

        private void Refresh()
        {
            SpawnModel();
            ApplyColor();
        }

        private void SpawnModel()
        {
            if (modelAnchor == null || skinRoster == null || skinRoster.Count == 0) return;

            GameObject prefab = skinRoster.GetSkin(PlayerCosmeticSelection.SkinIndex);
            if (prefab == null) return;

            if (modelInstance != null) Destroy(modelInstance);

            modelInstance = Instantiate(prefab, modelAnchor);
            modelInstance.transform.localPosition = Vector3.zero;
            // The preview camera sits in front of the model looking down
            // +Z (it's parked at local Z -4 with an otherwise identity
            // rotation, so its forward points back toward the origin).
            // A character's own authored forward is also +Z, so spawning
            // at identity rotation faces it away from the camera --
            // confirmed bug, the model showed its back. 180 around Y
            // turns it to face the camera instead.
            modelInstance.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            SetLayerRecursively(modelInstance, modelAnchor.gameObject.layer);

            modelColorizer = modelInstance.GetComponent<PlayerColorizer>();
            if (modelColorizer == null) modelColorizer = modelInstance.AddComponent<PlayerColorizer>();

            FrameCamera(CalculateBounds(modelInstance));
        }

        private void ApplyColor()
        {
            if (modelColorizer == null || palette == null || palette.Colors.Count == 0) return;

            int colorIndex = Mathf.Clamp(PlayerCosmeticSelection.ColorIndex, 0, palette.Colors.Count - 1);
            modelColorizer.ApplyBodyColor(palette.Colors[colorIndex]);
        }

        // Same reasoning as HotbarSlotUI.FrameCamera -- fits the
        // orthographic size to whatever the model's actual bounds turn
        // out to be, so this works for any skin's proportions without
        // per-skin tuning. Framed on the vertical extent specifically
        // (not the whole bounding sphere, unlike HotbarSlotUI's own
        // small spinning items) -- a standing character is much taller
        // than wide, and framing to height keeps the top of the head and
        // feet from getting clipped the way a sphere-radius fit would.
        private void FrameCamera(Bounds bounds)
        {
            if (previewCamera == null) return;

            float halfHeight = Mathf.Max(bounds.extents.y, 0.5f);
            previewCamera.orthographicSize = halfHeight * previewPadding;

            float clipRadius = bounds.extents.magnitude;
            float distance = clipRadius * 2f + 1f;
            previewCamera.nearClipPlane = Mathf.Max(0.01f, distance - clipRadius * 1.5f);
            previewCamera.farClipPlane = distance + clipRadius * 1.5f;

            // Recenter vertically on the model's own bounds center
            // rather than assuming its authored origin sits at its
            // visual center -- a standing character's root is at its
            // feet (ground level, see PlayerSkinSpawner's own comment on
            // this), so without this the camera would frame roughly the
            // lower half of the model instead of the whole thing.
            Vector3 localCenter = modelAnchor.InverseTransformPoint(bounds.center);
            previewCamera.transform.localPosition = new Vector3(0f, localCenter.y, -distance);
        }

        private static Bounds CalculateBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.one);

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform) SetLayerRecursively(child.gameObject, layer);
        }
    }
}
