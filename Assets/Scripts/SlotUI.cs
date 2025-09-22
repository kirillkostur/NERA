using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    [Header("UI")]
    public Image icon;
    public TMP_Text countText;

    public int SlotIndex { get; private set; } = -1;

    private LootableObject lootSource;
    private InventoryItem lootItem;
    private int lootCount;
    public bool isLootSlot = false;

    public void BindInventorySlot(int index)
    {
        SlotIndex = index;
        isLootSlot = false;
        lootSource = null;
        SetEmptyVisual();
    }

    public void BindLootSlot(LootableObject source, InventoryItem item, int count)
    {
        SlotIndex = -1;
        isLootSlot = true;
        lootSource = source;
        lootItem = item;
        lootCount = count;

        icon.enabled = true;
        icon.sprite = item.icon;

        bool showCount = count > 1;
        countText.enabled = showCount;
        countText.text = showCount ? count.ToString() : "";
    }

    public void SetSlot(InventorySlot data)
    {
        if (data == null || data.IsEmpty)
        {
            SetEmptyVisual();
            return;
        }

        icon.enabled = true;
        icon.sprite = data.item.icon;

        bool showCount = data.count > 1;
        countText.enabled = showCount;
        countText.text = showCount ? data.count.ToString() : "";
    }

    private void SetEmptyVisual()
    {
        icon.enabled = false;
        countText.enabled = false;
        countText.text = "";
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isLootSlot)
        {
            if (lootItem == null || lootCount <= 0) return;
            icon.enabled = false;
            countText.enabled = false;
            DragItem.Instance.StartDrag(this, lootItem.icon);
            return;
        }

        var inv = PlayerInventory.Instance;
        if (inv == null || !inv.IsValidIndex(SlotIndex)) return;
        var slot = inv.Slots[SlotIndex];
        if (slot.IsEmpty) return;

        icon.enabled = false;
        countText.enabled = false;
        DragItem.Instance.StartDrag(this, slot.item.icon);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (DragItem.Instance.IsDragging)
            DragItem.Instance.FollowCursor();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (isLootSlot)
        {
            icon.enabled = true;
            countText.enabled = lootCount > 1;
            DragItem.Instance.EndDrag();
            return;
        }

        if (DragItem.Instance.IsDragging && DragItem.Instance.Origin == this)
            SetSlot(PlayerInventory.Instance.Slots[SlotIndex]);

        DragItem.Instance.EndDrag();
    }

    public void OnDrop(PointerEventData eventData)
    {
        // Перетаскивание из лута в инвентарь
        if (DragItem.Instance.IsDragging &&
            DragItem.Instance.Origin != null &&
            DragItem.Instance.Origin.isLootSlot &&
            !isLootSlot)
        {
            var from = DragItem.Instance.Origin;
            int added = PlayerInventory.Instance.AddItem(from.lootItem, from.lootCount);
            int leftover = from.lootCount - added;

            if (leftover > 0)
                from.lootSource.ReturnItem(from.lootItem, leftover);
            else
                from.lootSource.RemoveItem(from.lootItem, from.lootCount);

            HUDLootUI.Instance.RefreshIfCurrent(from.lootSource);
            HUDInventoryUI.Instance.Refresh();
            DragItem.Instance.EndDrag();
            return;
        }

        // Перетаскивание из инвентаря обратно в инвентарь
        if (isLootSlot)
        {
            if (DragItem.Instance.IsDragging &&
                DragItem.Instance.Origin != null &&
                !DragItem.Instance.Origin.isLootSlot)
            {
                var origin = DragItem.Instance.Origin;
                origin.SetSlot(PlayerInventory.Instance.Slots[origin.SlotIndex]);
            }

            DragItem.Instance.EndDrag();
            return;
        }

        if (!DragItem.Instance.IsDragging) return;

        var fromSlot = DragItem.Instance.Origin;
        if (fromSlot == this) return;

        PlayerInventory.Instance.MoveOrSwap(fromSlot.SlotIndex, SlotIndex);

        fromSlot.SetSlot(PlayerInventory.Instance.Slots[fromSlot.SlotIndex]);
        SetSlot(PlayerInventory.Instance.Slots[SlotIndex]);

        DragItem.Instance.EndDrag();
    }
}
