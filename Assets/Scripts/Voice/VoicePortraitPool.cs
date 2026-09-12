using System.Collections.Generic;
using RobEveryone.Customization;
using RobEveryone.Inventory;
using RobEveryone.Player;
using UnityEngine;

namespace RobEveryone.Voice
{
    // A small fixed pool of offstage "portrait rigs" -- each one an
    // isolated spot far below the map with its own Camera rendering a
    // head shot into its own RenderTexture. VoiceCaptionsHUD acquires a
    // slot per currently-speaking player and shows that RenderTexture in
    // a RawImage, so the caption list reads as an actual live portrait
    // of that player's real skin/color, not just a name. Sized to the
    // game's own player-count ceiling (~8, per voip-setup.md's stated
    // design target) -- a speaking player only ever needs one slot, and
    // there can never be more simultaneous speakers than connected
    // players.
    //
    // Self-contained: builds its own cameras/RenderTextures/rig
    // Transforms at runtime (same "no manual per-instance Editor setup"
    // spirit as InputManager/AudioMixerApplier) -- the only Editor work
    // is dropping this component into the scene once and wiring the
    // same skinRoster/palette assets PlayerSkinSpawner already uses.
    public class VoicePortraitPool : MonoBehaviour
    {
        [SerializeField] private PlayerSkinRoster skinRoster;
        [SerializeField] private PlayerColorPalette palette;
        [SerializeField] private int poolSize = 8;
        [SerializeField] private int textureSize = 256;
        [SerializeField] private float slotSpacing = 4f;

        // World-space vertical extent the camera frames, centered on the
        // head -- not a raw camera distance, so it stays correct however
        // large/small a given skin's head actually is (this pack's 52
        // skins aren't all the same proportions). Increase for more
        // headroom/shoulders in shot, decrease to zoom in tighter.
        [SerializeField] private float framedHeight = 0.55f;
        [SerializeField] private float cameraFov = 30f;
        // A rig's "Head" bone conventionally sits at the neck/base-of-
        // skull joint, not the visual center of the head -- nudges the
        // look-at point up from that bone toward the head's actual
        // middle so framedHeight isn't spent mostly on empty space below
        // the chin.
        [SerializeField] private float headCenterOffset = 0.08f;
        // Only used if a skin has no bone literally named "Head" (see
        // FindBone) -- a last-resort guess so the camera isn't pointed
        // at nothing at all.
        [SerializeField] private float cameraHeightFallback = 1.6f;

        // Far enough below the map that nothing else is ever in frame --
        // no special layer/culling-mask setup needed, the dead space
        // itself does the isolating.
        private static readonly Vector3 StagingOrigin = new(0f, -500f, 0f);

        private class Slot
        {
            public Transform root;
            public Camera camera;
            public RenderTexture texture;
            public GameObject skinInstance;
            public PlayerInventory owner;
        }

        private readonly List<Slot> slots = new();

        public static VoicePortraitPool Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            for (int i = 0; i < poolSize; i++) slots.Add(BuildSlot(i));
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private Slot BuildSlot(int index)
        {
            Transform root = new GameObject($"VoicePortraitRig_{index}").transform;
            root.SetParent(transform, false);
            root.position = StagingOrigin + new Vector3(index * slotSpacing, 0f, 0f);

            RenderTexture rt = new RenderTexture(textureSize, textureSize, 16, RenderTextureFormat.ARGB32)
            {
                name = $"VoicePortraitRT_{index}"
            };
            rt.Create();

            // Placeholder pose -- FrameHead repositions this properly
            // (relative to the actual spawned skin's Head bone) the
            // moment a skin is first assigned to this slot.
            Camera cam = new GameObject("PortraitCamera").AddComponent<Camera>();
            cam.transform.SetParent(root, false);
            cam.fieldOfView = cameraFov;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f); // transparent -- the head floats over the HUD, no visible box
            cam.targetTexture = rt;
            cam.gameObject.SetActive(false); // enabled only while the slot is actually claimed

            return new Slot { root = root, camera = cam, texture = rt };
        }

        // Returns the RenderTexture to show for `player`, claiming a
        // free slot and spawning/re-skinning it for them if they don't
        // already have one. Null only if every slot is already claimed
        // (shouldn't happen at this project's player-count scale).
        public RenderTexture Acquire(PlayerInventory player)
        {
            foreach (Slot slot in slots)
            {
                if (slot.owner == player) return slot.texture;
            }

            Slot free = slots.Find(s => s.owner == null);
            if (free == null) return null;

            free.owner = player;
            free.camera.gameObject.SetActive(true);
            SpawnSkinFor(free, player);
            return free.texture;
        }

        public void Release(PlayerInventory player)
        {
            Slot slot = slots.Find(s => s.owner == player);
            if (slot == null) return;

            slot.owner = null;
            slot.camera.gameObject.SetActive(false);
            if (slot.skinInstance != null) slot.skinInstance.SetActive(false);
        }

        private void SpawnSkinFor(Slot slot, PlayerInventory player)
        {
            PlayerSkinSpawner spawner = player.GetComponent<PlayerSkinSpawner>();
            if (spawner == null)
            {
                Debug.LogWarning($"[VoicePortraitPool] {player.name} has no PlayerSkinSpawner -- portrait stays blank.");
                return;
            }
            if (skinRoster == null)
            {
                Debug.LogWarning("[VoicePortraitPool] Skin Roster isn't wired on this component -- drag Assets/PlayerSkinRoster.asset into it.");
                return;
            }

            GameObject prefab = skinRoster.GetSkin(spawner.SkinIndex);
            if (prefab == null)
            {
                Debug.LogWarning($"[VoicePortraitPool] SkinRoster.GetSkin({spawner.SkinIndex}) returned null -- SkinIndex probably hasn't synced yet (try again after a moment) or is out of range.");
                return;
            }

            if (slot.skinInstance != null) Destroy(slot.skinInstance);

            slot.skinInstance = Instantiate(prefab, slot.root.position, slot.root.rotation, slot.root);
            slot.skinInstance.SetActive(true);

            if (palette != null && palette.Colors.Count > 0)
            {
                int colorIndex = Mathf.Clamp(spawner.ColorIndex, 0, palette.Colors.Count - 1);
                PlayerColorizer colorizer = slot.skinInstance.GetComponent<PlayerColorizer>();
                if (colorizer == null) colorizer = slot.skinInstance.AddComponent<PlayerColorizer>();
                colorizer.ApplyBodyColor(palette.Colors[colorIndex]);
            }

            FrameHead(slot);
        }

        // Aims the slot's camera at the actual spawned model's real Head
        // bone instead of a single guessed height for every skin --
        // this pack's 52 skins don't all share the same proportions, so
        // a fixed number was always going to be wrong for some of them.
        private void FrameHead(Slot slot)
        {
            Transform head = FindBone(slot.skinInstance.transform, "Head");
            Vector3 focusPoint = head != null
                ? head.position + Vector3.up * headCenterOffset
                : slot.root.position + Vector3.up * cameraHeightFallback;

            if (head == null)
            {
                Debug.LogWarning($"[VoicePortraitPool] No 'Head' bone found on {slot.skinInstance.name} -- falling back to a guessed height, portrait framing may be off.");
            }

            // Distance derived from the desired framed height (world
            // units) rather than a hand-picked number -- stays correct
            // regardless of fieldOfView/head size, and directly matches
            // "how much of the head+margin should be visible" as an
            // actual tunable amount instead of trial-and-error.
            float distance = framedHeight / (2f * Mathf.Tan(cameraFov * 0.5f * Mathf.Deg2Rad));

            Vector3 camPos = focusPoint + slot.root.forward * distance;
            slot.camera.transform.position = camPos;
            slot.camera.transform.LookAt(focusPoint, Vector3.up);
        }

        private static Transform FindBone(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                Transform found = FindBone(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
