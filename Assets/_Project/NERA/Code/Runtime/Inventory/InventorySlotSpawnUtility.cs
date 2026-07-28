using UnityEngine;

namespace NERA.Inventory
{
    public static class InventorySlotSpawnUtility
    {
        public static InventorySlotView GetOrCreate(
            Transform spawnPoint,
            GameObject slotPrefab)
        {
            if (spawnPoint == null || slotPrefab == null)
                return null;

            for (int i = 0; i < spawnPoint.childCount; i++)
            {
                InventorySlotView existing =
                    spawnPoint.GetChild(i).GetComponent<InventorySlotView>();
                if (existing != null)
                {
                    Prepare(existing, spawnPoint, slotPrefab.name);
                    return existing;
                }
            }

            GameObject slotObject = Object.Instantiate(
                slotPrefab,
                spawnPoint,
                false);
            InventorySlotView view =
                slotObject.GetComponent<InventorySlotView>() ??
                slotObject.AddComponent<InventorySlotView>();
            Prepare(view, spawnPoint, slotPrefab.name);
            return view;
        }

        private static void Prepare(
            InventorySlotView view,
            Transform spawnPoint,
            string prefabName)
        {
            view.name = prefabName;
            view.gameObject.SetActive(true);
            view.transform.SetParent(spawnPoint, false);
            view.transform.localPosition = Vector3.zero;
            view.transform.localRotation = Quaternion.identity;
            view.transform.localScale = Vector3.one;

            if (view.transform is RectTransform rect)
                rect.anchoredPosition = Vector2.zero;
        }
    }
}
