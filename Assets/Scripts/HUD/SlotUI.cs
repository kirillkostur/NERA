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

    // 👉 Новое: режим апгрейда
    [HideInInspector] public UpgradeSlotData upgradeData;
    [HideInInspector] public HUDUpgradeUI upgradeParent;

    public void BindInventorySlot(int index)
    {
        SlotIndex = index;
        isLootSlot = false;
        upgradeData = null;
        upgradeParent = null;
        lootSource = null;
        SetEmptyVisual();
    }

    public void BindLootSlot(LootableObject source, InventoryItem item, int count)
    {
        SlotIndex = -1;
        isLootSlot = true;
        upgradeData = null;
        upgradeParent = null;

        lootSource = source;
        lootItem = item;
        lootCount = count;

        icon.enabled = true;
        icon.sprite = item.icon;
        bool showCount = count > 1;
        countText.enabled = showCount;
        countText.text = showCount ? count.ToString() : "";
    }

    public void BindUpgradeSlot(UpgradeSlotData data, HUDUpgradeUI parent)
    {
        SlotIndex = -1;
        isLootSlot = false;
        upgradeData = data;
        upgradeParent = parent;
        RefreshUpgrade();
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

    private void RefreshUpgrade()
    {
        if (upgradeData == null || upgradeData.config == null) return;

        icon.enabled = true;
        icon.sprite = (upgradeData.currentCount > 0 ? upgradeData.config.item.icon : upgradeData.config.placeholderIcon);
        icon.color = (upgradeData.currentCount > 0 ? Color.white : new Color(1, 1, 1, 0.5f));
        countText.enabled = true;
        countText.text = $"{upgradeData.currentCount}/{upgradeData.config.requiredCount}";
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (upgradeData != null) return; // апгрейд-слоты не перетаскиваются

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
        if (upgradeData != null) return; // апгрейд-слоты не перетаскиваются

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
        // 👉 режим апгрейда
        if (upgradeData != null)
        {
            if (!DragItem.Instance.IsDragging) return;
            var origin = DragItem.Instance.Origin;
            if (origin == null || origin.isLootSlot) return;

            var inv = PlayerInventory.Instance;
            if (inv == null || !inv.IsValidIndex(origin.SlotIndex)) return;

            var slot = inv.Slots[origin.SlotIndex];
            if (slot.IsEmpty || slot.item == null)
            {
                origin.SetSlot(inv.Slots[origin.SlotIndex]);
                DragItem.Instance.EndDrag();
                return;
            }

            if (slot.item.ID != upgradeData.config.item.ID)
            {
                upgradeParent.ShowHint("Нужен другой предмет");
                origin.SetSlot(inv.Slots[origin.SlotIndex]);
                DragItem.Instance.EndDrag();
                return;
            }

            if (upgradeData.IsFull)
            {
                upgradeParent.ShowHint("Слот уже заполнен");
                origin.SetSlot(inv.Slots[origin.SlotIndex]);
                DragItem.Instance.EndDrag();
                return;
            }

            int need = upgradeData.Need;
            int take = Mathf.Min(slot.count, need);
            inv.RemoveFromSlot(origin.SlotIndex, take);
            upgradeData.Add(take);

            origin.SetSlot(inv.Slots[origin.SlotIndex]);
            HUDInventoryUI.Instance?.Refresh();
            RefreshUpgrade();
            upgradeParent.RefreshButtonState();
            DragItem.Instance.EndDrag();
            return;
        }

        // обычные перетаскивания
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
