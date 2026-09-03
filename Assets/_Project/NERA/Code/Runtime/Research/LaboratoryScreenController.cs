using System;
using System.Collections.Generic;
using NERA.Energy;
using NERA.Inventory;
using NERA.Items;
using NERA.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NERA.Research
{
    public sealed class LaboratoryScreenController : MonoBehaviour
    {
        private enum LaboratoryMode
        {
            Scan,
            Upgrade
        }

        private sealed class InventoryView
        {
            public InventorySlotGroup Group;
            public int Index;
            public Button Button;
            public Image Icon;
            public LaboratoryInventoryItemDrag Drag;
            public LaboratoryItemDropSlot Drop;
            public ItemData Item;
        }

        private readonly List<InventoryView> inventoryViews =
            new List<InventoryView>();
        private readonly List<InventorySlotView> upgradeViews =
            new List<InventorySlotView>();

        private InventoryLabHUDController owner;
        private PlayerInventory inventory;
        [SerializeField] private Canvas rootCanvas;
        [SerializeField] private GameObject inventoryAndInfoScreen;
        [SerializeField] private GameObject scanScreen;
        [SerializeField] private GameObject upgradeScreen;
        private InventorySlotView scanView;
        [SerializeField] private Button scanButton;
        [SerializeField] private Button scanDropButton;
        [SerializeField] private TMP_Text scanProgressText;
        [SerializeField] private Button upgradeButton;
        [SerializeField] private Button upgradeDropButton;
        [SerializeField] private TMP_Text upgradeProgressText;
        [SerializeField] private TMP_Text infoName;
        [SerializeField] private TMP_Text infoDescription;
        [SerializeField] private Image infoImage;
        private LaboratoryMode activeMode = LaboratoryMode.Scan;
        private int navigationUnlockFrame;
        private ResearchController subscribedResearch;
        private LaboratoryWorkstationController subscribedWorkstation;
        private bool dataEventsBound;
        private bool initialized;

        public int ActiveModeIndex => (int)activeMode;

        public void Initialize(
            InventoryLabHUDController hudOwner,
            PlayerInventory playerInventory)
        {
            owner = hudOwner;
            BindInventory(playerInventory);
            if (initialized)
            {
                RefreshAll();
                return;
            }

            initialized = true;
            NERALocalization.LocaleChanged += RefreshAll;
            rootCanvas ??= GetComponentInParent<Canvas>();
            CacheHierarchy();
            BuildInventoryViews();
            BuildOperationViews();
            BindButtons();
            ShowMode(LaboratoryMode.Scan);
            RefreshAll();
        }

        public void Open(PlayerInventory playerInventory)
        {
            BindInventory(playerInventory);
            BindDataEvents();
            navigationUnlockFrame = Time.frameCount + 1;
            ShowSharedInventory();
            ShowMode(activeMode);
            RefreshAll();
        }

        public void Close()
        {
            UnbindDataEvents();
            ClearInfo();
        }

        private void Update()
        {
            if (!gameObject.activeInHierarchy)
                return;

            if (Time.frameCount > navigationUnlockFrame)
            {
                if (Input.GetKeyDown(KeyCode.Q))
                    ShowPreviousMode();
                else if (Input.GetKeyDown(KeyCode.E))
                    ShowNextMode();
            }
        }

        private void CacheHierarchy()
        {
            inventoryAndInfoScreen ??= Find(
                transform,
                "Inventory_and_info_Screen")?.gameObject;
            scanScreen ??= Find(transform, "ScanScreen")?.gameObject;
            upgradeScreen ??= Find(transform, "UpgradeScreen")?.gameObject;

            Transform infoRoot = Find(
                inventoryAndInfoScreen != null
                    ? inventoryAndInfoScreen.transform
                    : null,
                "background_Screen_Storage_Info");
            infoName ??= Find(infoRoot, "Text_Name")?.GetComponent<TMP_Text>();
            infoDescription ??= Find(
                infoRoot,
                "Text_Description")?.GetComponent<TMP_Text>();
            infoImage ??= Find(infoRoot, "Image_info")?.GetComponent<Image>();

            scanButton ??= Find(
                scanScreen != null ? scanScreen.transform : null,
                "ScanButton")?.GetComponent<Button>();
            scanDropButton ??= Find(
                scanScreen != null ? scanScreen.transform : null,
                "DropButton")?.GetComponent<Button>();
            scanProgressText ??= Find(
                scanScreen != null ? scanScreen.transform : null,
                "Text_progress")?.GetComponent<TMP_Text>();
            upgradeButton ??= Find(
                upgradeScreen != null ? upgradeScreen.transform : null,
                "UpgradeButton")?.GetComponent<Button>();
            upgradeDropButton ??= Find(
                upgradeScreen != null ? upgradeScreen.transform : null,
                "DropButton")?.GetComponent<Button>();
            upgradeProgressText ??= Find(
                upgradeScreen != null ? upgradeScreen.transform : null,
                "Text_progress")?.GetComponent<TMP_Text>();
        }

        private void BuildInventoryViews()
        {
            InventoryConfig config = InventoryConfig.Resolve(inventory?.Config);
            if (inventoryAndInfoScreen == null || config?.SlotPrefab == null)
                return;

            BuildInventoryGroup(
                Find(
                    inventoryAndInfoScreen.transform,
                    "background_Screen_Storage_Slot_Invent"),
                InventorySlotGroup.Backpack,
                config.SlotPrefab);
            BuildInventoryGroup(
                Find(
                    inventoryAndInfoScreen.transform,
                    "background_Screen_Storage_Slot_Invent_Anomaly"),
                InventorySlotGroup.Anomaly,
                config.SlotPrefab);
        }

        private void BuildInventoryGroup(
            Transform root,
            InventorySlotGroup group,
            GameObject slotPrefab)
        {
            List<Transform> spawnPoints = GetSpawnPoints(root, "Slot_");
            for (int index = 0; index < spawnPoints.Count; index++)
            {
                InventorySlotView view =
                    InventorySlotSpawnUtility.GetOrCreate(
                        spawnPoints[index],
                        slotPrefab);
                if (view == null)
                    continue;

                view.Initialize(index, false, rootCanvas);
                PrepareInteractiveSlot(view);
                InventoryView binding = new InventoryView
                {
                    Group = group,
                    Index = index,
                    Button = view.Button ?? EnsureButton(view.transform),
                    Icon = view.Icon ?? EnsureSlotIcon(view.transform),
                    Drag = view.LaboratoryDrag ??
                        view.gameObject.AddComponent<LaboratoryInventoryItemDrag>(),
                    Drop = view.GetComponent<LaboratoryItemDropSlot>() ??
                        view.gameObject.AddComponent<LaboratoryItemDropSlot>()
                };
                binding.Button?.onClick.AddListener(
                    () => SelectInventorySlot(binding));
                binding.Drag.InteractionStarted += _ =>
                    SelectInventorySlot(binding);
                binding.Drop.ItemDropped += drag =>
                    HandleInventoryDrop(
                        binding.Group,
                        binding.Index,
                        drag);
                inventoryViews.Add(binding);
            }
        }

        private void BuildOperationViews()
        {
            InventoryConfig config = InventoryConfig.Resolve(inventory?.Config);
            if (config?.SlotPrefab == null)
                return;

            Transform scanPoint = scanScreen != null
                ? scanScreen.transform.Find("background_Screen_Storage_Slot/Slot")
                : null;
            scanView = CreateOperationView(
                scanPoint,
                config.SlotPrefab,
                0,
                HandleScanDrop);

            Transform upgradeRoot = Find(
                upgradeScreen != null ? upgradeScreen.transform : null,
                "background_Screen_Storage_Slot");
            List<Transform> upgradePoints =
                GetSpawnPoints(upgradeRoot, "Slot_");
            for (int index = 0; index < upgradePoints.Count; index++)
            {
                int capturedIndex = index;
                InventorySlotView view = CreateOperationView(
                    upgradePoints[index],
                    config.SlotPrefab,
                    index,
                    drag => HandleUpgradeDrop(capturedIndex, drag));
                if (view != null)
                    upgradeViews.Add(view);
            }
        }

        private InventorySlotView CreateOperationView(
            Transform spawnPoint,
            GameObject slotPrefab,
            int index,
            Action<LaboratoryInventoryItemDrag> onDrop)
        {
            InventorySlotView view =
                InventorySlotSpawnUtility.GetOrCreate(
                    spawnPoint,
                    slotPrefab);
            if (view == null)
                return null;

            view.Initialize(index, false, rootCanvas);
            PrepareInteractiveSlot(view);
            LaboratoryItemDropSlot drop =
                view.GetComponent<LaboratoryItemDropSlot>() ??
                view.gameObject.AddComponent<LaboratoryItemDropSlot>();
            drop.ItemDropped += onDrop;
            view.Button.onClick.AddListener(
                () => ShowInfo(
                    view.LaboratoryDrag != null
                        ? view.LaboratoryDrag.Item
                        : null));
            view.LaboratoryDrag.InteractionStarted += drag =>
                ShowInfo(drag.Item);
            return view;
        }

        private void BindButtons()
        {
            Find(transform, "ScanMapButton")?.GetComponent<Button>()
                ?.onClick.AddListener(
                    () => ShowMode(LaboratoryMode.Scan));
            Find(transform, "UpgradeMapButton")?.GetComponent<Button>()
                ?.onClick.AddListener(
                    () => ShowMode(LaboratoryMode.Upgrade));
            Find(transform, "NextButton")?.GetComponent<Button>()
                ?.onClick.AddListener(ShowNextMode);
            Find(transform, "BackButton")?.GetComponent<Button>()
                ?.onClick.AddListener(ShowPreviousMode);
            Find(transform, "ExitButton")?.GetComponent<Button>()
                ?.onClick.AddListener(() => owner?.CloseAll());

            scanButton?.onClick.AddListener(StartScan);
            scanDropButton?.onClick.AddListener(ReturnScanItem);
            upgradeDropButton?.onClick.AddListener(ReturnUpgradeItems);
            upgradeButton?.onClick.AddListener(SynthesizeUpgrade);
        }

        private void ShowNextMode()
        {
            int next = ((int)activeMode + 1) % 2;
            ShowMode((LaboratoryMode)next);
        }

        private void ShowPreviousMode()
        {
            int previous = ((int)activeMode + 1) % 2;
            ShowMode((LaboratoryMode)previous);
        }

        private void ShowMode(LaboratoryMode mode)
        {
            activeMode = mode;
            if (scanScreen != null)
                scanScreen.SetActive(mode == LaboratoryMode.Scan);
            if (upgradeScreen != null)
                upgradeScreen.SetActive(mode == LaboratoryMode.Upgrade);

            SetOptionalLegacyScreenActive("MapScreen", false);
            SetOptionalLegacyScreenActive("LibraryScreen", false);
            SetOptionalLegacyScreenActive("StorageScreen", false);
            ShowSharedInventory();
            RefreshAll();
        }

        private void ShowSharedInventory()
        {
            if (inventoryAndInfoScreen == null)
                return;

            inventoryAndInfoScreen.SetActive(true);
            inventoryAndInfoScreen.transform.SetAsLastSibling();
        }

        private void SetOptionalLegacyScreenActive(
            string objectName,
            bool active)
        {
            Transform screen = transform.Find(objectName);
            if (screen != null)
                screen.gameObject.SetActive(active);
        }

        private void HandleInventoryDrop(
            InventorySlotGroup destinationGroup,
            int destinationIndex,
            LaboratoryInventoryItemDrag drag)
        {
            if (inventory == null || drag?.Item == null)
                return;

            bool moved;
            if (drag.IsLaboratorySource)
            {
                moved = ResearchController.Instance != null &&
                    ResearchController.Instance.MoveLoadedItemToInventory(
                        inventory,
                        destinationGroup,
                        destinationIndex);
            }
            else if (drag.IsUpgradeSource)
            {
                moved = LaboratoryWorkstationController.Instance != null &&
                    LaboratoryWorkstationController.Instance
                        .MoveUpgradeItemToInventory(
                            drag.SourceIndex,
                            inventory,
                            destinationGroup,
                            destinationIndex);
            }
            else
            {
                moved = drag.SourceIndex >= 0 &&
                    inventory.TryMoveItem(
                        drag.SourceGroup,
                        drag.SourceIndex,
                        destinationGroup,
                        destinationIndex);
            }

            if (moved)
                RefreshAll();
        }

        private void HandleScanDrop(LaboratoryInventoryItemDrag drag)
        {
            if (!IsPlayerInventorySource(drag))
                return;

            ResearchController research = ResearchController.Instance;
            if (research != null &&
                research.LoadItem(
                    drag.Item,
                    inventory,
                    drag.SourceGroup,
                    drag.SourceIndex))
            {
                RefreshAll();
            }
        }

        private void HandleUpgradeDrop(
            int slotIndex,
            LaboratoryInventoryItemDrag drag)
        {
            if (!IsPlayerInventorySource(drag))
                return;

            LaboratoryWorkstationController workstation =
                LaboratoryWorkstationController.Instance;
            if (workstation != null &&
                workstation.LoadUpgradeItem(
                    slotIndex,
                    inventory,
                    drag.SourceGroup,
                    drag.SourceIndex))
            {
                RefreshAll();
            }
        }

        private static bool IsPlayerInventorySource(
            LaboratoryInventoryItemDrag drag)
        {
            return drag?.Item != null &&
                drag.SourceIndex >= 0 &&
                !drag.IsLaboratorySource &&
                !drag.IsUpgradeSource &&
                !drag.IsStationStorageSource;
        }

        private void StartScan()
        {
            ResearchController.Instance?.StartAnalysis();
            RefreshAll();
        }

        private void ReturnScanItem()
        {
            ResearchController.Instance?.RetrieveLoadedItem();
            RefreshAll();
        }

        private void ReturnUpgradeItems()
        {
            LaboratoryWorkstationController.Instance
                ?.RetrieveAllUpgradeItems(inventory);
            RefreshAll();
        }

        private void SynthesizeUpgrade()
        {
            LaboratoryWorkstationController workstation =
                LaboratoryWorkstationController.Instance;
            if (workstation?.TrySynthesize() == true)
            {
                ShowInfo(workstation.GetUpgradeItem(0)?.ItemData);
            }

            RefreshAll();
        }

        private void RefreshAll()
        {
            if (inventory == null)
                return;

            RefreshInventoryViews();
            RefreshScanView();
            RefreshUpgradeViews();
        }

        private void RefreshActiveOperationView()
        {
            switch (activeMode)
            {
                case LaboratoryMode.Scan:
                    RefreshScanView();
                    break;
                case LaboratoryMode.Upgrade:
                    RefreshUpgradeViews();
                    break;
            }
        }

        private void RefreshInventoryViews()
        {
            foreach (InventoryView binding in inventoryViews)
            {
                binding.Item = inventory.GetItem(
                    binding.Group,
                    binding.Index);
                SetItemIcon(binding.Icon, binding.Item);
                if (binding.Drag != null)
                {
                    binding.Drag.Initialize(
                        binding.Item,
                        rootCanvas,
                        binding.Group,
                        binding.Index);
                }
                if (binding.Button != null)
                    binding.Button.interactable = true;
            }
        }

        private void SelectInventorySlot(InventoryView view)
        {
            ShowInfo(view?.Item);
        }

        private void RefreshScanView()
        {
            ResearchController research = ResearchController.Instance;
            ItemData item = research?.LoadedItem;
            SetSlotItem(
                scanView,
                item,
                item != null
                    ? PlayerInventory.GetSlotGroup(item.ItemType)
                    : InventorySlotGroup.Backpack,
                0,
                isScanSource: true);

            bool analyzing =
                research?.State == ResearchController.ResearchState.Analyzing;
            if (scanView?.LaboratoryDrag != null)
                scanView.LaboratoryDrag.enabled = !analyzing;
            if (scanButton != null)
                scanButton.interactable = research?.CanStartAnalysis == true;
            if (scanDropButton != null)
                scanDropButton.interactable = item != null && !analyzing;
            if (scanProgressText != null)
            {
                scanProgressText.gameObject.SetActive(analyzing);
                scanProgressText.text = analyzing
                    ? NERALocalization.Get(
                        NERALocalization.InventoryLaboratoryTable,
                        "scan.progress_mixed_case",
                        "Progress - {0}%",
                        Mathf.RoundToInt(
                            Mathf.Clamp01(research.Progress) * 100f))
                    : string.Empty;
            }
        }

        private void RefreshUpgradeViews()
        {
            LaboratoryWorkstationController workstation =
                LaboratoryWorkstationController.Instance;
            bool processing = workstation?.IsUpgradeProcessing == true;
            bool hasAny = false;
            for (int index = 0; index < upgradeViews.Count; index++)
            {
                ItemInstance instance =
                    workstation?.GetUpgradeItem(index);
                ItemData item = instance?.ItemData;
                hasAny |= item != null;
                SetSlotItem(
                    upgradeViews[index],
                    item,
                    item != null
                        ? PlayerInventory.GetSlotGroup(item.ItemType)
                        : InventorySlotGroup.Backpack,
                    index,
                    isUpgradeSource: true);
                if (upgradeViews[index]?.LaboratoryDrag != null)
                    upgradeViews[index].LaboratoryDrag.enabled = !processing;
            }

            if (upgradeDropButton != null)
            {
                upgradeDropButton.interactable =
                    hasAny &&
                    !processing;
            }
            if (upgradeButton != null)
            {
                upgradeButton.interactable =
                    workstation?.CanSynthesize(out _) == true;
            }
            if (upgradeProgressText != null)
            {
                upgradeProgressText.gameObject.SetActive(processing);
                upgradeProgressText.text = processing
                    ? NERALocalization.Get(
                        NERALocalization.InventoryLaboratoryTable,
                        "scan.progress_mixed_case",
                        "Progress - {0}%",
                        Mathf.RoundToInt(
                            workstation.SynthesisProgress * 100f))
                    : string.Empty;
            }
        }

        private void SetSlotItem(
            InventorySlotView view,
            ItemData item,
            InventorySlotGroup group,
            int index,
            bool isScanSource = false,
            bool isUpgradeSource = false)
        {
            if (view == null)
                return;

            if (view.Icon != null)
            {
                view.Icon.sprite = item?.Icon;
                view.Icon.color = item != null && item.Icon == null
                    ? new Color(0.18f, 0.28f, 0.31f, 1f)
                    : Color.white;
                view.Icon.enabled = item != null;
            }
            view.SetKeyLabel(string.Empty, false);
            LaboratoryInventoryItemDrag drag = view.LaboratoryDrag;
            if (drag != null)
            {
                drag.Initialize(
                    item,
                    rootCanvas,
                    group,
                    index,
                    isScanSource,
                    false,
                    isUpgradeSource);
            }
        }

        private void ShowInfo(ItemData item)
        {
            if (infoName != null)
                infoName.text = item?.DisplayName ?? string.Empty;
            if (infoDescription != null)
                infoDescription.text = item?.Description ?? string.Empty;
            if (infoImage != null)
            {
                infoImage.sprite = item?.Icon;
                infoImage.enabled = item?.Icon != null;
            }
        }

        private void ClearInfo()
        {
            ShowInfo(null);
        }

        private static Button EnsureButton(Transform slot)
        {
            if (slot == null)
                return null;

            Button button = slot.GetComponent<Button>() ??
                slot.gameObject.AddComponent<Button>();
            if (button.targetGraphic == null)
                button.targetGraphic = slot.GetComponent<Graphic>();
            return button;
        }

        private static Image EnsureSlotIcon(Transform slot)
        {
            if (slot == null)
                return null;

            Transform authored = slot.Find("Icon");
            if (authored != null)
                return authored.GetComponent<Image>();

            GameObject iconObject = new GameObject(
                "RuntimeIcon",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            iconObject.transform.SetParent(slot, false);
            RectTransform rect = (RectTransform)iconObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(6f, 6f);
            rect.offsetMax = new Vector2(-6f, -6f);
            Image image = iconObject.GetComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.enabled = false;
            return image;
        }

        private static void SetItemIcon(Image image, ItemData item)
        {
            if (image == null)
                return;

            image.sprite = item?.Icon;
            image.color = item != null && item.Icon == null
                ? new Color(0.18f, 0.28f, 0.31f, 1f)
                : Color.white;
            image.enabled = item != null;
        }

        private static void PrepareInteractiveSlot(InventorySlotView view)
        {
            if (view == null)
                return;

            Button button = view.Button ?? EnsureButton(view.transform);
            if (button != null)
                button.interactable = true;

            Graphic background =
                view.Background ?? view.GetComponent<Graphic>();
            if (background != null)
            {
                background.enabled = true;
                background.raycastTarget = true;
            }

            CanvasGroup group =
                view.GetComponent<CanvasGroup>() ??
                view.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }

        private void BindInventory(PlayerInventory playerInventory)
        {
            if (inventory == playerInventory)
                return;

            if (dataEventsBound && inventory != null)
                inventory.InventoryChanged -= HandleInventoryChanged;
            inventory = playerInventory;
            if (dataEventsBound && inventory != null)
                inventory.InventoryChanged += HandleInventoryChanged;
        }

        private void BindDataEvents()
        {
            if (dataEventsBound)
                return;

            dataEventsBound = true;
            if (inventory != null)
                inventory.InventoryChanged += HandleInventoryChanged;

            subscribedResearch = ResearchController.Instance;
            if (subscribedResearch != null)
            {
                subscribedResearch.StateChanged += HandleResearchStateChanged;
                subscribedResearch.ProgressChanged += HandleResearchProgressChanged;
            }

            subscribedWorkstation = LaboratoryWorkstationController.Instance;
            if (subscribedWorkstation != null)
            {
                subscribedWorkstation.StateChanged +=
                    HandleWorkstationChanged;
                subscribedWorkstation.ItemsChanged +=
                    HandleWorkstationChanged;
            }
        }

        private void UnbindDataEvents()
        {
            if (!dataEventsBound)
                return;

            dataEventsBound = false;
            if (inventory != null)
                inventory.InventoryChanged -= HandleInventoryChanged;

            if (subscribedResearch != null)
            {
                subscribedResearch.StateChanged -= HandleResearchStateChanged;
                subscribedResearch.ProgressChanged -=
                    HandleResearchProgressChanged;
                subscribedResearch = null;
            }

            if (subscribedWorkstation != null)
            {
                subscribedWorkstation.StateChanged -=
                    HandleWorkstationChanged;
                subscribedWorkstation.ItemsChanged -=
                    HandleWorkstationChanged;
                subscribedWorkstation = null;
            }
        }

        private void HandleInventoryChanged()
        {
            if (gameObject.activeInHierarchy)
                RefreshAll();
        }

        private void HandleResearchStateChanged(
            ResearchController.ResearchState _)
        {
            if (gameObject.activeInHierarchy)
                RefreshActiveOperationView();
        }

        private void HandleResearchProgressChanged(float _)
        {
            if (gameObject.activeInHierarchy)
                RefreshActiveOperationView();
        }

        private void HandleWorkstationChanged()
        {
            if (gameObject.activeInHierarchy)
                RefreshActiveOperationView();
        }

        private static List<Transform> GetSpawnPoints(
            Transform root,
            string prefix)
        {
            List<Transform> result = new List<Transform>();
            if (root == null)
                return result;

            for (int index = 0; index < root.childCount; index++)
            {
                Transform child = root.GetChild(index);
                if (child.name.StartsWith(prefix, StringComparison.Ordinal))
                    result.Add(child);
            }
            result.Sort((left, right) =>
                ParseSlotNumber(left.name).CompareTo(
                    ParseSlotNumber(right.name)));
            return result;
        }

        private static int ParseSlotNumber(string objectName)
        {
            int separator = objectName.LastIndexOf('_');
            return separator >= 0 &&
                int.TryParse(
                    objectName.Substring(separator + 1),
                    out int number)
                ? number
                : int.MaxValue;
        }

        private static Transform Find(
            Transform root,
            string objectName)
        {
            if (root == null)
                return null;
            if (root.name == objectName)
                return root;

            for (int index = 0; index < root.childCount; index++)
            {
                Transform found = Find(
                    root.GetChild(index),
                    objectName);
                if (found != null)
                    return found;
            }

            return null;
        }

        private void OnDestroy()
        {
            NERALocalization.LocaleChanged -= RefreshAll;
            UnbindDataEvents();
        }

        private void OnDisable()
        {
            UnbindDataEvents();
        }
    }
}
