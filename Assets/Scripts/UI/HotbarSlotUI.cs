using System.Collections;
using RobEveryone.Inventory;
using RobEveryone.Items;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RobEveryone.UI
{
    // One box in the hotbar. Shows a live, slowly-spinning 3D render of
    // the carried item's WorldModelPrefab -- falls back to its 2D Icon,
    // then just its name text, if no model is set. Scales up slightly
    // while selected; selecting also briefly spins the model faster
    // before settling back to its idle speed.
    //
    // The spinning render works by instantiating the model into a private
    // "stage" (a camera + an empty anchor transform) placed far outside
    // the playable world on a dedicated layer, so it's invisible to every
    // other camera and only the slot's own preview camera renders it, onto
    // a RenderTexture displayed by `modelImage`. Each slot gets its own
    // stage, offset far apart from every other slot's, so they never see
    // into each other despite sharing one culling layer.
    public class HotbarSlotUI : MonoBehaviour
    {
        private const string PreviewLayerName = "ItemPreview";
        private const int PreviewTextureSize = 256;
        private const float StageSpacing = 50f;

        [SerializeField] private TextMeshProUGUI itemText;
        // Shown only for an item with tracked durability/ammo
        // (ItemDefinition.MaxUses > 0 -- Bat, Hammer, Tranquilizer Gun);
        // hidden otherwise, same as every other item shown here today.
        [SerializeField] private TextMeshProUGUI usesText;
        [SerializeField] private Image iconImage;
        [SerializeField] private RawImage modelImage;
        // Multiplier on the model's own bounding-sphere radius -- the
        // camera reframes itself to this every time a slot's item
        // changes, so it always fits regardless of the model's actual
        // size. >1 leaves breathing room around the model.
        [SerializeField] private float previewPadding = 1.2f;
        [SerializeField] private float selectedScale = 1.15f;
        [SerializeField] private float normalSpinSpeed = 30f; // degrees/sec
        [SerializeField] private float fastSpinSpeed = 240f;
        [SerializeField] private float fastSpinDuration = 1f;
        [SerializeField] private float spinRampDuration = 0.3f;

        private static int nextStageIndex;

        private Transform modelAnchor;
        private Transform modelPivot;
        private GameObject modelInstance;
        private Camera previewCamera;
        private RenderTexture renderTexture;
        private float currentSpinSpeed;
        private Coroutine spinBoostCoroutine;

        // For merging a bulky item's boxes into one wide rectangle (see
        // SetSpan) -- this prefab lays each slot out with its own fixed
        // anchoredPosition/sizeDelta (Slot0..Slot4 spaced by hand), not a
        // Horizontal Layout Group, so there's no layout system to hand a
        // preferred width to. Instead each slot captures its own
        // originally-authored center X and width once in Awake, and
        // HotbarUI reads those (BaseAnchoredX/BaseWidth) off every slot a
        // bulky item covers to compute exactly where its merged box
        // should sit and how wide it should be.
        private RectTransform rectTransform;
        private float baseAnchoredX;
        private float baseWidth;

        public float BaseAnchoredX => baseAnchoredX;
        public float BaseWidth => baseWidth;

        private void Awake()
        {
            currentSpinSpeed = normalSpinSpeed;
            if (modelImage != null) BuildPreviewStage();

            rectTransform = GetComponent<RectTransform>();
            baseAnchoredX = rectTransform.anchoredPosition.x;
            baseWidth = rectTransform.sizeDelta.x;

            // Clear whatever placeholder name/uses text the prefab (or a
            // duplicated slot, e.g. the wallet box) shipped with, so a
            // box shows nothing until something binds an item to it.
            SetItem(null);
        }

        // Resizes and repositions this box to cover a span of physical
        // slots -- centerAnchoredX/width are computed by HotbarUI from
        // this slot's own and its covered siblings' BaseAnchoredX/
        // BaseWidth, so the merged box lines up exactly with the slots
        // it's replacing regardless of their spacing. Passing this same
        // slot's own base geometry back resets it to a normal, unmerged
        // single slot.
        public void SetSpan(float centerAnchoredX, float width)
        {
            Vector2 pos = rectTransform.anchoredPosition;
            pos.x = centerAnchoredX;
            rectTransform.anchoredPosition = pos;

            Vector2 size = rectTransform.sizeDelta;
            size.x = width;
            rectTransform.sizeDelta = size;
        }

        private void BuildPreviewStage()
        {
            int layer = LayerMask.NameToLayer(PreviewLayerName);
            if (layer < 0)
            {
                Debug.LogWarning($"HotbarSlotUI: layer '{PreviewLayerName}' doesn't exist -- add it in Project Settings > Tags and Layers for item model previews to render.");
                return;
            }

            // Far out in world space and spaced well clear of every other
            // slot's stage, so each preview camera only ever sees its own
            // model despite sharing one culling layer.
            float stageOffset = nextStageIndex++ * StageSpacing;
            var stageRoot = new GameObject($"HotbarPreviewStage_{stageOffset}");
            stageRoot.transform.position = new Vector3(stageOffset, 5000f, 0f);
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

            renderTexture = new RenderTexture(PreviewTextureSize, PreviewTextureSize, 16) { name = "HotbarPreviewRT" };
            previewCamera.targetTexture = renderTexture;
            modelImage.texture = renderTexture;
        }

        // The live spinning-model RenderTexture for whatever this slot is
        // currently showing, or null if it's showing an icon / is empty.
        // InventoryScreenUI's drag ghost reuses it so the thing you drag
        // is a live mini-render of the item, not a blank box.
        public Texture PreviewTexture => modelInstance != null && renderTexture != null ? renderTexture : null;

        public void SetItem(InventorySlot? slot)
        {
            ItemDefinition item = slot.HasValue ? slot.Value.Item : null;

            if (itemText != null) itemText.text = item != null ? item.ItemName : "";

            if (usesText != null)
            {
                bool showUses = item != null && item.MaxUses > 0;
                usesText.text = showUses ? $"x{slot.Value.RemainingUses}" : "";
                usesText.enabled = showUses;
            }

            SetModel(item != null ? item.WorldModelPrefab : null);
            bool showingModel = modelInstance != null;
            if (modelImage != null) modelImage.enabled = showingModel;

            if (iconImage != null)
            {
                Sprite icon = !showingModel && item != null ? item.Icon : null;
                iconImage.sprite = icon;
                iconImage.enabled = icon != null;
            }
        }

        private void SetModel(GameObject prefab)
        {
            if (modelPivot != null)
            {
                Destroy(modelPivot.gameObject);
                modelPivot = null;
            }
            modelInstance = null;

            if (prefab == null || modelAnchor == null) return;

            // The pivot is what actually spins, sitting at a fixed point
            // in the stage -- the model goes inside it, but shifted so its
            // *visual* center lines up with the pivot's origin, since a
            // mesh's authored origin (what Rotate would otherwise spin
            // around) is often a corner or base, not the center.
            modelPivot = new GameObject("ModelPivot").transform;
            modelPivot.SetParent(modelAnchor, false);
            modelPivot.gameObject.layer = modelAnchor.gameObject.layer;

            modelInstance = Instantiate(prefab, modelPivot);
            modelInstance.transform.localPosition = Vector3.zero;
            modelInstance.transform.localRotation = Quaternion.identity;
            SetLayerRecursively(modelInstance, modelAnchor.gameObject.layer);

            Bounds bounds = CalculateBounds(modelInstance);
            Vector3 recenterOffset = modelPivot.position - bounds.center;
            modelInstance.transform.position += recenterOffset;

            FrameCamera(bounds.extents.magnitude);
        }

        // Reframes the preview camera to the model's own bounding-sphere
        // radius (rotation-invariant, unlike using width/height directly,
        // so it still fits no matter which way the model has spun) --
        // both the camera's distance and its clip planes move with it, so
        // a tiny item isn't left floating in an oversized view and a big
        // one doesn't get clipped.
        private void FrameCamera(float radius)
        {
            if (previewCamera == null) return;
            if (radius <= 0f) radius = 0.5f;

            previewCamera.orthographicSize = radius * previewPadding;

            float distance = radius * 2f + 1f;
            previewCamera.transform.localPosition = new Vector3(0f, 0f, -distance);
            previewCamera.nearClipPlane = Mathf.Max(0.01f, distance - radius * 1.5f);
            previewCamera.farClipPlane = distance + radius * 1.5f;
        }

        private static Bounds CalculateBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.zero);

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
            return bounds;
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        public void SetSelected(bool selected)
        {
            transform.localScale = Vector3.one * (selected ? selectedScale : 1f);

            if (selected)
            {
                if (spinBoostCoroutine != null) StopCoroutine(spinBoostCoroutine);
                spinBoostCoroutine = StartCoroutine(SpinBoost());
            }
        }

        private IEnumerator SpinBoost()
        {
            yield return RampSpin(fastSpinSpeed, spinRampDuration);
            yield return new WaitForSeconds(fastSpinDuration);
            yield return RampSpin(normalSpinSpeed, spinRampDuration);
            spinBoostCoroutine = null;
        }

        // Eases currentSpinSpeed from whatever it's at right now toward
        // `target`, rather than jumping -- starting from the live value
        // (not always normal/fast) so re-selecting mid-ramp doesn't pop.
        private IEnumerator RampSpin(float target, float duration)
        {
            float from = currentSpinSpeed;
            if (duration <= 0f)
            {
                currentSpinSpeed = target;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                currentSpinSpeed = Mathf.Lerp(from, target, elapsed / duration);
                yield return null;
            }
            currentSpinSpeed = target;
        }

        private void Update()
        {
            if (modelPivot != null)
            {
                modelPivot.Rotate(0f, currentSpinSpeed * Time.deltaTime, 0f);
            }
        }

        private void OnDestroy()
        {
            if (renderTexture != null) renderTexture.Release();
        }
    }
}
