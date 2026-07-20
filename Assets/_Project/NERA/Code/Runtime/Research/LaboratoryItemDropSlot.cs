using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NERA.Research
{
    public sealed class LaboratoryItemDropSlot : MonoBehaviour, IDropHandler
    {
        public Action<LaboratoryInventoryItemDrag> ItemDropped;

        public void OnDrop(PointerEventData eventData)
        {
            LaboratoryInventoryItemDrag dragItem = eventData.pointerDrag != null
                ? eventData.pointerDrag.GetComponent<LaboratoryInventoryItemDrag>()
                : null;

            if (dragItem != null && dragItem.Item != null)
                ItemDropped?.Invoke(dragItem);
        }
    }
}
