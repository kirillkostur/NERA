using NERA.Interaction;
using NERA.Items;
using NERA.Research;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NERA.Inventory
{
    /// <summary>
    /// Single owner of inventory, quick-access and laboratory UI stored in Boot/HUD_Canvas.
    /// World objects only request panels to open; they never create canvases.
    /// </summary>
    public sealed class InventoryLabHUDController : MonoBehaviour
    {
        private static readonly Color SelectedColor =
            new Color(0.04f, 0.42f, 0.50f, 1f);
        private static readonly Color MissingIconColor =
            new Color(0.18f, 0.28f, 0.31f, 1f);

        public static InventoryLabHUDController Instance { get; private set; }

        private readonly List<InventorySlotView> backpackViews =
            new List<InventorySlotView>();
        private readonly List<InventorySlotView> anomalyViews =
            new List<InventorySlotView>();
        private readonly List<InventorySlotView> quickViews =
            new List<InventorySlotView>();

        private Canvas rootCanvas;
        private GameObject inventoryPanel;
        private GameObject laboratoryPanel;
        private Transform backpackSlotRoot;
        private Transform anomalySlotRoot;
        private Transform quickSlotRoot;
        private TMP_Text selectedItemName;
        private TMP_Text selectedItemDescription;
        private Button dropButton;
        private Image laboratorySlotIcon;
        private TMP_Text laboratoryStatusLabel;
        private TMP_Text scanButtonLabel;
        private Button scanButton;
        private Button takeButton;
        private PlayerInventory inventory;
        private ItemData selectedItem;
        private InventorySlotGroup selectedGroup;
        private int selectedIndex = -1;
        private PlayerController playerController;
        private PlayerInteractionController interactionController;
        private PlayerFollowCamera followCamera;
        private bool inventoryOpen;
        private bool laboratoryOpen;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            rootCanvas = GetComponent<Canvas>();
            CacheHierarchy();
            inventoryPanel.SetActive(false);
            laboratoryPanel.SetActive(false);
        }

        private void Start()
        {
            BindInventory(FindFirstObjectByType<PlayerInventory>());
            BuildSlotViews();
            BindButtons();
            RefreshAll();
        }

        private void Update()
        {
            if (inventory == null)
                BindInventory(FindFirstObjectByType<PlayerInventory>());

            if (Input.GetKeyDown(KeyCode.I))
            {
                if (inventoryOpen || laboratoryOpen)
                    CloseAll();
                else
                    OpenInventory();
            }
            else if ((inventoryOpen || laboratoryOpen) &&
                     Input.GetKeyDown(KeyCode.Escape))
            {
                CloseAll();
            }

            if (!inventoryOpen && !laboratoryOpen && inventory != null)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1)) inventory.SelectQuickAccess(0);
                if (Input.GetKeyDown(KeyCode.Alpha2)) inventory.SelectQuickAccess(1);
                if (Input.GetKeyDown(KeyCode.Alpha3)) inventory.SelectQuickAccess(2);
            }

            if (laboratoryOpen)
                RefreshLaboratory();
        }

        public void OpenInventory()
        {
            BindInventory(inventory != null ? inventory : FindFirstObjectByType<PlayerInventory>());
            laboratoryOpen = false;
            inventoryOpen = true;
            laboratoryPanel.SetActive(false);
            inventoryPanel.SetActive(true);
            SetPlayerInput(false);
            RefreshAll();
        }

        public void OpenLaboratory(GameObject interactor)
        {
            PlayerInventory playerInventory = interactor != null
                ? interactor.GetComponentInParent<PlayerInventory>()
                : FindFirstObjectByType<PlayerInventory>();
            BindInventory(playerInventory);

            laboratoryOpen = true;
            inventoryOpen = true;
            inventoryPanel.SetActive(true);
            laboratoryPanel.SetActive(true);
            SetPlayerInput(false);
            RefreshAll();
        }

        public void CloseAll()
        {
            inventoryOpen = false;
            laboratoryOpen = false;
            selectedItem = null;
            selectedIndex = -1;
            inventoryPanel.SetActive(false);
            laboratoryPanel.SetActive(false);
            SetPlayerInput(true);
        }

        private void CacheHierarchy()
        {
            inventoryPanel = transform.Find("InventoryPanel").gameObject;
            laboratoryPanel = transform.Find("LaboratoryPanel").gameObject;

            selectedItemName = inventoryPanel.transform
                .Find("SelectionPanel/Name")
                .GetComponent<TMP_Text>();
            selectedItemDescription = inventoryPanel.transform
                .Find("SelectionPanel/Description")
                .GetComponent<TMP_Text>();
            dropButton = inventoryPanel.transform.Find("DropButton").GetComponent<Button>();
            laboratorySlotIcon = laboratoryPanel.transform
                .Find("SampleSlot/Icon")
                .GetComponent<Image>();
            laboratoryStatusLabel = laboratoryPanel.transform.Find("Status").GetComponent<TMP_Text>();
            scanButton = laboratoryPanel.transform.Find("ScanButton").GetComponent<Button>();
            scanButtonLabel = scanButton.GetComponentInChildren<TMP_Text>();
            takeButton = laboratoryPanel.transform.Find("TakeButton").GetComponent<Button>();
        }

        private void BuildSlotViews()
        {
            if (inventory == null)
            {
                Debug.LogError(
                    "InventoryLabHUDController: PlayerInventory is missing.",
                    this
                );
                return;
            }

            InventoryConfig inventoryConfig = inventory.Config;
            if (inventoryConfig == null || inventoryConfig.SlotPrefab == null)
            {
                Debug.LogError(
                    "InventoryLabHUDController: PlayerInventory Config or its slot prefab is missing.",
                    this
                );
                return;
            }

            backpackSlotRoot = inventoryPanel.transform.Find(
                "Backpack/Scroll View/Viewport/Content"
            );
            if (backpackSlotRoot == null)
            {
                Debug.LogError(
                    "InventoryLabHUDController: backpack slot root " +
                    "'Backpack/Scroll View/Viewport/Content' is missing.",
                    this
                );
                return;
            }

            CacheBackpackSlotViews(
                backpackSlotRoot,
                backpackViews,
                inventoryConfig
            );
            Transform anomaliesRoot = inventoryPanel.transform.Find(
                "Anomalies/Background"
            );
            if (anomaliesRoot == null)
                anomaliesRoot = inventoryPanel.transform.Find("Anomalies");

            anomalySlotRoot = CacheFixedSlotViews(
                anomaliesRoot,
                anomalyViews,
                PlayerInventory.AnomalyCapacity,
                false
            );
            quickSlotRoot = CacheFixedSlotViews(
                transform.Find("QuickAccessHUD"),
                quickViews,
                PlayerInventory.QuickAccessCapacity,
                true
            );
        }

        private void CacheBackpackSlotViews(
            Transform parent,
            List<InventorySlotView> destination,
            InventoryConfig inventoryConfig
        )
        {
            destination.Clear();
            int capacity = inventoryConfig.BackpackCapacity;

            for (int i = 0; i < InventoryConfig.MaxBackpackCapacity; i++)
            {
                Transform spawnPoint = parent.Find($"Slot_{i + 1}");
                if (spawnPoint == null)
                {
                    if (i < capacity)
                    {
                        Debug.LogError(
                            $"InventoryLabHUDController: backpack spawn point " +
                            $"'Slot_{i + 1}' is missing.",
                            this
                        );
                    }

                    continue;
                }

                bool shouldBeActive = i < capacity;
                spawnPoint.gameObject.SetActive(shouldBeActive);
                if (!shouldBeActive)
                    continue;

                InventorySlotView view =
                    spawnPoint.GetComponentInChildren<InventorySlotView>(true);
                if (view == null)
                {
                    view = Instantiate(
                        inventoryConfig.SlotPrefab,
                        spawnPoint,
                        false
                    );

                    RectTransform viewRect =
                        view.transform as RectTransform;
                    if (viewRect != null)
                        viewRect.anchoredPosition = Vector2.zero;

                    view.transform.localRotation = Quaternion.identity;
                    view.transform.localScale = Vector3.one;
                }

                view.gameObject.SetActive(true);
                view.Initialize(i, false, rootCanvas);
                destination.Add(view);
            }
        }

        private Transform CacheFixedSlotViews(
            Transform parent,
            List<InventorySlotView> destination,
            int count,
            bool showQuickAccessNumbers
        )
        {
            destination.Clear();

            for (int i = 0; i < count; i++)
            {
                Transform slot = parent.Find($"Slot_{i + 1}");
                if (slot == null)
                {
                    Debug.LogError(
                        $"InventoryLabHUDController: '{parent.name}/Slot_{i + 1}' is missing.",
                        this
                    );
                    continue;
                }

                InventorySlotView view =
                    slot.GetComponent<InventorySlotView>();
                if (view == null)
                    view = slot.gameObject.AddComponent<InventorySlotView>();

                view.Initialize(i, showQuickAccessNumbers, rootCanvas);
                destination.Add(view);
            }

            return parent;
        }

        private void BindButtons()
        {
            for (int i = 0; i < backpackViews.Count; i++)
            {
                int index = i;
                backpackViews[i].Button.onClick.AddListener(
                    () => SelectItem(InventorySlotGroup.Backpack, index)
                );
            }

            for (int i = 0; i < anomalyViews.Count; i++)
            {
                int index = i;
                anomalyViews[i].Button.onClick.AddListener(
                    () => SelectItem(InventorySlotGroup.Anomaly, index)
                );
            }

            for (int i = 0; i < quickViews.Count; i++)
            {
                int index = i;
                quickViews[i].Button.onClick.AddListener(
                    () => SelectQuickItem(index)
                );
            }

            dropButton.onClick.AddListener(DropSelected);
            Button laboratoryCloseButton = FindButtonInChildren(
                laboratoryPanel.transform,
                "CloseButton"
            );
            if (laboratoryCloseButton != null)
            {
                laboratoryCloseButton.onClick.AddListener(CloseAll);
            }
            else
            {
                Debug.LogWarning(
                    "InventoryLabHUDController: laboratory close button was not found. Keyboard closing remains available.",
                    this
                );
            }
            scanButton.onClick.AddListener(StartScan);
            takeButton.onClick.AddListener(TakeSample);
            laboratoryPanel.transform.Find("SampleSlot").GetComponent<LaboratoryItemDropSlot>()
                .ItemDropped += LoadSample;
        }

        private static Button FindButtonInChildren(
            Transform root,
            string objectName
        )
        {
            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            foreach (Button button in buttons)
            {
                if (button.name == objectName)
                    return button;
            }

            return null;
        }

        private void BindInventory(PlayerInventory playerInventory)
        {
            if (inventory == playerInventory)
                return;

            if (inventory != null)
                inventory.InventoryChanged -= RefreshAll;

            inventory = playerInventory;
            if (inventory != null)
            {
                inventory.InventoryChanged += RefreshAll;
                playerController = inventory.GetComponent<PlayerController>();
                interactionController = inventory.GetComponent<PlayerInteractionController>();
            }
            followCamera = FindFirstObjectByType<PlayerFollowCamera>();
        }

        private void SelectItem(InventorySlotGroup group, int index)
        {
            SetSelection(group, index);
        }

        private void SelectQuickItem(int index)
        {
            SetSelection(InventorySlotGroup.QuickAccess, index);
            inventory?.SelectQuickAccess(index);
        }

        private void DropSelected()
        {
            if (selectedItem == null || inventory == null || selectedIndex < 0)
                return;

            if (inventory.DropItemAt(
                selectedGroup,
                selectedIndex,
                inventory.transform.position,
                inventory.transform.forward
            ))
            {
                ClearSelection();
                RefreshAll();
            }
        }

        private void LoadSample(ItemData item)
        {
            if (!laboratoryOpen || item == null || item.ItemType != ItemType.ResearchSample)
                return;

            ResearchController controller = ResearchController.Instance;
            if (controller != null && controller.LoadItem(item, inventory))
            {
                ClearSelection();
                RefreshAll();
            }
        }

        private void StartScan()
        {
            ResearchController.Instance?.StartAnalysis();
            RefreshLaboratory();
        }

        private void TakeSample()
        {
            ResearchController.Instance?.RetrieveLoadedItem();
            RefreshAll();
        }

        private void RefreshAll()
        {
            if (inventory == null)
                return;

            ValidateSelection();
            RefreshSlots(
                backpackViews,
                inventory.BackpackSlots,
                InventorySlotGroup.Backpack
            );
            RefreshSlots(
                anomalyViews,
                inventory.AnomalySlots,
                InventorySlotGroup.Anomaly
            );
            RefreshSlots(
                quickViews,
                inventory.QuickAccessSlots,
                InventorySlotGroup.QuickAccess
            );
            RefreshSelection();

            for (int i = 0; i < backpackViews.Count; i++)
            {
                ItemData item = inventory.GetItem(InventorySlotGroup.Backpack, i);
                LaboratoryInventoryItemDrag drag =
                    backpackViews[i].LaboratoryDrag;
                if (drag != null)
                {
                    drag.Initialize(
                        item != null && item.ItemType == ItemType.ResearchSample
                            ? item
                            : null,
                        rootCanvas
                    );
                }
            }

            RefreshLaboratory();
        }

        private void RefreshLaboratory()
        {
            ResearchController controller = ResearchController.Instance;
            if (controller == null)
                return;

            bool analyzing = controller.State == ResearchController.ResearchState.Analyzing;
            bool hasItem = controller.LoadedItem != null;
            bool analyzed = hasItem && controller.IsAnalyzed(controller.LoadedItem);

            laboratorySlotIcon.sprite = hasItem
                ? controller.LoadedItem.Icon
                : null;
            laboratorySlotIcon.color = hasItem && controller.LoadedItem.Icon == null
                ? MissingIconColor
                : Color.white;
            laboratorySlotIcon.enabled = hasItem;
            laboratoryStatusLabel.text = controller.StatusMessage;
            scanButtonLabel.text = analyzing
                ? $"SCANNING {Mathf.RoundToInt(controller.Progress * 100f)}%"
                : analyzed ? "ANALYZED" : "START SCAN";
            scanButton.interactable =
                controller.CanStartAnalysis;
            takeButton.interactable = hasItem && !analyzing;
        }

        private void RefreshSlots(
            List<InventorySlotView> views,
            System.Collections.Generic.IReadOnlyList<ItemData> slots,
            InventorySlotGroup group
        )
        {
            for (int i = 0; i < views.Count; i++)
            {
                ItemData item = i < slots.Count ? slots[i] : null;
                Image icon = views[i].Icon;
                icon.sprite = item != null ? item.Icon : null;
                icon.color = item != null && item.Icon == null
                    ? MissingIconColor
                    : Color.white;
                icon.enabled = item != null;
                views[i].SetSelected(
                    selectedItem != null &&
                    selectedGroup == group &&
                    selectedIndex == i,
                    SelectedColor
                );
            }
        }

        private void SetSelection(InventorySlotGroup group, int index)
        {
            selectedGroup = group;
            selectedIndex = index;
            selectedItem = inventory != null ? inventory.GetItem(group, index) : null;
            if (selectedItem == null)
                selectedIndex = -1;
            RefreshAll();
        }

        private void ClearSelection()
        {
            selectedItem = null;
            selectedIndex = -1;
        }

        private void ValidateSelection()
        {
            if (selectedIndex < 0 || inventory == null)
            {
                ClearSelection();
                return;
            }

            ItemData currentItem = inventory.GetItem(selectedGroup, selectedIndex);
            if (currentItem != selectedItem)
                ClearSelection();
        }

        private void RefreshSelection()
        {
            bool hasSelection = selectedItem != null && selectedIndex >= 0;
            selectedItemName.text = hasSelection
                ? selectedItem.DisplayName
                : "SELECT AN ITEM";
            selectedItemDescription.text = hasSelection
                ? selectedItem.Description
                : string.Empty;
            dropButton.interactable = hasSelection;
        }

        private void SetPlayerInput(bool enabled)
        {
            if (playerController != null)
                playerController.SetInputEnabled(enabled);
            if (interactionController != null)
                interactionController.enabled = enabled;
            if (followCamera != null)
                followCamera.SetInputEnabled(enabled);

            Cursor.lockState = enabled ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !enabled;
        }

        private void OnDestroy()
        {
            if (inventory != null)
                inventory.InventoryChanged -= RefreshAll;
            if (Instance == this)
                Instance = null;
        }
    }
}
