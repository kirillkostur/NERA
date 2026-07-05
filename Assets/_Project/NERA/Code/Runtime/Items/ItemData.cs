using UnityEngine;

[CreateAssetMenu(
    fileName = "ItemData_NewItem",
    menuName = "NERA/Items/Item Data"
)]
public class ItemData : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string itemId = "new_item";
    [SerializeField] private ItemType itemType = ItemType.Resource;

    [Header("Localization")]
    [SerializeField] private LocalizedText itemName;
    [SerializeField] private LocalizedText description;

    [Header("Visual")]
    [SerializeField] private Sprite icon;

    public string ItemId => itemId;
    public ItemType ItemType => itemType;
    public LocalizedText ItemNameText => itemName;
    public LocalizedText DescriptionText => description;
    public Sprite Icon => icon;

    public string GetItemName()
    {
        if (itemName == null)
            return itemId;

        return itemName.GetText(itemId);
    }

    public string GetDescription()
    {
        if (description == null)
            return string.Empty;

        return description.GetText(string.Empty);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(itemId))
            itemId = name.ToLowerInvariant().Replace(" ", "_");
    }
#endif
}