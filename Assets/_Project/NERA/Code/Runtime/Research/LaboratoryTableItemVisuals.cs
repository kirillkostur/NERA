using NERA.Energy;
using NERA.Items;
using UnityEngine;

namespace NERA.Research
{
    [DisallowMultipleComponent]
    public sealed class LaboratoryTableItemVisuals : MonoBehaviour
    {
        private const int UpgradeSlotCount =
            LaboratoryWorkstationController.UpgradeSlotCapacity;

        private readonly Transform[] upgradeSlots =
            new Transform[UpgradeSlotCount];
        private readonly ItemInstance[] upgradeSources =
            new ItemInstance[UpgradeSlotCount];
        private readonly GameObject[] upgradeVisuals =
            new GameObject[UpgradeSlotCount];

        private Transform scanSlot;
        private ItemInstance scanSource;
        private GameObject scanVisual;
        private ResearchController subscribedResearch;
        private LaboratoryWorkstationController subscribedWorkstation;

        public GameObject ScanVisual => scanVisual;

        public GameObject GetUpgradeVisual(int index)
        {
            return IsValidIndex(upgradeVisuals, index)
                ? upgradeVisuals[index]
                : null;
        }

        private void Awake()
        {
            ResolveSlots();
        }

        private void OnEnable()
        {
            RefreshBindings();
            RefreshAll();
        }

        private void LateUpdate()
        {
            if (ReferenceEquals(
                    subscribedResearch,
                    ResearchController.Instance) &&
                ReferenceEquals(
                    subscribedWorkstation,
                    LaboratoryWorkstationController.Instance))
            {
                return;
            }

            RefreshBindings();
            RefreshAll();
        }

        private void OnDisable()
        {
            UnbindControllers();
            ClearAllVisuals();
        }

        private void ResolveSlots()
        {
            scanSlot = transform.Find("Slot_Scan/Slot_1");
            for (int index = 0; index < UpgradeSlotCount; index++)
            {
                upgradeSlots[index] = transform.Find(
                    $"Slot_Upgrade/Slot_{index + 1}");
            }
        }

        private void RefreshBindings()
        {
            UnbindControllers();

            subscribedResearch = ResearchController.Instance;
            if (subscribedResearch != null)
                subscribedResearch.StateChanged += HandleResearchStateChanged;

            subscribedWorkstation = LaboratoryWorkstationController.Instance;
            if (subscribedWorkstation != null)
                subscribedWorkstation.ItemsChanged += HandleWorkstationItemsChanged;
        }

        private void UnbindControllers()
        {
            if (subscribedResearch != null)
                subscribedResearch.StateChanged -= HandleResearchStateChanged;
            if (subscribedWorkstation != null)
                subscribedWorkstation.ItemsChanged -= HandleWorkstationItemsChanged;

            subscribedResearch = null;
            subscribedWorkstation = null;
        }

        private void HandleResearchStateChanged(
            ResearchController.ResearchState _)
        {
            RefreshScanVisual();
        }

        private void HandleWorkstationItemsChanged()
        {
            RefreshWorkstationVisuals();
        }

        private void RefreshAll()
        {
            RefreshScanVisual();
            RefreshWorkstationVisuals();
        }

        private void RefreshScanVisual()
        {
            ItemInstance source = subscribedResearch?.LoadedItemInstance;
            RefreshSlot(
                scanSlot,
                source,
                ref scanSource,
                ref scanVisual);
        }

        private void RefreshWorkstationVisuals()
        {
            for (int index = 0; index < UpgradeSlotCount; index++)
            {
                RefreshSlot(
                    upgradeSlots[index],
                    subscribedWorkstation?.GetUpgradeItem(index),
                    ref upgradeSources[index],
                    ref upgradeVisuals[index]);
            }
        }

        private static void RefreshSlot(
            Transform slot,
            ItemInstance source,
            ref ItemInstance currentSource,
            ref GameObject currentVisual)
        {
            if (ReferenceEquals(source, currentSource) && currentVisual != null)
                return;

            DestroyVisual(ref currentVisual);
            currentSource = source?.ItemData != null ? source : null;
            if (slot == null || currentSource == null)
                return;

            WorldItem worldPrefab = currentSource.ItemData.WorldPrefab;
            if (worldPrefab == null)
                return;

            WorldItem worldVisual = Instantiate(worldPrefab, slot, false);
            worldVisual.Initialize(currentSource);
            currentVisual = worldVisual.gameObject;
            currentVisual.name = $"Visual_{currentSource.ItemData.ItemId}";
            currentVisual.transform.localPosition = Vector3.zero;
            currentVisual.transform.localRotation = Quaternion.identity;
            SetLayerRecursively(currentVisual, slot.gameObject.layer);
            MakeVisualOnly(currentVisual);
        }

        private void ClearAllVisuals()
        {
            DestroyVisual(ref scanVisual);
            scanSource = null;

            for (int index = 0; index < UpgradeSlotCount; index++)
            {
                DestroyVisual(ref upgradeVisuals[index]);
                upgradeSources[index] = null;
            }
        }

        private static void DestroyVisual(ref GameObject visual)
        {
            if (visual != null)
            {
                if (Application.isPlaying)
                    Destroy(visual);
                else
                    DestroyImmediate(visual);
            }

            visual = null;
        }

        private static void MakeVisualOnly(GameObject root)
        {
            foreach (MonoBehaviour behaviour in
                     root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour != null)
                    behaviour.enabled = false;
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
                body.detectCollisions = false;
            }

            foreach (ParticleSystem particles in
                     root.GetComponentsInChildren<ParticleSystem>(true))
            {
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform)
                SetLayerRecursively(child.gameObject, layer);
        }

        private static bool IsValidIndex(GameObject[] values, int index)
        {
            return index >= 0 && index < values.Length;
        }
    }
}
