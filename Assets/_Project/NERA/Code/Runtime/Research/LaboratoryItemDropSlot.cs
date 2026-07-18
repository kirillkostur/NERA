using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NERA.Research
{
    public sealed class LaboratoryItemDropSlot : MonoBehaviour, IDropHandler
    {
        public Action<NERA.Items.ItemData> ItemDropped;

        public void OnDrop(PointerEventData eventData)
        {
            LaboratoryInventoryItemDrag dragItem = eventData.pointerDrag != null
                ? eventData.pointerDrag.GetComponent<LaboratoryInventoryItemDrag>()
                : null;

            if (dragItem != null)
                ItemDropped?.Invoke(dragItem.Item);
        }
    }
}
