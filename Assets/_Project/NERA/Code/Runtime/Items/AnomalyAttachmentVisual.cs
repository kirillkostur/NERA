using UnityEngine;

namespace NERA.Items
{
    public sealed class AnomalyAttachmentVisual : MonoBehaviour
    {
        private const string ContainerSlotName = "Slot_AnomalyContainer";
        private const string AnomalySlotName = "Slot_Anomaly";

        private GameObject spawnedVisual;

        public void Bind(ItemInstance instance)
        {
            Clear();
            ItemData owner = instance?.ItemData;
            if (owner == null)
                return;

            if (owner.AcceptsAnomalyContainer)
            {
                ItemInstance container =
                    instance.CreateInstalledAnomalyContainerInstance();
                if (container?.ItemData?.WorldPrefab == null)
                    return;

                Transform slot = FindChildRecursive(transform, ContainerSlotName);
                spawnedVisual = SpawnVisual(
                    container.ItemData.WorldPrefab.gameObject,
                    slot);
                if (spawnedVisual == null)
                    return;

                AnomalyAttachmentVisual nested =
                    spawnedVisual.GetComponent<AnomalyAttachmentVisual>();
                nested?.Bind(container);
                DisableWorldInteraction(spawnedVisual);
                return;
            }

            if (!owner.AcceptsAnomalyIntegration ||
                instance.IntegratedAnomaly?.WorldPrefab == null)
            {
                return;
            }

            spawnedVisual = SpawnVisual(
                instance.IntegratedAnomaly.WorldPrefab.gameObject,
                FindChildRecursive(transform, AnomalySlotName));
            DisableWorldInteraction(spawnedVisual);
        }

        private static GameObject SpawnVisual(GameObject prefab, Transform slot)
        {
            if (prefab == null || slot == null)
                return null;

            GameObject visual = Instantiate(prefab, slot, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            return visual;
        }

        private static void DisableWorldInteraction(GameObject root)
        {
            if (root == null)
                return;

            foreach (WorldItem worldItem in
                     root.GetComponentsInChildren<WorldItem>(true))
            {
                worldItem.enabled = false;
            }

            foreach (Collider collider in
                     root.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }

            foreach (Rigidbody body in
                     root.GetComponentsInChildren<Rigidbody>(true))
            {
                body.useGravity = false;
                body.isKinematic = true;
            }
        }

        private static Transform FindChildRecursive(
            Transform root,
            string childName)
        {
            if (root == null)
                return null;
            if (root.name == childName)
                return root;

            for (int index = 0; index < root.childCount; index++)
            {
                Transform match = FindChildRecursive(
                    root.GetChild(index),
                    childName);
                if (match != null)
                    return match;
            }

            return null;
        }

        private void Clear()
        {
            if (spawnedVisual == null)
                return;

            if (Application.isPlaying)
                Destroy(spawnedVisual);
            else
                DestroyImmediate(spawnedVisual);
            spawnedVisual = null;
        }
    }
}
