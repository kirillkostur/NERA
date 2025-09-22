public interface ILootPreviewProvider
{
    System.Collections.Generic.List<(InventoryItem item, int count)> GetPreview();
}

