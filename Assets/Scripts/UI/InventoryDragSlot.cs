using UnityEngine;
using UnityEngine.EventSystems;

namespace RobEveryone.UI
{
    // One draggable/droppable box in the inventory screen. Goes on the
    // same GameObject as each hotbar slot box, the wallet box, and (in
    // steal mode) each victim slot box -- alongside HotbarSlotUI, not
    // replacing it. Needs a raycast-target Graphic on this GameObject
    // (any Image) so pointer events land.
    //
    // Purely a conduit: it reports drag start/move/end and drop to the
    // parent InventoryScreenUI, which owns the ghost and decides what a
    // given (source -> target) pairing means.
    [RequireComponent(typeof(RectTransform))]
    public class InventoryDragSlot : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        public enum SlotKind { MyHotbar, MyWallet, VictimHotbar }

        [SerializeField] private SlotKind kind = SlotKind.MyHotbar;
        [SerializeField] private int index = -1; // hotbar slot index; ignored for the wallet

        public SlotKind Kind => kind;
        public int Index => index;

        // Set each time the screen opens, per mode (see
        // InventoryScreenUI.ApplySlotModes).
        public bool DragEnabled { get; set; }
        public bool DropEnabled { get; set; }

        // The visual for this box (same GameObject) -- the drag ghost
        // borrows its live model-preview texture.
        public HotbarSlotUI Slot { get; private set; }

        private InventoryScreenUI screen;

        private void Awake()
        {
            Slot = GetComponent<HotbarSlotUI>();
            screen = GetComponentInParent<InventoryScreenUI>(true);
            if (screen == null) screen = FindFirstObjectByType<InventoryScreenUI>(FindObjectsInactive.Include);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!DragEnabled || screen == null) return;
            if (screen.ItemAt(kind, index) == null) return; // nothing here to drag
            screen.BeginGhost(this, eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (screen != null) screen.MoveGhost(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            // The move itself happens in the target's OnDrop; this just
            // clears the ghost whether or not a drop landed.
            if (screen != null) screen.EndGhost();
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (!DropEnabled || screen == null) return;
            InventoryDragSlot source = eventData.pointerDrag != null
                ? eventData.pointerDrag.GetComponent<InventoryDragSlot>()
                : null;
            if (source == null || source == this) return;
            screen.ResolveDrag(source, this);
        }
    }
}
