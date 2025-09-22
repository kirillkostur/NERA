using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UpgradeEntryUI : MonoBehaviour
{
    [Header("UI секции")]
    public TMP_Text nameText;
    public Image icon;
    public Transform slotsParent;
    public SlotUI slotPrefab;   // 👉 Теперь используем SlotUI вместо UpgradeSlotUI

    private UpgradeEntryData data;
    private HUDUpgradeUI parent;
    private readonly List<SlotUI> spawnedSlots = new();

    public void Setup(UpgradeEntryData entryData, HUDUpgradeUI p)
    {
        data = entryData;
        parent = p;

        if (nameText != null)
            nameText.text = data.config.displayName;

        if (icon != null)
        {
            icon.sprite = data.config.displayIcon;
            icon.enabled = data.config.displayIcon != null;
        }

        // создаём слоты для требований
        foreach (var s in data.slots)
        {
            var ui = Instantiate(slotPrefab, slotsParent);
            ui.BindUpgradeSlot(s, parent);  // 👉 теперь связываем через SlotUI.BindUpgradeSlot
            spawnedSlots.Add(ui);
        }
    }

    public void Refresh()
    {
        foreach (var s in spawnedSlots)
        {
            // SlotUI обновляет внешний вид апгрейдного слота через BindUpgradeSlot
            // поэтому просто переназначим, чтобы вызвать RefreshUpgrade()
            if (s.upgradeData != null)
                s.BindUpgradeSlot(s.upgradeData, parent);
        }
    }
}
