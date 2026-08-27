using System;
using System.Collections.Generic;
using NERA.Library;
using NERA.Inventory;
using NERA.Items;
using NERA.Localization;
using NERA.Energy;
using NERA.Station;
using NERA.Quests;
using UnityEngine;

namespace NERA.Research
{
    public sealed class ResearchController : MonoBehaviour
    {
        private const string LaboratoryConsumerId = "laboratory_scan";
        public enum ResearchState
        {
            Idle,
            ItemLoaded,
            Analyzing,
            Complete
        }

        public static ResearchController Instance { get; private set; }

        public event Action<ResearchState> StateChanged;
        public event Action<float> ProgressChanged;
        public event Action<string> ResearchAnalyzed;

        private readonly HashSet<string> analyzedResearchIds = new HashSet<string>();
        [SerializeField] private StationPowerController stationPower;
        [SerializeField] private LibraryController library;

        public IReadOnlyCollection<string> AnalyzedResearchIds => analyzedResearchIds;
        public ResearchState State { get; private set; }
        public ItemInstance LoadedItemInstance { get; private set; }
        public ItemData LoadedItem => LoadedItemInstance?.ItemData;
        public float Progress { get; private set; }
        public string StatusMessage { get; private set; } = "Laboratory ready.";

        private PlayerInventory sourceInventory;
        private float analysisRemaining;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            StatusMessage = LocalizeStatus("ready", "Laboratory ready.");
            EnsureEnergyRegistration();
        }

        private void Start()
        {
            EnsureEnergyRegistration();
        }

        private void Update()
        {
            AdvanceAnalysis(Time.deltaTime);
        }

        public void AdvanceAnalysis(float deltaTime)
        {
            if (State != ResearchState.Analyzing || LoadedItem == null || deltaTime <= 0f)
                return;

            if (!IsSystemEnabled)
            {
                EnergySystemController.Instance?.SetConsumerActive(
                    LaboratoryConsumerId,
                    false);
                StatusMessage = LocalizeStatus(
                    "paused_stopped",
                    "Scanning paused — laboratory is stopped.");
                return;
            }

            EnergySystemController energy = EnergySystemController.Instance;
            if (energy != null &&
                !energy.IsConsumerPowered(LaboratoryConsumerId))
            {
                StatusMessage = LocalizeStatus(
                    "paused_energy",
                    "Scanning paused — insufficient station energy.");
                return;
            }

            ResearchDefinition definition = LoadedItem.ResearchDefinition;
            if (definition == null)
            {
                StatusMessage = LocalizeStatus(
                    "analysis_not_required",
                    "This item does not require analysis.");
                SetState(ResearchState.ItemLoaded);
                EnergySystemController.Instance?.SetConsumerActive(
                    LaboratoryConsumerId,
                    false
                );
                return;
            }
            StatusMessage = LocalizeStatus(
                "scanning_item",
                "Scanning {0}...",
                LoadedItem.DisplayName);
            analysisRemaining = Mathf.Max(0f, analysisRemaining - deltaTime);
            Progress = 1f - analysisRemaining / definition.AnalysisDuration;
            ProgressChanged?.Invoke(Progress);

            if (analysisRemaining <= 0f)
                CompleteAnalysis(definition);
        }

        public bool LoadItem(ItemData item, PlayerInventory inventory)
        {
            if (!CanLoadItem(item) || inventory == null)
                return false;

            InventorySlotGroup group = PlayerInventory.GetSlotGroup(item.ItemType);
            System.Collections.Generic.IReadOnlyList<ItemInstance> slots = group switch
            {
                InventorySlotGroup.Anomaly => inventory.AnomalyItemInstances,
                InventorySlotGroup.QuickAccess => inventory.QuickAccessItemInstances,
                _ => inventory.BackpackItemInstances
            };

            for (int index = 0; index < slots.Count; index++)
            {
                if (slots[index]?.ItemData == item)
                    return LoadItem(item, inventory, group, index);
            }

            return false;
        }

        public bool LoadItem(
            ItemData item,
            PlayerInventory inventory,
            InventorySlotGroup sourceGroup,
            int sourceIndex
        )
        {
            if (!CanLoadItem(item))
                return false;

            ItemInstance sourceInstance = inventory?.GetItemInstance(sourceGroup, sourceIndex);
            if (State == ResearchState.Analyzing || item == null || inventory == null ||
                sourceInstance?.ItemData != item)
            {
                return false;
            }

            ItemInstance previousLoadedInstance = LoadedItemInstance;
            ItemData previousLoadedItem = previousLoadedInstance?.ItemData;
            if (previousLoadedItem == null)
            {
                if (!inventory.RemoveInstanceAt(
                        sourceGroup,
                        sourceIndex,
                        out sourceInstance))
                {
                    return false;
                }
            }
            else
            {
                InventorySlotGroup previousGroup =
                    PlayerInventory.GetSlotGroup(previousLoadedItem.ItemType);
                if (previousGroup == sourceGroup)
                {
                    if (!inventory.TryReplaceInstanceAt(
                            sourceGroup,
                            sourceIndex,
                            previousLoadedInstance,
                            out sourceInstance))
                    {
                        return false;
                    }
                }
                else
                {
                    if (!inventory.RemoveInstanceAt(
                            sourceGroup,
                            sourceIndex,
                            out sourceInstance))
                    {
                        return false;
                    }

                    if (!inventory.AddItem(previousLoadedInstance))
                    {
                        inventory.TrySetInstanceAt(
                            sourceGroup,
                            sourceIndex,
                            sourceInstance);
                        return false;
                    }
                }
            }

            LoadedItemInstance = sourceInstance;
            sourceInventory = inventory;
            ResearchDefinition definition = item.ResearchDefinition;
            bool researchable = IsResearchable(item);
            bool typeKnown = researchable &&
                analyzedResearchIds.Contains(definition.ResearchId);
            bool instanceScanned = sourceInstance.IsScanned;
            Progress = 0f;
            StatusMessage = BuildLoadedItemStatus(
                item,
                researchable,
                instanceScanned,
                typeKnown);

            if (typeKnown)
                UnlockLibraryEntry(definition);

            SetState(ResearchState.ItemLoaded);
            return true;
        }

        public bool MoveLoadedItemToInventory(
            PlayerInventory inventory,
            InventorySlotGroup destinationGroup,
            int destinationIndex
        )
        {
            if (State == ResearchState.Analyzing || LoadedItem == null || inventory == null)
                return false;

            ItemInstance instanceToMove = LoadedItemInstance;
            ItemData itemToMove = instanceToMove.ItemData;
            if (PlayerInventory.GetSlotGroup(itemToMove.ItemType) != destinationGroup)
                return false;

            if (!inventory.TryReplaceInstanceAt(
                    destinationGroup,
                    destinationIndex,
                    instanceToMove,
                    out ItemInstance replacedInstance
                ))
            {
                return false;
            }

            LoadedItemInstance = replacedInstance;
            sourceInventory = replacedInstance != null ? inventory : null;
            Progress = 0f;

            if (LoadedItemInstance == null)
            {
                StatusMessage = LocalizeStatus("ready", "Laboratory ready.");
                SetState(ResearchState.Idle);
            }
            else
            {
                RefreshLoadedItemState();
            }

            return true;
        }

        public bool IsResearchable(ItemData item)
        {
            return item?.ResearchDefinition?.UnlockedEntry != null &&
                   !string.IsNullOrWhiteSpace(item.ResearchDefinition.ResearchId);
        }

        public bool CanLoadItem(ItemData item)
        {
            return item != null;
        }

        public bool IsAnalyzed(ItemData item)
        {
            return item != null &&
                   item.ResearchDefinition != null &&
                   analyzedResearchIds.Contains(item.ResearchDefinition.ResearchId);
        }

        public bool IsScanned(ItemInstance instance)
        {
            return instance?.IsScanned == true;
        }

        public bool HasOperationalPower
        {
            get
            {
                EnergySystemController energy = EnergySystemController.Instance;
                if (energy != null)
                {
                    EnsureEnergyRegistration();
                    return energy.CanPowerConsumer(LaboratoryConsumerId);
                }

                StationPowerController power = stationPower != null
                    ? stationPower
                    : StationPowerController.Instance;
                return power != null && power.IsPowered;
            }
        }

        public bool CanStartAnalysis =>
            State == ResearchState.ItemLoaded &&
            LoadedItem != null &&
            LoadedItemInstance.IsScanned == false &&
            IsSystemEnabled &&
            IsResearchable(LoadedItem) &&
            HasOperationalPower;

        public bool StartAnalysis()
        {
            if (State != ResearchState.ItemLoaded || LoadedItem == null)
                return false;

            if (!IsSystemEnabled)
            {
                StatusMessage = LocalizeStatus(
                    "stopped_from_terminal",
                    "Laboratory is stopped from the station terminal.");
                return false;
            }

            if (!IsResearchable(LoadedItem))
            {
                StatusMessage = LocalizeStatus(
                    "already_identified",
                    "This item is already identified and does not require analysis.");
                return false;
            }

            if (LoadedItemInstance.IsScanned)
            {
                StatusMessage = LocalizeStatus(
                    "already_scanned",
                    "{0} is already scanned.",
                    LoadedItem.DisplayName);
                return false;
            }

            StationPowerController power = stationPower != null
                ? stationPower
                : StationPowerController.Instance;
            EnergySystemController energy = EnergySystemController.Instance;

            if (energy != null)
            {
                EnsureEnergyRegistration();
                energy.SetConsumerActive(LaboratoryConsumerId, true);

                if (!energy.IsConsumerPowered(LaboratoryConsumerId))
                {
                    energy.SetConsumerActive(LaboratoryConsumerId, false);
                    StatusMessage = LocalizeStatus(
                        "insufficient_energy",
                        "Insufficient station energy.");
                    return false;
                }
            }
            else if (power == null || !power.IsPowered)
            {
                StatusMessage = LocalizeStatus(
                    "insufficient_power",
                    "Insufficient station power.");
                return false;
            }

            ResearchDefinition definition = LoadedItem.ResearchDefinition;
            analysisRemaining = definition.AnalysisDuration;
            Progress = 0f;
            StatusMessage = LocalizeStatus(
                "scanning_item",
                "Scanning {0}...",
                LoadedItem.DisplayName);
            SetState(ResearchState.Analyzing);
            return true;
        }

        public void SetPowerSource(StationPowerController powerSource)
        {
            stationPower = powerSource;
        }

        public void SetLibrary(LibraryController libraryController)
        {
            library = libraryController;
        }

        private void EnsureEnergyRegistration()
        {
            EnergySystemController energy = EnergySystemController.Instance;
            if (energy == null)
                return;

            float consumption = StationSystemsConfig.GetEffectiveStat(
                StationSystemType.Laboratory,
                string.Empty,
                StationObjectStat.IdleEnergyConsumption,
                4f);
            energy.RegisterConsumer(
                LaboratoryConsumerId,
                consumption,
                energy.Config.GetMinimumCharge01(
                    StationSystemType.Laboratory),
                StationSystemType.Laboratory
            );
            energy.SetConsumerActive(
                LaboratoryConsumerId,
                State == ResearchState.Analyzing
            );
        }

        private bool IsSystemEnabled =>
            StationSystemsController.Instance == null ||
            StationSystemsController.Instance.IsRequestedActive(
                StationSystemType.Laboratory);

        private void CompleteAnalysis(ResearchDefinition definition)
        {
            if (LoadedItemInstance == null ||
                !LoadedItemInstance.MarkScanned())
            {
                StatusMessage = LocalizeStatus(
                    "sample_already_scanned",
                    "This sample is already scanned.");
                analysisRemaining = 0f;
                Progress = 1f;
                SetState(ResearchState.Complete);
                EnergySystemController.Instance?.SetConsumerActive(
                    LaboratoryConsumerId,
                    false);
                return;
            }

            bool firstAnalysis =
                analyzedResearchIds.Add(definition.ResearchId);
            UnlockLibraryEntry(definition);

            StatusMessage = firstAnalysis
                ? LocalizeStatus(
                    "analysis_complete",
                    "Analysis complete: {0}",
                    definition.DisplayName)
                : LocalizeStatus(
                    "sample_complete",
                    "Sample scan complete: {0}",
                    definition.DisplayName);
            string completedId = definition.ResearchId;
            analysisRemaining = 0f;
            Progress = 1f;
            SetState(ResearchState.Complete);
            EnergySystemController.Instance?.SetConsumerActive(
                LaboratoryConsumerId,
                false
            );

            if (firstAnalysis)
            {
                ResearchAnalyzed?.Invoke(completedId);
                QuestController.Instance?.Report(
                    QuestSignalType.ResearchAnalyzed,
                    completedId,
                    definition.DisplayName);
            }
            Debug.Log(
                firstAnalysis
                    ? $"Research: analyzed '{completedId}'."
                    : $"Research: scanned another '{completedId}' sample.",
                this);
        }

        private void UnlockLibraryEntry(ResearchDefinition definition)
        {
            if (definition?.UnlockedEntry == null)
                return;

            LibraryController targetLibrary = library != null
                ? library
                : LibraryController.Instance;
            if (targetLibrary == null)
                return;

            targetLibrary.Unlock(definition.UnlockedEntry);
        }

        public void RestoreAnalyzed(IEnumerable<string> researchIds)
        {
            analyzedResearchIds.Clear();

            if (researchIds == null)
                return;

            foreach (string researchId in researchIds)
            {
                if (!string.IsNullOrWhiteSpace(researchId))
                    analyzedResearchIds.Add(researchId);
            }
        }

        public void RestoreLoadedItem(ItemInstance instance, PlayerInventory inventory)
        {
            EnergySystemController.Instance?.SetConsumerActive(
                LaboratoryConsumerId,
                false
            );
            LoadedItemInstance = instance?.ItemData != null ? instance : null;
            sourceInventory = LoadedItemInstance != null ? inventory : null;
            analysisRemaining = 0f;
            Progress = 0f;

            if (LoadedItemInstance == null)
            {
                StatusMessage = LocalizeStatus("ready", "Laboratory ready.");
                SetState(ResearchState.Idle);
                return;
            }

            RefreshLoadedItemState();
        }

        public bool RetrieveLoadedItem()
        {
            if (State == ResearchState.Analyzing || LoadedItem == null || sourceInventory == null)
                return false;

            if (!sourceInventory.AddItem(LoadedItemInstance))
            {
                StatusMessage = LocalizeStatus(
                    "no_inventory_slot",
                    "No free inventory slot for this sample.");
                return false;
            }

            LoadedItemInstance = null;
            sourceInventory = null;
            Progress = 0f;
            StatusMessage = LocalizeStatus("ready", "Laboratory ready.");
            SetState(ResearchState.Idle);
            return true;
        }

        private void RefreshLoadedItemState()
        {
            ResearchDefinition definition = LoadedItem?.ResearchDefinition;
            bool researchable = IsResearchable(LoadedItem);
            bool typeKnown = researchable &&
                analyzedResearchIds.Contains(definition.ResearchId);
            bool instanceScanned = LoadedItemInstance?.IsScanned == true;

            Progress = 0f;
            StatusMessage = BuildLoadedItemStatus(
                LoadedItem,
                researchable,
                instanceScanned,
                typeKnown);

            if (typeKnown)
                UnlockLibraryEntry(definition);

            SetState(ResearchState.ItemLoaded);
        }

        private static string BuildLoadedItemStatus(
            ItemData item,
            bool researchable,
            bool instanceScanned,
            bool typeKnown)
        {
            if (item == null)
                return string.Empty;
            if (!researchable)
                return item.DisplayName;
            if (instanceScanned)
            {
                return LocalizeStatus(
                    "already_scanned",
                    "{0} is already scanned.",
                    item.DisplayName);
            }
            if (typeKnown)
            {
                return LocalizeStatus(
                    "known_type_requires_scan",
                    "{0} type is known. This sample still requires scanning.",
                    item.DisplayName);
            }

            return LocalizeStatus(
                "loaded_ready",
                "{0} loaded. Ready to scan.",
                item.DisplayName);
        }

        private static string LocalizeStatus(
            string key,
            string fallback,
            params object[] arguments)
        {
            return NERALocalization.Get(
                NERALocalization.InventoryLaboratoryTable,
                $"laboratory.status.{key}",
                fallback,
                arguments);
        }

        private void SetState(ResearchState newState)
        {
            State = newState;
            StateChanged?.Invoke(State);
        }

        private void OnDestroy()
        {
            EnergySystemController.Instance?.SetConsumerActive(
                LaboratoryConsumerId,
                false
            );

            if (Instance == this)
                Instance = null;
        }
    }
}
