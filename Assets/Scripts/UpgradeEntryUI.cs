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
    public SlotUI slotPrefab;

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

        BuildSlots();
    }

    private void BuildSlots()
    {
        foreach (Transform child in slotsParent)
            Destroy(child.gameObject);
        spawnedSlots.Clear();

        foreach (var s in data.slots)
        {
            var ui = Object.Instantiate(slotPrefab, slotsParent);
            ui.BindUpgradeSlot(s, parent);
            spawnedSlots.Add(ui);
        }
    }

    public void Refresh()
    {
        foreach (var s in spawnedSlots)
        {
            if (s.upgradeData != null)
                s.BindUpgradeSlot(s.upgradeData, parent);
        }
    }
}
