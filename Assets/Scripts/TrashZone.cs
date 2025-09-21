using UnityEngine;
using UnityEngine.EventSystems;

public class TrashZone : MonoBehaviour, IDropHandler
{
    public void OnDrop(PointerEventData eventData)
    {
        if (!DragItem.Instance.IsDragging) return;
        var origin = DragItem.Instance.Origin;
        PlayerInventory.Instance.ClearSlot(origin.SlotIndex);
        HUDInventoryUI.Instance.Refresh();
        DragItem.Instance.EndDrag();
    }
}
