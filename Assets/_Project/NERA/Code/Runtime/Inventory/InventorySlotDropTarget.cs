using System;
using NERA.Research;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NERA.Inventory
{
    public sealed class InventorySlotDropTarget : MonoBehaviour, IDropHandler
    {
        private InventorySlotGroup group;
        private int index;
        private Action<InventorySlotGroup, int, LaboratoryInventoryItemDrag> onDrop;

        public void Initialize(
            InventorySlotGroup slotGroup,
            int slotIndex,
            Action<InventorySlotGroup, int, LaboratoryInventoryItemDrag> callback
        )
        {
            group = slotGroup;
            index = slotIndex;
            onDrop = callback;
        }

        public void OnDrop(PointerEventData eventData)
        {
            LaboratoryInventoryItemDrag drag = eventData.pointerDrag != null
                ? eventData.pointerDrag.GetComponent<LaboratoryInventoryItemDrag>()
                : null;
            if (drag != null)
                onDrop?.Invoke(group, index, drag);
        }
    }
}
