using System.Collections.Generic;
using Mirror;
using RobEveryone.Input;
using RobEveryone.Inventory;
using RobEveryone.Items;
using RobEveryone.Player;
using RobEveryone.Sabotage;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RobEveryone.UI
{
    // The Tab / steal screen. Press Tab: the local camera swaps to a
    // static front-facing third-person shot, the hotbar rises and grows,
    // the cursor appears, and every slot box (hotbar + wallet) becomes a
    // drag source/target. Pressing E on a stunned rival opens the *same*
    // screen with that rival's hotbar shown above yours and a "Steal
    // from: <name>" label -- drag one item from their row onto yours.
    //
    // Every mutation is a Command on PlayerInventory / PlayerTheftTarget;
    // this only ever requests changes and redraws from their events. You
    // stay fully present in the world while it's open (a rival can still
    // tase you) -- see FirstPersonController.LookSuppressed.
    //
    // One per scene, on the same Canvas as the Hotbar (so the dim overlay
    // and hotbar share a stacking context). The Canvas needs a
    // GraphicRaycaster and the scene an EventSystem.
    public class InventoryScreenUI : MonoBehaviour
    {
        // Checked by HotbarController / PlayerDropController / Interactor /
        // SabotageUseController so gameplay input pauses while the screen
        // is up.
        public static bool MenuOpen { get; private set; }

        [Header("Panels")]
        [SerializeField] private GameObject dimBackground;     // full-screen Image, toggled
        [SerializeField] private RectTransform hotbarContainer; // the Hotbar row's own RectTransform
        [SerializeField] private GameObject victimRow;         // holds victimHotbarUI + victimLabel, toggled
        [SerializeField] private HotbarUI victimHotbarUI;      // bindToLocalPlayer = false
        [SerializeField] private TMP_Text victimLabel;

        [Header("Drag slots")]
        [SerializeField] private InventoryDragSlot[] myHotbarSlots; // 5, left to right
        [SerializeField] private InventoryDragSlot myWalletSlot;
        [SerializeField] private InventoryDragSlot[] victimHotbarSlots; // 5, left to right

        [Header("Hotbar transform (compact vs expanded)")]
        [SerializeField] private Vector2 compactAnchoredPos;
        [SerializeField] private float compactScale = 1f;
        [SerializeField] private Vector2 expandedAnchoredPos;
        [SerializeField] private float expandedScale = 1.6f;
        [SerializeField] private float transformLerpSpeed = 12f;

        [Header("Drag ghost")]
        [SerializeField] private RectTransform dragGhost;      // container, follows the cursor; must be a child of Hotbar (not SlotRow)
        [SerializeField] private RawImage dragGhostImage;      // shows the dragged item's live spinning model
        [SerializeField] private TMP_Text dragGhostLabel;      // fallback name text when the item has no model
        [SerializeField] private float ghostSize = 160f;       // on-screen px -- the code sizes the ghost, so the Editor rect doesn't matter

        [Header("Steal")]
        [SerializeField] private float stealBreakDistance = 6f; // walk this far from the victim and the screen closes

        private enum Mode { Closed, Self, Steal, Closing }
        private Mode mode = Mode.Closed;

        private FirstPersonController fpc;
        private InventoryCameraRig cameraRig;
        private PlayerImpactRelay myRelay;
        private PlayerTheftTarget myTheft;
        private CarryController myCarry;

        private PlayerInventory stealVictim;
        private NetworkIdentity stealVictimIdentity;
        private Canvas ghostCanvas;

        private void Awake()
        {
            SetPanelsForClosed();
            EndGhost(); // the DragGhost object often ships un-disabled -- hide it on load
            if (hotbarContainer != null)
            {
                hotbarContainer.anchoredPosition = compactAnchoredPos;
                hotbarContainer.localScale = Vector3.one * compactScale;
            }
        }

        private void Update()
        {
            ResolveLocalRefs();

            if (mode == Mode.Closed)
            {
                if (CanOpenSelf() && TabPressed()) OpenSelf();
            }
            else if (mode == Mode.Closing)
            {
                // Hold the menu "closing" -- input still parked -- until
                // the camera has finished blending back, so
                // FirstPersonController doesn't fight the blend.
                if (cameraRig == null || !cameraRig.Transitioning) FinishClose();
            }
            else if (mode == Mode.Self)
            {
                if (TabPressed() || EscapePressed()) Close(sendRelease: false);
            }
            else // Steal
            {
                if (EscapePressed() || TabPressed()) { CancelStealLocally(); }
                else if (stealVictim == null || myRelay == null) { CancelStealLocally(); }
                else if (stealVictimIdentity == null) { CancelStealLocally(); }
                else if (!VictimStillStealable() || VictimTooFar()) { CancelStealLocally(); }
            }

            AnimateHotbar();

            // A stun landing on us while a screen is up (we're fully
            // vulnerable) -- bail so PlayerRagdoll can own the camera.
            if ((mode == Mode.Self || mode == Mode.Steal) && myRelay != null && myRelay.IsStunned)
            {
                if (mode == Mode.Steal) CancelStealLocally();
                else Close(sendRelease: false);
            }
        }

        // ---- open / close ----------------------------------------------

        private bool CanOpenSelf() =>
            fpc != null && !fpc.IsFrozen
            && (myRelay == null || !myRelay.IsStunned)
            && (myCarry == null || !myCarry.IsCarrying); // hands full carrying a body

        private void OpenSelf()
        {
            mode = Mode.Self;
            EnterScreen();
            if (victimRow != null) victimRow.SetActive(false);
            ApplySlotModes();
        }

        // Called from PlayerTheftTarget's TargetRpc on the thief's client.
        public void OpenSteal(PlayerInventory victim)
        {
            if (victim == null) return;
            if (myCarry != null && myCarry.IsCarrying) return; // can't rob anyone with your hands full
            stealVictim = victim;
            stealVictimIdentity = victim.GetComponent<NetworkIdentity>();

            mode = Mode.Steal;
            EnterScreen();

            if (victimRow != null) victimRow.SetActive(true);
            if (victimHotbarUI != null) victimHotbarUI.Bind(victim);
            if (victimLabel != null) victimLabel.text = $"Steal from: {victim.DisplayName}";
            ApplySlotModes();
        }

        // Server told us the steal completed (or is over) -- clean close,
        // no CmdCancel needed (the server already released the window).
        public void CloseSteal()
        {
            if (mode != Mode.Steal) return;
            Close(sendRelease: false);
        }

        private void CancelStealLocally()
        {
            Close(sendRelease: true);
        }

        private void Close(bool sendRelease)
        {
            if (mode == Mode.Steal && sendRelease && myTheft != null && stealVictimIdentity != null)
            {
                myTheft.ReleaseStealWindow(stealVictimIdentity);
            }

            stealVictim = null;
            stealVictimIdentity = null;
            if (victimHotbarUI != null) victimHotbarUI.Bind(null);

            EndGhost();
            foreach (var s in EnumerateSlots()) { s.DragEnabled = false; s.DropEnabled = false; }
            if (victimRow != null) victimRow.SetActive(false);
            if (cameraRig != null) cameraRig.Hide();

            // Stay in Closing -- input still parked, hotbar animates back
            // to compact, dim background stays up -- until the camera blend
            // finishes; FinishClose does the actual handback.
            mode = Mode.Closing;
        }

        private void FinishClose()
        {
            mode = Mode.Closed;
            if (dimBackground != null) dimBackground.SetActive(false);
            MenuOpen = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            if (fpc != null) fpc.LookSuppressed = false;
        }

        private void EnterScreen()
        {
            MenuOpen = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (fpc != null) fpc.LookSuppressed = true;
            if (cameraRig != null) cameraRig.Show();
            if (dimBackground != null) dimBackground.SetActive(true);
        }

        private void SetPanelsForClosed()
        {
            if (dimBackground != null) dimBackground.SetActive(false);
            if (victimRow != null) victimRow.SetActive(false);
            foreach (var s in EnumerateSlots()) { s.DragEnabled = false; s.DropEnabled = false; }
        }

        // ---- slot interactivity per mode -----------------------------

        private void ApplySlotModes()
        {
            bool self = mode == Mode.Self;
            bool steal = mode == Mode.Steal;

            SetSlots(myHotbarSlots, drag: true, drop: true);              // always rearrangeable while open
            SetSlot(myWalletSlot, drag: self, drop: self);               // wallet only in self mode
            SetSlots(victimHotbarSlots, drag: steal, drop: false);       // victim row: source only, steal only
        }

        private static void SetSlots(IEnumerable<InventoryDragSlot> slots, bool drag, bool drop)
        {
            if (slots == null) return;
            foreach (var s in slots) SetSlot(s, drag, drop);
        }

        private static void SetSlot(InventoryDragSlot s, bool drag, bool drop)
        {
            if (s == null) return;
            s.DragEnabled = drag;
            s.DropEnabled = drop;
        }

        private IEnumerable<InventoryDragSlot> EnumerateSlots()
        {
            if (myHotbarSlots != null) foreach (var s in myHotbarSlots) if (s != null) yield return s;
            if (victimHotbarSlots != null) foreach (var s in victimHotbarSlots) if (s != null) yield return s;
            if (myWalletSlot != null) yield return myWalletSlot;
        }

        // ---- drag resolution ----------------------------------------

        public ItemDefinition ItemAt(InventoryDragSlot.SlotKind kind, int index)
        {
            switch (kind)
            {
                case InventoryDragSlot.SlotKind.MyWallet:
                    return PlayerInventory.LocalPlayer != null ? PlayerInventory.LocalPlayer.WalletItem : null;

                case InventoryDragSlot.SlotKind.MyHotbar:
                    return HeadItem(PlayerInventory.LocalPlayer, index);

                case InventoryDragSlot.SlotKind.VictimHotbar:
                    return HeadItem(stealVictim, index);
            }
            return null;
        }

        private static ItemDefinition HeadItem(PlayerInventory inv, int index)
        {
            if (inv == null || index < 0 || index >= PlayerInventory.SlotCount) return null;
            if (index >= inv.SlotSpanLengths.Count || inv.SlotSpanLengths[index] <= 0) return null; // continuation
            return inv.Slots[index]?.Item;
        }

        public void ResolveDrag(InventoryDragSlot src, InventoryDragSlot dst)
        {
            PlayerInventory mine = PlayerInventory.LocalPlayer;
            if (mine == null || src == dst) return;

            InventoryDragSlot.SlotKind from = src.Kind;
            InventoryDragSlot.SlotKind to = dst.Kind;

            if (from == InventoryDragSlot.SlotKind.MyHotbar && to == InventoryDragSlot.SlotKind.MyHotbar)
            {
                mine.CmdMoveItem(src.Index, dst.Index);
            }
            else if (from == InventoryDragSlot.SlotKind.MyHotbar && to == InventoryDragSlot.SlotKind.MyWallet)
            {
                mine.CmdMoveToWallet(src.Index);
            }
            else if (from == InventoryDragSlot.SlotKind.MyWallet && to == InventoryDragSlot.SlotKind.MyHotbar)
            {
                mine.CmdMoveFromWallet(dst.Index);
            }
            else if (from == InventoryDragSlot.SlotKind.VictimHotbar && to == InventoryDragSlot.SlotKind.MyHotbar)
            {
                if (myTheft != null && stealVictimIdentity != null)
                    myTheft.RequestSteal(stealVictimIdentity, src.Index, dst.Index);
            }
            // any other pairing is a no-op
        }

        // ---- drag ghost --------------------------------------------

        public void BeginGhost(InventoryDragSlot from, Vector2 screenPos)
        {
            if (dragGhost == null) return;
            ItemDefinition item = ItemAt(from.Kind, from.Index);
            if (item == null) return;

            Texture preview = from.Slot != null ? from.Slot.PreviewTexture : null;
            if (dragGhostImage != null)
            {
                dragGhostImage.texture = preview;
                dragGhostImage.enabled = preview != null;
            }
            if (dragGhostLabel != null)
            {
                dragGhostLabel.text = item.ItemName;
                dragGhostLabel.enabled = preview == null; // text only when there's no model to show
            }

            // Force the geometry in code so a mis-sized Editor rect (or a
            // scaled parent) can't shrink it: centre-anchored, fixed px,
            // scale 1, and the image stretched to fill.
            dragGhost.anchorMin = dragGhost.anchorMax = dragGhost.pivot = new Vector2(0.5f, 0.5f);
            dragGhost.sizeDelta = new Vector2(ghostSize, ghostSize);
            dragGhost.localScale = Vector3.one;
            if (dragGhostImage != null)
            {
                RectTransform ir = dragGhostImage.rectTransform;
                ir.anchorMin = Vector2.zero;
                ir.anchorMax = Vector2.one;
                ir.offsetMin = ir.offsetMax = Vector2.zero;
            }

            dragGhost.gameObject.SetActive(true);
            MoveGhost(screenPos);
        }

        public void MoveGhost(Vector2 screenPos)
        {
            if (dragGhost == null || !dragGhost.gameObject.activeSelf) return;

            // Screen-Space-Camera canvases (this HUD is one) need the
            // screen point converted through the canvas camera -- a raw
            // world-space assignment would drop the ghost at Z=0 in the
            // world, nowhere near the cursor.
            Canvas canvas = ghostCanvas != null ? ghostCanvas : (ghostCanvas = dragGhost.GetComponentInParent<Canvas>());
            RectTransform parent = dragGhost.parent as RectTransform;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay && canvas.worldCamera != null && parent != null)
            {
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPos, canvas.worldCamera, out Vector2 local))
                    dragGhost.localPosition = local;
            }
            else
            {
                dragGhost.position = screenPos;
            }
        }

        public void EndGhost()
        {
            if (dragGhost != null) dragGhost.gameObject.SetActive(false);
        }

        // ---- helpers -----------------------------------------------

        private void ResolveLocalRefs()
        {
            if (fpc != null) return;
            if (NetworkClient.localPlayer == null) return;
            GameObject p = NetworkClient.localPlayer.gameObject;
            fpc = p.GetComponent<FirstPersonController>();
            cameraRig = p.GetComponent<InventoryCameraRig>();
            myRelay = p.GetComponent<PlayerImpactRelay>();
            myTheft = p.GetComponent<PlayerTheftTarget>();
            myCarry = p.GetComponent<CarryController>();
        }

        private bool VictimStillStealable()
        {
            PlayerImpactRelay vr = stealVictim != null ? stealVictim.GetComponent<PlayerImpactRelay>() : null;
            return vr != null && vr.IsStealable;
        }

        private bool VictimTooFar()
        {
            if (NetworkClient.localPlayer == null || stealVictim == null) return true;
            return Vector3.Distance(NetworkClient.localPlayer.transform.position, stealVictim.transform.position) > stealBreakDistance;
        }

        private void AnimateHotbar()
        {
            if (hotbarContainer == null) return;
            bool open = mode == Mode.Self || mode == Mode.Steal;
            Vector2 targetPos = open ? expandedAnchoredPos : compactAnchoredPos;
            float targetScale = open ? expandedScale : compactScale;

            hotbarContainer.anchoredPosition = Vector2.Lerp(hotbarContainer.anchoredPosition, targetPos, transformLerpSpeed * Time.deltaTime);
            float s = Mathf.Lerp(hotbarContainer.localScale.x, targetScale, transformLerpSpeed * Time.deltaTime);
            hotbarContainer.localScale = new Vector3(s, s, 1f);
        }

        private static bool TabPressed() => InputManager.Gameplay.ToggleInventory.WasPressedThisFrame();
        // Shared with the in-game pause overlay (settings-menu-setup.md
        // Milestone G) via the stock UI map's Cancel action, not a
        // Gameplay one -- this screen still gets first refusal on every
        // Escape press while it's open (see MenuOpen's other consumer).
        private static bool EscapePressed() => InputManager.UI.Cancel.WasPressedThisFrame();
    }
}
