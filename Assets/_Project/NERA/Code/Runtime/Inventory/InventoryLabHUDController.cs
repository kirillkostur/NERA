using NERA.Interaction;
using NERA.Items;
using NERA.Research;
using NERA.Energy;
using NERA.Station;
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
        public PlayerInventory BoundInventory => inventory;

        private readonly List<InventorySlotView> backpackViews =
            new List<InventorySlotView>();
        private readonly List<InventorySlotView> anomalyViews =
            new List<InventorySlotView>();
        private readonly List<InventorySlotView> quickViews =
            new List<InventorySlotView>();
        private readonly List<InventorySlotView> laboratoryBackpackViews =
            new List<InventorySlotView>();
        private readonly List<InventorySlotView> laboratoryAnomalyViews =
            new List<InventorySlotView>();
        private readonly List<InventorySlotView> laboratoryQuickViews =
            new List<InventorySlotView>();
        private readonly List<InventorySlotView> chargingBackpackViews =
            new List<InventorySlotView>();
        private readonly List<InventorySlotView> chargingAnomalyViews =
            new List<InventorySlotView>();
        private readonly List<InventorySlotView> chargingQuickViews =
            new List<InventorySlotView>();

        private Canvas rootCanvas;
        private GameObject quickAccessHud;
        private GameObject inventoryPanel;
        private GameObject laboratoryPanel;
        private GameObject chargingPanel;
        private ScrollRect backpackScrollRect;
        private Transform backpackSlotRoot;
        private TMP_Text selectedItemName;
        private TMP_Text selectedItemDescription;
        private Button dropButton;
        private Image laboratorySlotIcon;
        private LaboratoryInventoryItemDrag laboratorySlotDrag;
        private LaboratoryItemDropSlot laboratoryDropSlot;
        private TMP_Text laboratoryStatusLabel;
        private TMP_Text scanButtonLabel;
        private Button scanButton;
        private Button takeButton;
        private Image chargingSlotIcon;
        private LaboratoryInventoryItemDrag chargingSlotDrag;
        private LaboratoryItemDropSlot chargingDropSlot;
        private TMP_Text chargingStatusLabel;
        private Button chargingTakeButton;
        private PlayerInventory inventory;
        private ItemData selectedItem;
        private InventorySlotGroup selectedGroup;
        private int selectedIndex = -1;
        private PlayerController playerController;
        private PlayerInteractionController interactionController;
        private PlayerFollowCamera followCamera;
        private bool inventoryOpen;
        private bool laboratoryOpen;
        private bool chargingOpen;
        private bool stationStorageOpen;
        private bool externalUiLocked;
        private bool authoredHud;
        private GameObject laboratoryScanScreen;
        private GameObject laboratoryPowerScreen;
        private Button laboratoryTabButton;
        private Button chargingTabButton;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            rootCanvas = GetComponent<Canvas>();
            authoredHud = transform.Find("InventoryScreen") != null;
            CacheHierarchy();
            inventoryPanel.SetActive(false);
            laboratoryPanel.SetActive(false);
            if (!authoredHud && chargingPanel != null)
                chargingPanel.SetActive(false);
        }

        private void Start()
        {
            BindInventory(FindFirstObjectByType<PlayerInventory>());
            if (authoredHud)
            {
                BuildAuthoredSlotViews();
                BindAuthoredButtons();
            }
            else
            {
                BuildSlotViews();
                BindButtons();
            }
            RefreshAll();
        }

        private void Update()
        {
            if (inventory == null)
                BindInventory(FindFirstObjectByType<PlayerInventory>());

            if (stationStorageOpen || externalUiLocked)
                return;

            if (Input.GetKeyDown(KeyCode.I))
            {
                if (laboratoryOpen || chargingOpen)
                    return;
                if (inventoryOpen)
                    CloseAll();
                else
                    OpenInventory();
            }
            else if ((inventoryOpen || laboratoryOpen || chargingOpen) &&
                     Input.GetKeyDown(KeyCode.Escape))
            {
                CloseAll();
            }

            if (laboratoryOpen)
                RefreshLaboratory();
            if (chargingOpen)
                RefreshChargingTable();
        }

        public void OpenInventory()
        {
            if (externalUiLocked)
                return;

            BindInventory(inventory != null ? inventory : FindFirstObjectByType<PlayerInventory>());
            laboratoryOpen = false;
            chargingOpen = false;
            inventoryOpen = true;
            laboratoryPanel.SetActive(false);
            if (!authoredHud && chargingPanel != null)
                chargingPanel.SetActive(false);
            inventoryPanel.SetActive(true);
            SetQuickAccessVisible(true);
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
            chargingOpen = false;
            inventoryOpen = !authoredHud;
            inventoryPanel.SetActive(!authoredHud);
            laboratoryPanel.SetActive(true);
            if (!authoredHud && chargingPanel != null)
                chargingPanel.SetActive(false);
            if (authoredHud)
                ShowLaboratoryMode(false);
            SetQuickAccessVisible(false);
            SetPlayerInput(false);
            RefreshAll();
        }

        public void OpenChargingTable(GameObject interactor)
        {
            PlayerInventory playerInventory = interactor != null
                ? interactor.GetComponentInParent<PlayerInventory>()
                : FindFirstObjectByType<PlayerInventory>();
            BindInventory(playerInventory);

            laboratoryOpen = false;
            chargingOpen = true;
            inventoryOpen = !authoredHud;
            inventoryPanel.SetActive(!authoredHud);
            laboratoryPanel.SetActive(authoredHud);
            if (chargingPanel != null)
                chargingPanel.SetActive(true);
            if (authoredHud)
                ShowLaboratoryMode(true);
            SetQuickAccessVisible(false);
            SetPlayerInput(false);
            RefreshAll();
        }

        public void OpenStationStorage()
        {
            BindInventory(inventory != null
                ? inventory
                : FindFirstObjectByType<PlayerInventory>());
            stationStorageOpen = true;
            inventoryOpen = false;
            laboratoryOpen = false;
            chargingOpen = false;
            inventoryPanel.SetActive(true);
            laboratoryPanel.SetActive(false);
            chargingPanel.SetActive(false);
            SetQuickAccessVisible(false);
            SetPlayerInput(false);
            RefreshAll();
        }

        public void CloseStationStorage()
        {
            if (!stationStorageOpen)
                return;

            stationStorageOpen = false;
            selectedItem = null;
            selectedIndex = -1;
            inventoryPanel.SetActive(false);
            SetQuickAccessVisible(!externalUiLocked);
        }

        public void SetExternalUiLock(bool locked)
        {
            externalUiLocked = locked;
            if (locked)
            {
                stationStorageOpen = false;
                inventoryOpen = false;
                laboratoryOpen = false;
                chargingOpen = false;
                selectedItem = null;
                selectedIndex = -1;
                inventoryPanel.SetActive(false);
                laboratoryPanel.SetActive(false);
                if (!authoredHud && chargingPanel != null)
                    chargingPanel.SetActive(false);
            }

            SetQuickAccessVisible(!locked && !laboratoryOpen && !chargingOpen);
        }

        public void CloseAll()
        {
            stationStorageOpen = false;
            inventoryOpen = false;
            laboratoryOpen = false;
            chargingOpen = false;
            selectedItem = null;
            selectedIndex = -1;
            inventoryPanel.SetActive(false);
            laboratoryPanel.SetActive(false);
            if (!authoredHud && chargingPanel != null)
                chargingPanel.SetActive(false);
            SetQuickAccessVisible(!externalUiLocked);
            SetPlayerInput(true);
        }

        private void CacheHierarchy()
        {
            if (authoredHud)
            {
                CacheAuthoredHierarchy();
                return;
            }

            inventoryPanel = transform.Find("InventoryPanel").gameObject;
            laboratoryPanel = transform.Find("LaboratoryPanel").gameObject;
            chargingPanel = transform.Find("ChargingPanel").gameObject;
            Transform quickAccessRoot = transform.Find("QuickAccessHUD");
            quickAccessHud = quickAccessRoot != null ? quickAccessRoot.gameObject : null;

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
            Transform laboratorySampleSlot = laboratoryPanel.transform.Find("SampleSlot");
            laboratorySlotDrag = laboratorySampleSlot.GetComponent<LaboratoryInventoryItemDrag>();
            if (laboratorySlotDrag == null)
                laboratorySlotDrag = laboratorySampleSlot.gameObject.AddComponent<LaboratoryInventoryItemDrag>();
            laboratoryDropSlot = laboratorySampleSlot.GetComponent<LaboratoryItemDropSlot>();
            if (laboratoryDropSlot == null)
                laboratoryDropSlot = laboratorySampleSlot.gameObject.AddComponent<LaboratoryItemDropSlot>();
            laboratoryStatusLabel = laboratoryPanel.transform.Find("Status").GetComponent<TMP_Text>();
            scanButton = laboratoryPanel.transform.Find("ScanButton").GetComponent<Button>();
            scanButtonLabel = scanButton.GetComponentInChildren<TMP_Text>();
            takeButton = laboratoryPanel.transform.Find("TakeButton").GetComponent<Button>();

            Transform chargingItemSlot = chargingPanel.transform.Find("ItemSlot");
            chargingSlotIcon = chargingItemSlot.Find("Icon").GetComponent<Image>();
            chargingSlotDrag = chargingItemSlot.GetComponent<LaboratoryInventoryItemDrag>();
            if (chargingSlotDrag == null)
                chargingSlotDrag = chargingItemSlot.gameObject.AddComponent<LaboratoryInventoryItemDrag>();
            chargingDropSlot = chargingItemSlot.GetComponent<LaboratoryItemDropSlot>();
            if (chargingDropSlot == null)
                chargingDropSlot = chargingItemSlot.gameObject.AddComponent<LaboratoryItemDropSlot>();
            chargingStatusLabel = chargingPanel.transform.Find("Status").GetComponent<TMP_Text>();
            chargingTakeButton = chargingPanel.transform.Find("TakeButton").GetComponent<Button>();
        }

        private void CacheAuthoredHierarchy()
        {
            inventoryPanel = transform.Find("InventoryScreen").gameObject;
            laboratoryPanel = transform.Find("LaboratoryScreen").gameObject;
            chargingPanel = laboratoryPanel;

            Transform quickAccessRoot = transform.Find("Slot_Invent_Equipment");
            quickAccessHud = quickAccessRoot != null ? quickAccessRoot.gameObject : null;

            dropButton = FindDescendant(inventoryPanel.transform, "DropButton")
                ?.GetComponent<Button>();

            laboratoryScanScreen = laboratoryPanel.transform.Find("ScanScreen")?.gameObject;
            laboratoryPowerScreen = laboratoryPanel.transform.Find("PowerScreen")?.gameObject;
            laboratoryTabButton = laboratoryPanel.transform.Find("ScanButton")
                ?.GetComponent<Button>();
            chargingTabButton = laboratoryPanel.transform.Find("PowerButton")
                ?.GetComponent<Button>();

            Transform sampleArea = laboratoryScanScreen != null
                ? laboratoryScanScreen.transform.Find("background_Screen_Storage_Slot")
                : null;
            Transform sampleSlot = sampleArea != null ? sampleArea.Find("Slot") : null;
            if (sampleSlot != null)
            {
                laboratorySlotIcon = EnsureRuntimeIcon(sampleSlot);
                laboratorySlotDrag = EnsureDrag(sampleSlot);
                laboratoryDropSlot = sampleSlot.GetComponent<LaboratoryItemDropSlot>();
                if (laboratoryDropSlot == null)
                    laboratoryDropSlot = sampleSlot.gameObject.AddComponent<LaboratoryItemDropSlot>();
            }
            laboratoryStatusLabel = FindDescendant(
                laboratoryScanScreen != null ? laboratoryScanScreen.transform : null,
                "Text_Description")?.GetComponent<TMP_Text>();
            scanButton = sampleArea != null
                ? sampleArea.Find("ScanButton")?.GetComponent<Button>()
                : null;
            scanButtonLabel = scanButton != null
                ? scanButton.GetComponentInChildren<TMP_Text>(true)
                : null;
            takeButton = sampleArea != null
                ? sampleArea.Find("DropButton")?.GetComponent<Button>()
                : null;

            Transform chargeArea = laboratoryPowerScreen != null
                ? laboratoryPowerScreen.transform.Find("background_Screen_Storage_Slot")
                : null;
            Transform chargeSlot = chargeArea != null ? chargeArea.Find("Slot_01") : null;
            if (chargeSlot != null)
            {
                chargingSlotIcon = EnsureRuntimeIcon(chargeSlot);
                chargingSlotDrag = EnsureDrag(chargeSlot);
                chargingDropSlot = chargeSlot.GetComponent<LaboratoryItemDropSlot>();
                if (chargingDropSlot == null)
                    chargingDropSlot = chargeSlot.gameObject.AddComponent<LaboratoryItemDropSlot>();
            }
            chargingStatusLabel = FindDescendant(
                laboratoryPowerScreen != null ? laboratoryPowerScreen.transform : null,
                "Text_Description")?.GetComponent<TMP_Text>();
            chargingTakeButton = chargeArea != null
                ? chargeArea.Find("DropButton")?.GetComponent<Button>()
                : null;
        }

        private void SetQuickAccessVisible(bool visible)
        {
            if (quickAccessHud != null)
                quickAccessHud.SetActive(visible);
        }

        private void BuildAuthoredSlotViews()
        {
            if (inventory == null)
                return;

            Transform inventoryRoot = inventoryPanel.transform.Find("ScanScreen");
            CacheAuthoredSlotViews(
                inventoryRoot?.Find("background_Screen_Storage_Slot_Invent"),
                backpackViews,
                InventorySlotGroup.Backpack);
            CacheAuthoredSlotViews(
                inventoryRoot?.Find("background_Screen_Storage_Slot_Invent_Anomaly"),
                anomalyViews,
                InventorySlotGroup.Anomaly);
            CacheAuthoredSlotViews(
                quickAccessHud != null ? quickAccessHud.transform : null,
                quickViews,
                InventorySlotGroup.QuickAccess);

            CacheLaboratoryInventoryViews(
                laboratoryScanScreen,
                laboratoryBackpackViews,
                laboratoryAnomalyViews,
                laboratoryQuickViews);
            CacheLaboratoryInventoryViews(
                laboratoryPowerScreen,
                chargingBackpackViews,
                chargingAnomalyViews,
                chargingQuickViews);
        }

        private void CacheLaboratoryInventoryViews(
            GameObject screen,
            List<InventorySlotView> backpacks,
            List<InventorySlotView> anomalies,
            List<InventorySlotView> equipment)
        {
            if (screen == null)
                return;

            CacheAuthoredSlotViews(
                screen.transform.Find("background_Screen_Storage_Slot_Invent"),
                backpacks,
                InventorySlotGroup.Backpack);
            CacheAuthoredSlotViews(
                screen.transform.Find("background_Screen_Storage_Slot_Invent_Anomaly"),
                anomalies,
                InventorySlotGroup.Anomaly);
            CacheAuthoredSlotViews(
                screen.transform.Find("background_Screen_Storage_Slot_Invent_Equipment"),
                equipment,
                InventorySlotGroup.QuickAccess);
        }

        private void CacheAuthoredSlotViews(
            Transform root,
            List<InventorySlotView> destination,
            InventorySlotGroup group)
        {
            destination.Clear();
            if (root == null)
                return;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform slot = root.GetChild(i);
                if (slot == null || slot.GetComponent<Image>() == null)
                    continue;

                if (slot.GetComponent<Button>() == null)
                    slot.gameObject.AddComponent<Button>();
                EnsureRuntimeIcon(slot);

                InventorySlotView view = slot.GetComponent<InventorySlotView>();
                if (view == null)
                    view = slot.gameObject.AddComponent<InventorySlotView>();

                int slotIndex = destination.Count;
                view.Initialize(
                    slotIndex,
                    group == InventorySlotGroup.QuickAccess,
                    rootCanvas);
                ConfigureDropTarget(view, group, slotIndex);
                destination.Add(view);
            }
        }

        private void BindAuthoredButtons()
        {
            BindSlotButtons(backpackViews, InventorySlotGroup.Backpack);
            BindSlotButtons(anomalyViews, InventorySlotGroup.Anomaly);
            BindSlotButtons(quickViews, InventorySlotGroup.QuickAccess);
            BindSlotButtons(laboratoryBackpackViews, InventorySlotGroup.Backpack);
            BindSlotButtons(laboratoryAnomalyViews, InventorySlotGroup.Anomaly);
            BindSlotButtons(laboratoryQuickViews, InventorySlotGroup.QuickAccess);
            BindSlotButtons(chargingBackpackViews, InventorySlotGroup.Backpack);
            BindSlotButtons(chargingAnomalyViews, InventorySlotGroup.Anomaly);
            BindSlotButtons(chargingQuickViews, InventorySlotGroup.QuickAccess);

            if (dropButton != null)
                dropButton.onClick.AddListener(DropSelected);

            Button exitButton = laboratoryPanel.transform.Find("ExitButton")
                ?.GetComponent<Button>();
            if (exitButton != null)
                exitButton.onClick.AddListener(CloseAll);

            if (laboratoryTabButton != null)
                laboratoryTabButton.onClick.AddListener(() => ShowLaboratoryMode(false));
            if (chargingTabButton != null)
                chargingTabButton.onClick.AddListener(() => ShowLaboratoryMode(true));
            if (scanButton != null)
                scanButton.onClick.AddListener(StartScan);
            if (takeButton != null)
                takeButton.onClick.AddListener(TakeSample);
            if (laboratoryDropSlot != null)
                laboratoryDropSlot.ItemDropped += LoadSample;
            if (chargingTakeButton != null)
                chargingTakeButton.onClick.AddListener(TakeChargedItem);
            if (chargingDropSlot != null)
                chargingDropSlot.ItemDropped += LoadChargeItem;
        }

        private void ShowLaboratoryMode(bool charging)
        {
            laboratoryOpen = !charging;
            chargingOpen = charging;
            if (laboratoryScanScreen != null)
                laboratoryScanScreen.SetActive(!charging);
            if (laboratoryPowerScreen != null)
                laboratoryPowerScreen.SetActive(charging);

            Transform upgrade = laboratoryPanel.transform.Find("UpgradeScreen");
            Transform map = laboratoryPanel.transform.Find("MapScreen");
            Transform library = laboratoryPanel.transform.Find("LibraryScreen");
            Transform storage = laboratoryPanel.transform.Find("StorageScreen");
            if (upgrade != null) upgrade.gameObject.SetActive(false);
            if (map != null) map.gameObject.SetActive(false);
            if (library != null) library.gameObject.SetActive(false);
            if (storage != null) storage.gameObject.SetActive(false);
            RefreshAll();
        }

        private static Transform FindDescendant(Transform root, string objectName)
        {
            if (root == null)
                return null;
            if (root.name == objectName)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform result = FindDescendant(root.GetChild(i), objectName);
                if (result != null)
                    return result;
            }
            return null;
        }

        private static Image EnsureRuntimeIcon(Transform slot)
        {
            Transform existing = slot.Find("Icon");
            Image icon = existing != null ? existing.GetComponent<Image>() : null;
            if (icon != null)
                return icon;

            GameObject iconObject = new GameObject(
                "Icon",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            iconObject.transform.SetParent(slot, false);
            RectTransform rect = iconObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.12f, 0.12f);
            rect.anchorMax = new Vector2(0.88f, 0.88f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image created = iconObject.GetComponent<Image>();
            created.preserveAspect = true;
            created.raycastTarget = false;
            created.enabled = false;
            return created;
        }

        private static LaboratoryInventoryItemDrag EnsureDrag(Transform slot)
        {
            LaboratoryInventoryItemDrag drag =
                slot.GetComponent<LaboratoryInventoryItemDrag>();
            if (drag == null)
                drag = slot.gameObject.AddComponent<LaboratoryInventoryItemDrag>();
            return drag;
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

            backpackScrollRect = backpackSlotRoot.GetComponentInParent<ScrollRect>();
            ConfigureBackpackScrollRect();
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

            CacheFixedSlotViews(
                anomaliesRoot,
                anomalyViews,
                PlayerInventory.AnomalyCapacity,
                false,
                InventorySlotGroup.Anomaly
            );
            CacheFixedSlotViews(
                transform.Find("QuickAccessHUD"),
                quickViews,
                PlayerInventory.QuickAccessCapacity,
                true,
                InventorySlotGroup.QuickAccess
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

            for (int i = 0; i < capacity; i++)
            {
                Transform spawnPoint = parent.Find($"Slot_{i + 1}");
                if (spawnPoint == null)
                {
                    Debug.LogError(
                        $"InventoryLabHUDController: backpack spawn point " +
                        $"'Slot_{i + 1}' is missing.",
                        this
                    );
                    continue;
                }

                spawnPoint.gameObject.SetActive(true);

                InventorySlotView view =
                    spawnPoint.GetComponentInChildren<InventorySlotView>(true);
                if (view == null || view.transform == spawnPoint)
                {
                    view = Instantiate(
                        inventoryConfig.SlotPrefab,
                        spawnPoint,
                        false
                    );
                }

                view.name = $"InventorySlotView_{i + 1}";

                RectTransform viewRect = view.transform as RectTransform;
                if (viewRect != null)
                    viewRect.anchoredPosition = Vector2.zero;

                view.gameObject.SetActive(true);
                view.transform.localRotation = Quaternion.identity;
                view.transform.localScale = Vector3.one;
                view.Initialize(i, false, rootCanvas);
                ConfigureDropTarget(view, InventorySlotGroup.Backpack, i);
                destination.Add(view);
            }

            for (int i = capacity; i < InventoryConfig.MaxBackpackCapacity; i++)
            {
                Transform extraSlot = parent.Find($"Slot_{i + 1}");
                if (extraSlot != null)
                    extraSlot.gameObject.SetActive(false);
            }
        }

        private Transform CacheFixedSlotViews(
            Transform parent,
            List<InventorySlotView> destination,
            int count,
            bool showQuickAccessNumbers,
            InventorySlotGroup group
        )
        {
            destination.Clear();

            if (parent == null)
            {
                Debug.LogError(
                    $"InventoryLabHUDController: slot root for '{group}' is missing.",
                    this
                );
                return null;
            }

            if (group == InventorySlotGroup.QuickAccess)
            {
                return CacheQuickAccessSlotViews(
                    parent,
                    destination,
                    count,
                    showQuickAccessNumbers,
                    group
                );
            }

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
                ConfigureDropTarget(view, group, i);
                destination.Add(view);
            }

            return parent;
        }

        private Transform CacheQuickAccessSlotViews(
            Transform parent,
            List<InventorySlotView> destination,
            int count,
            bool showQuickAccessNumbers,
            InventorySlotGroup group
        )
        {
            List<RectTransform> slots = new List<RectTransform>();
            for (int i = 0; i < parent.childCount; i++)
            {
                RectTransform slot = parent.GetChild(i) as RectTransform;
                if (slot != null)
                    slots.Add(slot);
            }

            slots.Sort((left, right) =>
                left.anchoredPosition.x.CompareTo(right.anchoredPosition.x));

            if (slots.Count < count)
            {
                Debug.LogError(
                    $"InventoryLabHUDController: '{parent.name}' contains " +
                    $"{slots.Count} quick-access slots, but {count} are required.",
                    this
                );
            }

            int cachedCount = Mathf.Min(count, slots.Count);
            for (int i = 0; i < cachedCount; i++)
            {
                RectTransform slot = slots[i];
                InventorySlotView view = slot.GetComponent<InventorySlotView>();
                if (view == null)
                    view = slot.gameObject.AddComponent<InventorySlotView>();

                view.Initialize(i, showQuickAccessNumbers, rootCanvas);
                ConfigureDropTarget(view, group, i);
                destination.Add(view);
            }

            return parent;
        }

        private void BindButtons()
        {
            BindSlotButtons(backpackViews, InventorySlotGroup.Backpack);
            BindSlotButtons(anomalyViews, InventorySlotGroup.Anomaly);
            BindSlotButtons(quickViews, InventorySlotGroup.QuickAccess);

            dropButton.onClick.AddListener(DropSelected);
            Button laboratoryCloseButton = FindButtonInChildren(
                laboratoryPanel.transform,
                "CloseButton"
            );
            if (laboratoryCloseButton != null)
                laboratoryCloseButton.onClick.AddListener(CloseAll);
            scanButton.onClick.AddListener(StartScan);
            takeButton.onClick.AddListener(TakeSample);
            laboratoryDropSlot.ItemDropped += LoadSample;

            Button chargingCloseButton = FindButtonInChildren(
                chargingPanel.transform,
                "CloseButton"
            );
            if (chargingCloseButton != null)
                chargingCloseButton.onClick.AddListener(CloseAll);
            chargingTakeButton.onClick.AddListener(TakeChargedItem);
            chargingDropSlot.ItemDropped += LoadChargeItem;
        }

        private void BindSlotButtons(
            List<InventorySlotView> views,
            InventorySlotGroup group
        )
        {
            for (int i = 0; i < views.Count; i++)
            {
                int index = i;
                views[i].Button.onClick.AddListener(
                    () => HandleSlotClicked(group, index)
                );
            }
        }

        private void HandleSlotClicked(InventorySlotGroup group, int index)
        {
            if (group == InventorySlotGroup.QuickAccess)
            {
                SelectQuickItem(index);
                return;
            }

            SelectItem(group, index);
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
            {
                inventory.InventoryChanged -= RefreshAll;
            }

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

        private void LoadSample(LaboratoryInventoryItemDrag drag)
        {
            if (!laboratoryOpen || drag == null || drag.Item == null)
                return;

            ResearchController controller = ResearchController.Instance;
            if (controller != null && controller.LoadItem(
                drag.Item,
                inventory,
                drag.SourceGroup,
                drag.SourceIndex
            ))
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

        private void TakeChargedItem()
        {
            ItemChargingController.Instance?.RetrieveLoadedItem();
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
            RefreshSlots(
                laboratoryBackpackViews,
                inventory.BackpackSlots,
                InventorySlotGroup.Backpack);
            RefreshSlots(
                laboratoryAnomalyViews,
                inventory.AnomalySlots,
                InventorySlotGroup.Anomaly);
            RefreshSlots(
                laboratoryQuickViews,
                inventory.QuickAccessSlots,
                InventorySlotGroup.QuickAccess);
            RefreshSlots(
                chargingBackpackViews,
                inventory.BackpackSlots,
                InventorySlotGroup.Backpack);
            RefreshSlots(
                chargingAnomalyViews,
                inventory.AnomalySlots,
                InventorySlotGroup.Anomaly);
            RefreshSlots(
                chargingQuickViews,
                inventory.QuickAccessSlots,
                InventorySlotGroup.QuickAccess);
            RefreshSelection();

            RefreshDrags(backpackViews, inventory.BackpackSlots, InventorySlotGroup.Backpack);
            RefreshDrags(anomalyViews, inventory.AnomalySlots, InventorySlotGroup.Anomaly);
            RefreshDrags(quickViews, inventory.QuickAccessSlots, InventorySlotGroup.QuickAccess);
            RefreshDrags(
                laboratoryBackpackViews,
                inventory.BackpackSlots,
                InventorySlotGroup.Backpack);
            RefreshDrags(
                laboratoryAnomalyViews,
                inventory.AnomalySlots,
                InventorySlotGroup.Anomaly);
            RefreshDrags(
                laboratoryQuickViews,
                inventory.QuickAccessSlots,
                InventorySlotGroup.QuickAccess);
            RefreshDrags(
                chargingBackpackViews,
                inventory.BackpackSlots,
                InventorySlotGroup.Backpack);
            RefreshDrags(
                chargingAnomalyViews,
                inventory.AnomalySlots,
                InventorySlotGroup.Anomaly);
            RefreshDrags(
                chargingQuickViews,
                inventory.QuickAccessSlots,
                InventorySlotGroup.QuickAccess);

            if (laboratoryOpen)
                RefreshLaboratory();
            if (chargingOpen)
                RefreshChargingTable();
            RefreshBackpackScrollState();
        }

        private void RefreshLaboratory()
        {
            ResearchController controller = ResearchController.Instance;
            if (controller == null)
                return;

            bool analyzing = controller.State == ResearchController.ResearchState.Analyzing;
            bool hasItem = controller.LoadedItem != null;
            bool analyzed = hasItem && controller.IsAnalyzed(controller.LoadedItem);
            bool researchable = hasItem && controller.IsResearchable(controller.LoadedItem);

            if (laboratorySlotIcon != null)
            {
                laboratorySlotIcon.sprite = hasItem
                    ? controller.LoadedItem.Icon
                    : null;
                laboratorySlotIcon.color = hasItem && controller.LoadedItem.Icon == null
                    ? MissingIconColor
                    : Color.white;
                laboratorySlotIcon.enabled = hasItem;
            }
            if (laboratoryStatusLabel != null)
                laboratoryStatusLabel.text = controller.StatusMessage;
            if (scanButtonLabel != null)
            {
                scanButtonLabel.text = analyzing
                    ? $"SCANNING {Mathf.RoundToInt(controller.Progress * 100f)}%"
                    : !hasItem ? "START SCAN"
                    : !researchable ? "KNOWN ITEM"
                    : analyzed ? "ANALYZED"
                    : "START SCAN";
            }
            if (scanButton != null)
                scanButton.interactable = controller.CanStartAnalysis;
            if (takeButton != null)
                takeButton.interactable = hasItem && !analyzing;
            if (laboratorySlotDrag != null)
            {
                laboratorySlotDrag.Initialize(
                    hasItem ? controller.LoadedItem : null,
                    rootCanvas,
                    InventorySlotGroup.Backpack,
                    -1,
                    true
                );
            }
        }

        private void RefreshChargingTable()
        {
            ItemChargingController charger = ItemChargingController.Instance;
            ItemInstance instance = charger?.LoadedItem;
            ItemData item = instance?.ItemData;
            bool hasItem = item != null;

            if (chargingSlotIcon != null)
            {
                chargingSlotIcon.sprite = hasItem ? item.Icon : null;
                chargingSlotIcon.color = hasItem && item.Icon == null
                    ? MissingIconColor
                    : Color.white;
                chargingSlotIcon.enabled = hasItem;
            }
            if (chargingStatusLabel != null)
            {
                chargingStatusLabel.text = !hasItem
                    ? charger?.StatusMessage ?? "Charging table unavailable."
                    : $"{charger.StatusMessage}\nCHARGE {Mathf.RoundToInt(instance.Charge01 * 100f)}%";
            }
            if (chargingTakeButton != null)
                chargingTakeButton.interactable = hasItem;
            if (chargingSlotDrag != null)
            {
                chargingSlotDrag.Initialize(
                    item,
                    rootCanvas,
                    InventorySlotGroup.Backpack,
                    -1,
                    false,
                    true
                );
            }
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
                RefreshSlotKeyLabel(views[i], item, group, i);
            }
        }

        private void LoadChargeItem(LaboratoryInventoryItemDrag drag)
        {
            if (!chargingOpen || drag == null || drag.Item == null)
                return;

            ItemChargingController charger = ItemChargingController.Instance;
            if (charger != null && charger.LoadItem(
                    inventory,
                    drag.SourceGroup,
                    drag.SourceIndex))
            {
                ClearSelection();
                RefreshAll();
            }
        }

        private static void RefreshSlotKeyLabel(
            InventorySlotView view,
            ItemData item,
            InventorySlotGroup group,
            int index
        )
        {
            bool showLabel =
                group == InventorySlotGroup.QuickAccess &&
                PlayerInventory.IsActiveQuickAccessSlot(index) &&
                item != null &&
                item.QuickAccessAction != QuickAccessAction.None;

            view.SetKeyLabel(showLabel ? FormatUseKey(item.UseKey) : string.Empty, showLabel);
        }

        private static string FormatUseKey(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.Mouse0:
                    return "LCM";
                case KeyCode.Mouse1:
                    return "RCM";
                case KeyCode.Mouse2:
                    return "MCM";
                default:
                    return key.ToString();
            }
        }

        private void RefreshDrags(
            List<InventorySlotView> views,
            System.Collections.Generic.IReadOnlyList<ItemData> slots,
            InventorySlotGroup group
        )
        {
            for (int i = 0; i < views.Count; i++)
            {
                LaboratoryInventoryItemDrag drag = views[i].LaboratoryDrag;
                if (drag == null)
                    continue;

                ItemData item = i < slots.Count ? slots[i] : null;
                drag.Initialize(item, rootCanvas, group, i);
            }
        }

        private void ConfigureDropTarget(
            InventorySlotView view,
            InventorySlotGroup group,
            int index
        )
        {
            InventorySlotDropTarget target = view.GetComponent<InventorySlotDropTarget>();
            if (target == null)
                target = view.gameObject.AddComponent<InventorySlotDropTarget>();

            target.Initialize(group, index, HandleSlotDrop);
        }

        private void ConfigureBackpackScrollRect()
        {
            if (backpackScrollRect == null)
                return;

            backpackScrollRect.horizontal = false;
            backpackScrollRect.movementType = ScrollRect.MovementType.Clamped;
        }

        private void RefreshBackpackScrollState()
        {
            if (backpackScrollRect == null || backpackSlotRoot == null)
                return;

            Canvas.ForceUpdateCanvases();

            RectTransform content = backpackSlotRoot as RectTransform;
            RectTransform viewport = backpackScrollRect.viewport;
            if (content == null || viewport == null)
                return;

            LayoutRebuilder.ForceRebuildLayoutImmediate(content);

            bool shouldScroll = content.rect.height > viewport.rect.height + 1f;
            backpackScrollRect.vertical = shouldScroll;
            backpackScrollRect.verticalNormalizedPosition = 1f;

            if (!shouldScroll)
            {
                content.anchoredPosition = new Vector2(content.anchoredPosition.x, 0f);
                if (backpackScrollRect.verticalScrollbar != null)
                    backpackScrollRect.verticalScrollbar.gameObject.SetActive(false);
            }
            else if (backpackScrollRect.verticalScrollbar != null)
            {
                backpackScrollRect.verticalScrollbar.gameObject.SetActive(true);
            }
        }

        private void HandleSlotDrop(
            InventorySlotGroup destinationGroup,
            int destinationIndex,
            LaboratoryInventoryItemDrag drag
        )
        {
            if (inventory == null || drag == null)
                return;

            if (drag.IsStationStorageSource)
            {
                StationStorageController storage = StationStorageController.Instance;
                if (storage != null && storage.MoveToInventory(
                        drag.SourceGroup,
                        drag.SourceIndex,
                        inventory,
                        destinationGroup,
                        destinationIndex))
                {
                    SetSelection(destinationGroup, destinationIndex);
                }
                return;
            }

            if (drag.IsChargingSource)
            {
                ItemChargingController charger = ItemChargingController.Instance;
                if (charger != null && charger.MoveLoadedItemToInventory(
                        inventory,
                        destinationGroup,
                        destinationIndex))
                {
                    SetSelection(destinationGroup, destinationIndex);
                }
                return;
            }

            if (drag.IsLaboratorySource)
            {
                ResearchController controller = ResearchController.Instance;
                if (controller != null &&
                    controller.MoveLoadedItemToInventory(
                        inventory,
                        destinationGroup,
                        destinationIndex
                    ))
                {
                    SetSelection(destinationGroup, destinationIndex);
                }
                return;
            }

            if (drag.SourceIndex < 0)
                return;

            if (inventory.TryMoveItem(
                drag.SourceGroup,
                drag.SourceIndex,
                destinationGroup,
                destinationIndex
            ))
            {
                SetSelection(destinationGroup, destinationIndex);
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
            if (selectedItemName != null)
            {
                selectedItemName.text = hasSelection
                    ? selectedItem.DisplayName
                    : "SELECT AN ITEM";
            }
            if (selectedItemDescription != null)
            {
                selectedItemDescription.text = hasSelection
                    ? selectedItem.Description
                    : string.Empty;
            }
            if (dropButton != null)
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
