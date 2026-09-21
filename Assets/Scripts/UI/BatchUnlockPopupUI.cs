using System.Collections;
using System.Collections.Generic;
using RobEveryone.Core;
using RobEveryone.Items;
using RobEveryone.Shop;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RobEveryone.UI
{
    // Issue #61: a real "you got something new" moment for whatever just
    // unlocked at this batch -- "Item Unlocked" plus a big spinning model,
    // shown once the player actually lands in the Lobby (not baked into
    // the loading-screen text the way this first tried it: that ran from
    // GameFlowManager.HandleRoundEnded while the gameplay scene was still
    // active, before the Lobby's own ShopShelfItem instances existed to
    // even ask, so the message was silently always empty -- confirmed via
    // real playtest, never actually seen).
    //
    // Purely client-side, no networking -- same pattern as
    // NightModeVisuals/HomeownerAI's own tint: every client independently
    // reacts off the already-synced GameFlowManager state and its own
    // local copy of the Lobby scene, once that scene has actually
    // finished loading. That fixes the *original* bug's root cause (every
    // ShopShelfItem in this Lobby has already run its own Awake() by the
    // time this can run, so there's nothing left to be empty), but not by
    // itself a second, separate race -- see OnEnable's own comment.
    //
    // Fires on exactly one Lobby visit per batch, per direct request:
    // the Lobby that *precedes* a batch's own final/Night round, not the
    // one after it -- players get a shot at the next tier before their
    // toughest round, not only after they've already gotten through it.
    // RoundInBatch == 3 identifies that window on its own (see
    // GameFlowManager.EffectiveShopBatch's own comment for the full
    // reasoning) -- ShopShelfItem's own Unlocked check reads the same
    // EffectiveShopBatch, so the shelf and this popup always agree on
    // what's newly available.
    public class BatchUnlockPopupUI : MonoBehaviour
    {
        private const string PreviewLayerName = "ItemPreview";
        private const int PreviewTextureSize = 512;
        // HotbarSlotUI's own preview stages start at the world origin and
        // grow along X, spaced 50 apart -- parking this one far away on a
        // different axis means the two can never share a frustum despite
        // both living on the same "ItemPreview" culling layer.
        private static readonly Vector3 StageOrigin = new(0f, -20000f, 0f);

        [SerializeField] private GameObject panel;
        [SerializeField] private TextMeshProUGUI titleText; // "Item Unlocked"
        [SerializeField] private TextMeshProUGUI itemNameText;
        [SerializeField] private RawImage modelImage;
        [SerializeField] private float previewPadding = 1.3f;
        [SerializeField] private float spinSpeed = 40f; // degrees/sec
        [SerializeField] private float secondsPerItem = 3f;

        private Transform modelAnchor;
        private Transform modelPivot;
        private GameObject modelInstance;
        private Camera previewCamera;
        private RenderTexture renderTexture;

        // Guards TryShow below against deciding twice for the same Lobby
        // load -- see OnEnable's own comment for why a single plain
        // Start()-time read isn't safe here.
        private bool hasChecked;

        private void Awake()
        {
            // This component's own GameObject must stay active at all
            // times -- only `panel` (the actual visible content) gets
            // toggled by OnEnable/ShowSequence below, same convention as
            // LoadingScreenUI's own doc comment establishes for exactly
            // this problem. If `panel` is set to this same GameObject,
            // the panel.SetActive(false) call at the top of OnEnable
            // disables the very object this script lives on, which
            // silently kills the StartCoroutine call in TryShow below
            // (Unity won't run a coroutine on an inactive GameObject) --
            // confirmed bug: the popup never appeared at all, regardless
            // of whether anything had actually unlocked. Put this script
            // on an always-active parent/manager object and point `panel`
            // at a separate child instead.
            if (panel == gameObject)
            {
                Debug.LogError("BatchUnlockPopupUI: Panel is set to this component's own GameObject -- " +
                    "disabling it disables this script too, so the popup can never show. Move this component " +
                    "to a separate always-active object and point Panel at the actual content child instead.", this);
            }
        }

        // Issue #61 follow-up: a plain one-shot Start() read of
        // GameFlowManager's synced fields can race GameFlowManager's own
        // spawn/sync timing on a joining/scene-loading client -- the
        // *exact* bug NightModeVisuals already hit and fixed once before
        // (issue #12: GameFlowManager is a scene-placed NetworkIdentity
        // that starts disabled until Mirror's spawn batch reaches this
        // connection, which can land *after* this client's own local
        // scene load finishes -- a one-shot read can latch a stale
        // default with nothing to ever correct it). Reuses the same fix:
        // subscribe to the static OnTimeOfDayChanged event (fires once
        // more, with the real value, the moment GameFlowManager's own
        // sync actually lands, self-correcting an earlier guess) instead
        // of trusting Start() alone.
        private void OnEnable()
        {
            if (panel != null) panel.SetActive(false);
            GameFlowManager.OnTimeOfDayChanged += TryShow;
            TryShow(); // best-effort immediately; corrected later if this guessed wrong
        }

        private void OnDisable()
        {
            GameFlowManager.OnTimeOfDayChanged -= TryShow;
        }

        private void TryShow()
        {
            // Only ever decide once per Lobby load -- OnTimeOfDayChanged
            // firing a second time (the real, corrected value arriving)
            // after an already-correct immediate guess shouldn't re-show
            // the same popup a second time.
            if (hasChecked) return;
            // GameFlowManager.Instance not ready yet is NOT a real "no"
            // -- leave hasChecked false so a later, real firing of the
            // event still gets a chance to decide properly.
            if (GameFlowManager.Instance == null) return;

            hasChecked = true;

            if (GameFlowManager.Instance.RoundInBatch != 3) return;

            List<ItemDefinition> newlyUnlocked = CollectNewlyUnlockedItems(GameFlowManager.Instance.EffectiveShopBatch);
            if (newlyUnlocked.Count == 0) return;

            BuildPreviewStage();
            StartCoroutine(ShowSequence(newlyUnlocked));
        }

        // Every ShopShelfItem gating on this exact effective batch, de-
        // duped by ItemDefinition (not just name) since two shelves could
        // plausibly sell the same item -- HashSet<ItemDefinition> is a
        // reference-identity comparison, which is exactly right for a
        // ScriptableObject asset.
        private static List<ItemDefinition> CollectNewlyUnlockedItems(int effectiveBatch)
        {
            HashSet<ItemDefinition> seen = new();
            List<ItemDefinition> result = new();
            foreach (ShopShelfItem shelf in FindObjectsByType<ShopShelfItem>())
            {
                if (shelf.UnlockBatch != effectiveBatch || shelf.Item == null) continue;
                if (seen.Add(shelf.Item)) result.Add(shelf.Item);
            }
            return result;
        }

        private IEnumerator ShowSequence(List<ItemDefinition> items)
        {
            if (panel != null) panel.SetActive(true);

            foreach (ItemDefinition item in items)
            {
                if (titleText != null) titleText.text = "Item Unlocked";
                if (itemNameText != null) itemNameText.text = item.ItemName;
                SetModel(item.WorldModelPrefab);

                yield return new WaitForSeconds(secondsPerItem);
            }

            if (panel != null) panel.SetActive(false);
        }

        private void Update()
        {
            if (modelPivot != null) modelPivot.Rotate(0f, spinSpeed * Time.deltaTime, 0f);
        }

        // Same offscreen-stage-plus-RenderTexture technique as
        // HotbarSlotUI -- one shared stage here since only one item is
        // ever shown at a time (not one per slot), just bigger and
        // parked well clear of the hotbar's own stages (see StageOrigin).
        private void BuildPreviewStage()
        {
            int layer = LayerMask.NameToLayer(PreviewLayerName);
            if (layer < 0)
            {
                Debug.LogWarning($"BatchUnlockPopupUI: layer '{PreviewLayerName}' doesn't exist -- add it in Project Settings > Tags and Layers.");
                return;
            }

            var stageRoot = new GameObject("BatchUnlockPreviewStage");
            stageRoot.transform.position = StageOrigin;
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

            renderTexture = new RenderTexture(PreviewTextureSize, PreviewTextureSize, 16) { name = "BatchUnlockPreviewRT" };
            previewCamera.targetTexture = renderTexture;
            if (modelImage != null) modelImage.texture = renderTexture;
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

            // The pivot sits at a fixed point in the stage and is what
            // actually spins -- the model goes inside it, recentered so
            // its *visual* center lines up with the pivot's origin (a raw
            // mesh's authored origin is often a corner or base, not the
            // center it should visually rotate around).
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

        private void OnDestroy()
        {
            if (renderTexture != null) renderTexture.Release();
        }
    }
}
