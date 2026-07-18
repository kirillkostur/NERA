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

        public IReadOnlyCollection<string> AnalyzedResearchIds => analyzedResearchIds;
        public ResearchState State { get; private set; }
        public ItemData LoadedItem { get; private set; }
        public float Progress { get; private set; }
        public string StatusMessage { get; private set; } = "Laboratory ready.";

        private PlayerInventory sourceInventory;
        private float analysisRemaining;
        private EnergySystemController registeredEnergySystem;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

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
            StatusMessage = $"Scanning {LoadedItem.DisplayName}...";
            analysisRemaining = Mathf.Max(0f, analysisRemaining - deltaTime);
            Progress = 1f - analysisRemaining / definition.AnalysisDuration;

            if (analysisRemaining <= 0f)
                CompleteAnalysis(definition);
        }

        public bool LoadItem(ItemData item, PlayerInventory inventory)
        {
            if (State == ResearchState.Analyzing || LoadedItem != null || item == null || inventory == null ||
                item.ItemType != ItemType.ResearchSample || item.ResearchDefinition == null || !inventory.RemoveItem(item))
            {
                return false;
            }

            LoadedItem = item;
            sourceInventory = inventory;
            ResearchDefinition definition = item.ResearchDefinition;
            bool alreadyAnalyzed = analyzedResearchIds.Contains(definition.ResearchId);
            Progress = alreadyAnalyzed ? 1f : 0f;
            StatusMessage = alreadyAnalyzed
                ? $"{item.DisplayName} has already been analyzed."
                : $"{item.DisplayName} loaded. Ready to scan.";
            SetState(alreadyAnalyzed ? ResearchState.Complete : ResearchState.ItemLoaded);
            return true;
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
            !IsAnalyzed(LoadedItem) &&
            HasOperationalPower;

        public bool StartAnalysis()
        {
            if (State != ResearchState.ItemLoaded || LoadedItem == null)
                return false;

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

        private void EnsureEnergyRegistration()
        {
            EnergySystemController energy = EnergySystemController.Instance;
            if (energy == null || registeredEnergySystem == energy)
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
            registeredEnergySystem = energy;
        }

        private void CompleteAnalysis(ResearchDefinition definition)
        {
            analyzedResearchIds.Add(definition.ResearchId);

            if (definition.UnlockedEntry != null && LibraryController.Instance != null)
                LibraryController.Instance.Unlock(definition.UnlockedEntry.EntryId);

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
