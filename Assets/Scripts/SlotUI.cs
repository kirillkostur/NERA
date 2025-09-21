using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    [Header("UI")]
    public Image icon;         // дочерний Image для предмета (НЕ фон)
    public TMP_Text countText; // счётчик

    public int SlotIndex { get; private set; }

    public void Bind(int index)
    {
        SlotIndex = index;
        SetEmptyVisual();
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

        // растягиваем под слот
        var rt = icon.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // показывать цифру, если количество > 1 (или всегда, если хочешь)
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

    // === Drag & Drop ===
    public void OnBeginDrag(PointerEventData eventData)
    {
        var inv = PlayerInventory.Instance;
        if (inv == null || !inv.IsValidIndex(SlotIndex)) return;
        var slot = inv.Slots[SlotIndex];
        if (slot.IsEmpty) return;

        // визуально «вынимаем» предмет
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
        // если бросили не на слот — восстановим визуал из данных
        if (DragItem.Instance.IsDragging && DragItem.Instance.Origin == this)
            SetSlot(PlayerInventory.Instance.Slots[SlotIndex]);

        DragItem.Instance.EndDrag();
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (!DragItem.Instance.IsDragging) return;

        var from = DragItem.Instance.Origin;
        if (from == this) return;

        PlayerInventory.Instance.MoveOrSwap(from.SlotIndex, SlotIndex);

        // обновляем оба слота ТЕКУЩИМИ данными
        from.SetSlot(PlayerInventory.Instance.Slots[from.SlotIndex]);
        SetSlot(PlayerInventory.Instance.Slots[SlotIndex]);

        DragItem.Instance.EndDrag();
    }
}
