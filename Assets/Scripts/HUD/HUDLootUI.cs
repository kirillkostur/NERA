using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HUDLootUI : MonoBehaviour
{
    public static HUDLootUI Instance { get; private set; }

    [Header("Loot UI Elements")]
    public GameObject panel;
    public RectTransform slotsParent;
    public SlotUI slotPrefab;
    public Button takeAllButton;

    private LootableObject _current;

    private void Awake()
    {
        Instance = this;
        if (takeAllButton != null)
        {
            takeAllButton.onClick.RemoveAllListeners();
            takeAllButton.onClick.AddListener(() => _current?.TakeAll());
        }
        if (panel != null) panel.SetActive(false);
    }

    public void ShowLoot(LootableObject source, bool showTakeAll = false)
    {
        if (panel == null) return;

        _current = source;
        panel.SetActive(true);
        takeAllButton?.gameObject.SetActive(showTakeAll);

        Refresh();
    }

    public void HideLoot()
    {
        _current = null;
        if (panel != null && panel.gameObject != null)
            panel.SetActive(false);
    }

    public void HideLootIfCurrent(LootableObject source)
    {
        if (_current == source) HideLoot();
    }

    public void RefreshIfCurrent(LootableObject source)
    {
        if (_current == source && panel != null && panel.activeSelf) Refresh();
    }

    public void Refresh()
    {
        if (slotsParent == null) return;

        // Очистка слотов
        for (int i = slotsParent.childCount - 1; i >= 0; i--)
        {
            if (slotsParent.GetChild(i) != null)
                Destroy(slotsParent.GetChild(i).gameObject);
        }

        // Создание новых
        if (_current != null)
        {
            var preview = GetPreview();
            if (preview != null)
            {
                foreach (var p in preview)
                {
                    if (p.item == null || p.count <= 0) continue;
                    var slot = Instantiate(slotPrefab, slotsParent);
                    slot.BindLootSlot(_current, p.item, p.count);
                }
            }
        }

        // Принудительное обновление макета
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(slotsParent);
        LayoutRebuilder.ForceRebuildLayoutImmediate(panel.GetComponent<RectTransform>());
    }

    private List<(InventoryItem item, int count)> GetPreview()
    {
        var provider = _current as ILootPreviewProvider;
        return provider?.GetPreview();
    }
}
