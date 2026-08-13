using System;
using System.Collections.Generic;
using NERA.Inventory;
using NERA.Items;
using NERA.Player;
using NERA.Save;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NERA.Station
{
    [DisallowMultipleComponent]
    public sealed class StationUpgradeModeController : MonoBehaviour
    {
        private enum ItemSource
        {
            Inventory,
            Storage
        }

        private sealed class StagedPart
        {
            public StationUpgradeSlot Slot;
            public ItemInstance Item;
            public ItemSource Source;
        }

        [SerializeField] private Button applyButton;
        [SerializeField] private GameObject upgradeScreen;
        [SerializeField, Min(1)] private int cameraPriority = 100;
        [SerializeField, Min(1f)] private float clickDistance = 1000f;
        [SerializeField, Min(1f)] private float orbitDegreesPerSecond = 90f;

        private readonly Dictionary<StationUpgradeSlot, StagedPart> staged =
            new Dictionary<StationUpgradeSlot, StagedPart>();
        private StationUpgradeableObject activeObject;
        private PlayerInventory inventory;
        private StationStorageController storage;
        private ParkourPlayerBridge player;
        private Camera raycastCamera;
        private CinemachineOrbitalTransposer orbitalTransposer;
        private string previousOrbitInputAxisName;
        private float previousOrbitAxisValue;
        private bool hasPreviousOrbitAxisValue;
        private bool previousHeadingRecenteringEnabled;
        private PrioritySettings previousCameraPriority;
        private bool hasPreviousCameraPriority;
        private Transform upgradeCameraTransform;
        private Vector3 previousCameraLocalPosition;
        private Quaternion previousCameraLocalRotation;
        private bool hasPreviousCameraTransform;
        private AutoSaveService guardedAutoSave;
        private bool autoSaveWasSuspended;
        private bool autoSaveGuardActive;

        public static StationUpgradeModeController Instance { get; private set; }
        public bool IsOpen => activeObject != null;
        public StationUpgradeableObject ActiveObject => activeObject;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            ResolveApplyButton();
            ResolveUpgradeScreen();
            SetUpgradeScreenVisible(false);
        }

        private void Update()
        {
            if (!IsOpen)
                return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
                return;
            }

            UpdateOrbit();

            if (!Input.GetMouseButtonDown(0) ||
                EventSystem.current != null &&
                EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            TrySelectSlot();
        }

        public static StationUpgradeModeController GetOrCreate()
        {
            if (Instance != null)
                return Instance;

            Button targetButton = FindUpgradeButton();
            Canvas canvas = targetButton != null
                ? targetButton.GetComponentInParent<Canvas>(true)
                : null;
            GameObject host = canvas != null
                ? canvas.gameObject
                : new GameObject(nameof(StationUpgradeModeController));
            StationUpgradeModeController controller =
                host.GetComponent<StationUpgradeModeController>() ??
                host.AddComponent<StationUpgradeModeController>();
            if (targetButton != null)
                controller.applyButton = targetButton;
            controller.ResolveUpgradeScreen();
            controller.BindApplyButton();
            return controller;
        }

        public bool Open(
            StationUpgradeableObject target,
            GameObject interactor)
        {
            if (target == null || interactor == null || IsOpen ||
                target.IsFullyUpgraded)
                return false;

            player = interactor.GetComponentInParent<ParkourPlayerBridge>();
            inventory = interactor.GetComponentInParent<PlayerInventory>();
            storage = StationStorageController.Instance;
            raycastCamera = player != null
                ? player.GameplayCamera
                : Camera.main;
            if (target.UpgradeCamera == null || raycastCamera == null ||
                inventory == null)
            {
                Debug.LogError(
                    $"{target.name}: upgrade camera, gameplay camera or " +
                    "player inventory is missing.",
                    target);
                return false;
            }

            BeginAutoSaveGuard();
            activeObject = target;
            BindPartSources();
            RefreshAvailableSlotVisuals();
            target.SetUpgradeVisualsVisible(true);
            previousCameraPriority = target.UpgradeCamera.Priority;
            hasPreviousCameraPriority = true;
            upgradeCameraTransform = target.UpgradeCamera.transform;
            previousCameraLocalPosition =
                upgradeCameraTransform.localPosition;
            previousCameraLocalRotation =
                upgradeCameraTransform.localRotation;
            hasPreviousCameraTransform = true;
            target.UpgradeCamera.Priority = cameraPriority;
            orbitalTransposer = target.UpgradeCamera
                .GetComponentInChildren<CinemachineOrbitalTransposer>(true);
            if (orbitalTransposer != null)
            {
                previousOrbitInputAxisName =
                    orbitalTransposer.m_XAxis.m_InputAxisName;
                previousOrbitAxisValue = orbitalTransposer.m_XAxis.Value;
                hasPreviousOrbitAxisValue = true;
                previousHeadingRecenteringEnabled = orbitalTransposer
                    .m_RecenterToTargetHeading.m_enabled;
                orbitalTransposer.m_XAxis.m_InputAxisName = string.Empty;
                orbitalTransposer.m_XAxis.m_InputAxisValue = 0f;
                orbitalTransposer.m_XAxis.Reset();
                orbitalTransposer.m_RecenterToTargetHeading.m_enabled = false;
                orbitalTransposer.m_RecenterToTargetHeading
                    .CancelRecentering();
            }
            player?.SetInputEnabled(this, false);
            InventoryLabHUDController.Instance?.SetExternalUiLock(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SetUpgradeScreenVisible(true);
            SetApplyButtonVisible(false);
            return true;
        }

        public void Apply()
        {
            if (!IsOpen || staged.Count == 0)
                return;

            var requests = new List<StationPartInstallRequest>(staged.Count);
            foreach (StagedPart part in staged.Values)
            {
                requests.Add(new StationPartInstallRequest(
                    part.Slot.SlotId,
                    part.Item.ItemData));
            }

            StationSystemsController systems =
                StationSystemsController.Instance;
            string reason = systems == null
                ? "Station systems controller is missing."
                : string.Empty;
            if (systems == null || !systems.TryInstallParts(
                    activeObject.SystemType,
                    activeObject.ObjectId,
                    requests,
                    out reason))
            {
                Debug.LogWarning(
                    $"Station upgrade was not applied: {reason}",
                    activeObject);
                return;
            }

            staged.Clear();
            activeObject.RefreshVisuals();
            RefreshAvailableSlotVisuals();
            SetApplyButtonVisible(false);
            if (activeObject.IsFullyUpgraded)
                Close();
        }

        public void Close()
        {
            Close(false);
        }

        public void PrepareForSessionEnd()
        {
            if (IsOpen)
                Close(true);
            else
                EndAutoSaveGuard(true);
        }

        private void Close(bool flushReturnedParts)
        {
            if (!IsOpen)
                return;

            RollbackAll();
            UnbindPartSources();
            if (activeObject.UpgradeCamera != null &&
                hasPreviousCameraPriority)
            {
                activeObject.UpgradeCamera.Priority = previousCameraPriority;
            }

            activeObject.SetUpgradeVisualsVisible(false);
            activeObject = null;
            hasPreviousCameraPriority = false;
            if (orbitalTransposer != null)
            {
                orbitalTransposer.m_XAxis.m_InputAxisName =
                    previousOrbitInputAxisName;
                orbitalTransposer.m_XAxis.m_InputAxisValue = 0f;
                orbitalTransposer.m_XAxis.Reset();
                if (hasPreviousOrbitAxisValue)
                    orbitalTransposer.m_XAxis.Value = previousOrbitAxisValue;
                orbitalTransposer.m_RecenterToTargetHeading.m_enabled =
                    previousHeadingRecenteringEnabled;
                orbitalTransposer.m_RecenterToTargetHeading
                    .CancelRecentering();
            }
            if (upgradeCameraTransform != null &&
                hasPreviousCameraTransform)
            {
                upgradeCameraTransform.localPosition =
                    previousCameraLocalPosition;
                upgradeCameraTransform.localRotation =
                    previousCameraLocalRotation;
            }
            orbitalTransposer = null;
            previousOrbitInputAxisName = null;
            previousOrbitAxisValue = 0f;
            hasPreviousOrbitAxisValue = false;
            previousHeadingRecenteringEnabled = false;
            upgradeCameraTransform = null;
            previousCameraLocalPosition = Vector3.zero;
            previousCameraLocalRotation = Quaternion.identity;
            hasPreviousCameraTransform = false;
            SetUpgradeScreenVisible(false);
            player?.SetInputEnabled(this, true);
            InventoryLabHUDController.Instance?.SetExternalUiLock(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            EndAutoSaveGuard(flushReturnedParts);
            player = null;
            inventory = null;
            storage = null;
            raycastCamera = null;
        }

        private void UpdateOrbit()
        {
            if (orbitalTransposer == null)
                return;

            float direction = 0f;
            if (Input.GetKey(KeyCode.A))
                direction += 1f;
            if (Input.GetKey(KeyCode.D))
                direction -= 1f;

            orbitalTransposer.m_XAxis.m_InputAxisValue = 0f;
            if (Mathf.Approximately(direction, 0f))
                return;

            float value = orbitalTransposer.m_XAxis.Value +
                direction * orbitDegreesPerSecond * Time.unscaledDeltaTime;
            orbitalTransposer.m_XAxis.Value = Mathf.Repeat(
                value + 180f,
                360f) - 180f;
        }

        private void TrySelectSlot()
        {
            Ray ray = raycastCamera.ScreenPointToRay(Input.mousePosition);
            StationUpgradeSlot slot = FindSlotHit(
                ray,
                activeObject,
                clickDistance);
            if (slot == null)
                return;

            ToggleSlot(slot);
        }

        public bool ToggleSlot(StationUpgradeSlot slot)
        {
            if (!IsOpen || slot == null ||
                activeObject.FindSlot(slot.SlotId) != slot)
            {
                return false;
            }

            if (staged.TryGetValue(slot, out StagedPart existing))
            {
                ReturnToSource(existing);
                staged.Remove(slot);
                activeObject.RestoreSlot(slot);
                RefreshAvailableSlotVisuals();
                SetApplyButtonVisible(staged.Count > 0);
                return true;
            }

            if (!string.IsNullOrEmpty(
                    StationSystemsController.Instance?
                        .GetInstalledPartItemId(
                            activeObject.SystemType,
                            activeObject.ObjectId,
                            slot.SlotId)))
            {
                return false;
            }

            if (!TryTakeCompatiblePart(slot, out StagedPart stagedPart))
            {
                Debug.Log(
                    $"No compatible engineering part for " +
                    $"{activeObject.name}/{slot.SlotId} in inventory or storage.",
                    activeObject);
                return false;
            }

            staged[slot] = stagedPart;
            activeObject.ShowStaged(slot, stagedPart.Item.ItemData);
            RefreshAvailableSlotVisuals();
            SetApplyButtonVisible(true);
            return true;
        }

        public static StationUpgradeSlot FindSlotHit(
            Ray ray,
            StationUpgradeableObject target,
            float maxDistance)
        {
            if (target == null || maxDistance <= 0f)
                return null;

            RaycastHit[] hits = Physics.RaycastAll(
                ray,
                maxDistance,
                ~0,
                QueryTriggerInteraction.Collide);
            if (hits.Length == 0)
                return null;

            Array.Sort(
                hits,
                (left, right) => left.distance.CompareTo(right.distance));
            foreach (RaycastHit hit in hits)
            {
                Collider collider = hit.collider;
                if (collider == null)
                    continue;

                StationUpgradeSlot slot =
                    collider.GetComponentInParent<StationUpgradeSlot>();
                if (slot != null && target.FindSlot(slot.SlotId) == slot)
                    return slot;

                if (!collider.transform.IsChildOf(target.transform))
                    return null;
            }

            return null;
        }

        private bool TryTakeCompatiblePart(
            StationUpgradeSlot slot,
            out StagedPart stagedPart)
        {
            stagedPart = null;
            InventorySlotGroup[] groups =
            {
                InventorySlotGroup.Backpack,
                InventorySlotGroup.QuickAccess,
                InventorySlotGroup.Anomaly
            };

            foreach (InventorySlotGroup group in groups)
            {
                IReadOnlyList<ItemInstance> slots = GetInventorySlots(group);
                for (int index = 0; index < slots.Count; index++)
                {
                    ItemInstance candidate = slots[index];
                    if (!Fits(candidate, slot) ||
                        !inventory.RemoveInstanceAt(
                            group,
                            index,
                            out ItemInstance removed))
                    {
                        continue;
                    }

                    stagedPart = new StagedPart
                    {
                        Slot = slot,
                        Item = removed,
                        Source = ItemSource.Inventory
                    };
                    return true;
                }
            }

            if (storage == null)
                return false;

            foreach (InventorySlotGroup group in groups)
            {
                IReadOnlyList<ItemInstance> slots = storage.GetSlots(group);
                for (int index = 0; index < slots.Count; index++)
                {
                    ItemInstance candidate = slots[index];
                    if (!Fits(candidate, slot) ||
                        !storage.RemoveInstanceAt(
                            group,
                            index,
                            out ItemInstance removed))
                    {
                        continue;
                    }

                    stagedPart = new StagedPart
                    {
                        Slot = slot,
                        Item = removed,
                        Source = ItemSource.Storage
                    };
                    return true;
                }
            }

            return false;
        }

        private bool Fits(ItemInstance candidate, StationUpgradeSlot slot)
        {
            return candidate?.ItemData?.FindEngineeringCompatibility(
                activeObject.SystemType,
                activeObject.ObjectId,
                slot.SlotId) != null;
        }

        public void RefreshAvailableSlotVisuals()
        {
            if (!IsOpen)
                return;

            var availableSlotIds = new List<string>();
            IReadOnlyList<StationUpgradeSlot> slots = activeObject.Slots;
            if (slots != null)
            {
                foreach (StationUpgradeSlot slot in slots)
                {
                    if (slot != null && HasCompatiblePart(slot))
                        availableSlotIds.Add(slot.SlotId);
                }
            }

            activeObject.SetAvailableEmptySlots(availableSlotIds);
        }

        private bool HasCompatiblePart(StationUpgradeSlot slot)
        {
            if (slot == null)
                return false;

            InventorySlotGroup[] groups =
            {
                InventorySlotGroup.Backpack,
                InventorySlotGroup.QuickAccess,
                InventorySlotGroup.Anomaly
            };
            if (inventory != null)
            {
                foreach (InventorySlotGroup group in groups)
                {
                    foreach (ItemInstance candidate in GetInventorySlots(group))
                    {
                        if (Fits(candidate, slot))
                            return true;
                    }
                }
            }

            if (storage == null)
                return false;

            foreach (InventorySlotGroup group in groups)
            {
                foreach (ItemInstance candidate in storage.GetSlots(group))
                {
                    if (Fits(candidate, slot))
                        return true;
                }
            }

            return false;
        }

        private IReadOnlyList<ItemInstance> GetInventorySlots(
            InventorySlotGroup group)
        {
            return group switch
            {
                InventorySlotGroup.Anomaly => inventory.AnomalyItemInstances,
                InventorySlotGroup.QuickAccess =>
                    inventory.QuickAccessItemInstances,
                _ => inventory.BackpackItemInstances
            };
        }

        private void RollbackAll()
        {
            foreach (StagedPart part in staged.Values)
                ReturnToSource(part);
            staged.Clear();
        }

        private void BindPartSources()
        {
            if (inventory != null)
                inventory.InventoryChanged += RefreshAvailableSlotVisuals;
            if (storage != null)
                storage.StorageChanged += RefreshAvailableSlotVisuals;
        }

        private void UnbindPartSources()
        {
            if (inventory != null)
                inventory.InventoryChanged -= RefreshAvailableSlotVisuals;
            if (storage != null)
                storage.StorageChanged -= RefreshAvailableSlotVisuals;
        }

        private void ReturnToSource(StagedPart part)
        {
            if (part?.Item?.ItemData == null)
                return;

            bool returned = part.Source == ItemSource.Storage
                ? storage != null && storage.Deposit(part.Item)
                : inventory != null && inventory.AddItem(part.Item);
            if (!returned)
            {
                returned = part.Source == ItemSource.Storage
                    ? inventory != null && inventory.AddItem(part.Item)
                    : storage != null && storage.Deposit(part.Item);
            }

            if (!returned)
            {
                Debug.LogError(
                    $"Could not return staged part " +
                    $"'{part.Item.ItemData.ItemId}'.",
                    this);
            }
        }

        private void BeginAutoSaveGuard()
        {
            guardedAutoSave = AutoSaveService.Instance;
            if (guardedAutoSave == null)
                return;

            autoSaveWasSuspended = guardedAutoSave.IsSuspended;
            if (!autoSaveWasSuspended)
                guardedAutoSave.Flush();
            guardedAutoSave.SetSuspended(true);
            autoSaveGuardActive = true;
        }

        private void EndAutoSaveGuard(bool flush)
        {
            if (!autoSaveGuardActive)
                return;

            AutoSaveService autoSave = guardedAutoSave;
            bool restoreSuspended = autoSaveWasSuspended;
            guardedAutoSave = null;
            autoSaveWasSuspended = false;
            autoSaveGuardActive = false;
            if (autoSave == null)
                return;

            autoSave.SetSuspended(restoreSuspended);
            if (restoreSuspended)
                return;

            autoSave.MarkDirty();
            if (flush)
                autoSave.Flush();
        }

        private void ResolveApplyButton()
        {
            applyButton ??= FindUpgradeButton();
            ResolveUpgradeScreen();
            BindApplyButton();
        }

        private void ResolveUpgradeScreen()
        {
            if (upgradeScreen != null)
                return;

            Transform current = applyButton != null
                ? applyButton.transform
                : null;
            while (current != null)
            {
                if (current.name == "UpgradeScreen")
                {
                    upgradeScreen = current.gameObject;
                    return;
                }
                current = current.parent;
            }
        }

        private void BindApplyButton()
        {
            if (applyButton == null)
                return;
            applyButton.onClick.RemoveListener(Apply);
            applyButton.onClick.AddListener(Apply);
        }

        private void SetApplyButtonVisible(bool visible)
        {
            ResolveApplyButtonIfNeeded();
            if (applyButton != null)
                applyButton.gameObject.SetActive(visible);
        }

        private void SetUpgradeScreenVisible(bool visible)
        {
            ResolveUpgradeScreen();
            if (upgradeScreen != null)
                upgradeScreen.SetActive(visible);
            if (!visible && applyButton != null)
                applyButton.gameObject.SetActive(false);
        }

        private void ResolveApplyButtonIfNeeded()
        {
            if (applyButton == null)
            {
                applyButton = FindUpgradeButton();
                BindApplyButton();
            }
        }

        private static Button FindUpgradeButton()
        {
            Button[] buttons = FindObjectsByType<Button>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (Button button in buttons)
            {
                if (button != null && button.name == "UpgradesButton")
                    return button;
            }
            return null;
        }

        private void OnDestroy()
        {
            if (IsOpen)
                Close(true);
            else
                EndAutoSaveGuard(true);
            if (applyButton != null)
                applyButton.onClick.RemoveListener(Apply);
            if (Instance == this)
                Instance = null;
        }

        private void OnApplicationQuit()
        {
            PrepareForSessionEnd();
        }
    }
}
