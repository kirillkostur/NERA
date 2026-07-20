using System;
using System.Collections.Generic;
using NERA.Library;
using NERA.Inventory;
using NERA.Items;
using NERA.Energy;
using NERA.Station;
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
        public event Action<string> ResearchAnalyzed;

        private readonly HashSet<string> analyzedResearchIds = new HashSet<string>();
        [SerializeField] private StationPowerController stationPower;
        [SerializeField] private LibraryController library;

        public IReadOnlyCollection<string> AnalyzedResearchIds => analyzedResearchIds;
        public ResearchState State { get; private set; }
        public ItemData LoadedItem { get; private set; }
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

            EnergySystemController energy = EnergySystemController.Instance;
            if (energy != null &&
                !energy.IsConsumerPowered(LaboratoryConsumerId))
            {
                StatusMessage = "Scanning paused — insufficient station energy.";
                return;
            }

            ResearchDefinition definition = LoadedItem.ResearchDefinition;
            if (definition == null)
            {
                StatusMessage = "This item does not require analysis.";
                SetState(ResearchState.ItemLoaded);
                EnergySystemController.Instance?.SetConsumerActive(
                    LaboratoryConsumerId,
                    false
                );
                return;
            }
            StatusMessage = $"Scanning {LoadedItem.DisplayName}...";
            analysisRemaining = Mathf.Max(0f, analysisRemaining - deltaTime);
            Progress = 1f - analysisRemaining / definition.AnalysisDuration;

            if (analysisRemaining <= 0f)
                CompleteAnalysis(definition);
        }

        public bool LoadItem(ItemData item, PlayerInventory inventory)
        {
            if (!CanLoadItem(item) || inventory == null)
                return false;

            InventorySlotGroup group = PlayerInventory.GetSlotGroup(item.ItemType);
            System.Collections.Generic.IReadOnlyList<ItemData> slots = group switch
            {
                InventorySlotGroup.Anomaly => inventory.AnomalySlots,
                InventorySlotGroup.QuickAccess => inventory.QuickAccessSlots,
                _ => inventory.BackpackSlots
            };

            for (int index = 0; index < slots.Count; index++)
            {
                if (slots[index] == item)
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

            if (State == ResearchState.Analyzing || item == null || inventory == null ||
                inventory.GetItem(sourceGroup, sourceIndex) != item)
            {
                return false;
            }

            ItemData previousLoadedItem = LoadedItem;
            if (previousLoadedItem != null &&
                PlayerInventory.GetSlotGroup(previousLoadedItem.ItemType) != sourceGroup)
            {
                return false;
            }

            if (!inventory.RemoveItemAt(sourceGroup, sourceIndex, out _))
                return false;

            if (previousLoadedItem != null &&
                !inventory.TrySetItemAt(sourceGroup, sourceIndex, previousLoadedItem))
            {
                inventory.TrySetItemAt(sourceGroup, sourceIndex, item);
                return false;
            }

            LoadedItem = item;
            sourceInventory = inventory;
            ResearchDefinition definition = item.ResearchDefinition;
            bool researchable = IsResearchable(item);
            bool alreadyAnalyzed = researchable &&
                analyzedResearchIds.Contains(definition.ResearchId);
            Progress = alreadyAnalyzed ? 1f : 0f;
            StatusMessage = !researchable
                ? item.DisplayName
                : alreadyAnalyzed
                ? $"{item.DisplayName} has already been analyzed."
                : $"{item.DisplayName} loaded. Ready to scan.";

            if (alreadyAnalyzed)
                UnlockLibraryEntry(definition);

            SetState(researchable && alreadyAnalyzed
                ? ResearchState.Complete
                : ResearchState.ItemLoaded);
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

            ItemData itemToMove = LoadedItem;
            if (PlayerInventory.GetSlotGroup(itemToMove.ItemType) != destinationGroup)
                return false;

            if (!inventory.TryReplaceItemAt(
                    destinationGroup,
                    destinationIndex,
                    itemToMove,
                    out ItemData replacedItem
                ))
            {
                return false;
            }

            LoadedItem = replacedItem;
            sourceInventory = replacedItem != null ? inventory : null;
            Progress = 0f;

            if (LoadedItem == null)
            {
                StatusMessage = "Laboratory ready.";
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
            IsResearchable(LoadedItem) &&
            !IsAnalyzed(LoadedItem) &&
            HasOperationalPower;

        public bool StartAnalysis()
        {
            if (State != ResearchState.ItemLoaded || LoadedItem == null)
                return false;

            if (!IsResearchable(LoadedItem))
            {
                StatusMessage = "This item is already identified and does not require analysis.";
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
                    StatusMessage = "Insufficient station energy.";
                    return false;
                }
            }
            else if (power == null || !power.IsPowered)
            {
                StatusMessage = "Insufficient station power.";
                return false;
            }

            ResearchDefinition definition = LoadedItem.ResearchDefinition;
            if (analyzedResearchIds.Contains(definition.ResearchId))
            {
                StatusMessage = "Sample has already been analyzed.";
                return false;
            }
            analysisRemaining = definition.AnalysisDuration;
            Progress = 0f;
            StatusMessage = $"Scanning {LoadedItem.DisplayName}...";
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

            energy.RegisterConsumer(
                LaboratoryConsumerId,
                energy.Config.LaboratoryConsumption,
                true
            );
            energy.SetConsumerActive(
                LaboratoryConsumerId,
                State == ResearchState.Analyzing
            );
        }

        private void CompleteAnalysis(ResearchDefinition definition)
        {
            analyzedResearchIds.Add(definition.ResearchId);
            UnlockLibraryEntry(definition);

            StatusMessage = $"Analysis complete: {definition.DisplayName}";
            string completedId = definition.ResearchId;
            analysisRemaining = 0f;
            Progress = 1f;
            SetState(ResearchState.Complete);
            EnergySystemController.Instance?.SetConsumerActive(
                LaboratoryConsumerId,
                false
            );

            ResearchAnalyzed?.Invoke(completedId);
            Debug.Log($"Research: analyzed '{completedId}'.", this);
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

        public bool RetrieveLoadedItem()
        {
            if (State == ResearchState.Analyzing || LoadedItem == null || sourceInventory == null)
                return false;

            if (!sourceInventory.AddItem(LoadedItem))
            {
                StatusMessage = "No free inventory slot for this sample.";
                return false;
            }

            LoadedItem = null;
            sourceInventory = null;
            Progress = 0f;
            StatusMessage = "Laboratory ready.";
            SetState(ResearchState.Idle);
            return true;
        }

        private void RefreshLoadedItemState()
        {
            ResearchDefinition definition = LoadedItem?.ResearchDefinition;
            bool researchable = IsResearchable(LoadedItem);
            bool alreadyAnalyzed = researchable &&
                analyzedResearchIds.Contains(definition.ResearchId);

            Progress = alreadyAnalyzed ? 1f : 0f;
            StatusMessage = !researchable
                ? LoadedItem.DisplayName
                : alreadyAnalyzed
                ? $"{LoadedItem.DisplayName} has already been analyzed."
                : $"{LoadedItem.DisplayName} loaded. Ready to scan.";

            if (alreadyAnalyzed)
                UnlockLibraryEntry(definition);

            SetState(researchable && alreadyAnalyzed
                ? ResearchState.Complete
                : ResearchState.ItemLoaded);
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
